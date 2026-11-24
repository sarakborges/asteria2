using Asteria.Content.Chunks;
using Asteria.World.Lighting;

internal static class DataLayerStorageMapChecks
{
    private sealed class TestMap : DataLayerStorageMap<TestMap>
    {
        public TestMap()
            : this(new Dictionary<long, DataLayer>())
        {
        }

        private TestMap(Dictionary<long, DataLayer> map)
            : base(map)
        {
        }

        public override TestMap Copy() =>
            new(new Dictionary<long, DataLayer>(Map));
    }

    public static void Run()
    {
        CheckTwoEntryLookupCache();
        CheckSetAndRemoveCacheSemantics();
        CheckCopyDataLayer();
        CheckMapCopyAndCacheDisable();
    }

    private static void CheckTwoEntryLookupCache()
    {
        var map = new TestMap();
        var first = new DataLayer(1);
        var second = new DataLayer(2);
        var third = new DataLayer(3);
        map.SetLayer(11L, first);
        map.SetLayer(22L, second);
        map.SetLayer(33L, third);

        Require(ReferenceEquals(map.GetLayer(11L), first), "The first data layer must be returned.");
        Require(ReferenceEquals(map.GetLayer(22L), second), "The second data layer must be returned.");
        Require(ReferenceEquals(map.GetLayer(11L), first), "A cache hit must return the cached layer.");
        Require(ReferenceEquals(map.GetLayer(33L), third), "A new query must enter the two-entry cache.");
        Require(ReferenceEquals(map.GetLayer(22L), second), "The previously second cached entry must remain cached.");
        Require(ReferenceEquals(map.GetLayer(11L), first), "An evicted entry must still be resolved from the backing map.");
        Require(map.GetLayer(44L) is null, "A missing section must return null.");
    }

    private static void CheckSetAndRemoveCacheSemantics()
    {
        var map = new TestMap();
        var original = new DataLayer(4);
        var replacement = new DataLayer(9);
        map.SetLayer(1L, original);

        Require(ReferenceEquals(map.GetLayer(1L), original), "The first lookup must populate the cache.");
        map.SetLayer(1L, replacement);
        Require(ReferenceEquals(map.GetLayer(1L), original),
            "SetLayer must not implicitly invalidate the vanilla two-entry cache.");
        map.ClearCache();
        Require(ReferenceEquals(map.GetLayer(1L), replacement),
            "ClearCache must make a replaced layer visible.");

        Require(ReferenceEquals(map.RemoveLayer(1L), replacement), "RemoveLayer must return the removed object.");
        Require(ReferenceEquals(map.GetLayer(1L), replacement),
            "RemoveLayer must not implicitly invalidate an existing cache entry.");
        map.ClearCache();
        Require(map.GetLayer(1L) is null, "After cache invalidation, a removed layer must no longer be visible.");
    }

    private static void CheckCopyDataLayer()
    {
        var map = new TestMap();
        var original = new DataLayer(6);
        map.SetLayer(7L, original);
        Require(ReferenceEquals(map.GetLayer(7L), original), "Precondition: the layer should be cached.");

        DataLayer copied = map.CopyDataLayer(7L);
        Require(!ReferenceEquals(copied, original), "CopyDataLayer must replace the stored object with a copy.");
        Require(copied.Get(3, 5, 7) == 6, "CopyDataLayer must preserve light values.");
        Require(ReferenceEquals(map.GetLayer(7L), copied),
            "CopyDataLayer must clear the cache so the copied layer is returned.");
        copied.Set(3, 5, 7, 12);
        Require(original.Get(3, 5, 7) == 6, "The copied storage must not mutate the prior DataLayer.");
    }

    private static void CheckMapCopyAndCacheDisable()
    {
        var map = new TestMap();
        var layer = new DataLayer(5);
        map.SetLayer(9L, layer);
        _ = map.GetLayer(9L);

        TestMap copy = map.Copy();
        Require(ReferenceEquals(copy.GetLayer(9L), layer),
            "The concrete storage-map copy must preserve the contained DataLayer references.");
        copy.SetLayer(9L, new DataLayer(8));
        Require(ReferenceEquals(map.GetLayer(9L), layer),
            "A storage-map copy must own an independent map.");

        map.DisableCache();
        var updated = new DataLayer(10);
        map.SetLayer(9L, updated);
        Require(ReferenceEquals(map.GetLayer(9L), updated),
            "Disabling the cache must make each lookup read the backing map.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
