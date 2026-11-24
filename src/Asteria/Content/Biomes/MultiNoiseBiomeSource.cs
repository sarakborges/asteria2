using Asteria.Content.Registries;
using Asteria.World.LevelGen;

namespace Asteria.Content.Biomes;

/// <summary>
/// Multi-noise biome selection from Minecraft Java 26.3. The source keeps the
/// climate parameter index independent from the chunk-scoped climate sampler.
/// </summary>
public sealed class MultiNoiseBiomeSource
{
    private readonly Climate.ParameterList<Holder<Biome>> _parameters;
    private readonly IReadOnlySet<Holder<Biome>> _possibleBiomes;

    public IReadOnlySet<Holder<Biome>> PossibleBiomes => _possibleBiomes;

    public MultiNoiseBiomeSource(Climate.ParameterList<Holder<Biome>> parameters)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

        var possibleBiomes = new HashSet<Holder<Biome>>();
        foreach (var entry in parameters.Values())
            possibleBiomes.Add(entry.Second);

        if (possibleBiomes.Count == 0)
            throw new ArgumentException("A multi-noise biome source requires at least one biome entry.", nameof(parameters));

        _possibleBiomes = possibleBiomes;
    }

    public static MultiNoiseBiomeSource CreateFromList(
        Climate.ParameterList<Holder<Biome>> parameters) =>
        new(parameters);

    /// <summary>Returns the closest climate parameter entry using the vanilla seven-axis R-tree.</summary>
    public Holder<Biome> GetNoiseBiome(Climate.TargetPoint target) =>
        _parameters.FindValue(target);

    /// <summary>
    /// Binds the source to the six climate samplers used for chunk biome selection.
    /// The resulting resolver consumes quart-space coordinates, matching ChunkSection.
    /// </summary>
    public BiomeResolver CreateResolver(Climate.Sampler sampler)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        return new ClimateBiomeResolver(this, sampler);
    }

    private sealed class ClimateBiomeResolver(
        MultiNoiseBiomeSource source,
        Climate.Sampler sampler) : BiomeResolver
    {
        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ) =>
            source.GetNoiseBiome(sampler.Sample(quartX, quartY, quartZ));
    }
}
