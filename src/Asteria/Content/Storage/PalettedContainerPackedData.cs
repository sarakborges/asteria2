namespace Asteria.Content.Storage;

/// <summary>
/// Serialized palette entries and optional packed words for a paletted container.
/// A null storage array represents zero-bit, single-value storage.
/// </summary>
public sealed class PalettedContainerPackedData<T> where T : class
{
    public const int UnknownBitsPerEntry = -1;

    public IReadOnlyList<T> PaletteEntries { get; }
    public ulong[]? Storage { get; }
    public int BitsPerEntry { get; }

    public PalettedContainerPackedData(
        IReadOnlyList<T> paletteEntries,
        ulong[]? storage,
        int bitsPerEntry = UnknownBitsPerEntry)
    {
        ArgumentNullException.ThrowIfNull(paletteEntries);
        if (paletteEntries.Any(static value => value is null))
            throw new ArgumentException("Palette entries cannot contain null values.", nameof(paletteEntries));

        PaletteEntries = Array.AsReadOnly(paletteEntries.ToArray());
        Storage = storage is null ? null : (ulong[])storage.Clone();
        BitsPerEntry = bitsPerEntry;
    }
}
