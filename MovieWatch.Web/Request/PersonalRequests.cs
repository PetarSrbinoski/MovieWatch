using System.ComponentModel.DataAnnotations;
using MovieWatch.Domain.Models;

namespace MovieWatch.Web.Request;

public sealed record PreferenceRequest(Guid GenreId, Guid MoodId, [Range(-2, 2)] int Weight);
public sealed record PreferenceWeightRequest([Range(-2, 2)] int Weight);
public sealed record WatchlistRequest(Guid MovieId, WatchStatus Status, [MaxLength(1000)] string Note);
public sealed record WatchlistUpdateRequest(WatchStatus Status, [MaxLength(1000)] string Note);
