using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// Minecraft Java 26.3 noise-based aquifer. Cell layout, randomized centers, nearest-neighbor
/// selection, fluid-level sampling and pressure interpolation follow the vanilla algorithm.
/// </summary>
public sealed class NoiseBasedAquifer : Aquifer
{
    private const int X_RANGE = 10;
    private const int Y_RANGE = 9;
    private const int Z_RANGE = 10;

    private const int X_SPACING = 16;
    private const int Y_SPACING = 12;
    private const int Z_SPACING = 16;

    private const int SAMPLE_OFFSET_X = -5;
    private const int SAMPLE_OFFSET_Y = 1;
    private const int SAMPLE_OFFSET_Z = -5;

    private const int MIN_CELL_SAMPLE_X = 0;
    private const int MIN_CELL_SAMPLE_Y = -1;
    private const int MIN_CELL_SAMPLE_Z = 0;

    private const int MAX_CELL_SAMPLE_X = 1;
    private const int MAX_CELL_SAMPLE_Y = 1;
    private const int MAX_CELL_SAMPLE_Z = 1;

    private const int MAX_REASONABLE_DISTANCE_TO_AQUIFER_CENTER = 11;
    private const int MAX_FLOODEDNESS_DEPTH = 64;
    private const int PRELIMINARY_SURFACE_LEVEL_OFFSET = 8;
    private const int FLUID_CELL_WIDTH = 16;
    private const int FLUID_CELL_HEIGHT = 40;
    private const int FLUID_TYPE_CELL_WIDTH = 64;
    private const int WAY_BELOW_MIN_Y = NoiseSettings.DimensionMinY << 4;

    private static readonly double FLOWING_UPDATE_SIMILARITY =
        Similarity(Mth.Square(10.0), Mth.Square(12.0));

    private static readonly (int X, int Z)[] SURFACE_SAMPLING_OFFSETS_IN_CHUNKS =
    [
        (0, 0),
        (-2, -1),
        (-1, -1),
        (0, -1),
        (1, -1),
        (-3, 0),
        (-2, 0),
        (-1, 0),
        (1, 0),
        (-2, 1),
        (-1, 1),
        (0, 1),
        (1, 1)
    ];

    private readonly DensitySampler.Bound _barrierNoise;
    private readonly DensitySampler.Bound _fluidLevelFloodednessNoise;
    private readonly DensitySampler.Bound _fluidLevelSpreadNoise;
    private readonly DensitySampler.Bound _lavaNoise;
    private readonly DensitySampler.Bound _exclusion;
    private readonly DensitySampler.Bound _surfaceLevel;

    private readonly PositionalRandomFactory _positionalRandomFactory;
    private readonly Aquifer.FluidPicker _globalFluidPicker;

    private readonly Aquifer.FluidStatus?[] _aquiferCache;
    private readonly long[] _aquiferLocationCache;
    private readonly Dictionary<long, int> _surfaceLevelCache = new();

    private readonly int _skipSamplingAboveY;
    private readonly int _minGridX;
    private readonly int _minGridY;
    private readonly int _minGridZ;
    private readonly int _gridSizeX;
    private readonly int _gridSizeY;
    private readonly int _gridSizeZ;

    private bool _shouldScheduleFluidUpdate;

    public NoiseBasedAquifer(
        DensitySamplerSet cachingSamplers,
        Aquifer.Config config,
        PositionalRandomFactory positionalRandomFactory,
        DensityVolume volume,
        Aquifer.FluidPicker globalFluidPicker)
    {
        ArgumentNullException.ThrowIfNull(cachingSamplers);
        ArgumentNullException.ThrowIfNull(config);
        _positionalRandomFactory = positionalRandomFactory ??
            throw new ArgumentNullException(nameof(positionalRandomFactory));
        ArgumentNullException.ThrowIfNull(volume);
        _globalFluidPicker = globalFluidPicker ??
            throw new ArgumentNullException(nameof(globalFluidPicker));

        _barrierNoise = cachingSamplers.Get(config.BarrierNoise);
        _fluidLevelFloodednessNoise = cachingSamplers.Get(config.FluidLevelFloodednessNoise);
        _fluidLevelSpreadNoise = cachingSamplers.Get(config.FluidLevelSpreadNoise);
        _lavaNoise = cachingSamplers.Get(config.LavaNoise);
        _exclusion = cachingSamplers.Get(config.Exclusion);
        _surfaceLevel = cachingSamplers.Get(config.SurfaceLevel);

        _minGridX = GridX(unchecked(volume.MinBlockX + SAMPLE_OFFSET_X));
        int maxGridX = unchecked(GridX(unchecked(volume.MaxBlockX() + SAMPLE_OFFSET_X)) + 1);
        _gridSizeX = unchecked(maxGridX - _minGridX + 1);

        _minGridY = unchecked(GridY(unchecked(volume.MinBlockY + SAMPLE_OFFSET_Y)) - 1);
        int maxGridY = unchecked(GridY(unchecked(volume.MaxBlockY() + SAMPLE_OFFSET_Y)) + 1);
        _gridSizeY = unchecked(maxGridY - _minGridY + 1);

        _minGridZ = GridZ(unchecked(volume.MinBlockZ + SAMPLE_OFFSET_Z));
        int maxGridZ = unchecked(GridZ(unchecked(volume.MaxBlockZ() + SAMPLE_OFFSET_Z)) + 1);
        _gridSizeZ = unchecked(maxGridZ - _minGridZ + 1);

        int totalGridSize = unchecked(_gridSizeX * _gridSizeY * _gridSizeZ);
        _aquiferCache = new Aquifer.FluidStatus?[totalGridSize];
        _aquiferLocationCache = new long[totalGridSize];
        Array.Fill(_aquiferLocationCache, long.MaxValue);

        int maxAdjustedSurfaceLevel = AdjustSurfaceLevel(
            MaxSurfaceLevel(
                FromGridX(_minGridX, 0),
                FromGridZ(_minGridZ, 0),
                FromGridX(maxGridX, 9),
                FromGridZ(maxGridZ, 9)));

        int skipSamplingAboveGridY =
            unchecked(GridY(unchecked(maxAdjustedSurfaceLevel + Y_SPACING)) + 1);
        _skipSamplingAboveY =
            unchecked(FromGridY(skipSamplingAboveGridY, 11) - 1);
    }

    public int SkipSamplingAboveY => _skipSamplingAboveY;

    public bool ShouldScheduleFluidUpdate() =>
        _shouldScheduleFluidUpdate;

    public BlockState? ComputeSubstance(
        int blockX,
        int blockY,
        int blockZ,
        double density)
    {
        if (density > 0.0)
        {
            _shouldScheduleFluidUpdate = false;
            return null;
        }

        Aquifer.FluidStatus globalFluid =
            _globalFluidPicker(blockX, blockY, blockZ);

        if (blockY > _skipSamplingAboveY)
        {
            _shouldScheduleFluidUpdate = false;
            return globalFluid.At(blockY);
        }

        if (globalFluid.At(blockY).Is(Blocks.LAVA))
        {
            _shouldScheduleFluidUpdate = false;
            return Blocks.LAVA.DefaultState;
        }

        int xAnchor = GridX(unchecked(blockX + SAMPLE_OFFSET_X));
        int yAnchor = GridY(unchecked(blockY + SAMPLE_OFFSET_Y));
        int zAnchor = GridZ(unchecked(blockZ + SAMPLE_OFFSET_Z));

        int distanceSquared1 = int.MaxValue;
        int distanceSquared2 = int.MaxValue;
        int distanceSquared3 = int.MaxValue;
        int distanceSquared4 = int.MaxValue;

        int closestIndex1 = 0;
        int closestIndex2 = 0;
        int closestIndex3 = 0;
        int closestIndex4 = 0;

        for (int xOffset = MIN_CELL_SAMPLE_X; xOffset <= MAX_CELL_SAMPLE_X; xOffset++)
        {
            for (int yOffset = MIN_CELL_SAMPLE_Y; yOffset <= MAX_CELL_SAMPLE_Y; yOffset++)
            {
                for (int zOffset = MIN_CELL_SAMPLE_Z; zOffset <= MAX_CELL_SAMPLE_Z; zOffset++)
                {
                    int spacedGridX = unchecked(xAnchor + xOffset);
                    int spacedGridY = unchecked(yAnchor + yOffset);
                    int spacedGridZ = unchecked(zAnchor + zOffset);
                    int index = GetIndex(spacedGridX, spacedGridY, spacedGridZ);

                    long existingLocation = _aquiferLocationCache[index];
                    long location;
                    if (existingLocation != long.MaxValue)
                    {
                        location = existingLocation;
                    }
                    else
                    {
                        IRandomSource random =
                            _positionalRandomFactory.At(spacedGridX, spacedGridY, spacedGridZ);
                        location = BlockPos.AsLong(
                            FromGridX(spacedGridX, random.NextInt(X_RANGE)),
                            FromGridY(spacedGridY, random.NextInt(Y_RANGE)),
                            FromGridZ(spacedGridZ, random.NextInt(Z_RANGE)));
                        _aquiferLocationCache[index] = location;
                    }

                    int dx = unchecked(BlockPos.GetX(location) - blockX);
                    int dy = unchecked(BlockPos.GetY(location) - blockY);
                    int dz = unchecked(BlockPos.GetZ(location) - blockZ);
                    int newDistance = unchecked(dx * dx + dy * dy + dz * dz);

                    if (distanceSquared1 >= newDistance)
                    {
                        closestIndex4 = closestIndex3;
                        closestIndex3 = closestIndex2;
                        closestIndex2 = closestIndex1;
                        closestIndex1 = index;

                        distanceSquared4 = distanceSquared3;
                        distanceSquared3 = distanceSquared2;
                        distanceSquared2 = distanceSquared1;
                        distanceSquared1 = newDistance;
                    }
                    else if (distanceSquared2 >= newDistance)
                    {
                        closestIndex4 = closestIndex3;
                        closestIndex3 = closestIndex2;
                        closestIndex2 = index;

                        distanceSquared4 = distanceSquared3;
                        distanceSquared3 = distanceSquared2;
                        distanceSquared2 = newDistance;
                    }
                    else if (distanceSquared3 >= newDistance)
                    {
                        closestIndex4 = closestIndex3;
                        closestIndex3 = index;

                        distanceSquared4 = distanceSquared3;
                        distanceSquared3 = newDistance;
                    }
                    else if (distanceSquared4 >= newDistance)
                    {
                        closestIndex4 = index;
                        distanceSquared4 = newDistance;
                    }
                }
            }
        }

        Aquifer.FluidStatus closestStatus1 = GetAquiferStatus(closestIndex1);
        double similarity12 = Similarity(distanceSquared1, distanceSquared2);
        BlockState fluidState = closestStatus1.At(blockY);
        BlockState actualFluidState = fluidState;

        if (similarity12 <= 0.0)
        {
            if (similarity12 >= FLOWING_UPDATE_SIMILARITY)
            {
                Aquifer.FluidStatus closestStatus2 = GetAquiferStatus(closestIndex2);
                _shouldScheduleFluidUpdate = closestStatus1 != closestStatus2;
            }
            else
            {
                _shouldScheduleFluidUpdate = false;
            }

            return actualFluidState;
        }

        if (fluidState.Is(Blocks.WATER) &&
            _globalFluidPicker(blockX, unchecked(blockY - 1), blockZ)
                .At(unchecked(blockY - 1))
                .Is(Blocks.LAVA))
        {
            _shouldScheduleFluidUpdate = true;
            return actualFluidState;
        }

        double barrierNoiseValue = double.NaN;
        Aquifer.FluidStatus closestStatus2ForPressure = GetAquiferStatus(closestIndex2);
        double barrier12 = similarity12 * CalculatePressure(
            blockX,
            blockY,
            blockZ,
            ref barrierNoiseValue,
            closestStatus1,
            closestStatus2ForPressure);

        if (density + barrier12 > 0.0)
        {
            _shouldScheduleFluidUpdate = false;
            return null;
        }

        Aquifer.FluidStatus closestStatus3 = GetAquiferStatus(closestIndex3);
        double similarity13 = Similarity(distanceSquared1, distanceSquared3);

        if (similarity13 > 0.0)
        {
            double barrier13 = similarity12 * similarity13 * CalculatePressure(
                blockX,
                blockY,
                blockZ,
                ref barrierNoiseValue,
                closestStatus1,
                closestStatus3);

            if (density + barrier13 > 0.0)
            {
                _shouldScheduleFluidUpdate = false;
                return null;
            }
        }

        double similarity23 = Similarity(distanceSquared2, distanceSquared3);
        if (similarity23 > 0.0)
        {
            double barrier23 = similarity12 * similarity23 * CalculatePressure(
                blockX,
                blockY,
                blockZ,
                ref barrierNoiseValue,
                closestStatus2ForPressure,
                closestStatus3);

            if (density + barrier23 > 0.0)
            {
                _shouldScheduleFluidUpdate = false;
                return null;
            }
        }

        bool mayFlow12 = closestStatus1 != closestStatus2ForPressure;
        bool mayFlow23 =
            similarity23 >= FLOWING_UPDATE_SIMILARITY &&
            closestStatus2ForPressure != closestStatus3;
        bool mayFlow13 =
            similarity13 >= FLOWING_UPDATE_SIMILARITY &&
            closestStatus1 != closestStatus3;

        if (!mayFlow12 && !mayFlow23 && !mayFlow13)
        {
            _shouldScheduleFluidUpdate =
                similarity13 >= FLOWING_UPDATE_SIMILARITY &&
                Similarity(distanceSquared1, distanceSquared4) >= FLOWING_UPDATE_SIMILARITY &&
                closestStatus1 != GetAquiferStatus(closestIndex4);
        }
        else
        {
            _shouldScheduleFluidUpdate = true;
        }

        return actualFluidState;
    }

    private int SurfaceLevel(int blockX, int blockZ)
    {
        int quantizedX = QuartPos.ToBlock(QuartPos.FromBlock(blockX));
        int quantizedZ = QuartPos.ToBlock(QuartPos.FromBlock(blockZ));
        long key = ChunkPos.Pack(quantizedX, quantizedZ);

        if (_surfaceLevelCache.TryGetValue(key, out int cached))
            return cached;

        int surface = Mth.Floor(_surfaceLevel.SampleValue(quantizedX, 0, quantizedZ));
        _surfaceLevelCache.Add(key, surface);
        return surface;
    }

    private int MaxSurfaceLevel(int minBlockX, int minBlockZ, int maxBlockX, int maxBlockZ)
    {
        int minQuartX = QuartPos.FromBlock(minBlockX);
        int maxQuartX = QuartPos.FromBlock(maxBlockX);
        int minQuartZ = QuartPos.FromBlock(minBlockZ);
        int maxQuartZ = QuartPos.FromBlock(maxBlockZ);

        var volume = new DensityVolume(
            maxQuartX - minQuartX + 1,
            1,
            maxQuartZ - minQuartZ + 1,
            QuartPos.ToBlock(minQuartX),
            0,
            QuartPos.ToBlock(minQuartZ),
            4,
            1,
            4);

        using ScopedDensityBuffer buffer = _surfaceLevel.SampleVolume(volume);
        int maxY = int.MinValue;

        for (int z = 0; z < volume.SizeZ; z++)
        {
            for (int x = 0; x < volume.SizeX; x++)
            {
                int surface = Mth.Floor(buffer.Get(volume.IndexUnchecked(x, 0, z)));
                _surfaceLevelCache[ChunkPos.Pack(volume.BlockX(x), volume.BlockZ(z))] = surface;
                if (surface > maxY)
                    maxY = surface;
            }
        }

        return maxY;
    }

    private int GetIndex(int gridX, int gridY, int gridZ)
    {
        int x = unchecked(gridX - _minGridX);
        int y = unchecked(gridY - _minGridY);
        int z = unchecked(gridZ - _minGridZ);
        return unchecked((y * _gridSizeZ + z) * _gridSizeX + x);
    }

    private Aquifer.FluidStatus GetAquiferStatus(int index)
    {
        Aquifer.FluidStatus? oldStatus = _aquiferCache[index];
        if (oldStatus.HasValue)
            return oldStatus.Value;

        long location = _aquiferLocationCache[index];
        Aquifer.FluidStatus status = ComputeFluid(
            BlockPos.GetX(location),
            BlockPos.GetY(location),
            BlockPos.GetZ(location));
        _aquiferCache[index] = status;
        return status;
    }

    private Aquifer.FluidStatus ComputeFluid(int x, int y, int z)
    {
        Aquifer.FluidStatus globalFluid = _globalFluidPicker(x, y, z);
        int lowestPreliminarySurface = int.MaxValue;
        int topOfAquiferCell = unchecked(y + Y_SPACING);
        int bottomOfAquiferCell = unchecked(y - Y_SPACING);
        bool surfaceAtCenterIsUnderGlobalFluidLevel = false;

        foreach ((int offsetX, int offsetZ) in SURFACE_SAMPLING_OFFSETS_IN_CHUNKS)
        {
            int sampleX = unchecked(x + SectionPos.SectionToBlockCoord(offsetX));
            int sampleZ = unchecked(z + SectionPos.SectionToBlockCoord(offsetZ));
            int surfaceLevel = SurfaceLevel(sampleX, sampleZ);
            int adjustedSurfaceLevel = AdjustSurfaceLevel(surfaceLevel);
            bool start = offsetX == 0 && offsetZ == 0;

            if (start && bottomOfAquiferCell > adjustedSurfaceLevel)
                return globalFluid;

            bool topOfAquiferCellPokesAboveSurface = topOfAquiferCell > adjustedSurfaceLevel;
            if (topOfAquiferCellPokesAboveSurface || start)
            {
                Aquifer.FluidStatus globalFluidAtSurface =
                    _globalFluidPicker(sampleX, adjustedSurfaceLevel, sampleZ);
                if (!globalFluidAtSurface.At(adjustedSurfaceLevel).IsAir)
                {
                    if (start)
                        surfaceAtCenterIsUnderGlobalFluidLevel = true;

                    if (topOfAquiferCellPokesAboveSurface)
                        return globalFluidAtSurface;
                }
            }

            lowestPreliminarySurface = Math.Min(lowestPreliminarySurface, surfaceLevel);
        }

        int fluidSurfaceLevel = ComputeSurfaceLevel(
            x,
            y,
            z,
            globalFluid,
            lowestPreliminarySurface,
            surfaceAtCenterIsUnderGlobalFluidLevel);

        return new Aquifer.FluidStatus(
            fluidSurfaceLevel,
            ComputeFluidType(x, y, z, globalFluid, fluidSurfaceLevel));
    }

    private static int AdjustSurfaceLevel(int preliminarySurfaceLevel) =>
        unchecked(preliminarySurfaceLevel + PRELIMINARY_SURFACE_LEVEL_OFFSET);

    private int ComputeSurfaceLevel(
        int x,
        int y,
        int z,
        Aquifer.FluidStatus globalFluid,
        int lowestSurfaceLevel,
        bool surfaceAtCenterIsUnderGlobalFluidLevel)
    {
        double partiallyFloodedness;
        double fullyFloodedness;

        if (_exclusion.SampleValue(x, y, z) > 0.0)
        {
            partiallyFloodedness = -1.0;
            fullyFloodedness = -1.0;
        }
        else
        {
            int distanceBelowSurface =
                unchecked(AdjustSurfaceLevel(lowestSurfaceLevel) - y);
            double floodednessFactor = surfaceAtCenterIsUnderGlobalFluidLevel
                ? Mth.ClampedMap(distanceBelowSurface, 0.0, MAX_FLOODEDNESS_DEPTH, 1.0, 0.0)
                : 0.0;
            double floodednessNoiseValue = Mth.Clamp(
                _fluidLevelFloodednessNoise.SampleValue(x, y, z),
                -1.0,
                1.0);
            double fullyFloodedThreshold =
                Mth.Map(floodednessFactor, 1.0, 0.0, -0.3, 0.8);
            double partiallyFloodedThreshold =
                Mth.Map(floodednessFactor, 1.0, 0.0, -0.8, 0.4);

            partiallyFloodedness = floodednessNoiseValue - partiallyFloodedThreshold;
            fullyFloodedness = floodednessNoiseValue - fullyFloodedThreshold;
        }

        int fluidSurfaceLevel;
        if (fullyFloodedness > 0.0)
        {
            fluidSurfaceLevel = globalFluid.FluidLevel;
        }
        else if (partiallyFloodedness > 0.0)
        {
            fluidSurfaceLevel = ComputeRandomizedFluidSurfaceLevel(
                x,
                y,
                z,
                lowestSurfaceLevel);
        }
        else
        {
            fluidSurfaceLevel = WAY_BELOW_MIN_Y;
        }

        return fluidSurfaceLevel;
    }

    private int ComputeRandomizedFluidSurfaceLevel(
        int x,
        int y,
        int z,
        int lowestSurfaceLevel)
    {
        int fluidLevelCellX = Mth.FloorDiv(x, FLUID_CELL_WIDTH);
        int fluidLevelCellY = Mth.FloorDiv(y, FLUID_CELL_HEIGHT);
        int fluidLevelCellZ = Mth.FloorDiv(z, FLUID_CELL_WIDTH);
        int fluidCellMiddleY = unchecked(fluidLevelCellY * FLUID_CELL_HEIGHT + FLUID_CELL_HEIGHT / 2);

        double fluidLevelSpread =
            _fluidLevelSpreadNoise.SampleValue(
                fluidLevelCellX,
                fluidLevelCellY,
                fluidLevelCellZ) * 10.0f;
        int fluidLevelSpreadQuantized = Mth.Quantize(fluidLevelSpread, 3);
        int targetFluidSurfaceLevel = unchecked(fluidCellMiddleY + fluidLevelSpreadQuantized);

        return Math.Min(lowestSurfaceLevel, targetFluidSurfaceLevel);
    }

    private BlockState ComputeFluidType(
        int x,
        int y,
        int z,
        Aquifer.FluidStatus globalFluid,
        int fluidSurfaceLevel)
    {
        BlockState fluidType = globalFluid.FluidType;
        if (fluidSurfaceLevel <= -10 &&
            fluidSurfaceLevel != WAY_BELOW_MIN_Y &&
            !ReferenceEquals(globalFluid.FluidType, Blocks.LAVA.DefaultState))
        {
            int fluidTypeCellX = Mth.FloorDiv(x, FLUID_TYPE_CELL_WIDTH);
            int fluidTypeCellY = Mth.FloorDiv(y, FLUID_CELL_HEIGHT);
            int fluidTypeCellZ = Mth.FloorDiv(z, FLUID_TYPE_CELL_WIDTH);
            double lavaNoiseValue = _lavaNoise.SampleValue(
                fluidTypeCellX,
                fluidTypeCellY,
                fluidTypeCellZ);

            if (Math.Abs(lavaNoiseValue) > 0.3)
                fluidType = Blocks.LAVA.DefaultState;
        }

        return fluidType;
    }

    private double CalculatePressure(
        int blockX,
        int blockY,
        int blockZ,
        ref double barrierNoiseValue,
        Aquifer.FluidStatus statusClosest1,
        Aquifer.FluidStatus statusClosest2)
    {
        BlockState type1 = statusClosest1.At(blockY);
        BlockState type2 = statusClosest2.At(blockY);

        bool waterLavaPair =
            (type1.Is(Blocks.LAVA) && type2.Is(Blocks.WATER)) ||
            (type1.Is(Blocks.WATER) && type2.Is(Blocks.LAVA));
        if (!waterLavaPair)
        {
            int fluidYDifference = Math.Abs(
                unchecked(statusClosest1.FluidLevel - statusClosest2.FluidLevel));
            if (fluidYDifference == 0)
                return 0.0;

            double averageFluidY =
                0.5 * (statusClosest1.FluidLevel + statusClosest2.FluidLevel);
            double howFarAboveAverageFluidPoint = blockY + 0.5 - averageFluidY;
            double baseValue = fluidYDifference / 2.0;
            double distanceFromBarrierEdgeTowardsMiddle =
                baseValue - Math.Abs(howFarAboveAverageFluidPoint);

            double gradient;
            if (howFarAboveAverageFluidPoint > 0.0)
            {
                double centerPoint = distanceFromBarrierEdgeTowardsMiddle;
                gradient = centerPoint > 0.0
                    ? centerPoint / 1.5
                    : centerPoint / 2.5;
            }
            else
            {
                double centerPoint = 3.0 + distanceFromBarrierEdgeTowardsMiddle;
                gradient = centerPoint > 0.0
                    ? centerPoint / 3.0
                    : centerPoint / 10.0;
            }

            double noiseValue;
            if (gradient >= -2.0 && gradient <= 2.0)
            {
                if (double.IsNaN(barrierNoiseValue))
                    barrierNoiseValue = _barrierNoise.SampleValue(blockX, blockY, blockZ);

                noiseValue = barrierNoiseValue;
            }
            else
            {
                noiseValue = 0.0;
            }

            return 2.0 * (noiseValue + gradient);
        }

        return 2.0;
    }

    private static double Similarity(int distanceSquared1, int distanceSquared2) =>
        1.0 - (distanceSquared2 - distanceSquared1) / 25.0;

    private static int GridX(int blockCoordinate) =>
        blockCoordinate >> 4;

    private static int FromGridX(int gridCoordinate, int blockOffset) =>
        unchecked((gridCoordinate << 4) + blockOffset);

    private static int GridY(int blockCoordinate) =>
        Mth.FloorDiv(blockCoordinate, Y_SPACING);

    private static int FromGridY(int gridCoordinate, int blockOffset) =>
        unchecked(gridCoordinate * Y_SPACING + blockOffset);

    private static int GridZ(int blockCoordinate) =>
        blockCoordinate >> 4;

    private static int FromGridZ(int gridCoordinate, int blockOffset) =>
        unchecked((gridCoordinate << 4) + blockOffset);
}
