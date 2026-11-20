namespace Asteria.Content.Blocks;

/// <summary>
/// Mutable block behavior properties. Its fluent API follows BlockBehaviour.Properties
/// for the fields currently represented by Asteria.
/// </summary>
public sealed class BlockProperties
{
    public float DestroyTime { get; private set; }
    public float ExplosionResistance { get; private set; }
    public bool RequiresCorrectToolForDrops { get; private set; }
    public float Friction { get; private set; } = 0.6f;
    public bool IsAir { get; private set; }

    private BlockProperties()
    {
    }

    public static BlockProperties Of() => new();

    public BlockProperties Strength(float destroyTime, float explosionResistance) =>
        DestroyTime(destroyTime).ExplosionResistance(explosionResistance);

    public BlockProperties Strength(float destroyTime) =>
        Strength(destroyTime, destroyTime);

    public BlockProperties Instabreak() =>
        Strength(0.0f);

    public BlockProperties DestroyTime(float destroyTime)
    {
        DestroyTime = destroyTime;
        return this;
    }

    public BlockProperties ExplosionResistance(float explosionResistance)
    {
        ExplosionResistance = MathF.Max(0.0f, explosionResistance);
        return this;
    }

    public BlockProperties Friction(float friction)
    {
        Friction = friction;
        return this;
    }

    public BlockProperties RequiresCorrectToolForDrops()
    {
        RequiresCorrectToolForDrops = true;
        return this;
    }

    public BlockProperties Air()
    {
        IsAir = true;
        return this;
    }
}
