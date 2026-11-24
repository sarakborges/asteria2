using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Features;

/// <summary>
/// Algorithm contract for a Minecraft 26.3 world-generation feature.
/// Feature parameters live in the concrete feature object and its codec.
/// </summary>
public interface Feature
{
    MapCodec<Feature> Codec();

    bool Place(
        OverworldWorld level,
        NoiseBasedChunkGenerator generator,
        IRandomSource random,
        BlockPos origin);

    IEnumerable<Holder<Feature>> GetSubFeatures() =>
        Array.Empty<Holder<Feature>>();
}
