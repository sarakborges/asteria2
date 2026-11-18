using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>Defaulted lookup semantics layered on top of MappedRegistry-style holder maps.</summary>
public sealed class DefaultedRegistry<T> : Registry<T> where T : class
{
    private Holder<T>.Reference? _defaultHolder;

    public ResourceLocation DefaultKey { get; }

    public DefaultedRegistry(
        ResourceLocation location,
        ResourceLocation defaultKey,
        bool allowIntrusiveHolders = false)
        : base(location, allowIntrusiveHolders)
    {
        if (!defaultKey.IsValid)
            throw new ArgumentException("A valid default key is required.", nameof(defaultKey));
        DefaultKey = defaultKey;
    }

    public override T Get(ResourceLocation location) =>
        GetValue(location) ?? GetDefaultHolder().Value;

    public override int GetId(T value)
    {
        int id = base.GetId(value);
        return id >= 0 ? id : GetDefaultHolderId();
    }

    public override T GetById(int id) =>
        GetHolderById(id)?.Value ?? GetDefaultHolder().Value;

    public new Holder<T>.Reference? GetHolderById(int id) =>
        base.GetHolderById(id) ?? GetDefaultHolder();

    public override ResourceLocation? GetKey(T value) =>
        base.GetKey(value) ?? GetDefaultHolder().Key.Location;

    public override void Freeze()
    {
        Holder<T>.Reference? defaultHolder = base.GetHolder(DefaultKey);
        if (defaultHolder is null)
            throw new InvalidOperationException(
                $"Cannot freeze registry '{Location}': default entry '{DefaultKey}' was not registered.");

        _defaultHolder = defaultHolder;
        base.Freeze();
    }

    private Holder<T>.Reference GetDefaultHolder()
    {
        if (_defaultHolder is not null)
            return _defaultHolder;

        return base.GetHolder(DefaultKey) ??
            throw new InvalidOperationException(
                $"Default entry '{DefaultKey}' has not been registered in '{Location}'.");
    }

    private int GetDefaultHolderId() => base.GetId(GetDefaultHolder().Value);
}
