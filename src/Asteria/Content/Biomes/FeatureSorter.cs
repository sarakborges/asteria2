using Asteria.World.LevelGen.Placement;

namespace Asteria.Content.Biomes;

/// <summary>
/// Builds a stable per-decoration-step feature order from all contributing biomes.
/// Consecutive features declared by a biome form dependency edges; cycles are rejected
/// with the biome sources that still participate in the cycle.
/// </summary>
public static class FeatureSorter
{
    public static IReadOnlyList<StepFeatureData> BuildFeaturesPerStep<TSource>(
        IReadOnlyList<TSource> featureSources,
        Func<TSource, IReadOnlyList<HolderSet<PlacedFeature>>> featureGetter,
        bool tryReducingError)
    {
        ArgumentNullException.ThrowIfNull(featureSources);
        ArgumentNullException.ThrowIfNull(featureGetter);

        var featureIndexes = new Dictionary<PlacedFeature, int>(
            ReferenceEqualityComparer.Instance);
        int nextFeatureIndex = 0;
        var edges = new SortedDictionary<FeatureData, SortedSet<FeatureData>>(
            FeatureDataComparer.Instance);
        int maxStep = 0;

        foreach (TSource featureSource in featureSources)
        {
            IReadOnlyList<HolderSet<PlacedFeature>> featuresForStep =
                featureGetter(featureSource) ??
                throw new InvalidOperationException("A biome returned a null feature-step list.");
            maxStep = Math.Max(maxStep, featuresForStep.Count);

            var featureList = new List<FeatureData>();
            for (int step = 0; step < featuresForStep.Count; step++)
            {
                HolderSet<PlacedFeature> featureSet = featuresForStep[step] ??
                    throw new InvalidOperationException($"Feature holder set at step {step} is null.");

                foreach (Holder<PlacedFeature> featureHolder in featureSet)
                {
                    PlacedFeature feature = featureHolder.Value;
                    if (!featureIndexes.TryGetValue(feature, out int featureIndex))
                    {
                        featureIndex = nextFeatureIndex++;
                        featureIndexes.Add(feature, featureIndex);
                    }

                    featureList.Add(new FeatureData(featureIndex, step, feature));
                }
            }

            for (int index = 0; index < featureList.Count; index++)
            {
                FeatureData current = featureList[index];
                if (!edges.TryGetValue(current, out SortedSet<FeatureData>? successors))
                {
                    successors = new SortedSet<FeatureData>(FeatureDataComparer.Instance);
                    edges.Add(current, successors);
                }

                if (index < featureList.Count - 1)
                    successors.Add(featureList[index + 1]);
            }
        }

        var discovered = new HashSet<FeatureData>();
        var currentlyVisiting = new HashSet<FeatureData>();
        var sortedFeatures = new List<FeatureData>();

        foreach (FeatureData feature in edges.Keys)
        {
            if (currentlyVisiting.Count != 0)
            {
                throw new InvalidOperationException(
                    "FeatureSorter DFS finished with a non-empty in-progress vertex set.");
            }

            if (!discovered.Contains(feature) &&
                DepthFirstSearch(
                    edges,
                    discovered,
                    currentlyVisiting,
                    sortedFeatures,
                    feature))
            {
                if (!tryReducingError)
                    throw new InvalidOperationException("Feature order cycle found");

                List<TSource> reducedSources = featureSources.ToList();
                int lastSize;
                do
                {
                    lastSize = reducedSources.Count;
                    int sourceIndex = 0;
                    while (sourceIndex < reducedSources.Count)
                    {
                        TSource source = reducedSources[sourceIndex];
                        reducedSources.RemoveAt(sourceIndex);

                        try
                        {
                            _ = BuildFeaturesPerStep(reducedSources, featureGetter, tryReducingError: false);
                        }
                        catch (InvalidOperationException)
                        {
                            // This source is still needed to break the cycle; leave it removed
                            // while checking the remaining sources.
                            continue;
                        }

                        reducedSources.Insert(sourceIndex, source);
                        sourceIndex++;
                    }
                }
                while (lastSize != reducedSources.Count);

                throw new InvalidOperationException(
                    $"Feature order cycle found, involved sources: {string.Join(", ", reducedSources)}");
            }
        }

        sortedFeatures.Reverse();
        var result = new List<StepFeatureData>(maxStep);

        for (int step = 0; step < maxStep; step++)
        {
            int currentStep = step;
            List<PlacedFeature> featuresInStep = sortedFeatures
                .Where(feature => feature.Step == currentStep)
                .Select(feature => feature.Feature)
                .ToList();

            var indexMapping = new Dictionary<PlacedFeature, int>(
                ReferenceEqualityComparer.Instance);
            for (int index = 0; index < featuresInStep.Count; index++)
                indexMapping[featuresInStep[index]] = index;

            result.Add(new StepFeatureData(
                featuresInStep,
                feature => indexMapping.TryGetValue(feature, out int index) ? index : -1));
        }

        return result.AsReadOnly();
    }

    private static bool DepthFirstSearch(
        SortedDictionary<FeatureData, SortedSet<FeatureData>> edges,
        HashSet<FeatureData> discovered,
        HashSet<FeatureData> currentlyVisiting,
        List<FeatureData> sortedFeatures,
        FeatureData feature)
    {
        if (discovered.Contains(feature))
            return false;

        if (!currentlyVisiting.Add(feature))
            return true;

        try
        {
            if (edges.TryGetValue(feature, out SortedSet<FeatureData>? successors))
            {
                foreach (FeatureData successor in successors)
                {
                    if (DepthFirstSearch(
                            edges,
                            discovered,
                            currentlyVisiting,
                            sortedFeatures,
                            successor))
                    {
                        return true;
                    }
                }
            }

            discovered.Add(feature);
            sortedFeatures.Add(feature);
            return false;
        }
        finally
        {
            currentlyVisiting.Remove(feature);
        }
    }

    private sealed record FeatureData(
        int FeatureIndex,
        int Step,
        PlacedFeature Feature);

    private sealed class FeatureDataComparer : IComparer<FeatureData>
    {
        public static FeatureDataComparer Instance { get; } = new();

        public int Compare(FeatureData? left, FeatureData? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;

            int stepComparison = left.Step.CompareTo(right.Step);
            return stepComparison != 0
                ? stepComparison
                : left.FeatureIndex.CompareTo(right.FeatureIndex);
        }
    }

    public sealed class StepFeatureData
    {
        public IReadOnlyList<PlacedFeature> Features { get; }
        public Func<PlacedFeature, int> IndexMapping { get; }

        internal StepFeatureData(
            IReadOnlyList<PlacedFeature> features,
            Func<PlacedFeature, int> indexMapping)
        {
            Features = features ?? throw new ArgumentNullException(nameof(features));
            IndexMapping = indexMapping ?? throw new ArgumentNullException(nameof(indexMapping));
        }
    }
}
