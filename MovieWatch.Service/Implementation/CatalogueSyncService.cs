using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class CatalogueSyncService(ITmdbClient tmdb, ICatalogueImportRepository catalogue) : ICatalogueSyncService
{
    public async Task<int> SyncGenresAsync(CancellationToken cancellationToken)
    {
        var genres = await tmdb.GetGenresAsync(cancellationToken);
        await catalogue.UpsertGenresAsync(genres, cancellationToken);
        return genres.Count;
    }
}
