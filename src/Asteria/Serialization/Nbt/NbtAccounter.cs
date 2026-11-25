namespace Asteria.Serialization.Nbt;

/// <summary>
/// Bounds NBT allocations and recursive nesting while a value tree is read.
/// </summary>
public sealed class NbtAccounter
{
    public const long DEFAULT_NBT_QUOTA = 2_097_152L;
    public const long UNCOMPRESSED_NBT_QUOTA = 104_857_600L;
    public const int MAX_STACK_DEPTH = 512;

    private readonly long _quota;
    private readonly int _maxDepth;
    private long _usage;
    private int _depth;

    public long Usage => _usage;
    public long Quota => _quota;
    public int Depth => _depth;
    public int MaxDepth => _maxDepth;

    public NbtAccounter(long quota, int maxDepth = MAX_STACK_DEPTH)
    {
        if (quota < 0)
            throw new ArgumentOutOfRangeException(nameof(quota));
        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        _quota = quota;
        _maxDepth = maxDepth;
    }

    public static NbtAccounter Create(long quota) =>
        new(quota);

    public static NbtAccounter DefaultQuota() =>
        Create(DEFAULT_NBT_QUOTA);

    public static NbtAccounter UncompressedQuota() =>
        Create(UNCOMPRESSED_NBT_QUOTA);

    public static NbtAccounter UnlimitedHeap() =>
        new(long.MaxValue, int.MaxValue);

    public void AccountBytes(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        long nextUsage;
        try
        {
            nextUsage = checked(_usage + bytes);
        }
        catch (OverflowException exception)
        {
            throw new NbtAccounterException(
                $"NBT memory accounting overflowed while adding {bytes} bytes.",
                _usage,
                _quota,
                exception);
        }

        if (nextUsage > _quota)
        {
            throw new NbtAccounterException(
                $"Tried to read NBT tag that was too big; tried to allocate: {nextUsage} bytes where max allowed: {_quota}",
                nextUsage,
                _quota);
        }

        _usage = nextUsage;
    }

    public void AccountBytes(long bytesPerEntry, long count)
    {
        if (bytesPerEntry < 0)
            throw new ArgumentOutOfRangeException(nameof(bytesPerEntry));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        long total;
        try
        {
            total = checked(bytesPerEntry * count);
        }
        catch (OverflowException exception)
        {
            throw new NbtAccounterException(
                $"NBT memory accounting overflowed while multiplying {bytesPerEntry} by {count}.",
                _usage,
                _quota,
                exception);
        }

        AccountBytes(total);
    }

    public void PushDepth()
    {
        if (_depth >= _maxDepth)
        {
            throw new NbtAccounterException(
                $"Tried to read NBT tag with too high complexity, depth > {_maxDepth}",
                _usage,
                _quota);
        }

        _depth++;
    }

    public void PopDepth()
    {
        if (_depth == 0)
            throw new InvalidOperationException("NBT depth cannot be decremented below zero.");

        _depth--;
    }

    /// <summary>
    /// Accounts for the UTF-16 storage of a string and returns the unchanged string.
    /// </summary>
    public string ReadUTF(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        AccountBytes(sizeof(char), value.Length);
        return value;
    }
}