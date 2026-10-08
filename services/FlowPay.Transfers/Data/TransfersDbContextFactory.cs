using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowPay.Transfers.Data;

/// <summary>
/// Used only by `dotnet ef` design-time tooling run from a developer
/// machine, where the "flowpay-db" Docker Compose DNS name in
/// appsettings.json doesn't resolve. Points at localhost:5432 instead,
/// which works because docker-compose.yml publishes Postgres there. Not
/// used by the running application.
/// </summary>
public class TransfersDbContextFactory : IDesignTimeDbContextFactory<TransfersDbContext>
{
    public TransfersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TransfersDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=flowpay_transfers;Username=flowpay;Password=flowpay");

        return new TransfersDbContext(optionsBuilder.Options);
    }
}
