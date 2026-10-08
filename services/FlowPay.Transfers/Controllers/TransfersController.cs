using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Transfers.Features.Transfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Transfers.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TransfersController(ITransferService transferService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] [Required] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var authorizationHeaderValue = Request.Headers.Authorization.ToString();

        var result = await transferService.CreateAsync(
            accountId.Value,
            request.FromWalletId,
            request.ToWalletId,
            request.AmountMinorUnits,
            request.Currency,
            authorizationHeaderValue,
            idempotencyKey,
            cancellationToken);

        return result.Outcome switch
        {
            CreateTransferOutcome.Completed => Ok(TransferResponse.From(result.Transfer!)),

            // Not a failure — the sender really was debited and it's
            // recorded in the ledger. 202 Accepted + an explicit status in
            // the body, not a bare success or a scary 500.
            CreateTransferOutcome.PendingReconciliation => Accepted(TransferResponse.From(result.Transfer!)),

            CreateTransferOutcome.SourceWalletNotFound => NotFound(),
            CreateTransferOutcome.RecipientWalletNotFound => this.ProblemWithErrorCode(
                StatusCodes.Status404NotFound,
                "Recipient wallet not found",
                "The destination wallet does not exist.",
                errorCode: nameof(CreateTransferOutcome.RecipientWalletNotFound)),
            CreateTransferOutcome.CurrencyMismatch => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Currency mismatch",
                "Both wallets must match the transfer currency.",
                errorCode: nameof(CreateTransferOutcome.CurrencyMismatch)),
            CreateTransferOutcome.SameWallet => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Same wallet",
                "Cannot transfer a wallet to itself.",
                errorCode: nameof(CreateTransferOutcome.SameWallet)),
            CreateTransferOutcome.InsufficientFunds => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Insufficient funds",
                "The source wallet does not have enough balance for this transfer.",
                errorCode: nameof(CreateTransferOutcome.InsufficientFunds)),
            CreateTransferOutcome.IdempotencyKeyConflict => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Idempotency key conflict",
                "This Idempotency-Key was already used with different request parameters.",
                errorCode: nameof(CreateTransferOutcome.IdempotencyKeyConflict)),
            CreateTransferOutcome.DebitFailed => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Transfer did not complete",
                "The transfer could not be completed. No funds were moved — safe to retry with the same Idempotency-Key.",
                errorCode: nameof(CreateTransferOutcome.DebitFailed)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled CreateTransferOutcome."),
        };
    }
}
