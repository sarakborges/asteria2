using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// Non-generic base for block-state properties.
/// </summary>
public abstract class StateProperty
{
    private int? _cachedHashCode;

    public string Name { get; }
    public abstract Type ValueType { get; }
    public abstract int ValueCount { get; }

    protected StateProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    internal abstract object GetValueObject(int index);
    internal abstract int GetValueIndex(object value);
    internal abstract string GetSerializedValue(object value);
    internal abstract IStatePropertyValue CreateValue(object value);
    internal abstract BlockState CopyValue(BlockState source, BlockState target);

    public override bool Equals(object? obj) =>
        obj is StateProperty other &&
        ValueType == other.ValueType &&
        string.Equals(Name, other.Name, StringComparison.Ordinal);

    public virtual int GenerateHashCode() =>
        unchecked(31 * RuntimeHelpers.GetHashCode(ValueType) + GetJavaStringHashCode(Name));

    public sealed override int GetHashCode()
    {
        if (_cachedHashCode is int cachedHashCode)
            return cachedHashCode;

        int hashCode = GenerateHashCode();
        _cachedHashCode = hashCode;
        return hashCode;
    }

    private static int GetJavaStringHashCode(string value)
    {
        int hashCode = 0;
        foreach (char character in value)
            hashCode = unchecked(31 * hashCode + character);

        return hashCode;
    }

    internal static bool IsValidName(string value)
    {
        foreach (char character in value)
        {
            if (!(character is >= 'a' and <= 'z') &&
                !(character is >= '0' and <= '9') &&
                character != '_')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    protected static void ValidateSerializedName(string value, string parameterName)
    {
        if (!IsValidName(value))
        {
            throw new ArgumentException(
                $"Invalid serialized property value '{value}'. Values must use lowercase ASCII letters, digits, or underscores.",
                parameterName);
        }
    }
}

/// <summary>
/// A typed property with a finite, ordered set of allowed values.
/// </summary>
public abstract class StateProperty<T> : StateProperty where T : notnull, IComparable
{
    public override Type ValueType => typeof(T);

    public abstract IReadOnlyList<T> PossibleValues { get; }

    protected StateProperty(string name) : base(name)
    {
    }

    public abstract string GetName(T value);
    public abstract bool TryParse(string serializedValue, out T value);

    public virtual int IndexOf(T value)
    {
        for (int index = 0; index < PossibleValues.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(PossibleValues[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    protected int GetPossibleValuesHashCode()
    {
        int hash = 1;
        foreach (T value in PossibleValues)
        {
            int valueHashCode = typeof(T).IsEnum
                ? EnumValueIdentityHashCode<T>.GetHashCode(value)
                : EqualityComparer<T>.Default.GetHashCode(value);
            hash = unchecked(hash * 31 + valueHashCode);
        }

        return hash;
    }

    internal override object GetValueObject(int index) => PossibleValues[index];

    internal override int GetValueIndex(object value) =>
        value is T typedValue ? IndexOf(typedValue) : -1;

    internal override string GetSerializedValue(object value)
    {
        if (value is not T typedValue || IndexOf(typedValue) < 0)
        {
            throw new ArgumentException(
                $"Value '{value}' is not valid for property '{Name}'.",
                nameof(value));
        }

        return GetName(typedValue);
    }

    internal override IStatePropertyValue CreateValue(object value) =>
        new StatePropertyValue<T>(
            this,
            value is T typedValue
                ? typedValue
                : throw new ArgumentException($"Value {value} does not belong to property {this}"));

    internal override BlockState CopyValue(BlockState source, BlockState target) =>
        target.TrySetValue(this, source.GetValue(this));

    public StatePropertyValue<T> Value(T value) =>
        new(this, value);

    public StatePropertyValue<T> Value(BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Value(state.GetValue(this));
    }

    public IEnumerable<StatePropertyValue<T>> GetAllValues() =>
        PossibleValues.Select(Value);

    public override string ToString()
    {
        string propertyTypeName = GetType().Name;
        int genericSuffix = propertyTypeName.IndexOf('`');
        if (genericSuffix >= 0)
        {
            propertyTypeName = propertyTypeName[..genericSuffix];
        }

        string valueClassName = typeof(T) == typeof(bool)
            ? "java.lang.Boolean"
            : typeof(T) == typeof(int)
                ? "java.lang.Integer"
                : (typeof(T).FullName ?? typeof(T).Name).Replace('+', '$');

        string values = string.Join(", ", PossibleValues.Select(FormatValue));
        return $"{propertyTypeName}{{name={Name}, clazz=class {valueClassName}, values=[{values}]}}";
    }

    private static string FormatValue(T value) =>
        value switch
        {
            bool booleanValue => booleanValue ? "true" : "false",
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
}


internal static class EnumValueIdentityHashCode<T> where T : notnull
{
    private static readonly IReadOnlyDictionary<T, object> BoxedValues = CreateBoxedValues();

    public static int GetHashCode(T value) =>
        RuntimeHelpers.GetHashCode(BoxedValues[value]);

    private static IReadOnlyDictionary<T, object> CreateBoxedValues()
    {
        if (!typeof(T).IsEnum)
            throw new InvalidOperationException($"Type '{typeof(T)}' is not an enum.");

        var boxedValues = new Dictionary<T, object>();
        foreach (FieldInfo field in typeof(T)
                     .GetFields(BindingFlags.Public | BindingFlags.Static)
                     .OrderBy(field => field.MetadataToken))
        {
            object boxedValue = field.GetValue(null)!;
            T value = (T)boxedValue;
            boxedValues.TryAdd(value, boxedValue);
        }

        return boxedValues;
    }
}
