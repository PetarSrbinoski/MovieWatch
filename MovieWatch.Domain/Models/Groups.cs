using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class Group : BaseAuditableEntity
{
    private Group() { }
    public Group(string name, string description, Guid ownerViewerId)
    {
        Name = name;
        Description = description;
        OwnerViewerId = ownerViewerId;
        Memberships.Add(new GroupMembership(Id, ownerViewerId));
    }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public Guid OwnerViewerId { get; private set; }
    public List<GroupMembership> Memberships { get; private set; } = [];
    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }
    public void TransferOwnership(Guid viewerId) => OwnerViewerId = viewerId;
}

public sealed class GroupMembership : BaseAuditableEntity
{
    private GroupMembership() { }
    public GroupMembership(Guid groupId, Guid viewerId)
    {
        GroupId = groupId;
        ViewerId = viewerId;
    }
    public Guid GroupId { get; private set; }
    public Guid ViewerId { get; private set; }
    public bool IncludedInRecommendations { get; private set; } = true;
    public void SetParticipation(bool included) => IncludedInRecommendations = included;
}
