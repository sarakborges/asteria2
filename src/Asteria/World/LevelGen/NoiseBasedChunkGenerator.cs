using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Materials.Rules;

namespace Asteria.World.LevelGen;

/// <summary>
/// Minecraft Java 26.3 density, aquifer and generic surface-rule stages for a ProtoChunk.
/// Carvers and biome-specific surface extensions remain later pipeline work.
/// </summary>
public sealed class NoiseBasedChunkGenerator
{
    private const int LAVA_LEVEL = -54;

    private readonly NoiseGeneratorSettings _settings;
    private readonly RandomState _randomState;
    private readonly Aquifer.FluidPicker _globalFluidPicker;

    public NoiseGeneratorSettings Settings => _settings;

    public int GetMinY() => _settings.NoiseSettings.MinY;

    public int GetGenDepth() => _settings.NoiseSettings.Height;

    public int GetSeaLevel() => _settings.SeaLevel;

    public NoiseBasedChunkGenerator(
        NoiseGeneratorSettings settings,
        RandomState randomState,
        Aquifer.FluidPicker? globalFluidPicker = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _randomState = randomState ?? throw new ArgumentNullException(nameof(randomState));
        _globalFluidPicker = globalFluidPicker ?? CreateFluidPicker(settings);
    }

    /// <summary>Returns lava below min(-54, sea level), then the configured fluid below sea level.</summary>
    public static Aquifer.FluidPicker CreateFluidPicker(NoiseGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        int lavaCutoff = Math.Min(LAVA_LEVEL, settings.SeaLevel);
        var lavaStatus = new Aquifer.FluidStatus(LAVA_LEVEL, Blocks.LAVA.DefaultState);
        var defaultFluidStatus = new Aquifer.FluidStatus(settings.SeaLevel, settings.DefaultFluid);
        return (_, blockY, _) => blockY < lavaCutoff ? lavaStatus : defaultFluidStatus;
    }

    /// <summary>
    /// Fills final_density and aquifer results into the proto chunk. This method intentionally
    /// stops before surface materials, preserving the lower-level density-fill entry point.
    /// </summary>
    public void FillFromNoise(
        ProtoChunk chunk,
        Beardifier? beardifier = null,
        IChunkBlender? blender = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        NoiseSettings noiseSettings = _settings.NoiseSettings.ClampToHeightAccessor(chunk);
        if (noiseSettings.Height <= 0)
            return;

        DensityVolume volume = CreateVolume(chunk, noiseSettings);
        Heightmap.PrimeHeightmaps(chunk, ChunkStatus.BIOMES.GetHeightmapsAfter().ToHashSet());

        using var noiseChunk = new NoiseChunk(
            _randomState,
            beardifier,
            _settings,
            _globalFluidPicker,
            blender ?? IChunkBlender.Empty,
            volume);
        FillFromNoise(chunk, noiseChunk, volume);
        Heightmap.PrimeHeightmaps(chunk, ChunkStatus.TERRAIN.GetHeightmapsAfter().ToHashSet());
    }

    /// <summary>
    /// Runs density filling and the configured material-rule pass with one shared NoiseChunk.
    /// The biome lookup is supplied by the world/biome pipeline; no placeholder biome source is created.
    /// </summary>
    public void BuildTerrain(
        ProtoChunk chunk,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        IReadOnlySet<Holder<Biome>>? possibleBiomes = null,
        Beardifier? beardifier = null,
        IChunkBlender? blender = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(biomeGetter);

        NoiseSettings noiseSettings = _settings.NoiseSettings.ClampToHeightAccessor(chunk);
        if (noiseSettings.Height <= 0)
            return;

        DensityVolume volume = CreateVolume(chunk, noiseSettings);
        Heightmap.PrimeHeightmaps(chunk, ChunkStatus.BIOMES.GetHeightmapsAfter().ToHashSet());

        using var noiseChunk = new NoiseChunk(
            _randomState,
            beardifier,
            _settings,
            _globalFluidPicker,
            blender ?? IChunkBlender.Empty,
            volume);

        FillFromNoise(chunk, noiseChunk, volume);
        Heightmap.PrimeHeightmaps(chunk, ChunkStatus.TERRAIN.GetHeightmapsAfter().ToHashSet());

        MaterialSystem materialSystem = _randomState.GetOrCreateMaterialSystem(_settings);
        materialSystem.BuildSurface(
            _randomState,
            new WorldGenerationContext(this, chunk),
            chunk,
            noiseChunk,
            _settings.MaterialRule.Value,
            biomeGetter,
            possibleBiomes);
    }

    private static DensityVolume CreateVolume(ProtoChunk chunk, NoiseSettings noiseSettings)
    {
        ChunkPos chunkPosition = chunk.GetPos();
        return new DensityVolume(
            ChunkSection.Width,
            noiseSettings.Height,
            ChunkSection.Depth,
            chunkPosition.GetMinBlockX(),
            noiseSettings.MinY,
            chunkPosition.GetMinBlockZ());
    }

    private void FillFromNoise(ProtoChunk chunk, NoiseChunk noiseChunk, DensityVolume volume)
    {
        using ScopedDensityBuffer densities =
            noiseChunk.GetSampler(_settings.NoiseRouter.FinalDensity).SampleVolume(volume);

        for (int z = 0; z < volume.SizeZ; z++)
        {
            int blockZ = volume.BlockZ(z);
            for (int x = 0; x < volume.SizeX; x++)
            {
                int blockX = volume.BlockX(x);
                for (int y = volume.SizeY - 1; y >= 0; y--)
                {
                    int blockY = volume.BlockY(y);
                    int index = volume.IndexUnchecked(x, y, z);
                    BlockState? selectedState = noiseChunk.Aquifer.ComputeSubstance(
                        blockX, blockY, blockZ, densities.Get(index));
                    BlockState state = selectedState ?? _settings.DefaultBlock;
                    var position = new BlockPos(blockX, blockY, blockZ);

                    if (!state.IsAir)
                        chunk.SetBlockState(position, state, flags: 0);

                    if (noiseChunk.Aquifer.ShouldScheduleFluidUpdate())
                        chunk.MarkPosForPostProcessing(position);
                }
            }
        }
    }
}
