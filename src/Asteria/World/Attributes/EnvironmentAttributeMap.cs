using System.Collections.ObjectModel;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.Attributes;

/// <summary>Immutable typed attribute modifiers contributed by one environment source.</summary>
public sealed class EnvironmentAttributeMap : IEquatable<EnvironmentAttributeMap>
{
    private static readonly Lazy<Codec<EnvironmentAttributeMap>> CodecValue =
        new(CreateCodec, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<Codec<EnvironmentAttributeMap>> PositionalCodecValue =
        new(CreatePositionalCodec, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IReadOnlyDictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry> _entries;

    public static Codec<EnvironmentAttributeMap> CODEC => CodecValue.Value;
    public static Codec<EnvironmentAttributeMap> CODEC_ONLY_POSITIONAL => PositionalCodecValue.Value;

    public static EnvironmentAttributeMap Empty { get; } =
        new(new Dictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry>());

    public IReadOnlyDictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry> Entries => _entries;

    private static Codec<EnvironmentAttributeMap> CreateCodec()
    {
        Codec<IReadOnlyDictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry>> dispatched =
            Codecs.DispatchedMap(
                BuiltInRegistries.EnvironmentAttribute.ByNameCodec(),
                static attribute => attribute.CreateEntryCodec());

        return dispatched.XMap(
            entries => entries.Count == 0
                ? Empty
                : new EnvironmentAttributeMap(entries.ToDictionary(pair => pair.Key, pair => pair.Value)),
            map => map._entries);
    }

    private static Codec<EnvironmentAttributeMap> CreatePositionalCodec() =>
        CODEC.Validate(map =>
        {
            IEnvironmentAttribute[] illegalAttributes =
                map.KeySet.Where(attribute => !attribute.IsPositional).ToArray();

            return illegalAttributes.Length == 0
                ? DataResult<EnvironmentAttributeMap>.Success(map)
                : DataResult<EnvironmentAttributeMap>.Error(
                    () => $"The following attributes cannot be positional: [{string.Join(", ", illegalAttributes)}]");
        });
    public IReadOnlyCollection<IEnvironmentAttribute> KeySet => _entries.Keys.ToArray();

    private EnvironmentAttributeMap(
        Dictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry> entries) =>
        _entries = new ReadOnlyDictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry>(
            new Dictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry>(entries));

    public static Builder CreateBuilder() => new();

    public bool Contains(IEnvironmentAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return _entries.ContainsKey(attribute);
    }

    public T ApplyModifier<T>(EnvironmentAttribute<T> attribute, T baseValue)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (!_entries.TryGetValue(attribute, out IEnvironmentAttributeEntry? entry))
            return baseValue;

        if (entry is not IEnvironmentAttributeEntry<T> typedEntry)
            throw new InvalidOperationException("The stored attribute entry has an incompatible value type.");

        return typedEntry.ApplyModifier(baseValue);
    }

    public bool Equals(EnvironmentAttributeMap? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null || _entries.Count != other._entries.Count)
            return false;

        foreach (KeyValuePair<IEnvironmentAttribute, IEnvironmentAttributeEntry> pair in _entries)
        {
            if (!other._entries.TryGetValue(pair.Key, out IEnvironmentAttributeEntry? value) ||
                !pair.Value.Equals(value))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) =>
        obj is EnvironmentAttributeMap other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (KeyValuePair<IEnvironmentAttribute, IEnvironmentAttributeEntry> pair in _entries)
            hash ^= HashCode.Combine(pair.Key, pair.Value);
        return hash;
    }

    public sealed class Builder
    {
        private readonly Dictionary<IEnvironmentAttribute, IEnvironmentAttributeEntry> _entries = new();

        internal Builder() { }

        public Builder PutAll(EnvironmentAttributeMap map)
        {
            ArgumentNullException.ThrowIfNull(map);
            foreach (KeyValuePair<IEnvironmentAttribute, IEnvironmentAttributeEntry> entry in map._entries)
                _entries[entry.Key] = entry.Value;
            return this;
        }

        public Builder Modify<TValue, TArgument>(
            EnvironmentAttribute<TValue> attribute,
            AttributeModifier<TValue, TArgument> modifier,
            TArgument argument)
        {
            ArgumentNullException.ThrowIfNull(attribute);
            ArgumentNullException.ThrowIfNull(modifier);
            attribute.Type.CheckAllowedModifier(modifier);
            _entries[attribute] = new Entry<TValue, TArgument>(argument, modifier);
            return this;
        }

        public Builder Set<TValue>(EnvironmentAttribute<TValue> attribute, TValue value) =>
            Modify(attribute, AttributeModifiers.Override<TValue>(), value);

        public EnvironmentAttributeMap Build() =>
            _entries.Count == 0
                ? Empty
                : new EnvironmentAttributeMap(_entries);
    }

    public interface IEnvironmentAttributeEntry
    {
        object? ApplyModifierObject(object? subject);
    }

    public interface IEnvironmentAttributeEntry<T> : IEnvironmentAttributeEntry
    {
        IAttributeModifier<T> Modifier { get; }
        T ApplyModifier(T subject);
    }

    public sealed class Entry<TValue, TArgument> :
        IEnvironmentAttributeEntry<TValue>,
        IEnvironmentAttributeEntry,
        IEquatable<Entry<TValue, TArgument>>
    {
        public TArgument Argument { get; }
        public AttributeModifier<TValue, TArgument> Modifier { get; }

        internal Entry(TArgument argument, AttributeModifier<TValue, TArgument> modifier)
        {
            Argument = argument;
            Modifier = modifier;
        }

        IAttributeModifier<TValue> IEnvironmentAttributeEntry<TValue>.Modifier => Modifier;

        public TValue ApplyModifier(TValue subject) =>
            Modifier.Apply(subject, Argument);

        public object? ApplyModifierObject(object? subject) =>
            ApplyModifier((TValue)subject!);

        public bool Equals(Entry<TValue, TArgument>? other) =>
            other is not null &&
            ReferenceEquals(Modifier, other.Modifier) &&
            EqualityComparer<TArgument>.Default.Equals(Argument, other.Argument);

        public override bool Equals(object? obj) =>
            obj is Entry<TValue, TArgument> other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Modifier, Argument);
    }
}
