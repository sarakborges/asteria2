using System.Globalization;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Small shared helpers used by Minecraft 26.3 noise implementations.
/// </summary>
public static class NoiseUtils
{
    public static double BiasTowardsExtreme(double noise, double factor) =>
        noise + Math.Sin(Math.PI * noise) * factor / Math.PI;

    public static void ParityNoiseOctaveConfigString(
        StringBuilder builder,
        double offsetX,
        double offsetY,
        double offsetZ,
        byte[] permutations)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permutations);
        builder.AppendFormat(
            CultureInfo.InvariantCulture,
            "xo={0:F3}, yo={1:F3}, zo={2:F3}, p0={3}, p255={4}",
            (float)offsetX,
            (float)offsetY,
            (float)offsetZ,
            permutations[0],
            permutations[255]);
    }
}
