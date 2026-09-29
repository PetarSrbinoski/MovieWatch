using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface ITmdbClient
{
    Task<IReadOnlyList<long>> DiscoverMovieIdsAsync(int page, CancellationToken cancellationToken);
    Task<ImportedMovieDto?> GetMovieAsync(long tmdbId, CancellationToken cancellationToken);
}
