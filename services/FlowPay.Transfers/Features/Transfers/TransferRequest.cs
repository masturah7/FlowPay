using System.ComponentModel.DataAnnotations;

namespace FlowPay.Transfers.Features.Transfers;

public class TransferRequest
{
    [Required]
    public required Guid FromWalletId { get; init; }

    /// <summary>Exactly one of ToWalletId / ToBeneficiaryId must be set.</summary>
    public Guid? ToWalletId { get; init; }

    /// <summary>Exactly one of ToWalletId / ToBeneficiaryId must be set.</summary>
    public Guid? ToBeneficiaryId { get; init; }

    [Range(1, long.MaxValue)]
    public long AmountMinorUnits { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }
}
