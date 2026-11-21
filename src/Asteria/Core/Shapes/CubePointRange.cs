namespace Asteria.Core.Shapes;

public sealed class CubePointRange : DoubleList
{
    public int Parts { get; }

    public CubePointRange(int parts)
    {
        if (parts <= 0)
            throw new ArgumentException("Need at least 1 part");

        Parts = parts;
    }

    public override double GetDouble(int index)
    {
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return (double)index / Parts;
    }

    public override int Count => Parts + 1;
}
