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

        var testBlockId = ResourceLocation.FromNamespaceAndPath("asteria", "registry_test");
        var missingBlockId = ResourceLocation.FromNamespaceAndPath("asteria", "missing_test_entry");
        var testBlock = BuiltInRegistries.Block.GetEntry(testBlockId);
        var missingBlockFallback = BuiltInRegistries.Block.Get(missingBlockId);

        if (!ReferenceEquals(missingBlockFallback, Blocks.AIR))
        {
            throw new InvalidOperationException("Unknown block IDs must resolve to the configured air block.");
        }

        GD.Print(
            $"Block registry ready: {BuiltInRegistries.Block.Count} entries, " +
            $"'{testBlock.Key.Location}' has runtime id {testBlock.Id}, " +
            $"unknown-key fallback=air, frozen={BuiltInRegistries.Block.IsFrozen}.");
    }
}
