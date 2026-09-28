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
    public async Task Genre_sync_requires_admin_and_updates_full_list_without_duplicate_ids()
    {
        var source = new StubTmdbClient();
        using var factory = new ImportFactory(source);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/genres/sync", null)).StatusCode);
        await ApiAccounts.RegisterAsync(client, "viewer@example.com");
        await ApiAccounts.SignInAsync(client, "viewer@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/genres/sync", null)).StatusCode);
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/movies")).EnumerateArray());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/genres")).EnumerateArray());
        var first = await client.PostAsync("/api/genres/sync", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());
        var genres = (await client.GetFromJsonAsync<JsonElement>("/api/genres")).EnumerateArray().ToArray();
        var originalId = Id(genres.Single(g => g.GetProperty("tmdbId").GetInt64() == 1));
        source.GenreName = "Updated comedy";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/genres/sync", null)).StatusCode);
        var updated = (await client.GetFromJsonAsync<JsonElement>("/api/genres")).EnumerateArray().ToArray();
        Assert.Equal(2, updated.Length);
        Assert.Equal(originalId, Id(updated.Single(g => g.GetProperty("name").GetString() == "Updated comedy")));
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/movies")).EnumerateArray());
    }

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
        Assert.Equal("/first-poster.jpg", movie.GetProperty("posterPath").GetString());
        source.PosterPath = "/updated-poster.jpg";
        source.Title = "Updated title";
        var second = await SubmitAsync(client);
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default));
        var updated = await client.GetFromJsonAsync<JsonElement>("/api/movies");
        Assert.Equal("Updated title", updated.EnumerateArray().Single().GetProperty("title").GetString());
        Assert.Equal(Id(movie), Id(updated.EnumerateArray().Single()));
        Assert.Equal("/updated-poster.jpg", updated.EnumerateArray().Single().GetProperty("posterPath").GetString());
        var finishedSecond = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(second)}");
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/import-jobs/{Id(second)}?version={finishedSecond.GetProperty("version").GetInt32()}")).StatusCode);
    }

    [Fact]
    public async Task Import_keeps_manual_genre_and_its_movie_separate_from_external_genre()
    {
        var source = new StubTmdbClient();
        using var factory = new ImportFactory(source);
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var manualGenreResponse = await client.PostAsJsonAsync("/api/genres", new { name = "Comedy" });
        Assert.Equal(HttpStatusCode.Created, manualGenreResponse.StatusCode);
        var manualGenre = await manualGenreResponse.Content.ReadFromJsonAsync<JsonElement>();
        var manualMovieResponse = await client.PostAsJsonAsync("/api/movies", new
        {
            title = "Local comedy", overview = "Manual", runtimeMinutes = 90,
            releaseDate = "2020-01-01", genreIds = new[] { Id(manualGenre) }
        });
        Assert.Equal(HttpStatusCode.Created, manualMovieResponse.StatusCode);
        var manualMovie = await manualMovieResponse.Content.ReadFromJsonAsync<JsonElement>();
        await SubmitAsync(client);
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default));

        var genres = (await client.GetFromJsonAsync<JsonElement>("/api/genres")).EnumerateArray().ToArray();
        Assert.Equal(2, genres.Length);
        Assert.Null(genres.Single(g => Id(g) == Id(manualGenre)).GetProperty("tmdbId").GetString());
        Assert.Equal(1, genres.Single(g => Id(g) != Id(manualGenre)).GetProperty("tmdbId").GetInt64());

        source.GenreName = "Film comedy";
        await SubmitAsync(client);
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default));
        var manualAfter = await client.GetFromJsonAsync<JsonElement>($"/api/movies/{Id(manualMovie)}");
        Assert.Equal(Id(manualGenre), Id(manualAfter.GetProperty("genres")[0]));
        Assert.Equal("Comedy", manualAfter.GetProperty("genres")[0].GetProperty("name").GetString());
        var after = (await client.GetFromJsonAsync<JsonElement>("/api/genres")).EnumerateArray().ToArray();
        Assert.Equal("Film comedy", after.Single(g => g.GetProperty("tmdbId").ValueKind != JsonValueKind.Null)
            .GetProperty("name").GetString());
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
    public async Task Retry_after_is_capped_at_one_hour()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var source = new StubTmdbClient { FailuresRemaining = 1, Retryable = true, RetryAfter = TimeSpan.FromDays(5) };
        using var factory = new ImportFactory(source, clock);
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var job = await SubmitAsync(client);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IImportProcessor>().ProcessOneAsync(default));
        var pending = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(job)}");
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddHours(1), pending.GetProperty("nextAttemptAt").GetDateTime());
    }

    [Fact]
    public async Task Earlier_movie_survives_later_source_failure_and_retry_upserts_without_duplicates()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        var source = new StubTmdbClient { Ids = [42, 43], DetailFailuresRemaining = 1 };
        using var factory = new ImportFactory(source, clock);
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        var job = await SubmitAsync(client);
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IImportProcessor>();
        Assert.True(await processor.ProcessOneAsync(default));
        Assert.Equal("Pending", (await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(job)}"))
            .GetProperty("status").GetString());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/movies")).EnumerateArray());

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(await processor.ProcessOneAsync(default));
        var completed = await client.GetFromJsonAsync<JsonElement>($"/api/import-jobs/{Id(job)}");
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        var movies = (await client.GetFromJsonAsync<JsonElement>("/api/movies")).EnumerateArray().ToArray();
        Assert.Equal(new long[] { 42, 43 }, movies.Select(movie => movie.GetProperty("tmdbId").GetInt64()).Order().ToArray());
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

    private static Guid Id(JsonElement value)
    {
        return value.GetProperty("id").GetGuid();
    }

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
        public Task<IReadOnlyList<ImportedGenreDto>> GetGenresAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ImportedGenreDto>>([new ImportedGenreDto(1, GenreName), new ImportedGenreDto(2, "Drama")]);

        public string Title { get; set; } = "First title";
        public string? PosterPath { get; set; } = "/first-poster.jpg";
        public string GenreName { get; set; } = "Comedy";
        public IReadOnlyList<long> Ids { get; set; } = [42, 42];
        public int FailuresRemaining { get; set; }
        public int DetailFailuresRemaining { get; set; }
        public bool Retryable { get; set; }
        public TimeSpan RetryAfter { get; set; } = TimeSpan.FromMinutes(5);
        public Task<IReadOnlyList<long>> DiscoverMovieIdsAsync(int page, CancellationToken cancellationToken)
        {
            if (FailuresRemaining-- > 0)
                throw new ImportSourceException("Controlled TMDB failure.", Retryable, RetryAfter);
            return Task.FromResult(Ids);
        }
        public Task<ImportedMovieDto?> GetMovieAsync(long tmdbId, CancellationToken cancellationToken)
        {
            if (tmdbId == 43 && DetailFailuresRemaining-- > 0)
                throw new ImportSourceException("Controlled detail failure.", true, RetryAfter);
            return Task.FromResult<ImportedMovieDto?>(new ImportedMovieDto(tmdbId, Title, "Overview", 90,
                new DateOnly(2020, 1, 1), 100, [new ImportedGenreDto(1, GenreName)], PosterPath));
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
        public void Advance(TimeSpan duration)
        {
            _now += duration;
        }
    }
}
