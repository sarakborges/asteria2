using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>Particle options carrying the block state used to select the particle texture.</summary>
public sealed class BlockParticleOption : IParticleOptions
{
    public ParticleType<BlockParticleOption> Type { get; }
    public BlockState State { get; }

    public BlockParticleOption(ParticleType<BlockParticleOption> type, BlockState state)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public static MapCodec<BlockParticleOption> CreateCodec(
        ParticleType<BlockParticleOption> type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Codec<BlockState> stateCodec = BlockState.CODEC.WithAlternative(
            BuiltInRegistries.Block.ByNameCodec(),
            static block => block.DefaultState);

        return stateCodec.FieldOf("block_state").XMap(
            state => new BlockParticleOption(type, state),
            static options => options.State);
    }

    public ParticleType GetParticleType() => Type;
}
