# Epic 5: Transfers

Maps to the PRD capabilities: "Transfer money to other FlowPay users,"
"Transfer money to external bank accounts," "Save beneficiaries," "Pay
transaction fees," "Operate within configured transaction limits."

## Summary

Moving money out of a wallet (Epic 4): to another FlowPay user, to an
external bank account, or to a saved beneficiary — subject to fees and
transaction limits. This is the epic the PRD's financial-integrity section
is really about: every transfer must answer "where did the money come
from," "where did it go," and "can we prove it," and every failure mode
must answer "did money move," "can this be retried," "does anything need
reconciliation."

This first story (US-5.1) scopes down to the simplest real case — an
internal transfer between two FlowPay wallets, identified by wallet id (no
recipient lookup by email/username yet, no fees, no limits, no external
bank transfers). Getting *this* right — real ledger, real idempotency, real
accounting for partial failure — is the hard part; the rest (US-5.2+) is
mostly additional validation layered on the same mechanism.

## Decisions

**`FlowPay.Ledger` gets built out for real, now.** Epic 4 deferred this
deliberately ("becomes load-bearing the moment Epic 5 needs to move money
*between* wallets") — this is that moment. `FlowPay.Ledger` owns
`LedgerEntry` rows (immutable, double-entry: every transfer writes exactly
one debit + one credit, in the same transaction, idempotent on
`(WalletId, IdempotencyKey)`). It does **not** validate business rules
(ownership, sufficient funds, limits) — that's `FlowPay.Transfers`' job.
Ledger's job is to atomically and immutably record what Transfers tells it
happened, and to answer "what's this wallet's derived balance" from first
principles (sum of credits minus debits) for future reconciliation.

**Wallet gains internal-only debit/credit endpoints.** The existing
`POST /wallets/{id}/fund` is end-user-facing and ownership-checked (you can
only fund your own wallet). Crediting a *transfer recipient's* wallet is
fundamentally different: the recipient didn't authorize it, Transfers did
(on the sender's behalf, having already verified the sender owns the
source wallet). So `FlowPay.Wallet` gets a second, internal-only surface —
`POST /api/v1/internal/wallets/{id}/credit` and `.../debit` — protected by
a shared internal API key (config-based, same pattern as the shared JWT
signing key), not by end-user `[Authorize]`, and not routed through the
gateway. These skip the ownership check entirely and trust the caller
(Transfers) to have already decided the operation is authorized.

**Order of operations, and what happens when it partially fails:**
1. Create a `Transfer` row in `FlowPay.Transfers`' own DB immediately,
   status `Pending` — every attempt is recorded, even ones that go on to
   fail validation, so nothing is ever silently lost from the audit trail.
2. Fetch both wallets (via Wallet's normal authenticated API, forwarding
   the caller's token) to check: sender wallet belongs to the caller,
   both wallets exist, currencies match the transfer currency. Fail fast,
   no side effects, if any of this is wrong.
3. Debit the sender's wallet (internal endpoint — authoritative
   sufficient-funds + concurrency check happens here, atomically). If this
   fails, mark the Transfer `Failed` with a reason. Nothing else has
   happened; safe.
4. Record the double-entry in `FlowPay.Ledger`. The debit already
   happened, so this truthfully reflects it.
5. Credit the recipient's wallet (internal endpoint). **This is the one
   step that can fail after money has already left the sender** (network
   blip, Wallet momentarily down) — there's no saga/outbox/2PC
   infrastructure in this story to eliminate that window. If it fails, the
   Transfer is marked `PendingReconciliation` (not `Failed` — the debit and
   ledger entry are real) and the response says so explicitly, rather than
   returning a bare 500 or pretending to be a clean success.
6. If 3–5 all succeed, the Transfer is `Completed`.

**No automatic compensating reversal.** If step 5 fails, auto-reversing the
debit is tempting but risky without more infrastructure (what if the
reversal itself fails?). `PendingReconciliation` is a named, visible state
instead — the honest answer to "does anything need reconciliation" is
"yes, and here's exactly which transfer," not a silent retry that might
make things worse.

**Internal API key, not mTLS/service identity.** Service-to-service calls
(`Transfers` → `Wallet`, `Transfers` → `Ledger`) are authenticated with a
shared secret header, the simplest mechanism that still isn't "wide open."
Real service identity (mTLS, SPIFFE, per-service credentials) is out of
scope for this stage — flagged as a known gap, not a silent one.

## User stories

### US-5.1: Transfer money to another FlowPay user

As an authenticated customer, I want to send money from my wallet to
another FlowPay wallet, so that I can pay or send money to another user.

**Acceptance criteria**
- [x] `POST /transfers/api/v1/transfers` (authenticated) accepts
      `fromWalletId`, `toWalletId`, `amountMinorUnits`, `currency`, and an
      `Idempotency-Key` header
- [x] The caller must own `fromWalletId` — otherwise `404` (don't confirm
      a wallet exists that isn't theirs, consistent with Epic 4)
- [x] Both wallets must exist and both must be in the transfer's currency
      — otherwise `400`/`404` as appropriate
- [x] Insufficient funds in the source wallet → `400`, no partial effects
- [x] A successful transfer debits the sender, credits the recipient, and
      records a matching debit+credit pair in `FlowPay.Ledger` — all
      reachable from the `Transfer` record
- [x] Retrying the same `Idempotency-Key` returns the same outcome without
      moving money twice
- [x] If the recipient-credit step fails after the sender has already been
      debited, the transfer is reported as `PendingReconciliation`, not a
      silent success or a bare error
- [x] Every transfer attempt (including failed ones) is queryable later —
      nothing about a failed attempt is thrown away

**Status:** Implemented across `services/FlowPay.Ledger` (new),
`services/FlowPay.Wallet` (internal credit/debit endpoints added),
`services/FlowPay.Transfers` (new — `TransferService` orchestrates).

**Out of scope for this story** (tracked separately): recipient lookup by
email/username (wallet id only, for now), fees, transaction limits,
external bank transfers, beneficiaries, automatic reconciliation of
`PendingReconciliation` transfers (that state is surfaced, not yet
resolved automatically), currency conversion.

## Open questions

- Transfer limits (per-transaction, daily, by KYC status), fee schedule.
- How does a `PendingReconciliation` transfer actually get resolved? A
  background job retrying the credit step is the obvious next step, not
  built yet.
- Recipient resolution (email/username → wallet id) for a real product —
  this story requires the sender to already know the recipient's wallet id.
