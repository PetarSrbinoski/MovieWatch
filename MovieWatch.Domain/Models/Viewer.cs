using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class Viewer : BaseAuditableEntity
{
    public Viewer(string accountId, string displayName)
    {
        AccountId = accountId;
        DisplayName = displayName;
    }

    public string AccountId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
