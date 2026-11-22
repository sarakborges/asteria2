using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Core.Mathematics;
using Asteria.Serialization;
using Asteria.World.LevelGen;

namespace Asteria.Content.Blocks;

/// <summary>
/// The four horizontal block rotations used by the source block-position and direction APIs.
/// </summary>
public sealed class Rotation
{
    public static readonly Rotation NONE =
        new(0, "NONE", "none", OctahedralGroup.IDENTITY);
    public static readonly Rotation CLOCKWISE_90 =
        new(1, "CLOCKWISE_90", "clockwise_90", OctahedralGroup.ROT_90_Y_NEG);
    public static readonly Rotation CLOCKWISE_180 =
        new(2, "CLOCKWISE_180", "180", OctahedralGroup.ROT_180_FACE_XZ);
    public static readonly Rotation COUNTERCLOCKWISE_90 =
        new(3, "COUNTERCLOCKWISE_90", "counterclockwise_90", OctahedralGroup.ROT_90_Y_POS);

    private static readonly Rotation[] VALUES =
    {
        NONE,
        CLOCKWISE_90,
        CLOCKWISE_180,
        COUNTERCLOCKWISE_90
    };

    /// <summary>Continuous ID lookup with wrapping for values outside the enum range.</summary>
    public static readonly Func<int, Rotation> BY_ID = GetById;

    public static readonly Codec<Rotation> CODEC =
        Codecs.STRING.FlatXMap(
            id => BySerializedName(id) is { } rotation
                ? DataResult<Rotation>.Success(rotation)
                : DataResult<Rotation>.Error(() => "Unknown rotation: " + id),
            rotation => DataResult<string>.Success(
                rotation?.GetSerializedName() ?? throw new ArgumentNullException(nameof(rotation))))
        .WithLifecycle(Lifecycle.Stable());

    [Obsolete("Legacy enum-name codec.")]
    public static readonly Codec<Rotation> LEGACY_CODEC =
        Codecs.STRING.FlatXMap(
            name => ByLegacyName(name) is { } rotation
                ? DataResult<Rotation>.Success(rotation)
                : DataResult<Rotation>.Error(() => "No value with id: " + name),
            rotation => DataResult<string>.Success(
                rotation?.ToString() ?? throw new ArgumentNullException(nameof(rotation))));

    private readonly int _index;
    private readonly string _name;
    private readonly string _serializedName;
    private readonly OctahedralGroup _rotation;

    private Rotation(
        int index,
        string name,
        string serializedName,
        OctahedralGroup rotation)
    {
        _index = index;
        _name = name;
        _serializedName = serializedName;
        _rotation = rotation;
    }

    public static Rotation[] GetValues() => (Rotation[])VALUES.Clone();

    public Rotation GetRotated(Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);
        return BY_ID(unchecked(_index + rotation._index));
    }

    public OctahedralGroup GetRotation() => _rotation;

    public Direction Rotate(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);

        if (ReferenceEquals(direction.GetAxis(), Direction.Axis.Y))
            return direction;

        return _index switch
        {
            2 => direction.GetOpposite(),
            3 => direction.GetCounterClockWise(),
            1 => direction.GetClockWise(),
            _ => direction
        };
    }

    public int Rotate(int rotation, int steps) =>
        _index switch
        {
            2 => unchecked(rotation + steps / 2) % steps,
            3 => unchecked(rotation + steps * 3 / 4) % steps,
            1 => unchecked(rotation + steps / 4) % steps,
            _ => rotation
        };

    public static Rotation GetRandom(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return BY_ID(random.NextInt(VALUES.Length));
    }

    public static List<Rotation> GetShuffled(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var result = new List<Rotation>(VALUES);
        for (int i = result.Count; i > 1; i--)
        {
            int swapTo = random.NextInt(i);
            (result[i - 1], result[swapTo]) = (result[swapTo], result[i - 1]);
        }

        return result;
    }

    public string GetSerializedName() => _serializedName;

    private int GetIndex() => _index;

    private static Rotation GetById(int id)
    {
        int index = id % VALUES.Length;
        if (index < 0)
            index += VALUES.Length;
        return VALUES[index];
    }

    private static Rotation? BySerializedName(string name) =>
        VALUES.FirstOrDefault(value =>
            string.Equals(value._serializedName, name, StringComparison.Ordinal));

    private static Rotation? ByLegacyName(string name) =>
        VALUES.FirstOrDefault(value =>
            string.Equals(value._name, name, StringComparison.Ordinal));

    public override string ToString() => _name;
}
