using FlowPay.BuildingBlocks;
using FlowPay.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Identity.Data;

public interface IAccountRepository : IRepository<Account, Guid>
{
    Task<Account?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
}

public class AccountRepository(IdentityDbContext dbContext)
    : EfRepository<Account, Guid>(dbContext), IAccountRepository
{
    public Task<Account?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);
}
