using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController]
[Route("api/viewers")]
[Authorize]
public sealed class ViewersController(AccountMapper mapper) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<ViewerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ViewerResponse>> GetOwnProfile(CancellationToken cancellationToken)
        => Ok(await mapper.GetOwnProfileAsync(cancellationToken));

    [HttpPut("me")]
    public async Task<ActionResult<ViewerResponse>> UpdateOwn(UpdateViewerRequest request, CancellationToken cancellationToken)
        => Ok(await mapper.UpdateOwnAsync(request, cancellationToken));

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteOwn(CancellationToken cancellationToken)
    {
        await mapper.DeleteOwnAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<List<ViewerResponse>>> List(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
        => Ok(await mapper.ListAsync(skip, take, cancellationToken));

    [HttpGet("{viewerId:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ViewerResponse>> Get(Guid viewerId, CancellationToken cancellationToken)
        => Ok(await mapper.GetAsync(viewerId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ViewerResponse>> Create(RegisterRequest request, CancellationToken cancellationToken)
        => Created("/api/viewers", await mapper.RegisterAsync(request, cancellationToken));

    [HttpPut("{viewerId:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ViewerResponse>> Update(Guid viewerId, UpdateViewerRequest request, CancellationToken cancellationToken)
        => Ok(await mapper.UpdateAsync(viewerId, request, cancellationToken));

    [HttpDelete("{viewerId:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid viewerId, CancellationToken cancellationToken)
    {
        await mapper.DeleteAsync(viewerId, cancellationToken);
        return NoContent();
    }
}
