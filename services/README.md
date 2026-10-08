# Services

One ASP.NET Core project per bounded context. See
[Architecture](../docs/architecture.md) for responsibilities, data
ownership, and request flow between them.

| Service | Role |
|---|---|
| `FlowPay.Gateway` | Public entry point — YARP reverse proxy, no business logic |
| `FlowPay.Identity` | Registration, auth, KYC |
| `FlowPay.Wallet` | Wallet creation, balances, funding |
| `FlowPay.Ledger` | Double-entry, append-only ledger — source of truth for money movement |
| `FlowPay.Transfers` | Transfers, beneficiaries, fees, limits |
| `FlowPay.Notifications` | Transaction notifications |

Each service is self-contained: its own `.csproj`, `Program.cs`, `Dockerfile`,
and `appsettings.json`. All of them reference `shared/FlowPay.BuildingBlocks`
for logging, health checks, API versioning, and error handling rather than
re-wiring those independently — see that project before adding a new one.

To add a new service, copy the shape of an existing one (`FlowPay.Identity`
if it needs a database, `FlowPay.Notifications` if it doesn't), then:

1. Add its `Project Path` to `FlowPay.slnx`
2. Add a service block to `docker-compose.yml`
3. Add a route/cluster for it in `services/FlowPay.Gateway/appsettings.json`
   if it should be reachable externally
