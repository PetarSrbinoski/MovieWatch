namespace MovieWatch.Domain.Dto;

public record GenreDto(
    Guid Id,
    string Name,
    long? TmdbId
    );

public record MoodDto(
    Guid Id,
    string Name,
    string Description,
    string? PresetKey = null,
    IReadOnlyList<MoodGenreWeightDto>? DefaultWeights = null
    );

public record MoodGenreWeightDto(string GenreName, long TmdbId, int Weight);
public record MoodPresetDto(string Key, string Name, string Description, IReadOnlyList<MoodGenreWeightDto> DefaultWeights);

public record MovieDto(
    Guid Id,
    string Title,
    string Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    long? TmdbId,
    int VoteCount,
    DateTimeOffset? LastImportedAt,
    IReadOnlyList<GenreDto> Genres,
    string? PosterPath = null
    );

public record MovieInputDto(
    string Title,
    string Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    IReadOnlyList<Guid> GenreIds
    );
