using Asteria.World.LevelGen;

namespace Asteria.Util.Random;

/// <summary>Weight summation and roulette selection from Minecraft Java 26.3.</summary>
public static class WeightedRandom
{
    public static int GetTotalWeight<T>(
        IReadOnlyList<T> items,
        Func<T, int> weightGetter)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weightGetter);

        long totalWeight = 0L;
        foreach (T item in items)
            totalWeight = unchecked(totalWeight + weightGetter(item));

        if (totalWeight > int.MaxValue)
            throw new ArgumentException("Sum of weights must be <= 2147483647");

        return (int)totalWeight;
    }

    public static bool TryGetRandomItem<T>(
        IRandomSource random,
        IReadOnlyList<T> items,
        int totalWeight,
        Func<T, int> weightGetter,
        out T? item)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weightGetter);

        if (totalWeight < 0)
            throw new ArgumentException("Negative total weight in getRandomItem");

        if (totalWeight == 0)
        {
            item = default;
            return false;
        }

        int selection = random.NextInt(totalWeight);
        return TryGetWeightedItem(items, selection, weightGetter, out item);
    }

    public static bool TryGetWeightedItem<T>(
        IReadOnlyList<T> items,
        int index,
        Func<T, int> weightGetter,
        out T? item)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weightGetter);

        foreach (T current in items)
        {
            index = unchecked(index - weightGetter(current));
            if (index < 0)
            {
                item = current;
                return true;
            }
        }

        item = default;
        return false;
    }

    public static bool TryGetRandomItem<T>(
        IRandomSource random,
        IReadOnlyList<T> items,
        Func<T, int> weightGetter,
        out T? item)
    {
        int totalWeight = GetTotalWeight(items, weightGetter);
        return TryGetRandomItem(random, items, totalWeight, weightGetter, out item);
    }
}
