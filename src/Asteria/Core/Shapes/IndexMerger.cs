namespace Asteria.Core.Shapes;

public interface IndexMerger
{
    public delegate bool IndexConsumer(int firstIndex, int secondIndex, int resultIndex);

    bool ForMergedIndexes(IndexConsumer consumer);
    int Size();
    DoubleList GetList();
}

public sealed class IdenticalMerger : IndexMerger
{
    private readonly DoubleList _coordinates;

    public IdenticalMerger(DoubleList coordinates) =>
        _coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));

    public bool ForMergedIndexes(IndexMerger.IndexConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        int size = _coordinates.Count - 1;
        for (int index = 0; index < size; index++)
        {
            if (!consumer(index, index, index))
                return false;
        }

        return true;
    }

    public int Size() => _coordinates.Count;

    public DoubleList GetList() => _coordinates;
}

public sealed class NonOverlappingMerger : DoubleList, IndexMerger
{
    private readonly DoubleList _lower;
    private readonly DoubleList _upper;
    private readonly bool _swap;

    public NonOverlappingMerger(DoubleList lower, DoubleList upper, bool swap)
    {
        _lower = lower ?? throw new ArgumentNullException(nameof(lower));
        _upper = upper ?? throw new ArgumentNullException(nameof(upper));
        _swap = swap;
    }

    public override int Count => _lower.Count + _upper.Count;

    public override double GetDouble(int index) =>
        index < _lower.Count
            ? _lower.GetDouble(index)
            : _upper.GetDouble(index - _lower.Count);

    public bool ForMergedIndexes(IndexMerger.IndexConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        return _swap
            ? ForNonSwappedIndexes((firstIndex, secondIndex, resultIndex) =>
                consumer(secondIndex, firstIndex, resultIndex))
            : ForNonSwappedIndexes(consumer);
    }

    private bool ForNonSwappedIndexes(IndexMerger.IndexConsumer consumer)
    {
        int lowerSize = _lower.Count;
        for (int index = 0; index < lowerSize; index++)
        {
            if (!consumer(index, -1, index))
                return false;
        }

        int upperSize = _upper.Count - 1;
        for (int index = 0; index < upperSize; index++)
        {
            if (!consumer(lowerSize - 1, index, lowerSize + index))
                return false;
        }

        return true;
    }

    public int Size() => Count;

    public DoubleList GetList() => this;
}

public sealed class IndirectMerger : IndexMerger
{
    private const double Epsilon = 1.0E-7;
    private static readonly DoubleList EmptyCoordinates = new ArrayDoubleList(0.0);

    private readonly double[] _result;
    private readonly int[] _firstIndices;
    private readonly int[] _secondIndices;
    private readonly int _resultLength;

    public IndirectMerger(
        DoubleList first,
        DoubleList second,
        bool firstOnlyMatters,
        bool secondOnlyMatters)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        double lastValue = double.NaN;
        int firstSize = first.Count;
        int secondSize = second.Count;
        int capacity = checked(firstSize + secondSize);
        _result = new double[capacity];
        _firstIndices = new int[capacity];
        _secondIndices = new int[capacity];

        bool canSkipFirst = !firstOnlyMatters;
        bool canSkipSecond = !secondOnlyMatters;
        int resultIndex = 0;
        int firstIndex = 0;
        int secondIndex = 0;

        while (true)
        {
            bool ranOutOfFirst = firstIndex >= firstSize;
            bool ranOutOfSecond = secondIndex >= secondSize;
            if (ranOutOfFirst && ranOutOfSecond)
            {
                _resultLength = Math.Max(1, resultIndex);
                return;
            }

            bool choseFirst = !ranOutOfFirst &&
                (ranOutOfSecond || first.GetDouble(firstIndex) < second.GetDouble(secondIndex) + Epsilon);

            if (choseFirst)
            {
                firstIndex++;
                if (canSkipFirst && (secondIndex == 0 || ranOutOfSecond))
                    continue;
            }
            else
            {
                secondIndex++;
                if (canSkipSecond && (firstIndex == 0 || ranOutOfFirst))
                    continue;
            }

            int currentFirstIndex = firstIndex - 1;
            int currentSecondIndex = secondIndex - 1;
            double nextValue = choseFirst
                ? first.GetDouble(currentFirstIndex)
                : second.GetDouble(currentSecondIndex);

            if (!(lastValue >= nextValue - Epsilon))
            {
                _firstIndices[resultIndex] = currentFirstIndex;
                _secondIndices[resultIndex] = currentSecondIndex;
                _result[resultIndex] = nextValue;
                resultIndex++;
                lastValue = nextValue;
            }
            else
            {
                _firstIndices[resultIndex - 1] = currentFirstIndex;
                _secondIndices[resultIndex - 1] = currentSecondIndex;
            }
        }
    }

    public bool ForMergedIndexes(IndexMerger.IndexConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        int length = _resultLength - 1;
        for (int index = 0; index < length; index++)
        {
            if (!consumer(_firstIndices[index], _secondIndices[index], index))
                return false;
        }

        return true;
    }

    public int Size() => _resultLength;

    public DoubleList GetList() =>
        _resultLength <= 1
            ? EmptyCoordinates
            : new ArrayDoubleList(_result.AsSpan(0, _resultLength).ToArray());
}

public sealed class DiscreteCubeMerger : DoubleList, IndexMerger
{
    private readonly int _result;
    private readonly int _firstDiv;
    private readonly int _secondDiv;

    public DiscreteCubeMerger(int firstParts, int secondParts)
    {
        if (firstParts <= 0)
            throw new ArgumentOutOfRangeException(nameof(firstParts));
        if (secondParts <= 0)
            throw new ArgumentOutOfRangeException(nameof(secondParts));

        long result = (long)firstParts * (secondParts / GreatestCommonDivisor(firstParts, secondParts));
        if (result > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(firstParts));

        _result = (int)result;
        _firstDiv = _result / firstParts;
        _secondDiv = _result / secondParts;
    }

    public override int Count => _result + 1;

    public override double GetDouble(int index)
    {
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return (double)index / _result;
    }

    public bool ForMergedIndexes(IndexMerger.IndexConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        for (int index = 0; index < _result; index++)
        {
            if (!consumer(index / _firstDiv, index / _secondDiv, index))
                return false;
        }

        return true;
    }

    public int Size() => Count;

    public DoubleList GetList() => this;

    private static int GreatestCommonDivisor(int first, int second)
    {
        while (second != 0)
        {
            int remainder = first % second;
            first = second;
            second = remainder;
        }

        return first;
    }
}

public static class IndexMergers
{
    private const double Epsilon = 1.0E-7;

    public static IndexMerger Create(
        int cost,
        DoubleList first,
        DoubleList second,
        bool firstOnlyMatters,
        bool secondOnlyMatters)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        int firstSize = first.Count - 1;
        int secondSize = second.Count - 1;

        if (first is CubePointRange && second is CubePointRange)
        {
            long size = LeastCommonMultiple(firstSize, secondSize);
            if (cost * size <= 256L)
                return new DiscreteCubeMerger(firstSize, secondSize);
        }

        if (first.GetDouble(firstSize) < second.GetDouble(0) - Epsilon)
            return new NonOverlappingMerger(first, second, swap: false);

        if (second.GetDouble(secondSize) < first.GetDouble(0) - Epsilon)
            return new NonOverlappingMerger(second, first, swap: true);

        return firstSize == secondSize && first.Equals(second)
            ? new IdenticalMerger(first)
            : new IndirectMerger(first, second, firstOnlyMatters, secondOnlyMatters);
    }

    public static long LeastCommonMultiple(int first, int second) =>
        (long)first * (second / GreatestCommonDivisor(first, second));

    private static int GreatestCommonDivisor(int first, int second)
    {
        while (second != 0)
        {
            int remainder = first % second;
            first = second;
            second = remainder;
        }

        return first;
    }
}
