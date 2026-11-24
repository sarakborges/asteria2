using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.Rendering;
using Godot;

namespace Asteria;

/// <summary>
/// Maintains the diagnostic render region around the first-person player.
/// Chunk data stays in OverworldWorld; only render and collision nodes leave the
/// active region until local chunk persistence is available.
/// </summary>
public partial class ChunkStreamingController : Node3D
{
    private static readonly (int X, int Z)[] CARDINAL_OFFSETS =
    {
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1)
    };

    private OverworldWorld _world = null!;
    private PlayerController _player = null!;
    private readonly Dictionary<ChunkPos, ChunkDebugRenderer> _renderers = new();
    private readonly Queue<ChunkPos> _pendingChunks = new();
    private readonly HashSet<ChunkPos> _desiredChunks = new();
    private ChunkPos? _currentCenter;
    private int _renderRadius;
    private int _maxChunkLoadsPerFrame;
    private bool _configured;

    public int ActiveChunkCount => _renderers.Count;

    public int PendingChunkCount => _pendingChunks.Count;

    public void Configure(
        OverworldWorld world,
        PlayerController player,
        int renderRadius = 1,
        int maxChunkLoadsPerFrame = 1)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);

        if (_configured)
            throw new InvalidOperationException("Chunk streaming is already configured.");
        if (renderRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(renderRadius));
        if (maxChunkLoadsPerFrame < 1)
            throw new ArgumentOutOfRangeException(nameof(maxChunkLoadsPerFrame));

        _world = world;
        _player = player;
        _renderRadius = renderRadius;
        _maxChunkLoadsPerFrame = maxChunkLoadsPerFrame;
        _configured = true;
    }

    public override void _Ready()
    {
        if (!_configured)
            throw new InvalidOperationException(
                "Configure the chunk streaming controller before adding it to the scene tree.");
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (!_configured)
            return;

        ChunkPos center = GetPlayerChunkPosition();
        if (!center.IsValid())
            return;

        if (_currentCenter != center)
            UpdateRenderRegion(center);

        LoadPendingChunks();
    }

    private ChunkPos GetPlayerChunkPosition()
    {
        int blockX = Mathf.FloorToInt(_player.GlobalPosition.X);
        int blockZ = Mathf.FloorToInt(_player.GlobalPosition.Z);
        return ChunkPos.Containing(new BlockPos(blockX, _world.MinY, blockZ));
    }

    private void UpdateRenderRegion(ChunkPos center)
    {
        IReadOnlyList<ChunkPos> region =
            ChunkRenderRegion.GetChunkPositions(center, _renderRadius);
        _desiredChunks.Clear();
        foreach (ChunkPos position in region)
            _desiredChunks.Add(position);

        ChunkPos[] removedPositions = _renderers.Keys
            .Where(position => !_desiredChunks.Contains(position))
            .ToArray();

        foreach (ChunkPos position in removedPositions)
        {
            ChunkDebugRenderer renderer = _renderers[position];
            _renderers.Remove(position);
            renderer.QueueFree();
        }

        foreach (ChunkPos removed in removedPositions)
            RefreshCardinalNeighbors(removed);

        _pendingChunks.Clear();
        foreach (ChunkPos position in region)
        {
            if (!_renderers.ContainsKey(position))
                _pendingChunks.Enqueue(position);
        }

        _currentCenter = center;
    }

    private void LoadPendingChunks()
    {
        int loadedThisFrame = 0;
        while (loadedThisFrame < _maxChunkLoadsPerFrame &&
               _pendingChunks.TryDequeue(out ChunkPos position))
        {
            if (!_desiredChunks.Contains(position) || _renderers.ContainsKey(position))
                continue;

            ProtoChunk chunk = _world.GetChunk(position);
            var renderer = new ChunkDebugRenderer
            {
                Name = $"ChunkDebugRenderer_{position.X}_{position.Z}"
            };

            AddChild(renderer);
            _renderers.Add(position, renderer);
            renderer.Display(chunk, GetRenderedNeighborState);

            RefreshCardinalNeighbors(position);
            loadedThisFrame++;
        }
    }


    /// <summary>
    /// Rebuilds the visible mesh after a block mutation. Only renderers whose
    /// shared border can be affected are rebuilt; the world cache is not unloaded.
    /// </summary>
    public void RefreshAfterBlockChange(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!_configured)
            return;

        ChunkPos chunkPosition = ChunkPos.Containing(position);
        RefreshRenderer(chunkPosition);

        int localX = position.GetX() & 15;
        int localZ = position.GetZ() & 15;
        if (localX == 0)
            RefreshRenderer(new ChunkPos(unchecked(chunkPosition.X - 1), chunkPosition.Z));
        if (localX == 15)
            RefreshRenderer(new ChunkPos(unchecked(chunkPosition.X + 1), chunkPosition.Z));
        if (localZ == 0)
            RefreshRenderer(new ChunkPos(chunkPosition.X, unchecked(chunkPosition.Z - 1)));
        if (localZ == 15)
            RefreshRenderer(new ChunkPos(chunkPosition.X, unchecked(chunkPosition.Z + 1)));
    }

    private void RefreshRenderer(ChunkPos position)
    {
        if (!_renderers.TryGetValue(position, out ChunkDebugRenderer? renderer) ||
            !_world.TryGetChunk(position, out ProtoChunk? chunk) ||
            chunk is null)
        {
            return;
        }

        renderer.Display(chunk, GetRenderedNeighborState);
    }

    private BlockState? GetRenderedNeighborState(BlockPos position)
    {
        ChunkPos neighborPosition = ChunkPos.Containing(position);
        if (!_renderers.ContainsKey(neighborPosition) ||
            !_world.TryGetChunk(neighborPosition, out ProtoChunk? neighborChunk) ||
            neighborChunk is null)
        {
            return null;
        }

        return neighborChunk.GetBlockState(position);
    }

    private void RefreshCardinalNeighbors(ChunkPos position)
    {
        foreach ((int offsetX, int offsetZ) in CARDINAL_OFFSETS)
        {
            var neighborPosition = new ChunkPos(
                unchecked(position.X + offsetX),
                unchecked(position.Z + offsetZ));
            if (!_renderers.TryGetValue(neighborPosition, out ChunkDebugRenderer? renderer) ||
                !_world.TryGetChunk(neighborPosition, out ProtoChunk? chunk) ||
                chunk is null)
            {
                continue;
            }

            renderer.Display(chunk, GetRenderedNeighborState);
        }
    }
}
