namespace Asteria.Content.Blocks;

/// <summary>
/// Mutable block behavior properties. Its fluent API follows BlockBehaviour.Properties
/// for the fields currently represented by Asteria.
/// </summary>
public sealed class BlockProperties
{
    public float DestroyTimeValue { get; private set; }
    public float ExplosionResistanceValue { get; private set; }
    public bool RequiresCorrectToolForDropsValue { get; private set; }
    public float FrictionValue { get; private set; } = 0.6f;
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
        DestroyTimeValue = destroyTime;
        return this;
    }

    public BlockProperties ExplosionResistance(float explosionResistance)
    {
        ExplosionResistanceValue = MathF.Max(0.0f, explosionResistance);
        return this;
    }

    public BlockProperties Friction(float friction)
    {
        FrictionValue = friction;
        return this;
    }

    public BlockProperties RequiresCorrectToolForDrops()
    {
        RequiresCorrectToolForDropsValue = true;
        return this;
    }

    public BlockProperties Air()
    {
        IsAir = true;
        return this;
    }
}
