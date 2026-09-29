using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class Genre : BaseAuditableEntity
{
    public Genre(string name, long? tmdbId = null)
    {
        Name = name;
        TmdbId = tmdbId;
    }
    public string Name { get; set; } = "";
    public long? TmdbId { get; set; }
}

public sealed class Mood : BaseAuditableEntity
{
    public string? PresetKey { get; set; }
    public Mood(string name, string description)
    {
        Name = name;
        Description = description;
    }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class Movie : BaseAuditableEntity
{
    public Movie(string title, string overview, int? runtimeMinutes, DateOnly? releaseDate)
    {
        Title = title;
        Overview = overview;
        RuntimeMinutes = runtimeMinutes;
        ReleaseDate = releaseDate;
    }
    public string Title { get; set; } = "";
    public string Overview { get; set; } = "";
    public int? RuntimeMinutes { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public long? TmdbId { get; set; }
    public int VoteCount { get; set; }
    public DateTimeOffset? LastImportedAt { get; set; }
    public string? PosterPath { get; set; }
    public List<MovieGenre> MovieGenres { get; set; } = [];
}

public sealed class MovieGenre
{
    public MovieGenre(Guid movieId, Guid genreId)
    {
        MovieId = movieId;
        GenreId = genreId;
    }
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
