using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize]
public sealed class RecommendationsController(RecommendationMapper mapper) : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("api/viewers/{viewerId:guid}/recommendations")]
    public async Task<ActionResult<RecommendationResultResponse>> ForViewer(Guid viewerId,
        [FromQuery] RecommendationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await mapper.ForViewerAsync(viewerId, request, cancellationToken));
    }

    [HttpGet("api/groups/{groupId:guid}/recommendations")]
    public async Task<ActionResult<RecommendationResultResponse>> ForGroup(Guid groupId,
        [FromQuery] RecommendationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await mapper.ForGroupAsync(groupId, request, cancellationToken));
    }

    [HttpGet("api/viewers/{viewerId:guid}/recommendations.xlsx")]
    public async Task<IActionResult> ExportViewer(Guid viewerId,
        [FromQuery] RecommendationRequest request, CancellationToken cancellationToken)
    {
        return File(await mapper.ExportViewerAsync(viewerId, request, cancellationToken), ExcelContentType,
            "personal-recommendations.xlsx");
    }

    [HttpGet("api/groups/{groupId:guid}/recommendations.xlsx")]
    public async Task<IActionResult> ExportGroup(Guid groupId,
        [FromQuery] RecommendationRequest request, CancellationToken cancellationToken)
    {
        return File(await mapper.ExportGroupAsync(groupId, request, cancellationToken), ExcelContentType,
            "group-recommendations.xlsx");
    }
}
