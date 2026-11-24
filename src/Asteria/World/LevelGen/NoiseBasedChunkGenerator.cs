using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Materials.Rules;

namespace Asteria.World.LevelGen;

/// <summary>
/// Minecraft Java 26.3 density, aquifer, generic material-rule and carver stages for a ProtoChunk.
/// Biome-specific surface extensions and placed-feature generation remain later pipeline work.
/// </summary>
public sealed class NoiseBasedChunkGenerator
{
    private const int LAVA_LEVEL = -54;

    private readonly NoiseGeneratorSettings _settings;
    private readonly RandomState _randomState;
    private readonly Aquifer.FluidPicker _globalFluidPicker;
    private readonly MultiNoiseBiomeSource? _overworldBiomeSource;
    private readonly Lazy<IReadOnlyList<FeatureSorter.StepFeatureData>>? _featuresPerStep;

    public NoiseGeneratorSettings Settings => _settings;

    public int GetMinY() => _settings.NoiseSettings.MinY;

    public int GetGenDepth() => _settings.NoiseSettings.Height;

    public int GetSeaLevel() => _settings.SeaLevel;

    public long Seed => _randomState.Seed;

    public NoiseBasedChunkGenerator(
        NoiseGeneratorSettings settings,
        RandomState randomState,
        Aquifer.FluidPicker? globalFluidPicker = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _randomState = randomState ?? throw new ArgumentNullException(nameof(randomState));
        _globalFluidPicker = globalFluidPicker ?? CreateFluidPicker(settings);
    }

    private NoiseBasedChunkGenerator(
        NoiseGeneratorSettings settings,
        RandomState randomState,
        MultiNoiseBiomeSource overworldBiomeSource)
        : this(settings, randomState)
    {
        _overworldBiomeSource = overworldBiomeSource ??
            throw new ArgumentNullException(nameof(overworldBiomeSource));
        _featuresPerStep = new Lazy<IReadOnlyList<FeatureSorter.StepFeatureData>>(
            () => FeatureSorter.BuildFeaturesPerStep(
                _overworldBiomeSource.PossibleBiomes.ToArray(),
                static biome => biome.Value.GenerationSettings.Features,
                tryReducingError: true),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Creates a seeded generator from the registered Minecraft 26.3 Overworld
    /// noise settings, noise registry and complete vanilla Overworld biome source.
    /// </summary>
    public static NoiseBasedChunkGenerator CreateOverworld(long seed)
    {
        ResourceLocation settingsLocation =
            ResourceLocation.FromNamespaceAndPath("minecraft", "overworld");
        RegistryKey<NoiseGeneratorSettings> settingsKey =
            RegistryKey<NoiseGeneratorSettings>.Create(
                BuiltInRegistries.NoiseGeneratorSettings.Key,
                settingsLocation);
        NoiseGeneratorSettings settings =
            BuiltInRegistries.NoiseGeneratorSettings.GetValueOrThrow(settingsKey);

        MultiNoiseBiomeSource biomeSource =
            MultiNoiseBiomeSource.CreateVanillaOverworld(BuiltInRegistries.Biome);
        RandomState randomState = RandomState.Create(
            BuiltInRegistries.Noise,
            seed,
            settings.UseLegacyRandomSource,
            settings.NoiseRouter);

        var generator = new NoiseBasedChunkGenerator(settings, randomState, biomeSource);
        _ = generator.GetFeaturesPerStep();
        return generator;
    }

    /// <summary>
    /// Returns the validated, memoized PlacedFeature order for each decoration step.
    /// CreateOverworld evaluates this before returning the generator so feature-order
    /// cycles fail during world setup rather than during chunk generation.
    /// </summary>
    public IReadOnlyList<FeatureSorter.StepFeatureData> GetFeaturesPerStep() =>
        _featuresPerStep?.Value ??
        throw new InvalidOperationException(
            "Feature order validation requires the configured Overworld biome source.");

    /// <summary>
    /// Creates the uncached biome resolver used by WorldGenRegion. Unlike chunk biome
    /// lookup, this samples the configured climate source at any quart coordinate.
    /// </summary>
    public BiomeResolver CreateUncachedBiomeResolver()
    {
        MultiNoiseBiomeSource biomeSource = _overworldBiomeSource ??
            throw new InvalidOperationException(
                "An uncached biome resolver requires the configured Overworld biome source.");
        return biomeSource.CreateResolver(
            _randomState.CreateClimateSampler(SamplerContext.EmptyUncached));
    }

    /// <summary>
    /// Builds this chunk with the vanilla Overworld biome source configured by CreateOverworld.
    /// Custom generators must continue to supply their own biome source explicitly.
    /// </summary>
    public void BuildTerrain(ProtoChunk chunk)
    {
        MultiNoiseBiomeSource biomeSource = _overworldBiomeSource ??
            throw new InvalidOperationException(
                "This chunk generator has no configured Overworld biome source. Supply one to BuildTerrain.");
        BuildTerrain(chunk, biomeSource);
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
    /// Runs density filling, multi-noise biome selection and material rules using one shared NoiseChunk.
    /// </summary>
    public void BuildTerrain(
        ProtoChunk chunk,
        MultiNoiseBiomeSource biomeSource,
        IReadOnlySet<Holder<Biome>>? possibleBiomes = null,
        Beardifier? beardifier = null,
        IChunkBlender? blender = null)
    {
        ArgumentNullException.ThrowIfNull(biomeSource);

        BuildTerrainCore(
            chunk,
            densitySamplers => biomeSource.CreateResolver(
                _settings.NoiseRouter.CreateClimateSampler(densitySamplers)),
            () => biomeSource.CreateResolver(
                _randomState.CreateClimateSampler(SamplerContext.EmptyUncached)),
            possibleBiomes ?? biomeSource.PossibleBiomes,
            beardifier,
            blender);
    }

    /// <summary>
    /// Runs the terrain pipeline with a caller-supplied biome lookup. This overload is useful
    /// for fixed/custom biome sources; the callback is adapted to the same quart-resolution
    /// resolver used by the chunk biome palette and material conditions.
    /// </summary>
    public void BuildTerrain(
        ProtoChunk chunk,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        IReadOnlySet<Holder<Biome>>? possibleBiomes = null,
        Beardifier? beardifier = null,
        IChunkBlender? blender = null)
    {
        ArgumentNullException.ThrowIfNull(biomeGetter);

        BuildTerrainCore(
            chunk,
            _ => new DelegateBiomeResolver((quartX, quartY, quartZ) =>
                biomeGetter(new BlockPos(
                    QuartPos.ToBlock(quartX),
                    QuartPos.ToBlock(quartY),
                    QuartPos.ToBlock(quartZ)))),
            () => new DelegateBiomeResolver((quartX, quartY, quartZ) =>
                biomeGetter(new BlockPos(
                    QuartPos.ToBlock(quartX),
                    QuartPos.ToBlock(quartY),
                    QuartPos.ToBlock(quartZ)))),
            possibleBiomes,
            beardifier,
            blender);
    }

    private void BuildTerrainCore(
        ProtoChunk chunk,
        Func<DensitySamplerSet, BiomeResolver> biomeResolverFactory,
        Func<BiomeResolver> carverBiomeResolverFactory,
        IReadOnlySet<Holder<Biome>>? possibleBiomes,
        Beardifier? beardifier,
        IChunkBlender? blender)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(biomeResolverFactory);
        ArgumentNullException.ThrowIfNull(carverBiomeResolverFactory);

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

        BiomeResolver biomeResolver = biomeResolverFactory(noiseChunk.CachingSamplers);
        chunk.FillBiomesFromNoise(biomeResolver);
        chunk.SetPersistedStatus(ChunkStatus.BIOMES);

        FillFromNoise(chunk, noiseChunk, volume);
        Heightmap.PrimeHeightmaps(chunk, ChunkStatus.TERRAIN.GetHeightmapsAfter().ToHashSet());

        MaterialSystem materialSystem = _randomState.GetOrCreateMaterialSystem(_settings);
        var generationContext = new WorldGenerationContext(this, chunk);
        Func<BlockPos, Holder<Biome>> biomeGetter = position =>
            biomeResolver.GetNoiseBiome(
                QuartPos.FromBlock(position.GetX()),
                QuartPos.FromBlock(position.GetY()),
                QuartPos.FromBlock(position.GetZ()));

        materialSystem.BuildSurface(
            _randomState,
            generationContext,
            chunk,
            noiseChunk,
            _settings.MaterialRule.Value,
            biomeGetter,
            possibleBiomes);

        BiomeResolver carverBiomeResolver = carverBiomeResolverFactory();
        Func<BlockPos, Holder<Biome>> carverBiomeGetter = position =>
            carverBiomeResolver.GetNoiseBiome(
                QuartPos.FromBlock(position.GetX()),
                QuartPos.FromBlock(position.GetY()),
                QuartPos.FromBlock(position.GetZ()));
        ApplyCarvers(
            chunk,
            noiseChunk,
            generationContext,
            carverBiomeResolver,
            carverBiomeGetter,
            materialSystem);
        chunk.SetPersistedStatus(ChunkStatus.TERRAIN);
    }

    /// <summary>
    /// Runs the Minecraft 26.3 carver pass over the 17×17 source-chunk region,
    /// then applies the shared mask through the chunk's aquifer.
    /// </summary>
    private void ApplyCarvers(
        ProtoChunk chunk,
        NoiseChunk noiseChunk,
        WorldGenerationContext generationContext,
        BiomeResolver biomeResolver,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        MaterialSystem materialSystem)
    {
        int maskMinY = unchecked(generationContext.GetMinGenY() + 1);
        int maskMaxY = unchecked(
            generationContext.GetMinGenY() +
            generationContext.GetGenDepth() - 1 - 7);
        if (maskMaxY < maskMinY)
            return;

        var carvingMask = new CarvingMask(maskMinY, maskMaxY);
        ChunkPos targetChunk = chunk.GetPos();
        // NoiseBasedChunkGenerator.generateCarvers uses a legacy WorldgenRandom
        // for large-feature seeding in 26.3, regardless of the density noise algorithm.
        var random = new WorldgenRandom(new LegacyRandomSource(0L));

        for (int offsetX = -8; offsetX <= 8; offsetX++)
        {
            for (int offsetZ = -8; offsetZ <= 8; offsetZ++)
            {
                var sourceChunk = new ChunkPos(
                    unchecked(targetChunk.X + offsetX),
                    unchecked(targetChunk.Z + offsetZ));

                Holder<Biome> sourceBiome = biomeResolver.GetNoiseBiome(
                    QuartPos.FromBlock(sourceChunk.GetMinBlockX()),
                    quartY: 0,
                    QuartPos.FromBlock(sourceChunk.GetMinBlockZ()));

                int carverIndex = 0;
                foreach (Holder<WorldCarver> carverHolder in sourceBiome.Value.GenerationSettings.GetCarvers())
                {
                    WorldCarver carver = carverHolder.Value;
                    random.SetLargeFeatureSeed(
                        unchecked(_randomState.Seed + carverIndex),
                        sourceChunk.X,
                        sourceChunk.Z);

                    if (carver.IsStartChunk(random))
                    {
                        carver.Carve(
                            generationContext,
                            random,
                            targetChunk,
                            sourceChunk,
                            carvingMask);
                    }

                    carverIndex++;
                }
            }
        }

        if (!carvingMask.IsEmpty())
        {
            ApplyCarvingMask(
                chunk,
                noiseChunk,
                generationContext,
                biomeGetter,
                materialSystem,
                carvingMask);
        }
    }

    private void ApplyCarvingMask(
        ProtoChunk chunk,
        NoiseChunk noiseChunk,
        WorldGenerationContext generationContext,
        Func<BlockPos, Holder<Biome>> biomeGetter,
        MaterialSystem materialSystem,
        CarvingMask carvingMask)
    {
        ChunkPos targetChunk = chunk.GetPos();
        var position = new BlockPos.MutableBlockPos();

        carvingMask.Visit((localX, localZ, bottomY, topY) =>
        {
            int worldX = targetChunk.GetBlockX(localX);
            int worldZ = targetChunk.GetBlockZ(localZ);
            position.SetX(worldX);
            position.SetZ(worldZ);
            bool hasGrass = false;

            for (int worldY = topY; worldY >= bottomY; worldY--)
            {
                position.SetY(worldY);
                BlockState existingState = chunk.GetBlockState(position);
                if (existingState.Is(BlockTags.Uncarvable))
                    continue;

                if (existingState.Is(Blocks.GRASS_BLOCK) ||
                    existingState.Is(Blocks.MYCELIUM))
                {
                    hasGrass = true;
                }

                BlockState? carvedState = noiseChunk.Aquifer.ComputeSubstance(
                    worldX,
                    worldY,
                    worldZ,
                    density: 0.0);
                if (carvedState is null)
                    continue;

                chunk.SetBlockState(position, carvedState, flags: 0);
                if (noiseChunk.Aquifer.ShouldScheduleFluidUpdate() &&
                    !carvedState.GetFluidState().IsEmpty())
                {
                    chunk.MarkPosForPostProcessing(position);
                }

                if (!hasGrass)
                    continue;

                position.SetY(worldY - 1);
                BlockState belowState = chunk.GetBlockState(position);
                if (belowState.Is(Blocks.DIRT))
                {
                    BlockState? topMaterial = materialSystem.TopMaterial(
                        _settings.MaterialRule.Value,
                        _randomState,
                        generationContext,
                        biomeGetter,
                        chunk,
                        noiseChunk.CachingSamplers,
                        position,
                        underFluid: !carvedState.GetFluidState().IsEmpty());

                    if (topMaterial is not null)
                    {
                        chunk.SetBlockState(position, topMaterial, flags: 0);
                        if (!topMaterial.GetFluidState().IsEmpty())
                            chunk.MarkPosForPostProcessing(position);
                    }
                }

                position.SetY(worldY);
            }
        });
    }

    private sealed class DelegateBiomeResolver(
        Func<int, int, int, Holder<Biome>> resolver) : BiomeResolver
    {
        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ) =>
            resolver(quartX, quartY, quartZ);
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
