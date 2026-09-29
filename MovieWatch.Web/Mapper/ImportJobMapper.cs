using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Mapper;

public sealed class ImportJobMapper(IImportJobService service)
{
    public async Task<List<ImportJobResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => (await service.ListAsync(skip, take, cancellationToken)).Select(j => j.ToResponse()).ToList();

    public async Task<ImportJobResponse> GetAsync(Guid id, CancellationToken cancellationToken)
        => (await service.GetAsync(id, cancellationToken)).ToResponse();

    public async Task<ImportJobResponse> CreateAsync(CreateImportJobRequest request, CancellationToken cancellationToken)
        => (await service.CreateAsync(request.PageCount, cancellationToken)).ToResponse();

    public async Task<ImportJobResponse> UpdateAsync(Guid id, UpdateImportJobRequest request,
        CancellationToken cancellationToken)
        => (await service.UpdateAsync(id, request.Version, request.PageCount, cancellationToken)).ToResponse();

    public Task DeleteAsync(Guid id, int version, CancellationToken cancellationToken)
        => service.DeleteAsync(id, version, cancellationToken);
}

public static class ImportJobMappingExtensions
{
    public static ImportJobResponse ToResponse(this ImportJobDto dto) => new(dto.Id, dto.PageCount,
        dto.Status, dto.AttemptCount, dto.CreatedAt, dto.StartedAt, dto.FinishedAt,
        dto.NextAttemptAt, dto.LeaseUntil, dto.ImportedCount, dto.SkippedCount, dto.Error, dto.Version);
}
