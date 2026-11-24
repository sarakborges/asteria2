using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.Util.Random;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features.StateProviders;

internal static class BlockStateProviderChecks
{
    public static void Run()
    {
        CheckProviderRegistries();
        CheckSimpleProviderCodecAndShorthand();
        CheckWeightedProviderCodecAndSampling();
        CheckHolderCodec();
        CheckFactories();
    }

    private static void CheckProviderRegistries()
    {
        Require(
            BuiltInRegistries.BlockStateProviderType.Count == 2,
            "The block-state provider type registry must contain the two implemented 26.3 algorithms.");

        Require(
            BuiltInRegistries.BlockStateProviderType.ContainsKey(ResourceLocation.Parse("asteria:simple")) &&
            BuiltInRegistries.BlockStateProviderType.ContainsKey(ResourceLocation.Parse("asteria:weighted")),
            "The block-state provider registry must expose its simple and weighted codec identifiers.");

        Require(
            !BuiltInRegistries.BlockStateProviderType.ContainsKey(ResourceLocation.Parse("minecraft:simple")),
            "Asteria runtime registrations must keep the Asteria namespace.");
    }

    private static void CheckSimpleProviderCodecAndShorthand()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        BlockStateProvider decoded = BlockStateProvider.TYPED_CODEC
            .Parse(
                ops,
                ParseJson("""{"type":"asteria:simple","state":{"id":"minecraft:stone"}}"""))
            .GetOrThrow();

        Require(
            decoded is SimpleStateProvider { State: var state } &&
            ReferenceEquals(state, Blocks.STONE.DefaultState),
            "The simple provider type codec must decode the full block-state field.");

        JsonElement typed = BlockStateProvider.TYPED_CODEC
            .EncodeStart(ops, decoded)
            .GetOrThrow();
        Require(
            ops.Get(typed, "type").GetOrThrow().GetString() == "asteria:simple" &&
            ops.Get(ops.Get(typed, "state").GetOrThrow(), "id").GetOrThrow().GetString() == "minecraft:stone",
            "The simple provider must encode as a typed provider with a full block-state object.");

        BlockStateProvider shorthand = BlockStateProvider.STATE_OR_PROVIDER_CODEC
            .Parse(ops, ParseJson("""{"id":"minecraft:dirt"}"""))
            .GetOrThrow();
        Require(
            shorthand is SimpleStateProvider { State: var dirt } &&
            ReferenceEquals(dirt, Blocks.DIRT.DefaultState),
            "STATE_OR_PROVIDER_CODEC must accept a full inline BlockState as a simple provider.");

        JsonElement stateShorthand = BlockStateProvider.STATE_OR_PROVIDER_CODEC
            .EncodeStart(ops, BlockStateProvider.Of(Blocks.GRASS_BLOCK.DefaultState))
            .GetOrThrow();
        Require(
            ops.Get(stateShorthand, "id").GetOrThrow().GetString() == "minecraft:grass_block" &&
            !ops.Get(stateShorthand, "type").TryGetResult(out _),
            "A simple provider must use the inline BlockState shorthand in STATE_OR_PROVIDER_CODEC.");

        Require(
            BlockStateProvider.STATE_OR_PROVIDER_CODEC
                .Parse(ops, ParseJson("""{"id":"minecraft:missing_block"}""")).IsError,
            "The inline BlockState shorthand must reject unknown block identifiers.");
    }

    private static void CheckWeightedProviderCodecAndSampling()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var weightedList = WeightedList<BlockState>.OfWeighted(
            new Weighted<BlockState>(Blocks.STONE.DefaultState, 2),
            new Weighted<BlockState>(Blocks.DIRT.DefaultState, 1));
        var provider = new WeightedStateProvider(weightedList);

        JsonElement encoded = BlockStateProvider.TYPED_CODEC
            .EncodeStart(ops, provider)
            .GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:weighted" &&
            ops.Get(encoded, "entries").GetOrThrow().ValueKind == JsonValueKind.Array,
            "The weighted provider must encode its non-empty weighted state entries.");

        BlockStateProvider decoded = BlockStateProvider.TYPED_CODEC
            .Parse(ops, encoded)
            .GetOrThrow();
        Require(
            decoded is WeightedStateProvider weighted &&
            weighted.WeightedList.Count == 2 &&
            ReferenceEquals(weighted.WeightedList[0].Value, Blocks.STONE.DefaultState) &&
            weighted.WeightedList[0].Weight == 2 &&
            ReferenceEquals(weighted.WeightedList[1].Value, Blocks.DIRT.DefaultState) &&
            weighted.WeightedList[1].Weight == 1,
            "The weighted provider codec must round-trip the state entries and their weights.");

        var world = new OverworldWorld(
            seed: 0x425350524F564944L,
            LevelHeightAccessor.Create(-64, 96));
        var expectedRandom = new LegacyRandomSource(556677L);
        BlockState expected = expectedRandom.NextInt(weightedList.TotalWeight) < 2
            ? Blocks.STONE.DefaultState
            : Blocks.DIRT.DefaultState;
        BlockState actual = provider.GetState(
            world,
            new LegacyRandomSource(556677L),
            new BlockPos(3, 40, 5));

        Require(
            ReferenceEquals(actual, expected),
            "WeightedStateProvider must use the weighted-list selector and exactly one bounded random draw.");

        Require(
            BlockStateProvider.TYPED_CODEC
                .Parse(
                    ops,
                    ParseJson("""{"type":"asteria:weighted","entries":[]}"""))
                .IsError,
            "WeightedStateProvider must reject an empty distribution.");
    }

    private static void CheckHolderCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement inline = ParseJson(
            """{"type":"asteria:simple","state":{"id":"minecraft:stone"}}""");

        Holder<BlockStateProvider> holder = BlockStateProvider.CODEC
            .Parse(ops, inline)
            .GetOrThrow();
        Require(
            holder.Kind == HolderKind.Direct &&
            holder.Value is SimpleStateProvider { State: var state } &&
            ReferenceEquals(state, Blocks.STONE.DefaultState),
            "BlockStateProvider.CODEC must accept inline typed values and wrap them in a direct holder.");

        JsonElement encoded = BlockStateProvider.CODEC
            .EncodeStart(ops, holder)
            .GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:simple",
            "The holder codec must encode direct providers through the registered typed codec.");
    }

    private static void CheckFactories()
    {
        SimpleStateProvider blockProvider = BlockStateProvider.Of(Blocks.STONE);
        SimpleStateProvider stateProvider = BlockStateProvider.Of(Blocks.DIRT.DefaultState);
        Holder<BlockStateProvider> blockHolder = BlockStateProvider.HolderOf(Blocks.STONE);
        Holder<BlockStateProvider> stateHolder = BlockStateProvider.HolderOf(Blocks.DIRT.DefaultState);

        Require(
            ReferenceEquals(blockProvider.State, Blocks.STONE.DefaultState) &&
            ReferenceEquals(stateProvider.State, Blocks.DIRT.DefaultState) &&
            blockHolder.Value is SimpleStateProvider { State: var blockState } &&
            ReferenceEquals(blockState, Blocks.STONE.DefaultState) &&
            stateHolder.Value is SimpleStateProvider { State: var heldState } &&
            ReferenceEquals(heldState, Blocks.DIRT.DefaultState),
            "BlockStateProvider.of and holderOf factories must preserve the supplied block or state.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
