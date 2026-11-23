namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Coordinate tiling behavior for GradientFunction from Minecraft Java 26.3.
/// </summary>
public enum TilingMode
{
    CLAMP_TO_EDGE,
    REPEAT,
    MIRRORED_REPEAT
}

public static class TilingModeExtensions
{
    public static string GetSerializedName(this TilingMode mode) =>
        mode switch
        {
            TilingMode.CLAMP_TO_EDGE => "clamp_to_edge",
            TilingMode.REPEAT => "repeat",
            TilingMode.MIRRORED_REPEAT => "mirrored_repeat",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
}
