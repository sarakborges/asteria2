using Asteria.Content.Chunks;

namespace Asteria.World.Lighting;

/// <summary>
/// Stores light data layers by packed section position and caches the two most
/// recently queried sections, matching Minecraft Java 26.3.
/// </summary>
public abstract class DataLayerStorageMap<TMap>
    where TMap : DataLayerStorageMap<TMap>
{
    private const int CacheSize = 2;

    private readonly long[] _lastSectionKeys = new long[CacheSize];
    private readonly DataLayer?[] _lastSections = new DataLayer?[CacheSize];
    private bool _cacheEnabled;

    protected readonly Dictionary<long, DataLayer> Map;

    protected DataLayerStorageMap(Dictionary<long, DataLayer> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Map = map;
        ClearCache();
        _cacheEnabled = true;
    }

    public int Count => Map.Count;

    public abstract TMap Copy();

    public DataLayer CopyDataLayer(long sectionNode)
    {
        DataLayer newDataLayer = Map[sectionNode].Copy();
        Map[sectionNode] = newDataLayer;
        ClearCache();
        return newDataLayer;
    }

    public bool HasLayer(long sectionNode) =>
        Map.ContainsKey(sectionNode);

    public DataLayer? GetLayer(long sectionNode)
    {
        if (_cacheEnabled)
        {
            for (int i = 0; i < CacheSize; i++)
            {
                if (sectionNode == _lastSectionKeys[i])
                    return _lastSections[i];
            }
        }

        if (!Map.TryGetValue(sectionNode, out DataLayer? data))
            return null;

        if (_cacheEnabled)
        {
            for (int i = CacheSize - 1; i > 0; i--)
            {
                _lastSectionKeys[i] = _lastSectionKeys[i - 1];
                _lastSections[i] = _lastSections[i - 1];
            }

            _lastSectionKeys[0] = sectionNode;
            _lastSections[0] = data;
        }

        return data;
    }

    public DataLayer? RemoveLayer(long sectionNode)
    {
        Map.Remove(sectionNode, out DataLayer? removed);
        return removed;
    }

    public void SetLayer(long sectionNode, DataLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        Map[sectionNode] = layer;
    }

    public void ClearCache()
    {
        Array.Fill(_lastSectionKeys, long.MaxValue);
        Array.Clear(_lastSections);
    }

    public void DisableCache() =>
        _cacheEnabled = false;
}
