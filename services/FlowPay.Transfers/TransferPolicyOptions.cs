namespace FlowPay.Transfers;

/// <summary>
/// Bound from the "TransferPolicy" configuration section. Flat (not
/// currency-aware) minor-unit values — see "Open questions" in
/// docs/epics/07-external-transfers-beneficiaries-fees-limits.md.
/// </summary>
public class TransferPolicyOptions
{
    public const string SectionName = "TransferPolicy";

    public required long MaxPerTransactionMinorUnits { get; init; }

    public required long MaxDailyMinorUnits { get; init; }

    public required long ExternalTransferFeeMinorUnits { get; init; }
}
