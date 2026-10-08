namespace FlowPay.Transfers;

/// <summary>
/// Bound from the "Reconciliation" configuration section. See
/// docs/epics/08-reconciliation.md.
/// </summary>
public class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    public required int PollIntervalSeconds { get; init; }

    public required int MaxAttempts { get; init; }
}
