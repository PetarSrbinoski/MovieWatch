namespace MovieWatch.Web.Response;

public record GenreResponse(
    Guid Id,
    string Name,
    long? TmdbId
    );

public record MoodResponse(
    Guid Id,
    string Name,
    string Description
    );

public record MovieResponse(
    Guid Id,
    string Title,
    string Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    long? TmdbId,
    int VoteCount,
    DateTimeOffset? LastImportedAt,
    IReadOnlyList<GenreResponse> Genres
    );
