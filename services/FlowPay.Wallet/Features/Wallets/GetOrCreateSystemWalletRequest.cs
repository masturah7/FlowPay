using System.ComponentModel.DataAnnotations;

namespace FlowPay.Wallet.Features.Wallets;

public class GetOrCreateSystemWalletRequest
{
    [Required]
    public required Guid SystemAccountId { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }
}
