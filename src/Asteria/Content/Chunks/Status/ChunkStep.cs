namespace Asteria.Content.Chunks.Status;

/// <summary>
/// Describes one chunk-generation stage's direct and accumulated neighbor requirements.
/// Mirrors the metadata and dependency builder of Minecraft Java 26.3 ChunkStep.
/// Task execution is kept separate until the local world-generation context and holder scheduler exist.
/// </summary>
public sealed class ChunkStep
{
    public ChunkStatus TargetStatus { get; }
    public ChunkDependencies DirectDependencies { get; }
    public ChunkDependencies AccumulatedDependencies { get; }
    public int BlockStateWriteRadius { get; }

    private ChunkStep(
        ChunkStatus targetStatus,
        ChunkDependencies directDependencies,
        ChunkDependencies accumulatedDependencies,
        int blockStateWriteRadius)
    {
        TargetStatus = targetStatus ?? throw new ArgumentNullException(nameof(targetStatus));
        DirectDependencies = directDependencies ?? throw new ArgumentNullException(nameof(directDependencies));
        AccumulatedDependencies = accumulatedDependencies ??
            throw new ArgumentNullException(nameof(accumulatedDependencies));
        BlockStateWriteRadius = blockStateWriteRadius;
    }

    public int GetAccumulatedRadiusOf(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return ReferenceEquals(status, TargetStatus)
            ? 0
            : AccumulatedDependencies.GetRadiusOf(status);
    }

    /// <summary>Builds one immutable step from the status chain and radius requirements.</summary>
    public sealed class Builder
    {
        private readonly ChunkStatus _status;
        private readonly ChunkStep? _parent;
        private ChunkStatus[] _directDependenciesByRadius;
        private int _blockStateWriteRadius = -1;

        internal Builder(ChunkStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);
            if (!ReferenceEquals(status.GetParent(), status))
                throw new ArgumentException(
                    $"Not starting with the first status: {status}",
                    nameof(status));

            _status = status;
            _parent = null;
            _directDependenciesByRadius = Array.Empty<ChunkStatus>();
        }

        internal Builder(ChunkStatus status, ChunkStep parent)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(parent);

            if (parent.TargetStatus.GetIndex() != status.GetIndex() - 1)
                throw new ArgumentException($"Out of order status: {status}", nameof(status));

            _status = status;
            _parent = parent;
            _directDependenciesByRadius = new[] { parent.TargetStatus };
        }

        public Builder AddRequirement(ChunkStatus status, int radius)
        {
            ArgumentNullException.ThrowIfNull(status);
            if (status.IsOrAfter(_status))
            {
                throw new ArgumentException(
                    $"Status {status} can not be required by {_status}",
                    nameof(status));
            }

            int newLength = unchecked(radius + 1);
            ChunkStatus[] previous = _directDependenciesByRadius;
            if (newLength > previous.Length)
            {
                _directDependenciesByRadius = new ChunkStatus[newLength];
                Array.Fill(_directDependenciesByRadius, status);
            }

            for (int index = 0; index < Math.Min(newLength, previous.Length); index++)
            {
                _directDependenciesByRadius[index] =
                    ChunkStatus.Max(previous[index], status);
            }

            return this;
        }

        public Builder BlockStateWriteRadius(int radius)
        {
            _blockStateWriteRadius = radius;
            return this;
        }

        public ChunkStep Build()
        {
            ChunkStatus[] direct = (ChunkStatus[])_directDependenciesByRadius.Clone();
            var directDependencies = new ChunkDependencies(direct);
            var accumulatedDependencies =
                new ChunkDependencies(BuildAccumulatedDependencies(direct));

            return new ChunkStep(
                _status,
                directDependencies,
                accumulatedDependencies,
                _blockStateWriteRadius);
        }

        private ChunkStatus[] BuildAccumulatedDependencies(ChunkStatus[] direct)
        {
            if (_parent is null)
                return direct;

            int radiusOfParent = GetRadiusOfParent(_parent.TargetStatus, direct);
            ChunkDependencies parentDependencies = _parent.AccumulatedDependencies;
            int length = Math.Max(
                unchecked(radiusOfParent + parentDependencies.Size()),
                direct.Length);
            var accumulated = new ChunkStatus[length];

            for (int distance = 0; distance < accumulated.Length; distance++)
            {
                int distanceInParent = distance - radiusOfParent;
                if (distanceInParent < 0 || distanceInParent >= parentDependencies.Size())
                {
                    accumulated[distance] = direct[distance];
                }
                else if (distance >= direct.Length)
                {
                    accumulated[distance] = parentDependencies.Get(distanceInParent);
                }
                else
                {
                    accumulated[distance] = ChunkStatus.Max(
                        direct[distance],
                        parentDependencies.Get(distanceInParent));
                }
            }

            return accumulated;
        }

        private static int GetRadiusOfParent(ChunkStatus status, ChunkStatus[] direct)
        {
            for (int index = direct.Length - 1; index >= 0; index--)
            {
                if (direct[index].IsOrAfter(status))
                    return index;
            }

            return 0;
        }
    }
}
