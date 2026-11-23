namespace Asteria.World.LevelGen;

/// <summary>
/// The twelve named density-function slots shared by Minecraft Java 26.3
/// overworld, amplified-overworld and large-biome terrain configurations.
/// </summary>
public sealed record OverworldFunctionSet<T>(
    T Temperature,
    T Vegetation,
    T Continents,
    T Erosion,
    T Offset,
    T Factor,
    T Jaggedness,
    T Depth,
    T SlopedCheese,
    T PreliminarySurfaceLevel,
    T ChunkSurfaceLevel,
    T FinalDensity)
{
    /// <summary>
    /// Maps every slot in vanilla record-component order.
    /// </summary>
    public OverworldFunctionSet<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return new OverworldFunctionSet<TResult>(
            mapper(Temperature),
            mapper(Vegetation),
            mapper(Continents),
            mapper(Erosion),
            mapper(Offset),
            mapper(Factor),
            mapper(Jaggedness),
            mapper(Depth),
            mapper(SlopedCheese),
            mapper(PreliminarySurfaceLevel),
            mapper(ChunkSurfaceLevel),
            mapper(FinalDensity));
    }
}
