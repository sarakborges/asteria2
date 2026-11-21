using System.Numerics;
using Asteria.Core;
using Asteria.Core.Mathematics;
using Asteria.Core.Shapes;

internal static class OctahedralGroupChecks
{
    public static void Run()
    {
        CheckSymmetricGroup3();
        CheckOctahedralGroupTables();
        CheckDirectionAndFrontTopRotation();
        CheckVectorAndMatrixTransforms();
        CheckDiscreteShapeRotation();
    }

    private static void CheckSymmetricGroup3()
    {
        SymmetricGroup3[] permutations = SymmetricGroup3.GetValues();
        Require(permutations.Length == 6, "The permutation group must contain six members.");

        foreach (SymmetricGroup3 first in permutations)
        {
            Require(ReferenceEquals(first.Compose(SymmetricGroup3.P123), first),
                "The identity permutation must be a right identity.");
            Require(ReferenceEquals(SymmetricGroup3.P123.Compose(first), first),
                "The identity permutation must be a left identity.");
            Require(ReferenceEquals(first.Compose(first.Inverse()), SymmetricGroup3.P123),
                "A permutation composed with its inverse must be the identity.");
            Require(ReferenceEquals(first.Inverse().Compose(first), SymmetricGroup3.P123),
                "A permutation inverse composed on the left must be the identity.");

            foreach (SymmetricGroup3 second in permutations)
            foreach (SymmetricGroup3 third in permutations)
            {
                Require(
                    ReferenceEquals(first.Compose(second).Compose(third), first.Compose(second.Compose(third))),
                    "Permutation composition must be associative.");
            }
        }

        var integerVector = new MutableVec3i(10, 20, 30);
        Require(
            ReferenceEquals(SymmetricGroup3.P231.PermuteVector(integerVector), integerVector) &&
            integerVector.GetX() == 20 && integerVector.GetY() == 30 && integerVector.GetZ() == 10,
            "Integer vector permutation must mutate and return the original vector.");

        var floatVector = new MutableVec3f(1.25f, 2.5f, 5.0f);
        Require(
            ReferenceEquals(SymmetricGroup3.P312.PermuteVector(floatVector), floatVector) &&
            floatVector.X == 5.0f && floatVector.Y == 1.25f && floatVector.Z == 2.5f,
            "Float vector permutation must mutate and return the original vector.");
    }

    private static void CheckOctahedralGroupTables()
    {
        OctahedralGroup[] groups = OctahedralGroup.GetValues();
        Require(groups.Length == 48, "The octahedral group must contain exactly 48 members.");
        Require(groups.Select(group => group.GetSerializedName()).Distinct(StringComparer.Ordinal).Count() == 48,
            "Every octahedral group element must have a unique serialized name.");

        foreach (OctahedralGroup first in groups)
        {
            Require(ReferenceEquals(first.Compose(OctahedralGroup.IDENTITY), first),
                "The group identity must be a right identity.");
            Require(ReferenceEquals(OctahedralGroup.IDENTITY.Compose(first), first),
                "The group identity must be a left identity.");
            Require(ReferenceEquals(first.Compose(first.Inverse()), OctahedralGroup.IDENTITY),
                "Every element must compose with its inverse to identity.");
            Require(ReferenceEquals(first.Inverse().Compose(first), OctahedralGroup.IDENTITY),
                "Every inverse must compose on the left to identity.");

            foreach (OctahedralGroup second in groups)
            foreach (OctahedralGroup third in groups)
            {
                Require(
                    ReferenceEquals(first.Compose(second).Compose(third), first.Compose(second.Compose(third))),
                    "Octahedral group composition must be associative.");
            }
        }

        Require(ReferenceEquals(OctahedralGroup.BLOCK_ROT_X_90, OctahedralGroup.ROT_90_X_NEG) &&
                ReferenceEquals(OctahedralGroup.BLOCK_ROT_Y_180, OctahedralGroup.ROT_180_FACE_XZ) &&
                ReferenceEquals(OctahedralGroup.BLOCK_ROT_Z_270, OctahedralGroup.ROT_90_Z_POS),
            "Block-rotation aliases must reference the exact group elements.");
    }

    private static void CheckDirectionAndFrontTopRotation()
    {
        OctahedralGroup[] groups = OctahedralGroup.GetValues();
        Direction[] directions =
        {
            Direction.DOWN, Direction.UP, Direction.NORTH,
            Direction.SOUTH, Direction.WEST, Direction.EAST
        };

        foreach (OctahedralGroup group in groups)
        {
            var seen = new HashSet<Direction>(ReferenceEqualityComparer.Instance);
            foreach (Direction direction in directions)
            {
                Direction rotated = group.Rotate(direction);
                Require(seen.Add(rotated), "Every group transform must permute the six directions bijectively.");
                Require(
                    ReferenceEquals(group.Inverse().Rotate(rotated), direction),
                    "Inverse direction rotation must restore the original direction.");
            }

            foreach (FrontAndTop value in FrontAndTop.GetValues())
            {
                FrontAndTop? rotated = group.Rotate(value);
                if (rotated is not null)
                {
                    Require(
                        ReferenceEquals(group.Inverse().Rotate(rotated), value),
                        "Representable front/top pairs must round-trip through a group element and its inverse.");
                    Require(!ReferenceEquals(rotated.Front().GetAxis(), rotated.Top().GetAxis()),
                        "Representable rotated front and top directions must remain orthogonal.");
                }
                else
                {
                    Require(
                        FrontAndTop.FromFrontAndTop(group.Rotate(value.Front()), group.Rotate(value.Top())) is null,
                        "The group rotation must preserve the reference lookup's unsupported-pair result.");
                }
            }
        }

        Require(FrontAndTop.FromFrontAndTop(Direction.NORTH, Direction.SOUTH) is null,
            "Parallel front/top directions must not resolve to a FrontAndTop value.");
    }

    private static void CheckVectorAndMatrixTransforms()
    {
        Vector3 input = new(1.25f, -2.5f, 4.0f);

        foreach (OctahedralGroup group in OctahedralGroup.GetValues())
        {
            var expected = new MutableVec3f(input.X, input.Y, input.Z);
            group.Permutation.PermuteVector(expected);
            var expectedVector = new Vector3(
                expected.X * (group.Inverts(Direction.Axis.X) ? -1.0f : 1.0f),
                expected.Y * (group.Inverts(Direction.Axis.Y) ? -1.0f : 1.0f),
                expected.Z * (group.Inverts(Direction.Axis.Z) ? -1.0f : 1.0f));

            Vector3 transformed = group.Transformation.Transform(input);
            Require(
                NearlyEqual(transformed.X, expectedVector.X) &&
                NearlyEqual(transformed.Y, expectedVector.Y) &&
                NearlyEqual(transformed.Z, expectedVector.Z),
                "The exposed transformation matrix must match the signed permutation.");
        }
    }

    private static void CheckDiscreteShapeRotation()
    {
        var original = new BitSetDiscreteVoxelShape(2, 3, 4);
        original.Fill(0, 0, 0);
        original.Fill(1, 2, 3);
        original.Fill(1, 0, 2);
        original.Fill(0, 1, 3);

        foreach (OctahedralGroup group in OctahedralGroup.GetValues())
        {
            DiscreteVoxelShape rotated = original.Rotate(group);
            DiscreteVoxelShape roundTrip = rotated.Rotate(group.Inverse());
            Require(
                HasSameOccupancy(original, roundTrip),
                $"Discrete shape rotation must round-trip through {group.GetSerializedName()} and its inverse.");
        }

        Require(original.IsFull(0, 0, 0) && original.IsFull(1, 2, 3),
            "Rotation must not mutate the original occupancy grid.");
    }

    private static bool HasSameOccupancy(DiscreteVoxelShape first, DiscreteVoxelShape second)
    {
        if (first.GetXSize() != second.GetXSize() ||
            first.GetYSize() != second.GetYSize() ||
            first.GetZSize() != second.GetZSize())
            return false;

        for (int x = 0; x < first.GetXSize(); x++)
        for (int y = 0; y < first.GetYSize(); y++)
        for (int z = 0; z < first.GetZSize(); z++)
        {
            if (first.IsFull(x, y, z) != second.IsFull(x, y, z))
                return false;
        }

        return true;
    }

    private static bool NearlyEqual(float first, float second) =>
        MathF.Abs(first - second) <= 1.0e-6f;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
