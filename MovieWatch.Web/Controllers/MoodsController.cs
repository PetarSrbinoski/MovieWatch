using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize, Route("api/moods")]
public sealed class MoodsController(CatalogueMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MoodResponse>>> List(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
        => Ok((await mapper.ListMoodsAsync(skip, take, cancellationToken)).Select(m => m.ToResponse()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MoodResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok((await mapper.GetMoodAsync(id, cancellationToken)).ToResponse());

    [HttpPost, Authorize(Roles = "Administrator")]
    public async Task<ActionResult<MoodResponse>> Create(MoodRequest request, CancellationToken cancellationToken)
    {
        var mood = (await mapper.CreateMoodAsync(request, cancellationToken)).ToResponse();
        return CreatedAtAction(nameof(Get), new { id = mood.Id }, mood);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "Administrator")]
    public async Task<ActionResult<MoodResponse>> Update(Guid id, MoodRequest request, CancellationToken cancellationToken)
        => Ok((await mapper.UpdateMoodAsync(id, request, cancellationToken)).ToResponse());

    [HttpDelete("{id:guid}"), Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mapper.DeleteMoodAsync(id, cancellationToken);
        return NoContent();
    }
}
