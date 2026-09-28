using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Domain.Common;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize, Route("api/genres")]
public sealed class GenresController(CatalogueMapper mapper) : ControllerBase
{
    [HttpPost("sync"), Authorize(Roles = nameof(AccountRole.Administrator))]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        return Ok(new { Count = await mapper.SyncGenresAsync(cancellationToken) });
    }

    [HttpGet]
    public async Task<ActionResult<List<GenreResponse>>> List(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        return Ok((await mapper.ListGenresAsync(skip, take, cancellationToken)).Select(g => g.ToResponse()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GenreResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok((await mapper.GetGenreAsync(id, cancellationToken)).ToResponse());
    }

    [HttpPost, Authorize(Roles = nameof(AccountRole.Administrator))]
    public async Task<ActionResult<GenreResponse>> Create(GenreRequest request, CancellationToken cancellationToken)
    {
        var genre = (await mapper.CreateGenreAsync(request, cancellationToken)).ToResponse();
        return CreatedAtAction(nameof(Get), new { id = genre.Id }, genre);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = nameof(AccountRole.Administrator))]
    public async Task<ActionResult<GenreResponse>> Update(Guid id, GenreRequest request, CancellationToken cancellationToken)
    {
        return Ok((await mapper.UpdateGenreAsync(id, request, cancellationToken)).ToResponse());
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = nameof(AccountRole.Administrator))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mapper.DeleteGenreAsync(id, cancellationToken);
        return NoContent();
    }
}
