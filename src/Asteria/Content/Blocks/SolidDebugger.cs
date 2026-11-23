using System.Diagnostics;
using System.Threading;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects the reasons behind legacy block-state solidity during the registry cache pass.
/// </summary>
public sealed class SolidDebugger
{
    private static readonly AsyncLocal<SolidDebugger?> STATUS = new();
    private static readonly bool DEBUG_CALCULATE_SOLID = GetDebugFlag("CALCULATE_SOLID");

    private readonly Dictionary<Block, Reason> _reasonByBlock = new();

    public static bool LogAndGet(Block block, Reason reason)
    {
        ArgumentNullException.ThrowIfNull(block);

        SolidDebugger? debugger = STATUS.Value;
        if (debugger is not null)
        {
            if (debugger._reasonByBlock.TryGetValue(block, out Reason? existingReason))
            {
                if (existingReason.CausesSolid != reason.CausesSolid)
                {
                    Trace.WriteLine(
                        $"Block {block} has multiple values for solid. Existing: {existingReason.CausesSolid} " +
                        $"({existingReason.Message}) New: {reason.CausesSolid} ({reason.Message})");
                }
            }
            else
            {
                debugger._reasonByBlock.Add(block, reason);
            }
        }

        return reason.CausesSolid;
    }

    public void Dump()
    {
        foreach (IGrouping<Reason, KeyValuePair<Block, Reason>> group in
                 _reasonByBlock.GroupBy(entry => entry.Value))
        {
            Reason reason = group.Key;
            string blockNames = string.Join(
                "\n",
                group.Select(entry =>
                    "\t" + (entry.Key.BuiltInRegistryHolder?.KeyOrNull?.Location.ToString() ??
                             entry.Key.ToString())));

            Trace.WriteLine(
                $"The following blocks are {(reason.CausesSolid ? "solid" : "not solid")} because: " +
                $"{reason.Message}:\n {blockNames}");
        }
    }

    public static void RunAndDump(Action task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!DEBUG_CALCULATE_SOLID)
        {
            task();
            return;
        }

        var debugger = new SolidDebugger();
        SolidDebugger? previous = STATUS.Value;
        STATUS.Value = debugger;
        try
        {
            task();
        }
        finally
        {
            STATUS.Value = previous;
        }

        debugger.Dump();
    }

    private static bool GetDebugFlag(string name)
    {
        if (!ReadBooleanProperty("MC_DEBUG_ENABLED"))
            return false;

        return ReadBooleanProperty($"MC_DEBUG_{name}");
    }

    private static bool ReadBooleanProperty(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
               (value.Length == 0 || bool.TryParse(value, out bool enabled) && enabled);
    }

    public enum Reason
    {
        FORCE_SOLID_ON(true, "forceSolidOn called on properties"),
        LARGE_ENOUGH_COLLISION_SHAPE(true, "collision shape bounds size is large enough"),
        HIGH_ENOUGH_COLLISION_SHAPE(true, "collision shape bounds are higher than 1"),
        FORCE_SOLID_OFF(false, "forceSolidOff called on properties"),
        NULL_CACHE(false, "cache is null"),
        EMPTY_COLLISION_SHAPE(false, "collision shape is empty"),
        FALLTHROUGH(false, "fallthrough");

        public bool CausesSolid { get; }
        public string Message { get; }

        Reason(bool causesSolid, string message)
        {
            CausesSolid = causesSolid;
            Message = message;
        }
    }
}
