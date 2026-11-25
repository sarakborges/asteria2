namespace Asteria.Serialization.Nbt;

/// <summary>Raised when an NBT payload violates the binary format contract.</summary>
public sealed class NbtFormatException : IOException
{
    public NbtFormatException(string message)
        : base(message)
    {
    }

    public NbtFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
