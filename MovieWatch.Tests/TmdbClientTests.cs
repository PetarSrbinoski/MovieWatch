using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MovieWatch.Domain.Config;
using MovieWatch.Service.Implementation;

namespace MovieWatch.Tests;

public sealed class TmdbClientTests
{
    [Fact]
    public async Task Client_fetches_full_genre_list_without_discovering_movies()
    {
        var client = Client(request =>
        {
            Assert.Equal("/3/genre/movie/list", request.RequestUri!.AbsolutePath);
            Assert.Equal("?language=en", request.RequestUri.Query);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            return Json("""{"genres":[{"id":1,"name":" Comedy "},{"id":1,"name":"Comedy"},{"id":2,"name":"Drama"}]}""");
        });
        var genres = await client.GetGenresAsync(default);
        Assert.Equal(2, genres.Count);
        Assert.Equal("Comedy", genres[0].Name);
        Assert.Equal(2, genres[1].TmdbId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"genres\":[]}")]
    [InlineData("{\"genres\":[{\"id\":1,\"name\":\"\"}]}")]
    [InlineData("not json")]
    public async Task Client_rejects_invalid_genre_lists(string body)
    {
        var error = await Assert.ThrowsAsync<ImportSourceException>(() => Client(_ => Json(body)).GetGenresAsync(default));
        Assert.False(error.Transient);
    }

    [Fact]
    public async Task Client_extracts_discovery_and_normalizes_movie_details()
    {
        var calls = new List<Uri>();
        var client = Client(request =>
        {
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            calls.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath.EndsWith("discover/movie", StringComparison.Ordinal))
                return Json("""{"results":[{"id":42},{"id":42},{"id":43}]}""");
            return Json("""{"id":42,"title":"  Film  ","overview":"  Story  ","runtime":0,"release_date":"","vote_count":7,"poster_path":"/real-poster.jpg","genres":[{"id":1,"name":" Comedy "},{"id":1,"name":"Comedy"}]}""");
        });
        var ids = await client.DiscoverMovieIdsAsync(2, default);
        Assert.Equal(new long[] { 42, 43 }, ids);
        var movie = await client.GetMovieAsync(42, default);
        Assert.NotNull(movie);
        Assert.Equal("Film", movie.Title);
        Assert.Equal("Story", movie.Overview);
        Assert.Equal("/real-poster.jpg", movie.PosterPath);
        Assert.Null(movie.RuntimeMinutes);
        Assert.Null(movie.ReleaseDate);
        Assert.Single(movie.Genres);
        Assert.Equal("Comedy", movie.Genres[0].Name);
        Assert.Contains("sort_by=popularity.desc&page=2", calls[0].Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/poster.jpg")]
    [InlineData("/../poster.jpg")]
    public async Task Missing_or_invalid_poster_does_not_discard_the_movie(string? poster)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new { id = 42, title = "Film", poster_path = poster });
        var movie = await Client(_ => Json(body)).GetMovieAsync(42, default);
        Assert.NotNull(movie);
        Assert.Null(movie.PosterPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task Client_classifies_retryable_and_permanent_http_errors(HttpStatusCode status, bool transient)
    {
        var client = Client(_ =>
        {
            var response = new HttpResponseMessage(status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return response;
        });
        var error = await Assert.ThrowsAsync<ImportSourceException>(() => client.DiscoverMovieIdsAsync(1, default));
        Assert.Equal(transient, error.Transient);
        if (status == HttpStatusCode.TooManyRequests)
            Assert.Equal(TimeSpan.FromMinutes(3), error.RetryAfter);
    }

    private static TmdbClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new StubHandler(respond))
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
        return new TmdbClient(http, Options.Create(new TmdbSettings { ReadAccessToken = "test-token" }),
            TimeProvider.System);
    }

    private static HttpResponseMessage Json(string body)
    {
        return new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
