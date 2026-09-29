namespace MovieWatch.Web.Response;

public sealed record MembershipResponse(Guid Id, Guid GroupId, Guid ViewerId, bool IncludedInRecommendations);
public sealed record GroupResponse(Guid Id, string Name, string Description, Guid OwnerViewerId,
    IReadOnlyList<MembershipResponse> Memberships);
