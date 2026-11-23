using System.Diagnostics;
using System.Threading;
using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects the reasons behind legacy block-state solidity during the registry cache pass.
/// </summary>
public sealed class SolidDebugger
{
    private static readonly AsyncLocal<SolidDebugger?> STATUS = new();
    private static readonly bool DEBUG_CALCULATE_SOLID = GetDebugFlag("CALCULATE_SOLID");

    private readonly Dictionary<Holder<Block>, Reason> _firstReasonByBlock = new();
    private readonly List<KeyValuePair<Holder<Block>, Reason>> _reasonEntries = new();

    public static bool LogAndGet(Holder<Block> block, Reason reason)
    {
        ArgumentNullException.ThrowIfNull(block);

        SolidDebugger? debugger = STATUS.Value;
        if (debugger is not null)
        {
            if (debugger._firstReasonByBlock.TryGetValue(block, out Reason? existingReason))
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
                debugger._firstReasonByBlock.Add(block, reason);
                debugger._reasonEntries.Add(new KeyValuePair<Holder<Block>, Reason>(block, reason));
            }
        }

        return reason.CausesSolid;
    }

    public void Dump()
    {
        foreach (IGrouping<Reason, KeyValuePair<Holder<Block>, Reason>> group in
                 _reasonEntries.GroupBy(entry => entry.Value))
        {
            Reason reason = group.Key;
            string blockNames = string.Join(
                "\n",
                group.Select(entry =>
                    "\t" + (entry.Key.GetRegisteredName())));

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
               (value.Length == 0 || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class Reason
    {
        public static readonly Reason FORCE_SOLID_ON =
            new(0, nameof(FORCE_SOLID_ON), true, "forceSolidOn called on properties");
        public static readonly Reason LARGE_ENOUGH_COLLISION_SHAPE =
            new(1, nameof(LARGE_ENOUGH_COLLISION_SHAPE), true, "collision shape bounds size is large enough");
        public static readonly Reason HIGH_ENOUGH_COLLISION_SHAPE =
            new(2, nameof(HIGH_ENOUGH_COLLISION_SHAPE), true, "collision shape bounds are higher than 1");
        public static readonly Reason FORCE_SOLID_OFF =
            new(3, nameof(FORCE_SOLID_OFF), false, "forceSolidOff called on properties");
        public static readonly Reason NULL_CACHE =
            new(4, nameof(NULL_CACHE), false, "cache is null");
        public static readonly Reason EMPTY_COLLISION_SHAPE =
            new(5, nameof(EMPTY_COLLISION_SHAPE), false, "collision shape is empty");
        public static readonly Reason FALLTHROUGH =
            new(6, nameof(FALLTHROUGH), false, "fallthrough");

        public static IReadOnlyList<Reason> Values { get; } = Array.AsReadOnly(
            new[]
            {
                FORCE_SOLID_ON,
                LARGE_ENOUGH_COLLISION_SHAPE,
                HIGH_ENOUGH_COLLISION_SHAPE,
                FORCE_SOLID_OFF,
                NULL_CACHE,
                EMPTY_COLLISION_SHAPE,
                FALLTHROUGH
            });

        public int Ordinal { get; }
        public string Name { get; }
        public bool CausesSolid { get; }
        public string Message { get; }

        private Reason(int ordinal, string name, bool causesSolid, string message)
        {
            Ordinal = ordinal;
            Name = name;
            CausesSolid = causesSolid;
            Message = message;
        }

        public override string ToString() => Name;
    }
}
