using Asteria.Serialization;

namespace Asteria.Content.Storage;

/// <summary>
/// PalettedContainer with a published Data snapshot, strategy-selected palettes, and a one-writer detector.
/// </summary>
public sealed class PalettedContainer<T> : IPaletteResize<T>, IPalettedContainerReadOnly<T> where T : class
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
    public string? LocalPaletteImplementation
    {
        get
        {
            Data data = Volatile.Read(ref _data);
            return data.Configuration.Mode == PaletteMode.Local
                ? data.Palette.GetType().Name
                : null;
        }
    }

    public PalettedContainer(T defaultValue, PalettedContainerStrategy<T> strategy)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

        _data = CreateData(_strategy, _strategy.GetConfigurationForBitCount(0));
        _data.Palette.IdFor(defaultValue, this);
    }

    private PalettedContainer(PalettedContainerStrategy<T> strategy, Data data)
    {
        _strategy = strategy;
        _data = data;
    }

    private static Data CreateData(PalettedContainerStrategy<T> strategy, PaletteConfiguration configuration, Data? oldData = null)
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
            PaletteMode.Global => new GlobalPalette<T>(strategy.GlobalMap),
            _ => throw new InvalidOperationException($"Unknown palette mode '{configuration.Mode}'.")
        };

        IBitStorage storage = configuration.BitsInMemory == 0
            ? new ZeroBitStorage(strategy.EntryCount)
            : new PackedBitStorage(configuration.BitsInMemory, strategy.EntryCount);

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


    /// <summary>
    /// Packs this container into palette entries and optional packed words,
    /// matching the local serialization representation of Minecraft Java 26.3.
    /// </summary>

    /// <summary>
    /// Creates the writable local-data codec used for chunk block-state and biome palettes.
    /// Network stream codecs are deliberately separate.
    /// </summary>
    public static Codec<PalettedContainer<T>> CodecRW(
        Codec<T> elementCodec,
        PalettedContainerStrategy<T> strategy,
        T defaultValue)
    {
        Codec<PalettedContainerPackedData<T>> packedCodec =
            CreatePackedDataCodec(elementCodec, defaultValue);

        return packedCodec.FlatXMap(
            packed => Unpack(strategy, packed),
            container => DataResult<PalettedContainerPackedData<T>>.Success(container.Pack(strategy)));
    }

    /// <summary>Creates the read-only local-data codec for a paletted container.</summary>
    public static Codec<IPalettedContainerReadOnly<T>> CodecRO(
        Codec<T> elementCodec,
        PalettedContainerStrategy<T> strategy,
        T defaultValue)
    {
        Codec<PalettedContainerPackedData<T>> packedCodec =
            CreatePackedDataCodec(elementCodec, defaultValue);

        return packedCodec.FlatXMap(
            packed => Unpack(strategy, packed)
                .Map(container => (IPalettedContainerReadOnly<T>)container),
            container => DataResult<PalettedContainerPackedData<T>>.Success(
                container.Pack(strategy)));
    }

    private readonly record struct OptionalStorage(bool IsPresent, IReadOnlyList<long> Values)
    {
        public static OptionalStorage None { get; } =
            new(false, Array.Empty<long>());
    }

    private static OptionalStorage GetOptionalStorage(PalettedContainerPackedData<T> packed)
    {
        ulong[]? storage = packed.Storage;
        if (storage is null)
            return OptionalStorage.None;

        long[] values = Array.ConvertAll(
            storage,
            static value => unchecked((long)value));
        return new OptionalStorage(true, values);
    }

    private static Codec<PalettedContainerPackedData<T>> CreatePackedDataCodec(
        Codec<T> elementCodec,
        T defaultValue)
    {
        ArgumentNullException.ThrowIfNull(elementCodec);
        ArgumentNullException.ThrowIfNull(defaultValue);

        MapCodec<IReadOnlyList<T>> paletteField =
            ExtraCodecs.OrElsePartial(elementCodec, defaultValue)
                .ListOf()
                .FieldOf("palette");

        MapCodec<OptionalStorage> storageField =
            Codecs.LONG.ListOf()
                .XMap(
                    static values => new OptionalStorage(true, values),
                    static optional => optional.Values)
                .OptionalFieldOf("data", OptionalStorage.None);

        return RecordCodecBuilder.Create<PalettedContainerPackedData<T>>(instance =>
            instance.Group(
                paletteField.ForGetter<PalettedContainerPackedData<T>>(
                    static packed => packed.PaletteEntries),
                storageField.ForGetter<PalettedContainerPackedData<T>>(
                    static packed => GetOptionalStorage(packed)))
            .Apply(instance, static (paletteEntries, optionalStorage) =>
                new PalettedContainerPackedData<T>(
                    paletteEntries,
                    optionalStorage.IsPresent
                        ? Array.ConvertAll(
                            optionalStorage.Values.ToArray(),
                            static value => unchecked((ulong)value))
                        : null)));
    }

    public PalettedContainerPackedData<T> Pack() => Pack(_strategy);

    public PalettedContainerPackedData<T> Pack(PalettedContainerStrategy<T> strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        Acquire();
        try
        {
            Data data = Volatile.Read(ref _data);
            var paletteEntries = new List<T>();
            var paletteIds = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
            var packedContents = new int[strategy.EntryCount];

            for (int index = 0; index < packedContents.Length; index++)
            {
                T value = data.Palette.ValueFor(data.Storage.Get(index));
                if (!paletteIds.TryGetValue(value, out int paletteId))
                {
                    paletteId = paletteEntries.Count;
                    paletteEntries.Add(value);
                    paletteIds.Add(value, paletteId);
                }

                packedContents[index] = paletteId;
            }

            PaletteConfiguration storedConfiguration =
                strategy.GetConfigurationForPaletteSize(paletteEntries.Count);
            int bitsPerEntry = storedConfiguration.BitsInStorage;
            ulong[]? storage = bitsPerEntry == 0
                ? null
                : new PackedBitStorage(bitsPerEntry, strategy.EntryCount, packedContents).GetRawCopy();

            return new PalettedContainerPackedData<T>(paletteEntries, storage, bitsPerEntry);
        }
        finally
        {
            Release();
        }
    }

    /// <summary>
    /// Reconstructs a live container from its serialized palette entries and optional packed words.
    /// </summary>
    public static DataResult<PalettedContainer<T>> Unpack(
        PalettedContainerStrategy<T> strategy,
        PalettedContainerPackedData<T> packedData)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(packedData);

        IReadOnlyList<T> paletteEntries = packedData.PaletteEntries;
        int entryCount = strategy.EntryCount;
        PaletteConfiguration storedConfiguration =
            strategy.GetConfigurationForPaletteSize(paletteEntries.Count);
        int bitsOnDisk = storedConfiguration.BitsInStorage;

        if (packedData.BitsPerEntry != PalettedContainerPackedData<T>.UnknownBitsPerEntry &&
            bitsOnDisk != packedData.BitsPerEntry)
        {
            return DataResult<PalettedContainer<T>>.Error(() =>
                $"Invalid bit count, calculated {bitsOnDisk}, but container declared {packedData.BitsPerEntry}");
        }

        Data data = CreateData(strategy, storedConfiguration);
        if (storedConfiguration.BitsInMemory == 0)
        {
            if (paletteEntries.Count == 1)
                data.Palette.IdFor(paletteEntries[0], NoResizePaletteResize<T>.Instance);
            else if (paletteEntries.Count > 1)
                throw new ArgumentException(
                    $"Can't initialize SingleValuePalette with {paletteEntries.Count} values.",
                    nameof(packedData));

            return DataResult<PalettedContainer<T>>.Success(new PalettedContainer<T>(strategy, data));
        }

        if (packedData.Storage is null)
        {
            return DataResult<PalettedContainer<T>>.Error("Missing values for non-zero storage");
        }

        PackedBitStorage oldStorage;
        try
        {
            oldStorage = new PackedBitStorage(
                bitsOnDisk,
                entryCount,
                (ulong[])packedData.Storage.Clone());
        }
        catch (PackedBitStorage.InitializationException exception)
        {
            return DataResult<PalettedContainer<T>>.Error(() =>
                $"Failed to read PalettedContainer: {exception.Message}");
        }

        var oldToNewPaletteIds = new int[paletteEntries.Count];
        for (int index = 0; index < paletteEntries.Count; index++)
        {
            oldToNewPaletteIds[index] = data.Palette.IdFor(
                paletteEntries[index],
                NoResizePaletteResize<T>.Instance);
        }

        if (storedConfiguration.AlwaysRepack ||
            storedConfiguration.BitsInMemory != bitsOnDisk)
        {
            for (int index = 0; index < entryCount; index++)
            {
                int oldPaletteId = oldStorage.Get(index);
                if ((uint)oldPaletteId >= (uint)oldToNewPaletteIds.Length)
                    throw new MissingPaletteEntryException(oldPaletteId);

                data.Storage.Set(index, oldToNewPaletteIds[oldPaletteId]);
            }
        }
        else
        {
            data = new Data(
                storedConfiguration,
                data.Palette,
                new PackedBitStorage(bitsOnDisk, entryCount, (ulong[])packedData.Storage.Clone()));
        }

        return DataResult<PalettedContainer<T>>.Success(new PalettedContainer<T>(strategy, data));
    }

    public PalettedContainer<T> Copy() =>
        new(_strategy, Volatile.Read(ref _data).Copy());

    IPalettedContainerReadOnly<T> IPalettedContainerReadOnly<T>.Copy() => Copy();

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
        Data newData = CreateData(_strategy, configuration, oldData);
        newData.CopyFrom(oldData.Palette, oldData.Storage);

        // Publish only after all old palette entries have been copied into the replacement.
        Volatile.Write(ref _data, newData);

        return newData.Palette.IdFor(lastAddedValue, NoResizePaletteResize<T>.Instance);
    }

}
