using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MovieWatch.Domain.Common;
using MovieWatch.Domain.Models;
using MovieWatch.Repository;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public sealed class CollectionPaginationTests
{
    [Fact]
    public async Task Collections_filter_access_before_paging_and_preserve_all_records_across_pages()
    {
        using var factory = new MovieWatchFactory(overrides: new Dictionary<string, string?>
        {
            ["Administrator:Enabled"] = "true", ["Administrator:Email"] = "admin@example.com",
            ["Administrator:Password"] = ApiAccounts.Password, ["Administrator:DisplayName"] = "Admin"
        });
        using var viewer = factory.CreateClient();
        var viewerId = (await ApiAccounts.RegisterAsync(viewer)).GetProperty("id").GetGuid();
        await ApiAccounts.SignInAsync(viewer);
        using var outsider = factory.CreateClient();
        var outsiderId = (await ApiAccounts.RegisterAsync(outsider, "outsider@example.com")).GetProperty("id").GetGuid();
        await ApiAccounts.SignInAsync(outsider, "outsider@example.com");
        using var admin = factory.CreateClient();
        await ApiAccounts.SignInAsync(admin, "admin@example.com");

        var groups = new List<Group>();
        var preferences = new List<GenrePreference>();
        var watchlist = new List<WatchlistEntry>();
        var privateGroups = new List<Group>();
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mood = new Mood("Pagination mood", "");
            database.Add(mood);
            for (var index = 0; index < 105; index++)
            {
                var genre = new Genre($"Genre {index}");
                var movie = new Movie($"Movie {index}", "", 90, new DateOnly(2020, 1, 1));
                database.AddRange(genre, movie);
                preferences.Add(new GenrePreference(viewerId, genre.Id, mood.Id, 1));
                watchlist.Add(new WatchlistEntry(viewerId, movie.Id, WatchStatus.Planned, ""));
                // Interleave records owned by somebody else to catch paging before filtering.
                database.Add(new GenrePreference(outsiderId, genre.Id, mood.Id, -1));
                database.Add(new WatchlistEntry(outsiderId, movie.Id, WatchStatus.Watched, ""));
                var group = new Group($"Group {index}", "", index % 2 == 0 ? viewerId : outsiderId);
                group.Memberships.Add(new GroupMembership(group.Id, viewerId));
                if (group.OwnerViewerId == outsiderId)
                    group.Memberships.Add(new GroupMembership(group.Id, outsiderId));
                groups.Add(group);
                var privateGroup = new Group($"Private group {index}", "", outsiderId);
                privateGroup.Memberships.Add(new GroupMembership(privateGroup.Id, outsiderId));
                privateGroups.Add(privateGroup);
            }
            database.AddRange(groups);
            database.AddRange(privateGroups);
            database.AddRange(preferences);
            database.AddRange(watchlist);
            await database.SaveChangesAsync();
        }

        var collections = new (string Path, IEnumerable<BaseEntity> Records)[]
        {
            ("/api/groups", groups),
            ($"/api/viewers/{viewerId}/preferences", preferences),
            ($"/api/viewers/{viewerId}/watchlist", watchlist)
        };
        foreach (var (path, records) in collections)
        {
            var expected = records.Select(record => record.Id).Order().ToArray();
            Assert.Equal(expected.Take(20), await IdsAsync(viewer, path));
            var first = await IdsAsync(viewer, path + "?skip=0&take=100");
            var second = await IdsAsync(viewer, path + "?skip=100&take=100");
            Assert.Equal(100, first.Length);
            Assert.Equal(5, second.Length);
            Assert.Equal(expected, first.Concat(second));
            Assert.Equal(expected.Skip(7).Take(3), await IdsAsync(viewer, path + "?skip=7&take=3"));
            Assert.Empty(await IdsAsync(viewer, path + "?skip=105&take=20"));
            foreach (var invalid in new[] { "skip=-1", "take=0", "take=-1", "take=101" })
                Assert.Equal(HttpStatusCode.BadRequest, (await viewer.GetAsync(path + "?" + invalid)).StatusCode);
        }
        var allGroupIds = groups.Concat(privateGroups).Select(group => group.Id).Order().ToArray();
        Assert.Equal(allGroupIds.Skip(100).Take(100), await IdsAsync(admin, "/api/groups?skip=100&take=100"));
        foreach (var collection in new[] { "preferences", "watchlist" })
        {
            var path = $"/api/viewers/{viewerId}/{collection}?skip=100&take=100";
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
            Assert.Equal(await IdsAsync(viewer, path), await IdsAsync(admin, path));
        }
    }

    private static async Task<Guid[]> IdsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
        return rows.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToArray();
    }
}
