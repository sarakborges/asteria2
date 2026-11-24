using System.Diagnostics;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Trapezoidal height distribution between two vertical anchors.</summary>
public sealed record TrapezoidHeight(
    VerticalAnchor Minimum,
    VerticalAnchor Maximum,
    int Plateau) : HeightProvider
{
    public static MapCodec<TrapezoidHeight> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<TrapezoidHeight>(instance =>
            instance.Group(
                    VerticalAnchor.CODEC.FieldOf("min_inclusive")
                        .ForGetter<TrapezoidHeight>(static provider => provider.Minimum),
                    VerticalAnchor.CODEC.FieldOf("max_inclusive")
                        .ForGetter<TrapezoidHeight>(static provider => provider.Maximum),
                    Codecs.INT.OptionalFieldOf("plateau", 0)
                        .ForGetter<TrapezoidHeight>(static provider => provider.Plateau))
                .Apply(
                    instance,
                    static (minimum, maximum, plateau) =>
                        new TrapezoidHeight(minimum, maximum, plateau)));

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (TrapezoidHeight)provider);

    public static TrapezoidHeight Of(
        VerticalAnchor minimum,
        VerticalAnchor maximum,
        int plateau) =>
        new(minimum, maximum, plateau);

    public static TrapezoidHeight Of(
        VerticalAnchor minimum,
        VerticalAnchor maximum) =>
        Of(minimum, maximum, 0);

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);

        int minimum = Minimum.ResolveY(context);
        int maximum = Maximum.ResolveY(context);
        if (minimum > maximum)
        {
            Trace.TraceWarning("Empty height range: {0}", this);
            return minimum;
        }

        int range = unchecked(maximum - minimum);
        if (Plateau >= range)
            return RandomBetweenInclusive(random, minimum, maximum);

        int plateauStart = (range - Plateau) / 2;
        int plateauEnd = range - plateauStart;
        return unchecked(
            minimum +
            RandomBetweenInclusive(random, 0, plateauEnd) +
            RandomBetweenInclusive(random, 0, plateauStart));
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;

    private static int RandomBetweenInclusive(
        IRandomSource random,
        int minimum,
        int maximum) =>
        unchecked(random.NextInt(unchecked(maximum - minimum + 1)) + minimum);

    public override string ToString() =>
        Plateau == 0
            ? $"triangle ({Minimum}-{Maximum})"
            : $"trapezoid({Plateau}) in [{Minimum}-{Maximum}]";
}
