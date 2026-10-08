namespace FlowPay.BuildingBlocks;

/// <summary>
/// An exact monetary amount: an integer count of minor units (e.g. cents) plus
/// an ISO 4217 currency code. Never represent money as float/decimal-per-unit
/// floating math — minor units keep arithmetic exact.
/// </summary>
public readonly record struct Money
{
    public long MinorUnits { get; }

    public string Currency { get; }

    public Money(long minorUnits, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        MinorUnits = minorUnits;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(MinorUnits + other.MinorUnits, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(MinorUnits - other.MinorUnits, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in different currencies: {Currency} and {other.Currency}.");
        }
    }

    public override string ToString() => $"{MinorUnits} {Currency}";
}
