using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Vertical bounds used by the vanilla noise-based chunk generator.
/// Contracts and validation follow Minecraft Java 26.3 NoiseSettings.
/// </summary>
public readonly record struct NoiseSettings(int MinY, int Height)
{
    // DimensionType derives these from BlockPos.PACKED_Y_LENGTH (12 in 26.3):
    // Y_SIZE = (1 << 12) - 32, MAX_Y = (Y_SIZE >> 1) - 1.
    public const int DimensionMinY = -2032;
    public const int DimensionMaxY = 2031;
    public const int DimensionYSize = 4064;

    public static Codec<NoiseSettings> CODEC { get; } =
        RecordCodecBuilder.Create<NoiseSettings>(instance =>
            instance.Group(
                Codecs.IntRange(DimensionMinY, DimensionMaxY)
                    .FieldOf("min_y")
                    .ForGetter<NoiseSettings>(static settings => settings.MinY),
                Codecs.IntRange(0, DimensionYSize)
                    .FieldOf("height")
                    .ForGetter<NoiseSettings>(static settings => settings.Height))
            .Apply(
                instance,
                static (minY, height) => new NoiseSettings(minY, height)))
        .FlatXMap(
            GuardY,
            static settings => DataResult<NoiseSettings>.Success(settings));

    internal static readonly NoiseSettings OVERWORLD_NOISE_SETTINGS = Create(-64, 384);
    internal static readonly NoiseSettings NETHER_NOISE_SETTINGS = Create(0, 128);
    internal static readonly NoiseSettings END_NOISE_SETTINGS = Create(0, 128);
    internal static readonly NoiseSettings CAVES_NOISE_SETTINGS = Create(-64, 192);
    internal static readonly NoiseSettings FLOATING_ISLANDS_NOISE_SETTINGS = Create(0, 256);

    private static DataResult<NoiseSettings> GuardY(NoiseSettings settings)
    {
        if (unchecked(settings.MinY + settings.Height) > DimensionMaxY + 1)
        {
            return DataResult<NoiseSettings>.Error(
                () => $"min_y + height cannot be higher than: {DimensionMaxY + 1}");
        }

        if (settings.Height % 16 != 0)
        {
            return DataResult<NoiseSettings>.Error(
                () => "height has to be a multiple of 16");
        }

        if (settings.MinY % 16 != 0)
        {
            return DataResult<NoiseSettings>.Error(
                () => "min_y has to be a multiple of 16");
        }

        return DataResult<NoiseSettings>.Success(settings);
    }

    /// <summary>
    /// Creates settings after applying the same vertical-alignment and upper-bound guards
    /// as NoiseSettings.create in Minecraft Java 26.3.
    /// </summary>
    public static NoiseSettings Create(int minY, int height)
    {
        NoiseSettings settings = new(minY, height);
        return GuardY(settings).GetOrThrow(
            message => new InvalidOperationException(message));
    }

    /// <summary>
    /// Intersects these noise bounds with a world's build-height bounds.
    /// As in 26.3, this returns the arithmetic intersection directly, including a
    /// non-positive height when the ranges do not overlap.
    /// </summary>
    public NoiseSettings ClampToHeightAccessor(LevelHeightAccessor heightAccessor)
    {
        ArgumentNullException.ThrowIfNull(heightAccessor);

        int newMinY = Math.Max(MinY, heightAccessor.GetMinY());
        int newHeight = Math.Min(
            unchecked(MinY + Height),
            unchecked(heightAccessor.GetMaxY() + 1)) - newMinY;
        return new NoiseSettings(newMinY, newHeight);
    }
}
