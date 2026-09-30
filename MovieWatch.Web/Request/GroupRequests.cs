using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public record GroupRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(1000)] string Description
    );

public record AddMemberRequest(
    Guid ViewerId
    );

public record ParticipationRequest(
    bool IncludedInRecommendations
    );

public record TransferOwnershipRequest(
    Guid ViewerId
    );
