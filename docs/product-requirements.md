# Product Requirements

## Functional requirements

### Onboarding
- Account registration and management
- Secure authentication
- Identity verification (KYC)

### Wallets
- Create and manage wallets
- Hold supported currencies
- Fund wallets

### Transfers
- Transfer money to other FlowPay users
- Transfer money to external bank accounts
- Save beneficiaries
- Pay transaction fees
- Operate within configured transaction limits

### Transaction visibility
- View transaction history
- Receive transaction notifications
- Track transaction status: pending, successful, failed, reversed

## Financial integrity requirements

The platform must maintain accurate financial records at all times. For any
transaction, the system must be able to answer:

1. **Where did the money come from?** — the originating wallet/account and
   funding source for every credit.
2. **Where did the money go?** — the destination wallet/account (internal or
   external) for every debit.
3. **Can we prove exactly what happened?** — an immutable, append-only
   ledger entry trail sufficient to reconstruct the transaction independent
   of any mutable application state.

### Failure-mode questions

When a failure occurs (timeout, partial processing, downstream outage,
duplicate request, etc.), the system must additionally be able to answer:

- **Did money move?** — distinguish "we don't know" from "no, it didn't."
- **Should money move?** — determine the correct outcome given the ledger
  state, independent of what the caller believes happened.
- **Can this request safely be retried?** — every money-movement operation
  must be idempotent (keyed by an idempotency key / request ID), so retries
  never cause duplicate transfers.
- **Does anything need to be reversed?** — compensating transactions
  (reversals) must be modeled explicitly in the ledger, never as deletions
  or silent corrections.
- **Does anything require reconciliation?** — discrepancies between
  FlowPay's ledger and external systems (banking partners, payment
  processors) must be detectable, not just correctable after the fact.
- **Can operations reconstruct what happened?** — support/ops staff must be
  able to answer all of the above from durable records alone, without
  reasoning about service internals or logs that can be rotated away.

**Financial correctness, security, traceability, and recoverability take
priority over implementation convenience.** This governs every design
decision in this platform — if a shortcut would make any of the above
questions unanswerable, it is not an acceptable shortcut.

## Business rules

<!-- TODO: e.g. KYC/AML thresholds, transaction limits, fee schedules,
     currency support, hold/settlement windows. -->

## Non-functional requirements

### Security
- All sensitive data encrypted at rest and in transit
- <!-- TODO -->

### Compliance
- <!-- TODO: PCI-DSS, KYC/AML, regional regulatory requirements -->

### Performance
- <!-- TODO: expected latency/throughput for transfers -->

### Reliability
- All money-movement operations must be idempotent and safely retryable
- <!-- TODO: uptime targets -->

### Auditability
- All financial transactions must be traceable via an immutable,
  append-only ledger entry (double-entry: every debit has a matching
  credit)
- Reversals/compensations are recorded as new ledger entries, never as
  edits or deletions of existing ones
- <!-- TODO -->

### Deployment & infrastructure
- Architecture: microservices — one service per bounded context (Identity,
  Wallet, Ledger, Transfers, Notifications) behind a reverse-proxy gateway;
  see [Architecture](architecture.md)
- Implementation language: .NET (ASP.NET Core)
- All services, dependencies, and local environments are run via Docker /
  Docker Compose — no bare-metal or "works on my machine" install paths
- Each service owns its own database (database-per-service); services don't
  reach into another service's data store
- <!-- TODO: orchestration target for production (e.g. Kubernetes, ECS) -->

## Related docs

- [PRD (source document)](PRD.md)
- [Product Overview](product-overview.md)
- [Architecture](architecture.md)
- [Epics](epics/)
- [Glossary](glossary.md)
