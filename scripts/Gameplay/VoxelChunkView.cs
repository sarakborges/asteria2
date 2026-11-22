using Asteria.Content.Chunks;
using Asteria.World;
using Godot;

namespace Asteria.Gameplay;

/// <summary>
/// Godot scene representation of one logical chunk: visible mesh plus static collision.
/// </summary>
public sealed partial class VoxelChunkView : Node3D
{
    private VoxelWorld? _world;
    private Chunk? _chunk;
    private MeshInstance3D? _meshInstance;
    private StaticBody3D? _staticBody;
    private CollisionShape3D? _collisionShape;

    public void Configure(VoxelWorld world, Chunk chunk)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));

        ChunkPos position = chunk.GetPos();
        Position = new Vector3(position.GetMinBlockX(), 0.0f, position.GetMinBlockZ());
        Name = $"Chunk_{position.X}_{position.Z}";

        _meshInstance = new MeshInstance3D
        {
            Name = "TerrainMesh"
        };
        AddChild(_meshInstance);

        _staticBody = new StaticBody3D
        {
            Name = "TerrainCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        AddChild(_staticBody);

        _collisionShape = new CollisionShape3D
        {
            Name = "TerrainShape"
        };
        _staticBody.AddChild(_collisionShape);
    }

    public void Rebuild()
    {
        if (_world is null || _chunk is null ||
            _meshInstance is null || _collisionShape is null)
        {
            throw new InvalidOperationException("Chunk view must be configured before it is rebuilt.");
        }

        ArrayMesh mesh = BlockMeshBuilder.Build(_chunk, _world.GetBlockState);
        if (mesh.GetSurfaceCount() == 0)
        {
            _meshInstance.Mesh = null;
            _collisionShape.Shape = null;
            return;
        }

        _meshInstance.Mesh = mesh;
        _collisionShape.Shape = mesh.CreateTrimeshShape();
    }
}
