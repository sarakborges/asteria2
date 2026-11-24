using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Godot;

namespace Asteria;

public partial class Main : Node3D
{
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
    }
}
