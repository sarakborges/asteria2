using System.Text.Json;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Structure;

internal static class BeardifierChecks
{
    public static void Run()
    {
        CheckTerrainAdjustmentCodec();
        CheckMathPrimitives();
        CheckPointSampling();
        CheckJunctionSampling();
        CheckKernelBounds();
        CheckVolumeSampling();
    }

    private static void CheckTerrainAdjustmentCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (TerrainAdjustment adjustment in TerrainAdjustment.Values)
        {
            JsonElement encoded = TerrainAdjustment.CODEC
                .EncodeStart(ops, adjustment)
                .GetOrThrow();

            Require(
                ops.GetStringValue(encoded).GetOrThrow() == adjustment.GetSerializedName(),
                "TerrainAdjustment must serialize using its exact lowercase identifier.");
            Require(
                ReferenceEquals(
                    TerrainAdjustment.CODEC.Parse(ops, encoded).GetOrThrow(),
                    adjustment),
                "TerrainAdjustment decoding must resolve the canonical singleton.");
        }

        Require(
            TerrainAdjustment.CODEC.Parse(
                ops,
                ops.CreateString("beard_thick")).IsError,
            "TerrainAdjustment must reject identifiers that are not defined in 26.3.");
    }

    private static void CheckMathPrimitives()
    {
        Require(Mth.Sqrt(9.0F) == 3.0F,
            "Mth.sqrt(float) must return the float conversion of Math.sqrt.");
        Require(Mth.LengthSquared(1.0F, 2.0F, 3.0F) == 14.0F,
            "Mth.lengthSquared(float,float,float) must preserve float arithmetic.");
        Require(
            Math.Abs(Mth.FastInvSqrt(0.125) - 2.8277186031818555) < 1.0E-15,
            "Mth.fastInvSqrt must preserve the 64-bit inverse-square-root approximation.");
    }

    private static void CheckPointSampling()
    {
        BoundingBox pieceBox = new(0, 0, 0, 0, 0, 0);
        BoundingBox affectedBox = new(-24, -24, -24, 24, 24, 24);

        Beardifier bury = CreateSingleRigid(TerrainAdjustment.BURY, pieceBox, affectedBox);
        Require(bury.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 1.0F,
            "BURY must contribute its full value at the rigid piece's ground origin.");
        Require(Math.Abs(bury.SampleValue(SamplerContext.EmptyUncached, 3, 0, 0) - 0.5F) < 1.0E-6F,
            "BURY must use the six-block radial falloff and halve vertical distance.");
        Require(Math.Abs(bury.SampleValue(SamplerContext.EmptyUncached, 0, 6, 0) - 0.5F) < 1.0E-6F,
            "BURY must halve the vertical distance before evaluating its radial falloff.");
        Require(bury.SampleValue(SamplerContext.EmptyUncached, 6, 0, 0) == 0.0F,
            "BURY must reach zero at the inclusive six-block distance threshold.");

        Beardifier none = CreateSingleRigid(TerrainAdjustment.NONE, pieceBox, affectedBox);
        Require(none.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 0.0F,
            "NONE must not deform terrain.");

        Beardifier encapsulate =
            CreateSingleRigid(TerrainAdjustment.ENCAPSULATE, pieceBox, affectedBox);
        Require(Math.Abs(encapsulate.SampleValue(SamplerContext.EmptyUncached, 6, 0, 0) - 0.4F) < 1.0E-6F,
            "ENCAPSULATE must halve all axes and apply the 0.8 contribution factor.");

        Beardifier beardThin =
            CreateSingleRigid(TerrainAdjustment.BEARD_THIN, pieceBox, affectedBox);
        Require(Math.Abs(beardThin.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) -
                         -0.5567757487297058F) < 1.0E-6F,
            "BEARD_THIN must use the precomputed kernel, inverse-square-root factor and 0.8 scale.");

        Beardifier beardBox =
            CreateSingleRigid(TerrainAdjustment.BEARD_BOX, pieceBox, affectedBox);
        Require(Math.Abs(beardBox.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) -
                         -0.5567757487297058F) < 1.0E-6F,
            "BEARD_BOX must use its ground-to-box vertical distance and the beard kernel.");
        Require(
            bury.SampleValue(SamplerContext.EmptyUncached, 25, 0, 0) == 0.0F,
            "Point sampling must return zero outside the precomputed affected box.");

        Beardifier twoPieces = new(
            [
                new Beardifier.Rigid(pieceBox, TerrainAdjustment.BURY, 0),
                new Beardifier.Rigid(pieceBox, TerrainAdjustment.BURY, 0)
            ],
            Array.Empty<Beardifier.JunctionPoint>(),
            affectedBox);
        Require(twoPieces.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 2.0F,
            "Rigid-piece contributions must accumulate in source list order.");
    }

    private static void CheckJunctionSampling()
    {
        BoundingBox affectedBox = new(-24, -24, -24, 24, 24, 24);
        var junction = new Beardifier.JunctionPoint(0, 0, 0);
        var beardifier = new Beardifier(
            Array.Empty<Beardifier.Rigid>(),
            [junction],
            affectedBox);

        Require(Math.Abs(beardifier.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) -
                         -0.2783878743648529F) < 1.0E-6F,
            "Each junction must add the beard-kernel contribution scaled by 0.4.");
    }

    private static void CheckKernelBounds()
    {
        BoundingBox affectedBox = new(-24, -24, -24, 24, 24, 24);
        var junction = new Beardifier.JunctionPoint(-12, 0, 0);
        var beardifier = new Beardifier(
            Array.Empty<Beardifier.Rigid>(),
            [junction],
            affectedBox);

        Require(
            beardifier.SampleValue(SamplerContext.EmptyUncached, 12, 0, 0) == 0.0F,
            "The beard kernel must return zero when any shifted coordinate reaches 24.");
    }

    private static void CheckVolumeSampling()
    {
        BoundingBox pieceBox = new(0, 0, 0, 0, 0, 0);
        var beardifier = CreateSingleRigid(
            TerrainAdjustment.BURY,
            pieceBox,
            pieceBox);
        var volume = new DensityVolume(
            3, 3, 3,
            -2, -2, -2,
            2, 2, 2);
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());
        buffer.Fill(-1.0F);

        beardifier.SampleVolume(SamplerContext.EmptyUncached, buffer, volume);

        int originIndex = volume.IndexUnchecked(1, 1, 1);
        for (int index = 0; index < buffer.Size(); index++)
        {
            float expected = index == originIndex ? 1.0F : 0.0F;
            Require(buffer.Get(index) == expected,
                "Volume sampling must zero the full output and write only sampled points inside the affected box.");
        }

        DensityBuffer emptyBuffer = DensityBuffer.CreateUnpooled(volume.Size());
        emptyBuffer.Fill(7.0F);
        Beardifier.EMPTY.SampleVolume(SamplerContext.EmptyUncached, emptyBuffer, volume);
        for (int index = 0; index < emptyBuffer.Size(); index++)
        {
            Require(emptyBuffer.Get(index) == 0.0F,
                "The empty Beardifier must clear the output volume to zero.");
        }
    }

    private static Beardifier CreateSingleRigid(
        TerrainAdjustment adjustment,
        BoundingBox pieceBox,
        BoundingBox affectedBox) =>
        new(
            [new Beardifier.Rigid(pieceBox, adjustment, 0)],
            Array.Empty<Beardifier.JunctionPoint>(),
            affectedBox);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
