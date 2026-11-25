using Asteria.Content.Chunks;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;

internal static class ChunkStructureAccessChecks
{
    public static void Run()
    {
        CheckStructureStartStorage();
        CheckStructureReferenceStorage();
    }

    private static void CheckStructureStartStorage()
    {
        var chunk = CreateChunk();
        var firstStructure = new TestStructure();
        var secondStructure = new TestStructure();
        StructureStart firstStart = CreateStart(firstStructure, new ChunkPos(-2, 6));
        StructureStart secondStart = CreateStart(secondStructure, new ChunkPos(4, -9));

        Require(
            chunk.GetAllStarts().Count == 0 &&
            chunk.GetStartForStructure(firstStructure) is null,
            "A new chunk must start without structure starts.");

        chunk.SetStartForStructure(firstStructure, firstStart);
        Require(
            ReferenceEquals(chunk.GetStartForStructure(firstStructure), firstStart) &&
            ReferenceEquals(chunk.GetAllStarts()[firstStructure], firstStart) &&
            chunk.IsUnsaved(),
            "Setting one structure start must publish it through both getters and mark the chunk unsaved.");

        chunk.TryMarkSaved();
        chunk.SetAllStarts(new Dictionary<Structure, StructureStart>
        {
            [firstStructure] = firstStart,
            [secondStructure] = secondStart
        });
        Require(
            chunk.GetAllStarts().Count == 2 &&
            ReferenceEquals(chunk.GetStartForStructure(firstStructure), firstStart) &&
            ReferenceEquals(chunk.GetStartForStructure(secondStructure), secondStart) &&
            chunk.IsUnsaved(),
            "SetAllStarts must replace the map contents and mark the chunk unsaved.");

        chunk.TryMarkSaved();
        chunk.SetAllStarts(new Dictionary<Structure, StructureStart>
        {
            [secondStructure] = secondStart
        });
        Require(
            chunk.GetAllStarts().Count == 1 &&
            chunk.GetStartForStructure(firstStructure) is null &&
            ReferenceEquals(chunk.GetStartForStructure(secondStructure), secondStart) &&
            chunk.IsUnsaved(),
            "Replacing all starts must remove entries absent from the supplied map.");

        RequireThrows<NotSupportedException>(
            () => ((IDictionary<Structure, StructureStart>)chunk.GetAllStarts())
                .Add(new TestStructure(), firstStart),
            "The all-starts map view must prevent callers from adding map entries.");
    }

    private static void CheckStructureReferenceStorage()
    {
        var chunk = CreateChunk();
        var structure = new TestStructure();
        var unrelatedStructure = new TestStructure();

        Require(
            chunk.GetReferencesForStructure(structure).Count == 0 &&
            chunk.GetAllReferences().Count == 0,
            "A new chunk must expose an empty reference set and map.");

        long firstReference = ChunkPos.Pack(2, -3);
        long secondReference = ChunkPos.Pack(-8, 11);
        chunk.AddReferenceForStructure(structure, firstReference);

        ISet<long> firstReferenceSet =
            chunk.GetReferencesForStructure(structure);
        Require(
            firstReferenceSet.Count == 1 &&
            firstReferenceSet.Contains(firstReference) &&
            ReferenceEquals(chunk.GetAllReferences()[structure], firstReferenceSet) &&
            chunk.IsUnsaved(),
            "Adding a reference must create the structure's set, publish it and mark the chunk unsaved.");

        StructureAccess structureAccess = chunk;
        chunk.TryMarkSaved();
        long directMutationReference = ChunkPos.Pack(13, 14);
        ISet<long> mutableReferenceSet =
            structureAccess.GetReferencesForStructure(structure);
        Require(
            mutableReferenceSet.Add(directMutationReference) &&
            firstReferenceSet.Contains(directMutationReference) &&
            !chunk.IsUnsaved(),
            "StructureAccess must expose the stored mutable reference set; direct set mutation does not mark the chunk unsaved.");

        chunk.TryMarkSaved();
        chunk.AddReferenceForStructure(structure, firstReference);
        Require(
            firstReferenceSet.Count == 2 && chunk.IsUnsaved(),
            "Duplicate references must remain deduplicated while the add operation still marks the chunk unsaved.");

        chunk.TryMarkSaved();
        chunk.AddReferenceForStructure(structure, secondReference);
        Require(
            firstReferenceSet.Count == 3 &&
            firstReferenceSet.Contains(secondReference) &&
            chunk.IsUnsaved(),
            "Adding a distinct reference must extend the existing structure reference set.");

        var replacementReferences = new HashSet<long> { secondReference };
        chunk.TryMarkSaved();
        chunk.SetAllReferences(new Dictionary<Structure, HashSet<long>>
        {
            [unrelatedStructure] = new() { firstReference },
            [structure] = replacementReferences
        });

        Require(
            chunk.GetAllReferences().Count == 2 &&
            chunk.GetReferencesForStructure(structure).SetEquals(new[] { secondReference }) &&
            chunk.GetReferencesForStructure(unrelatedStructure).Contains(firstReference) &&
            ReferenceEquals(chunk.GetAllReferences()[structure], replacementReferences) &&
            chunk.IsUnsaved(),
            "SetAllReferences must replace the map and retain the supplied reference-set instances.");

        chunk.TryMarkSaved();
        chunk.SetAllReferences(new Dictionary<Structure, HashSet<long>>());
        Require(
            chunk.GetAllReferences().Count == 0 && chunk.IsUnsaved(),
            "Replacing all references with an empty map must clear previous entries and mark the chunk unsaved.");

        RequireThrows<NotSupportedException>(
            () => ((IDictionary<Structure, HashSet<long>>)chunk.GetAllReferences())
                .Add(structure, new HashSet<long>()),
            "The all-references map view must prevent callers from adding map entries.");
    }

    private static ProtoChunk CreateChunk() =>
        new(
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            PalettedContainerFactory.CreateBuiltIn());

    private static StructureStart CreateStart(Structure structure, ChunkPos position) =>
        new(
            structure,
            position,
            0,
            new PiecesContainer(new[]
            {
                new TestPiece(new BoundingBox(
                    position.GetMinBlockX(),
                    0,
                    position.GetMinBlockZ(),
                    position.GetMinBlockX() + 3,
                    4,
                    position.GetMinBlockZ() + 3))
            }));

    private sealed class TestStructure : Structure
    {
        public TestStructure() : base(StructureSettings.DEFAULT)
        {
        } 
        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;
    }

    private sealed class TestPiece : StructurePiece
    {
        public TestPiece(BoundingBox box) : base(0, box)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }
}
