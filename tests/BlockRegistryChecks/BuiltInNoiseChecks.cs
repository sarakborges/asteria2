using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Synth;

internal static class BuiltInNoiseChecks
{
    private static readonly string[] ExpectedPaths =
    [
        "aquifer_barrier",
        "aquifer_fluid_level_floodedness",
        "aquifer_fluid_level_spread",
        "aquifer_lava",
        "badlands_pillar",
        "badlands_pillar_roof",
        "badlands_surface",
        "calcite",
        "cave_cheese",
        "cave_entrance",
        "cave_layer",
        "clay_bands_offset",
        "continentalness",
        "continentalness_large",
        "erosion",
        "erosion_large",
        "gravel",
        "gravel_layer",
        "ice",
        "iceberg_pillar",
        "iceberg_pillar_roof",
        "iceberg_surface",
        "jagged",
        "nether_state_selector",
        "nether_wart",
        "netherrack",
        "noodle",
        "noodle_ridge_a",
        "noodle_ridge_b",
        "noodle_thickness",
        "offset",
        "ore_gap",
        "ore_vein_a",
        "ore_vein_b",
        "ore_veininess",
        "packed_ice",
        "patch",
        "pillar",
        "pillar_rareness",
        "pillar_thickness",
        "powder_snow",
        "ridge",
        "small_patch",
        "soul_sand_layer",
        "spaghetti_2d",
        "spaghetti_2d_elevation",
        "spaghetti_2d_modulator",
        "spaghetti_2d_thickness",
        "spaghetti_3d_1",
        "spaghetti_3d_2",
        "spaghetti_3d_rarity",
        "spaghetti_3d_thickness",
        "spaghetti_roughness",
        "spaghetti_roughness_modulator",
        "sulfur_cave_gradient",
        "surface",
        "surface_secondary",
        "surface_swamp",
        "temperature",
        "temperature_large",
        "vegetation",
        "vegetation_large",
        "nether/temperature",
        "nether/vegetation"
    ];

    public static void Run()
    {
        CheckCompleteRegistryCatalogue();
        CheckCanonicalNoiseParameters();
    }

    private static void CheckCompleteRegistryCatalogue()
    {
        Require(
            BuiltInRegistries.Noise.Location == ResourceLocation.Parse("asteria:worldgen/noise") &&
            BuiltInRegistries.Noise.Key.RegistryLocation == BuiltInRegistries.RootLocation,
            "The built-in noise registry must use the local worldgen/noise key rooted in the Asteria registry tree.");

        string[] actualPaths = BuiltInRegistries.Noise.Entries
            .Select(holder => holder.Key.Location.Path)
            .ToArray();

        Require(
            actualPaths.Length == 64 &&
            actualPaths.ToHashSet(StringComparer.Ordinal).SetEquals(ExpectedPaths),
            "The built-in normal-noise registry must contain the complete 64-entry Minecraft 26.3 dataset under local Asteria keys.");

        Require(
            BuiltInRegistries.Noise.Entries.All(holder => holder.IsBound),
            "Every built-in normal-noise entry must be bound before the registry is frozen.");
    }

    private static void CheckCanonicalNoiseParameters()
    {
        NormalNoise temperature = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(1.2453007926713473)
            .SetBaseOctave(-10)
            .SetOctaveCount(6)
            .SetAmplitudeModifier(0, 1.5)
            .SetAmplitudeModifier(1, 0.0)
            .SetAmplitudeModifier(2, 1.0)
            .SetAmplitudeModifier(3, 0.0)
            .SetAmplitudeModifier(4, 0.0)
            .SetAmplitudeModifier(5, 0.0)
            .Build();

        NormalNoise offset = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(0.9381732587751005)
            .SetBaseOctave(-3)
            .SetOctaveCount(4)
            .SetAmplitudeModifier(0, 1.0)
            .SetAmplitudeModifier(1, 1.0)
            .SetAmplitudeModifier(2, 1.0)
            .SetAmplitudeModifier(3, 0.0)
            .Build();

        NormalNoise caveCheese = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(0.8361300524356068)
            .SetBaseOctave(-8)
            .SetOctaveCount(9)
            .SetAmplitudeModifier(0, 0.5)
            .SetAmplitudeModifier(1, 1.0)
            .SetAmplitudeModifier(2, 2.0)
            .SetAmplitudeModifier(3, 1.0)
            .SetAmplitudeModifier(4, 2.0)
            .SetAmplitudeModifier(5, 1.0)
            .SetAmplitudeModifier(6, 0.0)
            .SetAmplitudeModifier(7, 2.0)
            .SetAmplitudeModifier(8, 0.0)
            .Build();

        NormalNoise netherTemperature = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(0.9494731054427981)
            .SetBaseOctave(-7)
            .SetOctaveCount(2)
            .Build();

        NormalNoise swamp = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(0.955388882960065)
            .SetBaseOctave(-2)
            .Build();

        Require(
            Get(Noises.TEMPERATURE).Equals(temperature),
            "The temperature noise must retain the exact 26.3 base amplitude, octave range and amplitude modifiers.");
        Require(
            Get(Noises.SHIFT).Equals(offset),
            "The offset noise must retain the exact 26.3 offset amplitude modifiers.");
        Require(
            Get(Noises.CAVE_CHEESE).Equals(caveCheese),
            "The cave-cheese noise must retain all nine 26.3 amplitude modifiers, including disabled octaves.");
        Require(
            Get(Noises.TEMPERATURE_NETHER).Equals(netherTemperature) &&
            Get(Noises.VEGETATION_NETHER).Equals(netherTemperature),
            "Nether temperature and vegetation must load the distinct canonical registry entries with matching parameters.");
        Require(
            Get(Noises.SWAMP).Equals(swamp),
            "The swamp surface noise must use the canonical one-octave default parameterization.");
    }

    private static NormalNoise Get(RegistryKey<NormalNoise> key)
    {
        Holder<NormalNoise>.Reference holder = BuiltInRegistries.Noise.GetHolder(key) ??
            throw new InvalidOperationException($"Missing built-in noise holder for {key}.");

        Require(
            holder.IsBound && ReferenceEquals(holder, BuiltInRegistries.Noise.GetHolder(key)),
            $"Built-in noise key {key} must resolve to its canonical bound holder.");

        return holder.Value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
