using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public class PersistenceTests
{
    [Theory]
    [InlineData("Viewers")]
    [InlineData("AspNetUserRoles")]
    public async Task Failed_registration_rolls_back_both_account_and_profile(string failingTable)
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var fail = connection.CreateCommand();
        // Simulate a storage failure after Identity has saved the account. Observe rollback only through the API.
        fail.CommandText = $"CREATE TRIGGER FailRegistration BEFORE INSERT ON {failingTable} BEGIN SELECT RAISE(ABORT, 'simulated storage failure'); END";
        await fail.ExecuteNonQueryAsync();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "ana@example.com", password = ApiAccounts.Password, displayName = "Ana"
        });
        Assert.Equal(HttpStatusCode.InternalServerError, registration.StatusCode);
        var problem = await registration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "ana@example.com", password = ApiAccounts.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var recover = connection.CreateCommand();
        recover.CommandText = "DROP TRIGGER FailRegistration";
        await recover.ExecuteNonQueryAsync();
        await ApiAccounts.RegisterAsync(client);
        await ApiAccounts.SignInAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/viewers/me")).StatusCode);
    }

    [Fact]
    public async Task Accounts_and_profiles_survive_a_host_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moviewatch-restart-{Guid.NewGuid()}.db");
        try
        {
            Guid viewerId;
            using (var firstHost = new MovieWatchFactory(path))
            using (var firstClient = firstHost.CreateClient())
                viewerId = (await ApiAccounts.RegisterAsync(firstClient)).GetProperty("id").GetGuid();

            using var secondHost = new MovieWatchFactory(path);
            using var secondClient = secondHost.CreateClient();
            await ApiAccounts.SignInAsync(secondClient);
            var profile = await secondClient.GetFromJsonAsync<JsonElement>("/api/viewers/me");
            Assert.Equal(viewerId, profile.GetProperty("id").GetGuid());
            Assert.Equal("Ana", profile.GetProperty("displayName").GetString());
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }
}
