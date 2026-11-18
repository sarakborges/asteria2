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

        ResourceLocation testBlockId =
            ResourceLocation.FromNamespaceAndPath("asteria", "registry_test");
        ResourceLocation missingBlockId =
            ResourceLocation.FromNamespaceAndPath("asteria", "missing_test_entry");

        RegistryEntry<Block> testBlockEntry =
            BuiltInRegistries.Block.GetEntry(testBlockId);
        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;

        if (defaultState.GetValue(Blocks.TEST_POWERED))
        {
            throw new InvalidOperationException("The test block default powered state must be false.");
        }

        if (defaultState.GetValue(Blocks.TEST_STAGE) != 1)
        {
            throw new InvalidOperationException("The test block default stage must be 1.");
        }

        BlockState changedState = defaultState
            .SetValue(Blocks.TEST_STAGE, 2)
            .SetValue(Blocks.TEST_POWERED, true);

        if (!changedState.GetValue(Blocks.TEST_POWERED) ||
            changedState.GetValue(Blocks.TEST_STAGE) != 2)
        {
            throw new InvalidOperationException("Block-state property transitions returned an invalid state.");
        }

        if (ReferenceEquals(defaultState, changedState) ||
            !ReferenceEquals(
                changedState,
                Blocks.TEST_BLOCK.DefaultState
                    .SetValue(Blocks.TEST_POWERED, true)
                    .SetValue(Blocks.TEST_STAGE, 2)))
        {
            throw new InvalidOperationException("Block states must be precomputed and reused.");
        }

        if (Blocks.TEST_BLOCK.StateDefinition.PossibleStates.Count != 6)
        {
            throw new InvalidOperationException("The test block should have exactly six possible states.");
        }

        if (BuiltInRegistries.BlockStates.Count != 7)
        {
            throw new InvalidOperationException("The global block-state map should contain seven states.");
        }

        if (!ReferenceEquals(
                BuiltInRegistries.BlockStates.ById(changedState.GlobalId),
                changedState))
        {
            throw new InvalidOperationException("Global block-state ID lookup failed.");
        }

        bool rejectedInvalidValue = false;
        try
        {
            changedState.TrySetValue(Blocks.TEST_STAGE, 9);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedInvalidValue = true;
        }

        if (!rejectedInvalidValue)
        {
            throw new InvalidOperationException("Invalid block-state values must be rejected.");
        }

        Block missingBlockFallback = BuiltInRegistries.Block.Get(missingBlockId);
        if (!ReferenceEquals(missingBlockFallback, Blocks.AIR))
        {
            throw new InvalidOperationException("Unknown block IDs must resolve to the configured air block.");
        }

        if (!BuiltInRegistries.AreFrozen || !RegistryBootstrap.IsInitialized)
        {
            throw new InvalidOperationException("Built-in content registries must be frozen after bootstrap.");
        }

        GD.Print(
            $"Block registry ready: {BuiltInRegistries.Block.Count} blocks, " +
            $"{BuiltInRegistries.BlockStates.Count} block states. " +
            $"'{testBlockEntry.Key.Location}' state '{changedState}' has global id " +
            $"{changedState.GlobalId}; registries frozen.");
    }
}
