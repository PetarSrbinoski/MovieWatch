using Microsoft.Extensions.DependencyInjection;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class ImportProcessor(
    IImportJobRepository jobs, ITmdbClient tmdb, IServiceScopeFactory scopes,
    TimeProvider clock) : IImportProcessor
{
    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        var owner = Guid.NewGuid();
        var job = await jobs.ClaimNextAsync(owner, Now(), cancellationToken);
        if (job is null)
            return false;
        using var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = HeartbeatAsync(job.Id, owner, work);
        try
        {
            var ids = new HashSet<long>();
            for (var page = 1; page <= job.PageCount; page++)
            {
                foreach (var id in await tmdb.DiscoverMovieIdsAsync(page, work.Token))
                    ids.Add(id);
            }
            foreach (var id in ids)
            {
                work.Token.ThrowIfCancellationRequested();
                var movie = await tmdb.GetMovieAsync(id, work.Token);
                if (movie is null)
                {
                    await RecordAsync(job.Id, owner, false, work.Token);
                    continue;
                }
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ICatalogueImportRepository>()
                        .UpsertAsync(movie, clock.GetUtcNow(), work.Token);
                    await RecordAsync(job.Id, owner, true, work.Token);
                }
                catch (ImportLoadException exception)
                {
                    Console.Error.WriteLine(exception);
                    await RecordAsync(job.Id, owner, false, work.Token);
                }
            }
            if (!await jobs.CompleteAsync(job.Id, owner, Now(), work.Token))
                throw new OperationCanceledException("Import lease was lost.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImportSourceException exception)
        {
            Console.Error.WriteLine(exception);
            if (exception.Transient && job.AttemptCount < 3)
            {
                var baseDelay = TimeSpan.FromSeconds(5 * (1 << (job.AttemptCount - 1)));
                var delay = exception.RetryAfter is { } retry && retry > baseDelay ? retry : baseDelay;
                if (delay > TimeSpan.FromHours(1))
                    delay = TimeSpan.FromHours(1);
                await jobs.RetryAsync(job.Id, owner, Now(), Now() + delay, exception.Message, cancellationToken);
            }
            else
                await jobs.FailAsync(job.Id, owner, Now(), exception.Message, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            await jobs.FailAsync(job.Id, owner, Now(), "Import failed while processing catalogue data.", cancellationToken);
        }
        finally
        {
            await work.CancelAsync();
            try { await heartbeat; }
            catch (OperationCanceledException) { }
        }
        return true;
    }

    private async Task RecordAsync(Guid id, Guid owner, bool imported, CancellationToken cancellationToken)
    {
        if (!await jobs.RecordProgressAsync(id, owner, Now(), imported, cancellationToken))
            throw new OperationCanceledException("Import lease was lost.");
    }

    private async Task HeartbeatAsync(Guid id, Guid owner, CancellationTokenSource work)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(work.Token))
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
            if (!await repository.RenewAsync(id, owner, Now(), work.Token))
            {
                await work.CancelAsync();
                return;
            }
        }
    }

    private DateTime Now()
    {
        return clock.GetUtcNow().UtcDateTime;
    }
}
