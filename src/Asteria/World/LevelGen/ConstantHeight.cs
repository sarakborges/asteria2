using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Resolves one vertical anchor without consuming random values.</summary>
public sealed class ConstantHeight : HeightProvider
{
    public static ConstantHeight ZERO { get; } =
        new(VerticalAnchor.Absolute(0));

    public static MapCodec<ConstantHeight> MAP_CODEC { get; } =
        VerticalAnchor.CODEC.FieldOf("value")
            .XMap<ConstantHeight>(
                static value => Of(value),
                static provider => provider.Value);

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (ConstantHeight)provider);

    public VerticalAnchor Value { get; }

    private ConstantHeight(VerticalAnchor value) =>
        Value = value ?? throw new ArgumentNullException(nameof(value));

    public static ConstantHeight Of(VerticalAnchor value) =>
        new(value);

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);
        return Value.ResolveY(context);
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;

    public override string ToString() => Value.ToString()!;
}
