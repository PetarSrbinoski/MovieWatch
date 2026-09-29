using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MovieWatch.Web.Controllers;

[ApiController, AllowAnonymous, Route("api/status")]
public sealed class StatusController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        TmdbConfigured = !string.IsNullOrWhiteSpace(configuration["Tmdb:ReadAccessToken"]),
        ImportWorkerEnabled = configuration.GetValue("Import:WorkerEnabled", true)
    });
}
