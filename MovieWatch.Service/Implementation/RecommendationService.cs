using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;
using MovieWatch.Service.Mapper;

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
            var contributions = Contributions(movie, weights, context.Mood);
            var score = Mean(contributions.Select(c => c.Weight));
            var explanation = Explain(contributions, score, context.Mood, query.MaximumRuntimeMinutes);
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
                Mean(Contributions(movie, byViewer[id], context.Mood).Select(c => c.Weight)))).ToArray();
            var score = Mean(members.Select(m => m.Score));
            var explanation = $"{(score > 0 ? "Positive group match" : "Other option")}: {context.Mood.Name}. "
                + $"Equal average of {members.Length} participating viewers, using each viewer's preferences over preset defaults. "
                + $"Within your {query.MaximumRuntimeMinutes}-minute limit.";
            return new Ranked(movie, score, explanation, [], members);
        });
        return Result(context, query, results, included);
    }

    private async Task<Context> PrepareAsync(RecommendationQueryDto query, CancellationToken cancellationToken)
    {
        if (query.MaximumRuntimeMinutes <= 0 || query.Limit is < 1 or > 50 || query.Skip < 0 || query.GenreIds is null)
            throw new OperationException(FailureKind.Validation, "Use a positive runtime, a limit of 1 to 50, a nonnegative skip, and valid genres.");
        var mood = await moods.GetAsync(m => m.Id == query.MoodId, cancellationToken)
            ?? throw new OperationException(FailureKind.Validation, "Unknown mood ID.");
        var filters = query.GenreIds.Distinct().ToArray();
        var found = await genres.ListAsync(g => filters.Contains(g.Id), cancellationToken);
        if (found.Count != filters.Length)
            throw new OperationException(FailureKind.Validation, "Unknown genre ID.");
        return new Context(MoodPresets.ToDto(mood),
            await movies.ListAsync(cancellationToken: cancellationToken),
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), clock.GetUtcNow());
    }

    private async Task<HashSet<Guid>> WatchedIdsAsync(Guid[] viewers, RecommendationQueryDto query, CancellationToken cancellationToken)
    {
        return query.IncludeWatched ? [] : (await watchlist.ListAsync(e => viewers.Contains(e.ViewerId)
            && e.Status == WatchStatus.Watched, cancellationToken)).Select(e => e.MovieId).ToHashSet();
    }

    private static IEnumerable<Movie> Eligible(IEnumerable<Movie> movies, RecommendationQueryDto query,
        HashSet<Guid> watched, DateOnly today)
    {
        return movies.Where(movie => movie.RuntimeMinutes is > 0 && movie.RuntimeMinutes <= query.MaximumRuntimeMinutes
            && movie.ReleaseDate is not null && movie.ReleaseDate <= today
            && !watched.Contains(movie.Id)
            && (query.GenreIds.Count == 0 || movie.MovieGenres.Any(link => query.GenreIds.Contains(link.GenreId))));
    }

    private static List<GenreContributionDto> Contributions(Movie movie, Dictionary<Guid, int> weights, MoodDto mood)
    {
        return movie.MovieGenres.GroupBy(link => link.GenreId).Select(group => group.First())
            .OrderBy(link => link.Genre.Name)
            .Select(link => new GenreContributionDto(link.GenreId, link.Genre.Name,
                weights.TryGetValue(link.GenreId, out var weight) ? weight : MoodPresets.Weight(mood, link.Genre),
                weights.ContainsKey(link.GenreId))).ToList();
    }

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
    {
        var ranked = results.ToArray();
        var ordered = ranked.OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Movie.VoteCount).ThenBy(r => r.Movie.Id).ToArray();
        var selected = ordered.Where(r => query.IncludeOtherOptions || r.Score > 0)
            .Skip(query.Skip).Take(query.Limit);
        return new(context.Mood, query, context.Now, selected.Select(r => new RecommendationDto(r.Movie.ToDto(), Math.Round(r.Score, 3), r.Explanation,
                r.Contributions, r.MemberScores.Select(m => m with { Score = Math.Round(m.Score, 3) }).ToArray()))
            .ToArray(), participants, ranked.Count(r => r.Score > 0), ranked.Count(r => r.Score <= 0));
    }

    private static string Explain(IReadOnlyList<GenreContributionDto> contributions, double score, MoodDto mood, int runtime)
    {
        var positive = contributions.Where(c => c.Weight > 0).ToArray();
        var basis = score > 0
            ? $"Matches {mood.Name} through {string.Join(", ", positive.Select(c => c.GenreName))}. "
            : $"Other option: no positive overall match for {mood.Name}. ";
        var sources = contributions.Any(c => c.IsPersonal)
            ? "Your genre preferences override the preset. "
            : mood.PresetKey is not null ? "Based on the mood's genre preset. " : "This custom mood has no preset. Adjust it to add your preferences. ";
        var lowered = contributions.Where(c => c.Weight < 0).ToArray();
        return basis + sources
            + (lowered.Length > 0 ? $"Lowered by your preference for {string.Join(", ", lowered.Select(c => c.GenreName))}. " : "")
            + $"Within your {runtime}-minute limit.";
    }

    private record Context(
        MoodDto Mood,
        List<Movie> Movies,
        DateOnly Today,
        DateTimeOffset Now
        );

    private record Ranked(
        Movie Movie,
        double Score,
        string Explanation,
        IReadOnlyList<GenreContributionDto> Contributions,
        IReadOnlyList<MemberScoreDto> MemberScores
        );
}
