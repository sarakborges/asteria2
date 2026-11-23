using System.Runtime.CompilerServices;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;

namespace Asteria.World.Ticks;

/// <summary>A block or fluid tick stored relative to a chunk's current game time.</summary>
public sealed record SavedTick<T> where T : class
{
    public static IEqualityComparer<SavedTick<T>> UNIQUE_TICK_HASH { get; } =
        new SavedTickEqualityComparer<T>();

    public T Type { get; }
    public BlockPos Pos { get; }
    public int Delay { get; }
    public TickPriority Priority { get; }

    public SavedTick(T type, BlockPos position, int delay, TickPriority priority)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        ArgumentNullException.ThrowIfNull(position);
        Pos = position.Immutable();
        Delay = delay;
        Priority = priority ?? throw new ArgumentNullException(nameof(priority));
    }

    public static Codec<SavedTick<T>> CreateCodec(Codec<T> typeCodec)
    {
        ArgumentNullException.ThrowIfNull(typeCodec);

        MapCodec<BlockPos> positionCodec = RecordCodecBuilder.MapCodec<BlockPos>(instance =>
            instance.Group(
                Codecs.INT.FieldOf("x").ForGetter<BlockPos>(static position => position.GetX()),
                Codecs.INT.FieldOf("y").ForGetter<BlockPos>(static position => position.GetY()),
                Codecs.INT.FieldOf("z").ForGetter<BlockPos>(static position => position.GetZ()))
            .Apply(instance, static (x, y, z) => new BlockPos(x, y, z)));

        return RecordCodecBuilder.Create<SavedTick<T>>(instance =>
            instance.Group(
                typeCodec.FieldOf("i").ForGetter<SavedTick<T>>(static tick => tick.Type),
                positionCodec.ForGetter<SavedTick<T>>(static tick => tick.Pos),
                Codecs.INT.FieldOf("t").ForGetter<SavedTick<T>>(static tick => tick.Delay),
                TickPriority.CODEC.FieldOf("p").ForGetter<SavedTick<T>>(static tick => tick.Priority))
            .Apply(instance, static (type, position, delay, priority) =>
                new SavedTick<T>(type, position, delay, priority)));
    }

    public static List<SavedTick<T>> FilterTickListForChunk(
        IEnumerable<SavedTick<T>> savedTicks,
        ChunkPos chunkPosition)
    {
        ArgumentNullException.ThrowIfNull(savedTicks);
        long positionKey = ChunkPos.Pack(chunkPosition.X, chunkPosition.Z);
        return savedTicks
            .Where(tick => ChunkPos.Pack(tick.Pos) == positionKey)
            .ToList();
    }

    public ScheduledTick<T> Unpack(long currentTick, long currentSubTick) =>
        new(Type, Pos, unchecked(currentTick + Delay), Priority, currentSubTick);

    public static SavedTick<T> Probe(T type, BlockPos position) =>
        new(type, position, 0, TickPriority.NORMAL);

    private sealed class SavedTickEqualityComparer<TValue> : IEqualityComparer<SavedTick<TValue>>
        where TValue : class
    {
        public bool Equals(SavedTick<TValue>? left, SavedTick<TValue>? right) =>
            ReferenceEquals(left, right) ||
            (left is not null && right is not null &&
                ReferenceEquals(left.Type, right.Type) &&
                left.Pos.Equals(right.Pos));

        public int GetHashCode(SavedTick<TValue> tick) =>
            HashCode.Combine(
                RuntimeHelpers.GetHashCode(tick.Type),
                tick.Pos.GetHashCode());
    }
}
