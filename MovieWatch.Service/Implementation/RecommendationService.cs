using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class RecommendationService(
    IRepository<Mood> moods, IRepository<Genre> genres, IRepository<Movie> movies,
    IRepository<GenrePreference> preferences, IRepository<WatchlistEntry> watchlist,
    IGroupService groups, TimeProvider clock) : IRecommendationService
{
    public async Task<RecommendationResultDto> ForViewerAsync(ActorDto actor, Guid viewerId,
        RecommendationQueryDto query, CancellationToken cancellationToken)
    {
        if (actor.ViewerId != viewerId && !actor.IsAdministrator)
            throw new OperationException(FailureKind.NotFound, "Viewer was not found.");
        var context = await PrepareAsync(query, cancellationToken);
        var weights = (await preferences.ListAsync(p => p.ViewerId == viewerId && p.MoodId == query.MoodId, cancellationToken))
            .ToDictionary(p => p.GenreId, p => p.Weight);
        var watched = await WatchedIdsAsync([viewerId], query, cancellationToken);
        var results = Eligible(context.Movies, query, watched, context.Today).Select(movie =>
        {
            var contributions = Contributions(movie, weights);
            var score = Mean(contributions.Select(c => c.Weight));
            var explanation = $"Mood: {context.Mood.Name}. " + (contributions.Count == 0
                ? "No genres; neutral score."
                : string.Join("; ", contributions.Select(c => $"{c.GenreName}: {c.Weight:+#;-#;0}")))
                + ". Popularity breaks score ties.";
            return new Ranked(movie, score, explanation, contributions, []);
        });
        return Result(context, query, results, []);
    }

    public async Task<RecommendationResultDto> ForGroupAsync(ActorDto actor, Guid groupId,
        RecommendationQueryDto query, CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(actor, groupId, cancellationToken);
        var included = group.Memberships.Where(m => m.IncludedInRecommendations).Select(m => m.ViewerId).ToArray();
        if (included.Length == 0)
            throw new OperationException(FailureKind.Conflict, "Enable participation for at least one group member.");
        var context = await PrepareAsync(query, cancellationToken);
        var all = await preferences.ListAsync(p => p.MoodId == query.MoodId && included.Contains(p.ViewerId), cancellationToken);
        var byViewer = included.ToDictionary(id => id, id => all.Where(p => p.ViewerId == id)
            .ToDictionary(p => p.GenreId, p => p.Weight));
        var watched = await WatchedIdsAsync(included, query, cancellationToken);
        var results = Eligible(context.Movies, query, watched, context.Today).Select(movie =>
        {
            var members = included.Select(id => new MemberScoreDto(id,
                Mean(Contributions(movie, byViewer[id]).Select(c => c.Weight)))).ToArray();
            var score = Mean(members.Select(m => m.Score));
            var explanation = $"Mood: {context.Mood.Name}. Equal average of {members.Length} participating member scores. Popularity breaks score ties.";
            return new Ranked(movie, score, explanation, [], members);
        });
        return Result(context, query, results, included);
    }

    private async Task<Context> PrepareAsync(RecommendationQueryDto query, CancellationToken cancellationToken)
    {
        if (query.MaximumRuntimeMinutes <= 0 || query.Limit is < 1 or > 50 || query.GenreIds is null)
            throw new OperationException(FailureKind.Validation, "Use a positive runtime, a limit of 1 to 50, and valid genres.");
        var mood = await moods.GetAsync(m => m.Id == query.MoodId, cancellationToken)
            ?? throw new OperationException(FailureKind.Validation, "Unknown mood ID.");
        var filters = query.GenreIds.Distinct().ToArray();
        var found = await genres.ListAsync(g => filters.Contains(g.Id), cancellationToken);
        if (found.Count != filters.Length)
            throw new OperationException(FailureKind.Validation, "Unknown genre ID.");
        return new Context(new MoodDto(mood.Id, mood.Name, mood.Description),
            await movies.ListAsync(cancellationToken: cancellationToken),
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), clock.GetUtcNow());
    }

    private async Task<HashSet<Guid>> WatchedIdsAsync(Guid[] viewers, RecommendationQueryDto query, CancellationToken cancellationToken)
        => query.IncludeWatched ? [] : (await watchlist.ListAsync(e => viewers.Contains(e.ViewerId)
            && e.Status == WatchStatus.Watched, cancellationToken)).Select(e => e.MovieId).ToHashSet();

    private static IEnumerable<Movie> Eligible(IEnumerable<Movie> movies, RecommendationQueryDto query,
        HashSet<Guid> watched, DateOnly today)
        => movies.Where(movie => movie.RuntimeMinutes is > 0 && movie.RuntimeMinutes <= query.MaximumRuntimeMinutes
            && movie.ReleaseDate is not null && movie.ReleaseDate <= today
            && !watched.Contains(movie.Id)
            && (query.GenreIds.Count == 0 || movie.MovieGenres.Any(link => query.GenreIds.Contains(link.GenreId))));

    private static List<GenreContributionDto> Contributions(Movie movie, Dictionary<Guid, int> weights)
        => movie.MovieGenres.GroupBy(link => link.GenreId).Select(group => group.First())
            .OrderBy(link => link.Genre.Name)
            .Select(link => new GenreContributionDto(link.GenreId, link.Genre.Name,
                weights.GetValueOrDefault(link.GenreId))).ToList();

    private static double Mean(IEnumerable<int> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0 : array.Average();
    }

    private static double Mean(IEnumerable<double> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0 : array.Average();
    }

    private static RecommendationResultDto Result(Context context, RecommendationQueryDto query,
        IEnumerable<Ranked> results, IReadOnlyList<Guid> participants)
        => new(context.Mood, query, context.Now, results.OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Movie.VoteCount).ThenBy(r => r.Movie.Id).Take(query.Limit)
            .Select(r => new RecommendationDto(ToDto(r.Movie), Math.Round(r.Score, 3), r.Explanation,
                r.Contributions, r.MemberScores.Select(m => m with { Score = Math.Round(m.Score, 3) }).ToArray()))
            .ToArray(), participants);

    private static MovieDto ToDto(Movie movie) => new(movie.Id, movie.Title, movie.Overview, movie.RuntimeMinutes,
        movie.ReleaseDate, movie.TmdbId, movie.VoteCount, movie.LastImportedAt,
        movie.MovieGenres.OrderBy(link => link.Genre.Name)
            .Select(link => new GenreDto(link.GenreId, link.Genre.Name, link.Genre.TmdbId)).ToArray());

    private sealed record Context(MoodDto Mood, List<Movie> Movies, DateOnly Today, DateTimeOffset Now);
    private sealed record Ranked(Movie Movie, double Score, string Explanation,
        IReadOnlyList<GenreContributionDto> Contributions, IReadOnlyList<MemberScoreDto> MemberScores);
}
