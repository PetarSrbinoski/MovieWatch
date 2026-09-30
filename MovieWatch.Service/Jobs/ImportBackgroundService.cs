using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Jobs;

public sealed class ImportBackgroundService(IServiceScopeFactory scopes, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Import:WorkerEnabled", true))
            return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IImportProcessor>();
                var processed = await processor.ProcessOneAsync(stoppingToken);
                if (!processed)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
