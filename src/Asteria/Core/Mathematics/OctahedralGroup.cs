using Asteria.Core;

namespace Asteria.Core.Mathematics;

/// <summary>
/// The 48 signed axis permutations of the octahedral symmetry group.
/// Composition, inversion, direction rotation and integer-vector rotation follow the 26.3 group tables.
/// </summary>
public sealed class OctahedralGroup
{
    public static readonly OctahedralGroup IDENTITY =
        new("identity", SymmetricGroup3.P123, false, false, false);
    public static readonly OctahedralGroup ROT_180_FACE_XY =
        new("rot_180_face_xy", SymmetricGroup3.P123, true, true, false);
    public static readonly OctahedralGroup ROT_180_FACE_XZ =
        new("rot_180_face_xz", SymmetricGroup3.P123, true, false, true);
    public static readonly OctahedralGroup ROT_180_FACE_YZ =
        new("rot_180_face_yz", SymmetricGroup3.P123, false, true, true);
    public static readonly OctahedralGroup ROT_120_NNN =
        new("rot_120_nnn", SymmetricGroup3.P231, false, false, false);
    public static readonly OctahedralGroup ROT_120_NNP =
        new("rot_120_nnp", SymmetricGroup3.P312, true, false, true);
    public static readonly OctahedralGroup ROT_120_NPN =
        new("rot_120_npn", SymmetricGroup3.P312, false, true, true);
    public static readonly OctahedralGroup ROT_120_NPP =
        new("rot_120_npp", SymmetricGroup3.P231, true, false, true);
    public static readonly OctahedralGroup ROT_120_PNN =
        new("rot_120_pnn", SymmetricGroup3.P312, true, true, false);
    public static readonly OctahedralGroup ROT_120_PNP =
        new("rot_120_pnp", SymmetricGroup3.P231, true, true, false);
    public static readonly OctahedralGroup ROT_120_PPN =
        new("rot_120_ppn", SymmetricGroup3.P231, false, true, true);
    public static readonly OctahedralGroup ROT_120_PPP =
        new("rot_120_ppp", SymmetricGroup3.P312, false, false, false);
    public static readonly OctahedralGroup ROT_180_EDGE_XY_NEG =
        new("rot_180_edge_xy_neg", SymmetricGroup3.P213, true, true, true);
    public static readonly OctahedralGroup ROT_180_EDGE_XY_POS =
        new("rot_180_edge_xy_pos", SymmetricGroup3.P213, false, false, true);
    public static readonly OctahedralGroup ROT_180_EDGE_XZ_NEG =
        new("rot_180_edge_xz_neg", SymmetricGroup3.P321, true, true, true);
    public static readonly OctahedralGroup ROT_180_EDGE_XZ_POS =
        new("rot_180_edge_xz_pos", SymmetricGroup3.P321, false, true, false);
    public static readonly OctahedralGroup ROT_180_EDGE_YZ_NEG =
        new("rot_180_edge_yz_neg", SymmetricGroup3.P132, true, true, true);
    public static readonly OctahedralGroup ROT_180_EDGE_YZ_POS =
        new("rot_180_edge_yz_pos", SymmetricGroup3.P132, true, false, false);
    public static readonly OctahedralGroup ROT_90_X_NEG =
        new("rot_90_x_neg", SymmetricGroup3.P132, false, false, true);
    public static readonly OctahedralGroup ROT_90_X_POS =
        new("rot_90_x_pos", SymmetricGroup3.P132, false, true, false);
    public static readonly OctahedralGroup ROT_90_Y_NEG =
        new("rot_90_y_neg", SymmetricGroup3.P321, true, false, false);
    public static readonly OctahedralGroup ROT_90_Y_POS =
        new("rot_90_y_pos", SymmetricGroup3.P321, false, false, true);
    public static readonly OctahedralGroup ROT_90_Z_NEG =
        new("rot_90_z_neg", SymmetricGroup3.P213, false, true, false);
    public static readonly OctahedralGroup ROT_90_Z_POS =
        new("rot_90_z_pos", SymmetricGroup3.P213, true, false, false);
    public static readonly OctahedralGroup INVERSION =
        new("inversion", SymmetricGroup3.P123, true, true, true);
    public static readonly OctahedralGroup INVERT_X =
        new("invert_x", SymmetricGroup3.P123, true, false, false);
    public static readonly OctahedralGroup INVERT_Y =
        new("invert_y", SymmetricGroup3.P123, false, true, false);
    public static readonly OctahedralGroup INVERT_Z =
        new("invert_z", SymmetricGroup3.P123, false, false, true);
    public static readonly OctahedralGroup ROT_60_REF_NNN =
        new("rot_60_ref_nnn", SymmetricGroup3.P312, true, true, true);
    public static readonly OctahedralGroup ROT_60_REF_NNP =
        new("rot_60_ref_nnp", SymmetricGroup3.P231, true, false, false);
    public static readonly OctahedralGroup ROT_60_REF_NPN =
        new("rot_60_ref_npn", SymmetricGroup3.P231, false, false, true);
    public static readonly OctahedralGroup ROT_60_REF_NPP =
        new("rot_60_ref_npp", SymmetricGroup3.P312, false, false, true);
    public static readonly OctahedralGroup ROT_60_REF_PNN =
        new("rot_60_ref_pnn", SymmetricGroup3.P231, false, true, false);
    public static readonly OctahedralGroup ROT_60_REF_PNP =
        new("rot_60_ref_pnp", SymmetricGroup3.P312, true, false, false);
    public static readonly OctahedralGroup ROT_60_REF_PPN =
        new("rot_60_ref_ppn", SymmetricGroup3.P312, false, true, false);
    public static readonly OctahedralGroup ROT_60_REF_PPP =
        new("rot_60_ref_ppp", SymmetricGroup3.P231, true, true, true);
    public static readonly OctahedralGroup SWAP_XY =
        new("swap_xy", SymmetricGroup3.P213, false, false, false);
    public static readonly OctahedralGroup SWAP_YZ =
        new("swap_yz", SymmetricGroup3.P132, false, false, false);
    public static readonly OctahedralGroup SWAP_XZ =
        new("swap_xz", SymmetricGroup3.P321, false, false, false);
    public static readonly OctahedralGroup SWAP_NEG_XY =
        new("swap_neg_xy", SymmetricGroup3.P213, true, true, false);
    public static readonly OctahedralGroup SWAP_NEG_YZ =
        new("swap_neg_yz", SymmetricGroup3.P132, false, true, true);
    public static readonly OctahedralGroup SWAP_NEG_XZ =
        new("swap_neg_xz", SymmetricGroup3.P321, true, false, true);
    public static readonly OctahedralGroup ROT_90_REF_X_NEG =
        new("rot_90_ref_x_neg", SymmetricGroup3.P132, true, false, true);
    public static readonly OctahedralGroup ROT_90_REF_X_POS =
        new("rot_90_ref_x_pos", SymmetricGroup3.P132, true, true, false);
    public static readonly OctahedralGroup ROT_90_REF_Y_NEG =
        new("rot_90_ref_y_neg", SymmetricGroup3.P321, true, true, false);
    public static readonly OctahedralGroup ROT_90_REF_Y_POS =
        new("rot_90_ref_y_pos", SymmetricGroup3.P321, false, true, true);
    public static readonly OctahedralGroup ROT_90_REF_Z_NEG =
        new("rot_90_ref_z_neg", SymmetricGroup3.P213, false, true, true);
    public static readonly OctahedralGroup ROT_90_REF_Z_POS =
        new("rot_90_ref_z_pos", SymmetricGroup3.P213, true, false, true);

    public static readonly OctahedralGroup BLOCK_ROT_X_270 = ROT_90_X_POS;
    public static readonly OctahedralGroup BLOCK_ROT_X_180 = ROT_180_FACE_YZ;
    public static readonly OctahedralGroup BLOCK_ROT_X_90 = ROT_90_X_NEG;
    public static readonly OctahedralGroup BLOCK_ROT_Y_270 = ROT_90_Y_POS;
    public static readonly OctahedralGroup BLOCK_ROT_Y_180 = ROT_180_FACE_XZ;
    public static readonly OctahedralGroup BLOCK_ROT_Y_90 = ROT_90_Y_NEG;
    public static readonly OctahedralGroup BLOCK_ROT_Z_270 = ROT_90_Z_POS;
    public static readonly OctahedralGroup BLOCK_ROT_Z_180 = ROT_180_FACE_XY;
    public static readonly OctahedralGroup BLOCK_ROT_Z_90 = ROT_90_Z_NEG;

    private static readonly OctahedralGroup[] Values =
    {
        IDENTITY, ROT_180_FACE_XY, ROT_180_FACE_XZ, ROT_180_FACE_YZ,
        ROT_120_NNN, ROT_120_NNP, ROT_120_NPN, ROT_120_NPP,
        ROT_120_PNN, ROT_120_PNP, ROT_120_PPN, ROT_120_PPP,
        ROT_180_EDGE_XY_NEG, ROT_180_EDGE_XY_POS,
        ROT_180_EDGE_XZ_NEG, ROT_180_EDGE_XZ_POS,
        ROT_180_EDGE_YZ_NEG, ROT_180_EDGE_YZ_POS,
        ROT_90_X_NEG, ROT_90_X_POS, ROT_90_Y_NEG, ROT_90_Y_POS,
        ROT_90_Z_NEG, ROT_90_Z_POS,
        INVERSION, INVERT_X, INVERT_Y, INVERT_Z,
        ROT_60_REF_NNN, ROT_60_REF_NNP, ROT_60_REF_NPN, ROT_60_REF_NPP,
        ROT_60_REF_PNN, ROT_60_REF_PNP, ROT_60_REF_PPN, ROT_60_REF_PPP,
        SWAP_XY, SWAP_YZ, SWAP_XZ, SWAP_NEG_XY, SWAP_NEG_YZ, SWAP_NEG_XZ,
        ROT_90_REF_X_NEG, ROT_90_REF_X_POS,
        ROT_90_REF_Y_NEG, ROT_90_REF_Y_POS,
        ROT_90_REF_Z_NEG, ROT_90_REF_Z_POS
    };

    private static readonly OctahedralGroup[][] CayleyTable = CreateCayleyTable();
    private static readonly OctahedralGroup[] InverseTable = CreateInverseTable();

    private readonly int _ordinal;
    private readonly string _name;
    private readonly bool _invertX;
    private readonly bool _invertY;
    private readonly bool _invertZ;
    private readonly SymmetricGroup3 _permutation;
    private Direction[]? _rotatedDirections;

    public Matrix3x3 Transformation { get; }

    private OctahedralGroup(
        string name,
        SymmetricGroup3 permutation,
        bool invertX,
        bool invertY,
        bool invertZ)
    {
        _name = name;
        _ordinal = ValuesIndex(name);
        _invertX = invertX;
        _invertY = invertY;
        _invertZ = invertZ;
        _permutation = permutation;
        Transformation = Matrix3x3.CreateScale(
                invertX ? -1.0f : 1.0f,
                invertY ? -1.0f : 1.0f,
                invertZ ? -1.0f : 1.0f)
            .Multiply(permutation.Transformation);
    }

    public static OctahedralGroup[] GetValues() => (OctahedralGroup[])Values.Clone();

    public OctahedralGroup Compose(OctahedralGroup that)
    {
        ArgumentNullException.ThrowIfNull(that);
        return CayleyTable[_ordinal][that._ordinal];
    }

    public OctahedralGroup Inverse() => InverseTable[_ordinal];

    public string GetSerializedName() => _name;

    public Direction Rotate(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);

        if (_rotatedDirections is null)
        {
            var rotated = new Direction[6];
            for (int index = 0; index < rotated.Length; index++)
            {
                Direction facing = Direction.From3DDataValue(index);
                Direction.Axis oldAxis = facing.GetAxis();
                Direction.AxisDirection oldDirection = facing.GetAxisDirection();
                Direction.Axis newAxis = _permutation.Inverse().PermuteAxis(oldAxis);
                Direction.AxisDirection newDirection =
                    Inverts(newAxis) ? oldDirection.Opposite() : oldDirection;
                rotated[index] = Direction.FromAxisAndDirection(newAxis, newDirection);
            }

            _rotatedDirections = rotated;
        }

        return _rotatedDirections[direction.Get3DDataValue()];
    }

    public MutableVec3i Rotate(MutableVec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        _permutation.PermuteVector(vector);
        vector.SetComponent(0, unchecked(vector.GetX() * (_invertX ? -1 : 1)));
        vector.SetComponent(1, unchecked(vector.GetY() * (_invertY ? -1 : 1)));
        vector.SetComponent(2, unchecked(vector.GetZ() * (_invertZ ? -1 : 1)));
        return vector;
    }

    public bool Inverts(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (ReferenceEquals(axis, Direction.Axis.X))
            return _invertX;
        if (ReferenceEquals(axis, Direction.Axis.Y))
            return _invertY;
        if (ReferenceEquals(axis, Direction.Axis.Z))
            return _invertZ;
        throw new ArgumentException("Unknown direction axis.", nameof(axis));
    }

    public SymmetricGroup3 Permutation => _permutation;

    public FrontAndTop? Rotate(FrontAndTop input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return FrontAndTop.FromFrontAndTop(Rotate(input.Front()), Rotate(input.Top()));
    }

    public override string ToString() => _name;

    private static OctahedralGroup[][] CreateCayleyTable()
    {
        var table = new OctahedralGroup[Values.Length][];
        var fingerprints = Values.ToDictionary(value => value.Trace());

        foreach (OctahedralGroup first in Values)
        {
            table[first._ordinal] = new OctahedralGroup[Values.Length];
            foreach (OctahedralGroup second in Values)
            {
                SymmetricGroup3 composedPermutation = second._permutation.Compose(first._permutation);
                bool invertX =
                    first.Inverts(Direction.Axis.X) ^
                    second.Inverts(first._permutation.PermuteAxis(Direction.Axis.X));
                bool invertY =
                    first.Inverts(Direction.Axis.Y) ^
                    second.Inverts(first._permutation.PermuteAxis(Direction.Axis.Y));
                bool invertZ =
                    first.Inverts(Direction.Axis.Z) ^
                    second.Inverts(first._permutation.PermuteAxis(Direction.Axis.Z));

                table[first._ordinal][second._ordinal] =
                    fingerprints[Trace(invertX, invertY, invertZ, composedPermutation)];
            }
        }

        return table;
    }

    private static OctahedralGroup[] CreateInverseTable()
    {
        var result = new OctahedralGroup[Values.Length];
        foreach (OctahedralGroup value in Values)
        {
            result[value._ordinal] =
                Values.First(candidate => ReferenceEquals(value.Compose(candidate), IDENTITY));
        }

        return result;
    }

    private int Trace() => Trace(_invertX, _invertY, _invertZ, _permutation);

    private static int Trace(
        bool invertX,
        bool invertY,
        bool invertZ,
        SymmetricGroup3 permutation)
    {
        int inversionIndex =
            (invertZ ? 4 : 0) +
            (invertY ? 2 : 0) +
            (invertX ? 1 : 0);
        int permutationOrdinal = permutation.Ordinal;
        return permutationOrdinal << 3 | inversionIndex;
    }

    private static int ValuesIndex(string name)
    {
        // The Java enum ordinal is declaration order. Constructor-time lookup preserves it without
        // relying on field initialization order for the public Values array.
        string[] names =
        {
            "identity", "rot_180_face_xy", "rot_180_face_xz", "rot_180_face_yz",
            "rot_120_nnn", "rot_120_nnp", "rot_120_npn", "rot_120_npp",
            "rot_120_pnn", "rot_120_pnp", "rot_120_ppn", "rot_120_ppp",
            "rot_180_edge_xy_neg", "rot_180_edge_xy_pos",
            "rot_180_edge_xz_neg", "rot_180_edge_xz_pos",
            "rot_180_edge_yz_neg", "rot_180_edge_yz_pos",
            "rot_90_x_neg", "rot_90_x_pos", "rot_90_y_neg", "rot_90_y_pos",
            "rot_90_z_neg", "rot_90_z_pos",
            "inversion", "invert_x", "invert_y", "invert_z",
            "rot_60_ref_nnn", "rot_60_ref_nnp", "rot_60_ref_npn", "rot_60_ref_npp",
            "rot_60_ref_pnn", "rot_60_ref_pnp", "rot_60_ref_ppn", "rot_60_ref_ppp",
            "swap_xy", "swap_yz", "swap_xz", "swap_neg_xy", "swap_neg_yz", "swap_neg_xz",
            "rot_90_ref_x_neg", "rot_90_ref_x_pos", "rot_90_ref_y_neg", "rot_90_ref_y_pos",
            "rot_90_ref_z_neg", "rot_90_ref_z_pos"
        };
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], name, StringComparison.Ordinal))
                return i;
        }

        throw new InvalidOperationException("Unknown octahedral group name: " + name);
    }
}
