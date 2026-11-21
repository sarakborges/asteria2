namespace Asteria.Content.Biomes;

public enum GrassColorModifier { NONE, DARK_FOREST, SWAMP }

/// <summary>Runtime color effects corresponding to the Minecraft 26.3 BiomeSpecialEffects record.</summary>
public sealed record BiomeSpecialEffects(int WaterColor, int? FoliageColorOverride, int? DryFoliageColorOverride, int? GrassColorOverride, GrassColorModifier GrassColorModifier)
{
    public int GetFoliageColor(int defaultColor) => FoliageColorOverride ?? defaultColor;
    public int GetDryFoliageColor(int defaultColor) => DryFoliageColorOverride ?? defaultColor;

    public int GetGrassColor(double x, double z, int defaultColor, Func<double, double, double>? biomeInfoNoise = null)
    {
        int color = GrassColorOverride ?? defaultColor;
        return ModifyGrassColor(GrassColorModifier, x, z, color, biomeInfoNoise);
    }

    public sealed class Builder
    {
        private int? _waterColor;
        private int? _foliageColorOverride;
        private int? _dryFoliageColorOverride;
        private int? _grassColorOverride;
        private GrassColorModifier _grassColorModifier = GrassColorModifier.NONE;
        public Builder WaterColor(int value) { _waterColor = value; return this; }
        public Builder FoliageColorOverride(int value) { _foliageColorOverride = value; return this; }
        public Builder DryFoliageColorOverride(int value) { _dryFoliageColorOverride = value; return this; }
        public Builder GrassColorOverride(int value) { _grassColorOverride = value; return this; }
        public Builder GrassColorModifier(GrassColorModifier value) { _grassColorModifier = value; return this; }
        public BiomeSpecialEffects Build() => new(
            _waterColor ?? throw new InvalidOperationException("Missing 'water' color."),
            _foliageColorOverride, _dryFoliageColorOverride, _grassColorOverride, _grassColorModifier);
    }

    public static string GetSerializedName(GrassColorModifier modifier) => modifier switch
    {
        GrassColorModifier.NONE => "none",
        GrassColorModifier.DARK_FOREST => "dark_forest",
        GrassColorModifier.SWAMP => "swamp",
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Unknown grass color modifier.")
    };

    private static int Opaque(int color) => color | unchecked((int)0xFF000000);

    private static int ModifyGrassColor(GrassColorModifier modifier, double x, double z, int baseColor, Func<double, double, double>? biomeInfoNoise) => modifier switch
    {
        GrassColorModifier.NONE => baseColor,
        GrassColorModifier.DARK_FOREST => Opaque(unchecked(((baseColor & 16711422) + 2634762) >> 1)),
        GrassColorModifier.SWAMP => ModifySwampColor(x, z, biomeInfoNoise),
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Unknown grass color modifier.")
    };

    private static int ModifySwampColor(double x, double z, Func<double, double, double>? biomeInfoNoise)
    {
        ArgumentNullException.ThrowIfNull(biomeInfoNoise);
        double groundValue = biomeInfoNoise(x * 0.0225, z * 0.0225);
        return groundValue < -0.1 ? -11766212 : -9801671;
    }
}