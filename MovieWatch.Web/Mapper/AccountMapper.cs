using System.Security.Claims;
using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Extensions;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Mapper;

public sealed class AccountMapper(IAccountService accounts, IHttpContextAccessor accessor)
{
    public async Task<ViewerResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        return (await accounts.RegisterAsync(request.ToDto(), cancellationToken)).ToResponse();
    }

    public async Task<AccessTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        return (await accounts.LoginAsync(request.ToDto(), cancellationToken)).ToResponse();
    }

    public async Task<ViewerResponse> GetOwnProfileAsync(CancellationToken cancellationToken)
    {
        var accountId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new OperationException(FailureKind.Unauthenticated, "Authentication is required.");
        return (await accounts.GetOwnProfileAsync(accountId, cancellationToken)).ToResponse();
    }

    public async Task<Guid> GetOwnViewerIdAsync(CancellationToken cancellationToken)
    {
        var accountId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new OperationException(FailureKind.Unauthenticated, "Authentication is required.");
        return (await accounts.GetOwnProfileAsync(accountId, cancellationToken)).Id;
    }

    public Task<ViewerResponse> UpdateAsync(Guid viewerId, UpdateViewerRequest request, CancellationToken cancellationToken)
    {
        return UpdateCoreAsync(viewerId, request, cancellationToken);
    }

    private async Task<ViewerResponse> UpdateCoreAsync(Guid viewerId, UpdateViewerRequest request, CancellationToken cancellationToken)
    {
        return (await accounts.UpdateProfileAsync(viewerId, new(request.Email, request.DisplayName), cancellationToken)).ToResponse();
    }

    public async Task<List<ViewerResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        return (await accounts.ListProfilesAsync(skip, take, cancellationToken)).Select(v => v.ToResponse()).ToList();
    }

    public async Task<ViewerResponse> GetAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        return (await accounts.GetProfileAsync(viewerId, cancellationToken)).ToResponse();
    }

    public Task DeleteAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        return accounts.DeleteProfileAsync(viewerId, cancellationToken);
    }

    public async Task DeleteOwnAsync(CancellationToken cancellationToken)
    {
        await accounts.DeleteProfileAsync(await GetOwnViewerIdAsync(cancellationToken), cancellationToken);
    }

    public async Task<ViewerResponse> UpdateOwnAsync(UpdateViewerRequest request, CancellationToken cancellationToken)
    {
        return await UpdateCoreAsync(await GetOwnViewerIdAsync(cancellationToken), request, cancellationToken);
    }

    public async Task<ActorDto> GetActorAsync(CancellationToken cancellationToken)
    {
        return new(await GetOwnViewerIdAsync(cancellationToken),
            accessor.HttpContext?.User.IsInRole(nameof(AccountRole.Administrator)) == true);
    }
}
