namespace Asteria.Client.Rendering;

/// <summary>
/// The three terrain mesh layers used by Minecraft Java 26.3.
/// The Godot renderer maps these semantic layers to its own materials and draw passes;
/// the reference buffer sizes are retained as source metadata, not used as Godot allocations.
/// </summary>
public enum ChunkSectionLayer
{
    SOLID,
    CUTOUT,
    TRANSLUCENT
}

/// <summary>
/// Minecraft 26.3 layer behavior independent of the graphics API backend.
/// </summary>
public static class ChunkSectionLayers
{
    public static ChunkSectionLayer ByTransparency(Transparency transparency)
    {
        if (transparency.HasTranslucent)
            return ChunkSectionLayer.TRANSLUCENT;

        return transparency.HasTransparent
            ? ChunkSectionLayer.CUTOUT
            : ChunkSectionLayer.SOLID;
    }

    public static int BufferSize(this ChunkSectionLayer layer) =>
        layer switch
        {
            ChunkSectionLayer.SOLID => 4_194_304,
            ChunkSectionLayer.CUTOUT => 4_194_304,
            ChunkSectionLayer.TRANSLUCENT => 786_432,
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };

    public static string Label(this ChunkSectionLayer layer) =>
        layer switch
        {
            ChunkSectionLayer.SOLID => "solid",
            ChunkSectionLayer.CUTOUT => "cutout",
            ChunkSectionLayer.TRANSLUCENT => "translucent",
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };

    public static bool IsTranslucent(this ChunkSectionLayer layer) =>
        layer switch
        {
            ChunkSectionLayer.SOLID or ChunkSectionLayer.CUTOUT => false,
            ChunkSectionLayer.TRANSLUCENT => true,
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
}
