namespace MovieWatch.Web.Response;

public sealed record GenreResponse(Guid Id, string Name, long? TmdbId);
public sealed record MoodResponse(Guid Id, string Name, string Description);
public sealed record MovieResponse(Guid Id, string Title, string Overview, int? RuntimeMinutes,
    DateOnly? ReleaseDate, long? TmdbId, int VoteCount, DateTimeOffset? LastImportedAt,
    IReadOnlyList<GenreResponse> Genres);
