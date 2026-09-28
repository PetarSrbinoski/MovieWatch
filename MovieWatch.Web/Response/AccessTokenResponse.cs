namespace MovieWatch.Web.Response;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string TokenType);
