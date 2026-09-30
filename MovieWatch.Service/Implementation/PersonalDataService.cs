using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class PersonalDataService(
    IRepository<Viewer> viewers, IRepository<Genre> genres, IRepository<Mood> moods,
    IRepository<Movie> movies, IRepository<GenrePreference> preferences,
    IRepository<WatchlistEntry> watchlist) : IPersonalDataService
{
    public async Task<List<PreferenceDto>> ListPreferencesAsync(ActorDto actor, Guid viewerId, CancellationToken cancellationToken)
    {
        RequireAccess(actor, viewerId);
        return (await preferences.ListAsync(p => p.ViewerId == viewerId, cancellationToken))
            .OrderBy(p => p.Id).Select(ToDto).ToList();
    }

    public async Task<PreferenceDto> GetPreferenceAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        return ToDto(await FindPreferenceAsync(actor, viewerId, id, cancellationToken));
    }

    public async Task<PreferenceDto> CreatePreferenceAsync(ActorDto actor, Guid viewerId, Guid genreId, Guid moodId,
        int weight, CancellationToken cancellationToken)
    {
        RequireAccess(actor, viewerId);
        ValidateWeight(weight);
        if (await viewers.GetAsync(v => v.Id == viewerId, cancellationToken) is null
            || await genres.GetAsync(g => g.Id == genreId, cancellationToken) is null
            || await moods.GetAsync(m => m.Id == moodId, cancellationToken) is null)
            throw new OperationException(FailureKind.Validation, "Viewer, genre, and mood must exist.");
        return ToDto(await preferences.InsertAsync(new GenrePreference(viewerId, genreId, moodId, weight), cancellationToken));
    }

    public async Task<PreferenceDto> UpdatePreferenceAsync(ActorDto actor, Guid viewerId, Guid id,
        int weight, CancellationToken cancellationToken)
    {
        ValidateWeight(weight);
        var preference = await FindPreferenceAsync(actor, viewerId, id, cancellationToken);
        preference.Weight = weight;
        await preferences.SaveAsync(cancellationToken);
        return ToDto(preference);
    }

    public async Task DeletePreferenceAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        await preferences.DeleteAsync(await FindPreferenceAsync(actor, viewerId, id, cancellationToken), cancellationToken);
    }

    public async Task<List<WatchlistDto>> ListWatchlistAsync(ActorDto actor, Guid viewerId, CancellationToken cancellationToken)
    {
        RequireAccess(actor, viewerId);
        return (await watchlist.ListAsync(e => e.ViewerId == viewerId, cancellationToken))
            .OrderBy(e => e.Id).Select(ToDto).ToList();
    }

    public async Task<WatchlistDto> GetWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        return ToDto(await FindWatchlistEntryAsync(actor, viewerId, id, cancellationToken));
    }

    public async Task<WatchlistDto> CreateWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid movieId,
        WatchStatus status, string note, CancellationToken cancellationToken)
    {
        RequireAccess(actor, viewerId);
        ValidateWatchlist(status, note);
        if (await viewers.GetAsync(v => v.Id == viewerId, cancellationToken) is null
            || await movies.GetAsync(m => m.Id == movieId, cancellationToken) is null)
            throw new OperationException(FailureKind.Validation, "Viewer and movie must exist.");
        return ToDto(await watchlist.InsertAsync(new WatchlistEntry(viewerId, movieId, status, note.Trim()), cancellationToken));
    }

    public async Task<WatchlistDto> UpdateWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id,
        WatchStatus status, string note, CancellationToken cancellationToken)
    {
        ValidateWatchlist(status, note);
        var entry = await FindWatchlistEntryAsync(actor, viewerId, id, cancellationToken);
        entry.Status = status;
        entry.Note = note.Trim();
        await watchlist.SaveAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task DeleteWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        await watchlist.DeleteAsync(await FindWatchlistEntryAsync(actor, viewerId, id, cancellationToken), cancellationToken);
    }

    private async Task<GenrePreference> FindPreferenceAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        RequireAccess(actor, viewerId);
        return await preferences.FindAsync(p => p.Id == id && p.ViewerId == viewerId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Preference was not found.");
    }

    private async Task<WatchlistEntry> FindWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        RequireAccess(actor, viewerId);
        return await watchlist.FindAsync(e => e.Id == id && e.ViewerId == viewerId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Watchlist entry was not found.");
    }

    private static void RequireAccess(ActorDto actor, Guid viewerId)
    {
        if (actor.ViewerId != viewerId && !actor.IsAdministrator)
            throw new OperationException(FailureKind.NotFound, "Viewer data was not found.");
    }

    private static void ValidateWeight(int weight)
    {
        if (weight is < -2 or > 2)
            throw new OperationException(FailureKind.Validation, "Preference weight must be from -2 to 2.");
    }

    private static void ValidateWatchlist(WatchStatus status, string note)
    {
        if (!Enum.IsDefined(status) || note is null || note.Length > 1000)
            throw new OperationException(FailureKind.Validation, "Status or note is invalid.");
    }

    private static PreferenceDto ToDto(GenrePreference p)
    {
        return new(p.Id, p.ViewerId, p.GenreId, p.MoodId, p.Weight);
    }
    private static WatchlistDto ToDto(WatchlistEntry e)
    {
        return new(e.Id, e.ViewerId, e.MovieId, e.Status, e.Note);
    }
}
