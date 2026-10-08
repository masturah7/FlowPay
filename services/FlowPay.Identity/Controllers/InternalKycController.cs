using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Identity.Features.Kyc;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Identity.Controllers;

/// <summary>
/// Internal, service-to-service only — never routed through the gateway,
/// never callable by an end user's own token. There's no reviewer/admin
/// account concept yet (see docs/epics/03-identity-verification.md), so
/// review actions are gated by the shared internal API key instead, the
/// same pattern as FlowPay.Wallet's internal credit/debit endpoints.
/// </summary>
[RequireInternalApiKey]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/kyc/submissions")]
public class InternalKycController(IKycService kycService) : ControllerBase
{
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<KycSubmissionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var submissions = await kycService.GetPendingAsync(cancellationToken);

        return Ok(submissions.Select(KycSubmissionResponse.From));
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(KycSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await kycService.ApproveAsync(id, cancellationToken);

        return result.Outcome switch
        {
            ReviewKycOutcome.SubmissionNotFound => NotFound(),
            ReviewKycOutcome.SubmissionNotPending => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Submission not pending",
                "Only a Pending submission can be approved.",
                errorCode: nameof(ReviewKycOutcome.SubmissionNotPending)),
            ReviewKycOutcome.AccountNotUnderReview => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Account not under review",
                "The account has moved out of UnderReview since this submission was made.",
                errorCode: nameof(ReviewKycOutcome.AccountNotUnderReview)),
            ReviewKycOutcome.Decided => Ok(KycSubmissionResponse.From(result.Submission!)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled ReviewKycOutcome."),
        };
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(KycSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] RejectKycRequest request, CancellationToken cancellationToken)
    {
        var result = await kycService.RejectAsync(id, request.Reason, cancellationToken);

        return result.Outcome switch
        {
            ReviewKycOutcome.SubmissionNotFound => NotFound(),
            ReviewKycOutcome.SubmissionNotPending => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Submission not pending",
                "Only a Pending submission can be rejected.",
                errorCode: nameof(ReviewKycOutcome.SubmissionNotPending)),
            ReviewKycOutcome.AccountNotUnderReview => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Account not under review",
                "The account has moved out of UnderReview since this submission was made.",
                errorCode: nameof(ReviewKycOutcome.AccountNotUnderReview)),
            ReviewKycOutcome.Decided => Ok(KycSubmissionResponse.From(result.Submission!)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled ReviewKycOutcome."),
        };
    }
}
