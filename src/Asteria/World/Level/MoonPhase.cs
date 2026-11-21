using Asteria.Serialization;

namespace Asteria.World.Level;

public enum MoonPhase
{
    FULL_MOON = 0,
    WANING_GIBBOUS = 1,
    THIRD_QUARTER = 2,
    WANING_CRESCENT = 3,
    NEW_MOON = 4,
    WAXING_CRESCENT = 5,
    FIRST_QUARTER = 6,
    WAXING_GIBBOUS = 7
}

public static class MoonPhaseExtensions
{
    public const int PHASE_LENGTH = 24000;
    public const int COUNT = 8;

    public static Codec<MoonPhase> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            name => name switch
            {
                "full_moon" => DataResult<MoonPhase>.Success(MoonPhase.FULL_MOON),
                "waning_gibbous" => DataResult<MoonPhase>.Success(MoonPhase.WANING_GIBBOUS),
                "third_quarter" => DataResult<MoonPhase>.Success(MoonPhase.THIRD_QUARTER),
                "waning_crescent" => DataResult<MoonPhase>.Success(MoonPhase.WANING_CRESCENT),
                "new_moon" => DataResult<MoonPhase>.Success(MoonPhase.NEW_MOON),
                "waxing_crescent" => DataResult<MoonPhase>.Success(MoonPhase.WAXING_CRESCENT),
                "first_quarter" => DataResult<MoonPhase>.Success(MoonPhase.FIRST_QUARTER),
                "waxing_gibbous" => DataResult<MoonPhase>.Success(MoonPhase.WAXING_GIBBOUS),
                _ => DataResult<MoonPhase>.Error($"Unknown moon phase: {name}")
            },
            phase => DataResult<string>.Success(phase.GetSerializedName()))
        .OrCompressed(
            Codecs.INT.FlatXMap(
                index => index >= 0 && index < COUNT
                    ? DataResult<MoonPhase>.Success((MoonPhase)index)
                    : DataResult<MoonPhase>.Error($"Unknown moon phase id: {index}"),
                phase => DataResult<int>.Success(phase.Index())));

    public static int Index(this MoonPhase phase) => (int)phase;

    public static int StartTick(this MoonPhase phase) =>
        unchecked(phase.Index() * PHASE_LENGTH);

    public static string GetSerializedName(this MoonPhase phase) =>
        phase switch
        {
            MoonPhase.FULL_MOON => "full_moon",
            MoonPhase.WANING_GIBBOUS => "waning_gibbous",
            MoonPhase.THIRD_QUARTER => "third_quarter",
            MoonPhase.WANING_CRESCENT => "waning_crescent",
            MoonPhase.NEW_MOON => "new_moon",
            MoonPhase.WAXING_CRESCENT => "waxing_crescent",
            MoonPhase.FIRST_QUARTER => "first_quarter",
            MoonPhase.WAXING_GIBBOUS => "waxing_gibbous",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown moon phase.")
        };
}
