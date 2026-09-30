using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;

namespace MovieWatch.Repository.Interface;

public interface IIdentityRepository
{
    Task<ViewerDto> RegisterAsync(RegisterViewerDto registration, AccountRole role, CancellationToken cancellationToken = default);
    Task<AccountDto?> CheckCredentialsAsync(LoginDto login, CancellationToken cancellationToken = default);
    Task<ViewerDto?> GetProfileAsync(string accountId, CancellationToken cancellationToken = default);
    Task EnsureAdministratorAsync(RegisterViewerDto registration, CancellationToken cancellationToken = default);
    Task<List<ViewerDto>> ListProfilesAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<ViewerDto?> GetProfileByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default);
    Task<ViewerDto> UpdateProfileAsync(Guid viewerId, UpdateViewerDto update, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid viewerId, CancellationToken cancellationToken = default);
}
