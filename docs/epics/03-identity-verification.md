# Epic 3: Identity Verification (KYC)

Maps to the PRD capability: "Complete identity verification."

## Summary

Verifying a registered (Epic 1), authenticated (Epic 2) customer is who they
claim to be, flipping their account from `PendingVerification` to
`Verified` (or `Rejected`). No third-party KYC vendor is decided yet (see
Open Questions) — this epic builds a manual, self-attested review flow that
works today and doesn't block on that decision.

## Decisions

**Manual review, no vendor integration.** The customer submits identity
details (name, date of birth, a document type + number — no file
upload/OCR), and a reviewer approves or rejects it. This is a real,
usable flow, not a stub — it's just a human doing the review instead of
Onfido/Persona/Jumio. Swapping in a real provider later means replacing
how a submission gets decided, not the submission/account-status model
itself.

**Reviewer actions are internal-only, not a customer-facing role.** There's
no admin/reviewer account concept anywhere in FlowPay yet (no roles, no
RBAC). Rather than invent that now for a single feature, approve/reject
endpoints are gated the same way as other internal, service-to-service
surfaces (`[RequireInternalApiKey]`), not routed through the gateway —
consistent with how `FlowPay.Wallet`'s internal credit/debit endpoints work
(Epic 5). A real reviewer identity/role system is a fair follow-up once
there's more than one internal-ops use case.

**Account status gains two new states:** `UnderReview` (submission pending
a decision) and `Rejected` (decision was no). `PendingVerification` and
`Rejected` can both submit (or resubmit); `UnderReview` cannot submit again
until the current submission is decided.

**Not gating anything on KYC status yet.** Epic 4 (Wallets) explicitly
chose not to gate wallet creation/funding on `Verified` status. This epic
doesn't change that — it only makes `Verified`/`Rejected` real, reachable
states. Wiring other epics to actually check KYC status is a follow-up.

## User stories

### US-3.1: Submit identity information for verification

As a registered customer, I want to submit my identity details for
verification, so that my account can become `Verified`.

**Acceptance criteria**
- [x] `POST /identity/api/v1/kyc/submissions` (authenticated) accepts full
      name, date of birth, document type, and document number
- [x] Only allowed from `PendingVerification` or `Rejected` account status —
      `409 Conflict` if already `UnderReview` or `Verified`
- [x] On success, creates a submission (`Pending`) and moves the account to
      `UnderReview`, atomically
- [x] `GET /identity/api/v1/kyc/submissions/me` returns the caller's latest
      submission and its status

### US-3.2: Review a submission

As a reviewer, I want to approve or reject a pending submission, so that
the customer's account reflects the outcome.

**Acceptance criteria**
- [x] `GET /identity/api/v1/internal/kyc/submissions/pending` (internal
      API key) lists submissions awaiting review
- [x] `POST /identity/api/v1/internal/kyc/submissions/{id}/approve`
      (internal API key) sets the submission `Approved` and the account
      `Verified` — `409` if the submission isn't `Pending`
- [x] `POST /identity/api/v1/internal/kyc/submissions/{id}/reject`
      (internal API key, requires a reason) sets the submission `Rejected`
      and the account `Rejected` — `409` if the submission isn't `Pending`

**Status:** Implemented in `services/FlowPay.Identity`
(`KycController` + `KycService` + `IKycSubmissionRepository` +
`KycSubmission` entity; `Account.Status` extended with `UnderReview` and
`Rejected`).

**Out of scope for this story**: actual document upload/OCR/liveness
checks, third-party vendor integration, a real reviewer role/account
system, automatic re-gating of other epics' features on KYC status.

## Open questions

- Which KYC provider/vendor (if any) eventually does real verification —
  build further in-house, or integrate Onfido/Persona/Jumio/etc.? Still
  undecided; this epic is deliberately provider-agnostic so the answer
  doesn't block anything.
- Required documents by region/jurisdiction — not modeled; `DocumentType`
  is a flat enum today (Passport, DriversLicense, NationalId).
- When does a reviewer identity/role system become real, and does that
  replace the internal-API-key gate on review actions?
