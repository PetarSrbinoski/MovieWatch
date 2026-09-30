using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class GenrePreference : BaseAuditableEntity
{
    public GenrePreference(Guid viewerId, Guid genreId, Guid moodId, int weight)
    {
        ViewerId = viewerId;
        GenreId = genreId;
        MoodId = moodId;
        Weight = weight;
    }
    public Guid ViewerId { get; set; }
    public Guid GenreId { get; set; }
    public Guid MoodId { get; set; }
    public int Weight { get; set; }
}

public enum WatchStatus { Planned, Watched }

public sealed class WatchlistEntry : BaseAuditableEntity
{
    public WatchlistEntry(Guid viewerId, Guid movieId, WatchStatus status, string note)
    {
        ViewerId = viewerId;
        MovieId = movieId;
        Status = status;
        Note = note;
    }
    public Guid ViewerId { get; set; }
    public Guid MovieId { get; set; }
    public WatchStatus Status { get; set; } = WatchStatus.Planned;
    public string Note { get; set; } = "";
}
