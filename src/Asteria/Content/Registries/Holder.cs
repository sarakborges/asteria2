using System.Runtime.CompilerServices;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

public enum HolderKind
{
    Direct,
    Reference
}

/// <summary>
/// Holder equivalent. A value is either a direct, unregistered value or a registry-owned reference.
/// </summary>
public abstract class Holder<T> where T : class
{
    public abstract T Value { get; }
    public abstract HolderKind Kind { get; }
    public abstract bool IsBound { get; }
    public abstract RegistryKey<T>? KeyOrNull { get; }
    public abstract IEnumerable<TagKey<T>> Tags { get; }

    public bool Is(RegistryKey<T> key) =>
        KeyOrNull is RegistryKey<T> ownKey && ownKey.Equals(key);

    public bool Is(ResourceLocation location) =>
        KeyOrNull is RegistryKey<T> ownKey && ownKey.Location.Equals(location);

    public bool Is(TagKey<T> tag) => Tags.Contains(tag);

    public bool Is(Predicate<RegistryKey<T>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return KeyOrNull is RegistryKey<T> key && predicate(key);
    }

    public bool Is(Holder<T> holder)
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

        public DirectHolder(T value)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override T Value => _value;
        public override HolderKind Kind => HolderKind.Direct;
        public override bool IsBound => true;
        public override RegistryKey<T>? KeyOrNull => null;
        public override IEnumerable<TagKey<T>> Tags => Enumerable.Empty<TagKey<T>>();
    }

    /// <summary>
    /// Registry-bound holder, created either with a key or with an intrusive value awaiting registration.
    /// </summary>
    public sealed class Reference : Holder<T>
    {
        private readonly HolderOwner<T> _owner;
        private readonly bool _intrusive;
        private RegistryKey<T>? _key;
        private T? _value;
        private HashSet<TagKey<T>>? _tags;

        internal Reference(HolderOwner<T> owner, RegistryKey<T>? key, T? value, bool intrusive)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _key = key;
            _value = value;
            _intrusive = intrusive;
        }

        public override T Value =>
            _value ?? throw new InvalidOperationException("The holder value has not been bound.");

        public override HolderKind Kind => HolderKind.Reference;
        public override bool IsBound => _key.HasValue && _value is not null;
        public override RegistryKey<T>? KeyOrNull => _key;
        public RegistryKey<T> Key =>
            _key ?? throw new InvalidOperationException("The holder key has not been bound.");
        public bool IsIntrusive => _intrusive;
        public override IEnumerable<TagKey<T>> Tags =>
            _tags is null ? Enumerable.Empty<TagKey<T>>() : _tags;

        public override bool CanSerializeIn(HolderOwner<T> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return _owner.CanSerializeIn(owner);
        }

        internal static Reference CreateStandAlone(HolderOwner<T> owner, RegistryKey<T> key) =>
            new(owner, key, null, false);

        internal static Reference CreateIntrusive(HolderOwner<T> owner, T value) =>
            new(owner, null, value, true);

        internal void BindKey(RegistryKey<T> key)
        {
            if (_key.HasValue)
            {
                throw new InvalidOperationException("A reference holder key can only be bound once.");
            }

            _key = key;
        }

        internal void BindValue(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_value is not null)
            {
                throw new InvalidOperationException("A reference holder value can only be bound once.");
            }

            _value = value;
        }

        internal void BindTags(IEnumerable<TagKey<T>> tags)
        {
            ArgumentNullException.ThrowIfNull(tags);
            _tags = new HashSet<TagKey<T>>(tags);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is not Reference other || !_key.HasValue || !other._key.HasValue)
            {
                return false;
            }

            return _key.Value.Equals(other._key.Value);
        }

        public override int GetHashCode() =>
            _key.HasValue ? _key.Value.GetHashCode() : RuntimeHelpers.GetHashCode(this);

        public override string ToString() =>
            _key is RegistryKey<T> key
                ? $"Reference{{{key}={_value?.ToString() ?? "unbound"}}}"
                : $"IntrusiveReference{{{_value?.ToString() ?? "unbound"}}}";
    }
}
