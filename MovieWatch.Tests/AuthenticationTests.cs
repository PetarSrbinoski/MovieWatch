using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public class AuthenticationTests
{
    [Theory]
    [InlineData("not-an-email", "Example-password1!", "Ana")]
    [InlineData("ana@example.com", "short", "Ana")]
    [InlineData("ana@example.com", "abcdefghijklmnop", "Ana")]
    [InlineData("ana@example.com", "Example-password1!", " ")]
    [InlineData(null, "Example-password1!", "Ana")]
    [InlineData("ana@example.com", null, "Ana")]
    [InlineData("ana@example.com", "Example-password1!", null)]
    public async Task Invalid_registration_returns_a_safe_validation_problem(string? email, string? password, string? displayName)
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        if (password is not null)
            Assert.DoesNotContain(password, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Duplicate_email_is_case_insensitive_and_preserves_the_original_account()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        var original = await ApiAccounts.RegisterAsync(client);
        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "ANA@EXAMPLE.COM", password = "Different-password2!", displayName = "Imposter"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        await ApiAccounts.SignInAsync(client, "ANA@EXAMPLE.COM");
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/viewers/me");
        Assert.Equal(original.GetProperty("id").GetGuid(), profile.GetProperty("id").GetGuid());
        Assert.Equal("Ana", profile.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_return_the_same_credentials_error()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        await ApiAccounts.RegisterAsync(client);
        foreach (var email in new[] { "ana@example.com", "unknown@example.com" })
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-password9!" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Invalid email or password.", error.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public async Task Profile_requires_authentication_and_cannot_be_selected_by_another_viewer_identifier()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        var unauthenticated = await client.GetAsync("/api/viewers/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("application/problem+json", unauthenticated.Content.Headers.ContentType?.MediaType);
        var ana = await ApiAccounts.RegisterAsync(client);
        var boris = await ApiAccounts.RegisterAsync(client, "boris@example.com", "Boris");
        await ApiAccounts.SignInAsync(client);
        var selected = await client.GetFromJsonAsync<JsonElement>($"/api/viewers/me?viewerId={boris.GetProperty("id").GetGuid()}");
        Assert.Equal(ana.GetProperty("id").GetGuid(), selected.GetProperty("id").GetGuid());
        var other = await client.GetAsync($"/api/viewers/{boris.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task Public_registration_rejects_role_assignment()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "ana@example.com", password = "Example-password1!", displayName = "Ana",
            role = "Administrator"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ana@example.com", password = "Example-password1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Viewer_can_register_sign_in_and_read_their_persisted_profile()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "ana@example.com", password = "Example-password1!", displayName = "Ana"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal("/api/viewers/me", registration.Headers.Location?.OriginalString);
        var registered = await registration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ana", registered.GetProperty("displayName").GetString());

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ana@example.com", password = "Example-password1!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = token.GetProperty("accessToken").GetString()!;
        Assert.Equal(3, accessToken.Split('.').Length);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profile = await client.GetAsync("/api/viewers/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var viewer = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(registered.GetProperty("id").GetGuid(), viewer.GetProperty("id").GetGuid());
        Assert.Equal("ana@example.com", viewer.GetProperty("email").GetString());
        Assert.Equal("Ana", viewer.GetProperty("displayName").GetString());
        Assert.Equal(new[] { "displayName", "email", "id" }, viewer.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }
}
