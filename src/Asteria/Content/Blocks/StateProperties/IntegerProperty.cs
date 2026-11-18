using System.Globalization;

namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// An inclusive integer range property. Values are ordered from minimum to maximum.
/// </summary>
public sealed class IntegerProperty : StateProperty<int>
{
    private readonly IReadOnlyList<int> _values;

    public int Minimum { get; }
    public int Maximum { get; }
    public override IReadOnlyList<int> PossibleValues => _values;
    public override int ValueCount => _values.Count;

    private IntegerProperty(string name, int minimum, int maximum) : base(name)
    {
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum), "Maximum must be greater than or equal to minimum.");
        }

        long count = (long)maximum - minimum + 1L;
        if (count > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum), "The integer property range is too large to materialize.");
        }

        Minimum = minimum;
        Maximum = maximum;

        var values = new int[(int)count];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = (int)((long)minimum + index);
        }

        _values = Array.AsReadOnly(values);
    }

    public static IntegerProperty Create(string name, int minimum, int maximum) =>
        new(name, minimum, maximum);

    public override string GetName(int value)
    {
        if (IndexOf(value) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value is not valid for '{Name}'.");
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    public override bool TryParse(string serializedValue, out int value)
    {
        if (int.TryParse(
                serializedValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value) &&
            IndexOf(value) >= 0)
        {
            return true;
        }

        value = default;
        return false;
    }
}
