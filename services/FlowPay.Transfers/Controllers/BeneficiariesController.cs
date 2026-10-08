using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Features.Beneficiaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Transfers.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class BeneficiariesController(IBeneficiaryService beneficiaryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(BeneficiaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var result = await beneficiaryService.CreateAsync(accountId.Value, request, cancellationToken);

        switch (result.Outcome)
        {
            case CreateBeneficiaryOutcome.InvalidRequest:
                return this.ProblemWithErrorCode(
                    StatusCodes.Status400BadRequest,
                    "Invalid beneficiary",
                    "WalletId is required for InternalWallet; BankName and BankAccountNumber are required for ExternalBank.",
                    errorCode: nameof(CreateBeneficiaryOutcome.InvalidRequest));
            case CreateBeneficiaryOutcome.WalletNotFound:
                return NotFound();
            case CreateBeneficiaryOutcome.Created:
                var response = BeneficiaryResponse.From(result.Beneficiary!, maskBankAccountNumber: false);
                return CreatedAtAction(nameof(GetById), new { id = response.Id, version = "1.0" }, response);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result.Outcome), result.Outcome, "Unhandled CreateBeneficiaryOutcome.");
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<BeneficiaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var beneficiaries = await beneficiaryService.GetMineAsync(accountId.Value, cancellationToken);

        return Ok(beneficiaries.Select(b => BeneficiaryResponse.From(b)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BeneficiaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var beneficiary = await beneficiaryService.GetOwnedByIdAsync(accountId.Value, id, cancellationToken);

        if (beneficiary is null)
        {
            return NotFound();
        }

        return Ok(BeneficiaryResponse.From(beneficiary));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var outcome = await beneficiaryService.DeleteAsync(accountId.Value, id, cancellationToken);

        return outcome switch
        {
            DeleteBeneficiaryOutcome.Deleted => NoContent(),
            DeleteBeneficiaryOutcome.NotFound => NotFound(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unhandled DeleteBeneficiaryOutcome."),
        };
    }
}
