using MovieWatch.Domain.Models;

namespace MovieWatch.Domain.Dto;

public sealed record ImportJobDto(Guid Id, int PageCount, ImportJobStatus Status, int AttemptCount,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    DateTime? NextAttemptAt, DateTime? LeaseUntil, int ImportedCount, int SkippedCount,
    string? Error, int Version);
