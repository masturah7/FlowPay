using System.ComponentModel.DataAnnotations;

namespace FlowPay.Wallet.Features.Wallets;

public class FundWalletRequest
{
    [Range(1, long.MaxValue)]
    public long AmountMinorUnits { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }
}
