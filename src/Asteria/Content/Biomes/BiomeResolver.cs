using Asteria.Content.Registries;

namespace Asteria.Content.Biomes;

/// <summary>
/// Resolves a biome holder at quart-resolution world coordinates, as used by chunk sections.
/// </summary>
public interface BiomeResolver
{
    Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ);
}
