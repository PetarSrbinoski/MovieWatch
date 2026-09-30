using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieWatch.Domain.Dto;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;
using MovieWatch.Service.Implementation;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public sealed class ImportWorkflowTests
{
    [Fact]
    public async Task Persisted_job_imports_movies_and_reimport_updates_without_duplicates()
    {
        var source = new StubTmdbClient();
        using var factory = new ImportFactory(source);
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var first = await SubmitAsync(client);
        Assert.Equal("Pending", first.GetProperty("status").GetString());
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default));
        var done = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(first)}");
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal(1, done.GetProperty("importedCount").GetInt32());
        var catalogue = await client.GetFromJsonAsync<JsonElement>("/api/movies");
        var movie = catalogue.EnumerateArray().Single();
        Assert.Equal("First title", movie.GetProperty("title").GetString());
        source.Title = "Updated title";
        var second = await SubmitAsync(client);
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default));
        var updated = await client.GetFromJsonAsync<JsonElement>("/api/movies");
        Assert.Equal("Updated title", updated.EnumerateArray().Single().GetProperty("title").GetString());
        Assert.Equal(Id(movie), Id(updated.EnumerateArray().Single()));
        var finishedSecond = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(second)}");
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/import-jobs/{Id(second)}?version={finishedSecond.GetProperty("version").GetInt32()}")).StatusCode);
    }

    [Fact]
    public async Task Expired_lease_can_be_reclaimed_and_stale_owner_cannot_complete()
    {
        using var factory = new ImportFactory(new StubTmdbClient());
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var job = await SubmitAsync(client);
        using var scope = factory.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        var firstOwner = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var claimed = await jobs.ClaimNextAsync(firstOwner, now, default);
        Assert.Equal(Id(job), claimed?.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/import-jobs/{Id(job)}",
            new { pageCount = 2, version = claimed!.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.DeleteAsync($"/api/import-jobs/{Id(job)}?version={claimed.Version}")).StatusCode);
        Assert.Null(await jobs.ClaimNextAsync(Guid.NewGuid(), now.AddMinutes(1), default));
        var secondOwner = Guid.NewGuid();
        Assert.Equal(Id(job), (await jobs.ClaimNextAsync(secondOwner, now.AddMinutes(3), default))?.Id);
        Assert.False(await jobs.CompleteAsync(Id(job), firstOwner, now.AddMinutes(3), default));
        Assert.True(await jobs.CompleteAsync(Id(job), secondOwner, now.AddMinutes(3), default));
    }

    [Fact]
    public async Task Transient_failure_waits_for_retry_after_and_finishes_on_second_attempt()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var source = new StubTmdbClient { FailuresRemaining = 1, Retryable = true };
        using var factory = new ImportFactory(source, clock);
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var submitted = await SubmitAsync(client);
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IImportProcessor>();
        Assert.True(await processor.ProcessOneAsync(default));
        var pending = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(submitted)}");
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal(1, pending.GetProperty("attemptCount").GetInt32());
        Assert.True(pending.GetProperty("nextAttemptAt").GetDateTime() >= clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        Assert.False(await processor.ProcessOneAsync(default));
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(await processor.ProcessOneAsync(default));
        var completed = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(submitted)}");
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.Equal(2, completed.GetProperty("attemptCount").GetInt32());
    }

    [Fact]
    public async Task Permanent_errors_fail_immediately_and_transient_errors_stop_after_three_attempts()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var source = new StubTmdbClient { FailuresRemaining = 1, Retryable = false };
        using var factory = new ImportFactory(source, clock);
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var permanent = await SubmitAsync(client);
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IImportProcessor>();
        Assert.True(await processor.ProcessOneAsync(default));
        var failed = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(permanent)}");
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal(1, failed.GetProperty("attemptCount").GetInt32());
        source.FailuresRemaining = 3;
        source.Retryable = true;
        var exhausted = await SubmitAsync(client);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.True(await processor.ProcessOneAsync(default));
            clock.Advance(TimeSpan.FromMinutes(5));
        }
        var terminal = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(exhausted)}");
        Assert.Equal("Failed", terminal.GetProperty("status").GetString());
        Assert.Equal(3, terminal.GetProperty("attemptCount").GetInt32());
    }

    [Fact]
    public async Task Restart_recovers_expired_running_job_without_duplicate_movies()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moviewatch-recovery-{Guid.NewGuid()}.db");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var source = new StubTmdbClient();
        Guid jobId;
        try
        {
            using (var first = new ImportFactory(source, clock, path))
            {
                using var client = first.CreateClient();
                await ApiAccounts.SignInAsync(client, "admin@example.com");
                jobId = Id(await SubmitAsync(client));
                using var scope = first.Services.CreateScope();
                Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IImportJobRepository>()
                    .ClaimNextAsync(Guid.NewGuid(), clock.GetUtcNow().UtcDateTime, default));
            }
            clock.Advance(TimeSpan.FromMinutes(3));
            using var restarted = new ImportFactory(source, clock, path);
            using var restartedClient = restarted.CreateClient();
            await ApiAccounts.SignInAsync(restartedClient, "admin@example.com");
            using (var scope = restarted.Services.CreateScope())
            {
                var current = await scope.ServiceProvider.GetRequiredService<IImportJobRepository>().GetAsync(jobId, default);
                Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default),
                    $"{current?.Status}, lease {current?.LeaseUntil:o}, now {clock.GetUtcNow():o}");
            }
            var completed = await restartedClient.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{jobId}");
            Assert.Equal("Completed", completed.GetProperty("status").GetString());
            Assert.Equal(2, completed.GetProperty("attemptCount").GetInt32());
            Assert.Single((await restartedClient.GetFromJsonAsync<JsonElement>("/api/movies")).EnumerateArray());
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    private static Guid Id(JsonElement value) => value.GetProperty("id").GetGuid();

    private static async Task<JsonElement> SubmitAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/import-jobs", new { pageCount = 1 });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class ImportFactory(StubTmdbClient source, ManualTimeProvider? clock = null,
        string? databasePath = null) : MovieWatchFactory(databasePath, overrides: new Dictionary<string, string?>
    {
        ["Administrator:Enabled"] = "true",
        ["Administrator:Email"] = "admin@example.com",
        ["Administrator:Password"] = ApiAccounts.Password,
        ["Administrator:DisplayName"] = "Administrator",
        ["Import:WorkerEnabled"] = "false"
    })
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITmdbClient>();
                services.AddSingleton<ITmdbClient>(source);
                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }
            });
        }
    }

    private sealed class StubTmdbClient : ITmdbClient
    {
        public string Title { get; set; } = "First title";
        public int FailuresRemaining { get; set; }
        public bool Retryable { get; set; }
        public Task<IReadOnlyList<long>> DiscoverMovieIdsAsync(int page, CancellationToken cancellationToken)
        {
            if (FailuresRemaining-- > 0)
                throw new ImportSourceException("Controlled TMDB failure.", Retryable, TimeSpan.FromMinutes(5));
            return Task.FromResult<IReadOnlyList<long>>([42, 42]);
        }
        public Task<ImportedMovieDto?> GetMovieAsync(long tmdbId, CancellationToken cancellationToken)
            => Task.FromResult<ImportedMovieDto?>(new ImportedMovieDto(tmdbId, Title, "Overview", 90,
                new DateOnly(2020, 1, 1), 100, [new ImportedGenreDto(1, "Comedy")]));
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
