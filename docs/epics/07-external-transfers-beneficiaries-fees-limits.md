# Epic 7: External Transfers, Beneficiaries, Fees & Limits

Maps to the PRD capabilities explicitly deferred out of Epic 5 (see
`docs/epics/05-transfers.md`, "Out of scope for this story" and "Open
questions"): "Transfer money to external bank accounts," "Save
beneficiaries," "Pay transaction fees," "Operate within configured
transaction limits."

## Summary

Epic 5 built the hard part (real ledger, real idempotency, real partial-
failure accounting) for the simplest case: wallet-to-wallet transfers with
no fees or limits. This epic adds the remaining transfer-related PRD
capabilities on top of that same mechanism, plus a prerequisite: saved
beneficiaries, so a transfer can target a nickname instead of a raw wallet
id or raw bank details every time.

## Decisions

**No real banking/payment-rail partner is integrated.** There's no actual
way to move money out of FlowPay to a real bank account in this project.
Asked the user how to handle this (same situation as Epic 3's KYC-vendor
question); chosen approach: **simulate it honestly**. An external bank
transfer still debits the sender's wallet, charges a fee, and records a
genuine double-entry ledger pair — but it lands in a new terminal status,
`SubmittedExternally`, distinct from `Completed`, so the system never
claims money actually arrived at a real bank. `SubmittedExternally` is the
honest answer to "where did the money go": into FlowPay's own
external-settlement holding wallet, not out of FlowPay.

**Fees and limits reuse the existing debit/credit/ledger primitives — they
are not special-cased.** A fee is just a second, smaller transfer from the
sender to a reserved system wallet (`FeeRevenue`), using the same
debit → ledger-record → credit sequence as any other transfer, with its own
derived idempotency key and ledger `TransferReference` (so it doesn't
collide with the main transfer's debit/credit pair, preserving
`LedgerEntry`'s "exactly one debit + one credit per reference" invariant).
External transfers similarly credit a reserved system wallet
(`ExternalSettlement`) instead of a recipient's wallet.

**Reserved system wallets, not synthetic ledger-only identifiers.** Fee
revenue and pending-external-settlement balances are modeled as real
`FlowPay.Wallet` wallets owned by well-known reserved account ids
(`SystemAccountIds.FeeRevenue`, `SystemAccountIds.ExternalSettlement` —
`FlowPay.BuildingBlocks`), auto-provisioned per currency on first use via a
new internal `POST /api/v1/internal/wallets/system` endpoint
(get-or-create, reusing `WalletService.CreateAsync`'s existing
unique-constraint race handling). This keeps every credit/debit in the
system going through the same real, balance-tracked, auditable mechanism —
no special "this credit doesn't really count" path. Ops can read these
wallets' transaction history the same way as any customer wallet's.

**Fees apply only to external transfers.** Internal (FlowPay-to-FlowPay)
transfers stay fee-free, matching Epic 5/6's existing behavior and tests.
This is a scoping choice, not a technical constraint — documented here so
it's an explicit decision, not an accident.

**Limits apply to both internal and external transfers**, as a general
safety control: a flat (not currency-aware) per-transaction cap and a daily
cumulative cap on money sent, both from config
(`TransferPolicy` section). Checked early in `ExecuteAsync`, after the
idempotency-replay check (established rule: replay checks before business
rules) but before any wallet mutation. The daily cap counts amounts from
transfers that actually moved money (`Completed`, `SubmittedExternally`,
`PendingReconciliation`) — not `Failed` ones, and not fees (limits bound
how much a user *sends*, not what they pay to send it).

**Insufficient-funds is pre-checked against amount + fee together**, before
any debit call, specifically to avoid a scenario where the main transfer
debit succeeds but the fee debit then fails for a reason that was
knowable upfront. `WalletService.DebitAsync`'s own balance check remains
the authoritative, race-safe guard (the pre-check can't fully close a
concurrent-spend race) — if the fee debit still fails after the main
transfer already succeeded, that's handled as `PendingReconciliation`,
same as any other post-point-of-no-return failure.

**Bank account numbers are masked in API responses** (e.g. `****6789`)
everywhere except the beneficiary's creation response, where the caller
needs to see what they just entered. This is a reasonable default for a
fintech app's API surface even though, since these endpoints are all
ownership-checked, it isn't closing a cross-tenant leak.

## User stories

### US-7.1: Save beneficiaries

As an authenticated customer, I want to save a transfer destination (another
FlowPay wallet, or an external bank account) under a label, so I don't have
to re-enter raw wallet ids or bank details every time.

**Acceptance criteria**
- [x] `POST /transfers/api/v1/beneficiaries` accepts `label`, `type`
      (`InternalWallet` | `ExternalBank`), and either `walletId` (internal)
      or `bankName` + `bankAccountNumber` (external)
- [x] An `InternalWallet` beneficiary's `walletId` must exist (validated via
      Wallet's internal lookup) — it does **not** need to belong to the
      caller; beneficiaries are typically *other* people's wallets
- [x] `GET /transfers/api/v1/beneficiaries` lists the caller's own
      beneficiaries; `GET .../{id}` and `DELETE .../{id}` are
      ownership-checked (404 if not the caller's)
- [x] Bank account numbers are returned masked except in the create response

**Status:** Implemented in `services/FlowPay.Transfers` (`Domain/Beneficiary.cs`,
`Data/BeneficiaryRepository.cs`, `Features/Beneficiaries/*`,
`Controllers/BeneficiariesController.cs`).

**Out of scope:** editing a saved beneficiary (delete + re-add instead),
verifying an external bank account actually exists (no real rail to check
against).

### US-7.2: Operate within configured transaction limits

As the platform, I want to cap how much any single transfer can move and how
much an account can send per day, so exposure from a compromised account or
a fat-fingered amount is bounded.

**Acceptance criteria**
- [x] A transfer whose amount exceeds `TransferPolicy:MaxPerTransactionMinorUnits`
      is rejected (`400`, `PerTransactionLimitExceeded`) before any wallet
      call
- [x] A transfer that would push the sender's total sent-today (across
      `Completed`/`SubmittedExternally`/`PendingReconciliation` transfers)
      over `TransferPolicy:MaxDailyMinorUnits` is rejected (`400`,
      `DailyLimitExceeded`)
- [x] Both checks apply to internal and external transfers alike

**Status:** Implemented in `TransferService.ExecuteAsync` +
`TransferRepository.GetSentAmountSinceAsync`.

**Out of scope:** per-KYC-status limit tiers (flagged in Epic 3/5 as an open
question — still open; would need Transfers to query Identity for current
account status). Limits are flat long values, not currency-aware.

### US-7.3: Pay transaction fees

As the platform, I want to charge a fee on external bank transfers, so
operating the external rail (simulated or real) isn't free to run.

**Acceptance criteria**
- [x] An external transfer charges `TransferPolicy:ExternalTransferFeeMinorUnits`
      in addition to the transfer amount, debited from the same sender
      wallet
- [x] The fee is recorded as its own ledger debit/credit pair (sender →
      `FeeRevenue` system wallet), distinct from the main transfer's pair
- [x] The transfer response surfaces `feeMinorUnits` so the fee is never
      hidden from the sender
- [x] Internal transfers are unaffected (fee stays `0`)

**Status:** Implemented in `TransferService.ExecuteAsync`.

### US-7.4: Transfer money to an external bank account

As an authenticated customer, I want to send money from my wallet to an
external bank account (direct details or a saved beneficiary), so I can move
money out of FlowPay.

**Acceptance criteria**
- [x] `POST /transfers/api/v1/transfers` accepts `toBeneficiaryId` as an
      alternative to `toWalletId` — exactly one of the two must be set
- [x] Resolving an `ExternalBank` beneficiary routes the transfer down the
      external path: fee applied, limits checked, sender debited, ledger
      recorded against the `ExternalSettlement` system wallet
- [x] A successful external transfer reaches `SubmittedExternally`, never
      `Completed` — the system never claims money arrived at a real bank
- [x] Resolving an `InternalWallet` beneficiary behaves exactly like today's
      direct `toWalletId` transfer (no fee, reaches `Completed`)
- [x] The sender gets a notification on `SubmittedExternally` (reusing
      `TransferSent`, worded to reflect "submitted," not "received") — no
      recipient notification, since there is no FlowPay recipient account

**Status:** Implemented in `TransferService.ExecuteAsync`,
`services/FlowPay.Wallet` (new internal system-wallet endpoint),
`shared/FlowPay.BuildingBlocks/SystemAccountIds.cs` (new).

## Open questions

- How does a real external-settlement rail eventually get integrated, and
  what happens to money sitting in the `ExternalSettlement` system wallet
  (it currently just accumulates — nothing ever "pays it out" for real)?
- Per-KYC-status transaction limit tiers (still open from Epic 3/5).
- Currency-aware fee schedule and limits (currently flat minor-unit values
  applied regardless of currency).
- Recipient lookup by email/username for internal transfers (still open
  from Epic 5) — beneficiaries partially address this for *repeat*
  transfers, but adding a new one still requires knowing the raw wallet id.
