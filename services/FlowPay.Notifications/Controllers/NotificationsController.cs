using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Notifications.Features.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Notifications.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var notifications = await notificationService.GetMineAsync(accountId.Value, cancellationToken);

        return Ok(notifications.Select(NotificationResponse.From));
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();

        if (accountId is null)
        {
            return this.UnauthenticatedProblem();
        }

        var outcome = await notificationService.MarkReadAsync(accountId.Value, id, cancellationToken);

        return outcome switch
        {
            MarkReadOutcome.Success => NoContent(),
            MarkReadOutcome.NotFound => NotFound(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unhandled MarkReadOutcome."),
        };
    }
}
