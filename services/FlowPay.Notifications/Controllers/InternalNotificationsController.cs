using Asp.Versioning;
using FlowPay.BuildingBlocks;
using FlowPay.Notifications.Features.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Notifications.Controllers;

/// <summary>
/// Internal, service-to-service only — never routed through the gateway,
/// never callable by an end user's own token. FlowPay.Transfers is the
/// only intended caller (after a transfer resolves). See
/// docs/epics/06-transaction-visibility.md.
/// </summary>
[RequireInternalApiKey]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/notifications")]
public class InternalNotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var notification = await notificationService.CreateAsync(
            request.AccountId, request.Type, request.Message, request.RelatedTransferId, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, NotificationResponse.From(notification));
    }
}
