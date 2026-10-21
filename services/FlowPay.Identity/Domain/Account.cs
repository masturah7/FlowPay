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

    /// <summary>
    /// Set at registration if the email matches AdminBootstrap:Emails — see
    /// docs/epics/09-admin-ops-tooling.md. No promotion/demotion flow exists
    /// beyond that.
    /// </summary>
    public bool IsAdmin { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
