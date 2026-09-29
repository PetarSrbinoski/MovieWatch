using MovieWatch.Domain.Models;

namespace MovieWatch.Web.Response;

public sealed record ImportJobResponse(Guid Id, int PageCount, ImportJobStatus Status, int AttemptCount,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    DateTime? NextAttemptAt, DateTime? LeaseUntil, int ImportedCount, int SkippedCount,
    string? Error, int Version);
