using System.ComponentModel.DataAnnotations;
using FlowPay.Notifications.Domain;

namespace FlowPay.Notifications.Features.Notifications;

public class CreateNotificationRequest
{
    [Required]
    public required Guid AccountId { get; init; }

    [Required]
    public NotificationType Type { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public required string Message { get; init; }

    public Guid? RelatedTransferId { get; init; }
}
