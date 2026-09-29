namespace MovieWatch.Web.Response;

public record GenreResponse(
    Guid Id,
    string Name,
    long? TmdbId
    );

public record MoodResponse(
    Guid Id,
    string Name,
    string Description,
    string? PresetKey = null,
    IReadOnlyList<MoodGenreWeightResponse>? DefaultWeights = null
    );

public record MoodGenreWeightResponse(string GenreName, long TmdbId, int Weight);
public record MoodPresetResponse(string Key, string Name, string Description, IReadOnlyList<MoodGenreWeightResponse> DefaultWeights);

public record MovieResponse(
    Guid Id,
    string Title,
    string Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    long? TmdbId,
    int VoteCount,
    DateTimeOffset? LastImportedAt,
    IReadOnlyList<GenreResponse> Genres,
    string? PosterPath = null
    );
