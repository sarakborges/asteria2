using System.Text.Json;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen;

/// <summary>
/// Built-in normal-noise parameter data from Minecraft Java 26.3 worldgen resources.
/// The data is kept in the same field shape consumed by NormalNoise.DIRECT_CODEC.
/// </summary>
internal static class BuiltInNoises
{
    private sealed record Definition(RegistryKey<NormalNoise> Key, string Json);

    private static readonly Definition[] Definitions =
    [
        new(Noises.AQUIFER_BARRIER, """{"base_amplitude":0.955388882960065,"base_octave":-3}"""),
        new(Noises.AQUIFER_FLUID_LEVEL_FLOODEDNESS, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.AQUIFER_FLUID_LEVEL_SPREAD, """{"base_amplitude":0.955388882960065,"base_octave":-5}"""),
        new(Noises.AQUIFER_LAVA, """{"base_amplitude":0.955388882960065,"base_octave":-1}"""),
        new(Noises.BADLANDS_PILLAR, """{"base_amplitude":0.939546696581729,"base_octave":-2,"octave_count":4}"""),
        new(Noises.BADLANDS_PILLAR_ROOF, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.BADLANDS_SURFACE, """{"base_amplitude":0.9381732587751008,"base_octave":-6,"octave_count":3}"""),
        new(Noises.CALCITE, """{"base_amplitude":0.939546696581729,"base_octave":-9,"octave_count":4}"""),
        new(Noises.CAVE_CHEESE, """{"amplitude_modifiers":[0.5,1,2,1,2,1,0,2,0],"base_amplitude":0.8361300524356068,"base_octave":-8,"octave_count":9}"""),
        new(Noises.CAVE_ENTRANCE, """{"amplitude_modifiers":[0.4,0.5,1],"base_amplitude":0.8500634887071167,"base_octave":-7,"octave_count":3}"""),
        new(Noises.CAVE_LAYER, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.CLAY_BANDS_OFFSET, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.CONTINENTALNESS, """{"amplitude_modifiers":[1,1,2,2,2,1,1,1,1],"base_amplitude":0.8880832896205223,"base_octave":-9,"octave_count":9}"""),
        new(Noises.CONTINENTALNESS_LARGE, """{"amplitude_modifiers":[1,1,2,2,2,1,1,1,1],"base_amplitude":0.8880832896205223,"base_octave":-11,"octave_count":9}"""),
        new(Noises.EROSION, """{"amplitude_modifiers":[1,1,0,1,1],"base_amplitude":1.063180125160734,"base_octave":-9,"octave_count":5}"""),
        new(Noises.EROSION_LARGE, """{"amplitude_modifiers":[1,1,0,1,1],"base_amplitude":1.063180125160734,"base_octave":-11,"octave_count":5}"""),
        new(Noises.GRAVEL, """{"base_amplitude":0.939546696581729,"base_octave":-8,"octave_count":4}"""),
        new(Noises.GRAVEL_LAYER, """{"amplitude_modifiers":[1,1,1,1,0,0,0,0,0.013333333333333334],"base_amplitude":1.0569606747151457,"base_octave":-8,"octave_count":9}"""),
        new(Noises.ICE, """{"base_amplitude":0.939546696581729,"base_octave":-4,"octave_count":4}"""),
        new(Noises.ICEBERG_PILLAR, """{"base_amplitude":0.939546696581729,"base_octave":-6,"octave_count":4}"""),
        new(Noises.ICEBERG_PILLAR_ROOF, """{"base_amplitude":0.955388882960065,"base_octave":-3}"""),
        new(Noises.ICEBERG_SURFACE, """{"base_amplitude":0.9381732587751008,"base_octave":-6,"octave_count":3}"""),
        new(Noises.JAGGED, """{"base_amplitude":1.0383104856073737,"base_octave":-16,"octave_count":16}"""),
        new(Noises.NETHER_STATE_SELECTOR, """{"base_amplitude":0.955388882960065,"base_octave":-4}"""),
        new(Noises.NETHER_WART, """{"amplitude_modifiers":[1,0,0,0.9],"base_amplitude":1.3827102115748344,"base_octave":-3,"octave_count":4}"""),
        new(Noises.NETHERRACK, """{"amplitude_modifiers":[1,0,0,0.35],"base_amplitude":1.4659491761370222,"base_octave":-3,"octave_count":4}"""),
        new(Noises.NOODLE, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.NOODLE_RIDGE_A, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.NOODLE_RIDGE_B, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.NOODLE_THICKNESS, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.SHIFT, """{"amplitude_modifiers":[1,1,1,0],"base_amplitude":0.9381732587751005,"base_octave":-3,"octave_count":4}"""),
        new(Noises.ORE_GAP, """{"base_amplitude":0.955388882960065,"base_octave":-5}"""),
        new(Noises.ORE_VEIN_A, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.ORE_VEIN_B, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.ORE_VEININESS, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.PACKED_ICE, """{"base_amplitude":0.939546696581729,"base_octave":-7,"octave_count":4}"""),
        new(Noises.PATCH, """{"amplitude_modifiers":[1,0,0,0,0,0.013333333333333334],"base_amplitude":1.637127519350388,"base_octave":-5,"octave_count":6}"""),
        new(Noises.PILLAR, """{"base_amplitude":0.9494731054427981,"base_octave":-7,"octave_count":2}"""),
        new(Noises.PILLAR_RARENESS, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.PILLAR_THICKNESS, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.POWDER_SNOW, """{"base_amplitude":0.939546696581729,"base_octave":-6,"octave_count":4}"""),
        new(Noises.RIDGE, """{"amplitude_modifiers":[1,2,1,0,0,0],"base_amplitude":0.9147152149950137,"base_octave":-7,"octave_count":6}"""),
        new(Noises.SMALL_PATCH, """{"amplitude_modifiers":[3],"base_amplitude":0.955388882960065,"base_octave":-3}"""),
        new(Noises.SOUL_SAND_LAYER, """{"amplitude_modifiers":[1,1,1,1,0,0,0,0,0.013333333333333334],"base_amplitude":1.0569606747151457,"base_octave":-8,"octave_count":9}"""),
        new(Noises.SPAGHETTI_2D, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.SPAGHETTI_2D_ELEVATION, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.SPAGHETTI_2D_MODULATOR, """{"base_amplitude":0.955388882960065,"base_octave":-11}"""),
        new(Noises.SPAGHETTI_2D_THICKNESS, """{"base_amplitude":0.955388882960065,"base_octave":-11}"""),
        new(Noises.SPAGHETTI_3D_1, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.SPAGHETTI_3D_2, """{"base_amplitude":0.955388882960065,"base_octave":-7}"""),
        new(Noises.SPAGHETTI_3D_RARITY, """{"base_amplitude":0.955388882960065,"base_octave":-11}"""),
        new(Noises.SPAGHETTI_3D_THICKNESS, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.SPAGHETTI_ROUGHNESS, """{"base_amplitude":0.955388882960065,"base_octave":-5}"""),
        new(Noises.SPAGHETTI_ROUGHNESS_MODULATOR, """{"base_amplitude":0.955388882960065,"base_octave":-8}"""),
        new(Noises.SULFUR_CAVE_GRADIENT, """{"amplitude_modifiers":[1,0,1],"base_amplitude":1.1817507833955654,"base_octave":-5,"octave_count":3}"""),
        new(Noises.SURFACE, """{"base_amplitude":0.9381732587751008,"base_octave":-6,"octave_count":3}"""),
        new(Noises.SURFACE_SECONDARY, """{"amplitude_modifiers":[1,1,0,1],"base_amplitude":1.0582769165096106,"base_octave":-6,"octave_count":4}"""),
        new(Noises.SWAMP, """{"base_amplitude":0.955388882960065,"base_octave":-2}"""),
        new(Noises.TEMPERATURE, """{"amplitude_modifiers":[1.5,0,1,0,0,0],"base_amplitude":1.2453007926713473,"base_octave":-10,"octave_count":6}"""),
        new(Noises.TEMPERATURE_LARGE, """{"amplitude_modifiers":[1.5,0,1,0,0,0],"base_amplitude":1.2453007926713473,"base_octave":-12,"octave_count":6}"""),
        new(Noises.VEGETATION, """{"amplitude_modifiers":[1,1,0,0,0,0],"base_amplitude":0.9494731054427978,"base_octave":-8,"octave_count":6}"""),
        new(Noises.VEGETATION_LARGE, """{"amplitude_modifiers":[1,1,0,0,0,0],"base_amplitude":0.9494731054427978,"base_octave":-10,"octave_count":6}"""),
        new(Noises.TEMPERATURE_NETHER, """{"base_amplitude":0.9494731054427981,"base_octave":-7,"octave_count":2}"""),
        new(Noises.VEGETATION_NETHER, """{"base_amplitude":0.9494731054427981,"base_octave":-7,"octave_count":2}""")
    ];

    internal static int Count => Definitions.Length;

    internal static void RegisterAll()
    {
        foreach (Definition definition in Definitions)
        {
            using JsonDocument document = JsonDocument.Parse(definition.Json);
            NormalNoise value = NormalNoise.DIRECT_CODEC
                .Parse(JsonOps.INSTANCE, document.RootElement)
                .GetOrThrow(message => new InvalidOperationException(
                    $"Invalid built-in noise parameters for {definition.Key}: {message}"));

            BuiltInRegistries.Noise.Register(
                definition.Key,
                value,
                RegistrationInfo.BuiltIn);
        }
    }
}
