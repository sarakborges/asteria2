using Asteria.Content.Blocks;

namespace Asteria.Content.Registries;

/// <summary>Registers built-in content, assigns runtime state IDs, then freezes registries.</summary>
public static class RegistryBootstrap
{
    private enum BootstrapState { NotStarted, Running, Complete, Failed }

    private static readonly object Sync = new();
    private static BootstrapState _state;

    public static bool IsInitialized
    {
        get
        {
            lock (Sync)
                return _state == BootstrapState.Complete;
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_state == BootstrapState.Complete)
                return;
            if (_state == BootstrapState.Running)
                throw new InvalidOperationException("Registry bootstrap cannot be entered recursively.");
            if (_state == BootstrapState.Failed)
                throw new InvalidOperationException("Registry bootstrap previously failed; the process cannot safely retry it.");

            _state = BootstrapState.Running;
            try
            {
                global::Asteria.Content.Blocks.Blocks.RegisterAll();

                foreach (Holder<Block>.Reference blockHolder in BuiltInRegistries.Block.ListElements())
                {
                    foreach (BlockState state in blockHolder.Value.StateDefinition.PossibleStates)
                        BuiltInRegistries.BlockStates.Register(state);
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
