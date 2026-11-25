namespace Asteria.Serialization.Nbt;

/// <summary>
/// Mutable indexed NBT collection shared by list and primitive-array tags.
/// </summary>
public interface CollectionTag : Tag, IEnumerable<Tag>
{
    void Clear();
    bool SetTag(int index, Tag tag);
    bool AddTag(int index, Tag tag);
    Tag Remove(int index);
    Tag Get(int index);
    int Size();

    bool IsEmpty() => Size() == 0;

    IEnumerator<Tag> IEnumerable<Tag>.GetEnumerator()
    {
        for (int index = 0; index < Size(); index++)
            yield return Get(index);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        ((IEnumerable<Tag>)this).GetEnumerator();
}
