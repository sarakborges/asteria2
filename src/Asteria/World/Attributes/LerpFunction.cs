namespace Asteria.World.Attributes;

/// <summary>Typed interpolation for attribute values and modifier arguments.</summary>
public sealed class LerpFunction<T>
{
    private readonly Func<float, T, T, T> _lerp;

    public LerpFunction(Func<float, T, T, T> lerp) =>
        _lerp = lerp ?? throw new ArgumentNullException(nameof(lerp));

    public T Apply(float alpha, T from, T to) => _lerp(alpha, from, to);

    public static LerpFunction<float> OfFloat() =>
        new(static (alpha, from, to) => from + (to - from) * alpha);

    public static LerpFunction<T> OfConstant() => OfStep(1.0F);

    public static LerpFunction<T> OfStep(float threshold) =>
        new((alpha, from, to) => alpha >= threshold ? to : from);
}
