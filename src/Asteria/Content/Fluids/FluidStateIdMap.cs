using Asteria.Content.Registries;

namespace Asteria.Content.Fluids;

public sealed class FluidStateIdMap : IReadOnlyCollection<FluidState>, IIdMap<FluidState>
{
    private int _nextId;
    private readonly List<FluidState?> _states;
    private readonly Dictionary<FluidState, int> _ids;

    public int Count => _ids.Count;

    public FluidStateIdMap(int expectedSize = 64)
    {
        if (expectedSize < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedSize));

        _states = new List<FluidState?>(expectedSize);
        _ids = new Dictionary<FluidState, int>(expectedSize, ReferenceEqualityComparer.Instance);
    }

    public void Add(FluidState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        AddMapping(state, _nextId);
    }

    public void AddMapping(FluidState state, int id)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        _ids[state] = id;
        while (_states.Count <= id)
            _states.Add(null);

        _states[id] = state;
        if (_nextId <= id)
            _nextId = id + 1;
    }

    public int GetId(FluidState? state) =>
        state is not null && _ids.TryGetValue(state, out int id) ? id : -1;

    public FluidState? ById(int id) =>
        (uint)id < (uint)_states.Count ? _states[id] : null;

    public bool Contains(int id) => ById(id) is not null;

    public IEnumerator<FluidState> GetEnumerator() =>
        _states.Where(state => state is not null).Cast<FluidState>().GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
