namespace MovieWatch.Domain.Dto;

public record AccessTokenDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string TokenType = "Bearer"
    );
