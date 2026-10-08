using FlowPay.Transfers.Domain;

namespace FlowPay.Transfers.Features.Beneficiaries;

public record BeneficiaryResponse(
    Guid Id,
    string Label,
    BeneficiaryType Type,
    Guid? WalletId,
    string? BankName,
    string? BankAccountNumber,
    DateTimeOffset CreatedAtUtc)
{
    public static BeneficiaryResponse From(Beneficiary beneficiary, bool maskBankAccountNumber = true) => new(
        beneficiary.Id,
        beneficiary.Label,
        beneficiary.Type,
        beneficiary.WalletId,
        beneficiary.BankName,
        maskBankAccountNumber
            ? BankAccountNumberMasking.Mask(beneficiary.BankAccountNumber)
            : beneficiary.BankAccountNumber,
        beneficiary.CreatedAtUtc);
}
