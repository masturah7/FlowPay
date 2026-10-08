using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Wallet.Features.Wallets;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Wallet.Controllers;

/// <summary>
/// Internal, service-to-service only — never routed through the gateway,
/// never callable by an end user's own token. These skip the "do you own
/// this wallet" check entirely: the caller (FlowPay.Transfers) is trusted
/// to have already made that authorization decision. See
/// docs/epics/05-transfers.md.
/// </summary>
[RequireInternalApiKey]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/wallets")]
public class InternalWalletsController(IWalletService walletService) : ControllerBase
{
    [HttpPost("system")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrCreateSystemWallet(
        [FromBody] GetOrCreateSystemWalletRequest request, CancellationToken cancellationToken)
    {
        var wallet = await walletService.GetOrCreateSystemWalletAsync(
            request.SystemAccountId, request.Currency, cancellationToken);

        return Ok(WalletResponse.From(wallet));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var wallet = await walletService.GetByIdAsync(id, cancellationToken);

        if (wallet is null)
        {
            return NotFound();
        }

        return Ok(WalletResponse.From(wallet));
    }

    [HttpPost("{id:guid}/credit")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Credit(
        Guid id,
        [FromBody] FundWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] [Required] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await walletService.CreditAsync(
            id, request.AmountMinorUnits, request.Currency, idempotencyKey, cancellationToken);

        return result.Outcome switch
        {
            FundWalletOutcome.WalletNotFound => NotFound(),
            FundWalletOutcome.CurrencyMismatch => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Currency mismatch",
                "The funding currency must match the wallet's currency.",
                errorCode: nameof(FundWalletOutcome.CurrencyMismatch)),
            FundWalletOutcome.AmountTooLarge => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Amount too large",
                "This amount would overflow the wallet's balance.",
                errorCode: nameof(FundWalletOutcome.AmountTooLarge)),
            FundWalletOutcome.IdempotencyKeyConflict => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Idempotency key conflict",
                "This Idempotency-Key was already used with different request parameters.",
                errorCode: nameof(FundWalletOutcome.IdempotencyKeyConflict)),
            FundWalletOutcome.ConcurrentUpdateDetected => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Concurrent update",
                "Another request updated this wallet at the same time. Retry with the same Idempotency-Key.",
                errorCode: nameof(FundWalletOutcome.ConcurrentUpdateDetected)),
            FundWalletOutcome.Success => Ok(WalletResponse.From(result.Wallet!)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled FundWalletOutcome."),
        };
    }

    [HttpPost("{id:guid}/debit")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Debit(
        Guid id,
        [FromBody] FundWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] [Required] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await walletService.DebitAsync(
            id, request.AmountMinorUnits, request.Currency, idempotencyKey, cancellationToken);

        return result.Outcome switch
        {
            DebitWalletOutcome.WalletNotFound => NotFound(),
            DebitWalletOutcome.CurrencyMismatch => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Currency mismatch",
                "The debit currency must match the wallet's currency.",
                errorCode: nameof(DebitWalletOutcome.CurrencyMismatch)),
            DebitWalletOutcome.InsufficientFunds => this.ProblemWithErrorCode(
                StatusCodes.Status400BadRequest,
                "Insufficient funds",
                "The wallet does not have enough balance for this debit.",
                errorCode: nameof(DebitWalletOutcome.InsufficientFunds)),
            DebitWalletOutcome.IdempotencyKeyConflict => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Idempotency key conflict",
                "This Idempotency-Key was already used with different request parameters.",
                errorCode: nameof(DebitWalletOutcome.IdempotencyKeyConflict)),
            DebitWalletOutcome.ConcurrentUpdateDetected => this.ProblemWithErrorCode(
                StatusCodes.Status409Conflict,
                "Concurrent update",
                "Another request updated this wallet at the same time. Retry with the same Idempotency-Key.",
                errorCode: nameof(DebitWalletOutcome.ConcurrentUpdateDetected)),
            DebitWalletOutcome.Success => Ok(WalletResponse.From(result.Wallet!)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled DebitWalletOutcome."),
        };
    }
}
