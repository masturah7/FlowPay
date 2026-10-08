using System.ComponentModel.DataAnnotations;
using FlowPay.Transfers.Domain;

namespace FlowPay.Transfers.Features.Beneficiaries;

public class CreateBeneficiaryRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Label { get; init; }

    [Required]
    public BeneficiaryType Type { get; init; }

    /// <summary>Required when Type is InternalWallet.</summary>
    public Guid? WalletId { get; init; }

    /// <summary>Required when Type is ExternalBank.</summary>
    public string? BankName { get; init; }

    /// <summary>Required when Type is ExternalBank.</summary>
    public string? BankAccountNumber { get; init; }
}
