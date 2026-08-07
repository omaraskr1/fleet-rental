namespace FleetRental.Domain.Common;

/// <summary>
/// Base for all persisted aggregate roots and entities. Ids are assigned by the
/// application (not the database) so an entity is fully valid before it is saved.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
