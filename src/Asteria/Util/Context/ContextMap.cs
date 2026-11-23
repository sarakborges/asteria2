namespace Asteria.Util.Context;

/// <summary>
/// Immutable values indexed by typed context keys, matching Minecraft Java 26.3 ContextMap.
/// </summary>
public sealed class ContextMap
{
    public static readonly ContextMap Empty = new(new Dictionary<object, object?>());

    private readonly IReadOnlyDictionary<object, object?> _parameters;

    private ContextMap(IDictionary<object, object?> parameters) =>
        _parameters = new Dictionary<object, object?>(parameters);

    public static Builder CreateBuilder() => new();

    public bool Has<T>(ContextKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _parameters.ContainsKey(key);
    }

    public T? Get<T>(ContextKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _parameters.TryGetValue(key, out object? value)
            ? (T?)value
            : default;
    }

    public T? GetOrDefault<T>(ContextKey<T> key, T? defaultValue)
    {
        T? value = Get(key);
        return value is null ? defaultValue : value;
    }

    public T GetOrThrow<T>(ContextKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_parameters.TryGetValue(key, out object? value) || value is null)
            throw new KeyNotFoundException($"No value present for {key}");
        return (T)value;
    }

    public sealed class Builder
    {
        private readonly Dictionary<object, object?> _parameters = new();

        public Builder WithParameter<T>(ContextKey<T> key, T value)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            _parameters[key] = value;
            return this;
        }

        public Builder WithOptionalParameter<T>(ContextKey<T> key, T? value)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (value is null)
                _parameters.Remove(key);
            else
                _parameters[key] = value;
            return this;
        }

        public T GetParameter<T>(ContextKey<T> key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (!_parameters.TryGetValue(key, out object? value) || value is null)
                throw new KeyNotFoundException($"No value present for {key}");
            return (T)value;
        }

        public T? GetOptionalParameter<T>(ContextKey<T> key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return _parameters.TryGetValue(key, out object? value)
                ? (T?)value
                : default;
        }

        public ContextMap Build() =>
            _parameters.Count == 0 ? Empty : new ContextMap(_parameters);
    }
}
