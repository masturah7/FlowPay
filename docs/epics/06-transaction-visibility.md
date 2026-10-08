# Epic 6: Transaction Visibility & Notifications

Maps to the PRD capabilities: "View transaction history," "Receive
transaction notifications," "Track pending, successful, failed, and
reversed transactions."

## Summary

Letting a customer see what happened to their money after Epic 4 (Wallets)
and Epic 5 (Transfers) move it. Most of the underlying data already
exists — `WalletLedgerEntry` rows from Epic 4's funding, `Transfer` rows
with their `Status` from Epic 5 — this epic is mostly about exposing it
through read endpoints, plus building `FlowPay.Notifications` out for real
for the first time (it's been a stateless Ping scaffold since the start).

## Decisions

**Transaction history is per-service, not a new aggregation layer.**
`GET /wallets/{id}/transactions` reads `FlowPay.Wallet`'s own
`WalletLedgerEntry` rows; `GET /transfers` / `GET /transfers/{id}` read
`FlowPay.Transfers`' own `Transfer` rows. No new read-model/aggregation
service — the data already lives in the right place, it just needs an
endpoint.

**`GET /transfers` (mine) only covers transfers the caller *sent*.**
`Transfer.AccountId` is the sender; matching "transfers I received" would
need either a denormalized recipient-account-id on `Transfer` (Transfers
doesn't currently know the recipient's *account* id, only the wallet id)
or a cross-service lookup. Deferred — a recipient can already see the
credit land via their own wallet's transaction history
(`GET /wallets/{id}/transactions`), just not labeled "transfer" from their
side. Tracked as an open question below.

**Notifications fire only on transfer outcomes, not on funding.** Funding
is synchronous and self-initiated — the caller already gets the result in
the HTTP response. A transfer affects *two* parties, and the recipient in
particular benefits from being told something happened without having
polled for it. `FlowPay.Notifications` gets real a database and a
`Notification` entity for the first time; `FlowPay.Transfers` calls its
internal endpoint after a transfer resolves:
- `Completed` → notify both sender and recipient
- `Failed` → notify sender only (recipient was never involved)
- `PendingReconciliation` → notify sender only

**Notifications are best-effort, not part of the transfer's correctness.**
The notification call happens *after* `TransferService` has already
decided and persisted the transfer's final status. If it fails (timeout,
`FlowPay.Notifications` down), that's logged and swallowed — it must never
change the transfer's recorded outcome or bubble up as an error to the
caller. This is the one HTTP call in the whole system that is deliberately
allowed to fail silently from the caller's point of view.

**In-app only — no email/SMS/push.** `Notification` rows are read via an
authenticated `GET`. Real delivery channels are explicitly out of scope
(open question below) — this is the mechanism those would eventually hang
off of, not a replacement for deciding on them.

## User stories

### US-6.1: View wallet transaction history

As an authenticated customer, I want to see the history of balance changes
on my wallet, so that I can see where money came from and went.

**Acceptance criteria**
- [x] `GET /wallets/api/v1/wallets/{id}/transactions` (authenticated, must
      own the wallet — same 404-for-both-cases rule as other wallet
      endpoints) returns the wallet's ledger entries, newest first
- [x] Each entry shows direction (credit/debit), amount, currency, the
      resulting balance, and when it happened

### US-6.2: View my transfers and their status

As an authenticated customer, I want to list the transfers I've sent and
check an individual transfer's status, so that I can track pending,
successful, failed, and reversed transfers.

**Acceptance criteria**
- [x] `GET /transfers/api/v1/transfers` (authenticated) lists transfers the
      caller sent, newest first, each with its current `Status`
- [x] `GET /transfers/api/v1/transfers/{id}` (authenticated, must be the
      sender) returns one transfer's full detail including
      `FailureReason` when present

### US-6.3: Receive a notification when a transfer resolves

As an authenticated customer, I want to be notified when a transfer I sent
or received completes, fails, or needs reconciliation, so that I don't
have to poll for the outcome.

**Acceptance criteria**
- [x] On `Completed`, both sender and recipient get a notification
- [x] On `Failed` or `PendingReconciliation`, the sender gets a
      notification
- [x] `GET /notifications/api/v1/notifications` (authenticated) lists the
      caller's own notifications, newest first
- [x] `POST /notifications/api/v1/notifications/{id}/read` (authenticated,
      must own it) marks a notification read
- [x] A notification-delivery failure never changes a transfer's recorded
      outcome or surfaces as an error to the transfer's caller

**Status:** Implemented in `services/FlowPay.Notifications` (new —
`Notification` entity, public + internal controllers),
`services/FlowPay.Transfers` (`INotificationApiClient`, called from
`TransferService` after the transfer resolves), `services/FlowPay.Wallet`
(new transactions endpoint).

**Out of scope for this story**: email/SMS/push delivery channels,
notifications on funding, "transfers I received" as a labeled list (see
Decisions above), marking all notifications read in bulk, pagination (all
list endpoints return everything — fine at this data volume, a real
concern once accounts accumulate thousands of rows).

## Open questions

- Notification channels (push, email, SMS) and which events trigger which
  channel — still entirely undecided; in-app rows are the only mechanism
  today.
- Should "transfers I received" be a first-class list, and if so, does
  that need a recipient-account-id on `Transfer`, or a join against
  `FlowPay.Wallet`?
- Pagination/cursoring on all the new list endpoints, once data volume
  makes "return everything" impractical.
