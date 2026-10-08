namespace FlowPay.Transfers.Domain;

public enum BeneficiaryType
{
    InternalWallet,
    ExternalBank,
}

/// <summary>
/// A saved transfer destination, owned by the account that created it. See
/// docs/epics/07-external-transfers-beneficiaries-fees-limits.md.
/// </summary>
public class Beneficiary
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public required string Label { get; init; }

    public BeneficiaryType Type { get; init; }

    /// <summary>Required (and only meaningful) when Type is InternalWallet.</summary>
    public Guid? WalletId { get; init; }

    /// <summary>Required (and only meaningful) when Type is ExternalBank.</summary>
    public string? BankName { get; init; }

    /// <summary>Required (and only meaningful) when Type is ExternalBank.</summary>
    public string? BankAccountNumber { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
