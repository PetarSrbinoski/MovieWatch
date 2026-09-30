using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MovieWatch.Domain.Config;
using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class ImportSourceException(string message, bool transient, TimeSpan? retryAfter = null) : Exception(message)
{
    public bool Transient { get; } = transient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed class TmdbClient(HttpClient client, IOptions<TmdbSettings> settings, TimeProvider clock) : ITmdbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<long>> DiscoverMovieIdsAsync(int page, CancellationToken cancellationToken)
    {
        using var response = await SendAsync($"discover/movie?sort_by=popularity.desc&page={page}", false, cancellationToken);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<Discovery>(JsonOptions, cancellationToken);
            return result?.Results?.Where(m => m.Id > 0).Select(m => m.Id).Distinct().ToArray()
                ?? throw new JsonException();
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine(exception);
            throw new ImportSourceException("TMDB discovery response was invalid.", false);
        }
    }

    public async Task<ImportedMovieDto?> GetMovieAsync(long tmdbId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync($"movie/{tmdbId}", true, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        try
        {
            var result = await response.Content.ReadFromJsonAsync<MovieDetails>(JsonOptions, cancellationToken);
            if (result is null || result.Id <= 0 || string.IsNullOrWhiteSpace(result.Title)
                || result.Id != tmdbId || result.Title.Trim().Length > 200)
                return null;
            DateOnly? releaseDate = DateOnly.TryParse(result.ReleaseDate, out var date) ? date : null;
            var genres = result.Genres?.Where(g => g.Id > 0 && !string.IsNullOrWhiteSpace(g.Name)
                    && g.Name.Trim().Length <= 100)
                .GroupBy(g => g.Id).Select(g => new ImportedGenreDto(g.Key, g.First().Name!.Trim())).ToArray()
                ?? [];
            var overview = (result.Overview ?? "").Trim();
            return new ImportedMovieDto(result.Id, result.Title.Trim(),
                overview[..Math.Min(overview.Length, 4000)],
                result.Runtime is > 0 ? result.Runtime : null, releaseDate,
                Math.Max(0, result.VoteCount), genres);
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine(exception);
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, bool allowNotFound, CancellationToken cancellationToken)
    {
        var token = settings.Value.ReadAccessToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new ImportSourceException("TMDB read access token is not configured.", false);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine(exception);
            throw new ImportSourceException("TMDB connection failed.", true);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine(exception);
            throw new ImportSourceException("TMDB request timed out.", true);
        }
        if (response.IsSuccessStatusCode || allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            return response;
        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? response.Headers.RetryAfter?.Date - clock.GetUtcNow();
        var status = response.StatusCode;
        response.Dispose();
        if (status == HttpStatusCode.TooManyRequests || (int)status >= 500)
            throw new ImportSourceException("TMDB is temporarily unavailable.", true, retryAfter);
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ImportSourceException("TMDB rejected the configured credentials.", false);
        throw new ImportSourceException("TMDB rejected the request.", false);
    }

    private record Discovery(
        [property: JsonPropertyName("results")] DiscoveryItem[]? Results
        );

    private record DiscoveryItem(
        [property: JsonPropertyName("id")] long Id
        );

    private record ExternalGenre(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string? Name
        );

    private record MovieDetails(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("overview")] string? Overview,
        [property: JsonPropertyName("runtime")] int? Runtime,
        [property: JsonPropertyName("release_date")] string? ReleaseDate,
        [property: JsonPropertyName("vote_count")] int VoteCount,
        [property: JsonPropertyName("genres")] ExternalGenre[]? Genres
        );
}
