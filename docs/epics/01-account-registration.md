# Epic 1: Account Registration & Management

Maps to the PRD capability: "Register and manage an account."

## Summary

Getting a new customer from "nothing" to a usable FlowPay account record.
Authentication (Epic 2) and identity verification / KYC (Epic 3) are
deliberately separate epics — this one is just account creation.

## User stories

### US-1.1: Register a new account

As a prospective customer, I want to register an account with my email and a
password, so that I can start using FlowPay.

**Acceptance criteria**
- [x] `POST /identity/api/v1/accounts` accepts `email` + `password`
- [x] Email must be a valid, unique address — a duplicate returns `409
      Conflict` rather than creating a second account or silently
      overwriting the first
- [x] Password must be at least 8 characters — otherwise `400 Bad Request`
      with validation details
- [x] Passwords are never stored or logged in plaintext — only a salted
      hash is persisted
- [x] On success, returns `201 Created` with the new account's id, email,
      and status — never the password or its hash
- [x] A newly registered account starts in `PendingVerification` status
      (KYC, which flips it to `Verified`, is Epic 3)

**Status:** Implemented in `services/FlowPay.Identity`
(`AccountsController` + `AccountService` + `IAccountRepository` +
`IdentityDbContext`, EF Core migration `InitialCreate`).

**Out of scope for this story** (tracked separately): login/authentication
(Epic 2), KYC/identity verification (Epic 3), account recovery.

## Open questions

- Account recovery (forgot password) isn't scoped yet — needed before this
  epic can be considered fully closed out.
