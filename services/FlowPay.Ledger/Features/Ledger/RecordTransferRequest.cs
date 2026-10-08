using System.ComponentModel.DataAnnotations;

namespace FlowPay.Ledger.Features.Ledger;

public class RecordTransferRequest
{
    [Required]
    public required Guid TransferReference { get; init; }

    [Required]
    public required Guid FromWalletId { get; init; }

    [Required]
    public required Guid ToWalletId { get; init; }

    [Range(1, long.MaxValue)]
    public long AmountMinorUnits { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }
}
