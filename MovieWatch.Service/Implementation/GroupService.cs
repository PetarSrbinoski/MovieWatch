using MovieWatch.Domain.Common;
using MovieWatch.Domain.Dto;
using MovieWatch.Domain.Models;
using MovieWatch.Repository.Interface;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class GroupService(
    IRepository<Group> groups, IRepository<GroupMembership> memberships,
    IRepository<Viewer> viewers) : IGroupService
{
    public async Task<List<GroupDto>> ListAsync(ActorDto actor, int skip, int take, CancellationToken cancellationToken)
    {
        Pagination.Validate(skip, take);
        var accessible = await groups.PageAsync(skip, take, cancellationToken,
            g => actor.IsAdministrator || g.OwnerViewerId == actor.ViewerId
                || g.Memberships.Any(m => m.ViewerId == actor.ViewerId));
        return accessible.Select(ToDto).ToList();
    }

    public async Task<GroupDto> GetAsync(ActorDto actor, Guid groupId, CancellationToken cancellationToken)
    {
        return ToDto(await RequireReadableAsync(actor, groupId, cancellationToken));
    }

    public async Task<GroupDto> CreateAsync(ActorDto actor, string name, string description, CancellationToken cancellationToken)
    {
        var group = new Group(CleanName(name), CleanDescription(description), actor.ViewerId);
        group.Memberships.Add(new GroupMembership(group.Id, actor.ViewerId));
        await groups.InsertAsync(group, cancellationToken);
        return await GetAsync(actor, group.Id, cancellationToken);
    }

    public async Task<GroupDto> UpdateAsync(ActorDto actor, Guid groupId, string name, string description,
        CancellationToken cancellationToken)
    {
        var group = await RequireOwnerAsync(actor, groupId, cancellationToken);
        group.Name = CleanName(name);
        group.Description = CleanDescription(description);
        await groups.SaveAsync(cancellationToken);
        return await GetAsync(actor, groupId, cancellationToken);
    }

    public async Task DeleteAsync(ActorDto actor, Guid groupId, CancellationToken cancellationToken)
    {
        await groups.DeleteAsync(await RequireOwnerAsync(actor, groupId, cancellationToken), cancellationToken);
    }

    public async Task<MembershipDto> AddMemberAsync(ActorDto actor, Guid groupId, Guid viewerId,
        CancellationToken cancellationToken)
    {
        await RequireOwnerAsync(actor, groupId, cancellationToken);
        if (await viewers.GetAsync(v => v.Id == viewerId, cancellationToken) is null)
            throw new OperationException(FailureKind.Validation, "Viewer was not found.");
        return ToDto(await memberships.InsertAsync(new GroupMembership(groupId, viewerId), cancellationToken));
    }

    public async Task<MembershipDto> GetMemberAsync(ActorDto actor, Guid groupId, Guid membershipId,
        CancellationToken cancellationToken)
    {
        await RequireReadableAsync(actor, groupId, cancellationToken);
        var member = await memberships.GetAsync(m => m.Id == membershipId && m.GroupId == groupId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Membership was not found.");
        return ToDto(member);
    }

    public async Task<MembershipDto> SetParticipationAsync(ActorDto actor, Guid groupId, Guid membershipId,
        bool included, CancellationToken cancellationToken)
    {
        await RequireReadableAsync(actor, groupId, cancellationToken);
        var member = await memberships.FindAsync(m => m.Id == membershipId && m.GroupId == groupId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Membership was not found.");
        if (member.ViewerId != actor.ViewerId && !actor.IsAdministrator)
            throw new OperationException(FailureKind.NotFound, "Membership was not found.");
        member.IncludedInRecommendations = included;
        await memberships.SaveAsync(cancellationToken);
        return ToDto(member);
    }

    public async Task RemoveMemberAsync(ActorDto actor, Guid groupId, Guid membershipId,
        CancellationToken cancellationToken)
    {
        var group = await RequireReadableAsync(actor, groupId, cancellationToken);
        var member = await memberships.FindAsync(m => m.Id == membershipId && m.GroupId == groupId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Membership was not found.");
        if (actor.ViewerId != member.ViewerId && actor.ViewerId != group.OwnerViewerId && !actor.IsAdministrator)
            throw new OperationException(FailureKind.NotFound, "Membership was not found.");
        if (member.ViewerId == group.OwnerViewerId)
            throw new OperationException(FailureKind.Conflict, "Transfer ownership or delete the group before removing its owner.");
        await memberships.DeleteAsync(member, cancellationToken);
    }

    public async Task<GroupDto> TransferOwnershipAsync(ActorDto actor, Guid groupId, Guid targetViewerId,
        CancellationToken cancellationToken)
    {
        var group = await RequireOwnerAsync(actor, groupId, cancellationToken);
        if (await memberships.GetAsync(m => m.GroupId == groupId && m.ViewerId == targetViewerId, cancellationToken) is null)
            throw new OperationException(FailureKind.Validation, "The new owner must be a group member.");
        group.OwnerViewerId = targetViewerId;
        await groups.SaveAsync(cancellationToken);
        return await GetAsync(actor, groupId, cancellationToken);
    }

    private async Task<Group> RequireReadableAsync(ActorDto actor, Guid groupId, CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Group was not found.");
        if (!actor.IsAdministrator && group.OwnerViewerId != actor.ViewerId
            && await memberships.GetAsync(m => m.GroupId == groupId && m.ViewerId == actor.ViewerId, cancellationToken) is null)
            throw new OperationException(FailureKind.NotFound, "Group was not found.");
        return group;
    }

    private async Task<Group> RequireOwnerAsync(ActorDto actor, Guid groupId, CancellationToken cancellationToken)
    {
        var group = await groups.FindAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new OperationException(FailureKind.NotFound, "Group was not found.");
        if (!actor.IsAdministrator && group.OwnerViewerId != actor.ViewerId)
            throw new OperationException(FailureKind.NotFound, "Group was not found.");
        return group;
    }

    private static GroupDto ToDto(Group group)
    {
        return new(group.Id, group.Name, group.Description,
        group.OwnerViewerId, group.Memberships.OrderBy(m => m.Id).Select(ToDto).ToArray());
    }
    private static MembershipDto ToDto(GroupMembership member)
    {
        return new(member.Id, member.GroupId, member.ViewerId, member.IncludedInRecommendations);
    }
    private static string CleanName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100 ? name.Trim()
            : throw new OperationException(FailureKind.Validation, "Group name must contain 1 to 100 characters.");
    }
    private static string CleanDescription(string description)
    {
        return (description?.Trim() ?? "") is { Length: <= 1000 } value ? value
            : throw new OperationException(FailureKind.Validation, "Description must be at most 1000 characters.");
    }
}
