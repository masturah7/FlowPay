# FlowPay

FlowPay is a fintech platform providing digital wallets, money transfers, and
related payment infrastructure.

<!-- TODO: expand with a 2-3 sentence description of what FlowPay actually does
     and who it's for once the product scope is finalized. -->

## Project structure

```
FlowPay/
├── README.md
├── FlowPay.slnx
├── Directory.Build.props        # shared TFM/nullable/etc. for every project
├── Directory.Packages.props     # centrally-pinned NuGet package versions
├── docker-compose.yml
├── docs/
│   ├── product-overview.md
│   ├── product-requirements.md
│   ├── architecture.md
│   ├── epics/                   # numbered to match the PRD's capability list, in order
│   │   ├── 01-account-registration.md
│   │   ├── 02-authentication.md
│   │   ├── 03-identity-verification.md
│   │   ├── 04-wallets.md
│   │   ├── 05-transfers.md
│   │   └── 06-transaction-visibility.md
│   └── glossary.md
├── infra/
│   └── postgres/init-databases.sh   # creates one DB per service on first start
├── shared/
│   └── FlowPay.BuildingBlocks/      # cross-cutting: logging, health checks, versioning, Money type
└── services/
    ├── FlowPay.Gateway/             # YARP reverse proxy — public entry point
    ├── FlowPay.Identity/            # registration, auth, KYC
    ├── FlowPay.Wallet/              # wallet creation, balances, funding
    ├── FlowPay.Ledger/              # double-entry ledger, source of truth for money movement
    ├── FlowPay.Transfers/           # transfers, beneficiaries, fees, limits
    └── FlowPay.Notifications/       # transaction notifications
```

- `docs/product-overview.md` — problem being solved, target users, goals, scope
- `docs/product-requirements.md` — functional requirements, business rules, non-functional requirements
- `docs/architecture.md` — service boundaries, how they talk to each other, data store layout
- `docs/epics/` — user stories and acceptance criteria, one file per epic
- `docs/glossary.md` — shared vocabulary (idempotency, ledger, settlement, reconciliation, etc.)
- `shared/FlowPay.BuildingBlocks` — the only place cross-cutting concerns (logging, health checks, API versioning, ProblemDetails, the `Money` value type) are implemented; services consume it, they don't reimplement it
- `services/` — one ASP.NET Core project per bounded context, each independently deployable and containerized; see [Architecture](docs/architecture.md)

## Setup

All installation and running of this project is done through Docker /
Docker Compose — there is no supported bare-metal install path.

```
docker compose up --build
```

This builds all six services and starts them alongside a Postgres instance
(`flowpay-db`) seeded with one database per service (`flowpay_identity`,
`flowpay_wallet`, `flowpay_ledger`, `flowpay_transfers`), scaffolded ahead of
the data access layer. Default Postgres credentials in `docker-compose.yml`
are for local development only — do not reuse them anywhere real.

Only the gateway is published for external use — everything else is reached
through it:

| Service | Reachable via gateway (`http://localhost:8080`) | Direct (dev only) |
|---|---|---|
| Gateway | — | `:8080` |
| Identity | `/identity/*` | `:8081` |
| Wallet | `/wallets/*` | `:8082` |
| Ledger | `/ledger/*` | `:8083` |
| Transfers | `/transfers/*` | `:8084` |
| Notifications | `/notifications/*` | `:8085` |

Each service also exposes `/health/live` and `/health/ready`.

<!-- For local iteration on a single service without Docker, e.g. fast
     edit/test loops: -->

```
dotnet restore
dotnet build FlowPay.slnx
dotnet run --project services/FlowPay.Identity
```

## Docs

- [PRD (source document)](docs/PRD.md)
- [Product Overview](docs/product-overview.md)
- [Product Requirements](docs/product-requirements.md)
- [Architecture](docs/architecture.md)
- [Epics](docs/epics/)
- [Glossary](docs/glossary.md)

## Claude Code

A `lead-dotnet-developer` subagent is defined at
`.claude/agents/lead-dotnet-developer.md` — it encodes this project's
engineering principles (ledger-first money movement, mandatory idempotency,
Docker-only install/run, microservice boundaries, etc.) for implementation
and review work on this codebase.
