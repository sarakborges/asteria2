namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Structure metadata exposed by chunk implementations, matching Minecraft Java 26.3
/// StructureAccess. The returned reference set is mutable; the aggregate reference map
/// is read-only while its contained sets retain their mutability.
/// </summary>
public interface StructureAccess
{
    StructureStart? GetStartForStructure(Structure structure);

    void SetStartForStructure(Structure structure, StructureStart structureStart);

    ISet<long> GetReferencesForStructure(Structure structure);

    void AddReferenceForStructure(Structure structure, long reference);

    IReadOnlyDictionary<Structure, HashSet<long>> GetAllReferences();

    void SetAllReferences(
        IReadOnlyDictionary<Structure, HashSet<long>> structureReferences);
}
