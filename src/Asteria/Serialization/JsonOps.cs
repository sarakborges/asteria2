using System.Globalization;
using System.Text.Json;

namespace Asteria.Serialization;

/// <summary>
/// JSON implementation of DynamicOps using System.Text.Json's immutable JsonElement values.
/// </summary>
public sealed class JsonOps : DynamicOps<JsonElement>
{
    public static JsonOps INSTANCE { get; } = new(compressMaps: false);
    public static JsonOps COMPRESSED { get; } = new(compressMaps: true);

    public bool CompressMaps { get; }

    private JsonOps(bool compressMaps) =>
        CompressMaps = compressMaps;

    public JsonElement Empty => Serialize<object?>(null);

    public JsonElement EmptyMap => Serialize(new Dictionary<string, JsonElement>());

    public JsonElement EmptyList => Serialize(Array.Empty<JsonElement>());

    public bool IsEmpty(JsonElement input) =>
        input.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    public DataResult<NumberValue> GetNumberValue(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Number)
        {
            try
            {
                return DataResult<NumberValue>.Success(NumberValue.Parse(input.GetRawText()));
            }
            catch (FormatException exception)
            {
                return DataResult<NumberValue>.Error(() => $"Not a number: {exception} {Format(input)}");
            }
        }

        if (CompressMaps && input.ValueKind == JsonValueKind.String)
        {
            string text = input.GetString() ?? string.Empty;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                return DataResult<NumberValue>.Success(NumberValue.Of(integer));

            return DataResult<NumberValue>.Error(() => $"Not a number: {text} {Format(input)}");
        }

        return DataResult<NumberValue>.Error(() => $"Not a number: {Format(input)}");
    }

    public JsonElement CreateNumeric(NumberValue value) =>
        Parse(value.ToString());

    public JsonElement CreateByte(sbyte value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateUnsignedByte(byte value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateShort(short value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateUnsignedShort(ushort value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateInt(int value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateUnsignedInt(uint value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateLong(long value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateUnsignedLong(ulong value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateFloat(float value) => CreateNumeric(NumberValue.Of(value));
    public JsonElement CreateDouble(double value) => CreateNumeric(NumberValue.Of(value));


    public DataResult<bool> GetBooleanValue(JsonElement input) =>
        input.ValueKind == JsonValueKind.True
            ? DataResult<bool>.Success(true)
            : input.ValueKind == JsonValueKind.False
                ? DataResult<bool>.Success(false)
                : DataResult<bool>.Error(() => $"Not a boolean: {Format(input)}");

    public JsonElement CreateBoolean(bool value) =>
        Serialize(value);

    public DataResult<string> GetStringValue(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.String)
            return DataResult<string>.Success(input.GetString() ?? string.Empty);

        if (CompressMaps && input.ValueKind == JsonValueKind.Number)
            return DataResult<string>.Success(input.GetRawText());

        return DataResult<string>.Error(() => $"Not a string: {Format(input)}");
    }

    public JsonElement CreateString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Serialize(value);
    }

    public DataResult<JsonElement> Get(JsonElement input, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (input.ValueKind != JsonValueKind.Object)
            return DataResult<JsonElement>.Error(() => $"No element {key} in the map {Format(input)}");

        if (!input.TryGetProperty(key, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            return DataResult<JsonElement>.Error(() => $"No element {key} in the map {Format(input)}");

        return DataResult<JsonElement>.Success(value.Clone());
    }

    public DataResult<IReadOnlyList<KeyValuePair<JsonElement, JsonElement>>> GetMapValues(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object)
            return DataResult<IReadOnlyList<KeyValuePair<JsonElement, JsonElement>>>.Error(
                () => $"Not a JSON object: {Format(input)}");

        var entries = new List<KeyValuePair<JsonElement, JsonElement>>();
        foreach (JsonProperty property in input.EnumerateObject())
        {
            entries.Add(new KeyValuePair<JsonElement, JsonElement>(
                CreateString(property.Name),
                property.Value.Clone()));
        }

        return DataResult<IReadOnlyList<KeyValuePair<JsonElement, JsonElement>>>.Success(entries);
    }

    public JsonElement CreateMap(IEnumerable<KeyValuePair<JsonElement, JsonElement>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (KeyValuePair<JsonElement, JsonElement> entry in values)
        {
            string key = GetStringValue(entry.Key).GetOrThrow();
            map[key] = entry.Value.Clone();
        }

        return Serialize(map);
    }

    public DataResult<IReadOnlyList<JsonElement>> GetList(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Array)
            return DataResult<IReadOnlyList<JsonElement>>.Error(
                () => $"Not a JSON array: {Format(input)}");

        var values = new List<JsonElement>(input.GetArrayLength());
        foreach (JsonElement element in input.EnumerateArray())
            values.Add(element.Clone());

        return DataResult<IReadOnlyList<JsonElement>>.Success(values);
    }

    public JsonElement CreateList(IEnumerable<JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Serialize(values.Select(value => value.Clone()).ToArray());
    }

    public DataResult<JsonElement> MergeToList(JsonElement list, JsonElement value)
    {
        if (list.ValueKind != JsonValueKind.Array && !IsEmpty(list))
            return DataResult<JsonElement>.Error(
                () => $"mergeToList called with not a list: {Format(list)}",
                list);

        var result = new List<JsonElement>();
        if (!IsEmpty(list))
        {
            foreach (JsonElement element in list.EnumerateArray())
                result.Add(element.Clone());
        }

        result.Add(value.Clone());
        return DataResult<JsonElement>.Success(Serialize(result));
    }

    public DataResult<JsonElement> MergeToList(
        JsonElement list,
        IReadOnlyList<JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (list.ValueKind != JsonValueKind.Array && !IsEmpty(list))
            return DataResult<JsonElement>.Error(
                () => $"mergeToList called with not a list: {Format(list)}",
                list);

        if (values.Count == 0)
            return DataResult<JsonElement>.Success(IsEmpty(list) ? EmptyList : list);

        var result = new List<JsonElement>();
        if (!IsEmpty(list))
        {
            foreach (JsonElement element in list.EnumerateArray())
                result.Add(element.Clone());
        }

        foreach (JsonElement element in values)
            result.Add(element.Clone());

        return DataResult<JsonElement>.Success(Serialize(result));
    }

    public DataResult<JsonElement> MergeToMap(
        JsonElement map,
        JsonElement key,
        JsonElement value)
    {
        if (map.ValueKind != JsonValueKind.Object && !IsEmpty(map))
            return DataResult<JsonElement>.Error(
                () => $"mergeToMap called with not a map: {Format(map)}",
                map);

        DataResult<string> stringKey = GetStringValue(key);
        if (!stringKey.TryGetResult(out string name))
            return DataResult<JsonElement>.Error(() => $"key is not a string: {Format(key)}", map);

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!IsEmpty(map))
        {
            foreach (JsonProperty property in map.EnumerateObject())
                result[property.Name] = property.Value.Clone();
        }

        result[name] = value.Clone();
        return DataResult<JsonElement>.Success(Serialize(result));
    }

    public DataResult<JsonElement> MergeToMap(
        JsonElement map,
        IReadOnlyList<KeyValuePair<JsonElement, JsonElement>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (map.ValueKind != JsonValueKind.Object && !IsEmpty(map))
            return DataResult<JsonElement>.Error(
                () => $"mergeToMap called with not a map: {Format(map)}",
                map);

        if (values.Count == 0)
            return DataResult<JsonElement>.Success(IsEmpty(map) ? EmptyMap : map);

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!IsEmpty(map))
        {
            foreach (JsonProperty property in map.EnumerateObject())
                result[property.Name] = property.Value.Clone();
        }

        var missed = new List<string>();
        foreach (KeyValuePair<JsonElement, JsonElement> entry in values)
        {
            DataResult<string> stringKey = GetStringValue(entry.Key);
            if (!stringKey.TryGetResult(out string name))
            {
                missed.Add(Format(entry.Key));
                continue;
            }

            result[name] = entry.Value.Clone();
        }

        JsonElement output = Serialize(result);
        if (missed.Count > 0)
        {
            string missedKeys = string.Join(", ", missed);
            return DataResult<JsonElement>.Error(
                () => $"some keys are not strings: [{missedKeys}]",
                output);
        }

        return DataResult<JsonElement>.Success(output);
    }

    public JsonElement Remove(JsonElement input, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (input.ValueKind != JsonValueKind.Object)
            return input;

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in input.EnumerateObject())
        {
            if (!StringComparer.Ordinal.Equals(property.Name, key))
                result[property.Name] = property.Value.Clone();
        }

        return Serialize(result);
    }


    public DataResult<JsonElement> MergeToPrimitive(JsonElement prefix, JsonElement value) =>
        IsEmpty(prefix)
            ? DataResult<JsonElement>.Success(value.Clone())
            : DataResult<JsonElement>.Error(
                () => $"Do not know how to append a primitive value {Format(value)} to {Format(prefix)}",
                value.Clone());

    public bool TryGetNumber(JsonElement input, out NumberValue value)
    {
        DataResult<NumberValue> result = GetNumberValue(input);
        if (result.TryGetResult(out value))
            return true;

        value = default;
        return false;
    }

    public JsonElement Set(JsonElement input, string key, JsonElement value)
    {
        DataResult<JsonElement> result = MergeToMap(input, CreateString(key), value);
        return result.TryGetResult(out JsonElement merged) ? merged : input;
    }

    public JsonElement Update(JsonElement input, string key, Func<JsonElement, JsonElement> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        DataResult<JsonElement> current = Get(input, key);
        return current.TryGetResult(out JsonElement value)
            ? Set(input, key, function(value))
            : input;
    }

    public U ConvertTo<U>(DynamicOps<U> outputOps, JsonElement input)
    {
        ArgumentNullException.ThrowIfNull(outputOps);

        DataResult<IReadOnlyList<KeyValuePair<JsonElement, JsonElement>>> map = GetMapValues(input);
        if (map.TryGetResult(out IReadOnlyList<KeyValuePair<JsonElement, JsonElement>> entries))
        {
            var converted = new List<KeyValuePair<U, U>>(entries.Count);
            foreach (KeyValuePair<JsonElement, JsonElement> entry in entries)
            {
                converted.Add(new KeyValuePair<U, U>(
                    ConvertTo(outputOps, entry.Key),
                    ConvertTo(outputOps, entry.Value)));
            }

            return outputOps.CreateMap(converted);
        }

        DataResult<IReadOnlyList<JsonElement>> list = GetList(input);
        if (list.TryGetResult(out IReadOnlyList<JsonElement> elements))
        {
            var converted = new List<U>(elements.Count);
            foreach (JsonElement element in elements)
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

    public override string ToString() =>
        "JSON";

    private static JsonElement Serialize<TValue>(TValue value) =>
        JsonSerializer.SerializeToElement(value);

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Format(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? "undefined" : value.GetRawText();
}
