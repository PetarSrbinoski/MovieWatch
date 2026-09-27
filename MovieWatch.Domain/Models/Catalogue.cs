using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class Genre : BaseAuditableEntity
{
    private Genre() { }
    public Genre(string name, long? tmdbId = null)
    {
        Name = name;
        TmdbId = tmdbId;
    }
    public string Name { get; private set; } = "";
    public long? TmdbId { get; private set; }
    public void Rename(string name) => Name = name;
    public void Refresh(long tmdbId, string name)
    {
        TmdbId = tmdbId;
        Name = name;
    }
}

public sealed class Mood : BaseAuditableEntity
{
    private Mood() { }
    public Mood(string name, string description) => Update(name, description);
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

public sealed class Movie : BaseAuditableEntity
{
    private Movie() { }
    public Movie(string title, string overview, int? runtimeMinutes, DateOnly? releaseDate)
        => UpdateMetadata(title, overview, runtimeMinutes, releaseDate);
    public string Title { get; private set; } = "";
    public string Overview { get; private set; } = "";
    public int? RuntimeMinutes { get; private set; }
    public DateOnly? ReleaseDate { get; private set; }
    public long? TmdbId { get; private set; }
    public int VoteCount { get; private set; }
    public DateTimeOffset? LastImportedAt { get; private set; }
    public List<MovieGenre> MovieGenres { get; private set; } = [];

    public void UpdateMetadata(string title, string overview, int? runtimeMinutes, DateOnly? releaseDate)
    {
        Title = title;
        Overview = overview;
        RuntimeMinutes = runtimeMinutes;
        ReleaseDate = releaseDate;
    }

    public void Refresh(long tmdbId, string title, string overview, int? runtimeMinutes,
        DateOnly? releaseDate, int voteCount, DateTimeOffset importedAt)
    {
        TmdbId = tmdbId;
        UpdateMetadata(title, overview, runtimeMinutes, releaseDate);
        VoteCount = voteCount;
        LastImportedAt = importedAt;
    }

    public void ReplaceGenres(IEnumerable<Guid> genreIds)
    {
        var selected = genreIds.ToHashSet();
        MovieGenres.RemoveAll(link => !selected.Contains(link.GenreId));
        foreach (var id in selected.Where(id => MovieGenres.All(link => link.GenreId != id)))
            MovieGenres.Add(new MovieGenre(Id, id));
    }
}

public sealed class MovieGenre
{
    private MovieGenre() { }
    public MovieGenre(Guid movieId, Guid genreId)
    {
        MovieId = movieId;
        GenreId = genreId;
    }
    public Guid MovieId { get; private set; }
    public Movie Movie { get; private set; } = null!;
    public Guid GenreId { get; private set; }
    public Genre Genre { get; private set; } = null!;
}
