using Asteria.Client.Rendering;

internal static class RenderingLayerChecks
{
    public static void Run()
    {
        CheckTransparencyClassification();
        CheckTransparencyCombination();
        CheckChunkSectionLayers();
        CheckLayerSelection();
    }

    private static void CheckTransparencyClassification()
    {
        Require(Transparency.NONE.IsOpaque(), "NONE transparency must be opaque.");
        Require(!Transparency.TRANSPARENT.IsOpaque(), "Transparent pixels must make a sprite non-opaque.");
        Require(!Transparency.TRANSLUCENT.IsOpaque(), "Translucent pixels must make a sprite non-opaque.");
        Require(
            !Transparency.TRANSPARENT_AND_TRANSLUCENT.IsOpaque(),
            "A sprite containing both transparent and translucent pixels must not be opaque.");

        Require(
            Transparency.Of(false, false) == Transparency.NONE &&
            Transparency.Of(true, false) == Transparency.TRANSPARENT &&
            Transparency.Of(false, true) == Transparency.TRANSLUCENT &&
            Transparency.Of(true, true) == Transparency.TRANSPARENT_AND_TRANSLUCENT,
            "Transparency.Of must return the canonical 26.3 transparency values.");
    }

    private static void CheckTransparencyCombination()
    {
        Require(
            Transparency.NONE.Or(Transparency.NONE) == Transparency.NONE &&
            Transparency.NONE.Or(Transparency.TRANSPARENT) == Transparency.TRANSPARENT &&
            Transparency.NONE.Or(Transparency.TRANSLUCENT) == Transparency.TRANSLUCENT &&
            Transparency.TRANSPARENT.Or(Transparency.TRANSLUCENT) == Transparency.TRANSPARENT_AND_TRANSLUCENT,
            "Transparency.Or must union both transparency flags.");

        Require(
            Transparency.TRANSPARENT_AND_TRANSLUCENT.Or(Transparency.NONE) ==
            Transparency.TRANSPARENT_AND_TRANSLUCENT,
            "Transparency.Or must preserve already observed transparency flags.");
    }

    private static void CheckChunkSectionLayers()
    {
        ChunkSectionLayer[] layers = Enum.GetValues<ChunkSectionLayer>();
        Require(
            layers.SequenceEqual(new[]
            {
                ChunkSectionLayer.SOLID,
                ChunkSectionLayer.CUTOUT,
                ChunkSectionLayer.TRANSLUCENT
            }),
            "ChunkSectionLayer must preserve the 26.3 SOLID/CUTOUT/TRANSLUCENT order.");

        Require(
            ChunkSectionLayer.SOLID.Label() == "solid" &&
            ChunkSectionLayer.CUTOUT.Label() == "cutout" &&
            ChunkSectionLayer.TRANSLUCENT.Label() == "translucent",
            "Chunk section layer labels must be lowercase invariant names.");

        Require(
            ChunkSectionLayer.SOLID.BufferSize() == 4_194_304 &&
            ChunkSectionLayer.CUTOUT.BufferSize() == 4_194_304 &&
            ChunkSectionLayer.TRANSLUCENT.BufferSize() == 786_432,
            "Chunk section layers must retain their Minecraft 26.3 source buffer-size metadata.");

        Require(
            !ChunkSectionLayer.SOLID.IsTranslucent() &&
            !ChunkSectionLayer.CUTOUT.IsTranslucent() &&
            ChunkSectionLayer.TRANSLUCENT.IsTranslucent(),
            "Only the translucent chunk section layer may report translucent.");
    }

    private static void CheckLayerSelection()
    {
        Require(
            ChunkSectionLayers.ByTransparency(Transparency.NONE) == ChunkSectionLayer.SOLID,
            "An opaque quad must use SOLID.");
        Require(
            ChunkSectionLayers.ByTransparency(Transparency.TRANSPARENT) == ChunkSectionLayer.CUTOUT,
            "A quad with fully transparent texels must use CUTOUT.");
        Require(
            ChunkSectionLayers.ByTransparency(Transparency.TRANSLUCENT) == ChunkSectionLayer.TRANSLUCENT,
            "A quad with translucent texels must use TRANSLUCENT.");
        Require(
            ChunkSectionLayers.ByTransparency(Transparency.TRANSPARENT_AND_TRANSLUCENT) ==
            ChunkSectionLayer.TRANSLUCENT,
            "Translucent takes precedence when both transparency categories are present.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
