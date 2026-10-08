using FlowPay.Identity.Data;
using FlowPay.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowPay.Identity.Features.Kyc;

public enum SubmitKycOutcome
{
    Submitted,
    AccountNotEligible,
}

public record SubmitKycResult(SubmitKycOutcome Outcome, KycSubmission? Submission);

public enum ReviewKycOutcome
{
    Decided,
    SubmissionNotFound,
    SubmissionNotPending,

    /// <summary>
    /// The submission is Pending, but the account it belongs to has since
    /// moved out of UnderReview (e.g. Suspended for an unrelated reason) —
    /// refuse to silently move it to Verified/Rejected from there.
    /// </summary>
    AccountNotUnderReview,
}

public record ReviewKycResult(ReviewKycOutcome Outcome, KycSubmission? Submission);

public interface IKycService
{
    Task<SubmitKycResult> SubmitAsync(
        Guid accountId,
        string fullName,
        DateOnly dateOfBirth,
        KycDocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken);

    Task<KycSubmission?> GetLatestForAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<List<KycSubmission>> GetPendingAsync(CancellationToken cancellationToken);

    Task<ReviewKycResult> ApproveAsync(Guid submissionId, CancellationToken cancellationToken);

    Task<ReviewKycResult> RejectAsync(Guid submissionId, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// Manual KYC review workflow — see docs/epics/03-identity-verification.md
/// for why there's no third-party vendor call here. Account and
/// KycSubmission both live in IdentityDbContext, so a single
/// SaveChangesAsync (via either repository) commits both atomically.
/// </summary>
public class KycService(IAccountRepository accountRepository, IKycSubmissionRepository kycSubmissionRepository)
    : IKycService
{
    public async Task<SubmitKycResult> SubmitAsync(
        Guid accountId,
        string fullName,
        DateOnly dateOfBirth,
        KycDocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);

        if (account is null ||
            (account.Status != AccountStatus.PendingVerification && account.Status != AccountStatus.Rejected))
        {
            return new SubmitKycResult(SubmitKycOutcome.AccountNotEligible, null);
        }

        var submission = new KycSubmission
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            FullName = fullName,
            DateOfBirth = dateOfBirth,
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            Status = KycSubmissionStatus.Pending,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
        };

        kycSubmissionRepository.Add(submission);
        account.Status = AccountStatus.UnderReview;

        try
        {
            await kycSubmissionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The partial unique index on (AccountId) WHERE Status='Pending'
            // is the only thing that can fail here — a concurrent request
            // for the same account won the race and already has a Pending
            // submission in flight.
            return new SubmitKycResult(SubmitKycOutcome.AccountNotEligible, null);
        }

        return new SubmitKycResult(SubmitKycOutcome.Submitted, submission);
    }

    public Task<KycSubmission?> GetLatestForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        kycSubmissionRepository.GetLatestByAccountIdAsync(accountId, cancellationToken);

    public Task<List<KycSubmission>> GetPendingAsync(CancellationToken cancellationToken) =>
        kycSubmissionRepository.GetPendingAsync(cancellationToken);

    public async Task<ReviewKycResult> ApproveAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await kycSubmissionRepository.GetByIdAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return new ReviewKycResult(ReviewKycOutcome.SubmissionNotFound, null);
        }

        if (submission.Status != KycSubmissionStatus.Pending)
        {
            return new ReviewKycResult(ReviewKycOutcome.SubmissionNotPending, null);
        }

        var account = await accountRepository.GetByIdAsync(submission.AccountId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"KycSubmission {submissionId} references account {submission.AccountId}, which doesn't exist.");

        // The account may have moved on since this submission was made (e.g.
        // Suspended for an unrelated reason) — don't silently overwrite that
        // with Verified just because an old submission is still Pending.
        if (account.Status != AccountStatus.UnderReview)
        {
            return new ReviewKycResult(ReviewKycOutcome.AccountNotUnderReview, null);
        }

        submission.Status = KycSubmissionStatus.Approved;
        submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        account.Status = AccountStatus.Verified;

        await kycSubmissionRepository.SaveChangesAsync(cancellationToken);

        return new ReviewKycResult(ReviewKycOutcome.Decided, submission);
    }

    public async Task<ReviewKycResult> RejectAsync(Guid submissionId, string reason, CancellationToken cancellationToken)
    {
        var submission = await kycSubmissionRepository.GetByIdAsync(submissionId, cancellationToken);

        if (submission is null)
        {
            return new ReviewKycResult(ReviewKycOutcome.SubmissionNotFound, null);
        }

        if (submission.Status != KycSubmissionStatus.Pending)
        {
            return new ReviewKycResult(ReviewKycOutcome.SubmissionNotPending, null);
        }

        var account = await accountRepository.GetByIdAsync(submission.AccountId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"KycSubmission {submissionId} references account {submission.AccountId}, which doesn't exist.");

        if (account.Status != AccountStatus.UnderReview)
        {
            return new ReviewKycResult(ReviewKycOutcome.AccountNotUnderReview, null);
        }

        submission.Status = KycSubmissionStatus.Rejected;
        submission.RejectionReason = reason;
        submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        account.Status = AccountStatus.Rejected;

        await kycSubmissionRepository.SaveChangesAsync(cancellationToken);

        return new ReviewKycResult(ReviewKycOutcome.Decided, submission);
    }
}
