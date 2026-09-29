using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using MovieWatch.Tests.Hosting;

namespace MovieWatch.Tests;

public sealed class ExportWorkflowTests
{
    [Fact]
    public async Task Personal_and_group_workbooks_match_scores_and_keep_titles_as_text()
    {
        using var factory = new MovieWatchFactory(overrides: new Dictionary<string, string?>
        {
            ["Administrator:Enabled"] = "true",
            ["Administrator:Email"] = "admin@example.com",
            ["Administrator:Password"] = ApiAccounts.Password,
            ["Administrator:DisplayName"] = "Administrator"
        });
        using var admin = factory.CreateClient();
        var about = await admin.GetFromJsonAsync<JsonElement>("/api/about");
        Assert.Contains("TMDB", about.GetProperty("attribution").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/tmdb-logo.svg")).StatusCode);
        await ApiAccounts.SignInAsync(admin, "admin@example.com");
        var genre = await CreateAsync(admin, "/api/genres", new { name = "Comedy" });
        var mood = await CreateAsync(admin, "/api/moods", new { name = "Relaxing", description = "" });
        await CreateAsync(admin, "/api/movies", new
        {
            title = "=1+1", overview = "Formula-like title", runtimeMinutes = 90,
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
        var query = $"moodId={Id(mood)}&maximumRuntimeMinutes=100&includeOtherOptions=true";
        using var personal = await WorkbookAsync(ana,
            $"/api/viewers/{Id(anaProfile)}/recommendations.xlsx?{query}");
        var sheet = personal.Worksheet("Recommendations");
        Assert.Equal("=1+1", sheet.Cell(10, 1).GetString());
        Assert.True(string.IsNullOrEmpty(sheet.Cell(10, 1).FormulaA1));
        Assert.Equal(2, sheet.Cell(10, 4).GetDouble());
        Assert.Equal("Relaxing", sheet.Cell(2, 2).GetString());
        Assert.Contains("TMDB API", sheet.Cell(7, 1).GetString());
        using var empty = await WorkbookAsync(ana,
            $"/api/viewers/{Id(anaProfile)}/recommendations.xlsx?moodId={Id(mood)}&maximumRuntimeMinutes=10");
        Assert.Equal("Title", empty.Worksheet(1).Cell(9, 1).GetString());
        Assert.True(empty.Worksheet(1).Cell(10, 1).IsEmpty());
        var group = await CreateAsync(ana, "/api/groups", new { name = "Friday", description = "" });
        await CreateAsync(ana, $"/api/groups/{Id(group)}/members", new { viewerId = Id(borisProfile) });
        using var shared = await WorkbookAsync(ana, $"/api/groups/{Id(group)}/recommendations.xlsx?{query}");
        var groupSheet = shared.Worksheet(1);
        Assert.Equal(0, groupSheet.Cell(10, 4).GetDouble());
        var anaColumn = Enumerable.Range(6, 2).Single(column =>
            groupSheet.Cell(9, column).GetString().Contains(Id(anaProfile).ToString(), StringComparison.Ordinal));
        var borisColumn = Enumerable.Range(6, 2).Single(column =>
            groupSheet.Cell(9, column).GetString().Contains(Id(borisProfile).ToString(), StringComparison.Ordinal));
        Assert.Equal(2, groupSheet.Cell(10, anaColumn).GetDouble());
        Assert.Equal(-2, groupSheet.Cell(10, borisColumn).GetDouble());
        using var outsider = factory.CreateClient();
        await ApiAccounts.RegisterAsync(outsider, "outsider@example.com", "Outsider");
        await ApiAccounts.SignInAsync(outsider, "outsider@example.com");
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.GetAsync($"/api/groups/{Id(group)}/recommendations.xlsx?{query}")).StatusCode);
    }

    private static Guid Id(JsonElement value)
    {
        return value.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CreateAsync(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<XLWorkbook> WorkbookAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }
}
