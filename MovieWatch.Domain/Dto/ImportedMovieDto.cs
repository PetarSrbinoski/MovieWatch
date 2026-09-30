namespace MovieWatch.Domain.Dto;

public record ImportedGenreDto(
    long TmdbId,
    string Name
    );

public record ImportedMovieDto(
    long TmdbId,
    string Title,
    string Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    int VoteCount,
    IReadOnlyList<ImportedGenreDto> Genres
    );
