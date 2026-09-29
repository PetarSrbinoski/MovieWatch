using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface IRecommendationService
{
    Task<RecommendationResultDto> ForViewerAsync(ActorDto actor, Guid viewerId,
        RecommendationQueryDto query, CancellationToken cancellationToken);
    Task<RecommendationResultDto> ForGroupAsync(ActorDto actor, Guid groupId,
        RecommendationQueryDto query, CancellationToken cancellationToken);
}
