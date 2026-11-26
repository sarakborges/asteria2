namespace Asteria.Serialization;

/// <summary>
/// Explicit optional value for codec fields where null is not a valid DataResult value.
/// </summary>
public readonly record struct OptionalValue<T>
{
    private readonly T? _value;

    public bool HasValue { get; }

    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException("Optional value is not present.");

    private OptionalValue(T value)
    {
        _value = value;
        HasValue = true;
    }

    public static OptionalValue<T> Empty => default;

    public static OptionalValue<T> Of(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new OptionalValue<T>(value);
    }
}
