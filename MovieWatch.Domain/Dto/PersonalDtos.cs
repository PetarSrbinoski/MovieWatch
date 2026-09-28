using MovieWatch.Domain.Models;

namespace MovieWatch.Domain.Dto;

public sealed record ActorDto(Guid ViewerId, bool IsAdministrator);
public sealed record PreferenceDto(Guid Id, Guid ViewerId, Guid GenreId, Guid MoodId, int Weight);
public sealed record WatchlistDto(Guid Id, Guid ViewerId, Guid MovieId, WatchStatus Status, string Note);
