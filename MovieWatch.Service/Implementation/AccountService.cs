using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MovieWatch.Domain.Common;
using MovieWatch.Domain.Config;
using MovieWatch.Domain.Dto;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class AccountService(IIdentityRepository identity, IOptions<JwtSettings> settings) : IAccountService
{
    public Task<ViewerDto> RegisterAsync(RegisterViewerDto registration, CancellationToken cancellationToken = default)
        => identity.RegisterAsync(registration, AccountRoles.Viewer, cancellationToken);

    public async Task<AccessTokenDto> LoginAsync(LoginDto login, CancellationToken cancellationToken = default)
    {
        var account = await identity.CheckCredentialsAsync(login, cancellationToken)
            ?? throw new OperationException(FailureKind.Unauthenticated, "Invalid email or password.");
        return GenerateToken(account);
    }

    public async Task<ViewerDto> GetOwnProfileAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return await identity.GetProfileAsync(accountId, cancellationToken)
            ?? throw new OperationException(FailureKind.Unauthenticated, "The account is no longer available.");
    }

    public Task BootstrapAdministratorAsync(RegisterViewerDto registration, CancellationToken cancellationToken = default)
        => identity.EnsureAdministratorAsync(registration, cancellationToken);

    public Task<List<ViewerDto>> ListProfilesAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 100)
            throw new OperationException(FailureKind.Validation, "Use a nonnegative skip and a take of 1 to 100.");
        return identity.ListProfilesAsync(skip, take, cancellationToken);
    }

    public async Task<ViewerDto> GetProfileAsync(Guid viewerId, CancellationToken cancellationToken = default)
        => await identity.GetProfileByViewerIdAsync(viewerId, cancellationToken)
            ?? throw new OperationException(FailureKind.Validation, "Viewer was not found.");

    public Task<ViewerDto> UpdateProfileAsync(Guid viewerId, UpdateViewerDto update, CancellationToken cancellationToken = default)
        => identity.UpdateProfileAsync(viewerId, update, cancellationToken);

    public Task DeleteProfileAsync(Guid viewerId, CancellationToken cancellationToken = default)
        => identity.DeleteProfileAsync(viewerId, cancellationToken);

    private AccessTokenDto GenerateToken(AccountDto account)
    {
        var jwt = settings.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.LifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.AccountId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(account.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new AccessTokenDto(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
