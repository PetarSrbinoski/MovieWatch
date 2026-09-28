using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public sealed record GenreRequest([Required, MaxLength(100)] string Name);
public sealed record MoodRequest([Required, MaxLength(100)] string Name, [MaxLength(1000)] string Description);
public sealed record MovieRequest([Required, MaxLength(200)] string Title, [MaxLength(4000)] string Overview,
    int? RuntimeMinutes, DateOnly? ReleaseDate, IReadOnlyList<Guid> GenreIds);
