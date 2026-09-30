using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MovieWatch.Web.Controllers;

[ApiController, AllowAnonymous, Route("api/about")]
public sealed class AboutController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
    {
        Name = "MovieWatch",
        Attribution = "This product uses the TMDB API but is not endorsed or certified by TMDB.",
        TmdbLogoUrl = "/tmdb-logo.svg",
        TmdbUrl = "https://www.themoviedb.org"
    });
    }
}
