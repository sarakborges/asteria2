namespace Asteria.Core.Mathematics;

/// <summary>
/// Mutable single-precision vector used by the ported permutation operations.
/// </summary>
public sealed class MutableVec3f
{
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Z { get; private set; }

    public MutableVec3f() : this(0.0f, 0.0f, 0.0f)
    {
    }

    public MutableVec3f(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float Get(int component) =>
        component switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Component must be 0, 1 or 2.")
        };

    public MutableVec3f Set(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
        return this;
    }
}
