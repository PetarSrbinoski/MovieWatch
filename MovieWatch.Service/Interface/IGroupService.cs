using MovieWatch.Domain.Dto;

namespace MovieWatch.Service.Interface;

public interface IGroupService
{
    Task<List<GroupDto>> ListAsync(ActorDto actor, CancellationToken cancellationToken);
    Task<GroupDto> GetAsync(ActorDto actor, Guid groupId, CancellationToken cancellationToken);
    Task<GroupDto> CreateAsync(ActorDto actor, string name, string description, CancellationToken cancellationToken);
    Task<GroupDto> UpdateAsync(ActorDto actor, Guid groupId, string name, string description, CancellationToken cancellationToken);
    Task DeleteAsync(ActorDto actor, Guid groupId, CancellationToken cancellationToken);
    Task<MembershipDto> AddMemberAsync(ActorDto actor, Guid groupId, Guid viewerId, CancellationToken cancellationToken);
    Task<MembershipDto> GetMemberAsync(ActorDto actor, Guid groupId, Guid membershipId, CancellationToken cancellationToken);
    Task<MembershipDto> SetParticipationAsync(ActorDto actor, Guid groupId, Guid membershipId, bool included, CancellationToken cancellationToken);
    Task RemoveMemberAsync(ActorDto actor, Guid groupId, Guid membershipId, CancellationToken cancellationToken);
    Task<GroupDto> TransferOwnershipAsync(ActorDto actor, Guid groupId, Guid targetViewerId, CancellationToken cancellationToken);
}
