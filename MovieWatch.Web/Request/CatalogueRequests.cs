using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public record GenreRequest(
    [Required, MaxLength(100)] string Name
    );

public record MoodRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(1000)] string Description,
    [MaxLength(40)] string? PresetKey = null
    );

public record MovieRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string Overview,
    int? RuntimeMinutes,
    DateOnly? ReleaseDate,
    IReadOnlyList<Guid> GenreIds
    );
