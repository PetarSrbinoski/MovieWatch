namespace MovieWatch.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
}
