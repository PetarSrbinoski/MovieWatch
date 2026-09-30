using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MovieWatch.Tests.Hosting;

public static class ApiAccounts
{
    public const string Password = "Example-password1!";

    public static async Task<JsonElement> RegisterAsync(HttpClient client, string email = "ana@example.com", string displayName = "Ana")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> SignInAsync(HttpClient client, string email = "ana@example.com")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        return token;
    }
}
