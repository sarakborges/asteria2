using Asteria.Core;
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
    private const int DevelopmentRenderRadius = 1;
    private const int DevelopmentChunkLoadsPerFrame = 1;

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

        CreateDebugSun();
        Vector3 playerSpawn = FindPlayerSpawnPosition(originChunk);
        PlayerController player = CreatePlayer(playerSpawn);

        var streaming = new ChunkStreamingController
        {
            Name = "ChunkStreaming"
        };
        streaming.Configure(
            World,
            player,
            DevelopmentRenderRadius,
            DevelopmentChunkLoadsPerFrame);
        player.ConfigureWorld(World, streaming.RefreshAfterBlockChange);
        AddChild(streaming);

        GD.Print(
            $"Overworld ready: seed={World.Seed}, cachedChunks={World.LoadedChunkCount}, " +
            $"origin={originChunk.GetPos()}, sections={originChunk.GetSections().Length}, " +
            $"nonEmptySections={nonEmptySections}, status={originChunk.GetPersistedStatus().GetIndex()}, " +
            $"playerSpawn={playerSpawn}, renderRadius={DevelopmentRenderRadius}");
    }

    private void CreateDebugSun()
    {
        var sun = new DirectionalLight3D
        {
            Name = "DebugSun",
            RotationDegrees = new Vector3(-50.0f, 35.0f, 0.0f),
            ShadowEnabled = true
        };
        AddChild(sun);
    }

    private static Vector3 FindPlayerSpawnPosition(ProtoChunk chunk)
    {
        ChunkPos position = chunk.GetPos();
        int centerX = position.GetMiddleBlockX();
        int centerZ = position.GetMiddleBlockZ();
        int bestX = -1;
        int bestZ = -1;
        int bestY = chunk.GetMinY();
        double bestDistance = double.PositiveInfinity;

        for (int localZ = 1; localZ < 15; localZ++)
        {
            for (int localX = 1; localX < 15; localX++)
            {
                int worldX = position.GetBlockX(localX);
                int worldZ = position.GetBlockZ(localZ);
                int surfaceY = chunk.GetHeight(Heightmap.Types.WORLD_SURFACE, worldX, worldZ);
                if (surfaceY < chunk.GetMinY())
                    continue;

                BlockState surfaceState = chunk.GetBlockState(new BlockPos(worldX, surfaceY, worldZ));
                if (surfaceState.IsAir || !surfaceState.GetFluidState().IsEmpty())
                    continue;

                BlockState feetState = chunk.GetBlockState(new BlockPos(worldX, surfaceY + 1, worldZ));
                BlockState headState = chunk.GetBlockState(new BlockPos(worldX, surfaceY + 2, worldZ));
                if (!feetState.IsAir || !headState.IsAir)
                    continue;

                double deltaX = localX - 7.5;
                double deltaZ = localZ - 7.5;
                double distance = deltaX * deltaX + deltaZ * deltaZ;
                if (distance >= bestDistance)
                    continue;

                bestX = worldX;
                bestZ = worldZ;
                bestY = surfaceY;
                bestDistance = distance;
            }
        }

        if (bestX < 0)
        {
            bestX = centerX;
            bestZ = centerZ;
            bestY = chunk.GetHeight(Heightmap.Types.OCEAN_FLOOR, centerX, centerZ);
            if (bestY < chunk.GetMinY())
                throw new InvalidOperationException("The generated origin chunk has no solid player spawn surface.");
        }

        return new Vector3(bestX + 0.5f, bestY + 1.95f, bestZ + 0.5f);
    }

    private PlayerController CreatePlayer(Vector3 spawnPosition)
    {
        RegisterMovementActions();

        var player = new PlayerController
        {
            Name = "Player",
            Position = spawnPosition,
            FloorSnapLength = 0.2f
        };
        player.AddChild(new CollisionShape3D
        {
            Name = "CapsuleCollision",
            Shape = new CapsuleShape3D
            {
                Radius = 0.35f,
                Height = 1.8f
            }
        });
        player.AddChild(new Camera3D
        {
            Name = "Camera3D",
            Position = new Vector3(0.0f, 0.72f, 0.0f),
            Current = true,
            Fov = 75.0f,
            Far = 768.0f
        });

        AddChild(player);
        return player;
    }

    private static void RegisterMovementActions()
    {
        AddKeyAction("move_forward", Key.W, Key.Up);
        AddKeyAction("move_back", Key.S, Key.Down);
        AddKeyAction("move_left", Key.A, Key.Left);
        AddKeyAction("move_right", Key.D, Key.Right);
        AddKeyAction("jump", Key.Space);
    }

    private static void AddKeyAction(string action, params Key[] keys)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        foreach (Key key in keys)
        {
            InputMap.ActionAddEvent(action, new InputEventKey
            {
                PhysicalKeycode = key
            });
        }
    }
}
