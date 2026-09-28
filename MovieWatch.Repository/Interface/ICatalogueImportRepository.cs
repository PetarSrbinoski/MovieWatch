using MovieWatch.Domain.Dto;

namespace MovieWatch.Repository.Interface;

public interface ICatalogueImportRepository
{
    Task UpsertGenresAsync(IReadOnlyList<ImportedGenreDto> genres, CancellationToken cancellationToken);
    Task UpsertAsync(ImportedMovieDto imported, DateTimeOffset now, CancellationToken cancellationToken);
}
