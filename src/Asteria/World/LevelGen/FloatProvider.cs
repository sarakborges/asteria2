using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Context-free world-generation float provider from Minecraft Java 26.3.
/// This is distinct from the context-sensitive context_float_provider registry.
/// </summary>
public interface FloatProvider
{
    float Sample(IRandomSource random);

    float Min();

    float Max();

    MapCodec<FloatProvider> Codec();
}
