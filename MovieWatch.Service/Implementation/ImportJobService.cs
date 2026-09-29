using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class ImportJobService(IImportJobRepository jobs, TimeProvider clock) : IImportJobService
{
    public async Task<ImportJobDto> CreateAsync(int pageCount, CancellationToken cancellationToken)
    {
        ValidatePages(pageCount);
        return ToDto(await jobs.CreateAsync(pageCount, clock.GetUtcNow(), cancellationToken));
    }

    public async Task<List<ImportJobDto>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        if (skip < 0 || take is < 1 or > 100)
            throw new OperationException(FailureKind.Validation, "Use nonnegative skip and take from 1 to 100.");
        return (await jobs.ListAsync(skip, take, cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<ImportJobDto> GetAsync(Guid id, CancellationToken cancellationToken)
        => ToDto(await jobs.GetAsync(id, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Import job was not found."));

    public async Task<ImportJobDto> UpdateAsync(Guid id, int version, int pageCount, CancellationToken cancellationToken)
    {
        ValidatePages(pageCount);
        if (!await jobs.UpdatePendingAsync(id, version, pageCount, cancellationToken))
            await ThrowNotEditableAsync(id, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        if (!await jobs.DeleteEditableAsync(id, version, cancellationToken))
            await ThrowNotEditableAsync(id, cancellationToken);
    }

    private async Task ThrowNotEditableAsync(Guid id, CancellationToken cancellationToken)
    {
        _ = await GetAsync(id, cancellationToken);
        throw new OperationException(FailureKind.Conflict, "The job changed or its current state prevents this operation.");
    }

    private static void ValidatePages(int pageCount)
    {
        if (pageCount is < 1 or > 5)
            throw new OperationException(FailureKind.Validation, "Page count must be 1 to 5.");
    }

    private static ImportJobDto ToDto(ImportJob job) => new(job.Id, job.PageCount, job.Status,
        job.AttemptCount, job.CreatedAt, job.StartedAt, job.FinishedAt, job.NextAttemptAt,
        job.LeaseUntil, job.ImportedCount, job.SkippedCount, job.Error, job.Version);
}
