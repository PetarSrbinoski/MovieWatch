using MovieWatch.Domain.Dto;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Extensions;

public static class AccountMappingExtensions
{
    public static RegisterViewerDto ToDto(this RegisterRequest request)
        => new(request.Email.Trim(), request.Password, request.DisplayName.Trim());

    public static LoginDto ToDto(this LoginRequest request)
        => new(request.Email.Trim(), request.Password);

    public static ViewerResponse ToResponse(this ViewerDto viewer)
        => new(viewer.Id, viewer.Email, viewer.DisplayName);

    public static AccessTokenResponse ToResponse(this AccessTokenDto token)
        => new(token.AccessToken, token.ExpiresAt, token.TokenType);
}
