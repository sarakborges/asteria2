using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
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
        MapCodec<DensityFunction> Noise,
        MapCodec<DensityFunction> DistanceToPoint,
        MapCodec<DensityFunction> EndOuterIslands,
        MapCodec<DensityFunction> ShiftA,
        MapCodec<DensityFunction> ShiftB,
        MapCodec<DensityFunction> Shift,
        IReadOnlyDictionary<UnaryFunction.Type, MapCodec<DensityFunction>> Unary,
        IReadOnlyDictionary<BinaryFunction.Type, MapCodec<DensityFunction>> Binary,
        IReadOnlyDictionary<RoundFunction.Type, MapCodec<DensityFunction>> Round,
        MapCodec<DensityFunction> Pow,
        MapCodec<DensityFunction> Spline,
        MapCodec<DensityFunction> Lerp,
        MapCodec<DensityFunction> Cache,
        MapCodec<DensityFunction> Interpolated,
        MapCodec<DensityFunction> Clamp,
        MapCodec<DensityFunction> RangeChoice,
        MapCodec<DensityFunction> IntervalSelect,
        MapCodec<DensityFunction> Slice,
        MapCodec<DensityFunction> Gradient,
        MapCodec<DensityFunction> FindTopSurface);

    // Lazy initialization is required because these map codecs compose DIRECT_CODEC,
    // whose registry dispatch is created while DensityFunctions itself is initialized.
    private static readonly Lazy<CodecCatalog> Catalog = new(CreateCatalog);

    internal static void RegisterAll(Registry<MapCodec<DensityFunction>> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        CodecCatalog codecs = Catalog.Value;

        Register(registry, "constant", codecs.Constant);
        Register(registry, "noise", codecs.Noise);
        Register(registry, "end_outer_islands", codecs.EndOuterIslands);
        Register(registry, "distance_to_point", codecs.DistanceToPoint);
        Register(registry, "gradient", codecs.Gradient);
        Register(registry, "shift_a", codecs.ShiftA);
        Register(registry, "shift_b", codecs.ShiftB);
        Register(registry, "shift", codecs.Shift);

        foreach (UnaryFunction.Type operation in Enum.GetValues<UnaryFunction.Type>())
            Register(registry, UnaryFunction.GetId(operation), codecs.Unary[operation]);

        foreach (RoundFunction.Type operation in Enum.GetValues<RoundFunction.Type>())
            Register(registry, RoundFunction.GetId(operation), codecs.Round[operation]);

        foreach (BinaryFunction.Type operation in Enum.GetValues<BinaryFunction.Type>())
            Register(registry, BinaryFunction.GetId(operation), codecs.Binary[operation]);

        Register(registry, "pow", codecs.Pow);
        Register(registry, "spline", codecs.Spline);
        Register(registry, "lerp", codecs.Lerp);
        Register(registry, "clamp", codecs.Clamp);
        Register(registry, "range_choice", codecs.RangeChoice);
        Register(registry, "interval_select", codecs.IntervalSelect);
        Register(registry, "cache", codecs.Cache);
        Register(registry, "interpolated", codecs.Interpolated);
        Register(registry, "slice", codecs.Slice);
        Register(registry, "find_top_surface", codecs.FindTopSurface);
    }

    internal static MapCodec<DensityFunction>? GetCodec(DensityFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        CodecCatalog codecs = Catalog.Value;

        return function switch
        {
            ConstantFunction => codecs.Constant,
            NoiseFunction => codecs.Noise,
            DistanceToPointFunction => codecs.DistanceToPoint,
            EndIslandFunction => codecs.EndOuterIslands,
            ShiftNoiseFunction.ShiftA => codecs.ShiftA,
            ShiftNoiseFunction.ShiftB => codecs.ShiftB,
            ShiftNoiseFunction.Shift => codecs.Shift,
            UnaryFunction unary => codecs.Unary[unary.Operation],
            BinaryFunction binary => codecs.Binary[binary.Operation],
            RoundFunction round => codecs.Round[round.Operation],
            PowFunction => codecs.Pow,
            SplineFunction => codecs.Spline,
            LerpFunction => codecs.Lerp,
            CacheFunction => codecs.Cache,
            InterpolatedFunction => codecs.Interpolated,
            ClampFunction => codecs.Clamp,
            RangeChoiceFunction => codecs.RangeChoice,
            IntervalSelectFunction => codecs.IntervalSelect,
            SliceFunction => codecs.Slice,
            GradientFunction => codecs.Gradient,
            FindTopSurfaceFunction => codecs.FindTopSurface,
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

        return new CodecCatalog(
            constant,
            CreateNoiseCodec(),
            CreateDistanceToPointCodec(),
            CreateEndIslandCodec(),
            CreateShiftACodec(),
            CreateShiftBCodec(),
            CreateShiftCodec(),
            unary,
            binary,
            round,
            CreatePowCodec(),
            CreateSplineCodec(),
            CreateLerpCodec(),
            CreateCacheCodec(),
            CreateInterpolatedCodec(),
            CreateClampCodec(),
            CreateRangeChoiceCodec(),
            CreateIntervalSelectCodec(),
            CreateSliceCodec(),
            CreateGradientCodec(),
            CreateFindTopSurfaceCodec());
    }

    private static MapCodec<DensityFunction> CreateNoiseCodec() =>
        NoiseFunction.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (NoiseFunction)function);

    private static MapCodec<DensityFunction> CreateShiftACodec() =>
        ShiftNoiseFunction.ShiftA.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (ShiftNoiseFunction.ShiftA)function);

    private static MapCodec<DensityFunction> CreateShiftBCodec() =>
        ShiftNoiseFunction.ShiftB.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (ShiftNoiseFunction.ShiftB)function);

    private static MapCodec<DensityFunction> CreateShiftCodec() =>
        ShiftNoiseFunction.Shift.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (ShiftNoiseFunction.Shift)function);

    private static MapCodec<DensityFunction> CreateEndIslandCodec() =>
        EndIslandFunction.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (EndIslandFunction)function);

    private static MapCodec<DensityFunction> CreateDistanceToPointCodec() =>
        DistanceToPointFunction.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (DistanceToPointFunction)function);

    private static MapCodec<DensityFunction> CreateUnaryCodec(UnaryFunction.Type operation) =>
        DensityFunction.CODEC.FieldOf("input")
            .XMap<DensityFunction>(
                input => new UnaryFunction(operation, input),
                function => ((UnaryFunction)function).Input);

    private static MapCodec<DensityFunction> CreateBinaryCodec(BinaryFunction.Type operation) =>
        RecordCodecBuilder.MapCodec<BinaryFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("left")
                        .ForGetter<BinaryFunction>(static function => function.Left),
                    DensityFunction.CODEC.FieldOf("right")
                        .ForGetter<BinaryFunction>(static function => function.Right))
                .Apply(
                    instance,
                    (left, right) => new BinaryFunction(operation, left, right)))
            .XMap<DensityFunction>(
                static function => function,
                function => (BinaryFunction)function);

    private static MapCodec<DensityFunction> CreatePowCodec() =>
        RecordCodecBuilder.MapCodec<PowFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("base")
                        .ForGetter<PowFunction>(static function => function.Base),
                    DensityFunction.CODEC.FieldOf("exponent")
                        .ForGetter<PowFunction>(static function => function.Exponent))
                .Apply(
                    instance,
                    static (baseFunction, exponent) =>
                        new PowFunction(baseFunction, exponent)))
            .XMap<DensityFunction>(
                static function => function,
                function => (PowFunction)function);

    private static MapCodec<DensityFunction> CreateSplineCodec() =>
        SplineFunction.CODEC.XMap<DensityFunction>(
            static function => function,
            static function => (SplineFunction)function);

    private static MapCodec<DensityFunction> CreateLerpCodec() =>
        RecordCodecBuilder.MapCodec<LerpFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("alpha")
                        .ForGetter<LerpFunction>(static function => function.Alpha),
                    DensityFunction.CODEC.FieldOf("first")
                        .ForGetter<LerpFunction>(static function => function.First),
                    DensityFunction.CODEC.FieldOf("second")
                        .ForGetter<LerpFunction>(static function => function.Second))
                .Apply(
                    instance,
                    static (alpha, first, second) =>
                        new LerpFunction(alpha, first, second)))
            .XMap<DensityFunction>(
                static function => function,
                function => (LerpFunction)function);

    private static MapCodec<DensityFunction> CreateCacheCodec() =>
        DensityFunction.CODEC.FieldOf("input")
            .XMap<DensityFunction>(
                static input => new CacheFunction(input),
                static function => ((CacheFunction)function).Input);

    private static MapCodec<DensityFunction> CreateInterpolatedCodec() =>
        RecordCodecBuilder.MapCodec<InterpolatedFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("input")
                        .ForGetter<InterpolatedFunction>(static function => function.Input),
                    Codecs.IntRange(1, int.MaxValue).FieldOf("cell_size_xz")
                        .ForGetter<InterpolatedFunction>(static function => function.CellSizeXz),
                    Codecs.IntRange(1, int.MaxValue).FieldOf("cell_size_y")
                        .ForGetter<InterpolatedFunction>(static function => function.CellSizeY))
                .Apply(
                    instance,
                    static (input, cellSizeXz, cellSizeY) =>
                        new InterpolatedFunction(input, cellSizeXz, cellSizeY)))
            .XMap<DensityFunction>(
                static function => function,
                function => (InterpolatedFunction)function);

    private static MapCodec<DensityFunction> CreateClampCodec() =>
        RecordCodecBuilder.MapCodec<ClampFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("input")
                        .ForGetter<ClampFunction>(static function => function.Input),
                    DensityFunctions.NOISE_VALUE_CODEC.FieldOf("min")
                        .ForGetter<ClampFunction>(static function => function.Min),
                    DensityFunctions.NOISE_VALUE_CODEC.FieldOf("max")
                        .ForGetter<ClampFunction>(static function => function.Max))
                .Apply(
                    instance,
                    static (input, min, max) => new ClampFunction(input, min, max)))
            .Validate(static function =>
                function.Max < function.Min
                    ? DataResult<ClampFunction>.Error(
                        () => $"min ({function.Min}) must be less than or equal to max ({function.Max})")
                    : DataResult<ClampFunction>.Success(function))
            .XMap<DensityFunction>(
                static function => function,
                function => (ClampFunction)function);

    private static MapCodec<DensityFunction> CreateRangeChoiceCodec() =>
        RecordCodecBuilder.MapCodec<RangeChoiceFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("input")
                        .ForGetter<RangeChoiceFunction>(static function => function.Input),
                    CreateRangeChoiceBoundsCodec()
                        .ForGetter<RangeChoiceFunction>(static function =>
                            new RangeChoiceBounds(function.MinInclusive, function.MaxExclusive)),
                    CreateRangeChoiceBranchesCodec()
                        .ForGetter<RangeChoiceFunction>(static function =>
                            new RangeChoiceBranches(function.WhenInRange, function.WhenOutOfRange)))
                .Apply(
                    instance,
                    static (input, bounds, branches) =>
                        new RangeChoiceFunction(
                            input,
                            bounds.MinInclusive,
                            bounds.MaxExclusive,
                            branches.WhenInRange,
                            branches.WhenOutOfRange)))
            .XMap<DensityFunction>(
                static function => function,
                function => (RangeChoiceFunction)function);

    private static MapCodec<RangeChoiceBounds> CreateRangeChoiceBoundsCodec() =>
        RecordCodecBuilder.MapCodec<RangeChoiceBounds>(instance =>
            instance.Group(
                    DensityFunctions.NOISE_VALUE_CODEC.FieldOf("min_inclusive")
                        .ForGetter<RangeChoiceBounds>(static bounds => bounds.MinInclusive),
                    DensityFunctions.NOISE_VALUE_CODEC.FieldOf("max_exclusive")
                        .ForGetter<RangeChoiceBounds>(static bounds => bounds.MaxExclusive))
                .Apply(
                    instance,
                    static (minInclusive, maxExclusive) =>
                        new RangeChoiceBounds(minInclusive, maxExclusive)));

    private static MapCodec<RangeChoiceBranches> CreateRangeChoiceBranchesCodec() =>
        RecordCodecBuilder.MapCodec<RangeChoiceBranches>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("when_in_range")
                        .ForGetter<RangeChoiceBranches>(static branches => branches.WhenInRange),
                    DensityFunction.CODEC.FieldOf("when_out_of_range")
                        .ForGetter<RangeChoiceBranches>(static branches => branches.WhenOutOfRange))
                .Apply(
                    instance,
                    static (whenInRange, whenOutOfRange) =>
                        new RangeChoiceBranches(whenInRange, whenOutOfRange)));

    private static MapCodec<DensityFunction> CreateIntervalSelectCodec() =>
        RecordCodecBuilder.MapCodec<IntervalSelectFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("input")
                        .ForGetter<IntervalSelectFunction>(static function => function.Input),
                    DensityFunctions.NOISE_VALUE_CODEC.ListOf()
                        .FieldOf("thresholds")
                        .ForGetter<IntervalSelectFunction>(static function => function.Thresholds),
                    DensityFunction.CODEC.ListOf(2)
                        .FieldOf("functions")
                        .ForGetter<IntervalSelectFunction>(static function => function.Functions))
                .Apply(
                    instance,
                    static (input, thresholds, functions) =>
                        new IntervalSelectFunction(input, thresholds, functions)))
            .Validate(ValidateIntervalSelect)
            .XMap<DensityFunction>(
                static function => function,
                function => (IntervalSelectFunction)function);

    private static DataResult<IntervalSelectFunction> ValidateIntervalSelect(
        IntervalSelectFunction function)
    {
        if (function.Thresholds.Count != function.Functions.Count - 1)
        {
            return DataResult<IntervalSelectFunction>.Error(
                () => $"Expected {function.Functions.Count - 1} thresholds for {function.Functions.Count} functions, but got {function.Thresholds.Count}");
        }

        for (int index = 1; index < function.Thresholds.Count; index++)
        {
            if (JavaFloatCompare(function.Thresholds[index - 1], function.Thresholds[index]) > 0)
            {
                return DataResult<IntervalSelectFunction>.Error(
                    "Threshold values must be ordered from smallest to largest");
            }
        }

        return DataResult<IntervalSelectFunction>.Success(function);
    }

    private static int JavaFloatCompare(float left, float right)
    {
        if (left < right)
            return -1;
        if (left > right)
            return 1;

        int leftBits = BitConverter.SingleToInt32Bits(left);
        int rightBits = BitConverter.SingleToInt32Bits(right);
        return leftBits == rightBits
            ? 0
            : leftBits < rightBits ? -1 : 1;
    }

    private sealed record RangeChoiceBounds(float MinInclusive, float MaxExclusive);
    private sealed record RangeChoiceBranches(
        DensityFunction WhenInRange,
        DensityFunction WhenOutOfRange);

    private static MapCodec<DensityFunction> CreateSliceCodec() =>
        RecordCodecBuilder.MapCodec<SliceFunction>(instance =>
            instance.Group(
                    AxisCodec.FieldOf("axis")
                        .ForGetter<SliceFunction>(static function => function.Axis),
                    Codecs.INT.FieldOf("coordinate")
                        .ForGetter<SliceFunction>(static function => function.Coordinate),
                    DensityFunction.CODEC.FieldOf("input")
                        .ForGetter<SliceFunction>(static function => function.Input))
                .Apply(
                    instance,
                    static (axis, coordinate, input) =>
                        new SliceFunction(axis, coordinate, input)))
            .XMap<DensityFunction>(
                static function => function,
                function => (SliceFunction)function);

    private static Codec<Direction.Axis> AxisCodec { get; } =
        Codecs.STRING.FlatXMap(
            name =>
            {
                Direction.Axis? axis = Direction.Axis.ByName(name);
                return axis is not null
                    ? DataResult<Direction.Axis>.Success(axis)
                    : DataResult<Direction.Axis>.Error(
                        () => $"Unknown axis: {name}");
            },
            axis => DataResult<string>.Success(axis.GetSerializedName()));

    private static MapCodec<DensityFunction> CreateGradientCodec() =>
        RecordCodecBuilder.MapCodec<GradientFunction>(instance =>
            instance.Group(
                    AxisCodec.FieldOf("axis")
                        .ForGetter<GradientFunction>(static function => function.Axis),
                    TilingModeCodec.OptionalFieldOf(
                            "tiling",
                            TilingMode.CLAMP_TO_EDGE)
                        .ForGetter<GradientFunction>(static function => function.Tiling),
                    CreateGradientCoordinatesCodec()
                        .ForGetter<GradientFunction>(static function =>
                            new GradientCoordinates(function.FromCoordinate, function.ToCoordinate)),
                    CreateGradientValuesCodec()
                        .ForGetter<GradientFunction>(static function =>
                            new GradientValues(function.FromValue, function.ToValue)))
                .Apply(
                    instance,
                    static (axis, tiling, coordinates, values) =>
                        new GradientFunction(
                            axis,
                            tiling,
                            coordinates.FromCoordinate,
                            coordinates.ToCoordinate,
                            values.FromValue,
                            values.ToValue)))
            .Validate(static function =>
                function.FromCoordinate == function.ToCoordinate
                    ? DataResult<GradientFunction>.Error(
                        "from_coordinate cannot be equal to to_coordinate")
                    : DataResult<GradientFunction>.Success(function))
            .XMap<DensityFunction>(
                static function => function,
                function => (GradientFunction)function);

    private static MapCodec<GradientCoordinates> CreateGradientCoordinatesCodec() =>
        RecordCodecBuilder.MapCodec<GradientCoordinates>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("from_coordinate")
                        .ForGetter<GradientCoordinates>(static coordinates => coordinates.FromCoordinate),
                    Codecs.INT.FieldOf("to_coordinate")
                        .ForGetter<GradientCoordinates>(static coordinates => coordinates.ToCoordinate))
                .Apply(
                    instance,
                    static (fromCoordinate, toCoordinate) =>
                        new GradientCoordinates(fromCoordinate, toCoordinate)));

    private static MapCodec<GradientValues> CreateGradientValuesCodec() =>
        RecordCodecBuilder.MapCodec<GradientValues>(instance =>
            instance.Group(
                    DensityFunctions.NOISE_VALUE_CODEC.FieldOf("from_value")
                        .ForGetter<GradientValues>(static values => values.FromValue),
                    DensityFunctions.NOISE_VALUE_CODEC.FieldOf("to_value")
                        .ForGetter<GradientValues>(static values => values.ToValue))
                .Apply(
                    instance,
                    static (fromValue, toValue) =>
                        new GradientValues(fromValue, toValue)));

    private static Codec<TilingMode> TilingModeCodec { get; } =
        Codecs.STRING.FlatXMap(
            name =>
            {
                TilingMode? tiling = name switch
                {
                    "clamp_to_edge" => TilingMode.CLAMP_TO_EDGE,
                    "repeat" => TilingMode.REPEAT,
                    "mirrored_repeat" => TilingMode.MIRRORED_REPEAT,
                    _ => null
                };

                return tiling.HasValue
                    ? DataResult<TilingMode>.Success(tiling.Value)
                    : DataResult<TilingMode>.Error(
                        () => $"Unknown tiling mode: {name}");
            },
            tiling => DataResult<string>.Success(tiling.GetSerializedName()));

    private sealed record GradientCoordinates(int FromCoordinate, int ToCoordinate);
    private sealed record GradientValues(float FromValue, float ToValue);

    private static MapCodec<DensityFunction> CreateFindTopSurfaceCodec() =>
        RecordCodecBuilder.MapCodec<FindTopSurfaceFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("density")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.Density),
                    DensityFunction.CODEC.FieldOf("upper_bound")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.UpperBound),
                    Codecs.IntRange(
                            Asteria.World.LevelGen.NoiseSettings.DimensionMinY * 2,
                            Asteria.World.LevelGen.NoiseSettings.DimensionMaxY * 2)
                        .FieldOf("lower_bound")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.LowerBound),
                    Codecs.IntRange(1, int.MaxValue)
                        .FieldOf("cell_height")
                        .ForGetter<FindTopSurfaceFunction>(static function => function.CellHeight))
                .Apply(
                    instance,
                    static (density, upperBound, lowerBound, cellHeight) =>
                        new FindTopSurfaceFunction(density, upperBound, lowerBound, cellHeight)))
            .XMap<DensityFunction>(
                static function => function,
                function => (FindTopSurfaceFunction)function);

    private static MapCodec<DensityFunction> CreateRoundCodec(RoundFunction.Type operation) =>
        RecordCodecBuilder.MapCodec<RoundFunction>(instance =>
            instance.Group(
                    DensityFunction.CODEC.FieldOf("input")
                        .ForGetter<RoundFunction>(static function => function.Input),
                    DensityFunction.CODEC.OptionalFieldOf(
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
