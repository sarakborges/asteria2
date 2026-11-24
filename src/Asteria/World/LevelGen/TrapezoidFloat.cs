using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Trapezoid-distribution float provider from Minecraft Java 26.3.</summary>
public sealed record TrapezoidFloat(
    float Minimum,
    float Maximum,
    float Plateau) : FloatProvider
{
    public static MapCodec<TrapezoidFloat> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<TrapezoidFloat>(instance =>
            instance.Group(
                    Codecs.FLOAT.FieldOf("min")
                        .ForGetter<TrapezoidFloat>(static provider => provider.Minimum),
                    Codecs.FLOAT.FieldOf("max")
                        .ForGetter<TrapezoidFloat>(static provider => provider.Maximum),
                    Codecs.FLOAT.FieldOf("plateau")
                        .ForGetter<TrapezoidFloat>(static provider => provider.Plateau))
                .Apply(
                    instance,
                    static (minimum, maximum, plateau) =>
                        new TrapezoidFloat(minimum, maximum, plateau)))
        .Validate(static provider =>
        {
            if (provider.Maximum < provider.Minimum)
            {
                return DataResult<TrapezoidFloat>.Error(() =>
                    $"Max must be larger than min: [{provider.Minimum}, {provider.Maximum}]");
            }

            return provider.Plateau > provider.Maximum - provider.Minimum
                ? DataResult<TrapezoidFloat>.Error(() =>
                    $"Plateau can at most be the full span: [{provider.Minimum}, {provider.Maximum}]")
                : DataResult<TrapezoidFloat>.Success(provider);
        });

    public static MapCodec<FloatProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<FloatProvider>(
            static provider => provider,
            static provider => (TrapezoidFloat)provider);

    public static TrapezoidFloat Of(
        float minimum,
        float maximum,
        float plateau) =>
        new(minimum, maximum, plateau);

    public float Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        float range = Maximum - Minimum;
        float plateauStart = (range - Plateau) / 2.0f;
        float plateauEnd = range - plateauStart;
        return Minimum +
            random.NextFloat() * plateauEnd +
            random.NextFloat() * plateauStart;
    }

    public float Min() => Minimum;

    public float Max() => Maximum;

    public MapCodec<FloatProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        $"trapezoid({Plateau.ToString(System.Globalization.CultureInfo.InvariantCulture)}) in " +
        $"[{Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}-" +
        $"{Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
}
