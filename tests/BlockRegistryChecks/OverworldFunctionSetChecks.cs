using Asteria.Content.Registries;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;

internal static class OverworldFunctionSetChecks
{
    public static void Run()
    {
        var source = new OverworldFunctionSet<int>(
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
        var mapped = source.Map(static value => $"density/{value}");

        Require(
            mapped == new OverworldFunctionSet<string>(
                "density/1",
                "density/2",
                "density/3",
                "density/4",
                "density/5",
                "density/6",
                "density/7",
                "density/8",
                "density/9",
                "density/10",
                "density/11",
                "density/12"),
            "OverworldFunctionSet.map must preserve all twelve vanilla slots and their declared order.");

        var identity = source.Map(static value => value);
        Require(
            identity == source,
            "Mapping an OverworldFunctionSet with the identity function must preserve its complete value.");

        CheckVanillaFunctionSets();

        try
        {
            source.Map<int>(null!);
        }
        catch (ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException("OverworldFunctionSet.map must reject a null mapper.");
    }

    private static void CheckVanillaFunctionSets()
    {
        OverworldFunctionSet<RegistryKey<DensityFunction>> normal =
            DensityFunctionKeys.OVERWORLD_FUNCTIONS;
        OverworldFunctionSet<RegistryKey<DensityFunction>> amplified =
            DensityFunctionKeys.AMPLIFIED_OVERWORLD_FUNCTIONS;
        OverworldFunctionSet<RegistryKey<DensityFunction>> large =
            DensityFunctionKeys.LARGE_OVERWORLD_FUNCTIONS;

        CheckPaths(
            normal,
            "overworld/temperature",
            "overworld/vegetation",
            "overworld/continents",
            "overworld/erosion",
            "overworld/offset",
            "overworld/factor",
            "overworld/jaggedness",
            "overworld/depth",
            "overworld/sloped_cheese",
            "overworld/preliminary_surface_level",
            "overworld/chunk_surface_level",
            "overworld/final_density");

        CheckPaths(
            amplified,
            "overworld/temperature",
            "overworld/vegetation",
            "overworld/continents",
            "overworld/erosion",
            "overworld_amplified/offset",
            "overworld_amplified/factor",
            "overworld_amplified/jaggedness",
            "overworld_amplified/depth",
            "overworld_amplified/sloped_cheese",
            "overworld_amplified/preliminary_surface_level",
            "overworld_amplified/chunk_surface_level",
            "overworld_amplified/final_density");

        CheckPaths(
            large,
            "overworld_large_biomes/temperature",
            "overworld_large_biomes/vegetation",
            "overworld_large_biomes/continents",
            "overworld_large_biomes/erosion",
            "overworld_large_biomes/offset",
            "overworld_large_biomes/factor",
            "overworld_large_biomes/jaggedness",
            "overworld_large_biomes/depth",
            "overworld_large_biomes/sloped_cheese",
            "overworld_large_biomes/preliminary_surface_level",
            "overworld_large_biomes/chunk_surface_level",
            "overworld_large_biomes/final_density");

        Require(
            amplified.Temperature.Equals(normal.Temperature) &&
            amplified.Vegetation.Equals(normal.Vegetation) &&
            amplified.Continents.Equals(normal.Continents) &&
            amplified.Erosion.Equals(normal.Erosion),
            "The vanilla amplified function set must reuse the normal Overworld's four climate keys.");

        Require(
            !amplified.Offset.Equals(normal.Offset) &&
            !amplified.Factor.Equals(normal.Factor) &&
            !amplified.Jaggedness.Equals(normal.Jaggedness) &&
            !amplified.Depth.Equals(normal.Depth) &&
            !amplified.SlopedCheese.Equals(normal.SlopedCheese) &&
            !amplified.PreliminarySurfaceLevel.Equals(normal.PreliminarySurfaceLevel) &&
            !amplified.ChunkSurfaceLevel.Equals(normal.ChunkSurfaceLevel) &&
            !amplified.FinalDensity.Equals(normal.FinalDensity),
            "The vanilla amplified function set must use distinct keys for all eight terrain functions.");

        Require(
            large.Temperature.Equals(normal.Temperature) == false &&
            large.Vegetation.Equals(normal.Vegetation) == false &&
            large.Continents.Equals(normal.Continents) == false &&
            large.Erosion.Equals(normal.Erosion) == false,
            "The vanilla large-biome function set must use its own four climate keys.");
    }

    private static void CheckPaths(
        OverworldFunctionSet<RegistryKey<DensityFunction>> functions,
        params string[] expectedPaths)
    {
        string[] actualPaths =
        [
            functions.Temperature.Location.ToString(),
            functions.Vegetation.Location.ToString(),
            functions.Continents.Location.ToString(),
            functions.Erosion.Location.ToString(),
            functions.Offset.Location.ToString(),
            functions.Factor.Location.ToString(),
            functions.Jaggedness.Location.ToString(),
            functions.Depth.Location.ToString(),
            functions.SlopedCheese.Location.ToString(),
            functions.PreliminarySurfaceLevel.Location.ToString(),
            functions.ChunkSurfaceLevel.Location.ToString(),
            functions.FinalDensity.Location.ToString()
        ];

        string[] expectedLocations = expectedPaths
            .Select(static path => $"minecraft:{path}")
            .ToArray();

        Require(
            actualPaths.SequenceEqual(expectedLocations, StringComparer.Ordinal),
            $"Overworld function-set keys differ from the vanilla 26.3 definition. Expected [{string.Join(", ", expectedLocations)}], got [{string.Join(", ", actualPaths)}].");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
