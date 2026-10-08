using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace FlowPay.BuildingBlocks;

/// <summary>
/// Propagates (or mints) a correlation id for every request, echoes it back on
/// the response, and pushes it into the Serilog log context so every log line
/// for a request — across services, once the gateway forwards the header —
/// can be tied back together.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("n");

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
