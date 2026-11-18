# Block registry, block states, and chunk-section storage

## Reference

Minecraft Java Edition 26.3 source is the reference for the port. Asteria content definitions remain original; system architecture and data flow are ported rather than re-invented.

## Registry and holder model

- Registry<T> mirrors the MappedRegistry lookup model: key, location, value-identity, and numeric-ID maps point to stable Holder.Reference<T> objects.
- RegistryEntry<T> is a thin Asteria inspection view around a holder and runtime ID; it is not the identity-bearing content reference.
- Holder<T> distinguishes direct values from registry references. Direct holders cannot be serialized as registry references.
- Standalone references may be requested before registration through HolderGetter<T>. Their values bind during registry freeze.
- Intrusive registries require values to have an intrusive holder created before registration. Registration binds the key to that same holder; missing intrusive holders are rejected.
- Freeze binds values, rejects unbound placeholders and unregistered intrusive holders, and publishes the initial bound tag set.
- Registration-time tags are named holder sets. Tag access is unavailable before binding. Post-freeze changes use PrepareTagReload and publish a replacement tag snapshot.
- DefaultedRegistry<T> adds default lookup semantics without changing exact holder lookups.

The port does not yet include RegistrationInfo/Lifecycle, data-component lookup, HolderLookup.Provider, the complete tag-loader pipeline, or registry synchronization codecs.

## Block states

- StateProperty<T> describes a named, typed property and its finite ordered values.
- StateDefinition sorts property names, materializes the Cartesian product, and links each state to precomputed neighbor states.
- BlockState.SetValue, TrySetValue, and Cycle resolve through cached neighbor tables, following the StateHolder transition model.
- Global runtime state IDs are assigned after block registration and frozen before palette containers are created.

## Paletted storage

- PalettedContainerStrategy<T> selects cubic size, coordinate layout, and palette configuration. Block states use 16³ entries and 4–8 bit local palettes; biomes use 4³ entries and 1–3 bit local palettes.
- PalettedContainer<T>.Data groups the active palette, storage, and configuration. Resize builds a new data snapshot and publishes it atomically.
- Acquire/Release enforce one writer at a time; unchecked writes are for callers already holding the permit.
- Palette implementations are single-value, linear, hash-map, and global/direct.
- PackedBitStorage matches the vanilla word layout: entries never cross 64-bit word boundaries.
- Full codec, network buffer, NBT packing, and disk serialization paths are not implemented yet.

## Chunk sections

- ChunkSection is a 16×16×16 block-state container.
- Linear index is (y << 8) | (z << 4) | x.
- The current section tracks the non-air block count and exposes the palette threading permit.
- Other LevelChunkSection counters, fluid-state container, biome holder palette, copy, and serialization operations still need their matching domain types and codecs.

## Remaining parity work

- Complete MappedRegistry details: registration metadata/lifecycle, complete Holder/HolderSet contracts, HolderLookup, PendingTags, and synchronization.
- Port block/fluid state behavior, section counters, biome storage, chunk column layout, and chunk persistence using matching Minecraft class relationships.
- Compare each subsequent system against the same Minecraft 26.3 source before implementation; do not substitute a fresh Asteria-specific design for a vanilla system.
