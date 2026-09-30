namespace MovieWatch.Domain.Dto;

public record ViewerDto(
    Guid Id,
    string Email,
    string DisplayName
    );
