using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface IAccountService
{
    Task<ViewerDto> RegisterAsync(RegisterViewerDto registration, CancellationToken cancellationToken = default);
    Task<AccessTokenDto> LoginAsync(LoginDto login, CancellationToken cancellationToken = default);
    Task<ViewerDto> GetOwnProfileAsync(string accountId, CancellationToken cancellationToken = default);
    Task BootstrapAdministratorAsync(RegisterViewerDto registration, CancellationToken cancellationToken = default);
    Task<List<ViewerDto>> ListProfilesAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<ViewerDto> GetProfileAsync(Guid viewerId, CancellationToken cancellationToken = default);
    Task<ViewerDto> UpdateProfileAsync(Guid viewerId, UpdateViewerDto update, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid viewerId, CancellationToken cancellationToken = default);
}
