using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Rendering;

internal static class ChunkDebugMeshChecks
{
    public static void Run()
    {
        RegistryBootstrap.Initialize();

        ProtoChunk chunk = CreateChunk(ChunkPos.Zero);
        Require(
            ChunkDebugMeshBuilder.Build(chunk).IsEmpty,
            "An all-air chunk must produce no diagnostic mesh geometry.");

        BlockPos firstPosition = new(1, 1, 1);
        chunk.SetBlockState(firstPosition, Blocks.STONE.DefaultState, flags: 0);

        ChunkDebugMeshData isolated = ChunkDebugMeshBuilder.Build(chunk);
        Require(
            isolated.FaceCount == 6 &&
            isolated.TriangleCount == 12 &&
            isolated.VertexCount == 36,
            "An isolated full-cube block must produce six exposed quad faces.");

        int expectedColor = Blocks.STONE.DefaultState.GetMapColor(chunk, firstPosition).Col;
        Require(
            isolated.Vertices.All(vertex =>
                vertex.Red == (byte)(expectedColor >> 16 & 0xFF) &&
                vertex.Green == (byte)(expectedColor >> 8 & 0xFF) &&
                vertex.Blue == (byte)(expectedColor & 0xFF)),
            "Diagnostic mesh vertices must retain the block state's map-color RGB value.");

        BlockPos adjacentPosition = new(2, 1, 1);
        chunk.SetBlockState(adjacentPosition, Blocks.DIRT.DefaultState, flags: 0);
        ChunkDebugMeshData joined = ChunkDebugMeshBuilder.Build(chunk);
        Require(
            joined.FaceCount == 10 &&
            joined.TriangleCount == 20,
            "Adjacent full-cube blocks must not emit their shared internal face.");

        chunk.SetBlockState(adjacentPosition, Blocks.WATER.DefaultState, flags: 0);
        ChunkDebugMeshData besideWater = ChunkDebugMeshBuilder.Build(chunk);
        Require(
            besideWater.FaceCount == 6,
            "A non-colliding fluid block must not be rendered as a solid cube or hide a solid block face.");

        ProtoChunk verticalChunk = CreateChunk(ChunkPos.Zero);
        verticalChunk.SetBlockState(new BlockPos(4, 4, 4), Blocks.STONE.DefaultState, flags: 0);
        verticalChunk.SetBlockState(new BlockPos(4, 5, 4), Blocks.STONE.DefaultState, flags: 0);
        Require(
            ChunkDebugMeshBuilder.Build(verticalChunk).FaceCount == 10,
            "Vertical neighbors inside a chunk must also cull their shared face.");

        ProtoChunk edgeChunk = CreateChunk(ChunkPos.Zero);
        edgeChunk.SetBlockState(new BlockPos(15, 4, 4), Blocks.STONE.DefaultState, flags: 0);
        ChunkDebugMeshData openBorder = ChunkDebugMeshBuilder.Build(edgeChunk);
        ChunkDebugMeshData closedBorder = ChunkDebugMeshBuilder.Build(
            edgeChunk,
            neighbor => neighbor.GetX() == 16 ? Blocks.STONE.DefaultState : null);
        Require(
            openBorder.FaceCount == 6 &&
            closedBorder.FaceCount == 5,
            "A loaded neighboring chunk state must cull a shared face without causing a chunk lookup or load.");

        ChunkDebugMeshVertex upVertex = isolated.Vertices.Single(vertex =>
            vertex.NormalX == 0.0f && vertex.NormalY == 1.0f && vertex.NormalZ == 0.0f);
        ChunkDebugMeshVertex northVertex = isolated.Vertices.First(vertex =>
            vertex.NormalX == 0.0f && vertex.NormalY == 0.0f && vertex.NormalZ == -1.0f);
        Require(
            upVertex.Y == 2.0f && northVertex.Z == 1.0f,
            "Diagnostic face positions and normals must use block-space coordinates and outward directions.");
    }

    private static ProtoChunk CreateChunk(ChunkPos position) =>
        new(
            position,
            LevelHeightAccessor.Create(0, 16),
            PalettedContainerFactory.CreateBuiltIn());

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
