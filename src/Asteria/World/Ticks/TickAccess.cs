using Asteria.Core;

namespace Asteria.World.Ticks;

public interface TickAccess<T> where T : class
{
    void Schedule(ScheduledTick<T> tick);
    bool HasScheduledTick(BlockPos position, T type);
    int Count();
}

public interface TickContainerAccess<T> : TickAccess<T> where T : class
{
}

public interface SerializableTickContainer<T> where T : class
{
    List<SavedTick<T>> Pack(long currentTick);
}
