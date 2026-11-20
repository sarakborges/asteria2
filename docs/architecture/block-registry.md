# Block registry, block states, and chunk-section storage

## Reference and scope

Minecraft Java Edition 26.3 is the reference for architecture, contracts, and behavior. Asteria keeps its own block content; this port follows vanilla systems rather than substituting project-specific designs.

This stage covers only systems already introduced in Asteria: registries and holders, blocks and block states, block-state IDs, palette storage, and the block-state portion of chunk sections. Fluid, biome, data-component, network, and disk-persistence behavior are deferred until their corresponding domain systems are introduced.

## Registry and holder model

- `Registry<T>` follows `MappedRegistry<T>`: location, registry-key, value-identity, numeric-ID, and holder indexes share stable `Holder.Reference<T>` instances.
- `Count`/registry size includes standalone key placeholders. Numeric ID lookup remains separate from key lookup.
- `AsHolderIdMap` exposes registry holders through the vanilla `IdMap<Holder<T>>` view.
- Registration records `RegistrationInfo` and aggregates `Lifecycle`; ordinary registration uses the built-in stable metadata.
- Registry freezing binds values, rejects unbound standalone references and unregistered intrusive holders, verifies tag binding, and publishes the bound tag set.
- Intrusive registries require an intrusive holder before registration. A block creates its intrusive holder before its state definition, as vanilla `Block` does.
- `Holder<T>` supports direct and reference variants, key/predicate/tag membership, ownership checks, unwrap/unwrap-key, registered names, and bound/unbound tag behavior.
- `HolderSet<T>` supports direct and named sets, the vanilla direct-set factories, ordered contents, membership, random selection, unwrap/unwrap-key, and serialization ownership.
- `HolderGetter<T>` exposes optional and throwing key/tag lookup plus random element selection. Registration lookups create stable standalone placeholders.
- `HolderLookup<T>` and `RegistryLookup` expose element/tag enumeration, registry key and lifecycle metadata, and vanilla-style element filtering.
- `DefaultedRegistry<T>` applies fallback semantics only to the vanilla defaulted lookup paths; exact holder and optional lookups remain exact.
- Tag reload uses a pending replacement snapshot. The cross-registry `HolderGetter.Provider` / `HolderLookup.Provider`, complete tag-loader pipeline, and serialization context are not yet introduced.

## Identifiers and resource keys

- `ResourceLocation` follows vanilla identifier parsing, validation, ordering, path transformations, and language/debug naming helpers.
- `RegistryKey<T>` and `TagKey<T>` expose registry matching, typed casts, and dependent resource-key creation.

## Blocks and block states

- `BlockProperties` is a mutable fluent builder for the properties represented so far. Defaults and setter behavior follow `BlockBehaviour.Properties`.
- `Block` creates its intrusive holder before building states and calls the virtual `CreateBlockStateDefinition` extension point.
- `StateProperty<T>` exposes a name, value type, ordered possible values, parsing, and serialization. Equality follows vanilla property type/name/value contracts.
- `StateDefinition` sorts property names, materializes the Cartesian product in vanilla order, and links each state to precomputed neighbor states.
- `BlockState.SetValue`, `TrySetValue`, and `Cycle` resolve through the precomputed neighbor tables.
- Each `BlockState` caches the currently modeled `BlockStateBase` values (`IsAir`, destroy speed, and correct-tool requirement) at construction. Mutating the retained properties builder does not retroactively change existing states.
- `BlockStateIdMap` follows `IdMapper`: mutable insertion and explicit ID mapping, sparse slots, nullable reverse lookup, and size based on mapped values. It is not frozen with the block registry.

## Paletted storage

- `PalettedContainerStrategy<T>` follows the vanilla cubic dimensions and coordinate layout. Block-state containers use 16³ entries and 4–8 bit local palettes. The 4³ biome strategy exists as a sizing strategy only; biome values and biome storage are deferred.
- Configuration selection follows vanilla single-value, linear, hash-map, and global modes, including distinct resident-memory and packed-storage bit widths for global mode.
- `PalettedContainer<T>` exposes coordinate-based get, set, get-and-set, unchecked mutation, palette enumeration, counting, copying, and recreation.
- Resize builds or reuses the matching configuration data, remaps old palette values, then publishes the resulting snapshot.
- `Acquire`/`Release` use a one-writer detector. Unchecked writes are for callers that already hold the write permit.
- `SingleValuePalette`, `LinearPalette`, `HashMapPalette`, and `GlobalPalette` follow their corresponding vanilla palette contracts.
- `PackedBitStorage` follows `SimpleBitStorage` word packing, per-word indexing, fill, get-all, unpack, and raw-array access. `ZeroBitStorage` represents zero-bit palettes without allocating a packed array.
- Missing palette entries use the dedicated exception type.
- Data codecs, network buffer read/write, NBT packing, and disk serialization are not yet introduced.

## Chunk sections

- `ChunkSection` receives a prebuilt 16×16×16 block-state `PalettedContainer`, checks the expected size, and recalculates its initial non-air count.
- Block-state access is coordinate-based. The flattening formula remains the vanilla `(y << 8) | (z << 4) | x` through the strategy.
- The currently applicable cached counter is non-air block count. The section exposes its state container, can recalculate this count, and copies its state container and cached count.
- Fluid states, fluid/random-tick counters, biome holder storage and resolution, and section serialization are deferred until those matching domain systems and codecs exist.

## Parity gate

This document records implemented scope; it does not certify total parity. The stage remains open until every behavior and structural contract within the systems above has been compared with Minecraft 26.3 and the relevant checks/build pass. Future systems remain deferred rather than being counted as current-stage defects. No later development stage may begin while a current-scope divergence remains.
