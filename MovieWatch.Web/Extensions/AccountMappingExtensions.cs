using MovieWatch.Domain.Dto;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Extensions;

public static class AccountMappingExtensions
{
    public static RegisterViewerDto ToDto(this RegisterRequest request)
    {
        return new(request.Email.Trim(), request.Password, request.DisplayName.Trim());
    }

    public static LoginDto ToDto(this LoginRequest request)
    {
        return new(request.Email.Trim(), request.Password);
    }

    public static ViewerResponse ToResponse(this ViewerDto viewer)
    {
        return new(viewer.Id, viewer.Email, viewer.DisplayName);
    }

    public static AccessTokenResponse ToResponse(this AccessTokenDto token)
    {
        return new(token.AccessToken, token.ExpiresAt, token.TokenType);
    }
}
