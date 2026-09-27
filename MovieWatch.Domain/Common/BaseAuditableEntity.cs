namespace MovieWatch.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = "";
    public string UpdatedBy { get; private set; } = "";

    public void Stamp(DateTimeOffset now, string actor, bool created)
    {
        if (created)
        {
            CreatedAt = now;
            CreatedBy = actor;
        }
        UpdatedAt = now;
        UpdatedBy = actor;
    }
}
