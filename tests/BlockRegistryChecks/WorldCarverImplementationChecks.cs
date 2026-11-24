using System.Text.Json;
using Asteria.Content.Chunks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;

internal static class WorldCarverImplementationChecks
{
    private sealed class RecordingCarverOutput(int minimumY, int maximumY) : CarverOutput
    {
        public int MinimumY { get; } = minimumY;
        public int MaximumY { get; } = maximumY;
        public HashSet<(int X, int Y, int Z)> Positions { get; } = new();

        public void Carve(int x, int y, int z) => Positions.Add((x, y, z));
        public int MinY() => MinimumY;
        public int MaxY() => MaximumY;
    }

    public static void Run()
    {
        CheckCarverTypeRegistry();
        CheckCaveCodecAndDefaults();
        CheckCanyonCodec();
        CheckCaveCarvingDeterminism();
        CheckCanyonCarvingDeterminism();
        CheckInvalidInputs();
    }

    private static void CheckCarverTypeRegistry()
    {
        Require(
            BuiltInRegistries.CarverType.Count == 2,
            "The worldgen carver type registry must contain Cave and Canyon.");

        Require(
            BuiltInRegistries.CarverType.ContainsKey(ResourceLocation.Parse("minecraft:cave")) &&
            BuiltInRegistries.CarverType.ContainsKey(ResourceLocation.Parse("minecraft:canyon")),
            "The carver type registry must expose the Minecraft 26.3 cave and canyon identifiers.");

        Require(
            !BuiltInRegistries.CarverType.ContainsKey(ResourceLocation.Parse("asteria:cave")),
            "Built-in carver types must preserve their Minecraft namespace identifiers.");
    }

    private static void CheckCaveCodecAndDefaults()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement input = ParseJson(
            """
            {
              "type": "minecraft:cave",
              "probability": 0.75,
              "y": { "absolute": 32 },
              "count": 2,
              "thickness": 1.5,
              "room_vertical_radius_multiplier": 1.2,
              "horizontal_radius_multiplier": 1.0,
              "vertical_radius_multiplier": 0.8,
              "floor_level": 0.0
            }
            """);

        WorldCarver decoded = WorldCarvers.DIRECT_CODEC.Parse(ops, input).GetOrThrow();
        Require(decoded is CaveWorldCarver, "The cave type ID must decode to CaveWorldCarver.");
        var cave = (CaveWorldCarver)decoded;

        Require(
            cave.Probability == 0.75f &&
            cave.Y is ConstantHeight { Value: var anchor } &&
            anchor == VerticalAnchor.Absolute(32) &&
            cave.Count is ConstantInt { Value: 2 } &&
            cave.Thickness is ConstantFloat { Value: 1.5f },
            "CaveWorldCarver must decode probability, height, count and thickness through the correct provider codecs.");

        Require(
            !cave.WeirdThicknessBias &&
            cave.StartVerticalRadiusMultiplier is ConstantFloat { Value: 1.0f },
            "CaveWorldCarver must apply the 26.3 defaults for weird_thickness_bias and start_vertical_radius_multiplier.");

        JsonElement encoded = WorldCarvers.DIRECT_CODEC.EncodeStart(ops, cave).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:cave" &&
            ops.Get(encoded, "floor_level").GetOrThrow().ValueKind == JsonValueKind.Number,
            "The direct carver codec must encode CaveWorldCarver with its registry type and flat constant providers.");

        WorldCarver roundTrip = WorldCarvers.DIRECT_CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            roundTrip is CaveWorldCarver roundTripCave &&
            roundTripCave.Probability == cave.Probability &&
            roundTripCave.WeirdThicknessBias == cave.WeirdThicknessBias &&
            roundTripCave.Count.MinInclusive() == 2 &&
            roundTripCave.Count.MaxInclusive() == 2 &&
            roundTripCave.FloorLevel.Min() == 0.0f,
            "CaveWorldCarver must preserve all of its configured values on codec round-trip.");

        JsonElement optionalTrue = ParseJson(
            """
            {
              "type":"minecraft:cave",
              "probability":1.0,
              "y":{"absolute":24},
              "count":1,
              "thickness":2.0,
              "weird_thickness_bias":true,
              "room_vertical_radius_multiplier":1.0,
              "horizontal_radius_multiplier":1.0,
              "vertical_radius_multiplier":1.0,
              "start_vertical_radius_multiplier":0.5,
              "floor_level":-0.25
            }
            """);
        var configured = (CaveWorldCarver)WorldCarvers.DIRECT_CODEC
            .Parse(ops, optionalTrue)
            .GetOrThrow();

        Require(
            configured.WeirdThicknessBias &&
            configured.StartVerticalRadiusMultiplier.Min() == 0.5f &&
            configured.FloorLevel.Min() == -0.25f,
            "Explicit optional cave fields must override their vanilla defaults.");
    }

    private static void CheckCanyonCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement input = ParseJson(
            """
            {
              "type": "minecraft:canyon",
              "probability": 0.6,
              "y": { "absolute": 40 },
              "vertical_rotation": 0.0,
              "shape": {
                "distance_factor": 0.5,
                "thickness": 2.0,
                "width_smoothness": 3,
                "horizontal_radius_factor": 1.25,
                "vertical_radius_default_factor": 0.7,
                "vertical_radius_center_factor": 0.9,
                "y_scale": 1.1
              }
            }
            """);

        WorldCarver decoded = WorldCarvers.DIRECT_CODEC.Parse(ops, input).GetOrThrow();
        Require(decoded is CanyonWorldCarver, "The canyon type ID must decode to CanyonWorldCarver.");
        var canyon = (CanyonWorldCarver)decoded;

        Require(
            canyon.Probability == 0.6f &&
            canyon.Y is ConstantHeight { Value: var anchor } &&
            anchor == VerticalAnchor.Absolute(40) &&
            canyon.VerticalRotation is ConstantFloat { Value: 0.0f },
            "CanyonWorldCarver must decode its probability, vertical anchor and rotation provider.");

        CanyonWorldCarver.Shape shape = canyon.CarverShape;
        Require(
            shape.DistanceFactor is ConstantFloat { Value: 0.5f } &&
            shape.Thickness is ConstantFloat { Value: 2.0f } &&
            shape.WidthSmoothness == 3 &&
            shape.HorizontalRadiusFactor is ConstantFloat { Value: 1.25f } &&
            shape.VerticalRadiusDefaultFactor == 0.7f &&
            shape.VerticalRadiusCenterFactor == 0.9f &&
            shape.YScale is ConstantFloat { Value: 1.1f },
            "Canyon Shape must retain the seven 26.3 fields without changing their types or values.");

        JsonElement encoded = WorldCarvers.DIRECT_CODEC.EncodeStart(ops, canyon).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:canyon" &&
            ops.Get(ops.Get(encoded, "shape").GetOrThrow(), "width_smoothness").GetOrThrow().GetInt32() == 3,
            "CanyonWorldCarver and its nested Shape must round-trip through the registry dispatch codec.");
    }

    private static void CheckCaveCarvingDeterminism()
    {
        var context = new WorldGenerationContext(-64, 384, 63);
        var carver = new CaveWorldCarver(
            Probability: 1.0f,
            Y: ConstantHeight.Of(VerticalAnchor.Absolute(28)),
            Count: ConstantInt.Of(1),
            Thickness: ConstantFloat.Of(2.5f),
            WeirdThicknessBias: true,
            RoomVerticalRadiusMultiplier: ConstantFloat.Of(1.0f),
            HorizontalRadiusMultiplier: ConstantFloat.Of(1.0f),
            VerticalRadiusMultiplier: ConstantFloat.Of(1.0f),
            StartVerticalRadiusMultiplier: ConstantFloat.Of(1.0f),
            FloorLevel: ConstantFloat.Of(-0.5f));

        var first = new RecordingCarverOutput(0, 80);
        var second = new RecordingCarverOutput(0, 80);
        Require(
            carver.IsStartChunk(new LegacyRandomSource(1L)),
            "A carver with probability one must always pass the start-chunk check.");

        Require(
            carver.Carve(
                context,
                new LegacyRandomSource(123456789L),
                ChunkPos.Zero,
                ChunkPos.Zero,
                first),
            "CaveWorldCarver must report its source chunk as processed even when geometry is clipped.");

        carver.Carve(
            context,
            new LegacyRandomSource(123456789L),
            ChunkPos.Zero,
            ChunkPos.Zero,
            second);

        Require(
            first.Positions.Count > 0,
            "The configured cave carver must mark at least one ellipsoid position.");
        Require(
            first.Positions.SetEquals(second.Positions),
            "CaveWorldCarver must produce identical carved positions for the same random seed and chunk inputs.");
        Require(
            first.Positions.All(position =>
                position.X is >= 0 and <= 15 &&
                position.Z is >= 0 and <= 15 &&
                position.Y is >= 0 and <= 80),
            "CaveWorldCarver must keep output coordinates inside the target chunk and CarverOutput vertical bounds.");
    }

    private static void CheckCanyonCarvingDeterminism()
    {
        var context = new WorldGenerationContext(-64, 384, 63);
        var shape = new CanyonWorldCarver.Shape(
            DistanceFactor: ConstantFloat.Of(0.5f),
            Thickness: ConstantFloat.Of(2.0f),
            WidthSmoothness: 2,
            HorizontalRadiusFactor: ConstantFloat.Of(1.0f),
            VerticalRadiusDefaultFactor: 0.8f,
            VerticalRadiusCenterFactor: 0.8f,
            YScale: ConstantFloat.Of(1.0f));
        var carver = new CanyonWorldCarver(
            Probability: 1.0f,
            Y: ConstantHeight.Of(VerticalAnchor.Absolute(36)),
            VerticalRotation: ConstantFloat.Of(0.0f),
            CarverShape: shape);

        var first = new RecordingCarverOutput(0, 80);
        var second = new RecordingCarverOutput(0, 80);

        Require(
            carver.Carve(
                context,
                new LegacyRandomSource(987654321L),
                ChunkPos.Zero,
                ChunkPos.Zero,
                first),
            "CanyonWorldCarver must report its source chunk as processed.");

        carver.Carve(
            context,
            new LegacyRandomSource(987654321L),
            ChunkPos.Zero,
            ChunkPos.Zero,
            second);

        Require(
            first.Positions.Count > 0,
            "The configured canyon carver must mark at least one ellipsoid position.");
        Require(
            first.Positions.SetEquals(second.Positions),
            "CanyonWorldCarver must produce identical carved positions for the same random seed and chunk inputs.");
        Require(
            first.Positions.All(position =>
                position.X is >= 0 and <= 15 &&
                position.Z is >= 0 and <= 15 &&
                position.Y is >= 0 and <= 80),
            "CanyonWorldCarver must keep output coordinates inside the target chunk and CarverOutput vertical bounds.");
    }

    private static void CheckInvalidInputs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(
            WorldCarvers.DIRECT_CODEC.Parse(
                ops,
                ParseJson(
                    """
                    {
                      "type":"minecraft:cave",
                      "probability":1.1,
                      "y":{"absolute":24},
                      "count":1,
                      "thickness":1.0,
                      "room_vertical_radius_multiplier":1.0,
                      "horizontal_radius_multiplier":1.0,
                      "vertical_radius_multiplier":1.0,
                      "floor_level":0.0
                    }
                    """)).IsError,
            "CaveWorldCarver must reject probabilities outside the inclusive zero-to-one interval.");

        Require(
            WorldCarvers.DIRECT_CODEC.Parse(
                ops,
                ParseJson(
                    """
                    {
                      "type":"minecraft:cave",
                      "probability":1.0,
                      "y":{"absolute":24},
                      "count":-1,
                      "thickness":1.0,
                      "room_vertical_radius_multiplier":1.0,
                      "horizontal_radius_multiplier":1.0,
                      "vertical_radius_multiplier":1.0,
                      "floor_level":0.0
                    }
                    """)).IsError,
            "CaveWorldCarver must reject negative tunnel counts.");

        Require(
            WorldCarvers.DIRECT_CODEC.Parse(
                ops,
                ParseJson(
                    """
                    {
                      "type":"minecraft:cave",
                      "probability":1.0,
                      "y":{"absolute":24},
                      "count":1,
                      "thickness":-0.1,
                      "room_vertical_radius_multiplier":1.0,
                      "horizontal_radius_multiplier":1.0,
                      "vertical_radius_multiplier":1.0,
                      "floor_level":0.0
                    }
                    """)).IsError,
            "CaveWorldCarver must reject negative thickness ranges.");

        Require(
            WorldCarvers.DIRECT_CODEC.Parse(
                ops,
                ParseJson(
                    """
                    {
                      "type":"minecraft:canyon",
                      "probability":1.0,
                      "y":{"absolute":24},
                      "vertical_rotation":0.0,
                      "shape":{
                        "distance_factor":0.5,
                        "thickness":1.0,
                        "width_smoothness":0,
                        "horizontal_radius_factor":1.0,
                        "vertical_radius_default_factor":1.0,
                        "vertical_radius_center_factor":1.0,
                        "y_scale":1.0
                      }
                    }
                    """)).IsError,
            "CanyonWorldCarver Shape must reject non-positive width smoothness.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
