namespace Asteria.Serialization.Nbt;

/// <summary>Raised when NBT input exceeds its configured memory or nesting budget.</summary>
public sealed class NbtAccounterException : IOException
{
    public long Usage { get; }
    public long Quota { get; }

    public NbtAccounterException(
        string message,
        long usage,
        long quota,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Usage = usage;
        Quota = quota;
    }
}