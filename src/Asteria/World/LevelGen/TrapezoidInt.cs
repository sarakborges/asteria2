using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Trapezoid-distribution integer provider from Minecraft Java 26.3.</summary>
public sealed record TrapezoidInt(
    int Minimum,
    int Maximum,
    int Plateau) : IntProvider
{
    public static MapCodec<TrapezoidInt> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<TrapezoidInt>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("min")
                        .ForGetter<TrapezoidInt>(static provider => provider.Minimum),
                    Codecs.INT.FieldOf("max")
                        .ForGetter<TrapezoidInt>(static provider => provider.Maximum),
                    Codecs.INT.FieldOf("plateau")
                        .ForGetter<TrapezoidInt>(static provider => provider.Plateau))
                .Apply(
                    instance,
                    static (minimum, maximum, plateau) =>
                        new TrapezoidInt(minimum, maximum, plateau)))
        .Validate(static provider =>
        {
            if (provider.Maximum < provider.Minimum)
            {
                return DataResult<TrapezoidInt>.Error(() =>
                    $"Max must be larger than min: [{provider.Minimum}, {provider.Maximum}]");
            }

            return provider.Plateau > provider.Maximum - provider.Minimum
                ? DataResult<TrapezoidInt>.Error(() =>
                    $"Plateau can at most be the full span: [{provider.Minimum}, {provider.Maximum}]")
                : DataResult<TrapezoidInt>.Success(provider);
        });

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (TrapezoidInt)provider);

    public static TrapezoidInt Of(int minimum, int maximum, int plateau) =>
        new(minimum, maximum, plateau);

    public static IntProvider Triangle(int range) =>
        Of(unchecked(-range), range, 0);

    public int Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (Plateau == 0 && Maximum == unchecked(-Minimum))
        {
            return unchecked(
                random.NextInt(unchecked(Maximum + 1)) -
                random.NextInt(unchecked(Maximum + 1)));
        }

        int range = unchecked(Maximum - Minimum);
        if (Plateau == range)
            return RandomBetweenInclusive(random, Minimum, Maximum);

        int plateauStart = (range - Plateau) / 2;
        int plateauEnd = range - plateauStart;
        return unchecked(
            Minimum +
            RandomBetweenInclusive(random, 0, plateauEnd) +
            RandomBetweenInclusive(random, 0, plateauStart));
    }

    public int MinInclusive() => Minimum;

    public int MaxInclusive() => Maximum;

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;

    private static int RandomBetweenInclusive(IRandomSource random, int minimum, int maximum) =>
        unchecked(random.NextInt(unchecked(maximum - minimum + 1)) + minimum);

    public override string ToString() =>
        $"trapezoid({Plateau}) in [{Minimum}-{Maximum}]";
}
