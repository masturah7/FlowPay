using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Identity.Features.Kyc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Identity.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/kyc/submissions")]
public class KycController(IKycService kycService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(KycSubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit([FromBody] SubmitKycRequest request, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var result = await kycService.SubmitAsync(
            accountId.Value, request.FullName, request.DateOfBirth, request.DocumentType, request.DocumentNumber,
            cancellationToken);

        if (result.Outcome == SubmitKycOutcome.AccountNotEligible)
        {
            return this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Account not eligible",
                "A submission can only be made while the account is PendingVerification or Rejected.",
                errorCode: nameof(SubmitKycOutcome.AccountNotEligible));
        }

        var response = KycSubmissionResponse.From(result.Submission!);

        return CreatedAtAction(nameof(GetMine), new { version = "1.0" }, response);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(KycSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var submission = await kycService.GetLatestForAccountAsync(accountId.Value, cancellationToken);

        if (submission is null)
        {
            return NotFound();
        }

        return Ok(KycSubmissionResponse.From(submission));
    }
}
