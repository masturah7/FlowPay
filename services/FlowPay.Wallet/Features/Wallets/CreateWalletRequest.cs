using System.ComponentModel.DataAnnotations;

namespace FlowPay.Wallet.Features.Wallets;

public class CreateWalletRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }
}
