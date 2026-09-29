using MovieWatch.Domain.Models;

namespace MovieWatch.Repository.Interface;

public interface IImportJobRepository
{
    Task<ImportJob> CreateAsync(int pageCount, DateTimeOffset now, CancellationToken cancellationToken);
    Task<List<ImportJob>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UpdatePendingAsync(Guid id, int expectedVersion, int pageCount, CancellationToken cancellationToken);
    Task<bool> DeleteEditableAsync(Guid id, int expectedVersion, CancellationToken cancellationToken);
    Task<ImportJob?> ClaimNextAsync(Guid owner, DateTime now, CancellationToken cancellationToken);
    Task<bool> RenewAsync(Guid id, Guid owner, DateTime now, CancellationToken cancellationToken);
    Task<bool> RecordProgressAsync(Guid id, Guid owner, DateTime now, bool imported, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(Guid id, Guid owner, DateTime now, CancellationToken cancellationToken);
    Task<bool> RetryAsync(Guid id, Guid owner, DateTime now, DateTime nextAttemptAt, string error, CancellationToken cancellationToken);
    Task<bool> FailAsync(Guid id, Guid owner, DateTime now, string error, CancellationToken cancellationToken);
}
