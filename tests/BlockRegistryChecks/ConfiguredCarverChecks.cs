using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen;

internal static class ConfiguredCarverChecks
{
    public static void Run()
    {
        CheckRegistryEntries();
        CheckConfiguredCave();
        CheckConfiguredExtraUndergroundCave();
        CheckConfiguredCanyon();
    }

    private static void CheckRegistryEntries()
    {
        Require(
            BuiltInRegistries.Carver.Count == 3,
            "The configured Overworld carver registry must contain exactly the three Minecraft 26.3 carvers.");

        Require(
            BuiltInRegistries.Carver.GetValue(Carvers.CAVE) == Carvers.CAVE_VALUE &&
            BuiltInRegistries.Carver.GetValue(Carvers.CAVE_EXTRA_UNDERGROUND) == Carvers.CAVE_EXTRA_UNDERGROUND_VALUE &&
            BuiltInRegistries.Carver.GetValue(Carvers.CANYON) == Carvers.CANYON_VALUE,
            "Each configured carver key must resolve to its canonical registered value.");

        Require(
            BuiltInRegistries.Carver.GetResourceKey(Carvers.CAVE_VALUE) == Carvers.CAVE &&
            BuiltInRegistries.Carver.GetResourceKey(Carvers.CAVE_EXTRA_UNDERGROUND_VALUE) == Carvers.CAVE_EXTRA_UNDERGROUND &&
            BuiltInRegistries.Carver.GetResourceKey(Carvers.CANYON_VALUE) == Carvers.CANYON,
            "Configured carver values must retain their canonical registry keys.");

        Require(
            Carvers.CAVE.Location == ResourceLocation.Parse("minecraft:cave") &&
            Carvers.CAVE_EXTRA_UNDERGROUND.Location == ResourceLocation.Parse("minecraft:cave_extra_underground") &&
            Carvers.CANYON.Location == ResourceLocation.Parse("minecraft:canyon"),
            "Configured carver keys must preserve their 26.3 Minecraft identifiers.");
    }

    private static void CheckConfiguredCave()
    {
        Require(Carvers.CAVE_VALUE is CaveWorldCarver, "The CAVE key must hold a CaveWorldCarver.");
        var cave = (CaveWorldCarver)Carvers.CAVE_VALUE;

        Require(
            cave.Probability == 0.15f &&
            cave.Y is UniformHeight
            {
                Minimum: var minimum,
                Maximum: var maximum
            } &&
            minimum == VerticalAnchor.AboveBottom(8) &&
            maximum == VerticalAnchor.Absolute(180),
            "The default cave must retain the 26.3 probability and inclusive vertical-anchor range.");

        Require(
            cave.Count is VeryBiasedToBottomInt { Minimum: 0, Maximum: 14 } &&
            cave.Thickness is TrapezoidFloat
            {
                Minimum: 0.0f,
                Maximum: 3.0f,
                Plateau: 1.0f
            } &&
            cave.WeirdThicknessBias,
            "The default cave must retain its count, thickness distribution and weird-thickness flag.");

        Require(
            cave.RoomVerticalRadiusMultiplier is UniformFloat { Minimum: 0.1f, Maximum: 0.9f } &&
            cave.HorizontalRadiusMultiplier is UniformFloat { Minimum: 0.7f, Maximum: 1.4f } &&
            cave.VerticalRadiusMultiplier is UniformFloat { Minimum: 0.8f, Maximum: 1.3f } &&
            cave.StartVerticalRadiusMultiplier is ConstantFloat { Value: 1.0f } &&
            cave.FloorLevel is UniformFloat { Minimum: -1.0f, Maximum: -0.4f },
            "The default cave must retain all configured shape multiplier distributions.");
    }

    private static void CheckConfiguredExtraUndergroundCave()
    {
        Require(
            Carvers.CAVE_EXTRA_UNDERGROUND_VALUE is CaveWorldCarver,
            "The CAVE_EXTRA_UNDERGROUND key must hold a CaveWorldCarver.");
        var cave = (CaveWorldCarver)Carvers.CAVE_EXTRA_UNDERGROUND_VALUE;

        Require(
            cave.Probability == 0.07f &&
            cave.Y is UniformHeight
            {
                Minimum: var minimum,
                Maximum: var maximum
            } &&
            minimum == VerticalAnchor.AboveBottom(8) &&
            maximum == VerticalAnchor.Absolute(47),
            "The extra underground cave must retain its 26.3 probability and vertical range.");

        Require(
            cave.Count is VeryBiasedToBottomInt { Minimum: 0, Maximum: 14 } &&
            cave.Thickness is TrapezoidFloat { Minimum: 0.0f, Maximum: 3.0f, Plateau: 1.0f } &&
            cave.WeirdThicknessBias,
            "The extra underground cave must retain its count, thickness distribution and weird-thickness flag.");
    }

    private static void CheckConfiguredCanyon()
    {
        Require(Carvers.CANYON_VALUE is CanyonWorldCarver, "The CANYON key must hold a CanyonWorldCarver.");
        var canyon = (CanyonWorldCarver)Carvers.CANYON_VALUE;

        Require(
            canyon.Probability == 0.01f &&
            canyon.Y is UniformHeight
            {
                Minimum: var minimum,
                Maximum: var maximum
            } &&
            minimum == VerticalAnchor.Absolute(10) &&
            maximum == VerticalAnchor.Absolute(67) &&
            canyon.VerticalRotation is UniformFloat { Minimum: -0.125f, Maximum: 0.125f },
            "The default canyon must retain the 26.3 probability, height range and vertical rotation.");

        CanyonWorldCarver.Shape shape = canyon.CarverShape;
        Require(
            shape.DistanceFactor is UniformFloat { Minimum: 0.75f, Maximum: 1.0f } &&
            shape.Thickness is TrapezoidFloat { Minimum: 0.0f, Maximum: 6.0f, Plateau: 2.0f } &&
            shape.WidthSmoothness == 3 &&
            shape.HorizontalRadiusFactor is UniformFloat { Minimum: 0.75f, Maximum: 1.0f } &&
            shape.VerticalRadiusDefaultFactor == 1.0f &&
            shape.VerticalRadiusCenterFactor == 0.0f &&
            shape.YScale is ConstantFloat { Value: 3.0f },
            "The default canyon must retain all seven 26.3 shape values.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
