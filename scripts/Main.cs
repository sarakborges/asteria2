using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.Level;
using Godot;

namespace Asteria;

public partial class Main : Node3D
{
    private const long DevelopmentWorldSeed = 132456789L;

    public OverworldWorld? World { get; private set; }

    public override void _Ready()
    {
        RegistryBootstrap.Initialize();

        ResourceLocation airId = ResourceLocation.FromNamespaceAndPath("minecraft", "air");
        if (!ReferenceEquals(BuiltInRegistries.Block.GetValue(airId), Blocks.AIR))
            throw new InvalidOperationException("The built-in air block must be registered.");

        if (!BuiltInRegistries.AreFrozen || !RegistryBootstrap.IsInitialized)
            throw new InvalidOperationException("Built-in content registries must be frozen after bootstrap.");

        GD.Print(
            $"Content registries ready: {BuiltInRegistries.Block.Count} blocks, " +
            $"{Block.BlockStateRegistry.Count} block states.");

        World = new OverworldWorld(DevelopmentWorldSeed);
        ProtoChunk originChunk = World.GetChunk(ChunkPos.Zero);
        int nonEmptySections = originChunk.GetSections().Count(section => !section.HasOnlyAir);

        var chunkRenderer = new ChunkDebugRenderer
        {
            Name = "ChunkDebugRenderer"
        };
        AddChild(chunkRenderer);
        chunkRenderer.Display(originChunk);

        CreateDebugCamera(originChunk);

        GD.Print(
            $"Overworld ready: seed={World.Seed}, chunk={originChunk.GetPos()}, " +
            $"sections={originChunk.GetSections().Length}, nonEmptySections={nonEmptySections}, " +
            $"status={originChunk.GetPersistedStatus().GetIndex()}, " +
            $"debugFaces={chunkRenderer.FaceCount}");
    }

    private void CreateDebugCamera(ProtoChunk chunk)
    {
        ChunkPos position = chunk.GetPos();
        int targetX = position.GetMiddleBlockX();
        int targetZ = position.GetMiddleBlockZ();
        int surfaceY = chunk.GetHeight(Heightmap.Types.WORLD_SURFACE, targetX, targetZ);

        var sun = new DirectionalLight3D
        {
            Name = "DebugSun",
            RotationDegrees = new Vector3(-50.0f, 35.0f, 0.0f),
            ShadowEnabled = true
        };
        AddChild(sun);

        var camera = new Camera3D
        {
            Name = "DebugCamera",
            Position = new Vector3(
                targetX + 28.0f,
                Math.Max(surfaceY + 34.0f, 96.0f),
                targetZ + 28.0f),
            Current = true,
            Fov = 70.0f,
            Far = 768.0f
        };
        AddChild(camera);
        camera.LookAt(
            new Vector3(targetX, surfaceY, targetZ),
            Vector3.Up);
    }
}
