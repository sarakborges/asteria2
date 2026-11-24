using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Inclusive uniform world-generation integer provider from Minecraft Java 26.3.</summary>
public sealed record UniformInt(int Minimum, int Maximum) : IntProvider
{
    public static MapCodec<UniformInt> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<UniformInt>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("min_inclusive")
                        .ForGetter<UniformInt>(static provider => provider.Minimum),
                    Codecs.INT.FieldOf("max_inclusive")
                        .ForGetter<UniformInt>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (minimum, maximum) => new UniformInt(minimum, maximum)))
        .Validate(static provider =>
            provider.Maximum < provider.Minimum
                ? DataResult<UniformInt>.Error(() =>
                    $"Max must be at least min, min_inclusive: {provider.Minimum}, max_inclusive: {provider.Maximum}")
                : DataResult<UniformInt>.Success(provider));

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (UniformInt)provider);

    public static UniformInt Of(int minimum, int maximum) =>
        new(minimum, maximum);

    public int Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return unchecked(random.NextInt(unchecked(Maximum - Minimum + 1)) + Minimum);
    }

    public int MinInclusive() => Minimum;

    public int MaxInclusive() => Maximum;

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;

    public override string ToString() => $"[{Minimum}-{Maximum}]";
}
