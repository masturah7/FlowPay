namespace FlowPay.Identity.Domain;

public enum AccountStatus
{
    PendingVerification,
    UnderReview,
    Verified,
    Rejected,
    Suspended,
}

public class Account
{
    public Guid Id { get; init; }

    public required string Email { get; init; }

    public required string PasswordHash { get; set; }

    public AccountStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
