using System.Diagnostics;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Uniform inclusive height distribution between two vertical anchors.</summary>
public sealed class UniformHeight : HeightProvider
{
    private readonly HashSet<long> _warnedFor = new();
    private readonly object _warningLock = new();

    public VerticalAnchor Minimum { get; }
    public VerticalAnchor Maximum { get; }

    public static MapCodec<UniformHeight> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<UniformHeight>(instance =>
            instance.Group(
                    VerticalAnchor.CODEC.FieldOf("min_inclusive")
                        .ForGetter<UniformHeight>(static provider => provider.Minimum),
                    VerticalAnchor.CODEC.FieldOf("max_inclusive")
                        .ForGetter<UniformHeight>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (minimum, maximum) => new UniformHeight(minimum, maximum)));

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (UniformHeight)provider);

    public UniformHeight(VerticalAnchor minimum, VerticalAnchor maximum)
    {
        Minimum = minimum ?? throw new ArgumentNullException(nameof(minimum));
        Maximum = maximum ?? throw new ArgumentNullException(nameof(maximum));
    }

    public static UniformHeight Of(VerticalAnchor minimum, VerticalAnchor maximum) =>
        new(minimum, maximum);

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);

        int minimum = Minimum.ResolveY(context);
        int maximum = Maximum.ResolveY(context);
        if (minimum > maximum)
        {
            long key = unchecked(((long)minimum << 32) | maximum);
            bool shouldWarn;
            lock (_warningLock)
                shouldWarn = _warnedFor.Add(key);

            if (shouldWarn)
                Trace.TraceWarning("Empty height range: {0}", this);

            return minimum;
        }

        return RandomBetweenInclusive(random, minimum, maximum);
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;

    private static int RandomBetweenInclusive(
        IRandomSource random,
        int minimum,
        int maximum) =>
        unchecked(random.NextInt(unchecked(maximum - minimum + 1)) + minimum);

    public override string ToString() => $"[{Minimum}-{Maximum}]";
}
