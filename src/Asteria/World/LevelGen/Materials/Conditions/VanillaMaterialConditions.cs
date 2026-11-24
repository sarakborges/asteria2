using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.Placement;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>
/// Named vanilla material conditions from Minecraft Java 26.3. These are registry values,
/// not material-condition codec types; rule JSON references them by minecraft resource key.
/// </summary>
public static class VanillaMaterialConditions
{
    public static ResourceLocation ON_FLOOR { get; } = Key("on_floor");
    public static ResourceLocation UNDER_FLOOR { get; } = Key("under_floor");
    public static ResourceLocation DEEP_UNDER_FLOOR { get; } = Key("deep_under_floor");
    public static ResourceLocation VERY_DEEP_UNDER_FLOOR { get; } = Key("very_deep_under_floor");
    public static ResourceLocation ON_CEILING { get; } = Key("on_ceiling");
    public static ResourceLocation UNDER_CEILING { get; } = Key("under_ceiling");
    public static ResourceLocation NOT_UNDERWATER { get; } = Key("not_underwater");
    public static ResourceLocation NOT_UNDER_DEEP_WATER { get; } = Key("not_under_deep_water");

    private static ResourceLocation Key(string path) =>
        ResourceLocation.FromNamespaceAndPath("minecraft", path);

    internal static void RegisterAll()
    {
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            ON_FLOOR,
            new StoneDepthCondition(0, false, 0, CaveSurface.FLOOR));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            UNDER_FLOOR,
            new StoneDepthCondition(0, true, 0, CaveSurface.FLOOR));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            DEEP_UNDER_FLOOR,
            new StoneDepthCondition(0, true, 6, CaveSurface.FLOOR));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            VERY_DEEP_UNDER_FLOOR,
            new StoneDepthCondition(0, true, 30, CaveSurface.FLOOR));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            ON_CEILING,
            new StoneDepthCondition(0, false, 0, CaveSurface.CEILING));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            UNDER_CEILING,
            new StoneDepthCondition(0, true, 0, CaveSurface.CEILING));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            NOT_UNDERWATER,
            new WaterCondition(-1, 0, false));
        Registry.Register(
            BuiltInRegistries.MaterialCondition,
            NOT_UNDER_DEEP_WATER,
            new WaterCondition(-6, -1, true));
    }
}
