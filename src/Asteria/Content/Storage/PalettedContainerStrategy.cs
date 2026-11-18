using Asteria.Content.Registries;

namespace Asteria.Content.Storage;

/// <summary>
/// Strategy for vanilla paletted containers. Block states and biomes use separate cubic dimensions and thresholds.
/// </summary>
public sealed class PalettedContainerStrategy<T> where T : class
{
    private enum StrategyKind
    {
        BlockStates,
        Biomes
    }

    private readonly StrategyKind _kind;

    public IIdMap<T> GlobalMap { get; }
    public int BitsPerAxis { get; }
    public int EntryCount { get; }
    public int GlobalPaletteBitsInMemory { get; }

    private PalettedContainerStrategy(IIdMap<T> globalMap, int bitsPerAxis, StrategyKind kind)
    {
        GlobalMap = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
        if (!GlobalMap.IsFrozen)
        {
            throw new InvalidOperationException("The global ID map must be frozen before creating a palette strategy.");
        }

        if (GlobalMap.Count <= 0)
        {
            throw new ArgumentException("The global ID map must contain at least one value.", nameof(globalMap));
        }

        if (bitsPerAxis is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerAxis));
        }

        BitsPerAxis = bitsPerAxis;
        EntryCount = 1 << (bitsPerAxis * 3);
        GlobalPaletteBitsInMemory = PackedBitStorage.RequiredBitsForValueCount(GlobalMap.Count);
        _kind = kind;
    }

    public static PalettedContainerStrategy<T> CreateForBlockStates(IIdMap<T> globalMap) =>
        new(globalMap, 4, StrategyKind.BlockStates);

    public static PalettedContainerStrategy<T> CreateForBiomes(IIdMap<T> globalMap) =>
        new(globalMap, 2, StrategyKind.Biomes);

    public int GetIndex(int x, int y, int z) =>
        (y << BitsPerAxis | z) << BitsPerAxis | x;

    public PaletteConfiguration GetConfigurationForBitCount(int bits)
    {
        if (bits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bits));
        }

        if (bits == 0)
        {
            return new PaletteConfiguration(PaletteMode.SingleValue, 0);
        }

        if (_kind == StrategyKind.BlockStates)
        {
            if (bits <= 4)
            {
                return new PaletteConfiguration(PaletteMode.Local, 4);
            }

            if (bits <= 8)
            {
                return new PaletteConfiguration(PaletteMode.Local, bits);
            }
        }
        else if (bits <= 3)
        {
            return new PaletteConfiguration(PaletteMode.Local, bits);
        }

        return new PaletteConfiguration(PaletteMode.Global, GlobalPaletteBitsInMemory);
    }
}

public readonly record struct PaletteConfiguration(PaletteMode Mode, int BitsPerEntry);
