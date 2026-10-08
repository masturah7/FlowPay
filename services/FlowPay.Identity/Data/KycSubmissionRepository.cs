using FlowPay.BuildingBlocks;
using FlowPay.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Identity.Data;

public interface IKycSubmissionRepository : IRepository<KycSubmission, Guid>
{
    Task<KycSubmission?> GetLatestByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<List<KycSubmission>> GetPendingAsync(CancellationToken cancellationToken);
}

public class KycSubmissionRepository(IdentityDbContext dbContext)
    : EfRepository<KycSubmission, Guid>(dbContext), IKycSubmissionRepository
{
    public Task<KycSubmission?> GetLatestByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        Set.Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<KycSubmission>> GetPendingAsync(CancellationToken cancellationToken) =>
        Set.Where(s => s.Status == KycSubmissionStatus.Pending)
            .OrderBy(s => s.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
}
