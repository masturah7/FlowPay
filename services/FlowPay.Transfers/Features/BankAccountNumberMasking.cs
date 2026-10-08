namespace FlowPay.Transfers.Features;

/// <summary>
/// Masks a bank account number for API responses — used by both
/// BeneficiaryResponse and TransferResponse. See
/// docs/epics/07-external-transfers-beneficiaries-fees-limits.md for why
/// this is masked by default (everywhere except a beneficiary's creation
/// response).
/// </summary>
public static class BankAccountNumberMasking
{
    public static string? Mask(string? bankAccountNumber)
    {
        if (string.IsNullOrEmpty(bankAccountNumber))
        {
            return bankAccountNumber;
        }

        return bankAccountNumber.Length <= 4
            ? new string('*', bankAccountNumber.Length)
            : new string('*', bankAccountNumber.Length - 4) + bankAccountNumber[^4..];
    }
}
