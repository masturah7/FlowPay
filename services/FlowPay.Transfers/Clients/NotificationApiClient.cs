using System.Net.Http.Json;
using FlowPay.BuildingBlocks;
using Microsoft.Extensions.Logging;

namespace FlowPay.Transfers.Clients;

/// <summary>
/// Mirrors FlowPay.Notifications' NotificationType enum by name — the two
/// services don't share an assembly, so these member names must stay in
/// sync by hand. Sent as a string (via .ToString() below), not the
/// underlying number, so this doesn't depend on both enums declaring their
/// members in the same order.
/// </summary>
public enum NotificationType
{
    TransferSent,
    TransferReceived,
    TransferFailed,
    TransferPendingReconciliation,
}

public interface INotificationApiClient
{
    /// <summary>
    /// Best-effort — never throws. A notification failure must never affect
    /// the transfer's recorded outcome; failures are logged and swallowed.
    /// See docs/epics/06-transaction-visibility.md.
    /// </summary>
    Task NotifyAsync(
        Guid accountId,
        NotificationType type,
        string message,
        Guid? relatedTransferId,
        CancellationToken cancellationToken);
}

public class NotificationApiClient(
    HttpClient httpClient,
    InternalApiKeyOptions internalApiKeyOptions,
    ILogger<NotificationApiClient> logger) : INotificationApiClient
{
    public async Task NotifyAsync(
        Guid accountId,
        NotificationType type,
        string message,
        Guid? relatedTransferId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/internal/notifications")
            {
                Content = JsonContent.Create(new
                {
                    AccountId = accountId,
                    Type = type.ToString(),
                    Message = message,
                    RelatedTransferId = relatedTransferId,
                }),
            };
            request.Headers.Add(RequireInternalApiKeyAttribute.HeaderName, internalApiKeyOptions.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Notification delivery failed with status {StatusCode} for account {AccountId}, type {Type}",
                    response.StatusCode, accountId, type);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(
                ex, "Notification delivery threw for account {AccountId}, type {Type}", accountId, type);
        }
    }
}
