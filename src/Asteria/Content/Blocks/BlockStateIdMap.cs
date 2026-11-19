using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Global insertion-ordered ID map for every precomputed block state.
/// IDs are runtime values and must not be serialized as persistent block identifiers.
/// </summary>
public sealed class BlockStateIdMap : IReadOnlyCollection<BlockState>, IIdMap<BlockState>
{
    private readonly List<BlockState> _states = new();
    private readonly Dictionary<BlockState, int> _ids =
        new(ReferenceEqualityComparer.Instance);

    public int Count => _states.Count;
    public bool IsFrozen { get; private set; }

    internal int Register(BlockState state)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(state);

        if (_ids.TryGetValue(state, out int existingId))
        {
            throw new InvalidOperationException(
                $"Block state '{state}' is already registered with global ID {existingId}.");
        }

        int id = _states.Count;
        _states.Add(state);
        _ids.Add(state, id);
        return id;
    }

    public int GetId(BlockState? state) =>
        state is not null && _ids.TryGetValue(state, out int id) ? id : -1;

    public BlockState? ById(int id) =>
        (uint)id < (uint)_states.Count ? _states[id] : null;

    public BlockState GetById(int id) =>
        ById(id) ?? throw new ArgumentOutOfRangeException(
            nameof(id), id, "The block state runtime ID does not exist.");

    public bool TryGetById(int id, out BlockState? state)
    {
        if ((uint)id < (uint)_states.Count)
        {
            state = _states[id];
            return true;
        }

        state = null;
        return false;
    }

    public void Freeze() => IsFrozen = true;

    public IEnumerator<BlockState> GetEnumerator() => _states.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    private void EnsureWritable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("The block state ID map is frozen.");
        }
    }
}
