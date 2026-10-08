using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Data;

public interface IBeneficiaryRepository : IRepository<Beneficiary, Guid>
{
    Task<List<Beneficiary>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
}

public class BeneficiaryRepository(TransfersDbContext dbContext)
    : EfRepository<Beneficiary, Guid>(dbContext), IBeneficiaryRepository
{
    public Task<List<Beneficiary>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        Set.Where(b => b.AccountId == accountId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);
}
