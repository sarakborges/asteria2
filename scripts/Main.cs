using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Godot;

namespace Asteria;

public partial class Main : Node3D
{
    public override void _Ready()
    {
        RegistryBootstrap.Initialize();

        var testBlockId = ResourceLocation.FromNamespaceAndPath("asteria", "registry_test");
        var testBlock = BuiltInRegistries.Block.GetEntry(testBlockId);

        GD.Print(
            $"Block registry ready: {BuiltInRegistries.Block.Count} entry, " +
            $"'{testBlock.Key.Location}' has runtime id {testBlock.Id}, " +
            $"frozen={BuiltInRegistries.Block.IsFrozen}.");
    }
}
