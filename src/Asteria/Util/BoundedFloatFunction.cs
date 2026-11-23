using System;

namespace Asteria.Util;

/// <summary>
/// A context-dependent float function with a conservative output interval.
/// </summary>
public interface IBoundedFloatFunction<in TContext>
{
    float Apply(TContext context);

    Interval Range();
}

public static class BoundedFloatFunctions
{
    private sealed class Function<TContext> : IBoundedFloatFunction<TContext>
    {
        private readonly Func<TContext, float> _apply;
        private readonly Interval _range;

        public Function(Func<TContext, float> apply, Interval range)
        {
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            _range = range ?? throw new ArgumentNullException(nameof(range));
        }

        public float Apply(TContext context) => _apply(context);

        public Interval Range() => _range;
    }

    private static readonly IBoundedFloatFunction<float> IdentityFunction =
        new Function<float>(static value => value, Interval.INFINITE);

    public static IBoundedFloatFunction<float> Identity => IdentityFunction;

    public static IBoundedFloatFunction<TContext> Constant<TContext>(float value)
    {
        Interval range = Interval.OfExact(value);
        return new Function<TContext>(_ => value, range);
    }

    public static IBoundedFloatFunction<TNextContext> Comap<TContext, TNextContext>(
        this IBoundedFloatFunction<TContext> function,
        Func<TNextContext, TContext> mapper)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(mapper);

        return new Function<TNextContext>(
            context => function.Apply(mapper(context)),
            function.Range());
    }
}
