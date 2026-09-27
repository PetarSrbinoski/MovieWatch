using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Models;

public sealed class Viewer : BaseAuditableEntity
{
    private Viewer() { }

    public Viewer(string accountId, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
            throw new OperationException(FailureKind.Validation, "Display name must contain 1 to 100 characters.");
        AccountId = accountId;
        DisplayName = displayName.Trim();
    }

    public string AccountId { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;

    public void Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
            throw new OperationException(FailureKind.Validation, "Display name must contain 1 to 100 characters.");
        DisplayName = displayName.Trim();
    }
}
