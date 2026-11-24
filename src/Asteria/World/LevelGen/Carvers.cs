using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.World.LevelGen;

/// <summary>
/// Configured Overworld carvers and their registry keys from Minecraft Java 26.3.
/// BiomeGenerationSettings references these canonical values during terrain carving.
/// </summary>
public static class Carvers
{
    public static RegistryKey<WorldCarver> CAVE { get; } = CreateKey("cave");
    public static RegistryKey<WorldCarver> CAVE_EXTRA_UNDERGROUND { get; } =
        CreateKey("cave_extra_underground");
    public static RegistryKey<WorldCarver> CANYON { get; } = CreateKey("canyon");

    public static WorldCarver CAVE_VALUE { get; } =
        new CaveWorldCarver(
            Probability: 0.15f,
            Y: UniformHeight.Of(
                VerticalAnchor.AboveBottom(8),
                VerticalAnchor.Absolute(180)),
            Count: VeryBiasedToBottomInt.Of(0, 14),
            Thickness: TrapezoidFloat.Of(0.0f, 3.0f, 1.0f),
            WeirdThicknessBias: true,
            RoomVerticalRadiusMultiplier: UniformFloat.Of(0.1f, 0.9f),
            HorizontalRadiusMultiplier: UniformFloat.Of(0.7f, 1.4f),
            VerticalRadiusMultiplier: UniformFloat.Of(0.8f, 1.3f),
            StartVerticalRadiusMultiplier: ConstantFloat.Of(1.0f),
            FloorLevel: UniformFloat.Of(-1.0f, -0.4f));

    public static WorldCarver CAVE_EXTRA_UNDERGROUND_VALUE { get; } =
        new CaveWorldCarver(
            Probability: 0.07f,
            Y: UniformHeight.Of(
                VerticalAnchor.AboveBottom(8),
                VerticalAnchor.Absolute(47)),
            Count: VeryBiasedToBottomInt.Of(0, 14),
            Thickness: TrapezoidFloat.Of(0.0f, 3.0f, 1.0f),
            WeirdThicknessBias: true,
            RoomVerticalRadiusMultiplier: UniformFloat.Of(0.1f, 0.9f),
            HorizontalRadiusMultiplier: UniformFloat.Of(0.7f, 1.4f),
            VerticalRadiusMultiplier: UniformFloat.Of(0.8f, 1.3f),
            StartVerticalRadiusMultiplier: ConstantFloat.Of(1.0f),
            FloorLevel: UniformFloat.Of(-1.0f, -0.4f));

    public static WorldCarver CANYON_VALUE { get; } =
        new CanyonWorldCarver(
            Probability: 0.01f,
            Y: UniformHeight.Of(
                VerticalAnchor.Absolute(10),
                VerticalAnchor.Absolute(67)),
            VerticalRotation: UniformFloat.Of(-0.125f, 0.125f),
            CarverShape: new CanyonWorldCarver.Shape(
                DistanceFactor: UniformFloat.Of(0.75f, 1.0f),
                Thickness: TrapezoidFloat.Of(0.0f, 6.0f, 2.0f),
                WidthSmoothness: 3,
                HorizontalRadiusFactor: UniformFloat.Of(0.75f, 1.0f),
                VerticalRadiusDefaultFactor: 1.0f,
                VerticalRadiusCenterFactor: 0.0f,
                YScale: ConstantFloat.Of(3.0f)));

    internal static void RegisterAll()
    {
        Registry.Register(BuiltInRegistries.Carver, CAVE.Location, CAVE_VALUE);
        Registry.Register(
            BuiltInRegistries.Carver,
            CAVE_EXTRA_UNDERGROUND.Location,
            CAVE_EXTRA_UNDERGROUND_VALUE);
        Registry.Register(BuiltInRegistries.Carver, CANYON.Location, CANYON_VALUE);
    }

    private static RegistryKey<WorldCarver> CreateKey(string name) =>
        RegistryKey<WorldCarver>.Create(
            Registries.CARVER,
            ResourceLocation.FromNamespaceAndPath("minecraft", name));
}
