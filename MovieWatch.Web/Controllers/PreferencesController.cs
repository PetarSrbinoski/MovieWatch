using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize, Route("api/viewers/{viewerId:guid}/preferences")]
public sealed class PreferencesController(PersonalMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PreferenceResponse>>> List(Guid viewerId, CancellationToken cancellationToken)
        => Ok(await mapper.ListPreferencesAsync(viewerId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PreferenceResponse>> Get(Guid viewerId, Guid id, CancellationToken cancellationToken)
        => Ok(await mapper.GetPreferenceAsync(viewerId, id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PreferenceResponse>> Create(Guid viewerId, PreferenceRequest request, CancellationToken cancellationToken)
    {
        var preference = await mapper.CreatePreferenceAsync(viewerId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { viewerId, id = preference.Id }, preference);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PreferenceResponse>> Update(Guid viewerId, Guid id,
        PreferenceWeightRequest request, CancellationToken cancellationToken)
        => Ok(await mapper.UpdatePreferenceAsync(viewerId, id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        await mapper.DeletePreferenceAsync(viewerId, id, cancellationToken);
        return NoContent();
    }
}
