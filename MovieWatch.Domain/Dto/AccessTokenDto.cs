namespace MovieWatch.Domain.Dto;

public sealed record AccessTokenDto(string AccessToken, DateTimeOffset ExpiresAt, string TokenType = "Bearer");
