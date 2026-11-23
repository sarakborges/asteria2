using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// The first complete registration slice of Minecraft 26.3 density-function map codecs.
/// The registry is extended as the remaining density-function node codecs are ported.
/// </summary>
internal static class DensityFunctionCodecBootstrap
{
    private sealed record CodecCatalog(
        MapCodec<DensityFunction> Constant,
        IReadOnlyDictionary<UnaryFunction.Type, MapCodec<DensityFunction>> Unary,
        IReadOnlyDictionary<BinaryFunction.Type, MapCodec<DensityFunction>> Binary,
        IReadOnlyDictionary<RoundFunction.Type, MapCodec<DensityFunction>> Round);

    // Lazy initialization is required because these map codecs compose DIRECT_CODEC,
    // whose registry dispatch is created while DensityFunctions itself is initialized.
    private static readonly Lazy<CodecCatalog> Catalog = new(CreateCatalog);

    internal static void RegisterAll(Registry<MapCodec<DensityFunction>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        CodecCatalog codecs = Catalog.Value;

        Register(registry, "constant", codecs.Constant);

        foreach (UnaryFunction.Type operation in Enum.GetValues<UnaryFunction.Type>())
            Register(registry, UnaryFunction.GetId(operation), codecs.Unary[operation]);

        foreach (RoundFunction.Type operation in Enum.GetValues<RoundFunction.Type>())
            Register(registry, RoundFunction.GetId(operation), codecs.Round[operation]);

        foreach (BinaryFunction.Type operation in Enum.GetValues<BinaryFunction.Type>())
            Register(registry, BinaryFunction.GetId(operation), codecs.Binary[operation]);
    }

    internal static MapCodec<DensityFunction>? GetCodec(DensityFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        CodecCatalog codecs = Catalog.Value;

        return function switch
        {
            ConstantFunction => codecs.Constant,
            UnaryFunction unary => codecs.Unary[unary.Operation],
            BinaryFunction binary => codecs.Binary[binary.Operation],
            RoundFunction round => codecs.Round[round.Operation],
            _ => null
        };
    }

    internal static Codec<DensityFunction> CreateFullDirectCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.DensityFunctionType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<DensityFunction> dispatch =
            MapCodec<DensityFunction>.Dispatch<ResourceLocation>(
                typeField,
                function =>
                {
                    MapCodec<DensityFunction>? codec = GetCodec(function);
                    if (codec is null)
                    {
                        return DataResult<ResourceLocation>.Error(
                            () => $"Unregistered density function type '{function.GetType().Name}'");
                    }

                    RegistryKey<MapCodec<DensityFunction>>? key =
                        BuiltInRegistries.DensityFunctionType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered density function codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<DensityFunction>? codec =
                        BuiltInRegistries.DensityFunctionType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<DensityFunction>>.Success(codec)
                        : DataResult<MapCodec<DensityFunction>>.Error(
                            () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.DensityFunctionType.Key}");
                });

        return dispatch.Codec();
    }

    private static CodecCatalog CreateCatalog()
    {
        MapCodec<DensityFunction> constant =
            DensityFunctions.NOISE_VALUE_CODEC.FieldOf("value")
                .XMap<DensityFunction>(
                    static value => new ConstantFunction(value),
                    static function => ((ConstantFunction)function).Value);

        var unary = Enum.GetValues<UnaryFunction.Type>()
            .ToDictionary(operation => operation, CreateUnaryCodec);

        var binary = Enum.GetValues<BinaryFunction.Type>()
            .ToDictionary(operation => operation, CreateBinaryCodec);

        var round = Enum.GetValues<RoundFunction.Type>()
            .ToDictionary(operation => operation, CreateRoundCodec);

        return new CodecCatalog(constant, unary, binary, round);
    }

    private static MapCodec<DensityFunction> CreateUnaryCodec(UnaryFunction.Type operation) =>
        DensityFunctions.DIRECT_CODEC.FieldOf("input")
            .XMap<DensityFunction>(
                input => new UnaryFunction(operation, input),
                function => ((UnaryFunction)function).Input);

    private static MapCodec<DensityFunction> CreateBinaryCodec(BinaryFunction.Type operation) =>
        RecordCodecBuilder.MapCodec<BinaryFunction>(instance =>
            instance.Group(
                    DensityFunctions.DIRECT_CODEC.FieldOf("left")
                        .ForGetter<BinaryFunction>(static function => function.Left),
                    DensityFunctions.DIRECT_CODEC.FieldOf("right")
                        .ForGetter<BinaryFunction>(static function => function.Right))
                .Apply(
                    instance,
                    (left, right) => new BinaryFunction(operation, left, right)))
            .XMap<DensityFunction>(
                static function => function,
                function => (BinaryFunction)function);

    private static MapCodec<DensityFunction> CreateRoundCodec(RoundFunction.Type operation) =>
        RecordCodecBuilder.MapCodec<RoundFunction>(instance =>
            instance.Group(
                    DensityFunctions.DIRECT_CODEC.FieldOf("input")
                        .ForGetter<RoundFunction>(static function => function.Input),
                    DensityFunctions.DIRECT_CODEC.OptionalFieldOf(
                            "multiple",
                            DensityFunctions.Constant(1.0f))
                        .ForGetter<RoundFunction>(static function => function.Multiple))
                .Apply(
                    instance,
                    (input, multiple) => new RoundFunction(operation, input, multiple)))
            .XMap<DensityFunction>(
                static function => function,
                function => (RoundFunction)function);

    private static void Register(
        Registry<MapCodec<DensityFunction>> registry,
        string path,
        MapCodec<DensityFunction> codec)
    {
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(path);
        RegistryKey<MapCodec<DensityFunction>> key =
            RegistryKey<MapCodec<DensityFunction>>.Create(
                RegistryKey<MapCodec<DensityFunction>>.CreateRegistryKey(registry.Location),
                location);

        registry.Register(key, codec, RegistrationInfo.BuiltIn);
    }
}

/// <summary>
/// DensityFunctions.DIRECT_CODEC: scalar constants first, then registered typed map codecs.
/// Encoding selects the scalar form for ConstantFunction and the type-dispatch form otherwise.
/// </summary>
internal sealed class DensityFunctionDirectCodec : Codec<DensityFunction>
{
    private readonly Codec<float> _noiseValueCodec;
    private readonly Codec<DensityFunction> _fullDirectCodec;

    public DensityFunctionDirectCodec(
        Codec<float> noiseValueCodec,
        Codec<DensityFunction> fullDirectCodec)
    {
        _noiseValueCodec = noiseValueCodec ?? throw new ArgumentNullException(nameof(noiseValueCodec));
        _fullDirectCodec = fullDirectCodec ?? throw new ArgumentNullException(nameof(fullDirectCodec));
    }

    public override DataResult<TData> Encode<TData>(
        DensityFunction input,
        DynamicOps<TData> ops,
        TData prefix)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input is ConstantFunction constant
            ? _noiseValueCodec.Encode(constant.Value, ops, prefix)
            : _fullDirectCodec.Encode(input, ops, prefix);
    }

    public override DataResult<Pair<DensityFunction, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input)
    {
        DataResult<Pair<DensityFunction, TData>> scalar =
            _noiseValueCodec.Decode(ops, input).Map(pair =>
                new Pair<DensityFunction, TData>(
                    new ConstantFunction(pair.First),
                    pair.Second));

        if (scalar.IsSuccess)
            return scalar;

        DataResult<Pair<DensityFunction, TData>> typed =
            _fullDirectCodec.Decode(ops, input);

        if (typed.IsSuccess)
            return typed;
        if (scalar.HasResultOrPartial)
            return scalar;
        if (typed.HasResultOrPartial)
            return typed;

        return DataResult<Pair<DensityFunction, TData>>.Error(
            () => $"Failed to parse either. First: {scalar.ErrorMessage}; Second: {typed.ErrorMessage}");
    }
}
