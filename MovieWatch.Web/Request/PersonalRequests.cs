using System.ComponentModel.DataAnnotations;
using MovieWatch.Domain.Models;

namespace MovieWatch.Web.Request;

public record PreferenceRequest(
    Guid GenreId,
    Guid MoodId,
    [Range(-2, 2)] int Weight
    );

public record PreferenceWeightRequest(
    [Range(-2, 2)] int Weight
    );

public record WatchlistRequest(
    Guid MovieId,
    WatchStatus Status,
    [MaxLength(1000)] string Note
    );

public record WatchlistUpdateRequest(
    WatchStatus Status,
    [MaxLength(1000)] string Note
    );
