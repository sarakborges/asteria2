using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.World;
using Godot;

namespace Asteria.Gameplay;

/// <summary>
/// Builds visible faces for the full-cube block states currently used by the local world.
/// Adjacent opaque cubes share no internal faces; chunk-edge visibility is resolved by the
/// owning world so neighboring chunks mesh consistently.
/// </summary>
internal static class BlockMeshBuilder
{
    private static readonly Block[] RenderOrder =
    {
        Blocks.STONE,
        Blocks.DIRT,
        Blocks.GRASS_BLOCK
    };

    public static ArrayMesh Build(
        Chunk chunk,
        Func<int, int, int, BlockState> getWorldBlockState)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(getWorldBlockState);

        var buffers = new Dictionary<Block, MeshBuffer>();
        ChunkPos chunkPosition = chunk.GetPos();
        int baseX = chunkPosition.GetMinBlockX();
        int baseZ = chunkPosition.GetMinBlockZ();

        for (int y = chunk.GetMinY(); y <= chunk.GetMaxY(); y++)
        for (int localZ = 0; localZ < ChunkSection.Depth; localZ++)
        for (int localX = 0; localX < ChunkSection.Width; localX++)
        {
            BlockState state = chunk.GetSection(chunk.GetSectionIndex(y))
                .GetBlockState(localX, y & 15, localZ);
            if (state.IsAir)
                continue;

            Block block = state.Block;
            if (!buffers.TryGetValue(block, out MeshBuffer? buffer))
            {
                buffer = new MeshBuffer();
                buffers.Add(block, buffer);
            }

            int worldX = baseX + localX;
            int worldZ = baseZ + localZ;

            if (getWorldBlockState(worldX - 1, y, worldZ).IsAir)
                AddFace(buffer, localX, y, localZ, Face.Left);
            if (getWorldBlockState(worldX + 1, y, worldZ).IsAir)
                AddFace(buffer, localX, y, localZ, Face.Right);
            if (getWorldBlockState(worldX, y - 1, worldZ).IsAir)
                AddFace(buffer, localX, y, localZ, Face.Bottom);
            if (getWorldBlockState(worldX, y + 1, worldZ).IsAir)
                AddFace(buffer, localX, y, localZ, Face.Top);
            if (getWorldBlockState(worldX, y, worldZ - 1).IsAir)
                AddFace(buffer, localX, y, localZ, Face.Back);
            if (getWorldBlockState(worldX, y, worldZ + 1).IsAir)
                AddFace(buffer, localX, y, localZ, Face.Front);
        }

        var mesh = new ArrayMesh();
        foreach (Block block in RenderOrder)
        {
            if (!buffers.TryGetValue(block, out MeshBuffer? buffer) || buffer.Indices.Count == 0)
                continue;

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = buffer.Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = buffer.Normals.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = buffer.Indices.ToArray();

            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, CreateMaterial(block));
        }

        return mesh;
    }

    private static StandardMaterial3D CreateMaterial(Block block)
    {
        Color color = ReferenceEquals(block, Blocks.STONE)
            ? new Color(0.48f, 0.50f, 0.53f)
            : ReferenceEquals(block, Blocks.DIRT)
                ? new Color(0.39f, 0.25f, 0.14f)
                : new Color(0.24f, 0.54f, 0.22f);

        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 1.0f,
            CullMode = StandardMaterial3D.CullModeEnum.Disabled
        };
    }

    private static void AddFace(MeshBuffer buffer, int x, int y, int z, Face face)
    {
        Vector3 a;
        Vector3 b;
        Vector3 c;
        Vector3 d;
        Vector3 normal;

        switch (face)
        {
            case Face.Left:
                a = new Vector3(x, y, z);
                b = new Vector3(x, y, z + 1);
                c = new Vector3(x, y + 1, z + 1);
                d = new Vector3(x, y + 1, z);
                normal = new Vector3(-1, 0, 0);
                break;
            case Face.Right:
                a = new Vector3(x + 1, y, z + 1);
                b = new Vector3(x + 1, y, z);
                c = new Vector3(x + 1, y + 1, z);
                d = new Vector3(x + 1, y + 1, z + 1);
                normal = new Vector3(1, 0, 0);
                break;
            case Face.Bottom:
                a = new Vector3(x, y, z + 1);
                b = new Vector3(x, y, z);
                c = new Vector3(x + 1, y, z);
                d = new Vector3(x + 1, y, z + 1);
                normal = new Vector3(0, -1, 0);
                break;
            case Face.Top:
                a = new Vector3(x, y + 1, z);
                b = new Vector3(x, y + 1, z + 1);
                c = new Vector3(x + 1, y + 1, z + 1);
                d = new Vector3(x + 1, y + 1, z);
                normal = new Vector3(0, 1, 0);
                break;
            case Face.Back:
                a = new Vector3(x + 1, y, z);
                b = new Vector3(x, y, z);
                c = new Vector3(x, y + 1, z);
                d = new Vector3(x + 1, y + 1, z);
                normal = new Vector3(0, 0, -1);
                break;
            case Face.Front:
                a = new Vector3(x, y, z + 1);
                b = new Vector3(x + 1, y, z + 1);
                c = new Vector3(x + 1, y + 1, z + 1);
                d = new Vector3(x, y + 1, z + 1);
                normal = new Vector3(0, 0, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(face));
        }

        int firstVertex = buffer.Vertices.Count;
        buffer.Vertices.Add(a);
        buffer.Vertices.Add(b);
        buffer.Vertices.Add(c);
        buffer.Vertices.Add(d);
        buffer.Normals.Add(normal);
        buffer.Normals.Add(normal);
        buffer.Normals.Add(normal);
        buffer.Normals.Add(normal);

        buffer.Indices.Add(firstVertex);
        buffer.Indices.Add(firstVertex + 1);
        buffer.Indices.Add(firstVertex + 2);
        buffer.Indices.Add(firstVertex);
        buffer.Indices.Add(firstVertex + 2);
        buffer.Indices.Add(firstVertex + 3);
    }

    private enum Face
    {
        Left,
        Right,
        Bottom,
        Top,
        Back,
        Front
    }

    private sealed class MeshBuffer
    {
        public List<Vector3> Vertices { get; } = new();
        public List<Vector3> Normals { get; } = new();
        public List<int> Indices { get; } = new();
    }
}
