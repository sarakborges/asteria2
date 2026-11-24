using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;

namespace Asteria.World.LevelGen.Features.StateProviders;

/// <summary>
/// Supplies a block state for one feature placement position.
/// The typed codec, state-or-provider shorthand and holder codec follow Minecraft Java 26.3.
/// </summary>
public abstract record BlockStateProvider
{
    public static Codec<BlockStateProvider> TYPED_CODEC { get; } = CreateTypedCodec();

    public static Codec<BlockStateProvider> STATE_OR_PROVIDER_CODEC { get; } =
        new StateOrProviderCodec();

    /// <summary>Provider object codec, accepting inline typed values.</summary>
    public static Codec<BlockStateProvider> DIRECT_CODEC { get; } = TYPED_CODEC;

    /// <summary>Registry reference or inline provider codec.</summary>
    public static Codec<Holder<BlockStateProvider>> CODEC { get; } =
        RegistryCodecs.Holder(
            Registries.BLOCK_STATE_PROVIDER,
            DIRECT_CODEC);

    public abstract BlockState GetState(BlockGetter level, IRandomSource random, BlockPos position);

    public virtual BlockState? GetOptionalState(
        BlockGetter level,
        IRandomSource random,
        BlockPos position) =>
        GetState(level, random, position);

    public abstract MapCodec<BlockStateProvider> Codec();

    public static SimpleStateProvider Of(BlockState state) =>
        new(state ?? throw new ArgumentNullException(nameof(state)));

    public static SimpleStateProvider Of(Block block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return Of(block.DefaultState);
    }

    public static Holder<BlockStateProvider> HolderOf(BlockState state) =>
        Holder<BlockStateProvider>.Direct(Of(state));

    public static Holder<BlockStateProvider> HolderOf(Block block) =>
        Holder<BlockStateProvider>.Direct(Of(block));

    private static Codec<BlockStateProvider> CreateTypedCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid block-state provider type key '{name}' for {BuiltInRegistries.BlockStateProviderType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<BlockStateProvider> dispatch =
            MapCodec<BlockStateProvider>.Dispatch<ResourceLocation>(
                typeField,
                provider =>
                {
                    MapCodec<BlockStateProvider> codec = provider.Codec();
                    RegistryKey<MapCodec<BlockStateProvider>>? key =
                        BuiltInRegistries.BlockStateProviderType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered block-state provider codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<BlockStateProvider>? codec =
                        BuiltInRegistries.BlockStateProviderType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<BlockStateProvider>>.Success(codec)
                        : DataResult<MapCodec<BlockStateProvider>>.Error(
                            () => $"Unknown block-state provider type '{identifier}' in {BuiltInRegistries.BlockStateProviderType.Key}");
                });

        return dispatch.Codec();
    }

    private sealed class StateOrProviderCodec : Codec<BlockStateProvider>
    {
        public override DataResult<TData> Encode<TData>(
            BlockStateProvider input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(ops);

            return input is SimpleStateProvider simple
                ? BlockState.FULL_CODEC.Encode(simple.State, ops, prefix)
                : BlockStateProvider.TYPED_CODEC.Encode(input, ops, prefix);
        }

        public override DataResult<Pair<BlockStateProvider, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            ArgumentNullException.ThrowIfNull(ops);

            DataResult<Pair<BlockState, TData>> state = BlockState.FULL_CODEC.Decode(ops, input);
            if (state.TryGetResult(out Pair<BlockState, TData> statePair))
            {
                return DataResult<Pair<BlockStateProvider, TData>>.Success(
                    new Pair<BlockStateProvider, TData>(
                        Of(statePair.First),
                        statePair.Second));
            }

            return BlockStateProvider.TYPED_CODEC.Decode(ops, input);
        }

        public override string ToString() => "BlockStateProvider.STATE_OR_PROVIDER_CODEC";
    }
}
