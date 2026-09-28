namespace MovieWatch.Domain.Dto;

public sealed record GenreDto(Guid Id, string Name, long? TmdbId);
public sealed record MoodDto(Guid Id, string Name, string Description);
public sealed record MovieDto(Guid Id, string Title, string Overview, int? RuntimeMinutes,
    DateOnly? ReleaseDate, long? TmdbId, int VoteCount, DateTimeOffset? LastImportedAt,
    IReadOnlyList<GenreDto> Genres);
public sealed record MovieInputDto(string Title, string Overview, int? RuntimeMinutes,
    DateOnly? ReleaseDate, IReadOnlyList<Guid> GenreIds);
