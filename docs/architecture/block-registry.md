# Block registry, block states, and chunk-section storage

## Reference and scope

Minecraft Java Edition 26.3 is the reference for architecture, contracts, and behavior. Asteria keeps its own block content; this port follows vanilla systems rather than substituting project-specific designs.

This stage covers only systems already introduced in Asteria: registries and holders, blocks and block states, block-state IDs, palette storage, and the block-state portion of chunk sections. Fluid, biome, data-component, network, and disk-persistence behavior are deferred until their corresponding domain systems are introduced.

## Registry and holder model

- `BuiltInRegistries.Root` is the root registry of registries. It owns the `asteria:root` identity; each child registry is registered there under its own identifier.
- Each built-in registry is registered with an ordered bootstrap loader. Bootstrap executes those loaders in declaration order before freezing the root and its child registries.
- `Registry<T>` follows `MappedRegistry<T>`: location, registry-key, value-identity, numeric-ID, and holder indexes share stable `Holder.Reference<T>` instances.
- Built-in values use the vanilla static `Registry.Register` helper overloads. These accept a concrete value subtype of the registry's declared value type; `RegisterForHolder` likewise accepts subtype values and returns the registry's base-typed reference holder.
- The root registry freezes first. Each child registry then binds all bootstrapped tags to empty, freezes, and validates its default entry when it is a `DefaultedRegistry`.
- `Count`/registry size includes standalone key placeholders. Numeric ID lookup remains separate from key lookup.
- `AsHolderIdMap` exposes registry holders through the vanilla `IdMap<Holder<T>>` view defined by the `Registry` interface.
- Registration records `RegistrationInfo` and aggregates `Lifecycle`; ordinary registration uses the built-in stable metadata.
- Registry freezing binds values, rejects unbound standalone references and unregistered intrusive holders, verifies tag binding, and publishes the bound tag set.
- Intrusive registries require an intrusive holder before registration. A block creates its intrusive holder before its state definition, as vanilla `Block` does.
- `Holder<T>` supports direct and reference variants, key/predicate/tag membership, ownership checks, unwrap/unwrap-key, registered names, and bound/unbound tag behavior. `Holder.Reference<T>` remains subclassable through its protected typed constructor and exposes public standalone/intrusive factories; the intrusive factory is deprecated.
- `HolderSet<T>` supports direct and named sets, the vanilla direct-set factories, ordered contents, membership, random selection, unwrap/unwrap-key, and serialization ownership. Direct sets compare ordered contents and derive their hash codes from those contents; named and other `ListBacked` variants retain object-identity equality. Direct-set membership uses holder equality.
- `HolderGetter<T>` exposes optional and throwing key/tag lookup plus random element selection. Registration lookups create stable standalone placeholders.
- `HolderLookup<T>` and `RegistryLookup` expose element/tag enumeration, registry key and lifecycle metadata, and vanilla-style element filtering.
- `DefaultedRegistry<T>` applies fallback semantics only to the vanilla defaulted lookup paths; exact holder and optional lookups remain exact.
- Tag reload uses a pending replacement snapshot. The cross-registry `HolderGetter.Provider` / `HolderLookup.Provider`, complete tag-loader pipeline, and serialization context are not yet introduced.

## Identifiers and resource keys

- `ResourceLocation` follows vanilla identifier parsing, custom separators, validation, ordering, path transformations, and language/debug naming helpers. Its project default namespace is `asteria` (not Minecraft's `minecraft` namespace). `ResolveAgainst` also normalizes and guards the resolved path against escaping its root.
- `RegistryKey<T>` has a private constructor and exposes the vanilla `Create`/`CreateRegistryKey` factories, typed casts, and dependent resource-key creation. `IsFor` compares only the registry identifier, independent of the root key. Its value equality preserves the observable contract of vanilla's weakly interned keys. `TagKey<T>` is a record with a public deprecated canonical constructor and a preferred `Create` factory; it retains the full registry key, compares the entire key for registry membership, and supports typed casts.

## Blocks and block states

- `BlockProperties` is a mutable fluent builder for the modeled `BlockBehaviour.Properties` values: destroy time, explosion resistance, friction, speed/jump factors, bounce restitution, fall-distance reduction, collision/occlusion, random ticking, dynamic shape, air/tool flags, lava ignition, the deprecated liquid compatibility flag, replaceability, and terrain particles. Defaults and setter behavior follow the reference contract.
- `Block` snapshots block-level values at construction: explosion resistance, friction, speed/jump factors, bounce restitution, fall-distance reduction, collision, random ticking, and dynamic-shape flags. It creates its intrusive holder before building states and calls the virtual `CreateBlockStateDefinition` extension point.
- `StateProperty<T>` exposes a name, value type, ordered possible values, parsing, serialization, typed `Property.Value` wrappers, `GetAllValues`, and overridable public hash generation. Hashing follows the reference class-identity/string formula, including ordered integer/enum value-list hashing, and `hashCode` caches its generated result.
- `BlockState.GetProperties` exposes the immutable, sorted property collection. `BlockState.GetValues` enumerates typed property/value pairs in the same state-definition order.
- `StateDefinition` sorts property names, materializes the Cartesian product in reference order, and links each state to precomputed neighbor states.
- `BlockState.SetValue`, `TrySetValue`, and `Cycle` resolve through the precomputed neighbor tables; `TryGetValue` provides the optional-value lookup path.
- Each `BlockState` snapshots the currently modeled state-level `BlockStateBase` values at construction: air, destroy speed, correct-tool requirement, occlusion, random ticking, lava ignition, liquid, replaceability, and terrain-particle behavior. Mutating the retained properties builder does not retroactively change existing states.
- Block map colors, sound/instrument data, light emission/dampening, push reaction, shape/predicate behavior, loot metadata, feature flags, and the complete state cache remain pending their corresponding domain systems.
- `Block.BLOCK_STATE_REGISTRY` is the global `IdMapper<BlockState>` equivalent. Each state receives its insertion-order ID in its constructor, as soon as `StateDefinition` materializes it; this map is independent of the built-in block registry and is not frozen with it.

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

- `ChunkSection` receives a prebuilt block-state `PalettedContainer` and recalculates its initial non-air count without imposing an additional size check on the supplied container.
- Block-state access is coordinate-based. The flattening formula remains the vanilla `(y << 8) | (z << 4) | x` through the strategy.
- The currently applicable cached counters are non-air block count and randomly-ticking block count. Both counters include only non-air block states; the ticking counter does not count an air state even if its modeled flags also mark it as randomly ticking. `IsRandomlyTickingBlocks` and `IsRandomlyTicking` expose the block portion of the reference section's ticking gate. The section exposes its state container, can recalculate both counts, and copies its state container and cached counters.
- Fluid states and their two counters, biome holder storage and resolution, and section serialization are deferred until those matching domain systems and codecs exist.

## Parity gate

This document records implemented scope; it does not certify total parity. The stage remains open until every behavior and structural contract within the systems above has been compared with Minecraft 26.3 and the relevant checks/build pass. Future systems remain deferred rather than being counted as current-stage defects. No later development stage may begin while a current-scope divergence remains.
