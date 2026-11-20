using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

public enum HolderKind { Direct, Reference }

/// <summary>Represents the two vanilla Holder.unwrap() alternatives.</summary>
public readonly struct HolderUnwrap<T> where T : class
{
    public bool IsKey { get; }
    public RegistryKey<T>? Key { get; }
    public T? Value { get; }

    private HolderUnwrap(RegistryKey<T>? key, T? value)
    {
        IsKey = key.HasValue;
        Key = key;
        Value = value;
    }

    public static HolderUnwrap<T> FromKey(RegistryKey<T> key) => new(key, null);
    public static HolderUnwrap<T> FromValue(T value) =>
        new(null, value ?? throw new ArgumentNullException(nameof(value)));
}

/// <summary>Registry value reference with direct/reference variants.</summary>
public abstract class Holder<T> where T : class
{
    public abstract T Value { get; }
    public abstract HolderKind Kind { get; }
    public abstract bool IsBound { get; }
    public abstract RegistryKey<T>? KeyOrNull { get; }
    public abstract IEnumerable<TagKey<T>> Tags { get; }

    public virtual bool Is(RegistryKey<T> key) =>
        KeyOrNull is RegistryKey<T> ownKey && ownKey.Equals(key);

    public virtual bool Is(ResourceLocation location) =>
        KeyOrNull is RegistryKey<T> ownKey && ownKey.Location.Equals(location);

    public bool Is(TagKey<T> tag) => Tags.Contains(tag);

    public virtual bool Is(Predicate<RegistryKey<T>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return KeyOrNull is RegistryKey<T> key && predicate(key);
    }

    public virtual HolderUnwrap<T> Unwrap() =>
        HolderUnwrap<T>.FromValue(Value);

    public virtual RegistryKey<T>? UnwrapKey() => KeyOrNull;

    public string? GetRegisteredNameIfPresent() =>
        UnwrapKey()?.Location.ToString();

    public string GetRegisteredName() =>
        GetRegisteredNameIfPresent() ?? "[unregistered]";

    public virtual bool Is(Holder<T> holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return Equals(holder);
    }

    public virtual bool CanSerializeIn(HolderOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return false;
    }

    public static Holder<T> Direct(T value) => new DirectHolder(value);

    private sealed class DirectHolder : Holder<T>
    {
        private readonly T _value;
        public DirectHolder(T value) => _value = value ?? throw new ArgumentNullException(nameof(value));
        public override T Value => _value;
        public override HolderKind Kind => HolderKind.Direct;
        public override bool IsBound => true;
        public override RegistryKey<T>? KeyOrNull => null;
        public override IEnumerable<TagKey<T>> Tags => Array.Empty<TagKey<T>>();

        public override bool Is(Holder<T> holder)
        {
            ArgumentNullException.ThrowIfNull(holder);
            return EqualityComparer<T>.Default.Equals(_value, holder.Value);
        }

        public override bool CanSerializeIn(HolderOwner<T> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return true;
        }

        public override bool Equals(object? obj) =>
            obj is DirectHolder other && EqualityComparer<T>.Default.Equals(_value, other._value);

        public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(_value);

        public override string ToString() => $"Direct{{{_value}}}";
    }

    public sealed class Reference : Holder<T>
    {
        private readonly HolderOwner<T> _owner;
        private readonly bool _intrusive;
        private RegistryKey<T>? _key;
        private T? _value;
        private IReadOnlyList<TagKey<T>>? _tags;

        internal Reference(HolderOwner<T> owner, RegistryKey<T>? key, T? value, bool intrusive)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _key = key;
            _value = value;
            _intrusive = intrusive;
        }

        public override T Value =>
            _value ?? throw new InvalidOperationException(
                $"Trying to access unbound value '{_key?.ToString() ?? "null"}' from registry {_owner}.");
        public override HolderKind Kind => HolderKind.Reference;
        public override bool IsBound => _key.HasValue && _value is not null;
        public override RegistryKey<T>? KeyOrNull => _key;
        public RegistryKey<T> Key => _key ?? throw new InvalidOperationException(
            $"Trying to access unbound value '{_value?.ToString() ?? "null"}' from registry {_owner}");
        public bool IsIntrusive => _intrusive;
        public override IEnumerable<TagKey<T>> Tags =>
            _tags ?? throw new InvalidOperationException("Tags not bound");

        public override HolderUnwrap<T> Unwrap() =>
            HolderUnwrap<T>.FromKey(Key);

        public override RegistryKey<T>? UnwrapKey() => Key;

        public override bool Is(RegistryKey<T> key) =>
            Key.Equals(key);

        public override bool Is(ResourceLocation location) =>
            Key.Location.Equals(location);

        public override bool Is(Predicate<RegistryKey<T>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            return predicate(Key);
        }

        public override bool Is(Holder<T> holder)
        {
            ArgumentNullException.ThrowIfNull(holder);
            return holder.Is(Key);
        }

        public override bool CanSerializeIn(HolderOwner<T> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return owner.CanSerializeIn(_owner);
        }

        internal static Reference CreateStandAlone(HolderOwner<T> owner, RegistryKey<T> key) =>
            new(owner, key, null, false);
        internal static Reference CreateIntrusive(HolderOwner<T> owner, T value) =>
            new(owner, null, value, true);

        internal void BindKey(RegistryKey<T> key)
        {
            if (_key.HasValue && !_key.Value.Equals(key))
                throw new InvalidOperationException($"Can't change holder key: existing={_key.Value}, new={key}");
            _key = key;
        }

        internal void BindValue(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_value is not null && !ReferenceEquals(_value, value))
                throw new InvalidOperationException("Can't change holder value.");
            _value = value;
        }

        internal void BindTags(IEnumerable<TagKey<T>> tags)
        {
            ArgumentNullException.ThrowIfNull(tags);
            _tags = Array.AsReadOnly(tags.Distinct().ToArray());
        }

        public override string ToString() =>
            $"Reference{{{_key?.ToString() ?? "null"}={_value?.ToString() ?? "null"}}}";
    }
}
