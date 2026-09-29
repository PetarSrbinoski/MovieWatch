using Microsoft.EntityFrameworkCore;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;

namespace MovieWatch.Repository.Implementation;

public sealed class ImportJobRepository(ApplicationDbContext context) : IImportJobRepository
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<ImportJob> CreateAsync(int pageCount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var job = new ImportJob(pageCount, now);
        context.ImportJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public Task<List<ImportJob>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => context.ImportJobs.AsNoTracking().OrderBy(j => j.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);

    public Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken)
        => context.ImportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<bool> UpdatePendingAsync(Guid id, int expectedVersion, int pageCount, CancellationToken cancellationToken)
        => await context.ImportJobs.Where(j => j.Id == id && j.Status == ImportJobStatus.Pending
                && j.Version == expectedVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.PageCount, pageCount)
                .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;

    public async Task<bool> DeleteEditableAsync(Guid id, int expectedVersion, CancellationToken cancellationToken)
        => await context.ImportJobs.Where(j => j.Id == id && j.Status != ImportJobStatus.Running
                && j.Version == expectedVersion)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public async Task<ImportJob?> ClaimNextAsync(Guid owner, DateTime now, CancellationToken cancellationToken)
    {
        var candidates = await context.ImportJobs.AsNoTracking()
            .Where(j => (j.Status == ImportJobStatus.Pending && (j.NextAttemptAt == null || j.NextAttemptAt <= now))
                || (j.Status == ImportJobStatus.Running && j.LeaseUntil <= now))
            .OrderBy(j => j.Id).Take(20).ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (candidate.AttemptCount >= 3)
            {
                await context.ImportJobs.Where(j => j.Id == candidate.Id && j.Version == candidate.Version)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.Status, ImportJobStatus.Failed)
                        .SetProperty(j => j.Error, "Import interrupted after the attempt limit.")
                        .SetProperty(j => j.FinishedAt, new DateTimeOffset(now, TimeSpan.Zero))
                        .SetProperty(j => j.LeaseOwner, (Guid?)null)
                        .SetProperty(j => j.LeaseUntil, (DateTime?)null)
                        .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken);
                continue;
            }
            var claimed = await context.ImportJobs.Where(j => j.Id == candidate.Id && j.Version == candidate.Version
                    && j.Status == candidate.Status)
                .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.Status, ImportJobStatus.Running)
                    .SetProperty(j => j.AttemptCount, candidate.AttemptCount + 1)
                    .SetProperty(j => j.StartedAt, new DateTimeOffset(now, TimeSpan.Zero))
                    .SetProperty(j => j.FinishedAt, (DateTimeOffset?)null)
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseUntil, now + LeaseDuration)
                    .SetProperty(j => j.LeaseOwner, owner)
                    .SetProperty(j => j.ImportedCount, 0)
                    .SetProperty(j => j.SkippedCount, 0)
                    .SetProperty(j => j.Error, (string?)null)
                    .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken);
            if (claimed == 1)
                return await GetAsync(candidate.Id, cancellationToken);
        }
        return null;
    }

    public async Task<bool> RenewAsync(Guid id, Guid owner, DateTime now, CancellationToken cancellationToken)
        => await Owned(id, owner, now).ExecuteUpdateAsync(setters => setters
            .SetProperty(j => j.LeaseUntil, now + LeaseDuration)
            .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;

    public async Task<bool> RecordProgressAsync(Guid id, Guid owner, DateTime now, bool imported, CancellationToken cancellationToken)
    {
        if (imported)
            return await Owned(id, owner, now).ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.ImportedCount, j => j.ImportedCount + 1)
                .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;
        return await Owned(id, owner, now).ExecuteUpdateAsync(setters => setters
            .SetProperty(j => j.SkippedCount, j => j.SkippedCount + 1)
            .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;
    }

    public async Task<bool> CompleteAsync(Guid id, Guid owner, DateTime now, CancellationToken cancellationToken)
        => await Owned(id, owner, now).ExecuteUpdateAsync(setters => setters
            .SetProperty(j => j.Status, ImportJobStatus.Completed)
            .SetProperty(j => j.FinishedAt, new DateTimeOffset(now, TimeSpan.Zero))
            .SetProperty(j => j.LeaseOwner, (Guid?)null)
            .SetProperty(j => j.LeaseUntil, (DateTime?)null)
            .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;

    public async Task<bool> RetryAsync(Guid id, Guid owner, DateTime now, DateTime nextAttemptAt,
        string error, CancellationToken cancellationToken)
        => await Owned(id, owner, now).ExecuteUpdateAsync(setters => setters
            .SetProperty(j => j.Status, ImportJobStatus.Pending)
            .SetProperty(j => j.NextAttemptAt, nextAttemptAt)
            .SetProperty(j => j.LeaseOwner, (Guid?)null)
            .SetProperty(j => j.LeaseUntil, (DateTime?)null)
            .SetProperty(j => j.Error, error)
            .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;

    public async Task<bool> FailAsync(Guid id, Guid owner, DateTime now, string error, CancellationToken cancellationToken)
        => await Owned(id, owner, now).ExecuteUpdateAsync(setters => setters
            .SetProperty(j => j.Status, ImportJobStatus.Failed)
            .SetProperty(j => j.FinishedAt, new DateTimeOffset(now, TimeSpan.Zero))
            .SetProperty(j => j.LeaseOwner, (Guid?)null)
            .SetProperty(j => j.LeaseUntil, (DateTime?)null)
            .SetProperty(j => j.Error, error)
            .SetProperty(j => j.Version, j => j.Version + 1), cancellationToken) == 1;

    private IQueryable<ImportJob> Owned(Guid id, Guid owner, DateTime now)
        => context.ImportJobs.Where(j => j.Id == id && j.Status == ImportJobStatus.Running
            && j.LeaseOwner == owner && j.LeaseUntil > now);
}
