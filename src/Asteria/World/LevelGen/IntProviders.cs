using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Registry-dispatched context-free integer providers used by Minecraft 26.3 world generation.
/// Numeric values are the inline shorthand for ConstantInt.
/// </summary>
public static class IntProviders
{
    public static Codec<IntProvider> CODEC { get; } = CreateCodec();

    public static Codec<IntProvider> NON_NEGATIVE_CODEC { get; } =
        Codec(0, int.MaxValue);

    public static Codec<IntProvider> POSITIVE_CODEC { get; } =
        Codec(1, int.MaxValue);

    public static Codec<IntProvider> Codec(int minimum, int maximum) =>
        ValidateCodec(minimum, maximum, CODEC);

    public static Codec<T> ValidateCodec<T>(
        int minimum,
        int maximum,
        Codec<T> codec)
        where T : IntProvider
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Validate(provider => Validate(minimum, maximum, provider));
    }

    private static DataResult<T> Validate<T>(
        int minimum,
        int maximum,
        T provider)
        where T : IntProvider
    {
        if (provider.MinInclusive() < minimum)
        {
            return DataResult<T>.Error(() =>
                $"Value provider too low: {minimum} [{provider.MinInclusive()}-{provider.MaxInclusive()}]");
        }

        return provider.MaxInclusive() > maximum
            ? DataResult<T>.Error(() =>
                $"Value provider too high: {maximum} [{provider.MinInclusive()}-{provider.MaxInclusive()}]")
            : DataResult<T>.Success(provider);
    }

    private static Codec<IntProvider> CreateCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.IntProviderType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<IntProvider> dispatch =
            MapCodec<IntProvider>.Dispatch<ResourceLocation>(
                typeField,
                provider =>
                {
                    MapCodec<IntProvider>? codec = provider.Codec();
                    if (codec is null)
                    {
                        return DataResult<ResourceLocation>.Error(
                            () => $"Unregistered integer provider type '{provider.GetType().Name}'");
                    }

                    RegistryKey<MapCodec<IntProvider>>? key =
                        BuiltInRegistries.IntProviderType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered integer provider codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<IntProvider>? codec =
                        BuiltInRegistries.IntProviderType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<IntProvider>>.Success(codec)
                        : DataResult<MapCodec<IntProvider>>.Error(
                            () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.IntProviderType.Key}");
                });

        return new ProviderCodec(dispatch.Codec());
    }

    private sealed class ProviderCodec(Codec<IntProvider> dispatch) : Codec<IntProvider>
    {
        public override DataResult<TData> Encode<TData>(
            IntProvider input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(ops);

            return input is ConstantInt constant
                ? Codecs.INT.Encode(constant.Value, ops, prefix)
                : dispatch.Encode(input, ops, prefix);
        }

        public override DataResult<Pair<IntProvider, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            ArgumentNullException.ThrowIfNull(ops);

            DataResult<Pair<int, TData>> numeric = Codecs.INT.Decode(ops, input);
            if (numeric.IsSuccess)
            {
                return numeric.Map(pair =>
                    new Pair<IntProvider, TData>(
                        ConstantInt.Of(pair.First),
                        pair.Second));
            }

            return dispatch.Decode(ops, input);
        }

        public override string ToString() => "IntProvider";
    }
}
