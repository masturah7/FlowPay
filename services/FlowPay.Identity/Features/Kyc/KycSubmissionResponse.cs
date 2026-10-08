using FlowPay.Identity.Domain;

namespace FlowPay.Identity.Features.Kyc;

public record KycSubmissionResponse(
    Guid Id,
    Guid AccountId,
    string FullName,
    DateOnly DateOfBirth,
    KycDocumentType DocumentType,
    string DocumentNumber,
    KycSubmissionStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc)
{
    public static KycSubmissionResponse From(KycSubmission submission) => new(
        submission.Id,
        submission.AccountId,
        submission.FullName,
        submission.DateOfBirth,
        submission.DocumentType,
        submission.DocumentNumber,
        submission.Status,
        submission.RejectionReason,
        submission.SubmittedAtUtc,
        submission.ReviewedAtUtc);
}
