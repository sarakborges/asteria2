namespace Asteria.Serialization;

/// <summary>A pair returned by a decoder, containing the decoded value and unconsumed input.</summary>
public readonly record struct Pair<TFirst, TSecond>(TFirst First, TSecond Second)
{
    public TFirst GetFirst() => First;
    public TSecond GetSecond() => Second;

    public Pair<TSecond, TFirst> Swap() =>
        new(Second, First);

    public Pair<TMapped, TSecond> MapFirst<TMapped>(Func<TFirst, TMapped> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new Pair<TMapped, TSecond>(function(First), Second);
    }

    public Pair<TFirst, TMapped> MapSecond<TMapped>(Func<TSecond, TMapped> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new Pair<TFirst, TMapped>(First, function(Second));
    }
}
