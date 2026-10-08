using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace FlowPay.BuildingBlocks;

/// <summary>
/// Gates an internal, service-to-service-only endpoint behind the shared
/// InternalApiKeyOptions secret instead of end-user [Authorize]. Put this on
/// endpoints that trust an upstream service to have already made the real
/// authorization decision (e.g. Wallet's internal credit endpoint trusts
/// Transfers to have already verified the transfer is legitimate) — never
/// on anything reachable directly by an end user.
/// </summary>
public class RequireInternalApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<InternalApiKeyOptions>();

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out StringValues provided) ||
            !FixedTimeEquals(provided.ToString(), options.ApiKey))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Missing or invalid internal API key.",
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };

            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        // CryptographicOperations.FixedTimeEquals requires equal-length
        // inputs; padding the shorter one keeps the comparison itself
        // constant-time while still correctly reporting "not equal" for a
        // length mismatch (no early-return based on length).
        var length = Math.Max(providedBytes.Length, expectedBytes.Length);
        Array.Resize(ref providedBytes, length);
        Array.Resize(ref expectedBytes, length);

        var lengthsMatch = provided.Length == expected.Length;
        var bytesMatch = CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);

        return lengthsMatch & bytesMatch;
    }
}
