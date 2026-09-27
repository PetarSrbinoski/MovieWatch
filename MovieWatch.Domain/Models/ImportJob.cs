using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public enum ImportJobStatus { Pending, Running, Completed, Failed }

public sealed class ImportJob : BaseEntity
{
    private ImportJob() { }
    public ImportJob(int pageCount, DateTimeOffset createdAt)
    {
        PageCount = pageCount;
        CreatedAt = createdAt;
    }
    public int PageCount { get; private set; }
    public ImportJobStatus Status { get; private set; } = ImportJobStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public Guid? LeaseOwner { get; private set; }
    public int ImportedCount { get; private set; }
    public int SkippedCount { get; private set; }
    public string? Error { get; private set; }
    public int Version { get; private set; }
}
