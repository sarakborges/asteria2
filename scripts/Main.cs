using Asteria.Content.Registries;
using Godot;

namespace Asteria;

/// <summary>
/// Godot application bootstrap. Gameplay and world rendering are activated only
/// after their Minecraft 26.3 equivalents have been ported.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        RegistryBootstrap.Initialize();
        GD.Print(
            $"Asteria content registries initialized: {BuiltInRegistries.Block.Count} blocks, " +
            $"{Asteria.Content.Blocks.Block.BlockStateRegistry.Count} block states.");
    }
}
