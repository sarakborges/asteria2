using Asteria.Core;

namespace Asteria.World.Ticks;

/// <summary>Live chunk tick queue, retaining pending saved ticks until they are unpacked.</summary>
public sealed class LevelChunkTicks<T> : TickContainerAccess<T>, SerializableTickContainer<T>
    where T : class
{
    private PriorityQueue<ScheduledTick<T>, ScheduledTick<T>> _tickQueue =
        new(ScheduledTick<T>.DRAIN_ORDER);
    private List<SavedTick<T>>? _pendingTicks;
    private readonly HashSet<ScheduledTick<T>> _ticksPerPosition =
        new(ScheduledTick<T>.UNIQUE_TICK_HASH);
    private Action<LevelChunkTicks<T>, ScheduledTick<T>>? _onTickAdded;

    public LevelChunkTicks()
    {
    }

    public LevelChunkTicks(List<SavedTick<T>> pendingTicks)
    {
        _pendingTicks = pendingTicks ?? throw new ArgumentNullException(nameof(pendingTicks));
        foreach (SavedTick<T> tick in pendingTicks)
            _ticksPerPosition.Add(ScheduledTick<T>.Probe(tick.Type, tick.Pos));
    }

    public void SetOnTickAdded(Action<LevelChunkTicks<T>, ScheduledTick<T>>? onTickAdded) =>
        _onTickAdded = onTickAdded;

    public ScheduledTick<T>? Peek() =>
        _tickQueue.TryPeek(out ScheduledTick<T>? tick, out _) ? tick : null;

    public ScheduledTick<T>? Poll()
    {
        if (!_tickQueue.TryDequeue(out ScheduledTick<T>? tick, out _))
            return null;

        _ticksPerPosition.Remove(tick);
        return tick;
    }

    public void Schedule(ScheduledTick<T> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        if (_ticksPerPosition.Add(tick))
            ScheduleUnchecked(tick);
    }

    private void ScheduleUnchecked(ScheduledTick<T> tick)
    {
        _tickQueue.Enqueue(tick, tick);
        _onTickAdded?.Invoke(this, tick);
    }

    public bool HasScheduledTick(BlockPos position, T type) =>
        _ticksPerPosition.Contains(ScheduledTick<T>.Probe(type, position));

    public void RemoveIf(Predicate<ScheduledTick<T>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var retained = new List<ScheduledTick<T>>(_tickQueue.Count);
        foreach (PriorityQueue<ScheduledTick<T>, ScheduledTick<T>>.UnorderedItemsCollection entry
            in _tickQueue.UnorderedItems)
        {
            ScheduledTick<T> tick = entry.Element;
            if (predicate(tick))
                _ticksPerPosition.Remove(tick);
            else
                retained.Add(tick);
        }

        _tickQueue = new PriorityQueue<ScheduledTick<T>, ScheduledTick<T>>(
            ScheduledTick<T>.DRAIN_ORDER);
        foreach (ScheduledTick<T> tick in retained)
            _tickQueue.Enqueue(tick, tick);
    }

    public IEnumerable<ScheduledTick<T>> GetAll()
    {
        foreach (var entry in _tickQueue.UnorderedItems)
            yield return entry.Element;
    }

    public int Count() =>
        _tickQueue.Count + (_pendingTicks?.Count ?? 0);

    public List<SavedTick<T>> Pack(long currentTick)
    {
        var result = new List<SavedTick<T>>(
            _tickQueue.Count + (_pendingTicks?.Count ?? 0));
        if (_pendingTicks is not null)
            result.AddRange(_pendingTicks);

        foreach (ScheduledTick<T> tick in GetAll().OrderBy(tick => tick.SubTickOrder))
            result.Add(tick.ToSavedTick(currentTick));

        return result;
    }

    public void Unpack(long currentTick)
    {
        if (_pendingTicks is null)
            return;

        int subTickBase = -_pendingTicks.Count;
        foreach (SavedTick<T> pendingTick in _pendingTicks)
            ScheduleUnchecked(pendingTick.Unpack(currentTick, subTickBase++));

        _pendingTicks = null;
    }
}
