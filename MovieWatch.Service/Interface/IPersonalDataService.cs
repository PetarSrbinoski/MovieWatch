using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;

namespace MovieWatch.Service.Interface;

public interface IPersonalDataService
{
    Task<List<PreferenceDto>> ListPreferencesAsync(ActorDto actor, Guid viewerId, CancellationToken cancellationToken);
    Task<PreferenceDto> GetPreferenceAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken);
    Task<PreferenceDto> CreatePreferenceAsync(ActorDto actor, Guid viewerId, Guid genreId, Guid moodId, int weight, CancellationToken cancellationToken);
    Task<PreferenceDto> UpdatePreferenceAsync(ActorDto actor, Guid viewerId, Guid id, int weight, CancellationToken cancellationToken);
    Task DeletePreferenceAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken);
    Task<List<WatchlistDto>> ListWatchlistAsync(ActorDto actor, Guid viewerId, CancellationToken cancellationToken);
    Task<WatchlistDto> GetWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken);
    Task<WatchlistDto> CreateWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid movieId, WatchStatus status, string note, CancellationToken cancellationToken);
    Task<WatchlistDto> UpdateWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id, WatchStatus status, string note, CancellationToken cancellationToken);
    Task DeleteWatchlistEntryAsync(ActorDto actor, Guid viewerId, Guid id, CancellationToken cancellationToken);
}
