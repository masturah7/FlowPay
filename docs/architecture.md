# Architecture

## Services

One ASP.NET Core project per bounded context, each independently buildable,
testable, and deployable (own Dockerfile, own database).

| Service | Responsibility | Owns data for |
|---|---|---|
| `FlowPay.Gateway` | Public entry point. YARP reverse proxy — routes requests to the service that owns them, nothing else. No business logic. | — |
| `FlowPay.Identity` | Registration, authentication, KYC/identity verification. | Users, identity/KYC records |
| `FlowPay.Wallet` | Wallet creation, currency balances, funding. Balances are a projection of the ledger, not the source of truth. | Wallets |
| `FlowPay.Ledger` | The double-entry, append-only ledger. Source of truth for "did money move." Every debit has a matching credit; reversals are new entries, never edits. | Ledger entries |
| `FlowPay.Transfers` | Orchestrates transfers (internal + external), beneficiaries, fees, transaction limits. Writes to the ledger; doesn't hold its own copy of balances. | Beneficiaries, transfer requests/status |
| `FlowPay.Notifications` | Transaction notifications to users. Stateless; no database (yet). | — |

`shared/FlowPay.BuildingBlocks` is a class library, not a service — it has no
Dockerfile and runs in-process inside every service. It exists so that
logging, health checks, API versioning, error responses, and the `Money`
value type are implemented exactly once rather than copy-pasted per service.

## Request flow

External clients only ever talk to `FlowPay.Gateway` on port 8080. The
gateway strips a path prefix and forwards to the owning service over the
internal Docker network (`flowpay-network`) — see
`services/FlowPay.Gateway/appsettings.json` for the route/cluster config:

```
/identity/*      -> flowpay-identity:8080
/wallets/*       -> flowpay-wallet:8080
/transfers/*     -> flowpay-transfers:8080
/notifications/* -> flowpay-notifications:8080
```

`FlowPay.Ledger` is deliberately **not** routed through the gateway at all —
it has no end-user auth, only the shared internal API key (see below), and
its only intended caller is `FlowPay.Transfers`. `FlowPay.Wallet`'s
`internal/*` routes (credit/debit/get-by-id without an ownership check)
live on the *same* service as its public, JWT-authenticated endpoints, so
the gateway's `wallet-route` is an explicit **allowlist**
(`/wallets/api/v1/wallets/*` and `/wallets/health/*`) rather than a
catch-all — a catch-all would also forward `/wallets/api/v1/internal/*`
straight through, leaving only the internal API key between an external
caller and crediting/debiting an arbitrary wallet. Don't widen that route
back to a wildcard without re-adding an explicit carve-out.

Services are not meant to call each other's databases directly — only
through each other's APIs. `FlowPay.Transfers` orchestrating a transfer
calls `FlowPay.Wallet` (debit sender, credit recipient) and
`FlowPay.Ledger` (record the double-entry) rather than writing to either
service's tables itself — see `docs/epics/05-transfers.md` for the full
orchestration and what happens when a step fails partway through.

### Service-to-service auth

Two different mechanisms, for two different relationships:
- **End user → service**: JWT bearer token issued by `FlowPay.Identity`
  (Epic 2), validated by every service via `AddFlowPayJwtBearer`.
- **Service → service** (e.g. `FlowPay.Transfers` calling `FlowPay.Wallet`'s
  or `FlowPay.Ledger`'s internal endpoints): a shared secret header
  (`X-Internal-Api-Key`), validated via `RequireInternalApiKeyAttribute`.
  This is a stand-in for real service identity (mTLS, per-service
  credentials) — a known, documented gap, not a silent one.

## Data

Database-per-service on a single shared Postgres container for local dev
(`flowpay-db`), seeded via `infra/postgres/init-databases.sh`:

- `flowpay_identity`
- `flowpay_wallet`
- `flowpay_ledger`
- `flowpay_transfers`

`FlowPay.Notifications` has no database. Connection strings are wired in
each service's `appsettings.json` and checked by a `/health/ready` Postgres
check — but no data access code (EF Core, Dapper, etc.) exists yet. That's
the next layer to build per service.

<!-- TODO once a messaging need appears (e.g. Ledger needs to tell
     Notifications a transfer settled): decide on an event bus (RabbitMQ,
     Azure Service Bus, etc.) rather than services polling each other's APIs
     for state changes. -->

## Cross-cutting concerns (`FlowPay.BuildingBlocks`)

Every service's `Program.cs` calls the same handful of extension methods
instead of re-wiring these independently:

- `AddFlowPaySerilog(serviceName)` — structured console logging, enriched with `Service` and `CorrelationId`
- `AddFlowPayApiVersioning()` — URL-segment API versioning (`/api/v1/...`)
- `AddFlowPayProblemDetails()` — RFC 7807 error responses, enriched with the correlation id
- `AddFlowPayHealthChecks()` — returns an `IHealthChecksBuilder` so a service can chain its own checks (e.g. `.AddNpgSql(...)`) tagged `FlowPayPlatform.ReadyTag`
- `UseFlowPayPlatform()` — wires the correlation-id middleware, exception handler, request logging, and maps `/health/live` + `/health/ready`

`/health/live` never checks dependencies (cheap, always answers — "is the
process up"). `/health/ready` runs every check tagged `ready` (e.g. can this
instance reach Postgres right now) — that's what an orchestrator should
gate traffic/restarts on.

## Deployment

Local dev: `docker compose up --build` (see root `README.md`). Each
service's `Dockerfile` is a multi-stage build (SDK image to publish, smaller
ASP.NET runtime image to run), listening on port 8080 inside the container.

<!-- TODO: production orchestration target (Kubernetes, ECS, etc.) and a
     CI/CD pipeline (restore/build/test on push) — neither exists yet. -->
