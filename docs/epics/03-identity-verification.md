# Epic 3: Identity Verification (KYC)

Maps to the PRD capability: "Complete identity verification."

## Summary

Verifying a registered (Epic 1), authenticated (Epic 2) customer is who they
claim to be, flipping their account from `PendingVerification` to
`Verified`. Wallet features (Epic 4) are expected to gate on `Verified`
status, though that gating itself is scoped to Epic 4, not here.

## User stories

### US-3.1: <!-- TODO title, e.g. "Submit identity documents for verification" -->
As a <!-- user type -->, I want to <!-- action -->, so that <!-- benefit -->.

**Acceptance criteria**
- [ ] <!-- TODO -->

## Open questions

- Which KYC provider/vendor (if any) does verification — build in-house
  document review, or integrate a third party (Onfido, Persona, Jumio, etc.)?
- What documents are required, and does that vary by region/jurisdiction?
- What's the account status transition on verification failure — does
  `PendingVerification` stay, or is there a `Rejected` status with a retry
  path?
