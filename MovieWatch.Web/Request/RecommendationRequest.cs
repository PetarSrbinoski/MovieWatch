namespace MovieWatch.Web.Request;

public sealed class RecommendationRequest
{
    public Guid MoodId { get; init; }
    public int MaximumRuntimeMinutes { get; init; }
    public Guid[] GenreIds { get; init; } = [];
    public bool IncludeWatched { get; init; }
    public int Limit { get; init; } = 6;
    public bool IncludeOtherOptions { get; init; }
    public int Skip { get; init; }
}
