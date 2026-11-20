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
    public float SpeedFactorValue { get; private set; } = 1.0f;
    public float JumpFactorValue { get; private set; } = 1.0f;
    public float BounceRestitutionValue { get; private set; }
    public float FallDistanceReductionValue { get; private set; }
    public bool HasCollisionValue { get; private set; } = true;
    public bool CanOccludeValue { get; private set; } = true;
    public bool IsRandomlyTickingValue { get; private set; }
    public bool DynamicShapeValue { get; private set; }
    public bool IgnitedByLavaValue { get; private set; }
    public bool IsLiquidValue { get; private set; }
    public bool ReplaceableValue { get; private set; }
    public bool SpawnTerrainParticlesValue { get; private set; } = true;
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

    public BlockProperties SpeedFactor(float speedFactor)
    {
        SpeedFactorValue = speedFactor;
        return this;
    }

    public BlockProperties JumpFactor(float jumpFactor)
    {
        JumpFactorValue = jumpFactor;
        return this;
    }

    public BlockProperties BounceRestitution(float bounceRestitution)
    {
        BounceRestitutionValue = bounceRestitution;
        return this;
    }

    public BlockProperties FallDistanceReduction(float fallDistanceReduction)
    {
        FallDistanceReductionValue = fallDistanceReduction;
        return this;
    }

    public BlockProperties NoCollision()
    {
        HasCollisionValue = false;
        return this;
    }

    public BlockProperties NoOcclusion()
    {
        CanOccludeValue = false;
        return this;
    }

    public BlockProperties RandomTicks()
    {
        IsRandomlyTickingValue = true;
        return this;
    }

    public BlockProperties DynamicShape()
    {
        DynamicShapeValue = true;
        return this;
    }

    public BlockProperties IgnitedByLava()
    {
        IgnitedByLavaValue = true;
        return this;
    }

    public BlockProperties Liquid()
    {
        IsLiquidValue = true;
        return this;
    }

    public BlockProperties Replaceable()
    {
        ReplaceableValue = true;
        return this;
    }

    public BlockProperties NoTerrainParticles()
    {
        SpawnTerrainParticlesValue = false;
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
