using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Distribution used to select a candidate chunk inside each random-spread grid cell.
/// Values and evaluation order match Minecraft Java 26.3 RandomSpreadType.
/// </summary>
public sealed class RandomSpreadType
{
    public static readonly RandomSpreadType LINEAR = new("linear");
    public static readonly RandomSpreadType TRIANGULAR = new("triangular");

    private static readonly RandomSpreadType[] VALUES_ARRAY =
    {
        LINEAR,
        TRIANGULAR
    };

    public static IReadOnlyList<RandomSpreadType> Values { get; } =
        Array.AsReadOnly(VALUES_ARRAY);

    private static readonly IReadOnlyDictionary<string, RandomSpreadType> BY_NAME =
        VALUES_ARRAY.ToDictionary(static value => value._id, StringComparer.Ordinal);

    public static Codec<RandomSpreadType> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            static name => BY_NAME.TryGetValue(name, out RandomSpreadType? value)
                ? DataResult<RandomSpreadType>.Success(value)
                : DataResult<RandomSpreadType>.Error(
                    () => $"Unknown element name: {name}"),
            static value => value is not null
                ? DataResult<string>.Success(value.GetSerializedName())
                : DataResult<string>.Error("Random spread type cannot be null"));

    private readonly string _id;

    private RandomSpreadType(string id) =>
        _id = id;

    public string GetSerializedName() =>
        _id;

    public int Evaluate(IRandomSource random, int limit)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (ReferenceEquals(this, LINEAR))
            return random.NextInt(limit);

        if (ReferenceEquals(this, TRIANGULAR))
            return (random.NextInt(limit) + random.NextInt(limit)) / 2;

        throw new InvalidOperationException(
            $"Unknown random spread type instance: {_id}");
    }

    public override string ToString() =>
        _id;
}
