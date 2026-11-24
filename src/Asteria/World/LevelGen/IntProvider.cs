using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Context-free integer provider contract used by Minecraft 26.3 world generation.</summary>
public interface IntProvider
{
    int Sample(IRandomSource random);

    int MinInclusive();

    int MaxInclusive();

    MapCodec<IntProvider> Codec();
}
