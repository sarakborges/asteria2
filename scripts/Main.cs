using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Gameplay;
using Godot;

namespace Asteria;

public partial class Main : Node3D
{
    public override void _Ready()
    {
        RegistryBootstrap.Initialize();

        ResourceLocation airId = ResourceLocation.FromNamespaceAndPath("asteria", "air");
        if (!ReferenceEquals(BuiltInRegistries.Block.GetValue(airId), Blocks.AIR))
            throw new InvalidOperationException("The built-in air block must be registered.");

        if (!BuiltInRegistries.AreFrozen || !RegistryBootstrap.IsInitialized)
            throw new InvalidOperationException("Built-in content registries must be frozen after bootstrap.");

        var sunlight = new DirectionalLight3D
        {
            Rotation = new Vector3(-0.85f, 0.7f, 0.0f),
            LightEnergy = 1.15f,
            ShadowEnabled = true
        };
        AddChild(sunlight);

        var world = new VoxelWorld();
        AddChild(world);

        var player = new FirstPersonPlayer();
        player.Configure(world);
        player.Position = new Vector3(
            VoxelWorld.WorldSize / 2.0f,
            7.0f,
            VoxelWorld.WorldSize / 2.0f);
        AddChild(player);

        Input.MouseMode = Input.MouseModeEnum.Captured;

        GD.Print(
            $"Local world ready: {BuiltInRegistries.Block.Count} blocks, " +
            $"{Block.BlockStateRegistry.Count} block states, " +
            $"{VoxelWorld.ChunkCount * VoxelWorld.ChunkCount} chunks.");
    }
}
