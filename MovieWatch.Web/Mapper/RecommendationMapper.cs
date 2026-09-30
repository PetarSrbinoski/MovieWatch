using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Mapper;

public sealed class RecommendationMapper(IRecommendationService service, IWorkbookExportService workbooks,
    AccountMapper accounts)
{
    public async Task<RecommendationResultResponse> ForViewerAsync(Guid viewerId, RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        return (await service.ForViewerAsync(await accounts.GetActorAsync(cancellationToken), viewerId,
            request.ToDto(), cancellationToken)).ToResponse();
    }

    public async Task<RecommendationResultResponse> ForGroupAsync(Guid groupId, RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        return (await service.ForGroupAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            request.ToDto(), cancellationToken)).ToResponse();
    }

    public async Task<byte[]> ExportViewerAsync(Guid viewerId, RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        return workbooks.CreateWorkbook(await service.ForViewerAsync(await accounts.GetActorAsync(cancellationToken),
            viewerId, request.ToDto(), cancellationToken));
    }

    public async Task<byte[]> ExportGroupAsync(Guid groupId, RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        return workbooks.CreateWorkbook(await service.ForGroupAsync(await accounts.GetActorAsync(cancellationToken),
            groupId, request.ToDto(), cancellationToken));
    }
}

public static class RecommendationMappingExtensions
{
    public static RecommendationQueryDto ToDto(this RecommendationRequest request)
    {
        return new(request.MoodId, request.MaximumRuntimeMinutes, request.GenreIds,
            request.IncludeWatched, request.Limit);
    }

    public static RecommendationResultResponse ToResponse(this RecommendationResultDto dto)
    {
        return new(dto.Mood.ToResponse(), dto.GeneratedAt, dto.Recommendations.Select(r =>
            new RecommendationResponse(r.Movie.ToResponse(), r.Score, r.Explanation,
                r.GenreContributions.Select(c => new GenreContributionResponse(c.GenreId, c.GenreName, c.Weight)).ToArray(),
                r.MemberScores.Select(m => new MemberScoreResponse(m.ViewerId, m.Score)).ToArray())).ToArray(),
            dto.ParticipatingViewerIds);
    }
}
