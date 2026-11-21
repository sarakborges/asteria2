using System.Globalization;

namespace Asteria.World.Attributes;

/// <summary>Validation and runtime sanitization policy for an attribute value.</summary>
public sealed class AttributeRange<T>
{
    private readonly Func<T, bool> _isValid;
    private readonly Func<T, T> _sanitize;
    private readonly Func<T, string>? _errorMessage;

    private AttributeRange(
        Func<T, bool> isValid,
        Func<T, T> sanitize,
        Func<T, string>? errorMessage)
    {
        _isValid = isValid ?? throw new ArgumentNullException(nameof(isValid));
        _sanitize = sanitize ?? throw new ArgumentNullException(nameof(sanitize));
        _errorMessage = errorMessage;
    }

    public bool IsValid(T value) => _isValid(value);

    public bool TryValidate(T value, out string? error)
    {
        if (_isValid(value))
        {
            error = null;
            return true;
        }

        error = _errorMessage?.Invoke(value) ??
            $"{Convert.ToString(value, CultureInfo.InvariantCulture)} is outside the allowed range.";
        return false;
    }

    public T Sanitize(T value) => _sanitize(value);

    public static AttributeRange<T> Any() =>
        new(static _ => true, static value => value, null);

    public static AttributeRange<float> OfFloat(float minValue, float maxValue) =>
        new(
            value => value >= minValue && value <= maxValue,
            value => value >= minValue && value <= maxValue
                ? value
                : Math.Clamp(value, minValue, maxValue),
            value => FormattableString.Invariant(
                $"{value} is not in range [{minValue}; {maxValue}]"));

    public static AttributeRange<float> UnitFloat { get; } =
        OfFloat(0.0F, 1.0F);

    public static AttributeRange<float> UnitFloatEpsilon { get; } =
        OfFloat(0.0F, 0.9999999F);

    public static AttributeRange<float> NonNegativeFloat { get; } =
        OfFloat(0.0F, float.PositiveInfinity);
}
