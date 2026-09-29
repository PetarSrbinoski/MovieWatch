namespace MovieWatch.Domain.Dto;

public sealed record RecommendationQueryDto(Guid MoodId, int MaximumRuntimeMinutes, IReadOnlyList<Guid> GenreIds,
    bool IncludeWatched = false, int Limit = 20);
public sealed record GenreContributionDto(Guid GenreId, string GenreName, int Weight);
public sealed record MemberScoreDto(Guid ViewerId, double Score);
public sealed record RecommendationDto(MovieDto Movie, double Score, string Explanation,
    IReadOnlyList<GenreContributionDto> GenreContributions, IReadOnlyList<MemberScoreDto> MemberScores);
public sealed record RecommendationResultDto(MoodDto Mood, RecommendationQueryDto Query,
    DateTimeOffset GeneratedAt, IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<Guid> ParticipatingViewerIds);
