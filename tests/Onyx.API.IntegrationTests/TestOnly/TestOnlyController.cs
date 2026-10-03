using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Onyx.API.Configuration;

namespace Onyx.API.IntegrationTests.TestOnly;

/// <summary>Exists only in the test assembly, to exercise the real pipeline (errors, limits, rate limiting).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/test-only")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class TestOnlyController : ControllerBase
{
    public const string SecretExceptionMessage = "Sensitive internal detail: db-secret-host password=hunter2";

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "ok" });

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(SecretExceptionMessage);

    [HttpPost("echo")]
    public IActionResult Echo([FromBody] EchoRequest request) => Ok(new { length = request.Text?.Length ?? 0 });

    [HttpGet("strict")]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    public IActionResult Strict() => Ok();
}
