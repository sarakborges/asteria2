using Asteria.Content.Blocks;

namespace Asteria.Content.Registries;

/// <summary>
/// Registers built-in content, assigns every block-state runtime ID, then freezes the registries.
/// </summary>
public static class RegistryBootstrap
{
    private enum BootstrapState
    {
        NotStarted,
        Running,
        Complete,
        Failed
    }

    private static readonly object Sync = new();
    private static BootstrapState _state;

    public static bool IsInitialized
    {
        get
        {
            lock (Sync)
            {
                return _state == BootstrapState.Complete;
            }
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_state == BootstrapState.Complete)
            {
                return;
            }

            if (_state == BootstrapState.Running)
            {
                throw new InvalidOperationException("Registry bootstrap cannot be entered recursively.");
            }

            if (_state == BootstrapState.Failed)
            {
                throw new InvalidOperationException(
                    "Registry bootstrap previously failed; the process cannot safely retry it.");
            }

            _state = BootstrapState.Running;

            try
            {
                Asteria.Content.Blocks.Blocks.RegisterAll();

                // The global state ID order follows block registry order, then each block's
                // deterministic StateDefinition.PossibleStates order.
                foreach (RegistryEntry<Block> blockEntry in BuiltInRegistries.Block)
                {
                    foreach (BlockState state in blockEntry.Value.StateDefinition.PossibleStates)
                    {
                        BuiltInRegistries.BlockStates.Register(state);
                    }
                }

                BuiltInRegistries.FreezeAll();
                _state = BootstrapState.Complete;
            }
            catch
            {
                _state = BootstrapState.Failed;
                throw;
            }
        }
    }
}
