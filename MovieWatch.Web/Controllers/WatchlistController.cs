using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize, Route("api/viewers/{viewerId:guid}/watchlist")]
public sealed class WatchlistController(PersonalMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WatchlistResponse>>> List(Guid viewerId, CancellationToken cancellationToken)
    {
        return Ok(await mapper.ListWatchlistAsync(viewerId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WatchlistResponse>> Get(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        return Ok(await mapper.GetWatchlistEntryAsync(viewerId, id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<WatchlistResponse>> Create(Guid viewerId, WatchlistRequest request, CancellationToken cancellationToken)
    {
        var entry = await mapper.CreateWatchlistEntryAsync(viewerId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { viewerId, id = entry.Id }, entry);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WatchlistResponse>> Update(Guid viewerId, Guid id,
        WatchlistUpdateRequest request, CancellationToken cancellationToken)
    {
        return Ok(await mapper.UpdateWatchlistEntryAsync(viewerId, id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        await mapper.DeleteWatchlistEntryAsync(viewerId, id, cancellationToken);
        return NoContent();
    }
}
