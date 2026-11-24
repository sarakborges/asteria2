using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Materials.Rules;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen.Materials;

/// <summary>
/// Surface-material pass from Minecraft Java 26.3. This slice implements the generic column
/// scan and material-rule context; biome-specific badlands/frozen-ocean extensions and clay-band
/// replacement remain separate content-dependent increments.
/// </summary>
public sealed class MaterialSystem
{
    private readonly BlockState _defaultBlock;
    private readonly int _seaLevel;
    private readonly PositionalRandomFactory _noiseRandom;
    private readonly Noise _surfaceNoise;
    private readonly Noise _surfaceSecondaryNoise;
    private readonly Noise _clayBandsOffsetNoise;
    private readonly BlockState[] _clayBands;

    public DensityFunction PreliminarySurfaceFunction { get; }
    public BlockState DefaultBlock => _defaultBlock;

    public MaterialSystem(
        RandomState randomState,
        BlockState defaultBlock,
        int seaLevel,
        DensityFunction preliminarySurfaceFunction,
        PositionalRandomFactory noiseRandom)
    {
        ArgumentNullException.ThrowIfNull(randomState);
        _defaultBlock = defaultBlock ?? throw new ArgumentNullException(nameof(defaultBlock));
        _seaLevel = seaLevel;
        PreliminarySurfaceFunction = preliminarySurfaceFunction ??
            throw new ArgumentNullException(nameof(preliminarySurfaceFunction));
        _noiseRandom = noiseRandom ?? throw new ArgumentNullException(nameof(noiseRandom));
        _surfaceNoise = randomState.GetOrCreateNoise(Noises.SURFACE);
        _surfaceSecondaryNoise = randomState.GetOrCreateNoise(Noises.SURFACE_SECONDARY);
        _clayBandsOffsetNoise = randomState.GetOrCreateNoise(Noises.CLAY_BANDS_OFFSET);
        IRandomSource clayBandsRandom = _noiseRandom.FromHashOf(
            Asteria.Content.Identifiers.ResourceLocation.FromNamespaceAndPath("minecraft", "clay_bands"));
        _clayBands = GenerateClayBands(clayBandsRandom);
    }

    public int GetSurfaceDepth(int blockX, int blockZ)
    {
        double noiseValue = _surfaceNoise.Get(blockX, 0.0, blockZ);
        return (int)(noiseValue * 2.75 + 3.0 +
            _noiseRandom.At(blockX, 0, blockZ).NextDouble() * 0.25);
    }

    public double GetSurfaceSecondary(int blockX, int blockZ) =>
        _surfaceSecondaryNoise.Get(blockX, 0.0, blockZ);

    public int GetSeaLevel() => _seaLevel;

    /// <summary>Returns the vanilla clay-band state for a world-space block position.</summary>
    public BlockState GetBand(int worldX, int y, int worldZ)
    {
        double bandOffsetNoise = _clayBandsOffsetNoise.Get(worldX, 0.0, worldZ) * 4.0;
        int offset = Mth.Floor(bandOffsetNoise + 0.5);
        int index = (y + offset + _clayBands.Length) % _clayBands.Length;
        return _clayBands[index];
    }

    internal static BlockState[] GenerateClayBands(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        BlockState[] bands = new BlockState[192];
        Array.Fill(bands, Blocks.TERRACOTTA.DefaultState);

        for (int index = 0; index < bands.Length; index++)
        {
            index += random.NextInt(5) + 1;
            if (index < bands.Length)
                bands[index] = Blocks.ORANGE_TERRACOTTA.DefaultState;
        }

        MakeClayBands(random, bands, 1, Blocks.YELLOW_TERRACOTTA.DefaultState);
        MakeClayBands(random, bands, 2, Blocks.BROWN_TERRACOTTA.DefaultState);
        MakeClayBands(random, bands, 1, Blocks.RED_TERRACOTTA.DefaultState);

        int whiteBandCount = random.NextIntBetweenInclusive(9, 15);
        int start = 0;
        for (int band = 0; band < whiteBandCount && start < bands.Length; band++)
        {
            bands[start] = Blocks.WHITE_TERRACOTTA.DefaultState;
            if (start - 1 > 0 && random.NextBoolean())
                bands[start - 1] = Blocks.LIGHT_GRAY_TERRACOTTA.DefaultState;
            if (start + 1 < bands.Length && random.NextBoolean())
                bands[start + 1] = Blocks.LIGHT_GRAY_TERRACOTTA.DefaultState;

            start += random.NextInt(16) + 4;
        }

        return bands;
    }

    private static void MakeClayBands(
        IRandomSource random,
        BlockState[] bands,
        int baseWidth,
        BlockState state)
    {
        int bandCount = random.NextIntBetweenInclusive(6, 15);
        for (int band = 0; band < bandCount; band++)
        {
            int width = baseWidth + random.NextInt(3);
            int start = random.NextInt(bands.Length);
            for (int offset = 0; start + offset < bands.Length && offset < width; offset++)
                bands[start + offset] = state;
        }
    }

    /// <summary>
    /// Applies the configured material rule once per solid block in each occupied column.
    /// The same NoiseChunk used for density filling supplies the cached samplers.
    /// </summary>
    public void BuildSurface(
        RandomState randomState,
        WorldGenerationContext generationContext,
        ChunkAccess chunk,
        NoiseChunk noiseChunk,
        MaterialRule ruleSource,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        IReadOnlySet<Holder<Biome>>? possibleBiomes = null)
    {
        ArgumentNullException.ThrowIfNull(randomState);
        ArgumentNullException.ThrowIfNull(generationContext);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(noiseChunk);
        ArgumentNullException.ThrowIfNull(ruleSource);
        ArgumentNullException.ThrowIfNull(biomeGetter);

        int highestFilledSectionIndex = chunk.GetHighestFilledSectionIndex();
        if (highestFilledSectionIndex == ChunkAccess.NoFilledSection)
            return;

        int minY = chunk.GetMinY();
        int maxY = chunk.GetMaxY();
        int maxBlockY = chunk.GetSectionYFromSectionIndex(highestFilledSectionIndex) * 16 + 15;
        DensityVolume fullVolume = noiseChunk.Volume;
        var narrowedVolume = new DensityVolume(
            fullVolume.SizeX,
            Math.Max(maxBlockY - fullVolume.MinBlockY + 1, 1),
            fullVolume.SizeZ,
            fullVolume.MinBlockX,
            fullVolume.MinBlockY,
            fullVolume.MinBlockZ);

        var context = new MaterialRuleContext(
            this,
            randomState,
            narrowedVolume,
            noiseChunk.CachingSamplers,
            biomeGetter,
            generationContext,
            possibleBiomes);
        RuleEvaluator rule = ruleSource.Compile(context);
        var columnPosition = new BlockPos.MutableBlockPos();

        ChunkPos chunkPosition = chunk.GetPos();
        int minBlockX = chunkPosition.GetMinBlockX();
        int minBlockZ = chunkPosition.GetMinBlockZ();

        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                int blockX = minBlockX + x;
                int blockZ = minBlockZ + z;
                int height = chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, z) + 1;
                int gradientX = GetSurfaceGradientX(chunk, x, z);
                int gradientZ = GetSurfaceGradientZ(chunk, x, z);

                columnPosition.SetX(blockX);
                columnPosition.SetZ(blockZ);
                context.UpdateXZ(blockX, blockZ, gradientX, gradientZ);

                int stoneAboveDepth = 0;
                int waterHeight = int.MinValue;
                int nextCeilingStoneY = int.MaxValue;

                for (int y = height; y >= minY; y--)
                {
                    columnPosition.SetY(y);
                    BlockState oldState = chunk.GetBlockState(columnPosition);

                    if (oldState.IsAir)
                    {
                        stoneAboveDepth = 0;
                        waterHeight = int.MinValue;
                    }
                    else if (!oldState.GetFluidState().IsEmpty())
                    {
                        if (waterHeight == int.MinValue)
                            waterHeight = y + 1;
                    }
                    else
                    {
                        if (nextCeilingStoneY >= y)
                        {
                            nextCeilingStoneY = int.MinValue;
                            for (int lookaheadY = y - 1; lookaheadY >= minY - 1; lookaheadY--)
                            {
                                columnPosition.SetY(lookaheadY);
                                BlockState nextState = chunk.GetBlockState(columnPosition);
                                if (!IsStone(nextState))
                                {
                                    nextCeilingStoneY = lookaheadY + 1;
                                    break;
                                }
                            }

                            columnPosition.SetY(y);
                        }

                        stoneAboveDepth++;
                        int stoneBelowDepth = y - nextCeilingStoneY + 1;
                        context.UpdateY(stoneAboveDepth, stoneBelowDepth, waterHeight, y);

                        if (y >= minY && y <= maxY)
                        {
                            BlockState? state = rule(blockX, y, blockZ);
                            if (state is not null)
                                SetBlock(chunk, columnPosition, state);
                        }
                    }
                }
            }
        }
    }

    private static void SetBlock(ChunkAccess chunk, BlockPos.MutableBlockPos position, BlockState state)
    {
        chunk.SetBlockState(position, state);
        if (!state.GetFluidState().IsEmpty())
            chunk.MarkPosForPostProcessing(position);
    }

    private static int GetSurfaceGradientX(ChunkAccess chunk, int x, int z) =>
        chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, Math.Min(x + 1, 15), z) -
        chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, Math.Max(x - 1, 0), z);

    private static int GetSurfaceGradientZ(ChunkAccess chunk, int x, int z) =>
        chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, Math.Min(z + 1, 15)) -
        chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, Math.Max(z - 1, 0));

    private static bool IsStone(BlockState state) =>
        !state.IsAir && state.GetFluidState().IsEmpty();
}
