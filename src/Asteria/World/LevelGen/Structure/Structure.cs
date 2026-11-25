using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Content.Identifiers;
using Asteria.Serialization;
using Asteria.Core;
using Asteria.World.LevelGen.Structure.Pieces;
using Asteria.World;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Shared definition settings and bounding-box adjustment for a structure.
/// Generation-point selection and placement are separate operations that require
/// the structure manager and template systems not yet ported into Asteria.
/// </summary>
public abstract class Structure
{
    /// <summary>
    /// Dispatches structure data through registered structure-type map codecs.
    /// Types without a registered codec remain unavailable for serialization.
    /// </summary>
    public static MapCodec<Structure> DIRECT_CODEC { get; } =
        MapCodec<Structure>.Dispatch(
            Codecs.STRING.FieldOf("type"),
            static structure =>
            {
                ArgumentNullException.ThrowIfNull(structure);

                MapCodec<Structure> codec;
                try
                {
                    codec = structure.Codec();
                }
                catch (NotSupportedException exception)
                {
                    return DataResult<string>.Error(() => exception.Message);
                }

                RegistryKey<MapCodec<Structure>>? key =
                    BuiltInRegistries.StructureType.GetResourceKey(codec);

                return key.HasValue
                    ? DataResult<string>.Success(key.Value.Location.ToString())
                    : DataResult<string>.Error(() =>
                        $"Unregistered structure type codec for {structure.GetType().Name}");
            },
            static type =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(type);
                if (!location.HasValue)
                {
                    return DataResult<MapCodec<Structure>>.Error(() =>
                        $"Invalid structure type '{type}'");
                }

                MapCodec<Structure>? codec =
                    BuiltInRegistries.StructureType.GetValue(location.Value);

                return codec is not null
                    ? DataResult<MapCodec<Structure>>.Success(codec)
                    : DataResult<MapCodec<Structure>>.Error(() =>
                        $"Unknown structure type '{type}'");
            });

    /// <summary>
    /// Registry-aware holder codec, supporting registered structure references and
    /// top-level inline definitions through RegistryFileCodec.
    /// </summary>
    public static Codec<Holder<Structure>> CODEC { get; } =
        RegistryCodecs.Holder<Structure>(
            BuiltInRegistries.Structure.Key,
            DIRECT_CODEC.Codec());

    protected readonly StructureSettings _settings;

    protected Structure(StructureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public StructureSettings Settings => _settings;

    /// <summary>
    /// Returns the subtype's registered map codec. Concrete structure types must
    /// override this member; the base implementation deliberately fails closed.
    /// </summary>
    public virtual MapCodec<Structure> Codec() =>
        throw new NotSupportedException(
            $"No structure type codec has been ported for {GetType().Name}.");

    /// <summary>
    /// Creates the common one-field settings codec used by simple structure types.
    /// </summary>
    public static MapCodec<TStructure> SimpleCodec<TStructure>(
        Func<StructureSettings, TStructure> constructor)
        where TStructure : Structure
    {
        ArgumentNullException.ThrowIfNull(constructor);
        return StructureSettings.CODEC.XMap(
            constructor,
            static structure => structure.Settings);
    }

    /// <summary>
    /// Adapts a subtype codec for the base Structure type used by the type registry.
    /// Concrete structure types should retain the returned codec as a static singleton.
    /// </summary>
    public static MapCodec<Structure> AdaptCodec<TStructure>(
        MapCodec<TStructure> codec)
        where TStructure : Structure
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.XMap<Structure>(
            static structure => structure,
            static structure => structure as TStructure ??
                throw new InvalidOperationException(
                    $"Expected {typeof(TStructure).Name}, received {structure.GetType().Name}."));
    }

    public HolderSet<Biome> Biomes() =>
        _settings.Biomes;

    public GenerationStep.Decoration Step() =>
        _settings.Step;

    public TerrainAdjustment TerrainAdaptation() =>
        _settings.TerrainAdaptation;

    /// <summary>
    /// Inflates structure bounds by twelve blocks on all axes whenever terrain
    /// adaptation is not NONE, matching Minecraft Java 26.3 Structure.adjustBoundingBox.
    /// </summary>
    public BoundingBox AdjustBoundingBox(BoundingBox boundingBox)
    {
        ArgumentNullException.ThrowIfNull(boundingBox);

        return ReferenceEquals(TerrainAdaptation(), TerrainAdjustment.NONE)
            ? boundingBox
            : boundingBox.InflatedBy(12);
    }

    /// <summary>
    /// Determines whether this structure can start in the supplied chunk and, if so,
    /// returns the generation stub.
    /// </summary>
    protected abstract GenerationStub? FindGenerationPoint(GenerationContext context);

    /// <summary>
    /// Applies the generation-context biome predicate to the stub returned by the
    /// concrete structure, matching Minecraft Java 26.3 findValidGenerationPoint.
    /// </summary>
    public GenerationStub? FindValidGenerationPoint(GenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        GenerationStub? stub = FindGenerationPoint(context);
        return stub is not null && context.IsValidBiome(stub) ? stub : null;
    }

    /// <summary>
    /// Executes this structure's deferred generation stub and creates its in-memory start.
    /// Registry access, dimension/template services and piece serialization are supplied
    /// by later world-generation stages; this overload ports the shared 26.3 core.
    /// </summary>
    public StructureStart Generate(GenerationContext context, int references)
    {
        ArgumentNullException.ThrowIfNull(context);

        GenerationStub? generation = FindValidGenerationPoint(context);
        if (generation is null)
            return StructureStart.INVALID_START;

        StructurePiecesBuilder builder = generation.GetPiecesBuilder();
        var start = new StructureStart(this, context.ChunkPos, references, builder.Build());
        return start.IsValid() ? start : StructureStart.INVALID_START;
    }
    /// <summary>
    /// Creates a center-column start only when a valid structure biome can exist there.
    /// </summary>
    protected static GenerationStub? OnTopOfChunkCenter(
        GenerationContext context,
        Heightmap.Types heightmapType,
        Action<StructurePiecesBuilder> pieceGenerator)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(heightmapType);
        ArgumentNullException.ThrowIfNull(pieceGenerator);

        return context.CouldValidBiomeExistOnTopOfChunkCenter()
            ? OnTopOfChunkCenterWithoutBiomeCheck(context, heightmapType, pieceGenerator)
            : null;
    }

    /// <summary>
    /// Creates a center-column start at the first occupied height without checking biomes.
    /// The caller is responsible for validating the candidate column beforehand.
    /// </summary>
    protected static GenerationStub OnTopOfChunkCenterWithoutBiomeCheck(
        GenerationContext context,
        Heightmap.Types heightmapType,
        Action<StructurePiecesBuilder> pieceGenerator)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(heightmapType);
        ArgumentNullException.ThrowIfNull(pieceGenerator);

        ChunkPos chunkPosition = context.ChunkPos;
        int blockX = chunkPosition.GetMiddleBlockX();
        int blockZ = chunkPosition.GetMiddleBlockZ();
        int blockY = context.Generator.GetFirstOccupiedHeight(
            blockX,
            blockZ,
            heightmapType,
            context.HeightAccessor,
            context.RandomState);

        return new GenerationStub(
            new BlockPos(blockX, blockY, blockZ),
            pieceGenerator);
    }
    /// <summary>
    /// Position where the structure starts and the deferred/prebuilt piece builder.
    /// A deferred generator creates a fresh builder on every GetPiecesBuilder call;
    /// a prebuilt builder is returned by identity, matching Minecraft Java 26.3.
    /// </summary>
    /// <summary>
    /// Seeded biome-validation context for one structure generation attempt.
    /// Template management and registry access are omitted until their runtime services
    /// are ported; the available biome and chunk-height checks retain the 26.3 coordinates.
    /// </summary>
    public sealed class GenerationContext
    {
        public NoiseBasedChunkGenerator Generator { get; }
        public BiomeResolver BiomeResolver { get; }
        public RandomState RandomState { get; }
        public WorldgenRandom Random { get; }
        public long Seed { get; }
        public ChunkPos ChunkPos { get; }
        public LevelHeightAccessor HeightAccessor { get; }
        public Predicate<Holder<Biome>> ValidBiome { get; }

        public GenerationContext(
            NoiseBasedChunkGenerator generator,
            BiomeResolver biomeResolver,
            RandomState randomState,
            long seed,
            ChunkPos chunkPos,
            LevelHeightAccessor heightAccessor,
            Predicate<Holder<Biome>> validBiome)
        {
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
            BiomeResolver = biomeResolver ?? throw new ArgumentNullException(nameof(biomeResolver));
            RandomState = randomState ?? throw new ArgumentNullException(nameof(randomState));
            ChunkPos = chunkPos;
            HeightAccessor = heightAccessor ?? throw new ArgumentNullException(nameof(heightAccessor));
            ValidBiome = validBiome ?? throw new ArgumentNullException(nameof(validBiome));
            Seed = seed;
            Random = MakeRandom(seed, chunkPos);
        }

        /// <summary>
        /// Matches Structure.GenerationContext.isValidBiome: sample the stub's block
        /// position at quart resolution, then apply the configured biome predicate.
        /// </summary>
        public bool IsValidBiome(GenerationStub stub)
        {
            ArgumentNullException.ThrowIfNull(stub);
            BlockPos position = stub.Position;
            Holder<Biome> biome = BiomeResolver.GetNoiseBiome(
                QuartPos.FromBlock(position.GetX()),
                QuartPos.FromBlock(position.GetY()),
                QuartPos.FromBlock(position.GetZ()));
            return ValidBiome(biome);
        }

        /// <summary>
        /// Tests every quart-Y coordinate in the inclusive block-height interval,
        /// using the source's block-to-quart truncation and preserving the first match.
        /// </summary>
        public bool CouldStructureExistInColumn(
            int blockX,
            int blockZ,
            int minBlockY,
            int maxBlockY)
        {
            int quartX = QuartPos.FromBlock(blockX);
            int quartZ = QuartPos.FromBlock(blockZ);
            int minQuartY = QuartPos.FromBlock(minBlockY);
            int maxQuartY = QuartPos.FromBlock(maxBlockY);

            for (int quartY = minQuartY; quartY <= maxQuartY; quartY++)
            {
                Holder<Biome> biome =
                    BiomeResolver.GetNoiseBiome(quartX, quartY, quartZ);
                if (ValidBiome(biome))
                    return true;

                if (quartY == int.MaxValue)
                    break;
            }

            return false;
        }

        /// <summary>
        /// Tests the entire terrain column from one block below the build minimum
        /// through the inclusive maximum build height.
        /// </summary>
        public bool CouldValidBiomeExistInTerrainColumn(int blockX, int blockZ) =>
            CouldStructureExistInColumn(
                blockX,
                blockZ,
                unchecked(HeightAccessor.GetMinY() - 1),
                HeightAccessor.GetMaxY());

        /// <summary>
        /// Checks the complete terrain column at the chunk center for any biome
        /// accepted by this generation context, matching Minecraft Java 26.3.
        /// </summary>
        public bool CouldValidBiomeExistOnTopOfChunkCenter() =>
            CouldValidBiomeExistInTerrainColumn(
                ChunkPos.GetMiddleBlockX(),
                ChunkPos.GetMiddleBlockZ());

        private static WorldgenRandom MakeRandom(long seed, ChunkPos chunkPos)
        {
            var random = new WorldgenRandom(new LegacyRandomSource(0L));
            random.SetLargeFeatureSeed(seed, chunkPos.X, chunkPos.Z);
            return random;
        }
    }

    public sealed class GenerationStub
    {
        private readonly Action<StructurePiecesBuilder>? _pieceGenerator;
        private readonly StructurePiecesBuilder? _prebuiltBuilder;

        public BlockPos Position { get; }

        public GenerationStub(
            BlockPos position,
            Action<StructurePiecesBuilder> pieceGenerator)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            _pieceGenerator = pieceGenerator ??
                throw new ArgumentNullException(nameof(pieceGenerator));
        }

        public GenerationStub(
            BlockPos position,
            StructurePiecesBuilder prebuiltBuilder)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            _prebuiltBuilder = prebuiltBuilder ??
                throw new ArgumentNullException(nameof(prebuiltBuilder));
        }

        public StructurePiecesBuilder GetPiecesBuilder()
        {
            if (_prebuiltBuilder is not null)
                return _prebuiltBuilder;

            var builder = new StructurePiecesBuilder();
            _pieceGenerator!(builder);
            return builder;
        }
    }
}
