using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize, Route("api/movies")]
public sealed class MoviesController(CatalogueMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MovieResponse>>> List(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
        => Ok((await mapper.ListMoviesAsync(skip, take, cancellationToken)).Select(m => m.ToResponse()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MovieResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok((await mapper.GetMovieAsync(id, cancellationToken)).ToResponse());

    [HttpPost, Authorize(Roles = "Administrator")]
    public async Task<ActionResult<MovieResponse>> Create(MovieRequest request, CancellationToken cancellationToken)
    {
        var movie = (await mapper.CreateMovieAsync(request, cancellationToken)).ToResponse();
        return CreatedAtAction(nameof(Get), new { id = movie.Id }, movie);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "Administrator")]
    public async Task<ActionResult<MovieResponse>> Update(Guid id, MovieRequest request, CancellationToken cancellationToken)
        => Ok((await mapper.UpdateMovieAsync(id, request, cancellationToken)).ToResponse());

    [HttpDelete("{id:guid}"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mapper.DeleteMovieAsync(id, cancellationToken);
        return NoContent();
    }
}
