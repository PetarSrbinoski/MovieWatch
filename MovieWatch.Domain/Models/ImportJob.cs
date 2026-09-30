using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public enum ImportJobStatus { Pending, Running, Completed, Failed }

public sealed class ImportJob : BaseEntity
{
    public ImportJob(int pageCount, DateTimeOffset createdAt)
    {
        PageCount = pageCount;
        CreatedAt = createdAt;
    }
    public int PageCount { get; set; }
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseOwner { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public string? Error { get; set; }
    public int Version { get; set; }
}
