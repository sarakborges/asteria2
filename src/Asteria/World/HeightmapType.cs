namespace Asteria.World;

/// <summary>
/// Heightmap variants supported by Asteria's current block-state and block-tag domains.
/// Fluid-sensitive variants remain unavailable until FluidState exists.
/// Numeric values and serialized keys follow the reference type identifiers.
/// </summary>
public enum HeightmapType
{
    WorldSurfaceWorldgen = 0,
    WorldSurface = 1,
    OceanFloorWorldgen = 2,
    OceanFloor = 3
}

public static class HeightmapTypeExtensions
{
    public static string GetSerializationKey(this HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurfaceWorldgen => "WORLD_SURFACE_WG",
            HeightmapType.WorldSurface => "WORLD_SURFACE",
            HeightmapType.OceanFloorWorldgen => "OCEAN_FLOOR_WG",
            HeightmapType.OceanFloor => "OCEAN_FLOOR",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static bool SendToClient(this HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurface => true,
            HeightmapType.WorldSurfaceWorldgen or HeightmapType.OceanFloorWorldgen or HeightmapType.OceanFloor => false,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static bool KeepAfterWorldgen(this HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurfaceWorldgen or HeightmapType.OceanFloorWorldgen => false,
            HeightmapType.WorldSurface or HeightmapType.OceanFloor => true,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };
}
