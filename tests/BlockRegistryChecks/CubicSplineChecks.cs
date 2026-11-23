using Asteria.Util;

internal static class CubicSplineChecks
{
    private sealed record FloatCoordinate(float Value, Interval ValueRange) :
        IBoundedFloatFunction<float>
    {
        public float Apply(float context) => Value + context;

        public Interval Range() => ValueRange;
    }

    private sealed class NaICoordinate : IBoundedFloatFunction<float>
    {
        public float Apply(float context) => context;

        public Interval Range() => Interval.NaI;
    }

    public static void Run()
    {
        CheckBoundedFloatFunction();
        CheckConstantSpline();
        CheckLinearSplineAndBoundaryExtensions();
        CheckCubicInterpolation();
        CheckNestedSplineAndCoordinateTraversal();
        CheckMappedCoordinates();
        CheckRangePropagation();
        CheckBuilderAndConstructorValidation();
    }

    private static void CheckBoundedFloatFunction()
    {
        Require(
            BoundedFloatFunctions.Identity.Apply(-3.25f) == -3.25f &&
            BoundedFloatFunctions.Identity.Range().Equals(Interval.INFINITE),
            "BoundedFloatFunction.IDENTITY must return the input and expose the infinite range.");

        IBoundedFloatFunction<string> constant = BoundedFloatFunctions.Constant<string>(7.5f);
        Require(
            constant.Apply("ignored") == 7.5f &&
            constant.Range().Equals(Interval.OfExact(7.5f)),
            "BoundedFloatFunction.constant must retain one exact range independent of context.");

        IBoundedFloatFunction<string> mapped = constant.Comap<string, int>(value => value.ToString().Length);
        Require(
            mapped.Apply(1234) == 7.5f &&
            mapped.Range().Equals(constant.Range()),
            "BoundedFloatFunction.comap must map the context and preserve the original range.");
    }

    private static void CheckConstantSpline()
    {
        CubicSpline<float> constant = CubicSpline<float>.CreateConstant(-2.75f);
        Require(
            CubicSpline<float>.Sample(constant, 123.0f) == -2.75f &&
            constant.Range().Equals(Interval.OfExact(-2.75f)),
            "CubicSpline.constant must sample and bound the exact stored value.");

        IBoundedFloatFunction<float> sampler = CubicSpline<float>.AsSampler(constant);
        Require(
            sampler.Apply(9.0f) == -2.75f &&
            sampler.Range().Equals(constant.Range()),
            "CubicSpline.asSampler must retain constant evaluation and range.");

        int visited = 0;
        constant.ForEachCoordinate(_ => visited++);
        Require(visited == 0, "A constant spline must not visit any coordinates.");
    }

    private static void CheckLinearSplineAndBoundaryExtensions()
    {
        var builder = CubicSpline<float>.CreateBuilder(BoundedFloatFunctions.Identity);
        CubicSpline<float> spline = builder
            .AddPoint(0.0f, 0.0f, 2.0f)
            .AddPoint(10.0f, 10.0f, -1.0f)
            .Build();

        Require(
            CubicSpline<float>.Sample(spline, -5.0f) == -10.0f &&
            CubicSpline<float>.Sample(spline, 15.0f) == 5.0f,
            "A multipoint spline must linearly extend beyond both endpoints using the endpoint derivatives.");

        Require(
            CubicSpline<float>.Sample(spline, 0.0f) == 0.0f &&
            CubicSpline<float>.Sample(spline, 10.0f) == 10.0f,
            "Spline endpoint coordinates must select the matching endpoint and preserve endpoint values.");
    }

    private static void CheckCubicInterpolation()
    {
        CubicSpline<float> spline = CubicSpline<float>.CreateBuilder(BoundedFloatFunctions.Identity)
            .AddPoint(0.0f, 0.0f)
            .AddPoint(10.0f, 10.0f)
            .Build();

        float sample = CubicSpline<float>.Sample(spline, 2.5f);
        Require(
            Math.Abs(sample - 4.0625f) < 0.00001f,
            "Multipoint interpolation must use the Minecraft cubic Hermite expression, not linear interpolation.");
    }

    private static void CheckNestedSplineAndCoordinateTraversal()
    {
        var nested = CubicSpline<float>.CreateBuilder(
            new FloatCoordinate(1.0f, Interval.Of(-1.0f, 1.0f)));
        nested.AddPoint(0.0f, 2.0f);
        nested.AddPoint(4.0f, 6.0f);
        CubicSpline<float> nestedSpline = nested.Build();

        var outer = CubicSpline<float>.CreateBuilder(BoundedFloatFunctions.Identity);
        outer.AddPoint(0.0f, nestedSpline);
        outer.AddPoint(10.0f, CubicSpline<float>.CreateConstant(20.0f));
        CubicSpline<float> spline = outer.Build();

        float sample = CubicSpline<float>.Sample(spline, 1.0f);
        Require(
            Math.Abs(sample - 2.0f) < 0.00001f,
            "Nested cubic splines must evaluate their child splines against the same context.");
        
        var coordinates = new List<IBoundedFloatFunction<float>>();
        spline.ForEachCoordinate(coordinates.Add);
        Require(
            coordinates.Count == 2 &&
            ReferenceEquals(coordinates[0], ((CubicSpline<float>.Multipoint)spline).Coordinate) &&
            ReferenceEquals(coordinates[1], ((CubicSpline<float>.Multipoint)nestedSpline).Coordinate),
            "forEachCoordinate must visit each coordinate in pre-order and recurse into nested spline values.");
    }

    private static void CheckMappedCoordinates()
    {
        CubicSpline<float> spline = CubicSpline<float>.CreateBuilder(
                new FloatCoordinate(1.0f, Interval.Of(0.0f, 2.0f)))
            .AddPoint(0.0f, 0.0f)
            .AddPoint(10.0f, 10.0f)
            .Build();

        CubicSpline<int> mapped = spline.MapCoordinates<float, int>(
            coordinate => coordinate.Comap<int, float>(value => value * 2.0f));

        Require(
            Math.Abs(CubicSpline<int>.Sample(mapped, 0) - CubicSpline<float>.Sample(spline, 1.0f)) < 0.00001f &&
            mapped.Range().Equals(spline.Range()),
            "mapCoordinates must transform every coordinate recursively while retaining spline values and intervals.");
    }

    private static void CheckRangePropagation()
    {
        var coordinate = new FloatCoordinate(0.0f, Interval.Of(-2.0f, 12.0f));
        CubicSpline<float> spline = CubicSpline<float>.CreateBuilder(coordinate)
            .AddPoint(0.0f, 1.0f, 2.0f)
            .AddPoint(10.0f, 5.0f, -1.0f)
            .Build();

        Interval range = spline.Range();
        Require(
            range.Min() <= -3.0f &&
            range.Max() >= 7.0f &&
            spline.Range().Equals(range),
            "CubicSpline.range must include endpoint extrapolation and all child ranges.");

        CubicSpline<float> naiSpline = CubicSpline<float>.CreateBuilder(new NaICoordinate())
            .AddPoint(0.0f, 1.0f)
            .Build();
        Require(naiSpline.Range().IsNaI(),
            "A multipoint spline must propagate a NaI coordinate interval.");
    }

    private static void CheckBuilderAndConstructorValidation()
    {
        var builder = CubicSpline<float>.CreateBuilder(BoundedFloatFunctions.Identity);
        RequireThrows<InvalidOperationException>(
            () => builder.Build(),
            "CubicSpline.Builder.build must reject an empty point list.");

        builder.AddPoint(1.0f, 2.0f);
        RequireThrows<ArgumentException>(
            () => builder.AddPoint(1.0f, 3.0f),
            "CubicSpline.Builder must require strictly ascending point locations.");

        RequireThrows<ArgumentException>(
            () => new CubicSpline<float>.Multipoint(
                BoundedFloatFunctions.Identity,
                new[] { 0.0f, 1.0f },
                new CubicSpline<float>[] { CubicSpline<float>.CreateConstant(0.0f) },
                new[] { 0.0f, 0.0f }),
            "CubicSpline.Multipoint must reject mismatched locations/values/derivatives lengths.");

        RequireThrows<ArgumentException>(
            () => new CubicSpline<float>.Multipoint(
                BoundedFloatFunctions.Identity,
                Array.Empty<float>(),
                Array.Empty<CubicSpline<float>>(),
                Array.Empty<float>()),
            "CubicSpline.Multipoint must reject empty location arrays.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
