using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.Util.Random;

/// <summary>
/// Immutable weighted values with vanilla's flat selector for total weights below 64
/// and compact cumulative selector at 64 or above.
/// </summary>
/// <remarks>
/// The Java network StreamCodec is not included because Asteria has no corresponding
/// network codec layer yet. Data codecs and random selection are implemented here.
/// </remarks>
public sealed class WeightedList<T> : IReadOnlyList<Weighted<T>>, IEquatable<WeightedList<T>>
{
    private const int FLAT_THRESHOLD = 64;

    private readonly IReadOnlyList<Weighted<T>> _items;
    private readonly ISelector? _selector;

    public int TotalWeight { get; }
    public int Count => _items.Count;
    public Weighted<T> this[int index] => _items[index];
    public bool IsEmpty => _selector is null;

    private WeightedList(IEnumerable<Weighted<T>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Weighted<T>[] snapshot = items.ToArray();
        if (snapshot.Any(static item => item is null))
            throw new ArgumentException("Weighted list cannot contain null entries", nameof(items));

        _items = Array.AsReadOnly(snapshot);
        TotalWeight = WeightedRandom.GetTotalWeight(_items, static item => item.Weight);

        _selector = TotalWeight switch
        {
            0 => null,
            < FLAT_THRESHOLD => new FlatSelector(_items, TotalWeight),
            _ => new CompactSelector(_items)
        };
    }

    public static WeightedList<T> Of() => new(Array.Empty<Weighted<T>>());

    public static WeightedList<T> Of(T value) =>
        new(new[] { new Weighted<T>(value, 1) });

    public static WeightedList<T> OfValues(params T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new WeightedList<T>(values.Select(static value => new Weighted<T>(value, 1)));
    }

    public static WeightedList<T> OfWeighted(params Weighted<T>[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new WeightedList<T>(items);
    }

    public static WeightedList<T> FromWeighted(IEnumerable<Weighted<T>> items) =>
        new(items);

    public static Builder CreateBuilder() => new();

    public WeightedList<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new WeightedList<TResult>(_items.Select(item => item.Map(mapper)));
    }

    public bool TryGetRandom(IRandomSource random, out T? value)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (_selector is null)
        {
            value = default;
            return false;
        }

        value = _selector.Get(random.NextInt(TotalWeight));
        return true;
    }

    public T? GetRandomOrThrow(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (_selector is null)
            throw new InvalidOperationException("Weighted list has no elements");

        return _selector.Get(random.NextInt(TotalWeight));
    }

    public IReadOnlyList<Weighted<T>> Unwrap() => _items;

    public bool Contains(T? value)
    {
        foreach (Weighted<T> item in _items)
        {
            if (EqualityComparer<T>.Default.Equals(item.Value, value!))
                return true;
        }
        return false;
    }

    public static Codec<WeightedList<T>> CreateCodec(Codec<T> elementCodec) =>
        CreateCodecCore(Weighted.Codec(elementCodec));

    public static Codec<WeightedList<T>> CreateCodec(MapCodec<T> elementCodec) =>
        CreateCodecCore(Weighted.Codec(elementCodec));

    public static Codec<WeightedList<T>> CreateNonEmptyCodec(Codec<T> elementCodec) =>
        CreateNonEmptyCodecCore(Weighted.Codec(elementCodec));

    public static Codec<WeightedList<T>> CreateNonEmptyCodec(MapCodec<T> elementCodec) =>
        CreateNonEmptyCodecCore(Weighted.Codec(elementCodec));

    private static Codec<WeightedList<T>> CreateCodecCore(Codec<Weighted<T>> weightedCodec) =>
        weightedCodec.ListOf().XMap(
            static items => FromWeighted(items),
            static list => list.Unwrap());

    private static Codec<WeightedList<T>> CreateNonEmptyCodecCore(Codec<Weighted<T>> weightedCodec) =>
        CreateCodecCore(weightedCodec).Validate(static list =>
            list.IsEmpty
                ? DataResult<WeightedList<T>>.Error(
                    "Weighted list must contain at least one entry with non-zero weight")
                : DataResult<WeightedList<T>>.Success(list));

    public bool Equals(WeightedList<T>? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        return other is not null &&
               TotalWeight == other.TotalWeight &&
               _items.SequenceEqual(other._items);
    }

    public override bool Equals(object? obj) =>
        obj is WeightedList<T> other && Equals(other);

    public override int GetHashCode()
    {
        int itemsHash = 1;
        foreach (Weighted<T> item in _items)
            itemsHash = unchecked(itemsHash * 31 + item.GetHashCode());

        return unchecked(31 * TotalWeight + itemsHash);
    }

    public IEnumerator<Weighted<T>> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public sealed class Builder
    {
        private readonly List<Weighted<T>> _result = new();

        public Builder Add(T item) => Add(item, 1);

        public Builder Add(T item, int weight)
        {
            _result.Add(new Weighted<T>(item, weight));
            return this;
        }

        public Builder AddAll(WeightedList<T> other)
        {
            ArgumentNullException.ThrowIfNull(other);
            _result.AddRange(other.Unwrap());
            return this;
        }

        public WeightedList<T> Build() => new(_result);
    }

    private interface ISelector
    {
        T? Get(int selection);
    }

    private sealed class CompactSelector : ISelector
    {
        private readonly Weighted<T>[] _entries;

        public CompactSelector(IReadOnlyList<Weighted<T>> entries) =>
            _entries = entries.ToArray();

        public T? Get(int selection)
        {
            foreach (Weighted<T> entry in _entries)
            {
                selection = unchecked(selection - entry.Weight);
                if (selection < 0)
                    return entry.Value;
            }

            throw new InvalidOperationException($"{selection} exceeded total weight");
        }
    }

    private sealed class FlatSelector : ISelector
    {
        private readonly object?[] _entries;

        public FlatSelector(IReadOnlyList<Weighted<T>> entries, int totalWeight)
        {
            _entries = new object?[totalWeight];
            int index = 0;

            foreach (Weighted<T> entry in entries)
            {
                Array.Fill<object?>(_entries, entry.Value, index, entry.Weight);
                index += entry.Weight;
            }
        }

        public T? Get(int selection) => (T?)_entries[selection];
    }
}
