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

    /// <summary>
    /// Codec for a registry-backed holder set. Named sets use their tag key only when
    /// the matching registry context is available; otherwise their contents are encoded directly.
    /// </summary>
    public static Codec<HolderSet<T>> HolderSet<T>(
        RegistryKey<Registry<T>> registryKey) where T : class =>
        HolderSet<T>(registryKey, alwaysUseList: false);

    public static Codec<HolderSet<T>> HolderSet<T>(
        RegistryKey<Registry<T>> registryKey,
        bool alwaysUseList) where T : class
    {
        Codec<Holder<T>> elementCodec = RegistryCodecs.Holder<T>(registryKey);
        Codec<IReadOnlyList<Holder<T>>> directCodec = alwaysUseList
            ? elementCodec.ListOf()
            : ExtraCodecs.CompactListCodec(elementCodec);
        return new RegistryHolderSetCodec<T>(registryKey, directCodec);
    }

    private sealed class RegistryHolderSetCodec<T> : Codec<HolderSet<T>> where T : class
    {
        private readonly RegistryKey<Registry<T>> _registryKey;
        private readonly Codec<TagKey<T>> _tagCodec;
        private readonly Codec<IReadOnlyList<Holder<T>>> _directCodec;

        public RegistryHolderSetCodec(
            RegistryKey<Registry<T>> registryKey,
            Codec<IReadOnlyList<Holder<T>>> directCodec)
        {
            _registryKey = registryKey;
            _tagCodec = TagKey<T>.HashedCodec(registryKey);
            _directCodec = directCodec;
        }

        public override DataResult<TData> Encode<TData>(
            HolderSet<T> input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input is HolderSet<T>.Named named && ops is RegistryOps<TData> registryOps)
            {
                HolderLookup<T>.RegistryLookup? owner = registryOps.Lookup<T>(_registryKey);
                if (owner is null)
                {
                    return DataResult<TData>.Error(
                        () => $"Registry {_registryKey.Location} is not available in this context");
                }

                if (!named.CanSerializeIn(owner))
                {
                    return DataResult<TData>.Error(
                        () => $"HolderSet {named} is not valid in current registry set");
                }

                return _tagCodec.Encode(named.Key, ops, prefix);
            }

            return _directCodec.Encode(input, ops, prefix);
        }

        public override DataResult<Pair<HolderSet<T>, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            DataResult<Pair<TagKey<T>, TData>> decodedTag = _tagCodec.Decode(ops, input);
            if (decodedTag.TryGetResult(out Pair<TagKey<T>, TData> tagPair))
            {
                if (ops is not RegistryOps<TData> registryOps)
                {
                    return DataResult<Pair<HolderSet<T>, TData>>.Error(
                        "Registries are not available in this context");
                }

                HolderLookup<T>.RegistryLookup? lookup = registryOps.Lookup<T>(_registryKey);
                if (lookup is null)
                {
                    return DataResult<Pair<HolderSet<T>, TData>>.Error(
                        () => $"Registry {_registryKey.Location} is not available in this context");
                }

                HolderSet<T>.Named? named = lookup.Get(tagPair.First);
                return named is not null
                    ? DataResult<Pair<HolderSet<T>, TData>>.Success(
                        new Pair<HolderSet<T>, TData>(named, tagPair.Second))
                    : DataResult<Pair<HolderSet<T>, TData>>.Error(
                        () => $"Missing tag: '{tagPair.First.Location}' in '{tagPair.First.Registry.Location}'");
            }

            DataResult<Pair<IReadOnlyList<Holder<T>>, TData>> decodedDirect =
                _directCodec.Decode(ops, input);
            if (decodedDirect.TryGetResult(out Pair<IReadOnlyList<Holder<T>>, TData> directPair))
            {
                HolderSet<T> direct = Asteria.Content.Registries.HolderSet<T>.Direct(directPair.First);
                return DataResult<Pair<HolderSet<T>, TData>>.Success(
                    new Pair<HolderSet<T>, TData>(direct, directPair.Second));
            }

            return DataResult<Pair<HolderSet<T>, TData>>.Error(
                () => $"Failed to parse either. First: {decodedTag.ErrorMessage}; Second: {decodedDirect.ErrorMessage}");
        }
    }

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
