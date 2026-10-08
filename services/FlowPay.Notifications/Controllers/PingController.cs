using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace FlowPay.Notifications.Controllers;

/// <summary>
/// Placeholder endpoint proving the service, routing, and API versioning are
/// wired up. Replace with real notification endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { service = "FlowPay.Notifications", status = "ok" });
}
