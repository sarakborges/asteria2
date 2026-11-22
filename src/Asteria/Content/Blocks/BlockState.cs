using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Content.Fluids;

namespace Asteria.Content.Blocks;

public sealed class BlockState : StateHolder<Block, BlockState>
{
    private static class CodecHolder
    {
        private static readonly Codec<Block> OwnerCodec =
            BuiltInRegistries.Block.ByNameCodec();

        public static readonly Codec<BlockState> FULL_CODEC =
            new BlockStateCodec(OwnerCodec, allowCompactBlockId: false);

        public static readonly Codec<BlockState> CODEC =
            new BlockStateCodec(OwnerCodec, allowCompactBlockId: true);
    }

    public static Codec<BlockState> FULL_CODEC => CodecHolder.FULL_CODEC;

    public static Codec<BlockState> CODEC => CodecHolder.CODEC;

    public static Codec<BlockState> CreateCodec(Codec<Block> ownerCodec) =>
        new BlockStateCodec(ownerCodec, allowCompactBlockId: true);

    public static Codec<BlockState> CreateFullCodec(Codec<Block> ownerCodec) =>
        new BlockStateCodec(ownerCodec, allowCompactBlockId: false);

    public Block Block => Owner;
    public new StateDefinition Definition => (StateDefinition)base.Definition;

    public bool IsAir { get; }
    public float DestroySpeed { get; }
    public bool RequiresCorrectToolForDrops { get; }
    public bool CanOcclude { get; }
    public bool IsRandomlyTicking { get; }
    public bool IgnitedByLava { get; }
    public bool IsLiquid { get; }
    public bool IsReplaceable { get; }
    public bool ShouldSpawnTerrainParticles { get; }

    internal BlockState(StateDefinition definition, object[] values)
        : base(definition, values)
    {
        BlockProperties properties = Definition.Owner.Properties;
        IsAir = properties.IsAir;
        DestroySpeed = properties.DestroyTimeValue;
        RequiresCorrectToolForDrops = properties.RequiresCorrectToolForDropsValue;
        CanOcclude = properties.CanOccludeValue;
        IsRandomlyTicking = properties.IsRandomlyTickingValue;
        IgnitedByLava = properties.IgnitedByLavaValue;
        IsLiquid = properties.IsLiquidValue;
        IsReplaceable = properties.ReplaceableValue;
        ShouldSpawnTerrainParticles = properties.SpawnTerrainParticlesValue;
        global::Asteria.Content.Blocks.Block.BlockStateRegistry.Add(this);
    }

    public bool Is(Block block) => ReferenceEquals(Block, block);

    public bool Is(TagKey<Block> tag) =>
        Block.BuiltInRegistryHolder?.Is(tag) ?? false;

    public FluidState GetFluidState() => Block.GetFluidState(this);
}
