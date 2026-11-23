using Asteria.Core;

namespace Asteria.World.Ticks;

/// <summary>Tick storage used by ProtoChunk before its ticks are promoted to a live chunk.</summary>
public sealed class ProtoChunkTicks<T> : TickContainerAccess<T>, SerializableTickContainer<T>
    where T : class
{
    private readonly List<SavedTick<T>> _ticks = new();
    private readonly HashSet<SavedTick<T>> _ticksPerPosition =
        new(SavedTick<T>.UNIQUE_TICK_HASH);

    public void Schedule(ScheduledTick<T> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        Schedule(new SavedTick<T>(tick.Type, tick.Pos, 0, tick.Priority));
    }

    private void Schedule(SavedTick<T> tick)
    {
        if (_ticksPerPosition.Add(tick))
            _ticks.Add(tick);
    }

    public bool HasScheduledTick(BlockPos position, T type) =>
        _ticksPerPosition.Contains(SavedTick<T>.Probe(type, position));

    public int Count() => _ticks.Count;

    public List<SavedTick<T>> Pack(long currentTick) => _ticks;

    public IReadOnlyList<SavedTick<T>> ScheduledTicks() =>
        Array.AsReadOnly(_ticks.ToArray());

    public static ProtoChunkTicks<T> Load(IEnumerable<SavedTick<T>> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        var result = new ProtoChunkTicks<T>();
        foreach (SavedTick<T> tick in ticks)
            result.Schedule(tick);
        return result;
    }
}
