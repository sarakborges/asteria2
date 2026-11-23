using System.Runtime.CompilerServices;
using Asteria.Core;

namespace Asteria.World.Ticks;

/// <summary>A scheduled block or fluid tick with deterministic drain ordering.</summary>
public sealed record ScheduledTick<T> where T : class
{
    public static IComparer<ScheduledTick<T>> DRAIN_ORDER { get; } =
        Comparer<ScheduledTick<T>>.Create(static (left, right) =>
        {
            int comparison = left.TriggerTick.CompareTo(right.TriggerTick);
            if (comparison != 0)
                return comparison;

            comparison = left.Priority.CompareTo(right.Priority);
            return comparison != 0
                ? comparison
                : left.SubTickOrder.CompareTo(right.SubTickOrder);
        });

    public static IComparer<ScheduledTick<T>> INTRA_TICK_DRAIN_ORDER { get; } =
        Comparer<ScheduledTick<T>>.Create(static (left, right) =>
        {
            int comparison = left.Priority.CompareTo(right.Priority);
            return comparison != 0
                ? comparison
                : left.SubTickOrder.CompareTo(right.SubTickOrder);
        });

    public static IEqualityComparer<ScheduledTick<T>> UNIQUE_TICK_HASH { get; } =
        new ScheduledTickEqualityComparer<T>();

    public T Type { get; }
    public BlockPos Pos { get; }
    public long TriggerTick { get; }
    public TickPriority Priority { get; }
    public long SubTickOrder { get; }

    public ScheduledTick(
        T type,
        BlockPos position,
        long triggerTick,
        TickPriority priority,
        long subTickOrder)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        ArgumentNullException.ThrowIfNull(position);
        Pos = position.Immutable();
        TriggerTick = triggerTick;
        Priority = priority ?? throw new ArgumentNullException(nameof(priority));
        SubTickOrder = subTickOrder;
    }

    public ScheduledTick(T type, BlockPos position, long triggerTick, long subTickOrder)
        : this(type, position, triggerTick, TickPriority.NORMAL, subTickOrder)
    {
    }

    public static ScheduledTick<T> Probe(T type, BlockPos position) =>
        new(type, position, 0L, TickPriority.NORMAL, 0L);

    public SavedTick<T> ToSavedTick(long currentTick) =>
        new(Type, Pos, unchecked((int)(TriggerTick - currentTick)), Priority);

    private sealed class ScheduledTickEqualityComparer<TValue> : IEqualityComparer<ScheduledTick<TValue>>
        where TValue : class
    {
        public bool Equals(ScheduledTick<TValue>? left, ScheduledTick<TValue>? right) =>
            ReferenceEquals(left, right) ||
            (left is not null && right is not null &&
                ReferenceEquals(left.Type, right.Type) &&
                left.Pos.Equals(right.Pos));

        public int GetHashCode(ScheduledTick<TValue> tick) =>
            HashCode.Combine(
                RuntimeHelpers.GetHashCode(tick.Type),
                tick.Pos.GetHashCode());
    }
}
