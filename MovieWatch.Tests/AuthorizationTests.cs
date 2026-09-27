using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public class AuthorizationTests
{
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expired")]
    public async Task JWT_validation_rejects_invalid_tokens(string alteration)
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        await ApiAccounts.RegisterAsync(client);
        var login = await ApiAccounts.SignInAsync(client);
        var handler = new JwtSecurityTokenHandler();
        var original = handler.ReadJwtToken(login.GetProperty("accessToken").GetString());
        var replacement = new JwtSecurityToken(
            alteration == "issuer" ? "AnotherApp" : "MovieWatch.Tests",
            alteration == "audience" ? "AnotherApp" : "MovieWatch.Tests",
            original.Claims.Where(c => c.Type == "sub"),
            expires: alteration == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                alteration == "signature" ? "another-test-key-with-at-least-32-bytes" : MovieWatchFactory.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", handler.WriteToken(replacement));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/viewers/me")).StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_use_administrator_operations()
    {
        using var factory = new MovieWatchFactory();
        using var client = factory.CreateClient();
        await ApiAccounts.RegisterAsync(client);
        var login = await ApiAccounts.SignInAsync(client);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.GetProperty("accessToken").GetString());
        Assert.True(token.ValidTo <= DateTime.UtcNow.AddMinutes(15));
        Assert.True(token.ValidTo > DateTime.UtcNow.AddMinutes(14));
        var response = await client.GetAsync("/testing/administrator");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Configured_administrator_can_authenticate_and_use_administrator_operations()
    {
        using var factory = new MovieWatchFactory(overrides: new Dictionary<string, string?>
        {
            ["Administrator:Enabled"] = "true",
            ["Administrator:Email"] = "admin@example.com",
            ["Administrator:Password"] = ApiAccounts.Password,
            ["Administrator:DisplayName"] = "Administrator"
        });
        using var client = factory.CreateClient();
        await ApiAccounts.SignInAsync(client, "admin@example.com");
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/testing/administrator")).StatusCode);
    }
}
