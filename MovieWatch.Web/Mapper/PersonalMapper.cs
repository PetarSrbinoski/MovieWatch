using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Mapper;

public sealed class PersonalMapper(IPersonalDataService service, AccountMapper accounts)
{
    public async Task<List<PreferenceResponse>> ListPreferencesAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        return (await service.ListPreferencesAsync(await accounts.GetActorAsync(cancellationToken), viewerId, cancellationToken))
            .Select(p => p.ToResponse()).ToList();
    }

    public async Task<PreferenceResponse> GetPreferenceAsync(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        return (await service.GetPreferenceAsync(await accounts.GetActorAsync(cancellationToken), viewerId, id, cancellationToken)).ToResponse();
    }

    public async Task<PreferenceResponse> CreatePreferenceAsync(Guid viewerId, PreferenceRequest request,
        CancellationToken cancellationToken)
    {
        return (await service.CreatePreferenceAsync(await accounts.GetActorAsync(cancellationToken), viewerId,
            request.GenreId, request.MoodId, request.Weight, cancellationToken)).ToResponse();
    }

    public async Task<PreferenceResponse> UpdatePreferenceAsync(Guid viewerId, Guid id,
        PreferenceWeightRequest request, CancellationToken cancellationToken)
    {
        return (await service.UpdatePreferenceAsync(await accounts.GetActorAsync(cancellationToken), viewerId,
            id, request.Weight, cancellationToken)).ToResponse();
    }

    public async Task DeletePreferenceAsync(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        await service.DeletePreferenceAsync(await accounts.GetActorAsync(cancellationToken), viewerId, id, cancellationToken);
    }

    public async Task<List<WatchlistResponse>> ListWatchlistAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        return (await service.ListWatchlistAsync(await accounts.GetActorAsync(cancellationToken), viewerId, cancellationToken))
            .Select(e => e.ToResponse()).ToList();
    }

    public async Task<WatchlistResponse> GetWatchlistEntryAsync(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        return (await service.GetWatchlistEntryAsync(await accounts.GetActorAsync(cancellationToken), viewerId, id, cancellationToken)).ToResponse();
    }

    public async Task<WatchlistResponse> CreateWatchlistEntryAsync(Guid viewerId, WatchlistRequest request,
        CancellationToken cancellationToken)
    {
        return (await service.CreateWatchlistEntryAsync(await accounts.GetActorAsync(cancellationToken), viewerId,
            request.MovieId, request.Status, request.Note, cancellationToken)).ToResponse();
    }

    public async Task<WatchlistResponse> UpdateWatchlistEntryAsync(Guid viewerId, Guid id,
        WatchlistUpdateRequest request, CancellationToken cancellationToken)
    {
        return (await service.UpdateWatchlistEntryAsync(await accounts.GetActorAsync(cancellationToken), viewerId,
            id, request.Status, request.Note, cancellationToken)).ToResponse();
    }

    public async Task DeleteWatchlistEntryAsync(Guid viewerId, Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteWatchlistEntryAsync(await accounts.GetActorAsync(cancellationToken), viewerId, id, cancellationToken);
    }
}

public static class PersonalMappingExtensions
{
    public static PreferenceResponse ToResponse(this PreferenceDto dto)
    {
        return new(dto.Id, dto.ViewerId, dto.GenreId, dto.MoodId, dto.Weight);
    }
    public static WatchlistResponse ToResponse(this WatchlistDto dto)
    {
        return new(dto.Id, dto.ViewerId, dto.MovieId, dto.Status, dto.Note);
    }
}
