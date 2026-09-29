namespace MovieWatch.Domain.Dto;

public sealed record ImportedGenreDto(long TmdbId, string Name);
public sealed record ImportedMovieDto(long TmdbId, string Title, string Overview,
    int? RuntimeMinutes, DateOnly? ReleaseDate, int VoteCount,
    IReadOnlyList<ImportedGenreDto> Genres);
