namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Linear float storage used while evaluating Minecraft 26.3 density volumes.
/// </summary>
public class DensityBuffer
{
    protected readonly float[] Values;
    protected int CurrentSize;

    protected DensityBuffer(int size)
    {
        Values = new float[size];
        CurrentSize = size;
    }

    public static DensityBuffer CreateUnpooled(int size) =>
        new(size);

    public void Set(int index, float value) =>
        Values[index] = value;

    public void SetRange(int index, int size, float value) =>
        Array.Fill(Values, value, index, size);

    public void AddTo(int index, float value) =>
        Values[index] = Values[index] + value;

    public float Get(int index) =>
        Values[index];

    public void Fill(float value) =>
        Array.Fill(Values, value, 0, CurrentSize);

    public void CopyFrom(DensityBuffer other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Size() != other.Size())
        {
            throw new ArgumentException(
                $"Cannot copy from buffer with size={other.Size()}, expected{Size()}");
        }

        Array.Copy(other.Values, 0, Values, 0, Size());
    }

    public int Capacity() => Values.Length;

    public int Size() => CurrentSize;
}
