# Epic 4: Wallets

Maps to the PRD capabilities: "Create and manage wallets," "Hold supported
currencies," "Fund wallets."

## Summary

Giving a registered (Epic 1), authenticated (Epic 2) customer somewhere to
hold money: one wallet per currency, and a way to add funds to it.

## Decisions

**KYC gating:** Epic 3 (Identity Verification) is still unscoped — no KYC
provider is decided. Rather than block Wallets on that, this epic does
**not** gate wallet creation or funding on `Verified` status; any account in
`PendingVerification` or `Verified` status can create and fund wallets.
Revisit this the moment Epic 3 is scoped — gating funding (at least above
some threshold) on verified identity is a normal compliance expectation for
a real fintech, this is a deliberate "not yet," not a permanent "no."

**Funding is not yet backed by `FlowPay.Ledger`:** the architecture
(`docs/architecture.md`) states wallet balances should be a projection of
the ledger, with `FlowPay.Ledger` as the cross-service source of truth.
That service doesn't exist beyond a scaffold yet, and building it out is a
bigger unit of work than this epic. Instead, `FlowPay.Wallet` keeps its own
internal, append-only `WalletLedgerEntry` table — every balance change is a
new row, never an in-place update-only — which gives real auditability for
a single wallet today without the cross-service plumbing. This is an
interim design to replace, not a permanent substitute for `FlowPay.Ledger`;
it becomes load-bearing the moment Epic 5 (Transfers) needs to move money
*between* wallets, which genuinely needs a cross-wallet, cross-service
ledger.

**One wallet per currency per account:** creating a second wallet in a
currency the account already holds is rejected, rather than silently
creating an ambiguous second balance.

## User stories

### US-4.1: Create a wallet

As an authenticated customer, I want to create a wallet in a supported
currency, so that I have somewhere to hold money.

**Acceptance criteria**
- [x] `POST /wallets/api/v1/wallets` (authenticated) accepts a `currency`
      (ISO 4217 code) and creates a wallet owned by the caller, with a zero
      starting balance
- [x] An account cannot have two wallets in the same currency — a repeat
      attempt returns `409 Conflict`
- [x] Returns `201 Created` with the new wallet's id, owner, currency, and
      balance

### US-4.2: View my wallets

As an authenticated customer, I want to see my wallet(s) and their
balances, so that I know how much money I have.

**Acceptance criteria**
- [x] `GET /wallets/api/v1/wallets` (authenticated) returns only the
      caller's own wallets
- [x] `GET /wallets/api/v1/wallets/{id}` returns a single wallet — `404` if
      it doesn't exist *or* belongs to someone else (don't confirm another
      account's wallet exists)

### US-4.3: Fund a wallet

As an authenticated customer, I want to add money to my wallet, so that I
have a balance to spend or transfer later.

**Acceptance criteria**
- [x] `POST /wallets/api/v1/wallets/{id}/fund` (authenticated, must own the
      wallet) accepts an amount (minor units, as a positive integer) and
      currency, and requires an `Idempotency-Key` header
- [x] The funding currency must match the wallet's currency — mismatch is
      `400 Bad Request`
- [x] Amount must be a positive integer — zero or negative is
      `400 Bad Request`
- [x] Retrying the same `Idempotency-Key` against the same wallet returns
      the same outcome without crediting the wallet twice
- [x] Reusing the same `Idempotency-Key` with a *different* amount is
      `409 Conflict` (the key means "this exact request," not "any request
      from this client")
- [x] Every funding event is recorded as an append-only `WalletLedgerEntry`
      (never an in-place balance edit with no trail) and the wallet's
      balance is updated in the same database transaction
- [x] Returns `200 OK` with the wallet's updated balance (not `201` — this
      endpoint doesn't yet expose an addressable "ledger entry" resource to
      point a `Location` header at; see Epic 6 for transaction history)

**Status:** Implemented in `services/FlowPay.Wallet`
(`WalletsController` + `WalletService` + `IWalletRepository` +
`WalletDbContext`, EF Core migration `InitialCreate`).

**Out of scope for this story** (tracked separately): funding *sources*
(card, bank transfer, etc. — this story funds a wallet unconditionally,
as if funds already arrived), withdrawals, cross-wallet movement (Epic 5),
transaction history beyond the current balance (Epic 6).

## Open questions

- Funding sources (card/bank/etc.) and whatever verification *those*
  require — this epic assumes funds have already arrived by some
  out-of-scope mechanism.
- When Epic 3 (KYC) is scoped, decide whether wallet creation and/or
  funding (perhaps above a threshold) should gate on `Verified` status.
- When does `FlowPay.Wallet`'s internal ledger get replaced by real calls
  to `FlowPay.Ledger`? Likely forced by Epic 5, which needs a ledger that
  spans more than one wallet.
