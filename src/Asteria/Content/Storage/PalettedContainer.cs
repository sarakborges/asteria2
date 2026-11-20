

namespace Asteria.Content.Storage;

/// <summary>
/// PalettedContainer with a published Data snapshot, strategy-selected palettes, and a one-writer detector.
/// </summary>
public sealed class PalettedContainer<T> : IPaletteResize<T> where T : class
{
    private sealed class Data
    {
        public PaletteConfiguration Configuration { get; }
        public IPalette<T> Palette { get; }
        public IBitStorage Storage { get; }

        public Data(PaletteConfiguration configuration, IPalette<T> palette, IBitStorage storage)
        {
            Configuration = configuration;
            Palette = palette;
            Storage = storage;
        }

        public void CopyFrom(IPalette<T> oldPalette, IBitStorage oldStorage)
        {
            IPaletteResize<T> noResize = NoResizePaletteResize<T>.Instance;

            if (oldPalette.Count == 1)
            {
                int paletteId = Palette.IdFor(oldPalette.ValueFor(0), noResize);
                Storage.Fill(paletteId);
                return;
            }

            int previousOldId = -1;
            int newPaletteId = 0;
            for (int index = 0; index < oldStorage.Size; index++)
            {
                int oldPaletteId = oldStorage.Get(index);
                if (oldPaletteId != previousOldId)
                {
                    newPaletteId = Palette.IdFor(oldPalette.ValueFor(oldPaletteId), noResize);
                    previousOldId = oldPaletteId;
                }

                Storage.Set(index, newPaletteId);
            }
        }

        public Data Copy() =>
            new(Configuration, Palette.Copy(), Storage.Copy());
    }

    private readonly PalettedContainerStrategy<T> _strategy;
    private Data _data;
    private readonly ThreadingDetector _threadingDetector = new("PalettedContainer");

    public int Size => _strategy.EntryCount;
    public PaletteMode Mode => Volatile.Read(ref _data).Configuration.Mode;
    public int BitsPerEntry => Volatile.Read(ref _data).Storage.BitsPerEntry;
    public int BitsInStorage => Volatile.Read(ref _data).Configuration.BitsInStorage;
    public bool AlwaysRepack => Volatile.Read(ref _data).Configuration.AlwaysRepack;
    public int PaletteSize => Volatile.Read(ref _data).Palette.Count;
    public int StorageLongCount => Volatile.Read(ref _data).Storage.LongCount;
    public bool IsSingleValue => Volatile.Read(ref _data).Configuration.Mode == PaletteMode.SingleValue;
    public string? LocalPaletteImplementation =>
        Volatile.Read(ref _data).Configuration.Mode == PaletteMode.Local
            ? Volatile.Read(ref _data).Palette.GetType().Name
            : null;

    public PalettedContainer(T defaultValue, PalettedContainerStrategy<T> strategy)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

        _data = CreateData(_strategy.GetConfigurationForBitCount(0));
        _data.Palette.IdFor(defaultValue, this);
    }

    private PalettedContainer(PalettedContainerStrategy<T> strategy, Data data)
    {
        _strategy = strategy;
        _data = data;
    }

    private Data CreateData(PaletteConfiguration configuration, Data? oldData = null)
    {
        if (oldData is not null && configuration == oldData.Configuration)
        {
            return oldData;
        }

        IPalette<T> palette = configuration.Mode switch
        {
            PaletteMode.SingleValue => new SingleValuePalette<T>(),
            PaletteMode.Local when configuration.BitsInMemory <= 4 =>
                new LinearPalette<T>(configuration.BitsInMemory),
            PaletteMode.Local => new HashMapPalette<T>(configuration.BitsInMemory),
            PaletteMode.Global => new GlobalPalette<T>(_strategy.GlobalMap),
            _ => throw new InvalidOperationException($"Unknown palette mode '{configuration.Mode}'.")
        };

        IBitStorage storage = configuration.BitsInMemory == 0
            ? new ZeroBitStorage(_strategy.EntryCount)
            : new PackedBitStorage(configuration.BitsInMemory, _strategy.EntryCount);

        return new Data(configuration, palette, storage);
    }

    public T Get(int x, int y, int z) =>
        GetByIndex(_strategy.GetIndex(x, y, z));

    private T GetByIndex(int index)
    {
        Data data = Volatile.Read(ref _data);
        int paletteId = data.Storage.Get(index);
        return data.Palette.ValueFor(paletteId);
    }

    public void Set(int x, int y, int z, T value)
    {
        Acquire();
        try
        {
            SetUnchecked(_strategy.GetIndex(x, y, z), value);
        }
        finally
        {
            Release();
        }
    }

    public T GetAndSet(int x, int y, int z, T value)
    {
        Acquire();
        try
        {
            return GetAndSetUncheckedByIndex(_strategy.GetIndex(x, y, z), value);
        }
        finally
        {
            Release();
        }
    }

    public T GetAndSetUnchecked(int x, int y, int z, T value) =>
        GetAndSetUncheckedByIndex(_strategy.GetIndex(x, y, z), value);

    private T GetAndSetUncheckedByIndex(int index, T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Data data = Volatile.Read(ref _data);
        int paletteId = data.Palette.IdFor(value, this);

        // IdFor may resize and publish a new Data snapshot.
        data = Volatile.Read(ref _data);
        int previousId = data.Storage.GetAndSet(index, paletteId);
        return data.Palette.ValueFor(previousId);
    }

    private void SetUnchecked(int index, T value)
    {
        ValidateIndex(index);
        ArgumentNullException.ThrowIfNull(value);

        Data data = Volatile.Read(ref _data);
        int paletteId = data.Palette.IdFor(value, this);

        // IdFor may resize and publish a new Data snapshot.
        data = Volatile.Read(ref _data);
        data.Storage.Set(index, paletteId);
    }

    public void Acquire() => _threadingDetector.CheckAndLock();

    public void Release() => _threadingDetector.CheckAndUnlock();

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        Data data = Volatile.Read(ref _data);
        return data.Palette.MaybeHas(predicate);
    }

    public void ForEachInPalette(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Data data = Volatile.Read(ref _data);
        data.Palette.ForEach(action);
    }

    public void GetAll(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Data data = Volatile.Read(ref _data);

        var seenIds = new HashSet<int>();
        var presentIds = new List<int>();
        data.Storage.GetAll(id =>
        {
            if (seenIds.Add(id))
            {
                presentIds.Add(id);
            }
        });
        foreach (int paletteId in presentIds)
        {
            action(data.Palette.ValueFor(paletteId));
        }
    }

    public void Count(Action<T, int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Data data = Volatile.Read(ref _data);

        if (data.Palette.Count == 1)
        {
            output(data.Palette.ValueFor(0), Size);
            return;
        }

        var counts = new Dictionary<int, int>();
        data.Storage.GetAll(paletteId =>
        {
            counts.TryGetValue(paletteId, out int count);
            counts[paletteId] = count + 1;
        });

        foreach ((int paletteId, int count) in counts)
        {
            output(data.Palette.ValueFor(paletteId), count);
        }
    }

    public PalettedContainer<T> Copy() =>
        new(_strategy, Volatile.Read(ref _data).Copy());

    public PalettedContainer<T> Recreate()
    {
        Data data = Volatile.Read(ref _data);
        return new PalettedContainer<T>(data.Palette.ValueFor(0), _strategy);
    }

    int IPaletteResize<T>.OnResize(int bits, T lastAddedValue) =>
        OnResize(bits, lastAddedValue);

    private int OnResize(int bits, T lastAddedValue)
    {
        Data oldData = Volatile.Read(ref _data);
        PaletteConfiguration configuration = _strategy.GetConfigurationForBitCount(bits);
        Data newData = CreateData(configuration, oldData);
        newData.CopyFrom(oldData.Palette, oldData.Storage);

        // Publish only after all old palette entries have been copied into the replacement.
        Volatile.Write(ref _data, newData);

        return newData.Palette.IdFor(lastAddedValue, NoResizePaletteResize<T>.Instance);
    }

}
