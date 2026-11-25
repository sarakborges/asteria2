using Asteria.Serialization;

namespace Asteria.Serialization.Nbt;

/// <summary>
/// Numeric NBT values expose Java Number narrowing and widening conversions.
/// </summary>
public interface NumericTag : PrimitiveTag
{
    sbyte ByteValue();
    short ShortValue();
    int IntValue();
    long LongValue();
    float FloatValue();
    double DoubleValue();
    NumberValue Box();

    NumberValue? Tag.AsNumber() => Box();
    sbyte? Tag.AsByte() => ByteValue();
    short? Tag.AsShort() => ShortValue();
    int? Tag.AsInt() => IntValue();
    long? Tag.AsLong() => LongValue();
    float? Tag.AsFloat() => FloatValue();
    double? Tag.AsDouble() => DoubleValue();
    string? Tag.AsString() => Box().ToString();
}
