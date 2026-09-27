using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class GenrePreference : BaseAuditableEntity
{
    private GenrePreference() { }
    public GenrePreference(Guid viewerId, Guid genreId, Guid moodId, int weight)
    {
        ViewerId = viewerId;
        GenreId = genreId;
        MoodId = moodId;
        Weight = weight;
    }
    public Guid ViewerId { get; private set; }
    public Guid GenreId { get; private set; }
    public Guid MoodId { get; private set; }
    public int Weight { get; private set; }
    public void ChangeWeight(int weight) => Weight = weight;
}

public enum WatchStatus { Planned, Watched }

public sealed class WatchlistEntry : BaseAuditableEntity
{
    private WatchlistEntry() { }
    public WatchlistEntry(Guid viewerId, Guid movieId, WatchStatus status, string note)
    {
        ViewerId = viewerId;
        MovieId = movieId;
        Update(status, note);
    }
    public Guid ViewerId { get; private set; }
    public Guid MovieId { get; private set; }
    public WatchStatus Status { get; private set; } = WatchStatus.Planned;
    public string Note { get; private set; } = "";
    public void Update(WatchStatus status, string note)
    {
        Status = status;
        Note = note;
    }
}
