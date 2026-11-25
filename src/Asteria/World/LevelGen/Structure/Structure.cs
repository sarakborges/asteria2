using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Content.Identifiers;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Shared definition settings and bounding-box adjustment for a structure.
/// Generation-point selection and placement are separate operations that require
/// the structure manager and template systems not yet ported into Asteria.
/// </summary>
public abstract class Structure
{
    /// <summary>
    /// Dispatches structure data through registered structure-type map codecs.
    /// Types without a registered codec remain unavailable for serialization.
    /// </summary>
    public static MapCodec<Structure> DIRECT_CODEC { get; } =
        MapCodec<Structure>.Dispatch(
            Codecs.STRING.FieldOf("type"),
            static structure =>
            {
                ArgumentNullException.ThrowIfNull(structure);

                MapCodec<Structure> codec;
                try
                {
                    codec = structure.Codec();
                }
                catch (NotSupportedException exception)
                {
                    return DataResult<string>.Error(() => exception.Message);
                }

                RegistryKey<MapCodec<Structure>>? key =
                    BuiltInRegistries.StructureType.GetResourceKey(codec);

                return key.HasValue
                    ? DataResult<string>.Success(key.Value.Location.ToString())
                    : DataResult<string>.Error(() =>
                        $"Unregistered structure type codec for {structure.GetType().Name}");
            },
            static type =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(type);
                if (!location.HasValue)
                {
                    return DataResult<MapCodec<Structure>>.Error(() =>
                        $"Invalid structure type '{type}'");
                }

                MapCodec<Structure>? codec =
                    BuiltInRegistries.StructureType.GetValue(location.Value);

                return codec is not null
                    ? DataResult<MapCodec<Structure>>.Success(codec)
                    : DataResult<MapCodec<Structure>>.Error(() =>
                        $"Unknown structure type '{type}'");
            });

    /// <summary>
    /// Registry-aware holder codec, supporting registered structure references and
    /// top-level inline definitions through RegistryFileCodec.
    /// </summary>
    public static Codec<Holder<Structure>> CODEC { get; } =
        RegistryCodecs.Holder<Structure>(
            BuiltInRegistries.Structure.Key,
            DIRECT_CODEC);

    protected readonly StructureSettings _settings;

    protected Structure(StructureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public StructureSettings Settings => _settings;

    /// <summary>
    /// Returns the subtype's registered map codec. Concrete structure types must
    /// override this member; the base implementation deliberately fails closed.
    /// </summary>
    public virtual MapCodec<Structure> Codec() =>
        throw new NotSupportedException(
            $"No structure type codec has been ported for {GetType().Name}.");

    /// <summary>
    /// Creates the common one-field settings codec used by simple structure types.
    /// </summary>
    public static MapCodec<TStructure> SimpleCodec<TStructure>(
        Func<StructureSettings, TStructure> constructor)
        where TStructure : Structure
    {
        ArgumentNullException.ThrowIfNull(constructor);
        return StructureSettings.CODEC.XMap(
            constructor,
            static structure => structure.Settings);
    }

    /// <summary>
    /// Adapts a subtype codec for the base Structure type used by the type registry.
    /// Concrete structure types should retain the returned codec as a static singleton.
    /// </summary>
    public static MapCodec<Structure> AdaptCodec<TStructure>(
        MapCodec<TStructure> codec)
        where TStructure : Structure
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.XMap<Structure>(
            static structure => structure,
            static structure => structure as TStructure ??
                throw new InvalidOperationException(
                    $"Expected {typeof(TStructure).Name}, received {structure.GetType().Name}."));
    }

    public HolderSet<Biome> Biomes() =>
        _settings.Biomes;

    public GenerationStep.Decoration Step() =>
        _settings.Step;

    public TerrainAdjustment TerrainAdaptation() =>
        _settings.TerrainAdaptation;

    /// <summary>
    /// Inflates structure bounds by twelve blocks on all axes whenever terrain
    /// adaptation is not NONE, matching Minecraft Java 26.3 Structure.adjustBoundingBox.
    /// </summary>
    public BoundingBox AdjustBoundingBox(BoundingBox boundingBox)
    {
        ArgumentNullException.ThrowIfNull(boundingBox);

        return ReferenceEquals(TerrainAdaptation(), TerrainAdjustment.NONE)
            ? boundingBox
            : boundingBox.InflatedBy(12);
    }
}
