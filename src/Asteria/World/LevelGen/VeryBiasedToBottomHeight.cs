using System.Diagnostics;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Strongly lower-biased height distribution from Minecraft Java 26.3.</summary>
public sealed class VeryBiasedToBottomHeight : HeightProvider
{
    public VerticalAnchor Minimum { get; }
    public VerticalAnchor Maximum { get; }
    public int Inner { get; }

    public static MapCodec<VeryBiasedToBottomHeight> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<VeryBiasedToBottomHeight>(instance =>
            instance.Group(
                    VerticalAnchor.CODEC.FieldOf("min_inclusive")
                        .ForGetter<VeryBiasedToBottomHeight>(static provider => provider.Minimum),
                    VerticalAnchor.CODEC.FieldOf("max_inclusive")
                        .ForGetter<VeryBiasedToBottomHeight>(static provider => provider.Maximum),
                    Codecs.IntRange(1, int.MaxValue)
                        .OptionalFieldOf("inner", 1)
                        .ForGetter<VeryBiasedToBottomHeight>(static provider => provider.Inner))
                .Apply(
                    instance,
                    static (minimum, maximum, inner) =>
                        new VeryBiasedToBottomHeight(minimum, maximum, inner)));

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (VeryBiasedToBottomHeight)provider);

    public VeryBiasedToBottomHeight(
        VerticalAnchor minimum,
        VerticalAnchor maximum,
        int inner)
    {
        Minimum = minimum ?? throw new ArgumentNullException(nameof(minimum));
        Maximum = maximum ?? throw new ArgumentNullException(nameof(maximum));
        Inner = inner;
    }

    public static VeryBiasedToBottomHeight Of(
        VerticalAnchor minimum,
        VerticalAnchor maximum,
        int inner) =>
        new(minimum, maximum, inner);

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);

        int minimum = Minimum.ResolveY(context);
        int maximum = Maximum.ResolveY(context);
        if (unchecked(maximum - minimum - Inner + 1) <= 0)
        {
            Trace.TraceWarning("Empty height range: {0}", this);
            return minimum;
        }

        int upperInclusive = RandomBetweenInclusive(
            random,
            unchecked(minimum + Inner),
            maximum);
        int biasedUpperInclusive = RandomBetweenInclusive(
            random,
            minimum,
            unchecked(upperInclusive - 1));
        return RandomBetweenInclusive(
            random,
            minimum,
            unchecked(biasedUpperInclusive - 1 + Inner));
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;

    private static int RandomBetweenInclusive(
        IRandomSource random,
        int minimum,
        int maximum) =>
        unchecked(random.NextInt(unchecked(maximum - minimum + 1)) + minimum);

    public override string ToString() =>
        $"biased[{Minimum}-{Maximum} inner: {Inner}]";
}
