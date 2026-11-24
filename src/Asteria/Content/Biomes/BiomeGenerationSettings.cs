using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;
using Asteria.World.LevelGen.Placement;

namespace Asteria.Content.Biomes;

/// <summary>
/// Per-biome world-generation inputs from Minecraft Java 26.3:
/// configured carvers and ordered placed-feature groups indexed by decoration step.
/// </summary>
public sealed class BiomeGenerationSettings
{
    public static BiomeGenerationSettings EMPTY { get; } =
        new(
            HolderSet<WorldCarver>.Empty,
            Array.Empty<HolderSet<PlacedFeature>>());

    public static MapCodec<BiomeGenerationSettings> CODEC { get; } =
        RecordCodecBuilder.MapCodec<BiomeGenerationSettings>(instance =>
            instance.Group(
                    RegistryCodecs.HolderSet<WorldCarver>(Registries.CARVER)
                        .FieldOf("carvers")
                        .ForGetter<BiomeGenerationSettings>(static settings => settings.Carvers),
                    PlacedFeature.LIST_OF_LISTS_CODEC
                        .FieldOf("features")
                        .ForGetter<BiomeGenerationSettings>(static settings => settings.Features))
                .Apply(
                    instance,
                    static (carvers, features) =>
                        new BiomeGenerationSettings(carvers, features)));

    public HolderSet<WorldCarver> Carvers { get; }

    public IReadOnlyList<HolderSet<PlacedFeature>> Features { get; }

    private BiomeGenerationSettings(
        HolderSet<WorldCarver> carvers,
        IEnumerable<HolderSet<PlacedFeature>> features)
    {
        Carvers = carvers ?? throw new ArgumentNullException(nameof(carvers));
        ArgumentNullException.ThrowIfNull(features);

        HolderSet<PlacedFeature>[] featureSteps = features.ToArray();
        if (featureSteps.Any(static step => step is null))
            throw new ArgumentException("Feature steps cannot contain null holder sets.", nameof(features));

        Features = Array.AsReadOnly(featureSteps);
    }

    public IEnumerable<Holder<WorldCarver>> GetCarvers() => Carvers;

    public bool HasFeature(PlacedFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        return Features.Any(step => step.Any(holder => ReferenceEquals(holder.Value, feature)));
    }

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

    /// <summary>Builder for the carver and per-decoration-step feature lists.</summary>
    public sealed class PlainBuilder
    {
        private readonly List<Holder<WorldCarver>> _carvers = new();
        private readonly List<List<Holder<PlacedFeature>>> _features = new();

        public PlainBuilder AddCarver(Holder<WorldCarver> carver)
        {
            ArgumentNullException.ThrowIfNull(carver);
            _carvers.Add(carver);
            return this;
        }

        public PlainBuilder AddFeature(
            GenerationStep.Decoration step,
            Holder<PlacedFeature> feature)
        {
            ArgumentNullException.ThrowIfNull(step);
            int index = -1;
            for (int candidate = 0; candidate < GenerationStep.Decoration.Values.Count; candidate++)
            {
                if (ReferenceEquals(GenerationStep.Decoration.Values[candidate], step))
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(step));

            return AddFeature(index, feature);
        }

        public PlainBuilder AddFeature(int stepIndex, Holder<PlacedFeature> feature)
        {
            ArgumentNullException.ThrowIfNull(feature);
            if (stepIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(stepIndex));

            while (_features.Count <= stepIndex)
                _features.Add(new List<Holder<PlacedFeature>>());

            _features[stepIndex].Add(feature);
            return this;
        }

        public BiomeGenerationSettings Build() =>
            new(
                HolderSet<WorldCarver>.Direct(_carvers),
                _features.Select(static features =>
                    HolderSet<PlacedFeature>.Direct(features)));
    }
}
