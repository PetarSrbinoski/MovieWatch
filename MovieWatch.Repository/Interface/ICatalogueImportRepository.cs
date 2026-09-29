using MovieWatch.Domain.Dto;

namespace MovieWatch.Repository.Interface;

public interface ICatalogueImportRepository
{
    Task UpsertAsync(ImportedMovieDto imported, DateTimeOffset now, CancellationToken cancellationToken);
}
