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
/// Surface-material pass from Minecraft Java 26.3, including generic rules, Badlands pillars,
/// Frozen Ocean icebergs and the deterministic clay-band palette.
/// </summary>
public sealed class MaterialSystem
{
    private readonly BlockState _defaultBlock;
    private readonly int _seaLevel;
    private readonly PositionalRandomFactory _noiseRandom;
    private readonly Noise _surfaceNoise;
    private readonly Noise _surfaceSecondaryNoise;
    private readonly Noise _clayBandsOffsetNoise;
    private readonly Noise _badlandsPillarNoise;
    private readonly Noise _badlandsPillarRoofNoise;
    private readonly Noise _badlandsSurfaceNoise;
    private readonly Noise _icebergPillarNoise;
    private readonly Noise _icebergPillarRoofNoise;
    private readonly Noise _icebergSurfaceNoise;
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
        _badlandsPillarNoise = randomState.GetOrCreateNoise(Noises.BADLANDS_PILLAR);
        _badlandsPillarRoofNoise = randomState.GetOrCreateNoise(Noises.BADLANDS_PILLAR_ROOF);
        _badlandsSurfaceNoise = randomState.GetOrCreateNoise(Noises.BADLANDS_SURFACE);
        _icebergPillarNoise = randomState.GetOrCreateNoise(Noises.ICEBERG_PILLAR);
        _icebergPillarRoofNoise = randomState.GetOrCreateNoise(Noises.ICEBERG_PILLAR_ROOF);
        _icebergSurfaceNoise = randomState.GetOrCreateNoise(Noises.ICEBERG_SURFACE);
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
                int startingHeight = chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, z) + 1;

                columnPosition.SetX(blockX);
                columnPosition.SetZ(blockZ);
                Holder<Biome> surfaceBiome = biomeGetter(
                    new BlockPos(blockX, startingHeight, blockZ));

                if (ReferenceEquals(surfaceBiome.Value, Biomes.ERODED_BADLANDS))
                {
                    ErodedBadlandsExtension(
                        chunk, columnPosition, blockX, blockZ, startingHeight);
                }

                int height = chunk.GetHeight(Heightmap.Types.WORLD_SURFACE_WG, x, z) + 1;
                int gradientX = GetSurfaceGradientX(chunk, x, z);
                int gradientZ = GetSurfaceGradientZ(chunk, x, z);
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

                if (ReferenceEquals(surfaceBiome.Value, Biomes.FROZEN_OCEAN) ||
                    ReferenceEquals(surfaceBiome.Value, Biomes.DEEP_FROZEN_OCEAN))
                {
                    FrozenOceanExtension(
                        context.MinSurfaceLevel,
                        surfaceBiome.Value,
                        chunk,
                        columnPosition,
                        blockX,
                        blockZ,
                        startingHeight);
                }
            }
        }
    }

    /// <summary>
    /// Evaluates one block's top surface material after carving, matching the
    /// Minecraft Java 26.3 MaterialSystem.topMaterial path.
    /// </summary>
    public BlockState? TopMaterial(
        MaterialRule ruleSource,
        RandomState randomState,
        WorldGenerationContext generationContext,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        ChunkAccess chunk,
        DensitySamplerSet densitySamplers,
        BlockPos position,
        bool underFluid)
    {
        ArgumentNullException.ThrowIfNull(ruleSource);
        ArgumentNullException.ThrowIfNull(randomState);
        ArgumentNullException.ThrowIfNull(generationContext);
        ArgumentNullException.ThrowIfNull(biomeGetter);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(densitySamplers);
        ArgumentNullException.ThrowIfNull(position);

        int blockX = position.GetX();
        int blockY = position.GetY();
        int blockZ = position.GetZ();
        var volume = new DensityVolume(1, 1, 1, blockX, blockY, blockZ);
        var context = new MaterialRuleContext(
            this,
            randomState,
            volume,
            densitySamplers,
            biomeGetter,
            generationContext);
        RuleEvaluator rule = ruleSource.Compile(context);
        context.UpdateXZ(
            blockX,
            blockZ,
            GetSurfaceGradientX(
                chunk,
                SectionPos.SectionRelative(blockX),
                SectionPos.SectionRelative(blockZ)),
            GetSurfaceGradientZ(
                chunk,
                SectionPos.SectionRelative(blockX),
                SectionPos.SectionRelative(blockZ)));
        context.UpdateY(
            stoneDepthAbove: 1,
            stoneDepthBelow: 1,
            waterHeight: underFluid ? unchecked(blockY + 1) : int.MinValue,
            blockY: blockY);
        return rule(blockX, blockY, blockZ);
    }

    private void ErodedBadlandsExtension(
        ChunkAccess chunk,
        BlockPos.MutableBlockPos columnPosition,
        int blockX,
        int blockZ,
        int height)
    {
        double pillarBuffer = Math.Min(
            Math.Abs(_badlandsSurfaceNoise.Get(blockX, 0.0, blockZ) * 8.25),
            _badlandsPillarNoise.Get(blockX * 0.2, 0.0, blockZ * 0.2) * 15.0);

        if (pillarBuffer <= 0.0)
            return;

        double pillarFloor = Math.Abs(
            _badlandsPillarRoofNoise.Get(blockX * 0.75, 0.0, blockZ * 0.75) * 1.5);
        double extensionTop = 64.0 + Math.Min(
            pillarBuffer * pillarBuffer * 2.5,
            Math.Ceiling(pillarFloor * 50.0) + 24.0);
        int startY = Mth.Floor(extensionTop);

        if (height > startY)
            return;

        for (int y = startY; y >= chunk.GetMinY(); y--)
        {
            BlockState state = chunk.GetBlockState(columnPosition.SetY(y));
            if (ReferenceEquals(state.Block, _defaultBlock.Block))
                break;
            if (state.Is(Blocks.WATER))
                return;
        }

        for (int y = startY; y >= chunk.GetMinY(); y--)
        {
            columnPosition.SetY(y);
            if (!chunk.GetBlockState(columnPosition).IsAir)
                break;

            SetBlock(chunk, columnPosition, _defaultBlock);
        }
    }

    private void FrozenOceanExtension(
        int minSurfaceLevel,
        Biome surfaceBiome,
        ChunkAccess chunk,
        BlockPos.MutableBlockPos columnPosition,
        int blockX,
        int blockZ,
        int height)
    {
        double iceberg = Math.Min(
            Math.Abs(_icebergSurfaceNoise.Get(blockX, 0.0, blockZ) * 8.25),
            _icebergPillarNoise.Get(blockX * 1.28, 0.0, blockZ * 1.28) * 15.0);

        if (iceberg <= 1.8)
            return;

        double icebergRoof = Math.Abs(
            _icebergPillarRoofNoise.Get(blockX * 1.17, 0.0, blockZ * 1.17) * 1.5);
        double icebergTop = Math.Min(
            iceberg * iceberg * 1.2,
            Math.Ceiling(icebergRoof * 40.0) + 14.0);

        if (surfaceBiome.ShouldMeltFrozenOceanIcebergSlightly(
                new BlockPos(blockX, _seaLevel, blockZ),
                _seaLevel))
        {
            icebergTop -= 2.0;
        }

        if (icebergTop <= 2.0)
            return;

        double icebergBottom = _seaLevel - icebergTop - 7.0;
        icebergTop += _seaLevel;

        IRandomSource random = _noiseRandom.At(blockX, 0, blockZ);
        int snowDepth = 0;
        int maxSnowDepth = 2 + random.NextInt(4);
        int minSnowHeight = _seaLevel + 18 + random.NextInt(10);
        int icebergTopY = (int)icebergTop;

        for (int y = Math.Max(height, icebergTopY + 1); y >= minSurfaceLevel; y--)
        {
            columnPosition.SetY(y);
            BlockState state = chunk.GetBlockState(columnPosition);

            bool replace =
                state.IsAir &&
                y < icebergTopY &&
                random.NextDouble() > 0.01 ||
                state.Is(Blocks.WATER) &&
                y > (int)icebergBottom &&
                y < _seaLevel &&
                random.NextDouble() > 0.15;

            if (!replace)
                continue;

            if (snowDepth <= maxSnowDepth && y > minSnowHeight)
            {
                SetBlock(chunk, columnPosition, Blocks.SNOW_BLOCK.DefaultState);
                snowDepth++;
            }
            else
            {
                SetBlock(chunk, columnPosition, Blocks.PACKED_ICE.DefaultState);
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
