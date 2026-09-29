using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface IImportJobService
{
    Task<ImportJobDto> CreateAsync(int pageCount, CancellationToken cancellationToken);
    Task<List<ImportJobDto>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task<ImportJobDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ImportJobDto> UpdateAsync(Guid id, int version, int pageCount, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, int version, CancellationToken cancellationToken);
}
