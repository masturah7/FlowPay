using Asp.Versioning;
using FlowPay.Identity.Features.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Identity.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/accounts")]
public class AdminAccountsController(IAccountService accountService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(RegisterAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByEmail([FromQuery] string email, CancellationToken cancellationToken)
    {
        var account = await accountService.GetByEmailAsync(email, cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        return Ok(new RegisterAccountResponse(account.Id, account.Email, account.Status));
    }
}
