namespace MovieWatch.Domain.Dto;

public record RecommendationQueryDto(
    Guid MoodId,
    int MaximumRuntimeMinutes,
    IReadOnlyList<Guid> GenreIds,
    bool IncludeWatched = false,
    int Limit = 20
    );

public record GenreContributionDto(
    Guid GenreId,
    string GenreName,
    int Weight
    );

public record MemberScoreDto(
    Guid ViewerId,
    double Score
    );

public record RecommendationDto(
    MovieDto Movie,
    double Score,
    string Explanation,
    IReadOnlyList<GenreContributionDto> GenreContributions,
    IReadOnlyList<MemberScoreDto> MemberScores
    );

public record RecommendationResultDto(
    MoodDto Mood,
    RecommendationQueryDto Query,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<Guid> ParticipatingViewerIds
    );
