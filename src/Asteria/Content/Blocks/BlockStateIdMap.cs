using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// Block-state runtime ID map equivalent to Minecraft IdMapper.
/// Numeric IDs are insertion ordered by default, but explicit sparse mappings are supported.
/// </summary>
public sealed class BlockStateIdMap : IReadOnlyCollection<BlockState>, IIdMap<BlockState>
{
    private int _nextId;
    private readonly List<BlockState?> _states;
    private readonly Dictionary<BlockState, int> _ids =
        new(ReferenceEqualityComparer.Instance);

    public int Count => _ids.Count;

    public BlockStateIdMap(int expectedSize = 512)
    {
        if (expectedSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize));
        }

        _states = new List<BlockState?>(expectedSize);
    }

    public void Add(BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        AddMapping(state, _nextId);
    }

    public void AddMapping(BlockState state, int id)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        _ids[state] = id;
        while (_states.Count <= id)
        {
            _states.Add(null);
        }

        _states[id] = state;
        if (_nextId <= id)
        {
            _nextId = id + 1;
        }
    }

    public int GetId(BlockState? state) =>
        state is not null && _ids.TryGetValue(state, out int id) ? id : -1;

    public BlockState? ById(int id) =>
        (uint)id < (uint)_states.Count ? _states[id] : null;

    public bool Contains(int id) => ById(id) is not null;

    public IEnumerator<BlockState> GetEnumerator() =>
        _states.Where(state => state is not null).Cast<BlockState>().GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
