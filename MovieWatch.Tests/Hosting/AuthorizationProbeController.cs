using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Domain.Common;

namespace MovieWatch.Tests.Hosting;

// This endpoint is registered only in the test host to exercise the policy used by later tickets.
[ApiController]
[Route("testing/administrator")]
public sealed class AuthorizationProbeController : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = nameof(AccountRole.Administrator))]
    public IActionResult Get()
    {
        return NoContent();
    }
}
