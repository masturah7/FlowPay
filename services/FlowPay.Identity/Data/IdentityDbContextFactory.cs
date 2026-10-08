using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowPay.Identity.Data;

/// <summary>
/// Used only by `dotnet ef` design-time tooling (e.g. `dotnet ef migrations
/// add`) run from a developer machine, where the "flowpay-db" Docker Compose
/// DNS name in appsettings.json doesn't resolve. Points at localhost:5432
/// instead, which works because docker-compose.yml publishes Postgres there.
/// Not used by the running application — Program.cs wires the real
/// connection string from configuration.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=flowpay_identity;Username=flowpay;Password=flowpay");

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
