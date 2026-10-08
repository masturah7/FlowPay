using System.ComponentModel.DataAnnotations.Schema;
using FlowPay.BuildingBlocks;

namespace FlowPay.Wallet.Domain;

public class Wallet
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public required string Currency { get; init; }

    public long BalanceMinorUnits { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    [NotMapped]
    public Money Balance => new(BalanceMinorUnits, Currency);
}
