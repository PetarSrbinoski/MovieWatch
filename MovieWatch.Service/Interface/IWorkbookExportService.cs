using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface IWorkbookExportService
{
    byte[] CreateWorkbook(RecommendationResultDto result);
}
