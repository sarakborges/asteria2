using System.Globalization;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.Content.Biomes;

/// <summary>
/// Quantized six-axis biome climate coordinates and the seven-dimensional parameter
/// search used by Minecraft Java 26.3 biome selection.
/// </summary>
public static class Climate
{
    private const float QuantizationFactor = 10000.0F;
    public const int ParameterCount = 7;

    public static TargetPoint Target(
        float temperature,
        float humidity,
        float continentalness,
        float erosion,
        float depth,
        float weirdness) =>
        new(
            QuantizeCoord(temperature),
            QuantizeCoord(humidity),
            QuantizeCoord(continentalness),
            QuantizeCoord(erosion),
            QuantizeCoord(depth),
            QuantizeCoord(weirdness));

    public static ParameterPoint Parameters(
        float temperature,
        float humidity,
        float continentalness,
        float erosion,
        float depth,
        float weirdness,
        float offset) =>
        new(
            Parameter.Point(temperature),
            Parameter.Point(humidity),
            Parameter.Point(continentalness),
            Parameter.Point(erosion),
            Parameter.Point(depth),
            Parameter.Point(weirdness),
            QuantizeCoord(offset));

    public static ParameterPoint Parameters(
        Parameter temperature,
        Parameter humidity,
        Parameter continentalness,
        Parameter erosion,
        Parameter depth,
        Parameter weirdness,
        float offset) =>
        new(
            temperature,
            humidity,
            continentalness,
            erosion,
            depth,
            weirdness,
            QuantizeCoord(offset));

    public static long QuantizeCoord(float coordinate) =>
        unchecked((long)(coordinate * QuantizationFactor));

    public static float UnquantizeCoord(long coordinate) =>
        (float)coordinate / QuantizationFactor;

    public readonly record struct TargetPoint(
        long Temperature,
        long Humidity,
        long Continentalness,
        long Erosion,
        long Depth,
        long Weirdness)
    {
        internal long[] ToParameterArray() =>
        [
            Temperature,
            Humidity,
            Continentalness,
            Erosion,
            Depth,
            Weirdness,
            0L
        ];
    }

    public sealed record ParameterPoint(
        Parameter Temperature,
        Parameter Humidity,
        Parameter Continentalness,
        Parameter Erosion,
        Parameter Depth,
        Parameter Weirdness,
        long Offset)
    {
        public static Codec<ParameterPoint> CODEC { get; } =
            new ParameterPointMapCodec().Codec();

        public long Fitness(TargetPoint target)
        {
            long result = 0L;
            result = unchecked(result + Square(Temperature.Distance(target.Temperature)));
            result = unchecked(result + Square(Humidity.Distance(target.Humidity)));
            result = unchecked(result + Square(Continentalness.Distance(target.Continentalness)));
            result = unchecked(result + Square(Erosion.Distance(target.Erosion)));
            result = unchecked(result + Square(Depth.Distance(target.Depth)));
            result = unchecked(result + Square(Weirdness.Distance(target.Weirdness)));
            return unchecked(result + Square(Offset));
        }

        internal IReadOnlyList<Parameter> ParameterSpace() =>
        [
            Temperature,
            Humidity,
            Continentalness,
            Erosion,
            Depth,
            Weirdness,
            new Parameter(Offset, Offset)
        ];
    }

    public readonly record struct Parameter(long Min, long Max)
    {
        public static Codec<Parameter> CODEC { get; } =
            RecordCodecBuilder.Create<Parameter>(instance =>
                instance.Group(
                    Codecs.FloatRange(-2.0F, 2.0F)
                        .FieldOf("min")
                        .ForGetter<Parameter>(static parameter => UnquantizeCoord(parameter.Min)),
                    Codecs.FloatRange(-2.0F, 2.0F)
                        .FieldOf("max")
                        .ForGetter<Parameter>(static parameter => UnquantizeCoord(parameter.Max)))
                .Apply(
                    instance,
                    static (min, max) =>
                        new Parameter(QuantizeCoord(min), QuantizeCoord(max))))
            .FlatXMap(
                static parameter => parameter.Min > parameter.Max
                    ? DataResult<Parameter>.Error(() =>
                        $"Cannon construct interval, min > max ({UnquantizeCoord(parameter.Min)} > {UnquantizeCoord(parameter.Max)})")
                    : DataResult<Parameter>.Success(parameter),
                static parameter => DataResult<Parameter>.Success(parameter));

        public static Parameter Point(float value) =>
            Span(value, value);

        public static Parameter Span(float min, float max)
        {
            if (min > max)
                throw new ArgumentException($"min > max: {min} {max}");
            return new Parameter(QuantizeCoord(min), QuantizeCoord(max));
        }

        public static Parameter Span(Parameter min, Parameter max)
        {
            if (min.Min > max.Max)
                throw new ArgumentException($"min > max: {min} {max}");
            return new Parameter(min.Min, max.Max);
        }

        public long Distance(long target)
        {
            long above = unchecked(target - Max);
            long below = unchecked(Min - target);
            if (above > 0L)
                return above;
            return Math.Max(below, 0L);
        }

        public Parameter Span(Parameter? other) =>
            other is null
                ? this
                : new Parameter(
                    Math.Min(Min, other.Value.Min),
                    Math.Max(Max, other.Value.Max));

        public override string ToString() =>
            Min == Max
                ? Min.ToString(CultureInfo.InvariantCulture)
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{Min}-{Max}]");
    }

    public readonly record struct Sampler(
        DensitySampler.Bound Temperature,
        DensitySampler.Bound Humidity,
        DensitySampler.Bound Continentalness,
        DensitySampler.Bound Erosion,
        DensitySampler.Bound Depth,
        DensitySampler.Bound Weirdness)
    {
        public TargetPoint Sample(int quartX, int quartY, int quartZ)
        {
            int blockX = QuartPos.ToBlock(quartX);
            int blockY = QuartPos.ToBlock(quartY);
            int blockZ = QuartPos.ToBlock(quartZ);
            return Target(
                Temperature.SampleValue(blockX, blockY, blockZ),
                Humidity.SampleValue(blockX, blockY, blockZ),
                Continentalness.SampleValue(blockX, blockY, blockZ),
                Erosion.SampleValue(blockX, blockY, blockZ),
                Depth.SampleValue(blockX, blockY, blockZ),
                Weirdness.SampleValue(blockX, blockY, blockZ));
        }
    }

    public sealed class ParameterList<T>
    {
        private readonly IReadOnlyList<Pair<ParameterPoint, T>> _values;
        private readonly RTree<T> _index;

        public static Codec<ParameterList<T>> CreateCodec(MapCodec<T> valueCodec)
        {
            ArgumentNullException.ThrowIfNull(valueCodec);

            Codec<ParameterEntry<T>> entryCodec =
                RecordCodecBuilder.Create<ParameterEntry<T>>(instance =>
                    instance.Group(
                        ParameterPoint.CODEC.FieldOf("parameters")
                            .ForGetter<ParameterEntry<T>>(static entry => entry.Parameters),
                        valueCodec.ForGetter<ParameterEntry<T>>(static entry => entry.Value))
                    .Apply(
                        instance,
                        static (parameters, value) => new ParameterEntry<T>(parameters, value)));

            return entryCodec.ListOf(minSize: 1).XMap(
                static entries => new ParameterList<T>(
                    entries.Select(entry => new Pair<ParameterPoint, T>(
                        entry.Parameters,
                        entry.Value)).ToList()),
                static list => list._values.Select(pair =>
                    new ParameterEntry<T>(pair.First, pair.Second)).ToList());
        }

        public ParameterList(IReadOnlyList<Pair<ParameterPoint, T>> values)
            : this(values, 19)
        {
        }

        private ParameterList(
            IReadOnlyList<Pair<ParameterPoint, T>> values,
            int childrenPerNode)
        {
            ArgumentNullException.ThrowIfNull(values);
            _values = values;
            _index = RTree<T>.Create(values, childrenPerNode);
        }

        public ParameterList<T> RebuildWithChildrenPerNode(int childrenPerNode) =>
            new(_values, childrenPerNode);

        public IReadOnlyList<Pair<ParameterPoint, T>> Values() => _values;

        public T FindValue(TargetPoint target) =>
            _index.Search(target);

        public T FindValueBruteForce(TargetPoint target)
        {
            using var iterator = _values.GetEnumerator();
            if (!iterator.MoveNext())
                throw new InvalidOperationException("Need at least one value to search.");

            Pair<ParameterPoint, T> first = iterator.Current;
            long bestFitness = first.First.Fitness(target);
            T best = first.Second;

            while (iterator.MoveNext())
            {
                Pair<ParameterPoint, T> candidate = iterator.Current;
                long fitness = candidate.First.Fitness(target);
                if (fitness >= bestFitness)
                    continue;

                bestFitness = fitness;
                best = candidate.Second;
            }

            return best;
        }

        private readonly record struct ParameterEntry<T>(
            ParameterPoint Parameters,
            T Value);
    }

    private static long Square(long value) =>
        unchecked(value * value);

    private readonly record struct ClimateAxes(
        Parameter Temperature,
        Parameter Humidity,
        Parameter Continentalness,
        Parameter Erosion,
        Parameter Depth,
        Parameter Weirdness);

    private sealed class ParameterPointMapCodec : MapCodec<ParameterPoint>
    {
        private static readonly Codec<float> OffsetCodec = Codecs.FloatRange(0.0F, 1.0F);

        private static readonly string[] FieldNames =
        [
            "temperature",
            "humidity",
            "continentalness",
            "erosion",
            "depth",
            "weirdness",
            "offset"
        ];

        public override DataResult<ParameterPoint> Decode<TData>(
            DynamicOps<TData> ops,
            MapLike<TData> input)
        {
            DataResult<Parameter> GetParameter(string name) =>
                input.Get(name).FlatMap(value => Parameter.CODEC.Parse(ops, value));

            DataResult<Parameter> temperature = GetParameter("temperature");
            DataResult<Parameter> humidity = GetParameter("humidity");
            DataResult<Parameter> continentalness = GetParameter("continentalness");
            DataResult<Parameter> erosion = GetParameter("erosion");
            DataResult<Parameter> depth = GetParameter("depth");
            DataResult<Parameter> weirdness = GetParameter("weirdness");
            DataResult<float> offset = input.Get("offset")
                .FlatMap(value => OffsetCodec.Parse(ops, value));

            return temperature.Apply2(
                    static (first, second) => new ClimateAxes(
                        first, second, default, default, default, default),
                    humidity)
                .Apply2(
                    static (values, next) => values with { Continentalness = next },
                    continentalness)
                .Apply2(
                    static (values, next) => values with { Erosion = next },
                    erosion)
                .Apply2(
                    static (values, next) => values with { Depth = next },
                    depth)
                .Apply2(
                    static (values, next) => values with { Weirdness = next },
                    weirdness)
                .Apply2(
                    static (values, offsetValue) => new ParameterPoint(
                        values.Temperature,
                        values.Humidity,
                        values.Continentalness,
                        values.Erosion,
                        values.Depth,
                        values.Weirdness,
                        QuantizeCoord(offsetValue)),
                    offset);
        }

        public override DataResult<TData> Encode<TData>(
            ParameterPoint input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            DataResult<TData> temperature = Parameter.CODEC.EncodeStart(ops, input.Temperature);
            DataResult<TData> humidity = Parameter.CODEC.EncodeStart(ops, input.Humidity);
            DataResult<TData> continentalness = Parameter.CODEC.EncodeStart(ops, input.Continentalness);
            DataResult<TData> erosion = Parameter.CODEC.EncodeStart(ops, input.Erosion);
            DataResult<TData> depth = Parameter.CODEC.EncodeStart(ops, input.Depth);
            DataResult<TData> weirdness = Parameter.CODEC.EncodeStart(ops, input.Weirdness);
            DataResult<TData> offset = OffsetCodec.EncodeStart(ops, UnquantizeCoord(input.Offset));

            return CombineMaps(ops, temperature, humidity, continentalness, erosion, depth, weirdness, offset)
                .FlatMap(entries => ops.MergeToMap(prefix, entries));
        }

        public override IReadOnlyList<TData> Keys<TData>(DynamicOps<TData> ops) =>
            FieldNames.Select(name => ops.CreateString(name)).ToArray();

        private static DataResult<IReadOnlyList<KeyValuePair<TData, TData>>> CombineMaps<TData>(
            DynamicOps<TData> ops,
            params DataResult<TData>[] maps)
        {
            DataResult<IReadOnlyList<KeyValuePair<TData, TData>>> combined =
                DataResult<IReadOnlyList<KeyValuePair<TData, TData>>>.Success(
                    Array.Empty<KeyValuePair<TData, TData>>());

            foreach (DataResult<TData> map in maps)
            {
                combined = combined.FlatMap(current =>
                    map.FlatMap(value =>
                        ops.GetMapValues(value).Map(entries =>
                        {
                            var result = new List<KeyValuePair<TData, TData>>(
                                current.Count + entries.Count);
                            result.AddRange(current);
                            result.AddRange(entries);
                            return (IReadOnlyList<KeyValuePair<TData, TData>>)result;
                        })));
            }

            return combined;
        }

        public override string ToString() => "Climate.ParameterPoint";
    }

    private sealed class RTree<T>
    {
        private const int DefaultChildrenPerNode = 19;
        private readonly Node _root;
        private readonly ThreadLocal<Leaf?> _lastResult = new();

        private RTree(Node root) => _root = root;

        public static RTree<T> Create(
            IReadOnlyList<Pair<ParameterPoint, T>> values,
            int childrenPerNode = DefaultChildrenPerNode)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Count == 0)
                throw new ArgumentException(
                    "Need at least one value to build the search tree.",
                    nameof(values));

            int dimensions = values[0].First.ParameterSpace().Count;
            if (dimensions != ParameterCount)
                throw new InvalidOperationException(
                    $"Expecting parameter space to be {ParameterCount}, got {dimensions}");

            List<Node> leaves = values
                .Select(pair => (Node)new Leaf(pair.First, pair.Second))
                .ToList();

            return new RTree<T>(Build(dimensions, leaves, childrenPerNode));
        }

        private static Node Build(
            int dimensions,
            List<Node> children,
            int childrenPerNode)
        {
            if (children.Count == 0)
                throw new InvalidOperationException("Need at least one child to build a node.");

            if (children.Count == 1)
                return children[0];

            if (children.Count <= childrenPerNode)
            {
                List<Node> sorted = children
                    .OrderBy(TotalMagnitude)
                    .ToList();
                return new SubTree(sorted);
            }

            long minCost = long.MaxValue;
            int minDimension = -1;
            List<SubTree>? minBuckets = null;

            for (int dimension = 0; dimension < dimensions; dimension++)
            {
                Sort(children, dimensions, dimension, absolute: false);
                List<SubTree> buckets = Bucketize(children, childrenPerNode);
                long totalCost = 0L;
                foreach (SubTree bucket in buckets)
                    totalCost = unchecked(totalCost + Cost(bucket.ParameterSpace));

                if (minCost <= totalCost)
                    continue;

                minCost = totalCost;
                minDimension = dimension;
                minBuckets = buckets;
            }

            if (minBuckets is null)
                throw new InvalidOperationException("Unable to construct climate parameter search tree.");

            List<SubTree> sortedBuckets = SortBuckets(
                minBuckets, dimensions, minDimension, absolute: true);
            List<Node> rebuilt = sortedBuckets
                .Select(bucket => (Node)Build(
                    dimensions,
                    bucket.Children.ToList(),
                    childrenPerNode))
                .ToList();

            return new SubTree(rebuilt);
        }

        private static long TotalMagnitude(Node node)
        {
            long result = 0L;
            foreach (Parameter parameter in node.ParameterSpace)
            {
                long center = unchecked(parameter.Min + parameter.Max) / 2L;
                result = unchecked(result + JavaAbs(center));
            }

            return result;
        }

        private static void Sort(
            List<Node> children,
            int dimensions,
            int dimension,
            bool absolute)
        {
            Comparer<Node> comparer = Comparer<Node>.Create((left, right) =>
                CompareParameterSpaces(
                    left.ParameterSpace,
                    right.ParameterSpace,
                    dimensions,
                    dimension,
                    absolute));
            List<Node> sorted = children
                .Select((node, index) => (Node: node, Index: index))
                .OrderBy(entry => entry.Node, comparer)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Node)
                .ToList();
            children.Clear();
            children.AddRange(sorted);
        }

        private static int CompareParameterSpaces(
            Parameter[] left,
            Parameter[] right,
            int dimensions,
            int dimension,
            bool absolute)
        {
            for (int offset = 0; offset < dimensions; offset++)
            {
                int current = (dimension + offset) % dimensions;
                long leftCenter = unchecked(left[current].Min + left[current].Max) / 2L;
                long rightCenter = unchecked(right[current].Min + right[current].Max) / 2L;
                if (absolute)
                {
                    leftCenter = JavaAbs(leftCenter);
                    rightCenter = JavaAbs(rightCenter);
                }

                int result = leftCenter.CompareTo(rightCenter);
                if (result != 0)
                    return result;
            }

            return 0;
        }

        private static List<SubTree> SortBuckets(
            List<SubTree> buckets,
            int dimensions,
            int dimension,
            bool absolute)
        {
            return buckets
                .Select((bucket, index) => (Bucket: bucket, Index: index))
                .OrderBy(entry => entry.Bucket, Comparer<SubTree>.Create((left, right) =>
                    CompareParameterSpaces(
                        left.ParameterSpace,
                        right.ParameterSpace,
                        dimensions,
                        dimension,
                        absolute)))
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Bucket)
                .ToList();
        }

        private static List<SubTree> Bucketize(
            List<Node> nodes,
            int childrenPerNode)
        {
            int expectedChildrenCount = (int)Math.Pow(
                childrenPerNode,
                Math.Floor(Math.Log(nodes.Count - 0.01) / Math.Log(childrenPerNode)));

            var buckets = new List<SubTree>();
            var current = new List<Node>();
            foreach (Node node in nodes)
            {
                current.Add(node);
                if (current.Count < expectedChildrenCount)
                    continue;

                buckets.Add(new SubTree(current));
                current = new List<Node>();
            }

            if (current.Count > 0)
                buckets.Add(new SubTree(current));

            return buckets;
        }

        private static long Cost(Parameter[] parameterSpace)
        {
            long result = 0L;
            foreach (Parameter parameter in parameterSpace)
            {
                long difference = unchecked(parameter.Max - parameter.Min);
                result = unchecked(result + JavaAbs(difference));
            }

            return result;
        }

        private static Parameter[] BuildParameterSpace(IReadOnlyList<Node> children)
        {
            if (children.Count == 0)
                throw new ArgumentException("SubTree needs at least one child.", nameof(children));

            var bounds = new Parameter?[ParameterCount];
            foreach (Node child in children)
            {
                for (int dimension = 0; dimension < ParameterCount; dimension++)
                {
                    Parameter? existing = bounds[dimension];
                    Parameter next = child.ParameterSpace[dimension];
                    bounds[dimension] = existing is null
                        ? next
                        : next.Span(existing);
                }
            }

            return bounds.Select(parameter => parameter!.Value).ToArray();
        }

        public T Search(TargetPoint target)
        {
            long[] targetArray = target.ToParameterArray();
            Leaf result = _root.Search(targetArray, _lastResult.Value, static (node, values) =>
                node.Distance(values));
            _lastResult.Value = result;
            return result.Value;
        }

        private static long JavaAbs(long value) =>
            value == long.MinValue ? long.MinValue : Math.Abs(value);

        private abstract class Node
        {
            protected Node(IReadOnlyList<Parameter> parameterSpace) =>
                ParameterSpace = parameterSpace.ToArray();

            public Parameter[] ParameterSpace { get; }

            public abstract Leaf Search(
                long[] target,
                Leaf? candidate,
                Func<Node, long[], long> distanceMetric);

            public long Distance(long[] target)
            {
                long distance = 0L;
                for (int index = 0; index < ParameterCount; index++)
                {
                    long delta = new Parameter(
                        ParameterSpace[index].Min,
                        ParameterSpace[index].Max).Distance(target[index]);
                    distance = unchecked(distance + unchecked(delta * delta));
                }

                return distance;
            }
        }

        private sealed class SubTree : Node
        {
            public SubTree(IReadOnlyList<Node> children)
                : base(BuildParameterSpace(children))
            {
                Children = children.ToArray();
            }

            public Node[] Children { get; }

            public override Leaf Search(
                long[] target,
                Leaf? candidate,
                Func<Node, long[], long> distanceMetric)
            {
                long minDistance = candidate is null
                    ? long.MaxValue
                    : distanceMetric(candidate, target);
                Leaf? closestLeaf = candidate;

                foreach (Node child in Children)
                {
                    long childDistance = distanceMetric(child, target);
                    if (minDistance <= childDistance)
                        continue;

                    Leaf leaf = child.Search(target, closestLeaf, distanceMetric);
                    long leafDistance = ReferenceEquals(child, leaf)
                        ? childDistance
                        : distanceMetric(leaf, target);
                    if (minDistance <= leafDistance)
                        continue;

                    minDistance = leafDistance;
                    closestLeaf = leaf;
                }

                return closestLeaf ??
                    throw new InvalidOperationException("Climate parameter tree contains no leaf.");
            }
        }

        private sealed class Leaf : Node
        {
            public Leaf(ParameterPoint parameterPoint, T value)
                : base(parameterPoint.ParameterSpace()) =>
                Value = value;

            public T Value { get; }

            public override Leaf Search(
                long[] target,
                Leaf? candidate,
                Func<Node, long[], long> distanceMetric) =>
                this;
        }
    }
}
