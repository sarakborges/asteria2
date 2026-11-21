namespace Asteria.Core;

/// <summary>
/// Base data for a geometric hit result. Entity-specific distance helpers remain outside this domain
/// until an entity model exists.
/// </summary>
public abstract class HitResult
{
    public enum Type
    {
        MISS,
        BLOCK,
        ENTITY
    }

    public Vec3 Location { get; }

    protected HitResult(Vec3 location) =>
        Location = location ?? throw new ArgumentNullException(nameof(location));

    public Vec3 GetLocation() => Location;

    // System.Object.GetType is non-virtual in .NET, so the source API is exposed as GetHitType.
    public abstract Type GetHitType();
}
