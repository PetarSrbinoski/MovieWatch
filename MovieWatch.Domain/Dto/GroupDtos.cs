namespace MovieWatch.Domain.Dto;

public sealed record MembershipDto(Guid Id, Guid GroupId, Guid ViewerId, bool IncludedInRecommendations);
public sealed record GroupDto(Guid Id, string Name, string Description, Guid OwnerViewerId,
    IReadOnlyList<MembershipDto> Memberships);
