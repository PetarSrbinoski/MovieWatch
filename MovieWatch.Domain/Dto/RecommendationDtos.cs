namespace MovieWatch.Domain.Dto;

public record RecommendationQueryDto(
    Guid MoodId,
    int MaximumRuntimeMinutes,
    IReadOnlyList<Guid> GenreIds,
    bool IncludeWatched = false,
    int Limit = 6,
    bool IncludeOtherOptions = false,
    int Skip = 0
    );

public record GenreContributionDto(
    Guid GenreId,
    string GenreName,
    int Weight,
    bool IsPersonal = false
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
    IReadOnlyList<Guid> ParticipatingViewerIds,
    int PositiveMatchCount,
    int OtherOptionCount
    );
