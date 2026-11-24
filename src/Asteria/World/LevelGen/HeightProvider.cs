using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Context-dependent vertical coordinate provider from Minecraft Java 26.3.
/// Its codec accepts a VerticalAnchor as shorthand or a registered provider object.
/// </summary>
public abstract class HeightProvider
{
    public static Codec<HeightProvider> CODEC { get; } = CreateCodec();

    public abstract int Sample(IRandomSource random, WorldGenerationContext context);

    public abstract MapCodec<HeightProvider> Codec();

    private static Codec<HeightProvider> CreateCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.HeightProviderType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<HeightProvider> dispatch =
            MapCodec<HeightProvider>.Dispatch<ResourceLocation>(
                typeField,
                provider =>
                {
                    MapCodec<HeightProvider>? codec = provider.Codec();
                    RegistryKey<MapCodec<HeightProvider>>? key =
                        BuiltInRegistries.HeightProviderType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered height provider codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<HeightProvider>? codec =
                        BuiltInRegistries.HeightProviderType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<HeightProvider>>.Success(codec)
                        : DataResult<MapCodec<HeightProvider>>.Error(
                            () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.HeightProviderType.Key}");
                });

        return new AnchorOrDispatchCodec(dispatch.Codec());
    }

    private sealed class AnchorOrDispatchCodec(Codec<HeightProvider> dispatch)
        : Codec<HeightProvider>
    {
        public override DataResult<TData> Encode<TData>(
            HeightProvider input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(ops);

            return input is ConstantHeight constant
                ? VerticalAnchor.CODEC.Encode(constant.Value, ops, prefix)
                : dispatch.Encode(input, ops, prefix);
        }

        public override DataResult<Pair<HeightProvider, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            ArgumentNullException.ThrowIfNull(ops);

            DataResult<Pair<VerticalAnchor, TData>> anchor =
                VerticalAnchor.CODEC.Decode(ops, input);
            if (anchor.IsSuccess)
            {
                return anchor.Map(pair =>
                    new Pair<HeightProvider, TData>(
                        ConstantHeight.Of(pair.First),
                        pair.Second));
            }

            return dispatch.Decode(ops, input);
        }

        public override string ToString() => "HeightProvider";
    }
}
