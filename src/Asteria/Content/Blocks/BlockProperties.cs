using Asteria.Core;
using Asteria.Content.Materials;
using Asteria.World;

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
    public Func<BlockState, int> LightEmissionValue { get; private set; } = static _ => 0;
    public Func<BlockState, bool> EmissiveRenderingValue { get; private set; } = static _ => false;

    public Func<BlockState, BlockPos, Vec3>? OffsetFunctionValue { get; private set; }

    public Func<BlockState, BlockGetter, BlockPos, bool> IsRedstoneConductorValue { get; private set; } =
        static (state, level, position) => state.IsCollisionShapeFullBlock(level, position);

    public Func<BlockState, BlockGetter, BlockPos, bool> IsSuffocatingValue { get; private set; } =
        static (state, level, position) =>
            state.Is(BlockTags.CausesSuffocation) &&
            state.IsCollisionShapeFullBlock(level, position);

    public Func<BlockState, BlockGetter, BlockPos, AABB, bool> IsViewBlockingValue { get; private set; } =
        static (state, level, position, _) => state.IsSuffocating(level, position);

    public Func<BlockState, BlockGetter, BlockPos, BlockPos?> PostProcessValue { get; private set; } =
        static (_, _, _) => null;

    public PushReaction PushReactionValue { get; private set; } = PushReaction.PUSH_PULL;

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
        CanOccludeValue = false;
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

    public BlockProperties LightLevel(Func<BlockState, int> lightEmission)
    {
        LightEmissionValue = lightEmission ?? throw new ArgumentNullException(nameof(lightEmission));
        return this;
    }

    public BlockProperties EmissiveRendering(Func<BlockState, bool> emissiveRendering)
    {
        EmissiveRenderingValue = emissiveRendering ??
            throw new ArgumentNullException(nameof(emissiveRendering));
        return this;
    }

    public BlockProperties OffsetType(global::Asteria.Content.Blocks.OffsetType offsetType)
    {
        OffsetFunctionValue = offsetType switch
        {
            global::Asteria.Content.Blocks.OffsetType.NONE => null,
            global::Asteria.Content.Blocks.OffsetType.XZ => CalculateHorizontalOffset,
            global::Asteria.Content.Blocks.OffsetType.XYZ => CalculateThreeAxisOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(offsetType))
        };
        return this;
    }

    public BlockProperties IsRedstoneConductor(
        Func<BlockState, BlockGetter, BlockPos, bool> predicate)
    {
        IsRedstoneConductorValue = predicate ??
            throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public BlockProperties IsSuffocating(
        Func<BlockState, BlockGetter, BlockPos, bool> predicate)
    {
        IsSuffocatingValue = predicate ??
            throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public BlockProperties IsViewBlocking(
        Func<BlockState, BlockGetter, BlockPos, AABB, bool> predicate)
    {
        IsViewBlockingValue = predicate ??
            throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public BlockProperties PostProcess(
        Func<BlockState, BlockGetter, BlockPos, BlockPos?> postProcess)
    {
        PostProcessValue = postProcess ??
            throw new ArgumentNullException(nameof(postProcess));
        return this;
    }

    public BlockProperties PushReaction(PushReaction pushReaction)
    {
        PushReactionValue = pushReaction;
        return this;
    }

    private static Vec3 CalculateHorizontalOffset(BlockState state, BlockPos position)
    {
        long seed = Mth.GetSeed(position.GetX(), 0, position.GetZ());
        float maxHorizontalOffset = state.Block.GetMaxHorizontalOffsetForState();
        float xFraction = (float)(seed & 0xFL) / 15.0f;
        float zFraction = (float)(seed >> 8 & 0xFL) / 15.0f;
        double x = Mth.Clamp(((double)xFraction - 0.5) * 0.5,
            -maxHorizontalOffset, maxHorizontalOffset);
        double z = Mth.Clamp(((double)zFraction - 0.5) * 0.5,
            -maxHorizontalOffset, maxHorizontalOffset);
        return new Vec3(x, 0.0, z);
    }

    private static Vec3 CalculateThreeAxisOffset(BlockState state, BlockPos position)
    {
        long seed = Mth.GetSeed(position.GetX(), 0, position.GetZ());
        float maxVerticalOffset = state.Block.GetMaxVerticalOffsetForState();
        Vec3 horizontal = CalculateHorizontalOffset(state, position);
        float yFraction = (float)(seed >> 4 & 0xFL) / 15.0f;
        double y = ((double)yFraction - 1.0) * maxVerticalOffset;
        return new Vec3(horizontal.X, y, horizontal.Z);
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

    [Obsolete("Legacy compatibility flag.")]
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
