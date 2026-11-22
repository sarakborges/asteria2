using Asteria.Content.Registries;

namespace Asteria.World.Clocks;

/// <summary>Resolves a registered clock definition to its local runtime instance.</summary>
public interface ClockManager
{
    ClockInstance GetInstance(Holder<WorldClock> clock);
}
