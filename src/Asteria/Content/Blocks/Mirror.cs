using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Core.Mathematics;
using Asteria.Serialization;

namespace Asteria.Content.Blocks;

/// <summary>
/// Horizontal mirror modes for block-state and direction transformations.
/// </summary>
public sealed class Mirror
{
    public static readonly Mirror NONE = new("NONE", "none", OctahedralGroup.IDENTITY);
    public static readonly Mirror LEFT_RIGHT = new("LEFT_RIGHT", "left_right", OctahedralGroup.INVERT_Z);
    public static readonly Mirror FRONT_BACK = new("FRONT_BACK", "front_back", OctahedralGroup.INVERT_X);

    private static readonly Mirror[] VALUES = { NONE, LEFT_RIGHT, FRONT_BACK };

    public static readonly Codec<Mirror> CODEC =
        Codecs.STRING.FlatXMap(
            id => BySerializedName(id) is { } mirror
                ? DataResult<Mirror>.Success(mirror)
                : DataResult<Mirror>.Error(() => "Unknown mirror: " + id),
            mirror => DataResult<string>.Success(
                mirror?.GetSerializedName() ?? throw new ArgumentNullException(nameof(mirror))))
        .WithLifecycle(Lifecycle.Stable());

    [Obsolete("Legacy enum-name codec.")]
    public static readonly Codec<Mirror> LEGACY_CODEC =
        Codecs.STRING.FlatXMap(
            name => ByLegacyName(name) is { } mirror
                ? DataResult<Mirror>.Success(mirror)
                : DataResult<Mirror>.Error(() => "No value with id: " + name),
            mirror => DataResult<string>.Success(
                mirror?.ToString() ?? throw new ArgumentNullException(nameof(mirror))));

    private readonly string _name;
    private readonly string _serializedName;
    private readonly OctahedralGroup _rotation;

    private Mirror(string name, string serializedName, OctahedralGroup rotation)
    {
        _name = name;
        _serializedName = serializedName;
        _rotation = rotation;
    }

    public static Mirror[] GetValues() => (Mirror[])VALUES.Clone();

    public int GetMirrored(int rotation, int steps)
    {
        int halfSteps = steps / 2;
        int correctedRotation = rotation > halfSteps ? unchecked(rotation - steps) : rotation;

        return ReferenceEquals(this, FRONT_BACK)
            ? unchecked(steps - correctedRotation) % steps
            : ReferenceEquals(this, LEFT_RIGHT)
                ? unchecked(halfSteps - correctedRotation + steps) % steps
                : rotation;
    }

    public Rotation GetRotation(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        Direction.Axis axis = direction.GetAxis();
        return ReferenceEquals(this, LEFT_RIGHT) && ReferenceEquals(axis, Direction.Axis.Z) ||
               ReferenceEquals(this, FRONT_BACK) && ReferenceEquals(axis, Direction.Axis.X)
            ? Rotation.CLOCKWISE_180
            : Rotation.NONE;
    }

    public Direction MirrorDirection(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        if (ReferenceEquals(this, FRONT_BACK) && ReferenceEquals(direction.GetAxis(), Direction.Axis.X))
            return direction.GetOpposite();
        if (ReferenceEquals(this, LEFT_RIGHT) && ReferenceEquals(direction.GetAxis(), Direction.Axis.Z))
            return direction.GetOpposite();
        return direction;
    }

    public OctahedralGroup GetRotation() => _rotation;

    public string GetSerializedName() => _serializedName;

    private static Mirror? BySerializedName(string name) =>
        VALUES.FirstOrDefault(value =>
            string.Equals(value._serializedName, name, StringComparison.Ordinal));

    private static Mirror? ByLegacyName(string name) =>
        VALUES.FirstOrDefault(value =>
            string.Equals(value._name, name, StringComparison.Ordinal));

    public override string ToString() => _name;
}
