using Asteria.Util.Context;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Typed sampler-context keys used by Minecraft Java 26.3's simple density functions.
/// </summary>
public static class DensityFunctionContextKeys
{
    public static ContextKey<DensitySampler> BLEND_ALPHA { get; } =
        ContextKey<DensitySampler>.Vanilla("blend_alpha");

    public static ContextKey<DensitySampler> BLEND_OFFSET { get; } =
        ContextKey<DensitySampler>.Vanilla("blend_offset");

    public static ContextKey<DensitySampler> BEARDIFIER { get; } =
        Beardifier.CONTEXT_KEY;

    public static ContextKey<IChunkBlender> BLENDER { get; } =
        ContextKey<IChunkBlender>.Vanilla("blender");
}
