namespace Sallimni.Domain.Common;

/// <summary>أساس لكل الكيانات: معرّف Guid + ختم زمني للإنشاء/التعديل.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
