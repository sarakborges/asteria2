namespace Asteria.World;

/// <summary>
/// Heightmap variants and stable IDs from the reference implementation.
/// </summary>
public enum HeightmapType
{
    WorldSurfaceWorldgen = 0,
    WorldSurface = 1,
    OceanFloorWorldgen = 2,
    OceanFloor = 3,
    MotionBlocking = 4,
    MotionBlockingNoLeaves = 5
}

public static class HeightmapTypeExtensions
{
    private static readonly HeightmapType[] Values = Enum.GetValues<HeightmapType>();

    /// <summary>
    /// Resolves the continuous wire ID. The reference out-of-bounds strategy falls back to ID zero.
    /// </summary>
    public static HeightmapType ById(int id) =>
        (uint)id < (uint)Values.Length ? Values[id] : Values[0];

    public static int GetId(this HeightmapType type) => (int)type;

    public static string GetSerializationKey(this HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurfaceWorldgen => "WORLD_SURFACE_WG",
            HeightmapType.WorldSurface => "WORLD_SURFACE",
            HeightmapType.OceanFloorWorldgen => "OCEAN_FLOOR_WG",
            HeightmapType.OceanFloor => "OCEAN_FLOOR",
            HeightmapType.MotionBlocking => "MOTION_BLOCKING",
            HeightmapType.MotionBlockingNoLeaves => "MOTION_BLOCKING_NO_LEAVES",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static string GetSerializedName(this HeightmapType type) =>
        type.GetSerializationKey();

    public static bool SendToClient(this HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurface or
            HeightmapType.MotionBlocking or
            HeightmapType.MotionBlockingNoLeaves => true,
            HeightmapType.WorldSurfaceWorldgen or
            HeightmapType.OceanFloorWorldgen or
            HeightmapType.OceanFloor => false,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };

    public static bool KeepAfterWorldgen(this HeightmapType type) =>
        type switch
        {
            HeightmapType.WorldSurfaceWorldgen or
            HeightmapType.OceanFloorWorldgen => false,
            HeightmapType.WorldSurface or
            HeightmapType.OceanFloor or
            HeightmapType.MotionBlocking or
            HeightmapType.MotionBlockingNoLeaves => true,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown heightmap type.")
        };
}
