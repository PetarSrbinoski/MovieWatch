using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MovieWatch.Domain.Common;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Missing_signing_key_prevents_startup()
    {
        using var factory = new MovieWatchFactory(overrides: new Dictionary<string, string?> { ["Jwt:SigningKey"] = "" });
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.False(File.Exists(factory.DatabasePath));
    }

    [Fact]
    public async Task Administrator_bootstrap_is_idempotent_across_restarts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moviewatch-bootstrap-{Guid.NewGuid()}.db");
        var settings = AdministratorConfiguration();
        try
        {
            Guid profileId;
            using (var first = new MovieWatchFactory(path, settings))
            using (var client = first.CreateClient())
            {
                await ApiAccounts.SignInAsync(client, "admin@example.com");
                profileId = (await client.GetFromJsonAsync<JsonElement>("/api/viewers/me")).GetProperty("id").GetGuid();
            }
            using var second = new MovieWatchFactory(path, settings);
            using var secondClient = second.CreateClient();
            await ApiAccounts.SignInAsync(secondClient, "admin@example.com");
            Assert.Equal(HttpStatusCode.NoContent, (await secondClient.GetAsync("/testing/administrator")).StatusCode);
            Assert.Equal(profileId, (await secondClient.GetFromJsonAsync<JsonElement>("/api/viewers/me")).GetProperty("id").GetGuid());
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    [Fact]
    public async Task Bootstrap_does_not_promote_an_existing_public_viewer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moviewatch-bootstrap-conflict-{Guid.NewGuid()}.db");
        try
        {
            using (var original = new MovieWatchFactory(path))
            using (var client = original.CreateClient())
                await ApiAccounts.RegisterAsync(client, "admin@example.com");

            using (var bootstrap = new MovieWatchFactory(path, AdministratorConfiguration()))
            {
                var exception = Assert.Throws<OperationException>(() => bootstrap.CreateClient());
                Assert.Equal(FailureKind.Conflict, exception.Kind);
            }

            using var recovery = new MovieWatchFactory(path);
            using var recoveryClient = recovery.CreateClient();
            await ApiAccounts.SignInAsync(recoveryClient, "admin@example.com");
            Assert.Equal(HttpStatusCode.Forbidden, (await recoveryClient.GetAsync("/testing/administrator")).StatusCode);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    [Fact]
    public async Task API_documentation_exposes_public_contracts_and_JWT_authorization()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/auth/register", out _));
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/viewers/me", out _));
        var components = document.GetProperty("components");
        Assert.Equal("bearer", components.GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var properties = components.GetProperty("schemas").GetProperty("RegisterRequest").GetProperty("properties");
        Assert.Equal(new[] { "displayName", "email", "password" }, properties.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }

    private static Dictionary<string, string?> AdministratorConfiguration() => new()
    {
        ["Administrator:Enabled"] = "true",
        ["Administrator:Email"] = "admin@example.com",
        ["Administrator:Password"] = ApiAccounts.Password,
        ["Administrator:DisplayName"] = "Administrator"
    };
}
