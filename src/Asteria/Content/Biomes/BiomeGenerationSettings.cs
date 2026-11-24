using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen;

namespace Asteria.Content.Biomes;

/// <summary>
/// Generation settings currently ported for the carver portion of Minecraft Java 26.3.
/// Placed-feature groups and their codec remain unported until PlacedFeature exists.
/// </summary>
public sealed class BiomeGenerationSettings
{
    public static BiomeGenerationSettings EMPTY { get; } =
        new(HolderSet<WorldCarver>.Empty);

    private readonly HolderSet<WorldCarver> _carvers;

    public HolderSet<WorldCarver> Carvers => _carvers;

    private BiomeGenerationSettings(HolderSet<WorldCarver> carvers)
    {
        _carvers = carvers ?? throw new ArgumentNullException(nameof(carvers));
    }

    public IEnumerable<Holder<WorldCarver>> GetCarvers() => _carvers;

    /// <summary>
    /// Creates the three default carver references used by OverworldBiomes.globalOverworldGeneration.
    /// The carver registry must already be bootstrapped before this factory is called.
    /// </summary>
    public static BiomeGenerationSettings CreateDefaultOverworld()
    {
        var builder = new PlainBuilder();
        builder.AddCarver(GetCarverHolder(Carvers.CAVE));
        builder.AddCarver(GetCarverHolder(Carvers.CAVE_EXTRA_UNDERGROUND));
        builder.AddCarver(GetCarverHolder(Carvers.CANYON));
        return builder.Build();
    }

    private static Holder<WorldCarver> GetCarverHolder(RegistryKey<WorldCarver> key) =>
        BuiltInRegistries.Carver.GetHolder(key) ??
        throw new InvalidOperationException($"Missing configured world carver {key}.");

    /// <summary>Carver-only projection of BiomeGenerationSettings.PlainBuilder.</summary>
    public sealed class PlainBuilder
    {
        private readonly List<Holder<WorldCarver>> _carvers = new();

        public PlainBuilder AddCarver(Holder<WorldCarver> carver)
        {
            ArgumentNullException.ThrowIfNull(carver);
            _carvers.Add(carver);
            return this;
        }

        public BiomeGenerationSettings Build() =>
            new(HolderSet<WorldCarver>.Direct(_carvers));
    }
}
