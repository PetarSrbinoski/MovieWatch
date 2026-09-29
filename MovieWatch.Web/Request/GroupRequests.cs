using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public sealed record GroupRequest([Required, MaxLength(100)] string Name, [MaxLength(1000)] string Description);
public sealed record AddMemberRequest(Guid ViewerId);
public sealed record ParticipationRequest(bool IncludedInRecommendations);
public sealed record TransferOwnershipRequest(Guid ViewerId);
