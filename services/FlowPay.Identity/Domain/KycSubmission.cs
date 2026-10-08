namespace FlowPay.Identity.Domain;

public enum KycDocumentType
{
    Passport,
    DriversLicense,
    NationalId,
}

public enum KycSubmissionStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// One attempt by a customer to verify their identity. See
/// docs/epics/03-identity-verification.md — this is a manual review record,
/// not a call to a third-party KYC vendor (none is chosen yet).
/// </summary>
public class KycSubmission
{
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public required string FullName { get; init; }

    public DateOnly DateOfBirth { get; init; }

    public KycDocumentType DocumentType { get; init; }

    public required string DocumentNumber { get; init; }

    public KycSubmissionStatus Status { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; init; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }
}
