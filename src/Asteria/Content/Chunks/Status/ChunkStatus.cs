using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World;

namespace Asteria.Content.Chunks.Status;

/// <summary>
/// A registered stage in the Minecraft Java 26.3 chunk lifecycle.
/// The stage is metadata only; generation work and dependency radii belong to ChunkStep/ChunkPyramid.
/// </summary>
public class ChunkStatus
{
    public const int MAX_STRUCTURE_DISTANCE = 8;

    private static readonly ISet<Heightmap.Types> WORLDGEN_HEIGHTMAPS = new HashSet<Heightmap.Types>
    {
        Heightmap.Types.OCEAN_FLOOR_WG,
        Heightmap.Types.WORLD_SURFACE_WG
    };

    public static readonly ISet<Heightmap.Types> FINAL_HEIGHTMAPS = new HashSet<Heightmap.Types>
    {
        Heightmap.Types.OCEAN_FLOOR,
        Heightmap.Types.WORLD_SURFACE,
        Heightmap.Types.MOTION_BLOCKING,
        Heightmap.Types.MOTION_BLOCKING_NO_LEAVES
    };

    public static readonly ChunkStatus EMPTY =
        Register("empty", null, WORLDGEN_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus STRUCTURE_STARTS =
        Register("structure_starts", EMPTY, WORLDGEN_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus STRUCTURE_REFERENCES =
        Register("structure_references", STRUCTURE_STARTS, WORLDGEN_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus BIOMES =
        Register("biomes", STRUCTURE_REFERENCES, WORLDGEN_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus TERRAIN =
        Register("terrain", BIOMES, FINAL_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus FEATURES =
        Register("features", TERRAIN, FINAL_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus INITIALIZE_LIGHT =
        Register("initialize_light", FEATURES, FINAL_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus LIGHT =
        Register("light", INITIALIZE_LIGHT, FINAL_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus SPAWN =
        Register("spawn", LIGHT, FINAL_HEIGHTMAPS, ChunkType.PROTOCHUNK);
    public static readonly ChunkStatus FULL =
        Register("full", SPAWN, FINAL_HEIGHTMAPS, ChunkType.LEVELCHUNK);

    public static Codec<ChunkStatus> CODEC { get; } =
        BuiltInRegistries.ChunkStatus.ByNameCodec();

    private readonly int _index;
    private readonly ChunkStatus _parent;
    private readonly ChunkType _chunkType;
    private readonly ISet<Heightmap.Types> _heightmapsAfter;

    protected ChunkStatus(
        ChunkStatus? parent,
        ISet<Heightmap.Types> heightmapsAfter,
        ChunkType chunkType)
    {
        ArgumentNullException.ThrowIfNull(heightmapsAfter);

        _parent = parent ?? this;
        _chunkType = chunkType;
        _heightmapsAfter = heightmapsAfter;
        _index = parent is null ? 0 : parent.GetIndex() + 1;
    }

    private static ChunkStatus Register(
        string name,
        ChunkStatus? parent,
        ISet<Heightmap.Types> heightmapsAfter,
        ChunkType chunkType)
    {
        var status = new ChunkStatus(parent, heightmapsAfter, chunkType);
        ResourceLocation location = ResourceLocation.FromNamespaceAndPath("asteria", name);
        Asteria.Content.Registries.Registry.Register(BuiltInRegistries.ChunkStatus, location, status);
        return status;
    }

    public static List<ChunkStatus> GetStatusList()
    {
        var statuses = new List<ChunkStatus>();
        ChunkStatus status = FULL;

        while (!ReferenceEquals(status.GetParent(), status))
        {
            statuses.Add(status);
            status = status.GetParent();
        }

        statuses.Add(status);
        statuses.Reverse();
        return statuses;
    }

    public int GetIndex() => _index;

    public ChunkStatus GetParent() => _parent;

    public ChunkType GetChunkType() => _chunkType;

    public static ChunkStatus? ByName(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ResourceLocation? location = ResourceLocation.TryParse(key);
        return location.HasValue ? BuiltInRegistries.ChunkStatus.GetValue(location.Value) : null;
    }

    /// <summary>
    /// Returns the same mutable set held by the status, matching ChunkStatus.heightmapsAfter().
    /// </summary>
    public ISet<Heightmap.Types> GetHeightmapsAfter() => _heightmapsAfter;

    public bool IsOrAfter(ChunkStatus step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return GetIndex() >= step.GetIndex();
    }

    public bool IsAfter(ChunkStatus step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return GetIndex() > step.GetIndex();
    }

    public bool IsOrBefore(ChunkStatus step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return GetIndex() <= step.GetIndex();
    }

    public bool IsBefore(ChunkStatus step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return GetIndex() < step.GetIndex();
    }

    public static ChunkStatus Max(ChunkStatus a, ChunkStatus b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.IsAfter(b) ? a : b;
    }

    public override string ToString() => GetName();

    public string GetName() =>
        BuiltInRegistries.ChunkStatus.GetKey(this)?.ToString() ??
        throw new InvalidOperationException("Unregistered chunk status.");
}
