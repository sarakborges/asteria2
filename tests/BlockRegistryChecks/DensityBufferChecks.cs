using Asteria.World.LevelGen.DensityFunctions;

internal static class DensityBufferChecks
{
    public static void Run()
    {
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(8);
        Require(buffer.Capacity() == 8 && buffer.Size() == 8,
            "DensityBuffer.createUnpooled must allocate exactly the requested capacity and active size.");

        buffer.Set(2, 1.25f);
        Require(buffer.Get(2) == 1.25f && buffer.Get(1) == 0.0f,
            "DensityBuffer.set/get must read and write the addressed float entry.");

        buffer.SetRange(3, 3, -2.5f);
        Require(buffer.Get(2) == 1.25f && buffer.Get(3) == -2.5f &&
            buffer.Get(4) == -2.5f && buffer.Get(5) == -2.5f && buffer.Get(6) == 0.0f,
            "DensityBuffer.setRange must fill the half-open range without changing adjacent entries.");

        buffer.AddTo(2, 0.75f);
        buffer.AddTo(3, 1.0f);
        Require(buffer.Get(2) == 2.0f && buffer.Get(3) == -1.5f,
            "DensityBuffer.addTo must accumulate the value using single-precision addition.");

        buffer.Fill(4.0f);
        Require(Enumerable.Range(0, buffer.Size()).All(index => buffer.Get(index) == 4.0f),
            "DensityBuffer.fill must write the active range.");

        DensityBuffer other = DensityBuffer.CreateUnpooled(8);
        other.SetRange(0, 8, -3.0f);
        buffer.CopyFrom(other);
        Require(Enumerable.Range(0, buffer.Size()).All(index => buffer.Get(index) == -3.0f),
            "DensityBuffer.copyFrom must copy every active value.");

        DensityBuffer mismatch = DensityBuffer.CreateUnpooled(7);
        ArgumentException exception = Capture<ArgumentException>(
            () => buffer.CopyFrom(mismatch),
            "DensityBuffer.copyFrom must reject a buffer with a different size.");
        Require(exception.GetType() == typeof(ArgumentException) &&
            exception.Message == "Cannot copy from buffer with size=7, expected8",
            "DensityBuffer.copyFrom must preserve the reference exception type and message.");

        Require(buffer.Capacity() == 8 && buffer.Size() == 8,
            "DensityBuffer capacity and size remain fixed after writes and copies.");

        DensityBuffer empty = DensityBuffer.CreateUnpooled(0);
        empty.Fill(1.0f);
        Require(empty.Capacity() == 0 && empty.Size() == 0,
            "DensityBuffer must support zero-length storage.");
    }

    private static TException Capture<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException exception) { return exception; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
