using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MovieWatch.Repository;
using MovieWatch.Domain.Models;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public sealed class MoodRecommendationTests
{
    [Fact]
    public async Task Upgrade_preserves_existing_moods_and_adds_presets_only_once()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mood-upgrade-{Guid.NewGuid()}.db");
        try
        {
            await using var database = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False").Options);
            await database.GetService<IMigrator>().MigrateAsync("20260930145116_MoviePosters");
            await database.Database.ExecuteSqlRawAsync("""
                INSERT INTO Moods (Id, Name, Description, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES ('BB000000-0000-0000-0000-000000000001', 'My existing mood', 'Keep me',
                    '2026-09-01 00:00:00+00:00', '2026-09-01 00:00:00+00:00', 'test', 'test');
                """);
            await database.Database.MigrateAsync();
            Assert.Equal(12, await database.Moods.CountAsync());
            var existing = await database.Moods.SingleAsync(m => m.Name == "My existing mood");
            Assert.Equal("Keep me", existing.Description);
            Assert.Null(existing.PresetKey);
            var seeded = await database.Moods.SingleAsync(m => m.PresetKey == "laugh");
            database.Moods.Remove(seeded);
            await database.SaveChangesAsync();
            await database.Database.MigrateAsync();
            Assert.Equal(11, await database.Moods.CountAsync());
            Assert.False(await database.Moods.AnyAsync(m => m.PresetKey == "laugh"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Legacy_cleanup_preserves_existing_preset_preferences_and_moves_other_preferences()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mood-cleanup-{Guid.NewGuid()}.db");
        try
        {
            await using var database = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False").Options);
            await database.GetService<IMigrator>().MigrateAsync("20260930150629_MoodPresets");
            var account = new IdentityUser("migration-test");
            var viewer = new Viewer(account.Id, "Test viewer");
            var comedy = new Genre("Comedy");
            var horror = new Genre("Horror");
            var old = new Mood("Relaxing", "Legacy mood");
            var custom = new Mood("Personal occasion", "Keep this custom mood");
            var laugh = await database.Moods.SingleAsync(m => m.PresetKey == "laugh");
            database.AddRange(account, viewer, comedy, horror, old, custom);
            database.AddRange(new GenrePreference(viewer.Id, comedy.Id, old.Id, 2),
                new GenrePreference(viewer.Id, comedy.Id, laugh.Id, 0),
                new GenrePreference(viewer.Id, horror.Id, old.Id, -2));
            await database.SaveChangesAsync();
            await database.Database.MigrateAsync();
            database.ChangeTracker.Clear();
            Assert.False(await database.Moods.AnyAsync(m => m.Id == old.Id));
            Assert.True(await database.Moods.AnyAsync(m => m.Id == custom.Id));
            Assert.Equal(11, await database.Moods.CountAsync(m => m.PresetKey != null));
            var preferences = await database.GenrePreferences.ToListAsync();
            Assert.Equal(2, preferences.Count);
            Assert.All(preferences, p => Assert.Equal(laugh.Id, p.MoodId));
            Assert.Equal(0, preferences.Single(p => p.GenreId == comedy.Id).Weight);
            Assert.Equal(-2, preferences.Single(p => p.GenreId == horror.Id).Weight);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Every_catalogue_genre_can_produce_a_positive_match_without_personal_preferences()
    {
        using var fixture = new Fixture();
        await fixture.Initialize();
        var genreNames = new[] { "Action", "Adventure", "Animation", "Comedy", "Crime", "Documentary",
            "Drama", "Family", "Fantasy", "History", "Horror", "Music", "Mystery", "Romance",
            "Science Fiction", "TV Movie", "Thriller", "War", "Western" };
        foreach (var genreName in genreNames)
            await fixture.Movie(genreName, await fixture.Genre(genreName));
        var moods = await fixture.Viewer.GetFromJsonAsync<JsonElement>("/api/moods");
        Assert.Equal(11, moods.GetArrayLength());
        var matched = new HashSet<string>();
        foreach (var mood in moods.EnumerateArray())
        {
            var rows = Rows(await fixture.Recommend(mood.GetProperty("id").GetGuid(), "&limit=50"));
            Assert.NotEmpty(rows);
            foreach (var row in rows)
            {
                Assert.True(row.GetProperty("score").GetDouble() > 0);
                matched.Add(Title(row));
            }
        }
        Assert.Equal(genreNames.Order(), matched.Order());
    }

    [Fact]
    public async Task Imported_genres_match_by_external_identity_and_scores_average_all_movie_genres()
    {
        using var fixture = new Fixture();
        await fixture.Initialize();
        var mood = await fixture.Mood("laugh");
        var comedy = await fixture.Genre("Localized comedy name");
        var drama = await fixture.Genre("Drama");
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await database.Genres.SingleAsync(g => g.Id == comedy)).TmdbId = 35;
            await database.SaveChangesAsync();
        }
        await fixture.Create(fixture.Admin, "/api/movies", new
        {
            title = "Mixed genres", overview = "", runtimeMinutes = 90,
            releaseDate = "2020-01-01", genreIds = new[] { comedy, drama }
        });
        var row = Assert.Single(Rows(await fixture.Recommend(mood)));
        Assert.Equal(1, row.GetProperty("score").GetDouble());
        Assert.Contains("Localized comedy name", row.GetProperty("explanation").GetString());
    }

    [Fact]
    public async Task Recommendations_page_positive_matches_before_optional_other_options()
    {
        using var fixture = new Fixture();
        await fixture.Initialize();
        var laugh = await fixture.Mood("laugh");
        var scare = await fixture.Mood("scare");
        var comedy = await fixture.Genre("Comedy");
        var horror = await fixture.Genre("Horror");
        for (var index = 0; index < 8; index++) await fixture.Movie($"Comedy {index}", comedy);
        await fixture.Movie("Horror", horror);
        await fixture.Movie("Too long", comedy, 180);
        var happy = await fixture.Recommend(laugh);
        Assert.Equal(6, Rows(happy).Length);
        Assert.Equal(8, happy.GetProperty("positiveMatchCount").GetInt32());
        Assert.Equal(1, happy.GetProperty("otherOptionCount").GetInt32());
        Assert.All(Rows(happy), row => Assert.StartsWith("Comedy", Title(row)));
        var second = await fixture.Recommend(laugh, "&skip=6");
        Assert.Equal(2, Rows(second).Length);
        Assert.Equal(8, second.GetProperty("totalCount").GetInt32());
        Assert.Equal(6, second.GetProperty("skip").GetInt32());
        Assert.Equal(6, second.GetProperty("limit").GetInt32());
        Assert.Empty(Rows(happy).Select(Title).Intersect(Rows(second).Select(Title)));
        Assert.Equal(Rows(happy).Select(Title), Rows(await fixture.Recommend(laugh)).Select(Title));
        var scary = await fixture.Recommend(scare);
        Assert.Equal("Horror", Title(Assert.Single(Rows(scary))));
        var expanded = await fixture.Recommend(laugh, "&includeOtherOptions=true");
        Assert.Equal(6, Rows(expanded).Length);
        Assert.Equal(9, expanded.GetProperty("totalCount").GetInt32());
        Assert.Equal(Rows(happy).Select(Title), Rows(expanded).Select(Title));
        var expandedSecond = await fixture.Recommend(laugh, "&includeOtherOptions=true&skip=6");
        Assert.Equal(3, Rows(expandedSecond).Length);
        Assert.Equal("Horror", Title(Rows(expandedSecond).Last()));
        Assert.Contains("Other option", Rows(expandedSecond).Last().GetProperty("explanation").GetString());
        Assert.Empty(Rows(await fixture.Recommend(laugh, "&includeOtherOptions=true&skip=9")));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.Viewer.GetAsync($"{fixture.Path}/recommendations?moodId={laugh}&maximumRuntimeMinutes=120&skip=-1")).StatusCode);

        var group = await fixture.Create(fixture.Viewer, "/api/groups", new { name = "Movie night", description = "" });
        var groupPath = $"/api/groups/{group}/recommendations?moodId={laugh}&maximumRuntimeMinutes=120&includeOtherOptions=true&skip=6";
        var groupPage = await fixture.Viewer.GetFromJsonAsync<JsonElement>(groupPath);
        Assert.Equal(Rows(expandedSecond).Select(Title), Rows(groupPage).Select(Title));
        var export = await fixture.Viewer.GetAsync(groupPath.Replace("/recommendations?", "/recommendations.xlsx?"));
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
        Assert.Equal(Rows(groupPage).Select(Title), Enumerable.Range(10, 3).Select(row => workbook.Worksheet(1).Cell(row, 1).GetString()));
        Assert.True(workbook.Worksheet(1).Cell(13, 1).IsEmpty());
    }

    [Fact]
    public async Task Explicit_neutral_and_negative_override_defaults_and_removal_restores_the_preset()
    {
        using var fixture = new Fixture();
        await fixture.Initialize();
        var mood = await fixture.Mood("laugh");
        var comedy = await fixture.Genre("Comedy");
        await fixture.Movie("Comedy", comedy);
        var preference = await fixture.Create(fixture.Viewer, $"{fixture.Path}/preferences", new { moodId = mood, genreId = comedy, weight = 0 });
        Assert.Empty(Rows(await fixture.Recommend(mood)));
        var neutral = Assert.Single(Rows(await fixture.Recommend(mood, "&includeOtherOptions=true")));
        Assert.Equal(0, neutral.GetProperty("score").GetDouble());
        Assert.True(neutral.GetProperty("genreContributions")[0].GetProperty("isPersonal").GetBoolean());
        (await fixture.Viewer.PutAsJsonAsync($"{fixture.Path}/preferences/{preference}", new { weight = -2 })).EnsureSuccessStatusCode();
        Assert.Empty(Rows(await fixture.Recommend(mood)));
        Assert.Equal(-2, Assert.Single(Rows(await fixture.Recommend(mood, "&includeOtherOptions=true"))).GetProperty("score").GetDouble());
        (await fixture.Viewer.DeleteAsync($"{fixture.Path}/preferences/{preference}")).EnsureSuccessStatusCode();
        Assert.Equal(2, Assert.Single(Rows(await fixture.Recommend(mood))).GetProperty("score").GetDouble());
    }

    [Fact]
    public async Task Preset_survives_renaming_and_custom_moods_require_personal_preferences()
    {
        using var fixture = new Fixture();
        await fixture.Initialize();
        var mood = await fixture.Mood("laugh");
        var comedy = await fixture.Genre("Comedy");
        await fixture.Movie("Comedy", comedy);
        (await fixture.Admin.PutAsJsonAsync($"/api/moods/{mood}", new { name = "Friday laughs", description = "", presetKey = "laugh" })).EnsureSuccessStatusCode();
        Assert.Single(Rows(await fixture.Recommend(mood)));
        var custom = await fixture.Create(fixture.Admin, "/api/moods", new { name = "My custom mood", description = "Comedy is just text here" });
        Assert.Empty(Rows(await fixture.Recommend(custom)));
        await fixture.Create(fixture.Viewer, $"{fixture.Path}/preferences", new { moodId = custom, genreId = comedy, weight = 2 });
        Assert.Single(Rows(await fixture.Recommend(custom)));
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Admin.PostAsJsonAsync("/api/moods", new { name = "Invalid", description = "", presetKey = "unknown" })).StatusCode);
    }

    [Fact]
    public async Task Group_averages_each_viewers_effective_preferences_and_excludes_watched_movies()
    {
        using var fixture = new Fixture();
        await fixture.Initialize();
        var mood = await fixture.Mood("laugh");
        var comedy = await fixture.Genre("Comedy");
        var movie = await fixture.Movie("Comedy", comedy);
        using var second = fixture.Factory.CreateClient();
        var profile = await ApiAccounts.RegisterAsync(second, "second@example.com", "Second");
        await ApiAccounts.SignInAsync(second, "second@example.com");
        var secondId = profile.GetProperty("id").GetGuid();
        await fixture.Create(second, $"/api/viewers/{secondId}/preferences", new { moodId = mood, genreId = comedy, weight = -2 });
        var group = await fixture.Create(fixture.Viewer, "/api/groups", new { name = "Tonight", description = "" });
        await fixture.Create(fixture.Viewer, $"/api/groups/{group}/members", new { viewerId = secondId });
        var path = $"/api/groups/{group}/recommendations?moodId={mood}&maximumRuntimeMinutes=120";
        Assert.Empty(Rows(await fixture.Viewer.GetFromJsonAsync<JsonElement>(path)));
        var expanded = await fixture.Viewer.GetFromJsonAsync<JsonElement>(path + "&includeOtherOptions=true");
        var row = Assert.Single(Rows(expanded));
        Assert.Equal(0, row.GetProperty("score").GetDouble());
        Assert.Equal(new double[] { -2, 2 }, row.GetProperty("memberScores").EnumerateArray().Select(s => s.GetProperty("score").GetDouble()).Order().ToArray());
        await fixture.Create(second, $"/api/viewers/{secondId}/watchlist", new { movieId = movie, status = "Watched", note = "" });
        Assert.Empty(Rows(await fixture.Viewer.GetFromJsonAsync<JsonElement>(path + "&includeOtherOptions=true")));
    }

    private static JsonElement[] Rows(JsonElement result) => result.GetProperty("recommendations").EnumerateArray().ToArray();
    private static string Title(JsonElement row) => row.GetProperty("movie").GetProperty("title").GetString()!;

    private sealed class Fixture : IDisposable
    {
        public MovieWatchFactory Factory { get; } = new(overrides: new Dictionary<string, string?>
        {
            ["Administrator:Enabled"] = "true", ["Administrator:Email"] = "admin@example.com",
            ["Administrator:Password"] = ApiAccounts.Password, ["Administrator:DisplayName"] = "Admin"
        });
        public HttpClient Admin { get; private set; } = null!;
        public HttpClient Viewer { get; private set; } = null!;
        public string Path { get; private set; } = "";
        public async Task Initialize()
        {
            Admin = Factory.CreateClient();
            await ApiAccounts.SignInAsync(Admin, "admin@example.com");
            Viewer = Factory.CreateClient();
            var profile = await ApiAccounts.RegisterAsync(Viewer);
            await ApiAccounts.SignInAsync(Viewer);
            Path = $"/api/viewers/{profile.GetProperty("id").GetGuid()}";
        }
        public async Task<Guid> Create(HttpClient client, string path, object body)
        {
            var response = await client.PostAsJsonAsync(path, body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        public async Task<Guid> Mood(string preset)
        {
            var moods = await Viewer.GetFromJsonAsync<JsonElement>("/api/moods");
            return moods.EnumerateArray().Single(m => m.GetProperty("presetKey").GetString() == preset).GetProperty("id").GetGuid();
        }
        public Task<Guid> Genre(string name) => Create(Admin, "/api/genres", new { name });
        public Task<Guid> Movie(string title, Guid genre, int runtime = 90) => Create(Admin, "/api/movies", new { title, overview = "", runtimeMinutes = runtime, releaseDate = "2020-01-01", genreIds = new[] { genre } });
        public Task<JsonElement> Recommend(Guid mood, string extra = "") => Viewer.GetFromJsonAsync<JsonElement>($"{Path}/recommendations?moodId={mood}&maximumRuntimeMinutes=120{extra}");
        public void Dispose() { Admin?.Dispose(); Viewer?.Dispose(); Factory.Dispose(); }
    }
}
