using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;
using Asteria.Content.Materials;

namespace Asteria.World.Rendering;

/// <summary>
/// Vertex data for the temporary solid-terrain diagnostic view.
/// Map colors are used only to inspect generated block placement; this is not the
/// game's textured block renderer.
/// </summary>
public readonly record struct ChunkDebugMeshVertex(
    float X,
    float Y,
    float Z,
    float NormalX,
    float NormalY,
    float NormalZ,
    byte Red,
    byte Green,
    byte Blue);

/// <summary>
/// Triangle-list geometry for inspecting one generated chunk without a renderer dependency.
/// </summary>
public sealed class ChunkDebugMeshData
{
    private readonly IReadOnlyList<ChunkDebugMeshVertex> _vertices;

    public IReadOnlyList<ChunkDebugMeshVertex> Vertices => _vertices;

    public int VertexCount => _vertices.Count;

    public int TriangleCount => _vertices.Count / 3;

    public int FaceCount => _vertices.Count / 6;

    public bool IsEmpty => _vertices.Count == 0;

    internal ChunkDebugMeshData(IEnumerable<ChunkDebugMeshVertex> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        _vertices = Array.AsReadOnly(vertices.ToArray());
    }
}

/// <summary>
/// Builds an intentionally limited, temporary diagnostic mesh from full-cube model blocks.
/// It does not replace Minecraft's model, texture, fluid, transparency, or render-layer pipeline.
/// </summary>
public static class ChunkDebugMeshBuilder
{
    private readonly record struct Corner(float X, float Y, float Z);

    private readonly record struct Face(
        Direction Direction,
        int StepX,
        int StepY,
        int StepZ,
        float NormalX,
        float NormalY,
        float NormalZ,
        Corner A,
        Corner B,
        Corner C,
        Corner D);

    private static readonly Face[] FACES =
    {
        new(
            Direction.NORTH, 0, 0, -1, 0.0f, 0.0f, -1.0f,
            new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0)),
        new(
            Direction.SOUTH, 0, 0, 1, 0.0f, 0.0f, 1.0f,
            new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)),
        new(
            Direction.WEST, -1, 0, 0, -1.0f, 0.0f, 0.0f,
            new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)),
        new(
            Direction.EAST, 1, 0, 0, 1.0f, 0.0f, 0.0f,
            new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1)),
        new(
            Direction.DOWN, 0, -1, 0, 0.0f, -1.0f, 0.0f,
            new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)),
        new(
            Direction.UP, 0, 1, 0, 0.0f, 1.0f, 0.0f,
            new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0))
    };

    /// <summary>
    /// Builds only full-cube MODEL blocks. A supplied neighbor lookup must return
    /// already-loaded states only; it must not trigger chunk generation.
    /// Unloaded chunk borders remain open until their neighbor is available.
    /// </summary>
    public static ChunkDebugMeshData Build(
        ChunkAccess chunk,
        Func<BlockPos, BlockState?>? getLoadedNeighborState = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var vertices = new List<ChunkDebugMeshVertex>();
        ChunkPos chunkPosition = chunk.GetPos();
        int originX = chunkPosition.GetMinBlockX();
        int originZ = chunkPosition.GetMinBlockZ();

        chunk.FindBlocks(
            static state => !state.IsAir,
            (position, state) =>
            {
                if (state.GetRenderShape() != RenderShape.MODEL)
                    return;

                VoxelShape shape = state.GetShape(chunk, position);
                if (!Block.IsShapeFullBlock(shape))
                    return;

                int localX = position.GetX() - originX;
                int localY = position.GetY();
                int localZ = position.GetZ() - originZ;

                int mapColor = state.GetMapColor(chunk, position).Col;
                byte red = (byte)(mapColor >> 16 & 0xFF);
                byte green = (byte)(mapColor >> 8 & 0xFF);
                byte blue = (byte)(mapColor & 0xFF);

                foreach (Face face in FACES)
                {
                    var neighborPosition = new BlockPos(
                        position.GetX() + face.StepX,
                        position.GetY() + face.StepY,
                        position.GetZ() + face.StepZ);

                    BlockState? neighborState = chunkPosition.Contains(neighborPosition)
                        ? chunk.GetBlockState(neighborPosition)
                        : getLoadedNeighborState?.Invoke(neighborPosition);

                    if (neighborState is not null &&
                        (neighborState.IsSolidRender || state.SkipRendering(neighborState, face.Direction)))
                    {
                        continue;
                    }

                    AppendQuad(
                        vertices,
                        localX,
                        localY,
                        localZ,
                        face,
                        red,
                        green,
                        blue);
                }
            });

        return new ChunkDebugMeshData(vertices);
    }

    private static void AppendQuad(
        ICollection<ChunkDebugMeshVertex> vertices,
        int x,
        int y,
        int z,
        Face face,
        byte red,
        byte green,
        byte blue)
    {
        // Godot treats clockwise triangles as front-facing. Keep the stored normals outward.
        AppendVertex(vertices, x, y, z, face, face.A, red, green, blue);
        AppendVertex(vertices, x, y, z, face, face.C, red, green, blue);
        AppendVertex(vertices, x, y, z, face, face.B, red, green, blue);
        AppendVertex(vertices, x, y, z, face, face.A, red, green, blue);
        AppendVertex(vertices, x, y, z, face, face.D, red, green, blue);
        AppendVertex(vertices, x, y, z, face, face.C, red, green, blue);
    }

    private static void AppendVertex(
        ICollection<ChunkDebugMeshVertex> vertices,
        int x,
        int y,
        int z,
        Face face,
        Corner corner,
        byte red,
        byte green,
        byte blue) =>
        vertices.Add(new ChunkDebugMeshVertex(
            x + corner.X,
            y + corner.Y,
            z + corner.Z,
            face.NormalX,
            face.NormalY,
            face.NormalZ,
            red,
            green,
            blue));
}
