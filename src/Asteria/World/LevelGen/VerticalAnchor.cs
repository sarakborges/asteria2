using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Serializable vertical coordinate expression from Minecraft Java 26.3.
/// </summary>
public abstract record VerticalAnchor
{
    private const int MIN_VALUE = NoiseSettings.DimensionMinY;
    private const int MAX_VALUE = NoiseSettings.DimensionMaxY;

    public static VerticalAnchor BOTTOM { get; } = new AboveBottomAnchor(0);
    public static VerticalAnchor TOP { get; } = new BelowTopAnchor(0);

    public static Codec<VerticalAnchor> CODEC { get; } = new AnchorCodec();

    public abstract int ResolveY(WorldGenerationContext context);

    public static VerticalAnchor Absolute(int y) => new AbsoluteAnchor(y);
    public static VerticalAnchor AboveBottom(int offset) => new AboveBottomAnchor(offset);
    public static VerticalAnchor BelowTop(int offset) => new BelowTopAnchor(offset);
    public static VerticalAnchor RelativeToSeaLevel(int offset) => new RelativeToSeaLevelAnchor(offset);

    public sealed record AbsoluteAnchor(int Y) : VerticalAnchor
    {
        public override int ResolveY(WorldGenerationContext context) => Y;
        public override string ToString() => $"{Y} absolute";
    }

    public sealed record AboveBottomAnchor(int Offset) : VerticalAnchor
    {
        public override int ResolveY(WorldGenerationContext context) =>
            unchecked(context.GetMinGenY() + Offset);
        public override string ToString() => $"{Offset} above bottom";
    }

    public sealed record BelowTopAnchor(int Offset) : VerticalAnchor
    {
        public override int ResolveY(WorldGenerationContext context) =>
            unchecked(context.GetMinGenY() + context.GetGenDepth() - 1 - Offset);
        public override string ToString() => $"{Offset} below top";
    }

    public sealed record RelativeToSeaLevelAnchor(int Offset) : VerticalAnchor
    {
        public override int ResolveY(WorldGenerationContext context) =>
            unchecked(context.SeaLevel() + Offset);
        public override string ToString() => $"{Offset} relative to sea level";
    }

    private sealed class AnchorCodec : Codec<VerticalAnchor>
    {
        public override DataResult<Pair<VerticalAnchor, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            return ops.GetMap(input).FlatMap(map =>
            {
                if (map.Entries.Count != 1)
                {
                    return DataResult<Pair<VerticalAnchor, TData>>.Error(
                        "VerticalAnchor must contain exactly one anchor field.");
                }

                KeyValuePair<TData, TData> entry = map.Entries[0];
                return ops.GetStringValue(entry.Key).FlatMap(name =>
                    Codecs.IntRange(MIN_VALUE, MAX_VALUE).Parse(ops, entry.Value).Map(value =>
                    {
                        VerticalAnchor anchor = name switch
                        {
                            "absolute" => new AbsoluteAnchor(value),
                            "above_bottom" => new AboveBottomAnchor(value),
                            "below_top" => new BelowTopAnchor(value),
                            "relative_to_sea_level" => new RelativeToSeaLevelAnchor(value),
                            _ => throw new InvalidOperationException(
                                $"Unknown vertical anchor field '{name}'.")
                        };

                        return new Pair<VerticalAnchor, TData>(anchor, input);
                    }));
            });
        }

        public override DataResult<TData> Encode<TData>(
            VerticalAnchor input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            string name;
            int value;
            switch (input)
            {
                case AbsoluteAnchor absolute:
                    name = "absolute";
                    value = absolute.Y;
                    break;
                case AboveBottomAnchor aboveBottom:
                    name = "above_bottom";
                    value = aboveBottom.Offset;
                    break;
                case BelowTopAnchor belowTop:
                    name = "below_top";
                    value = belowTop.Offset;
                    break;
                case RelativeToSeaLevelAnchor relative:
                    name = "relative_to_sea_level";
                    value = relative.Offset;
                    break;
                default:
                    return DataResult<TData>.Error(
                        () => $"Unsupported vertical anchor type {input.GetType().Name}");
            }

            return Codecs.IntRange(MIN_VALUE, MAX_VALUE).EncodeStart(ops, value).FlatMap(encoded =>
                ops.MergeToMap(prefix, ops.CreateString(name), encoded));
        }
    }
}
