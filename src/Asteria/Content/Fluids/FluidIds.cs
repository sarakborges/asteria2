using Asteria.Content.Identifiers;

namespace Asteria.Content.Fluids;

public static class FluidIds
{
    public static ResourceLocation EMPTY { get; } = ResourceLocation.WithDefaultNamespace("empty");
    public static ResourceLocation FLOWING_WATER { get; } = ResourceLocation.WithDefaultNamespace("flowing_water");
    public static ResourceLocation WATER { get; } = ResourceLocation.WithDefaultNamespace("water");
    public static ResourceLocation FLOWING_LAVA { get; } = ResourceLocation.WithDefaultNamespace("flowing_lava");
    public static ResourceLocation LAVA { get; } = ResourceLocation.WithDefaultNamespace("lava");
}
