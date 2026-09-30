using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MovieWatch.Tests.Hosting;

public class MovieWatchFactory(string? databasePath = null, IReadOnlyDictionary<string, string?>? overrides = null) : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = databasePath ?? Path.Combine(Path.GetTempPath(), $"moviewatch-{Guid.NewGuid()}.db");
    public const string SigningKey = "test-only-signing-key-with-at-least-32-bytes";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:MovieWatch"] = $"Data Source={DatabasePath};Pooling=False",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = "MovieWatch.Tests",
                ["Jwt:Audience"] = "MovieWatch.Tests",
                ["Jwt:LifetimeMinutes"] = "15",
                ["Administrator:Enabled"] = "false",
                ["Logging:LogLevel:Default"] = "Warning"
            }).AddInMemoryCollection(overrides ?? new Dictionary<string, string?>()));
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(AuthorizationProbeController).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && databasePath is null)
        {
            File.Delete(DatabasePath);
            File.Delete(DatabasePath + "-wal");
            File.Delete(DatabasePath + "-shm");
        }
    }
}
