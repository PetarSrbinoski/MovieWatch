namespace MovieWatch.Service.Interface;

public interface ICatalogueSyncService
{
    Task<int> SyncGenresAsync(CancellationToken cancellationToken);
}
