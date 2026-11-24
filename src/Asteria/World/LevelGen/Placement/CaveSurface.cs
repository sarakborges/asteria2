using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Surface orientation used by cave placement and material rules.</summary>
public sealed class CaveSurface
{
    public static CaveSurface CEILING { get; } = new(Direction.UP, 1, "ceiling");
    public static CaveSurface FLOOR { get; } = new(Direction.DOWN, -1, "floor");

    public static Codec<CaveSurface> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            static name => name switch
            {
                "ceiling" => DataResult<CaveSurface>.Success(CEILING),
                "floor" => DataResult<CaveSurface>.Success(FLOOR),
                _ => DataResult<CaveSurface>.Error(() => $"Unknown element name {name}")
            },
            static surface => DataResult<string>.Success(surface.Id));

    private CaveSurface(Direction direction, int y, string id)
    {
        Direction = direction;
        Y = y;
        Id = id;
    }

    public Direction Direction { get; }
    public int Y { get; }
    public string Id { get; }

    public Direction GetDirection() => Direction;
    public int GetY() => Y;
    public string GetSerializedName() => Id;
}
