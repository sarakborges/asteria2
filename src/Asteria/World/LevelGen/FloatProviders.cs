using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Codec dispatch and range validation for context-free Minecraft 26.3 world-generation
/// float providers. Numeric values are the inline shorthand for ConstantFloat.
/// </summary>
public static class FloatProviders
{
    public static Codec<FloatProvider> CODEC { get; } = CreateCodec();

    public static Codec<FloatProvider> Codec(float minimum, float maximum)
    {
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        return CODEC.Validate(provider =>
        {
            if (provider.Min() < minimum)
            {
                return DataResult<FloatProvider>.Error(() =>
                    $"Value provider too low: {minimum} [{provider.Min()}-{provider.Max()}]");
            }

            return provider.Max() > maximum
                ? DataResult<FloatProvider>.Error(() =>
                    $"Value provider too high: {maximum} [{provider.Min()}-{provider.Max()}]")
                : DataResult<FloatProvider>.Success(provider);
        });
    }

    public static Codec<FloatProvider> Codec(float minimum) =>
        CODEC.Validate(provider =>
            provider.Min() < minimum
                ? DataResult<FloatProvider>.Error(() =>
                    $"Value provider too low: {minimum} [{provider.Min()}-{provider.Max()}]")
                : DataResult<FloatProvider>.Success(provider));

    private static Codec<FloatProvider> CreateCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.FloatProviderType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<FloatProvider> dispatch =
            MapCodec<FloatProvider>.Dispatch<ResourceLocation>(
                typeField,
                provider =>
                {
                    MapCodec<FloatProvider>? codec = provider.Codec();
                    if (codec is null)
                    {
                        return DataResult<ResourceLocation>.Error(
                            () => $"Unregistered float provider type '{provider.GetType().Name}'");
                    }

                    RegistryKey<MapCodec<FloatProvider>>? key =
                        BuiltInRegistries.FloatProviderType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered float provider codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<FloatProvider>? codec =
                        BuiltInRegistries.FloatProviderType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<FloatProvider>>.Success(codec)
                        : DataResult<MapCodec<FloatProvider>>.Error(
                            () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.FloatProviderType.Key}");
                });

        return new ProviderCodec(dispatch.Codec());
    }

    private sealed class ProviderCodec(Codec<FloatProvider> dispatch) : Codec<FloatProvider>
    {
        public override DataResult<TData> Encode<TData>(
            FloatProvider input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(ops);

            return input is ConstantFloat constant
                ? Codecs.FLOAT.Encode(constant.Value, ops, prefix)
                : dispatch.Encode(input, ops, prefix);
        }

        public override DataResult<Pair<FloatProvider, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            ArgumentNullException.ThrowIfNull(ops);

            DataResult<Pair<float, TData>> numeric = Codecs.FLOAT.Decode(ops, input);
            if (numeric.IsSuccess)
            {
                return numeric.Map(pair =>
                    new Pair<FloatProvider, TData>(
                        ConstantFloat.Of(pair.First),
                        pair.Second));
            }

            return dispatch.Decode(ops, input);
        }

        public override string ToString() => "FloatProvider";
    }
}
