using Asteria.Core;

namespace Asteria.Core.Mathematics;

/// <summary>
/// Mutable integer vector used by the signed axis-permutation operations.
/// Mutations preserve the instance, matching the mutable vector contract of the source math layer.
/// </summary>
public sealed class MutableVec3i : Vec3i
{
    public MutableVec3i() : this(0, 0, 0)
    {
    }

    public MutableVec3i(int x, int y, int z) : base(x, y, z)
    {
    }

    public int Get(int component) =>
        component switch
        {
            0 => GetX(),
            1 => GetY(),
            2 => GetZ(),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Component must be 0, 1 or 2.")
        };

    public MutableVec3i Set(int x, int y, int z)
    {
        SetX(x);
        SetY(y);
        SetZ(z);
        return this;
    }

    public MutableVec3i SetComponent(int component, int value)
    {
        switch (component)
        {
            case 0:
                SetX(value);
                break;
            case 1:
                SetY(value);
                break;
            case 2:
                SetZ(value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, "Component must be 0, 1 or 2.");
        }

        return this;
    }
}
