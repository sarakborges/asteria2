using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Applies independently sampled X/Y/Z offsets to one placement position.</summary>
public sealed class OffsetPlacement : PlacementModifier
{
    public IntProvider X { get; }
    public IntProvider Y { get; }
    public IntProvider Z { get; }

    public static MapCodec<OffsetPlacement> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<OffsetPlacement>(instance =>
            instance.Group(
                    IntProviders.Codec(-16, 16)
                        .FieldOf("x")
                        .ForGetter<OffsetPlacement>(static placement => placement.X),
                    IntProviders.Codec(-16, 16)
                        .FieldOf("y")
                        .ForGetter<OffsetPlacement>(static placement => placement.Y),
                    IntProviders.Codec(-16, 16)
                        .FieldOf("z")
                        .ForGetter<OffsetPlacement>(static placement => placement.Z))
                .Apply(
                    instance,
                    static (x, y, z) => new OffsetPlacement(x, y, z)));

    public static MapCodec<PlacementModifier> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<PlacementModifier>(
            static modifier => modifier,
            static modifier => (OffsetPlacement)modifier);

    public OffsetPlacement(IntProvider x, IntProvider y, IntProvider z)
    {
        X = x ?? throw new ArgumentNullException(nameof(x));
        Y = y ?? throw new ArgumentNullException(nameof(y));
        Z = z ?? throw new ArgumentNullException(nameof(z));
    }

    public static OffsetPlacement Of(IntProvider horizontal, IntProvider vertical) =>
        new(horizontal, vertical, horizontal);

    public static OffsetPlacement OfTriangle(int horizontalRange, int verticalRange) =>
        Of(
            TrapezoidInt.Triangle(horizontalRange),
            TrapezoidInt.Triangle(verticalRange));

    public static OffsetPlacement Vertical(IntProvider vertical) =>
        Of(ConstantInt.ZERO, vertical);

    public static OffsetPlacement Horizontal(IntProvider horizontal) =>
        Of(horizontal, ConstantInt.ZERO);

    public static OffsetPlacement Of(int x, int y, int z) =>
        new(ConstantInt.Of(x), ConstantInt.Of(y), ConstantInt.Of(z));

    public static OffsetPlacement Of(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        return Of(direction.GetStepX(), direction.GetStepY(), direction.GetStepZ());
    }

    public static OffsetPlacement Above() => Of(0, 1, 0);

    public void Modify(
        PlacementContext context,
        IRandomSource random,
        BlockPos origin,
        Action<BlockPos> output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(output);

        int x = X.Sample(random);
        int y = Y.Sample(random);
        int z = Z.Sample(random);
        output(new BlockPos(
            unchecked(origin.GetX() + x),
            unchecked(origin.GetY() + y),
            unchecked(origin.GetZ() + z)));
    }

    public MapCodec<PlacementModifier> Codec() => TYPE_CODEC;
}
