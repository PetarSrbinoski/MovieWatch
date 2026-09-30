namespace MovieWatch.Domain.Dto;

public record RegisterViewerDto(
    string Email,
    string Password,
    string DisplayName
    );
