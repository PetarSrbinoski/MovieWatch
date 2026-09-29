namespace MovieWatch.Service.Interface;

public interface IImportProcessor
{
    Task<bool> ProcessOneAsync(CancellationToken cancellationToken);
}
