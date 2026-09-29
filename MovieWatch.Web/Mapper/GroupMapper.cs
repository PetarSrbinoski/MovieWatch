using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Mapper;

public sealed class GroupMapper(IGroupService service, AccountMapper accounts)
{
    public async Task<List<GroupResponse>> ListAsync(CancellationToken cancellationToken)
        => (await service.ListAsync(await accounts.GetActorAsync(cancellationToken), cancellationToken))
            .Select(g => g.ToResponse()).ToList();

    public async Task<GroupResponse> GetAsync(Guid groupId, CancellationToken cancellationToken)
        => (await service.GetAsync(await accounts.GetActorAsync(cancellationToken), groupId, cancellationToken)).ToResponse();

    public async Task<GroupResponse> CreateAsync(GroupRequest request, CancellationToken cancellationToken)
        => (await service.CreateAsync(await accounts.GetActorAsync(cancellationToken), request.Name,
            request.Description, cancellationToken)).ToResponse();

    public async Task<GroupResponse> UpdateAsync(Guid groupId, GroupRequest request, CancellationToken cancellationToken)
        => (await service.UpdateAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            request.Name, request.Description, cancellationToken)).ToResponse();

    public async Task DeleteAsync(Guid groupId, CancellationToken cancellationToken)
        => await service.DeleteAsync(await accounts.GetActorAsync(cancellationToken), groupId, cancellationToken);

    public async Task<MembershipResponse> AddMemberAsync(Guid groupId, AddMemberRequest request,
        CancellationToken cancellationToken)
        => (await service.AddMemberAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            request.ViewerId, cancellationToken)).ToResponse();

    public async Task<MembershipResponse> GetMemberAsync(Guid groupId, Guid membershipId, CancellationToken cancellationToken)
        => (await service.GetMemberAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            membershipId, cancellationToken)).ToResponse();

    public async Task<MembershipResponse> SetParticipationAsync(Guid groupId, Guid membershipId,
        ParticipationRequest request, CancellationToken cancellationToken)
        => (await service.SetParticipationAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            membershipId, request.IncludedInRecommendations, cancellationToken)).ToResponse();

    public async Task RemoveMemberAsync(Guid groupId, Guid membershipId, CancellationToken cancellationToken)
        => await service.RemoveMemberAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            membershipId, cancellationToken);

    public async Task<GroupResponse> TransferOwnershipAsync(Guid groupId, TransferOwnershipRequest request,
        CancellationToken cancellationToken)
        => (await service.TransferOwnershipAsync(await accounts.GetActorAsync(cancellationToken), groupId,
            request.ViewerId, cancellationToken)).ToResponse();
}

public static class GroupMappingExtensions
{
    public static MembershipResponse ToResponse(this MembershipDto dto)
        => new(dto.Id, dto.GroupId, dto.ViewerId, dto.IncludedInRecommendations);
    public static GroupResponse ToResponse(this GroupDto dto)
        => new(dto.Id, dto.Name, dto.Description, dto.OwnerViewerId, dto.Memberships.Select(m => m.ToResponse()).ToArray());
}
