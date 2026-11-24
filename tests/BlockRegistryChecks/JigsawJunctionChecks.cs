using System.Text.Json;
using System.Runtime.CompilerServices;
using Asteria.Serialization;
using Asteria.World.LevelGen.Structure.Pools;

internal static class JigsawJunctionChecks
{
    public static void Run()
    {
        CheckProjectionCodec();
        CheckDynamicSerialization();
        CheckDefaultValues();
        CheckEqualityAndHashing();
    }

    private static void CheckProjectionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (StructureTemplatePool.Projection projection in StructureTemplatePool.Projection.Values)
        {
            JsonElement encoded = StructureTemplatePool.Projection.CODEC
                .EncodeStart(ops, projection)
                .GetOrThrow();
            Require(
                ops.GetStringValue(encoded).GetOrThrow() == projection.GetSerializedName(),
                "Projection.CODEC must use the serialized name from StructureTemplatePool.Projection.");
            Require(
                ReferenceEquals(
                    StructureTemplatePool.Projection.CODEC.Parse(ops, encoded).GetOrThrow(),
                    projection),
                "Projection.CODEC must resolve the canonical projection singleton.");
            Require(
                ReferenceEquals(
                    StructureTemplatePool.Projection.ByName(projection.GetName()),
                    projection),
                "Projection.byName must use the serialized projection name.");
        }

        Require(
            StructureTemplatePool.Projection.ByName("unknown") is null,
            "Projection.byName must return null for an unknown name.");
        Require(
            StructureTemplatePool.Projection.CODEC.Parse(
                ops,
                ops.CreateString("unknown")).IsError,
            "Projection.CODEC must reject an unknown serialized projection.");
    }

    private static void CheckDynamicSerialization()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var source = new JigsawJunction(
            -31,
            72,
            104,
            -5,
            StructureTemplatePool.Projection.TERRAIN_MATCHING);

        JsonElement encoded = source.Serialize(ops);
        Require(ops.GetMapValues(encoded).GetOrThrow().Count == 5,
            "JigsawJunction.serialize must emit exactly five fields.");
        Require(ops.Get(encoded, "source_x").GetOrThrow().GetInt32() == -31 &&
                ops.Get(encoded, "source_ground_y").GetOrThrow().GetInt32() == 72 &&
                ops.Get(encoded, "source_z").GetOrThrow().GetInt32() == 104 &&
                ops.Get(encoded, "delta_y").GetOrThrow().GetInt32() == -5 &&
                ops.Get(encoded, "dest_proj").GetOrThrow().GetString() == "terrain_matching",
            "JigsawJunction.serialize must preserve the exact field names and values.");

        JigsawJunction decoded = JigsawJunction.Deserialize(ops, encoded);
        Require(
            decoded.GetSourceX() == source.GetSourceX() &&
            decoded.GetSourceGroundY() == source.GetSourceGroundY() &&
            decoded.GetSourceZ() == source.GetSourceZ() &&
            decoded.GetDeltaY() == source.GetDeltaY() &&
            ReferenceEquals(decoded.GetDestProjection(), source.GetDestProjection()),
            "JigsawJunction.deserialize must round-trip all five persisted fields.");
    }

    private static void CheckDefaultValues()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JigsawJunction empty = JigsawJunction.Deserialize(ops, ops.EmptyMap);
        Require(
            empty.GetSourceX() == 0 &&
            empty.GetSourceGroundY() == 0 &&
            empty.GetSourceZ() == 0 &&
            empty.GetDeltaY() == 0 &&
            empty.GetDestProjection() is null,
            "JigsawJunction.deserialize must use zero/empty-string dynamic defaults.");
        
        JsonElement malformed = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("source_x"),
                ops.CreateString("not an integer")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("dest_proj"),
                ops.CreateString("not a projection"))
        });
        JigsawJunction decoded = JigsawJunction.Deserialize(ops, malformed);
        Require(
            decoded.GetSourceX() == 0 &&
            decoded.GetSourceGroundY() == 0 &&
            decoded.GetSourceZ() == 0 &&
            decoded.GetDeltaY() == 0 &&
            decoded.GetDestProjection() is null,
            "JigsawJunction.deserialize must preserve Dynamic.asInt/asString fallback behavior.");
    }

    private static void CheckEqualityAndHashing()
    {
        JigsawJunction first = new(
            3, 20, -4, 7, StructureTemplatePool.Projection.RIGID);
        JigsawJunction same = new(
            3, 20, -4, 7, StructureTemplatePool.Projection.RIGID);
        JigsawJunction differentGroundY = new(
            3, 21, -4, 7, StructureTemplatePool.Projection.RIGID);

        Require(first.Equals(same) && first.GetHashCode() == same.GetHashCode(),
            "JigsawJunction must compare coordinates, deltaY and projection identity.");
        Require(first.Equals(differentGroundY),
            "JigsawJunction.equals in 26.3 intentionally does not compare sourceGroundY.");
        Require(first.GetHashCode() != differentGroundY.GetHashCode(),
            "JigsawJunction.hashCode in 26.3 still includes sourceGroundY.");
        Require(
            first.GetHashCode() == unchecked(
                unchecked(unchecked(unchecked(3 * 31 + 20) * 31 - 4) * 31 + 7) * 31 +
                RuntimeHelpers.GetHashCode(StructureTemplatePool.Projection.RIGID)),
            "JigsawJunction.hashCode must preserve the source field order and 31 multiplier.");

        Require(
            first.ToString() ==
            "JigsawJunction{sourceX=3, sourceGroundY=20, sourceZ=-4, deltaY=7, destProjection=RIGID}",
            "JigsawJunction.toString must preserve the exact field names and declaration order.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
