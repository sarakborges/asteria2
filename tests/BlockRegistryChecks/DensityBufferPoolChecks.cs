using Asteria.Core;
using Asteria.World.LevelGen.DensityFunctions;

internal static class DensityBufferPoolChecks
{
    public static void Run()
    {
        CheckRoundToward();
        CheckRoundedAllocationAndExactReuse();
        CheckBestFitAndMaximumReuseCapacity();
        CheckGarbageCollection();
        CheckClear();
    }

    private static void CheckRoundToward()
    {
        Require(
            Mth.RoundToward(0, 16) == 0 &&
            Mth.RoundToward(1, 16) == 16 &&
            Mth.RoundToward(16, 16) == 16 &&
            Mth.RoundToward(17, 16) == 32 &&
            Mth.RoundToward(-1, 16) == 0 &&
            Mth.RoundToward(-16, 16) == -16 &&
            Mth.RoundToward(-17, 16) == -16,
            "Mth.roundToward must use positive ceiling division for positive and negative inputs.");

        Require(
            Mth.PositiveCeilDiv(17, 16) == 2 &&
            Mth.PositiveCeilDiv(-17, 16) == -1,
            "Mth.positiveCeilDiv must preserve Java floor-division semantics.");
    }

    private static void CheckRoundedAllocationAndExactReuse()
    {
        var pool = new DensityBufferPool(maxAge: 2);
        ScopedDensityBuffer first = pool.Acquire(1);

        Require(
            first.Capacity() == 16 && first.Size() == 1 && !first.IsClosed,
            "DensityBufferPool.acquire must round capacity to a multiple of sixteen while preserving the requested active size.");

        first.Close();
        first.Close();
        Require(pool.Count == 1, "Closing a scoped density buffer repeatedly must release it only once.");

        ScopedDensityBuffer reused = pool.Acquire(15);
        Require(
            ReferenceEquals(first, reused) &&
            reused.Capacity() == 16 &&
            reused.Size() == 15 &&
            reused.Age == 0 &&
            !reused.IsClosed &&
            pool.IsEmpty(),
            "The pool must prefer exact-capacity reuse and restore the active size, age and open state.");
        reused.Close();
    }

    private static void CheckBestFitAndMaximumReuseCapacity()
    {
        var pool = new DensityBufferPool(maxAge: 2);
        ScopedDensityBuffer capacity64 = pool.Acquire(49);
        ScopedDensityBuffer capacity48 = pool.Acquire(33);
        capacity64.Close();
        capacity48.Close();

        ScopedDensityBuffer bestFit = pool.Acquire(17);
        Require(
            ReferenceEquals(bestFit, capacity48) &&
            bestFit.Capacity() == 48 &&
            bestFit.Size() == 17 &&
            pool.Count == 1,
            "The pool must select the smallest reusable capacity above the rounded request.");

        bestFit.Close();

        var upperBoundPool = new DensityBufferPool(maxAge: 2);
        ScopedDensityBuffer capacity64Only = upperBoundPool.Acquire(49);
        capacity64Only.Close();

        ScopedDensityBuffer fresh32 = upperBoundPool.Acquire(17);
        Require(
            fresh32.Capacity() == 32 &&
            !ReferenceEquals(fresh32, capacity64Only) &&
            upperBoundPool.Count == 1,
            "The pool must reject a buffer whose capacity equals the exclusive maximum reuse bound.");
        fresh32.Close();
    }

    private static void CheckGarbageCollection()
    {
        var pool = new DensityBufferPool(maxAge: 2);
        ScopedDensityBuffer buffer = pool.Acquire(8);
        buffer.Close();

        pool.GarbageCollect();
        Require(pool.Count == 1 && buffer.Age == 1,
            "Garbage collection must age retained buffers once per pass.");
        pool.GarbageCollect();
        Require(pool.Count == 1 && buffer.Age == 2,
            "A buffer must remain reusable while its age is equal to maxAge.");
        pool.GarbageCollect();
        Require(pool.IsEmpty() && buffer.Age == 3,
            "A buffer must be removed when its incremented age exceeds maxAge.");

        ScopedDensityBuffer fresh = pool.Acquire(8);
        Require(!ReferenceEquals(fresh, buffer),
            "A buffer removed by garbage collection must not be reused.");
        fresh.Close();
    }

    private static void CheckClear()
    {
        var pool = new DensityBufferPool(maxAge: 2);
        ScopedDensityBuffer buffer = pool.Acquire(8);
        buffer.Close();
        pool.Clear();

        Require(pool.IsEmpty(),
            "DensityBufferPool.clear must discard every idle buffer.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
