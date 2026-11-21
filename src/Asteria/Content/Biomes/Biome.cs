using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Synth;

namespace Asteria.Content.Biomes;

/// <summary>Registry-backed biome climate and visual data.</summary>
public sealed class Biome
{
    private static readonly SimplexNoise TemperatureNoise = new(
        new WorldgenRandom(new LegacyRandomSource(1234L)),
        discardNoiseOffset: true);

    private static readonly NoiseStack FrozenTemperatureNoise = CreateFrozenTemperatureNoise();

    private const int TemperatureCacheSize = 1024;
    private readonly ThreadLocal<TemperatureCache> _temperatureCache =
        new(() => new TemperatureCache(TemperatureCacheSize));

    internal static float GetFrozenTemperatureNoise(double x, double z) => FrozenTemperatureNoise.Get(x, z);

    private static NoiseStack CreateFrozenTemperatureNoise()
    {
        var random = new WorldgenRandom(new LegacyRandomSource(3456L));
        return new NoiseStack.Builder()
            .Add(new SimplexNoise(random, discardNoiseOffset: true), 1.0, 0.14285715F)
            .Add(new SimplexNoise(random, discardNoiseOffset: true), 0.5, 0.2857143F)
            .Add(new SimplexNoise(random, discardNoiseOffset: true), 0.25, 0.5714286F)
            .Build();
    }

    public static float GetBiomeInfoNoise(double x, double z) => BiomeInfoNoise.Get(x, z);
    public Holder<Biome>.Reference BuiltInRegistryHolder { get; }
    public ClimateSettings Climate { get; }
    public BiomeSpecialEffects SpecialEffects { get; }
    public bool HasPrecipitation => Climate.HasPrecipitation;
    public float GetBaseTemperature() => Climate.Temperature;
    public float GetDownfall() => Climate.Downfall;

    public float GetHeightAdjustedTemperature(BlockPos position, int seaLevel)
    {
        ArgumentNullException.ThrowIfNull(position);
        float adjustedTemperature = Climate.TemperatureModifier.ModifyTemperature(position, GetBaseTemperature());
        int snowLevel = seaLevel + 17;
        if (position.GetY() > snowLevel)
        {
            float variation = TemperatureNoise.Get(position.GetX() / 8.0F, position.GetZ() / 8.0F) * 8.0F;
            return adjustedTemperature - (variation + position.GetY() - snowLevel) * 0.05F / 40.0F;
        }

        return adjustedTemperature;
    }

    public Precipitation GetPrecipitationAt(BlockPos position, int seaLevel)
    {
        if (!HasPrecipitation)
            return Precipitation.NONE;

        return WarmEnoughToRain(position, seaLevel)
            ? Precipitation.RAIN
            : Precipitation.SNOW;
    }

    private float GetTemperature(BlockPos position, int seaLevel)
    {
        ArgumentNullException.ThrowIfNull(position);
        TemperatureCache cache = _temperatureCache.Value!;
        long key = position.AsLong();
        if (cache.TryGet(key, out float cached))
            return cached;

        float temperature = GetHeightAdjustedTemperature(position, seaLevel);
        cache.Add(key, temperature);
        return temperature;
    }

    public bool WarmEnoughToRain(BlockPos position, int seaLevel) =>
        GetTemperature(position, seaLevel) >= 0.15F;

    public bool ColdEnoughToSnow(BlockPos position, int seaLevel) =>
        !WarmEnoughToRain(position, seaLevel);

    public bool ShouldMeltFrozenOceanIcebergSlightly(BlockPos position, int seaLevel) =>
        GetTemperature(position, seaLevel) > 0.1F;

    public Biome(ClimateSettings climate, BiomeSpecialEffects specialEffects)
    {
        Climate = climate;
        SpecialEffects = specialEffects ?? throw new ArgumentNullException(nameof(specialEffects));
        BuiltInRegistryHolder = BuiltInRegistries.Biome.CreateIntrusiveHolder(this);
    }

    private sealed class TemperatureCache
    {
        private readonly int _capacity;
        private readonly Dictionary<long, float> _values;
        private readonly Queue<long> _insertionOrder;

        public TemperatureCache(int capacity)
        {
            _capacity = capacity;
            _values = new Dictionary<long, float>(capacity);
            _insertionOrder = new Queue<long>(capacity);
        }

        public bool TryGet(long key, out float value) => _values.TryGetValue(key, out value);

        public void Add(long key, float value)
        {
            if (_values.ContainsKey(key))
                return;

            if (_values.Count == _capacity)
                _values.Remove(_insertionOrder.Dequeue());

            _values.Add(key, value);
            _insertionOrder.Enqueue(key);
        }
    }

    public readonly record struct ClimateSettings(bool HasPrecipitation, float Temperature, TemperatureModifier TemperatureModifier, float Downfall);
    public enum Precipitation { NONE, RAIN, SNOW }
    public enum TemperatureModifier { NONE, FROZEN }
}

public static class BiomeEnumExtensions
{
    public static float ModifyTemperature(this Biome.TemperatureModifier modifier, BlockPos position, float baseTemperature)
    {
        ArgumentNullException.ThrowIfNull(position);
        return modifier switch
        {
            Biome.TemperatureModifier.NONE => baseTemperature,
            Biome.TemperatureModifier.FROZEN => ModifyFrozenTemperature(position, baseTemperature),
            _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Unknown temperature modifier.")
        };
    }

    private static float ModifyFrozenTemperature(BlockPos position, float baseTemperature)
    {
        double largeVariation = GetFrozenTemperatureNoise(position.GetX() * 0.05, position.GetZ() * 0.05) * 7.0F;
        double edgeVariation = Biome.GetBiomeInfoNoise(position.GetX() * 0.2, position.GetZ() * 0.2);
        double icePatches = largeVariation + edgeVariation;
        if (icePatches < 0.3)
        {
            double smallVariation = Biome.GetBiomeInfoNoise(position.GetX() * 0.09, position.GetZ() * 0.09);
            if (smallVariation < 0.8)
                return 0.2F;
        }

        return baseTemperature;
    }

    private static float GetFrozenTemperatureNoise(double x, double z) =>
        Biome.GetFrozenTemperatureNoise(x, z);
    public static string GetSerializedName(this Biome.Precipitation precipitation) => precipitation switch
    {
        Biome.Precipitation.NONE => "none",
        Biome.Precipitation.RAIN => "rain",
        Biome.Precipitation.SNOW => "snow",
        _ => throw new ArgumentOutOfRangeException(nameof(precipitation), precipitation, "Unknown precipitation type.")
    };

    public static string GetSerializedName(this Biome.TemperatureModifier modifier) => modifier switch
    {
        Biome.TemperatureModifier.NONE => "none",
        Biome.TemperatureModifier.FROZEN => "frozen",
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Unknown temperature modifier.")
    };
}