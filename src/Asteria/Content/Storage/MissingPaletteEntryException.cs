namespace Asteria.Content.Storage;

/// <summary>
/// Raised when a palette is asked to resolve an index that it does not contain.
/// Signals a missing entry in a local palette.
/// </summary>
public sealed class MissingPaletteEntryException : Exception
{
    public int Index { get; }

    public MissingPaletteEntryException(int index)
        : base($"Missing Palette entry for index {index}.")
    {
        Index = index;
    }
}
