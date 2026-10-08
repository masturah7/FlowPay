using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Identity.Features.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Identity.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AccountsController(IAccountService accountService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RegisterAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.RegisterAsync(request.Email, request.Password, cancellationToken);

        if (result.Outcome == RegisterAccountOutcome.EmailAlreadyRegistered)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Email already registered",
                detail: "An account with this email already exists.");
        }

        var account = result.Account!;
        var response = new RegisterAccountResponse(account.Id, account.Email, account.Status);

        return CreatedAtAction(nameof(GetById), new { id = account.Id, version = "1.0" }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RegisterAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var account = await accountService.GetByIdAsync(id, cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        return Ok(new RegisterAccountResponse(account.Id, account.Email, account.Status));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(RegisterAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var account = await accountService.GetByIdAsync(accountId.Value, cancellationToken);

        if (account is null)
        {
            return this.UnauthenticatedProblem();
        }

        return Ok(new RegisterAccountResponse(account.Id, account.Email, account.Status));
    }
}
