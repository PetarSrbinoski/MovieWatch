using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieWatch.Repository;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public sealed class CoreWorkflowTests
{
    [Fact]
    public async Task Administrator_can_manage_account_and_deleted_token_is_rejected()
    {
        using var factory = AdminFactory();
        using var admin = factory.CreateClient();
        await ApiAccounts.SignInAsync(admin, "admin@example.com");
        var created = await CreateAsync(admin, "/api/viewers", new
        {
            email = "managed@example.com", password = ApiAccounts.Password, displayName = "Managed"
        });
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/viewers?skip=0&take=10");
        Assert.Contains(list.EnumerateArray(), item => Id(item) == Id(created));
        using var managed = factory.CreateClient();
        await ApiAccounts.SignInAsync(managed, "managed@example.com");
        var updated = await admin.PutAsJsonAsync($"/api/viewers/{Id(created)}", new
        {
            email = "renamed@example.com", displayName = "Renamed"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var profile = await admin.GetFromJsonAsync<JsonElement>($"/api/viewers/{Id(created)}");
        Assert.Equal("Renamed", profile.GetProperty("displayName").GetString());
        Assert.Equal("renamed@example.com", profile.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/viewers/{Id(created)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await managed.GetAsync("/api/viewers/me")).StatusCode);
    }

    [Fact]
    public async Task Preference_identity_uses_viewer_genre_and_mood_together()
    {
        using var factory = AdminFactory();
        using var admin = factory.CreateClient();
        await ApiAccounts.SignInAsync(admin, "admin@example.com");
        var genre = await CreateAsync(admin, "/api/genres", new { name = "Horror" });
        var relaxing = await CreateAsync(admin, "/api/moods", new { name = "Relaxing", description = "" });
        var adventurous = await CreateAsync(admin, "/api/moods", new { name = "Adventurous", description = "" });
        using var ana = factory.CreateClient();
        var anaProfile = await ApiAccounts.RegisterAsync(ana);
        await ApiAccounts.SignInAsync(ana);
        using var boris = factory.CreateClient();
        var borisProfile = await ApiAccounts.RegisterAsync(boris, "boris@example.com", "Boris");
        await ApiAccounts.SignInAsync(boris, "boris@example.com");
        var path = $"/api/viewers/{Id(anaProfile)}/preferences";
        var first = await CreateAsync(ana, path, new { genreId = Id(genre), moodId = Id(relaxing), weight = -2 });
        await CreateAsync(ana, path, new { genreId = Id(genre), moodId = Id(adventurous), weight = 2 });
        await CreateAsync(boris, $"/api/viewers/{Id(borisProfile)}/preferences",
            new { genreId = Id(genre), moodId = Id(relaxing), weight = 1 });
        Assert.Equal(HttpStatusCode.Conflict,
            (await ana.PostAsJsonAsync(path, new { genreId = Id(genre), moodId = Id(relaxing), weight = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await boris.GetAsync($"{path}/{Id(first)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/moods/{Id(relaxing)}")).StatusCode);
    }


    [Fact]
    public async Task Admin_catalogue_and_viewer_preferences_produce_explained_recommendations()
    {
        using var factory = AdminFactory();
        using var admin = factory.CreateClient();
        await ApiAccounts.SignInAsync(admin, "admin@example.com");
        var genre = await CreateAsync(admin, "/api/genres", new { name = "Comedy" });
        var mood = await CreateAsync(admin, "/api/moods", new { name = "Relaxing", description = "Easy evening" });
        var movie = await CreateAsync(admin, "/api/movies", new
        {
            title = "A Good Film", overview = "Story", runtimeMinutes = 90,
            releaseDate = "2020-01-01", genreIds = new[] { Id(genre) }
        });
        using var viewer = factory.CreateClient();
        var profile = await ApiAccounts.RegisterAsync(viewer);
        await ApiAccounts.SignInAsync(viewer);
        var viewerId = Id(profile);
        var preference = await CreateAsync(viewer, $"/api/viewers/{viewerId}/preferences", new
        {
            genreId = Id(genre), moodId = Id(mood), weight = 2
        });
        var result = await viewer.GetFromJsonAsync<JsonElement>(
            $"/api/viewers/{viewerId}/recommendations?moodId={Id(mood)}&maximumRuntimeMinutes=100");
        var recommendation = result.GetProperty("recommendations").EnumerateArray().Single();
        Assert.Equal(Id(movie), recommendation.GetProperty("movie").GetProperty("id").GetGuid());
        Assert.Equal(2, recommendation.GetProperty("score").GetDouble());
        Assert.Contains("Comedy", recommendation.GetProperty("explanation").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/genres/{Id(genre)}")).StatusCode);
        var watched = await CreateAsync(viewer, $"/api/viewers/{viewerId}/watchlist", new
        {
            movieId = Id(movie), status = "Watched", note = "Seen"
        });
        var excluded = await viewer.GetFromJsonAsync<JsonElement>(
            $"/api/viewers/{viewerId}/recommendations?moodId={Id(mood)}&maximumRuntimeMinutes=100");
        Assert.Empty(excluded.GetProperty("recommendations").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/movies/{Id(movie)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await viewer.DeleteAsync($"/api/viewers/{viewerId}/watchlist/{Id(watched)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await viewer.DeleteAsync($"/api/viewers/{viewerId}/preferences/{Id(preference)}")).StatusCode);
        var drama = await CreateAsync(admin, "/api/genres", new { name = "Drama" });
        var replaced = await admin.PutAsJsonAsync($"/api/movies/{Id(movie)}", new
        {
            title = "A Good Film", overview = "Updated", runtimeMinutes = 90,
            releaseDate = "2020-01-01", genreIds = new[] { Id(drama) }
        });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var changed = await replaced.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Id(drama), changed.GetProperty("genres").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/genres/{Id(genre)}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audited = await database.Movies.SingleAsync(m => m.Id == Id(movie));
        Assert.NotEqual(default, audited.CreatedAt);
        Assert.True(audited.UpdatedAt >= audited.CreatedAt);
        Assert.NotEmpty(audited.CreatedBy);
    }

    [Fact]
    public async Task Group_transfer_protects_owner_and_allows_former_owner_to_leave()
    {
        using var factory = new MovieWatchFactory();
        using var ana = factory.CreateClient();
        var anaProfile = await ApiAccounts.RegisterAsync(ana);
        await ApiAccounts.SignInAsync(ana);
        using var boris = factory.CreateClient();
        var borisProfile = await ApiAccounts.RegisterAsync(boris, "boris@example.com", "Boris");
        await ApiAccounts.SignInAsync(boris, "boris@example.com");
        var group = await CreateAsync(ana, "/api/groups", new { name = "Friday", description = "Friends" });
        var borisMember = await CreateAsync(ana, $"/api/groups/{Id(group)}/members", new { viewerId = Id(borisProfile) });
        Assert.Equal(HttpStatusCode.Conflict, (await ana.DeleteAsync("/api/viewers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.PutAsJsonAsync($"/api/groups/{Id(group)}/owner",
            new { viewerId = Id(borisProfile) })).StatusCode);
        var ownerMembership = group.GetProperty("memberships").EnumerateArray().Single();
        Assert.Equal(HttpStatusCode.NoContent,
            (await ana.DeleteAsync($"/api/groups/{Id(group)}/members/{Id(ownerMembership)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync("/api/viewers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ana.GetAsync("/api/viewers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await boris.GetAsync($"/api/groups/{Id(group)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await boris.DeleteAsync($"/api/groups/{Id(group)}/members/{Id(borisMember)}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_nonowner_removes_personal_records_and_membership_but_keeps_shared_data()
    {
        using var factory = AdminFactory();
        using var admin = factory.CreateClient();
        await ApiAccounts.SignInAsync(admin, "admin@example.com");
        var genre = await CreateAsync(admin, "/api/genres", new { name = "Drama" });
        var mood = await CreateAsync(admin, "/api/moods", new { name = "Calm", description = "" });
        var movie = await CreateAsync(admin, "/api/movies", new
        {
            title = "Shared film", overview = "", runtimeMinutes = 90,
            releaseDate = "2020-01-01", genreIds = new[] { Id(genre) }
        });
        using var ana = factory.CreateClient();
        await ApiAccounts.RegisterAsync(ana);
        await ApiAccounts.SignInAsync(ana);
        using var boris = factory.CreateClient();
        var borisProfile = await ApiAccounts.RegisterAsync(boris, "boris@example.com", "Boris");
        await ApiAccounts.SignInAsync(boris, "boris@example.com");
        var group = await CreateAsync(ana, "/api/groups", new { name = "Friends", description = "" });
        await CreateAsync(ana, $"/api/groups/{Id(group)}/members", new { viewerId = Id(borisProfile) });
        await CreateAsync(boris, $"/api/viewers/{Id(borisProfile)}/preferences", new
        {
            genreId = Id(genre), moodId = Id(mood), weight = 1
        });
        await CreateAsync(boris, $"/api/viewers/{Id(borisProfile)}/watchlist", new
        {
            movieId = Id(movie), status = "Planned", note = "Later"
        });

        Assert.Equal(HttpStatusCode.NoContent, (await boris.DeleteAsync("/api/viewers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await boris.GetAsync("/api/viewers/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await database.Viewers.AnyAsync(viewer => viewer.Id == Id(borisProfile)));
        Assert.False(await database.GenrePreferences.AnyAsync(preference => preference.ViewerId == Id(borisProfile)));
        Assert.False(await database.WatchlistEntries.AnyAsync(entry => entry.ViewerId == Id(borisProfile)));
        Assert.False(await database.GroupMemberships.AnyAsync(member => member.ViewerId == Id(borisProfile)));
        Assert.True(await database.Groups.AnyAsync(item => item.Id == Id(group)));
        Assert.True(await database.Movies.AnyAsync(item => item.Id == Id(movie)));
        Assert.True(await database.Genres.AnyAsync(item => item.Id == Id(genre)));
        Assert.True(await database.Moods.AnyAsync(item => item.Id == Id(mood)));
    }

    [Fact]
    public async Task Group_recommendations_average_members_and_ignore_opted_out_watched_history()
    {
        using var factory = AdminFactory();
        using var admin = factory.CreateClient();
        await ApiAccounts.SignInAsync(admin, "admin@example.com");
        var genre = await CreateAsync(admin, "/api/genres", new { name = "Drama" });
        var mood = await CreateAsync(admin, "/api/moods", new { name = "Thoughtful", description = "" });
        var movie = await CreateAsync(admin, "/api/movies", new
        {
            title = "Shared Film", overview = "", runtimeMinutes = 100,
            releaseDate = "2020-01-01", genreIds = new[] { Id(genre) }
        });
        using var ana = factory.CreateClient();
        var anaProfile = await ApiAccounts.RegisterAsync(ana);
        await ApiAccounts.SignInAsync(ana);
        using var boris = factory.CreateClient();
        var borisProfile = await ApiAccounts.RegisterAsync(boris, "boris@example.com", "Boris");
        await ApiAccounts.SignInAsync(boris, "boris@example.com");
        await CreateAsync(ana, $"/api/viewers/{Id(anaProfile)}/preferences", new
        {
            genreId = Id(genre), moodId = Id(mood), weight = 2
        });
        await CreateAsync(boris, $"/api/viewers/{Id(borisProfile)}/preferences", new
        {
            genreId = Id(genre), moodId = Id(mood), weight = -2
        });
        var group = await CreateAsync(ana, "/api/groups", new { name = "Friends", description = "" });
        var member = await CreateAsync(ana, $"/api/groups/{Id(group)}/members", new { viewerId = Id(borisProfile) });
        var query = $"/api/groups/{Id(group)}/recommendations?moodId={Id(mood)}&maximumRuntimeMinutes=120";
        var shared = await ana.GetFromJsonAsync<JsonElement>(query);
        var recommendation = shared.GetProperty("recommendations").EnumerateArray().Single();
        Assert.Equal(0, recommendation.GetProperty("score").GetDouble());
        Assert.Equal(2, recommendation.GetProperty("memberScores").GetArrayLength());
        await CreateAsync(boris, $"/api/viewers/{Id(borisProfile)}/watchlist", new
        {
            movieId = Id(movie), status = "Watched", note = "Seen"
        });
        var excluded = await ana.GetFromJsonAsync<JsonElement>(query);
        Assert.Empty(excluded.GetProperty("recommendations").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,
            (await boris.PutAsJsonAsync($"/api/groups/{Id(group)}/members/{Id(member)}",
                new { includedInRecommendations = false })).StatusCode);
        var solo = await ana.GetFromJsonAsync<JsonElement>(query);
        Assert.Equal(2, solo.GetProperty("recommendations").EnumerateArray().Single()
            .GetProperty("score").GetDouble());
        var ownerMember = group.GetProperty("memberships").EnumerateArray().Single();
        Assert.Equal(HttpStatusCode.OK,
            (await ana.PutAsJsonAsync($"/api/groups/{Id(group)}/members/{Id(ownerMember)}",
                new { includedInRecommendations = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ana.GetAsync(query)).StatusCode);
    }

    private static MovieWatchFactory AdminFactory()
    {
        return new(overrides: new Dictionary<string, string?>
    {
        ["Administrator:Enabled"] = "true",
        ["Administrator:Email"] = "admin@example.com",
        ["Administrator:Password"] = ApiAccounts.Password,
        ["Administrator:DisplayName"] = "Administrator"
    });
    }

    private static Guid Id(JsonElement value)
    {
        return value.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CreateAsync(HttpClient client, string path, object value)
    {
        var response = await client.PostAsJsonAsync(path, value);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
