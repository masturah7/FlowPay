using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Transfers.Data;

public interface ITransferRepository : IRepository<Transfer, Guid>
{
    Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
}

public class TransferRepository(TransfersDbContext dbContext)
    : EfRepository<Transfer, Guid>(dbContext), ITransferRepository
{
    public Task<Transfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        Set.SingleOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
}
