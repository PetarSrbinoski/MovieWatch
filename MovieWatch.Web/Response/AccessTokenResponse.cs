namespace MovieWatch.Web.Response;

public record AccessTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string TokenType
    );
