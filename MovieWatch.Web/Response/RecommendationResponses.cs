namespace MovieWatch.Web.Response;

public record GenreContributionResponse(
    Guid GenreId,
    string GenreName,
    int Weight
    );

public record MemberScoreResponse(
    Guid ViewerId,
    double Score
    );

public record RecommendationResponse(
    MovieResponse Movie,
    double Score,
    string Explanation,
    IReadOnlyList<GenreContributionResponse> GenreContributions,
    IReadOnlyList<MemberScoreResponse> MemberScores
    );

public record RecommendationResultResponse(
    MoodResponse Mood,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RecommendationResponse> Recommendations,
    IReadOnlyList<Guid> ParticipatingViewerIds
    );
