using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Content.Fluids;

/// <summary>
/// Built-in fluid tags used by generic fluid-selection and world queries.
/// Tag membership is supplied by the registry tag-reload lifecycle.
/// </summary>
public static class FluidTags
{
    public static TagKey<Fluid> Water { get; } =
        TagKey<Fluid>.Create(
            BuiltInRegistries.Fluid.Key,
            ResourceLocation.WithDefaultNamespace("water"));
}
