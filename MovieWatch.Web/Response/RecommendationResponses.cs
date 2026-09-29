namespace MovieWatch.Web.Response;

public sealed record GenreContributionResponse(Guid GenreId, string GenreName, int Weight);
public sealed record MemberScoreResponse(Guid ViewerId, double Score);
public sealed record RecommendationResponse(MovieResponse Movie, double Score, string Explanation,
    IReadOnlyList<GenreContributionResponse> GenreContributions, IReadOnlyList<MemberScoreResponse> MemberScores);
public sealed record RecommendationResultResponse(MoodResponse Mood, DateTimeOffset GeneratedAt,
    IReadOnlyList<RecommendationResponse> Recommendations, IReadOnlyList<Guid> ParticipatingViewerIds);
