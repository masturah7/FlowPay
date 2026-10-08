# Glossary

**Idempotency**
The property that performing the same operation multiple times (e.g. retrying
a transfer request after a timeout) produces the same result as performing it
once, without duplicate side effects.

**Ledger**
The authoritative, append-only record of every financial transaction and the
resulting balance changes. Used as the source of truth for reconciliation and
auditing.

**Settlement**
The process of finalizing a transaction so that funds are actually moved
between accounts/institutions, as opposed to merely being authorized or
pending.

**Reconciliation**
The process of comparing internal ledger records against external sources
(bank statements, payment processor reports) to confirm they match and
surface discrepancies.

**Wallet**
A user's holding of funds within FlowPay, tracked as a balance against the
ledger.

**Transfer**
A movement of funds from one wallet (or external account) to another.

**Idempotency key**
A unique identifier attached to a money-movement request so that retrying
the same request (e.g. after a client timeout) is recognized as a duplicate
and processed exactly once rather than re-executed.

**Double-entry (ledger)**
An accounting approach where every transaction records at least one debit
and one matching credit, so the ledger is always internally balanced and
every movement of funds has a traceable counterpart.

**Compensating transaction (reversal)**
A new ledger entry that undoes the effect of a prior transaction (e.g. a
failed or disputed transfer), recorded as an explicit new entry rather than
an edit or deletion of the original.

**Beneficiary**
An external bank account or recipient a user has saved for repeat transfers.

**KYC (Know Your Customer)**
The identity verification process required before a user can fully use
FlowPay, used to confirm the user is who they claim to be.

**AML (Anti-Money Laundering)**
Regulatory controls and monitoring intended to detect and prevent the use of
FlowPay for laundering illicit funds.

**Bounded context**
A boundary around a specific area of the domain (e.g. wallets, the ledger,
transfers) within which a term has one unambiguous meaning and one service
owns the data. FlowPay's services are split along these boundaries — see
[Architecture](architecture.md).

**Gateway (reverse proxy)**
The single public entry point (`FlowPay.Gateway`) that forwards each
incoming request to the service that owns it, based on the request path. It
holds no business logic or data of its own.

**Database-per-service**
Each service owns its own database; no service reads or writes another
service's tables directly. Cross-service data access only happens through
that service's API.

**Liveness / readiness**
Liveness (`/health/live`) answers "is the process still running" — no
dependency checks, always cheap. Readiness (`/health/ready`) answers "can
this instance actually serve traffic right now" (e.g. is its database
reachable) — what should gate whether an orchestrator sends it requests.

<!-- TODO: add more terms as they come up, e.g. chargeback, hold, float,
     payout, webhook. -->
