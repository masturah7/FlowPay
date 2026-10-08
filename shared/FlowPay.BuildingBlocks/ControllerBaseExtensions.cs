using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.BuildingBlocks;

public static class ControllerBaseExtensions
{
    /// <summary>
    /// The same ProblemDetails shape as every other error response — a bare
    /// Unauthorized() returns an empty body, inconsistent with everything
    /// else callers parse. Use this for "token is valid but doesn't resolve
    /// to a usable identity" (e.g. no/unparseable sub claim, or the account
    /// it names no longer exists) — not for "no token at all," which the
    /// [Authorize] challenge already handles before your action runs.
    /// </summary>
    public static ObjectResult UnauthenticatedProblem(this ControllerBase controller) =>
        controller.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "The access token is missing, invalid, or no longer corresponds to an account.");

    /// <summary>
    /// Same ProblemDetails shape as Problem(), plus a stable, machine-
    /// readable "errorCode" extension. Use this on internal,
    /// service-to-service endpoints whose responses another FlowPay service
    /// parses programmatically — the human-readable `title` text can be
    /// reworded without silently breaking a caller that matched on it.
    /// </summary>
    public static ObjectResult ProblemWithErrorCode(
        this ControllerBase controller, int statusCode, string title, string detail, string errorCode) =>
        new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Extensions = { ["errorCode"] = errorCode },
        })
        {
            StatusCode = statusCode,
        };
}
