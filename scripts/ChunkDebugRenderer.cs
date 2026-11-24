using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.World.Rendering;
using Godot;

namespace Asteria;

/// <summary>
/// Godot adapter for the temporary solid-terrain diagnostic mesh.
/// It deliberately does not implement block models, textures, fluids, or render layers.
/// </summary>
public partial class ChunkDebugRenderer : Node3D
{
    public int FaceCount { get; private set; }

    public void Display(ProtoChunk chunk, Func<BlockPos, BlockState?>? getLoadedNeighborState = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        ChunkDebugMeshData data = ChunkDebugMeshBuilder.Build(chunk, getLoadedNeighborState);
        FaceCount = data.FaceCount;
        if (data.IsEmpty)
            return;

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1.0f,
            CullMode = BaseMaterial3D.CullModeEnum.Back
        };

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        foreach (ChunkDebugMeshVertex vertex in data.Vertices)
        {
            surface.SetNormal(new Vector3(
                vertex.NormalX,
                vertex.NormalY,
                vertex.NormalZ));
            surface.SetColor(new Color(
                vertex.Red / 255.0f,
                vertex.Green / 255.0f,
                vertex.Blue / 255.0f));
            surface.AddVertex(new Vector3(vertex.X, vertex.Y, vertex.Z));
        }

        ArrayMesh mesh = surface.Commit();
        var meshInstance = new MeshInstance3D
        {
            Name = $"Chunk_{chunk.GetPos().X}_{chunk.GetPos().Z}_SolidPreview",
            Mesh = mesh,
            MaterialOverride = material,
            Position = new Vector3(
                chunk.GetPos().GetMinBlockX(),
                0.0f,
                chunk.GetPos().GetMinBlockZ())
        };
        AddChild(meshInstance);
        meshInstance.CreateTrimeshCollision();
    }
}
