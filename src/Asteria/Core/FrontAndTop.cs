namespace Asteria.Core;

/// <summary>
/// The twelve valid orthogonal front/top direction pairs.
/// </summary>
public sealed class FrontAndTop
{
    public static readonly FrontAndTop DOWN_EAST = new("down_east", Direction.DOWN, Direction.EAST);
    public static readonly FrontAndTop DOWN_NORTH = new("down_north", Direction.DOWN, Direction.NORTH);
    public static readonly FrontAndTop DOWN_SOUTH = new("down_south", Direction.DOWN, Direction.SOUTH);
    public static readonly FrontAndTop DOWN_WEST = new("down_west", Direction.DOWN, Direction.WEST);
    public static readonly FrontAndTop UP_EAST = new("up_east", Direction.UP, Direction.EAST);
    public static readonly FrontAndTop UP_NORTH = new("up_north", Direction.UP, Direction.NORTH);
    public static readonly FrontAndTop UP_SOUTH = new("up_south", Direction.UP, Direction.SOUTH);
    public static readonly FrontAndTop UP_WEST = new("up_west", Direction.UP, Direction.WEST);
    public static readonly FrontAndTop WEST_UP = new("west_up", Direction.WEST, Direction.UP);
    public static readonly FrontAndTop EAST_UP = new("east_up", Direction.EAST, Direction.UP);
    public static readonly FrontAndTop NORTH_UP = new("north_up", Direction.NORTH, Direction.UP);
    public static readonly FrontAndTop SOUTH_UP = new("south_up", Direction.SOUTH, Direction.UP);

    private const int DirectionCount = 6;
    private static readonly FrontAndTop[] Values =
    {
        DOWN_EAST, DOWN_NORTH, DOWN_SOUTH, DOWN_WEST,
        UP_EAST, UP_NORTH, UP_SOUTH, UP_WEST,
        WEST_UP, EAST_UP, NORTH_UP, SOUTH_UP
    };
    private static readonly FrontAndTop?[] ByTopFront = CreateLookup();

    private readonly string _name;
    private readonly Direction _front;
    private readonly Direction _top;

    private FrontAndTop(string name, Direction front, Direction top)
    {
        _name = name;
        _front = front;
        _top = top;
    }

    public static FrontAndTop[] GetValues() => (FrontAndTop[])Values.Clone();

    public static FrontAndTop? FromFrontAndTop(Direction front, Direction top)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(top);
        return ByTopFront[LookupKey(front, top)];
    }

    public Direction Front() => _front;

    public Direction Top() => _top;

    public string GetSerializedName() => _name;

    public override string ToString() => _name;

    private static FrontAndTop?[] CreateLookup()
    {
        var lookup = new FrontAndTop?[DirectionCount * DirectionCount];
        foreach (FrontAndTop value in Values)
            lookup[LookupKey(value._front, value._top)] = value;
        return lookup;
    }

    private static int LookupKey(Direction front, Direction top) =>
        front.Get3DDataValue() * DirectionCount + top.Get3DDataValue();
}
