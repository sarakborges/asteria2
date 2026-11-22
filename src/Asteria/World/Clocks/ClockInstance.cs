namespace Asteria.World.Clocks;

/// <summary>Read-only runtime state exposed by a clock instance.</summary>
public interface ClockInstance
{
    long TotalTicks { get; }
    float PartialTick { get; }
    float Rate { get; }
    bool IsPaused { get; }
}
