namespace Asteria.Content.Chunks.Status;

/// <summary>
/// Describes the minimum chunk status guaranteed at each Chebyshev-distance ring
/// from a chunk being processed. Mirrors Minecraft Java 26.3 ChunkDependencies.
/// </summary>
public sealed class ChunkDependencies
{
    private readonly IReadOnlyList<ChunkStatus> _dependencyByRadius;
    private readonly int[] _radiusByDependency;

    public ChunkDependencies(IEnumerable<ChunkStatus> dependencyByRadius)
    {
        ArgumentNullException.ThrowIfNull(dependencyByRadius);

        ChunkStatus[] dependencies = dependencyByRadius.ToArray();
        if (dependencies.Any(static status => status is null))
            throw new ArgumentException("Chunk dependencies cannot contain null statuses.", nameof(dependencyByRadius));

        _dependencyByRadius = Array.AsReadOnly(dependencies);
        _radiusByDependency = Enumerable.Repeat(-1, ChunkStatus.GetStatusList().Count).ToArray();

        for (int distance = 0; distance < dependencies.Length; distance++)
        {
            int maximumStatusIndex = dependencies[distance].GetIndex();
            for (int statusIndex = 0; statusIndex <= maximumStatusIndex; statusIndex++)
                _radiusByDependency[statusIndex] = distance;
        }
    }

    /// <summary>Returns the immutable status list indexed by distance from the center chunk.</summary>
    public IReadOnlyList<ChunkStatus> AsList() => _dependencyByRadius;

    /// <summary>Returns the number of distance rings, including the center chunk.</summary>
    public int Size() => _dependencyByRadius.Count;

    /// <summary>Returns the greatest represented distance, or -1 for an empty window.</summary>
    public int GetRadius() => _dependencyByRadius.Count - 1;

    /// <summary>Returns the required status at the supplied distance.</summary>
    public ChunkStatus Get(int distance) => _dependencyByRadius[distance];

    /// <summary>
    /// Returns the greatest radius at which the dependency list guarantees the supplied
    /// status or a later status, or -1 when no ring reaches that status.
    /// </summary>
    public int GetRadiusOf(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return _radiusByDependency[status.GetIndex()];
    }

    public override string ToString() =>
        "[" + string.Join(", ", _dependencyByRadius) + "]";
}
