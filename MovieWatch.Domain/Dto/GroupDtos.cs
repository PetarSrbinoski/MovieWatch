namespace MovieWatch.Domain.Dto;

public record MembershipDto(
    Guid Id,
    Guid GroupId,
    Guid ViewerId,
    bool IncludedInRecommendations
    );

public record GroupDto(
    Guid Id,
    string Name,
    string Description,
    Guid OwnerViewerId,
    IReadOnlyList<MembershipDto> Memberships
    );
