using Asteria.Content.Identifiers;
using Asteria.Serialization;

namespace Asteria.Content.Registries;

/// <summary>
/// Registry-aware Holder codecs matching the fixed-reference and inline-file codec contracts.
/// </summary>
public static class RegistryCodecs
{
    private static Codec<ResourceLocation> ResourceLocationCodec { get; } =
        Codecs.STRING.FlatXMap(
            static value =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(value);
                return location.HasValue
                    ? DataResult<ResourceLocation>.Success(location.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{value}'");
            },
            static location => DataResult<string>.Success(location.ToString()));

    /// <summary>Codec for registered references only. Inline values are never accepted.</summary>
    public static Codec<Holder<T>> Holder<T>(
        RegistryKey<Registry<T>> registryKey)
        where T : class =>
        new RegistryFixedCodec<T>(registryKey);

    /// <summary>Codec for registry references with optional inline values.</summary>
    public static Codec<Holder<T>> Holder<T>(
        RegistryKey<Registry<T>> registryKey,
        Codec<T> elementCodec,
        bool allowInline = true)
        where T : class =>
        new RegistryFileCodec<T>(registryKey, elementCodec, allowInline);

    private sealed class RegistryFixedCodec<T> : Codec<Holder<T>>
        where T : class
    {
        private readonly RegistryKey<Registry<T>> _registryKey;

        public RegistryFixedCodec(RegistryKey<Registry<T>> registryKey) =>
            _registryKey = registryKey;

        public override DataResult<TData> Encode<TData>(
            Holder<T> input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (ops is not RegistryOps<TData> registryOps)
            {
                return DataResult<TData>.Error(
                    () => $"Can't access registry {_registryKey.Location}");
            }

            HolderLookup<T>.RegistryLookup? owner = registryOps.Lookup<T>(_registryKey);
            if (owner is null)
            {
                return DataResult<TData>.Error(
                    () => $"Can't access registry {_registryKey.Location}");
            }

            if (!input.CanSerializeIn(owner))
            {
                return DataResult<TData>.Error(
                    () => $"Element {input} is not valid in current registry set");
            }

            HolderUnwrap<T> unwrapped = input.Unwrap();
            if (!unwrapped.IsKey || !unwrapped.Key.HasValue)
            {
                return DataResult<TData>.Error(
                    () => $"Elements from registry {_registryKey} can't be serialized to a value");
            }

            return ResourceLocationCodec.Encode(
                unwrapped.Key.Value.Location,
                ops,
                prefix);
        }

        public override DataResult<Pair<Holder<T>, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            if (ops is not RegistryOps<TData> registryOps)
            {
                return DataResult<Pair<Holder<T>, TData>>.Error(
                    () => $"Can't access registry {_registryKey.Location}");
            }

            HolderLookup<T>.RegistryLookup? lookup = registryOps.Lookup<T>(_registryKey);
            if (lookup is null)
            {
                return DataResult<Pair<Holder<T>, TData>>.Error(
                    () => $"Can't access registry {_registryKey.Location}");
            }

            return ResourceLocationCodec.Decode(ops, input).FlatMap(pair =>
            {
                RegistryKey<T> key = RegistryKey<T>.Create(_registryKey, pair.First);
                Holder<T>.Reference? holder = lookup.Get(key);
                return holder is not null
                    ? DataResult<Pair<Holder<T>, TData>>.Success(
                        new Pair<Holder<T>, TData>(holder, pair.Second))
                    : DataResult<Pair<Holder<T>, TData>>.Error(
                        () => $"Failed to get element {key.Location} from registry {_registryKey.Location}");
            });
        }

        public override string ToString() =>
            $"RegistryFixedCodec[{_registryKey}]";
    }

    private sealed class RegistryFileCodec<T> : Codec<Holder<T>>
        where T : class
    {
        private readonly RegistryKey<Registry<T>> _registryKey;
        private readonly Codec<T> _elementCodec;
        private readonly bool _allowInline;

        public RegistryFileCodec(
            RegistryKey<Registry<T>> registryKey,
            Codec<T> elementCodec,
            bool allowInline)
        {
            _registryKey = registryKey;
            _elementCodec = elementCodec ?? throw new ArgumentNullException(nameof(elementCodec));
            _allowInline = allowInline;
        }

        public override DataResult<TData> Encode<TData>(
            Holder<T> input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (ops is RegistryOps<TData> registryOps &&
                registryOps.Lookup<T>(_registryKey) is HolderLookup<T>.RegistryLookup owner)
            {
                if (!input.CanSerializeIn(owner))
                {
                    return DataResult<TData>.Error(
                        () => $"Element {input} is not valid in current registry set");
                }

                HolderUnwrap<T> unwrapped = input.Unwrap();
                return unwrapped.IsKey && unwrapped.Key.HasValue
                    ? ResourceLocationCodec.Encode(
                        unwrapped.Key.Value.Location,
                        ops,
                        prefix)
                    : _elementCodec.Encode(unwrapped.Value!, ops, prefix);
            }

            return _elementCodec.Encode(input.Value, ops, prefix);
        }

        public override DataResult<Pair<Holder<T>, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            if (ops is RegistryOps<TData> registryOps)
            {
                DataResult<Pair<ResourceLocation, TData>> identifier =
                    ResourceLocationCodec.Decode(ops, input);

                if (identifier.TryGetResult(out Pair<ResourceLocation, TData> pair))
                {
                    HolderLookup<T>.RegistryLookup? lookup = registryOps.Lookup<T>(_registryKey);
                    if (lookup is null)
                    {
                        return DataResult<Pair<Holder<T>, TData>>.Error(
                            () => $"Registry does not exist: {_registryKey.Location}");
                    }

                    RegistryKey<T> key = RegistryKey<T>.Create(_registryKey, pair.First);
                    Holder<T>.Reference? holder = lookup.Get(key);
                    return holder is not null
                        ? DataResult<Pair<Holder<T>, TData>>.Success(
                            new Pair<Holder<T>, TData>(holder, pair.Second))
                        : DataResult<Pair<Holder<T>, TData>>.Error(
                            () => $"Failed to get element {key.Location} from registry {_registryKey.Location}");
                }

                if (!_allowInline)
                {
                    return DataResult<Pair<Holder<T>, TData>>.Error(
                        "Inline definitions not allowed here");
                }
            }

            return _elementCodec.Decode(ops, input).Map(pair =>
                new Pair<Holder<T>, TData>(
                    global::Asteria.Content.Registries.Holder<T>.Direct(pair.First),
                    pair.Second));
        }

        public override string ToString() =>
            $"RegistryFileCodec[{_registryKey} {_elementCodec}]";
    }
}
