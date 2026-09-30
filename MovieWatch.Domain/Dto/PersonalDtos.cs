using MovieWatch.Domain.Models;

namespace MovieWatch.Domain.Dto;

public record ActorDto(
    Guid ViewerId,
    bool IsAdministrator
    );

public record PreferenceDto(
    Guid Id,
    Guid ViewerId,
    Guid GenreId,
    Guid MoodId,
    int Weight
    );

public record WatchlistDto(
    Guid Id,
    Guid ViewerId,
    Guid MovieId,
    WatchStatus Status,
    string Note
    );
