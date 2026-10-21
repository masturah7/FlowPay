# Epic 9: Admin/Ops Tooling

Not a PRD capability by itself — this closes a gap several earlier epics
created and then left open: KYC review, system-wallet balances, and stuck
transfers were all only reachable via the internal API key or direct DB
queries, never by a human operator with their own credentials through the
gateway.

## Summary

Every FlowPay-issued JWT has carried just enough to answer "who is this"
(`sub`, `email`). This epic adds "and are they allowed to act as an
operator" — a minimal admin role, a shared authorization policy any service
can check, and three admin-facing surfaces built on top of services that
already existed: KYC review (Epic 3), system wallet balances (Epic 7), and
reconciliation (Epic 8).

## Decisions

**Admin is a claim, checked by a shared policy — not a separate login
system.** `FlowPay.Identity` adds `Account.IsAdmin` (bool) and, when true,
`JwtTokenGenerator` includes an `is_admin: true` claim. `AddFlowPayJwtBearer`
(called identically by every service) registers an `"Admin"` authorization
policy requiring that claim — so `[Authorize(Policy = "Admin")]` works the
same way in `FlowPay.Wallet` and `FlowPay.Transfers` as it does in
`FlowPay.Identity`, with no per-service admin-check reimplementation.

**Bootstrapping the first admin is a config allowlist, not a promotion
flow.** There's no "make this account an admin" endpoint — that would need
its own authorization story (who can promote whom?) that's out of scope
here. Instead, `AdminBootstrap:Emails` in `FlowPay.Identity`'s config lists
emails that get `IsAdmin = true` automatically at registration. Flagged
plainly: this means admin status is fixed at registration time, there's no
way to revoke or grant it afterward without a DB edit, and like every
claim-based check in this system, a 60-minute-lifetime token issued before
a (hypothetical, DB-edited) admin change stays stale until it expires —
same accepted tradeoff as KYC status claims elsewhere in this codebase.

**Admin surfaces are additive, not replacements.** `FlowPay.Identity` keeps
`InternalKycController` (service-to-service, internal API key) exactly as
it was, and gains a new `AdminKycController` (human operator, JWT + Admin
policy) with the same three actions. Two different trust boundaries calling
the same `IKycService` — not one controller with mixed authorization modes.

**Reconciliation gets a manual escape hatch.** `AdminTransfersController`'s
force-retry action works on both `PendingReconciliation` (resume immediately
instead of waiting for the next sweep) and `ReconciliationFailed` (reset
`ReconciliationAttempts` to 0 and status to `PendingReconciliation` first,
then resume) — reusing `TransferService.ResumeReconciliationAsync` exactly
as the background job does, not a parallel implementation. This is the
designed way a human clears a transfer the automated job gave up on.

**Admin transfer search is capped, not paginated.** `GetAllAsync` caps at
200 rows, newest first. Real pagination is out of scope for this round —
flagged here rather than silently truncating without saying so.

**Gateway routes.** New explicit allowlist entries:
`identity-admin-route` (`/identity/api/v1/admin/{**catch-all}`) and
`wallet-admin-route` (`/wallets/api/v1/admin/{**catch-all}`) — neither
overlaps with the existing `internal/` patterns on those same services.
`FlowPay.Transfers`' existing `/transfers/{**catch-all}` wildcard already
covers its new admin routes (still safe — Transfers has zero
`[RequireInternalApiKey]` controllers of its own).

## User stories

### US-9.1: Admin role and authorization

**Acceptance criteria**
- [x] `Account.IsAdmin` set at registration if the email matches
      `AdminBootstrap:Emails`
- [x] Login issues an `is_admin: true` claim for admin accounts (omitted
      entirely for non-admins)
- [x] An `"Admin"` authorization policy is available in every service via
      `AddFlowPayJwtBearer`

### US-9.2: Admin KYC review

As an operator, I want to review and decide pending KYC submissions through
an authenticated endpoint, so I don't need the internal API key to do my job.

**Acceptance criteria**
- [x] `GET /identity/api/v1/admin/kyc/submissions/pending`,
      `POST .../{id}/approve`, `POST .../{id}/reject` — all `[Authorize(Policy
      = "Admin")]`, reusing `IKycService`
- [x] A non-admin JWT gets `403`, not `404` or a silent empty result

### US-9.3: Admin transfer visibility and manual reconciliation retry

**Acceptance criteria**
- [x] `GET /transfers/api/v1/admin/transfers?status=PendingReconciliation`
      (status filter optional) returns transfers across all accounts,
      including `accountId` (never exposed on the customer-facing
      `TransferResponse`)
- [x] `POST /transfers/api/v1/admin/transfers/{id}/force-retry` immediately
      attempts reconciliation for a `PendingReconciliation` or
      `ReconciliationFailed` transfer and returns the outcome

### US-9.4: Admin system wallet visibility

**Acceptance criteria**
- [x] `GET /wallets/api/v1/admin/wallets/system?currency=USD` returns both
      the `FeeRevenue` and `ExternalSettlement` system wallets' balances for
      that currency

### US-9.5: Admin account lookup by email

**Acceptance criteria**
- [x] `GET /identity/api/v1/admin/accounts?email=...` returns the matching
      account (id, email, status) or `404`

## Open questions

- No admin-promotion flow — fixed at registration via config allowlist.
- No pagination on admin transfer search (capped at 200).
- No audit log of admin actions (who approved/rejected what, who
  force-retried which transfer) — everything the underlying services
  already log (structured request logs) is the only trail right now.
