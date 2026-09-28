using MovieWatch.Domain.Models;

namespace MovieWatch.Web.Response;

public sealed record PreferenceResponse(Guid Id, Guid ViewerId, Guid GenreId, Guid MoodId, int Weight);
public sealed record WatchlistResponse(Guid Id, Guid ViewerId, Guid MovieId, WatchStatus Status, string Note);
