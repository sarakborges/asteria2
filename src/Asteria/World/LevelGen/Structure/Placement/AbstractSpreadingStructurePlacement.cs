using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Shared frequency, salt, locate offset and exclusion-zone behavior from
/// Minecraft Java 26.3 AbstractSpreadingStructurePlacement.
/// </summary>
public abstract class AbstractSpreadingStructurePlacement : StructurePlacement
{
    protected const int HIGHLY_ARBITRARY_RANDOM_SALT = 10387320;

    protected sealed record PlacementSettings(
        Vec3i LocateOffset,
        FrequencyReductionMethod FrequencyReductionMethod,
        float Frequency,
        int Salt,
        ExclusionZone? ExclusionZone);

    private sealed record PlacementCoreSettings(
        Vec3i LocateOffset,
        FrequencyReductionMethod FrequencyReductionMethod,
        float Frequency,
        int Salt);

    private sealed record OptionalExclusionZone(ExclusionZone? Value);

    private static Codec<Vec3i> LocateOffsetCodec { get; } =
        Vec3i.CODEC.Validate(static value =>
            JavaAbs(value.GetX()) <= 16 &&
            JavaAbs(value.GetY()) <= 16 &&
            JavaAbs(value.GetZ()) <= 16
                ? DataResult<Vec3i>.Success(value)
                : DataResult<Vec3i>.Error(
                    () => $"Position out of range, expected at most 16: {value}"));

    private static int JavaAbs(int value) =>
        value == int.MinValue ? int.MinValue : Math.Abs(value);

    private static MapCodec<PlacementCoreSettings> PlacementCoreCodec { get; } =
        RecordCodecBuilder.MapCodec<PlacementCoreSettings>(instance =>
            instance.Group(
                LocateOffsetCodec.OptionalFieldOf("locate_offset", Vec3i.Zero)
                    .ForGetter<PlacementCoreSettings>(static settings => settings.LocateOffset),
                global::Asteria.World.LevelGen.Structure.Placement.FrequencyReductionMethod.CODEC
                    .OptionalFieldOf(
                        "frequency_reduction_method",
                        global::Asteria.World.LevelGen.Structure.Placement.FrequencyReductionMethod.DEFAULT)
                    .ForGetter<PlacementCoreSettings>(static settings => settings.FrequencyReductionMethod),
                Codecs.FloatRange(0.0F, 1.0F)
                    .OptionalFieldOf("frequency", 1.0F)
                    .ForGetter<PlacementCoreSettings>(static settings => settings.Frequency),
                Codecs.IntRange(0, int.MaxValue)
                    .FieldOf("salt")
                    .ForGetter<PlacementCoreSettings>(static settings => settings.Salt))
            .Apply(
                instance,
                static (locateOffset, reductionMethod, frequency, salt) =>
                    new PlacementCoreSettings(locateOffset, reductionMethod, frequency, salt)));

    private sealed class OptionalExclusionZoneMapCodec : MapCodec<OptionalExclusionZone>
    {
        private const string FieldName = "exclusion_zone";

        public override DataResult<OptionalExclusionZone> Decode<TData>(
            DynamicOps<TData> ops,
            MapLike<TData> input)
        {
            if (!input.TryGet(FieldName, out TData encoded))
                return DataResult<OptionalExclusionZone>.Success(
                    new OptionalExclusionZone(null));

            return ExclusionZone.CODEC.Parse(ops, encoded)
                .Map(static value => new OptionalExclusionZone(value));
        }

        public override DataResult<TData> Encode<TData>(
            OptionalExclusionZone input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input.Value is null)
                return DataResult<TData>.Success(prefix);

            return ExclusionZone.CODEC.EncodeStart(ops, input.Value).FlatMap(encoded =>
                ops.MergeToMap(prefix, ops.CreateString(FieldName), encoded));
        }

        public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
            new[] { ops.CreateString(FieldName) };
    }

    private static MapCodec<OptionalExclusionZone> OptionalExclusionZoneCodec { get; } =
        new OptionalExclusionZoneMapCodec();

    protected static MapCodec<PlacementSettings> PlacementCodec { get; } =
        RecordCodecBuilder.MapCodec<PlacementSettings>(instance =>
            instance.Group(
                PlacementCoreCodec.ForGetter<PlacementSettings>(static settings =>
                    new PlacementCoreSettings(
                        settings.LocateOffset,
                        settings.FrequencyReductionMethod,
                        settings.Frequency,
                        settings.Salt)),
                OptionalExclusionZoneCodec.ForGetter<PlacementSettings>(
                    static settings => new OptionalExclusionZone(settings.ExclusionZone)))
            .Apply(
                instance,
                static (core, exclusionZone) => new PlacementSettings(
                    core.LocateOffset,
                    core.FrequencyReductionMethod,
                    core.Frequency,
                    core.Salt,
                    exclusionZone.Value)));

    private readonly Vec3i _locateOffset;
    private readonly FrequencyReductionMethod _frequencyReductionMethod;
    private readonly float _frequency;
    private readonly int _salt;
    private readonly ExclusionZone? _exclusionZone;

    protected AbstractSpreadingStructurePlacement(
        Vec3i locateOffset,
        FrequencyReductionMethod frequencyReductionMethod,
        float frequency,
        int salt,
        ExclusionZone? exclusionZone)
    {
        _locateOffset = locateOffset ?? throw new ArgumentNullException(nameof(locateOffset));
        _frequencyReductionMethod = frequencyReductionMethod ??
            throw new ArgumentNullException(nameof(frequencyReductionMethod));
        _frequency = frequency;
        _salt = salt;
        _exclusionZone = exclusionZone;
    }

    protected PlacementSettings GetPlacementSettings() =>
        new(
            _locateOffset,
            _frequencyReductionMethod,
            _frequency,
            _salt,
            _exclusionZone);

    public virtual MapCodec<StructurePlacement> Codec() =>
        throw new NotSupportedException(
            $"No placement codec has been ported for {GetType().Name}.");

    public Vec3i LocateOffset() =>
        _locateOffset;

    protected FrequencyReductionMethod FrequencyReductionMethod() =>
        _frequencyReductionMethod;

    protected float Frequency() =>
        _frequency;

    protected int Salt() =>
        _salt;

    protected ExclusionZone? ExclusionZoneValue() =>
        _exclusionZone;

    public bool IsStructureChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ)
    {
        ArgumentNullException.ThrowIfNull(state);

        return IsPlacementChunk(state, sourceX, sourceZ) &&
               ApplyAdditionalChunkRestrictions(sourceX, sourceZ, state.LevelSeed) &&
               ApplyInteractionsWithOtherStructures(state, sourceX, sourceZ);
    }

    public bool ApplyAdditionalChunkRestrictions(
        int sourceX,
        int sourceZ,
        long levelSeed) =>
        !(_frequency < 1.0F) ||
        _frequencyReductionMethod.ShouldGenerate(
            levelSeed,
            _salt,
            sourceX,
            sourceZ,
            _frequency);

    /// <summary>
    /// Exposes the interface's default locate-position operation as a class member.
    /// C# default interface implementations are not inherited into the concrete class API.
    /// </summary>
    public BlockPos GetLocatePos(ChunkPos chunkPos)
    {
        BlockPos origin = new(
            chunkPos.GetMinBlockX(),
            0,
            chunkPos.GetMinBlockZ());

        return origin.Offset(_locateOffset);
    }

    public bool ApplyInteractionsWithOtherStructures(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _exclusionZone is null ||
               !_exclusionZone.IsPlacementForbidden(
                   state,
                   sourceX,
                   sourceZ);
    }

    protected abstract bool IsPlacementChunk(
        IStructurePlacementState state,
        int sourceX,
        int sourceZ);

    public sealed record ExclusionZone(
        Holder<StructureSet> OtherSet,
        int ChunkCount)
    {
        public static Codec<ExclusionZone> CODEC { get; } =
            RecordCodecBuilder.Create<ExclusionZone>(instance =>
                instance.Group(
                    RegistryCodecs.Holder<StructureSet>(BuiltInRegistries.StructureSet.Key)
                        .FieldOf("other_set")
                        .ForGetter<ExclusionZone>(static zone => zone.OtherSet),
                    Codecs.IntRange(1, 16)
                        .FieldOf("chunk_count")
                        .ForGetter<ExclusionZone>(static zone => zone.ChunkCount))
                .Apply(
                    instance,
                    static (otherSet, chunkCount) =>
                        new ExclusionZone(otherSet, chunkCount)));

        public bool IsPlacementForbidden(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ)
        {
            ArgumentNullException.ThrowIfNull(state);
            return state.HasStructureChunkInRange(
                OtherSet,
                sourceX,
                sourceZ,
                ChunkCount);
        }
    }
}
