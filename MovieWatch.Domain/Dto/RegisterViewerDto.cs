namespace MovieWatch.Domain.Dto;

public sealed record RegisterViewerDto(string Email, string Password, string DisplayName);
