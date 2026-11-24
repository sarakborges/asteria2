using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Surface orientation used by cave placement and material rules.</summary>
public enum CaveSurface
{
    CEILING,
    FLOOR
}

public static class CaveSurfaceCodecs
{
    public static Codec<CaveSurface> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            static name => name switch
            {
                "ceiling" => DataResult<CaveSurface>.Success(CaveSurface.CEILING),
                "floor" => DataResult<CaveSurface>.Success(CaveSurface.FLOOR),
                _ => DataResult<CaveSurface>.Error(() => $"Unknown element name {name}")
            },
            static surface => DataResult<string>.Success(
                surface == CaveSurface.CEILING ? "ceiling" : "floor"));
}
