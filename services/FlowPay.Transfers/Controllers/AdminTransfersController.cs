using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Domain;
using FlowPay.Transfers.Features.Transfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Transfers.Controllers;

/// <summary>
/// Human-operator surface for transfers across all accounts. JWT + Admin
/// policy. See docs/epics/09-admin-ops-tooling.md.
/// </summary>
[Authorize(Policy = "Admin")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/transfers")]
public class AdminTransfersController(ITransferService transferService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<AdminTransferResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] TransferStatus? status, CancellationToken cancellationToken)
    {
        var transfers = await transferService.AdminSearchAsync(status, cancellationToken);

        return Ok(transfers.Select(AdminTransferResponse.From));
    }

    [HttpPost("{id:guid}/force-retry")]
    [ProducesResponseType(typeof(AdminTransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ForceRetry(Guid id, CancellationToken cancellationToken)
    {
        var result = await transferService.AdminForceRetryReconciliationAsync(id, cancellationToken);

        return result.Outcome switch
        {
            AdminForceRetryOutcome.TransferNotFound => NotFound(),
            AdminForceRetryOutcome.NotInReconciliation => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Not in reconciliation",
                "Only a PendingReconciliation or ReconciliationFailed transfer can be force-retried.",
                errorCode: nameof(AdminForceRetryOutcome.NotInReconciliation)),
            AdminForceRetryOutcome.Retried => Ok(AdminTransferResponse.From(result.Transfer!)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled AdminForceRetryOutcome."),
        };
    }
}
