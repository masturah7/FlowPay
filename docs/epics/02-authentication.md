# Epic 2: Authentication

Maps to the PRD capability: "Authenticate securely."

## Summary

Letting an already-registered customer (Epic 1) prove who they are and
obtain a credential that the rest of FlowPay's services can verify, without
each one calling back to Identity.

## User stories

### US-2.1: Log in to an existing account

As a registered customer, I want to log in with my email and password, so
that I can authenticate subsequent requests to FlowPay.

**Acceptance criteria**
- [x] `POST /identity/api/v1/auth/login` accepts `email` + `password` and
      returns a signed JWT access token + its expiry on success
- [x] Wrong password and unknown email return the *same* `401 Unauthorized`
      with a generic message — login must not reveal whether an email is
      registered (unlike registration's `409`, which is an intentional,
      different trade-off for a different endpoint)
- [x] A `Suspended` account cannot log in even with the correct password —
      `403 Forbidden` (safe to be specific here: the caller already proved
      they know the password)
- [x] A `PendingVerification` account *can* log in — KYC (Epic 3) gates
      wallet features, not authentication itself
- [x] The token identifies the account (`sub` claim) and is verifiable by
      any FlowPay service without calling back to Identity
- [x] A protected endpoint (`GET /identity/api/v1/accounts/me`) proves the
      token actually authenticates a request end-to-end

**Status:** Implemented in `services/FlowPay.Identity`
(`AuthController` + `AuthService` + `JwtTokenGenerator`; JWT validation
wired as `FlowPay.BuildingBlocks.AddFlowPayJwtBearer` so every other service
can validate the same tokens later without re-implementing this).

**Decision:** JWT bearer tokens, not session cookies. Reasoning: services
sit behind `FlowPay.Gateway` and need to authenticate requests
independently without a shared session store; a stateless, signed token
that any service can verify locally fits that shape better than
server-side sessions. Signed with a symmetric key for now (shared via
config) — revisit if/when a service other than Identity needs to verify
tokens without holding that secret (asymmetric keys + a JWKS endpoint).

**Out of scope for this story** (tracked separately): refresh tokens /
logout / token revocation, KYC/identity verification (Epic 3), account
recovery, protecting any endpoint outside Identity with this token (every
other service still accepts requests unauthenticated until their own
stories add `[Authorize]`).

## Open questions

- Token lifetime is currently a fixed 60 minutes with no refresh token —
  fine for this stage, but revisit once a real client needs longer-lived
  sessions.
