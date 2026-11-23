using Asteria.Util;

internal static class IntervalChecks
{
    public static void Run()
    {
        Interval zeroToOne = Interval.Of(0.0f, 1.0f);
        Interval negativeOneToOne = Interval.OfSymmetric(1.0f);
        Require(ReferenceEquals(Interval.Of(0.0f, 1.0f), zeroToOne) &&
            ReferenceEquals(Interval.Of(-1.0f, 1.0f), negativeOneToOne) &&
            ReferenceEquals(Interval.Of(float.NegativeInfinity, float.PositiveInfinity), Interval.INFINITE),
            "Interval.of must reuse the canonical zero-to-one, negative-one-to-one and infinite singletons.");
        RequireThrows<ArgumentException>(() => Interval.Of(2.0f, 1.0f),
            "Interval.of must reject inverted bounds.");
        RequireThrows<ArgumentException>(() => Interval.Of(float.NaN, 1.0f),
            "Interval.of must reject NaN bounds and require the NaI singleton.");

        Interval left = Interval.Of(-2.0f, 3.0f);
        Interval right = Interval.Of(4.0f, 5.0f);
        Require(Interval.Add(left, right).Equals(Interval.Of(2.0f, 8.0f)), "Interval.add must combine corresponding bounds.");
        Require(Interval.Sub(left, right).Equals(Interval.Of(-7.0f, -1.0f)), "Interval.sub must reverse the right bounds.");
        Require(Interval.Mul(left, right).Equals(Interval.Of(-10.0f, 15.0f)), "Interval.mul must select extrema across four products.");
        Require(Interval.Reciprocal(Interval.Of(-2.0f, 0.0f)).Equals(Interval.Of(float.NegativeInfinity, -0.5f)),
            "Interval.reciprocal must preserve the negative half-infinite range when its upper bound is zero.");
        Require(Interval.Reciprocal(Interval.Of(-1.0f, 1.0f)).Equals(Interval.INFINITE),
            "Interval.reciprocal across zero must be unbounded in both directions.");
        Require(Interval.Div(Interval.OfExact(1.0f), Interval.OfExact(0.0f)).IsNaI(),
            "Interval.div by the zero singleton must propagate NaI.");
        Require(Interval.Clamp(Interval.Of(-2.0f, 0.5f), -1.0f, 1.0f).Equals(Interval.Of(-1.0f, 0.5f)),
            "Interval.clamp must clip both bounds.");
        Require(Interval.Abs(Interval.Of(-3.0f, 2.0f)).Equals(Interval.Of(0.0f, 3.0f)), "Interval.abs must include zero when the input crosses it.");
        Require(Interval.Square(Interval.Of(-3.0f, 2.0f)).Equals(Interval.Of(0.0f, 9.0f)), "Interval.square must keep a zero lower bound for intervals spanning zero.");
        Require(Interval.Pow(Interval.OfExact(-2.0f), Interval.Of(2.0f, 3.0f)).Equals(Interval.Of(-8.0f, 4.0f)),
            "Interval.pow must account for negative bases and integer exponents inside an exponent range.");
        Require(Interval.Log(Interval.Of(-1.0f, 4.0f)).Equals(Interval.Of(float.NegativeInfinity, (float)Math.Log(4.0))),
            "Interval.log must clip a range crossing zero before applying the monotonic logarithm.");
        Require(Interval.MapMonotonic(Interval.Of(-2.0f, 3.0f), x => -x).Equals(Interval.Of(-3.0f, 2.0f)),
            "Interval.mapMonotonic must reorder mapped endpoint values.");
        RequireThrows<InvalidOperationException>(() => Interval.MapMonotonic(Interval.OfExact(0.0f), _ => float.NaN),
            "Interval.mapMonotonic must reject an operator that produces NaN.");
        Require(Interval.Lerp(Interval.Of(0.25f, 0.75f), 0.0f, 10.0f).Equals(Interval.Of(2.5f, 7.5f)),
            "Interval.lerp must propagate a bounded alpha interval.");
        Require(Interval.Sign(Interval.Of(-2.0f, 0.0f)).Equals(Interval.Of(-1.0f, 0.0f)) &&
            Interval.Sign(Interval.Of(0.0f, 2.0f)).Equals(Interval.Of(0.0f, 1.0f)) &&
            Interval.Sign(Interval.Of(-2.0f, 2.0f)).Equals(Interval.Of(-1.0f, 1.0f)),
            "Interval.sign must preserve the possible sign range and zero boundary.");

        Require(Interval.Encapsulating(Interval.NaI, Interval.Of(2.0f, 4.0f)).Equals(Interval.Of(2.0f, 4.0f)) &&
            Interval.Encapsulating(Interval.NaI, Interval.NaI).IsNaI(),
            "Interval.encapsulating must ignore NaI intervals and return NaI when no numeric interval remains.");
        Require(Interval.NaI.IsNaI() && Interval.NaI.ToString() == "[NaN]" &&
            Interval.Of(2.0f, 4.0f).ToString() == "[2; 4]",
            "NaI and ordinary intervals must preserve the reference identity check and textual form.");
        Require(Interval.Of(-1.0f, 2.0f).Contains(0.0f) &&
            Interval.Of(-1.0f, 2.0f).Intersects(Interval.Of(2.0f, 3.0f)) &&
            !Interval.Of(-1.0f, 1.0f).Intersects(Interval.Of(2.0f, 3.0f)),
            "Interval.contains and intersects must use inclusive closed bounds.");
        Require(Interval.OfExact(0.0f).GetHashCode() == Interval.OfExact(0.0f).GetHashCode(),
            "Equal intervals must have equal hash codes.");
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
