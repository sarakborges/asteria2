using Asteria.Serialization;
using Asteria.Util.Random;

namespace Asteria.World.LevelGen;

/// <summary>Weighted distribution of integer providers from Minecraft Java 26.3.</summary>
public sealed class WeightedListInt : IntProvider
{
    public static MapCodec<WeightedListInt> MAP_CODEC { get; } =
        WeightedList<IntProvider>.CreateNonEmptyCodec(IntProviders.CODEC)
            .FieldOf("distribution")
            .XMap<WeightedListInt>(
                static distribution => new WeightedListInt(distribution),
                static provider => provider.Distribution);

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (WeightedListInt)provider);

    private readonly int _minimum;
    private readonly int _maximum;

    public WeightedList<IntProvider> Distribution { get; }

    public WeightedListInt(WeightedList<IntProvider> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        Distribution = distribution;

        int minimum = int.MaxValue;
        int maximum = int.MinValue;
        foreach (Weighted<IntProvider> weighted in distribution.Unwrap())
        {
            minimum = Math.Min(minimum, weighted.Value.MinInclusive());
            maximum = Math.Max(maximum, weighted.Value.MaxInclusive());
        }

        _minimum = minimum;
        _maximum = maximum;
    }

    public int Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Distribution.GetRandomOrThrow(random)!.Sample(random);
    }

    public int MinInclusive() => _minimum;

    public int MaxInclusive() => _maximum;

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;
}
