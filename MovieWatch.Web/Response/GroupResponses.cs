namespace MovieWatch.Web.Response;

public record MembershipResponse(
    Guid Id,
    Guid GroupId,
    Guid ViewerId,
    bool IncludedInRecommendations
    );

public record GroupResponse(
    Guid Id,
    string Name,
    string Description,
    Guid OwnerViewerId,
    IReadOnlyList<MembershipResponse> Memberships
    );
