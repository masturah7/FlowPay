using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Ledger.Features.Ledger;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Ledger.Controllers;

/// <summary>
/// Internal, service-to-service only — never routed through the gateway,
/// never called directly by an end user. FlowPay.Transfers is the only
/// intended caller. See docs/epics/05-transfers.md for why this is gated by
/// a shared internal API key instead of end-user [Authorize].
/// </summary>
[RequireInternalApiKey]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ledger")]
public class LedgerController(ILedgerService ledgerService) : ControllerBase
{
    [HttpPost("transfers")]
    [ProducesResponseType(typeof(RecordTransferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordTransfer(
        [FromBody] RecordTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] [Required] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await ledgerService.RecordTransferAsync(
            request.TransferReference,
            request.FromWalletId,
            request.ToWalletId,
            request.AmountMinorUnits,
            request.Currency,
            idempotencyKey,
            cancellationToken);

        return result.Outcome switch
        {
            RecordTransferOutcome.IdempotencyKeyConflict => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Idempotency key conflict",
                "This Idempotency-Key was already used with different request parameters.",
                errorCode: nameof(RecordTransferOutcome.IdempotencyKeyConflict)),
            RecordTransferOutcome.Recorded => Created(
                string.Empty,
                new RecordTransferResponse(
                    request.TransferReference,
                    request.FromWalletId,
                    request.ToWalletId,
                    request.AmountMinorUnits,
                    request.Currency)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled RecordTransferOutcome."),
        };
    }

    [HttpGet("wallets/{walletId:guid}/balance")]
    [ProducesResponseType(typeof(WalletBalanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalance(Guid walletId, CancellationToken cancellationToken)
    {
        var balance = await ledgerService.GetBalanceAsync(walletId, cancellationToken);

        return Ok(new WalletBalanceResponse(walletId, balance));
    }
}

public record WalletBalanceResponse(Guid WalletId, long BalanceMinorUnits);
