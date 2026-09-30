using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class Group : BaseAuditableEntity
{
    public Group(string name, string description, Guid ownerViewerId)
    {
        Name = name;
        Description = description;
        OwnerViewerId = ownerViewerId;
    }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid OwnerViewerId { get; set; }
    public List<GroupMembership> Memberships { get; set; } = [];
}

public sealed class GroupMembership : BaseAuditableEntity
{
    public GroupMembership(Guid groupId, Guid viewerId)
    {
        GroupId = groupId;
        ViewerId = viewerId;
    }
    public Guid GroupId { get; set; }
    public Guid ViewerId { get; set; }
    public bool IncludedInRecommendations { get; set; } = true;
}
