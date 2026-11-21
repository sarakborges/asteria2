using Asteria.Content.Registries;

namespace Asteria.Serialization;

/// <summary>
/// Format-independent operations for constructing, inspecting, merging and converting
/// serialized values. Implementations provide the primitive and container operations;
/// shared conversions follow the DataFixerUpper DynamicOps contract.
/// </summary>
public interface DynamicOps<T>
{
    T Empty { get; }
    bool CompressMaps { get; }
    bool IsEmpty(T input);

    DataResult<NumberValue> GetNumberValue(T input);
    T CreateNumeric(NumberValue value);
    DataResult<bool> GetBooleanValue(T input);
    T CreateBoolean(bool value);
    DataResult<string> GetStringValue(T input);
    T CreateString(string value);

    DataResult<IReadOnlyList<KeyValuePair<T, T>>> GetMapValues(T input);

    DataResult<MapLike<T>> GetMap(T input) =>
        GetMapValues(input).Map(entries => new MapLike<T>(this, input, entries));
    T CreateMap(IEnumerable<KeyValuePair<T, T>> values);
    DataResult<IReadOnlyList<T>> GetList(T input);
    T CreateList(IEnumerable<T> values);

    DataResult<T> Get(T input, string key);
    DataResult<T> MergeToList(T list, T value);
    DataResult<T> MergeToMap(T map, T key, T value);
    T Remove(T input, string key);

    T EmptyMap => CreateMap(Array.Empty<KeyValuePair<T, T>>());
    T EmptyList => CreateList(Array.Empty<T>());

    T CreateByte(sbyte value) => CreateNumeric(NumberValue.Of(value));
    T CreateUnsignedByte(byte value) => CreateNumeric(NumberValue.Of(value));
    T CreateShort(short value) => CreateNumeric(NumberValue.Of(value));
    T CreateUnsignedShort(ushort value) => CreateNumeric(NumberValue.Of(value));
    T CreateInt(int value) => CreateNumeric(NumberValue.Of(value));
    T CreateUnsignedInt(uint value) => CreateNumeric(NumberValue.Of(value));
    T CreateLong(long value) => CreateNumeric(NumberValue.Of(value));
    T CreateUnsignedLong(ulong value) => CreateNumeric(NumberValue.Of(value));
    T CreateFloat(float value) => CreateNumeric(NumberValue.Of(value));
    T CreateDouble(double value) => CreateNumeric(NumberValue.Of(value));

    DataResult<T> MergeToList(T list, IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        DataResult<T> result = DataResult<T>.Success(list);
        foreach (T value in values)
            result = result.FlatMap(current => MergeToList(current, value));
        return result;
    }

    DataResult<T> MergeToMap(T map, IReadOnlyList<KeyValuePair<T, T>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        DataResult<T> result = DataResult<T>.Success(map);
        foreach (KeyValuePair<T, T> pair in values)
            result = result.FlatMap(current => MergeToMap(current, pair.Key, pair.Value));
        return result;
    }

    DataResult<T> MergeToPrimitive(T prefix, T value) =>
        IsEmpty(prefix)
            ? DataResult<T>.Success(value)
            : DataResult<T>.Error(
                () => $"Do not know how to append a primitive value {value} to {prefix}",
                value);

    bool TryGetNumber(T input, out NumberValue value)
    {
        DataResult<NumberValue> result = GetNumberValue(input);
        if (result.TryGetResult(out value))
            return true;

        value = default;
        return false;
    }

    T Set(T input, string key, T value)
    {
        DataResult<T> result = MergeToMap(input, CreateString(key), value);
        return result.TryGetResult(out T merged) ? merged : input;
    }

    T Update(T input, string key, Func<T, T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        DataResult<T> current = Get(input, key);
        return current.TryGetResult(out T value)
            ? Set(input, key, function(value))
            : input;
    }

    U ConvertTo<U>(DynamicOps<U> outputOps, T input)
    {
        ArgumentNullException.ThrowIfNull(outputOps);

        DataResult<IReadOnlyList<KeyValuePair<T, T>>> map = GetMapValues(input);
        if (map.TryGetResult(out IReadOnlyList<KeyValuePair<T, T>> entries))
        {
            var converted = new List<KeyValuePair<U, U>>(entries.Count);
            foreach (KeyValuePair<T, T> entry in entries)
            {
                converted.Add(new KeyValuePair<U, U>(
                    ConvertTo(outputOps, entry.Key),
                    ConvertTo(outputOps, entry.Value)));
            }
            return outputOps.CreateMap(converted);
        }

        DataResult<IReadOnlyList<T>> list = GetList(input);
        if (list.TryGetResult(out IReadOnlyList<T> elements))
        {
            var converted = new List<U>(elements.Count);
            foreach (T element in elements)
                converted.Add(ConvertTo(outputOps, element));
            return outputOps.CreateList(converted);
        }

        DataResult<string> stringValue = GetStringValue(input);
        if (stringValue.TryGetResult(out string text))
            return outputOps.CreateString(text);

        DataResult<bool> booleanValue = GetBooleanValue(input);
        if (booleanValue.TryGetResult(out bool boolean))
            return outputOps.CreateBoolean(boolean);

        DataResult<NumberValue> numberValue = GetNumberValue(input);
        if (numberValue.TryGetResult(out NumberValue number))
        {
            if (number.TryGetInt64Exact(out long integral))
            {
                if ((sbyte)integral == integral)
                    return outputOps.CreateByte((sbyte)integral);
                if ((short)integral == integral)
                    return outputOps.CreateShort((short)integral);
                if ((int)integral == integral)
                    return outputOps.CreateInt((int)integral);
                return outputOps.CreateLong(integral);
            }

            double floating = number.DoubleValue;
            float single = (float)floating;
            return (double)single == floating
                ? outputOps.CreateFloat(single)
                : outputOps.CreateDouble(floating);
        }

        return outputOps.Empty;
    }
}
