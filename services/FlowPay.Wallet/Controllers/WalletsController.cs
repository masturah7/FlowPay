using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Wallet.Features.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Wallet.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WalletsController(IWalletService walletService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWalletRequest request, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var result = await walletService.CreateAsync(accountId.Value, request.Currency, cancellationToken);

        if (result.Outcome == CreateWalletOutcome.CurrencyAlreadyExists)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Wallet already exists",
                detail: "This account already has a wallet in this currency.");
        }

        var response = WalletResponse.From(result.Wallet!);

        return CreatedAtAction(nameof(GetById), new { id = response.Id, version = "1.0" }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<WalletResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var wallets = await walletService.GetMineAsync(accountId.Value, cancellationToken);

        return Ok(wallets.Select(WalletResponse.From));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var wallet = await walletService.GetOwnedByIdAsync(accountId.Value, id, cancellationToken);

        if (wallet is null)
        {
            return NotFound();
        }

        return Ok(WalletResponse.From(wallet));
    }

    [HttpPost("{id:guid}/fund")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Fund(
        Guid id,
        [FromBody] FundWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] [Required] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var result = await walletService.FundAsync(
            accountId.Value, id, request.AmountMinorUnits, request.Currency, idempotencyKey, cancellationToken);

        // Every named FundWalletOutcome is handled explicitly; `_` only
        // catches a genuinely invalid enum value (e.g. an unchecked cast) —
        // same fail-loud pattern as AuthController.Login, so an unhandled
        // outcome throws immediately instead of silently defaulting to the
        // wrong HTTP response.
        return result.Outcome switch
        {
            FundWalletOutcome.WalletNotFound => NotFound(),
            FundWalletOutcome.CurrencyMismatch => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Currency mismatch",
                detail: "The funding currency must match the wallet's currency."),
            FundWalletOutcome.AmountTooLarge => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Amount too large",
                detail: "This amount would overflow the wallet's balance."),
            FundWalletOutcome.IdempotencyKeyConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency key conflict",
                detail: "This Idempotency-Key was already used with different request parameters."),
            FundWalletOutcome.ConcurrentUpdateDetected => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrent update",
                detail: "Another request updated this wallet at the same time. Retry with the same Idempotency-Key."),
            FundWalletOutcome.Success => Ok(WalletResponse.From(result.Wallet!)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Outcome), result.Outcome, "Unhandled FundWalletOutcome."),
        };
    }
}
