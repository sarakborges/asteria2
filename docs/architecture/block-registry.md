# Block registry, block states, and chunk-section storage

## Scope

Registry, block-state, palette, and section-storage systems are implemented in Asteria. Minecraft is the architectural and behavioural reference; Minecraft block definitions, assets, names, and content data are not imported.

The registered block definitions are Asteria-owned:
- `asteria:air` is the required default/fallback block for the block registry.
- `asteria:registry_test` is a temporary block used to exercise registration and block-state behaviour.

## Named registries and ID maps

- `ResourceLocation` identifies content with a lowercase `namespace:path` key.
- `RegistryKey<T>` identifies an entry together with the registry that owns it.
- `RegistryEntry<T>` is the stable reference to a value and exposes its runtime numeric ID.
- `Registry<T>` maintains lookups by namespaced key, runtime ID, and object identity.
- `DefaultedRegistry<T>` resolves missing keys and invalid numeric IDs to a configured default entry. Exact lookups remain available through `TryGetEntry` and `TryGetValue`.
- `IIdMap<T>` provides frozen runtime ID-to-value and value-to-ID lookup to palette storage.
- `BuiltInRegistries` declares the named registries and ID maps available to the game.
- `RegistryBootstrap` registers built-in content, assigns global block-state IDs, and freezes the registries.

## Block states

- A `Block` owns a `StateDefinition`.
- A `StateProperty<T>` describes a named, typed property and its finite ordered set of allowed values.
- `BooleanProperty`, `IntegerProperty`, and `EnumProperty<TEnum>` cover common property types.
- `StateDefinitionBuilder` collects the properties and the block's default value for each property.
- `StateDefinition` sorts properties by name, materializes the Cartesian product of their allowed values, and exposes the precomputed states.
- `BlockState` is immutable. `SetValue` and `Cycle` return an existing state instance from the owning definition rather than allocating a new state.
- `BlockStateIdMap` assigns one global insertion-ordered runtime ID to every possible state of every registered block.

A block with no properties has exactly one possible state. A block with properties has the product of the value counts as its possible-state count.

## Paletted storage

- `PackedBitStorage` packs fixed-width values into 64-bit words. Entries never cross a word boundary; unused bits in each word remain padding, matching the vanilla storage layout.
- `PalettedContainer<T>` maps each of its fixed positions to a value through a palette.
- Uniform containers use a zero-bit single-value palette and allocate no packed array.
- Block-state local palettes start at four bits and use linear lookup.
- When the local palette widens above four bits, the implementation switches to hash-map lookup.
- Local palettes grow up to eight bits. Every width increase repacks the existing storage without changing values.
- When the local palette exceeds its eight-bit capacity, the container switches to a direct/global palette whose stored integers are the frozen global state IDs.
- `MaybeHas` inspects the palette rather than scanning every position. It may return true for values that were previously in the local palette but are no longer present at any position. In global mode it conservatively returns true.
- The ID map must be frozen before a container can be created. A container is designed for one writer at a time.

## Chunk sections

- `ChunkSection` represents exactly 16×16×16 = 4096 block states.
- The linear index is `x | (z << 4) | (y << 8)`.
- Each section owns a `PalettedContainer<BlockState>`.
- `NonAirBlockCount` is updated incrementally when states change between air and non-air blocks; `HasOnlyAir` is a constant-time query.
- The current implementation tracks only non-air blocks. Fluid and random-ticking counters are not implemented yet.

## Invariants

1. A key is unique inside its registry. The same key may exist in a different registry.
2. A specific object instance can only be registered once in a registry.
3. Runtime registry IDs are assigned in registration order and are contiguous from zero.
4. Bootstrap order must be deterministic anywhere runtime IDs are used for network or in-memory lookup.
5. A frozen registry rejects further registration.
6. The block registry's default key must be registered before the registry can be frozen.
7. Runtime numeric IDs are not persistent identifiers. Persisted content references use namespaced keys.
8. Default fallback is not used by exact/optional lookups.
9. A block's state properties must be unique by both property instance and property name.
10. Every state combination is generated once, before the block-state ID map is frozen.
11. A state property can only be used with the definition that declared that exact property instance.
12. State property changes must resolve to precomputed state instances.
13. The block-state global ID map follows block registry order, followed by each block's deterministic state order.
14. Palette containers may only use a frozen ID map, so IDs cannot shift underneath packed storage.
15. Palette transitions preserve every existing position's value.
16. Chunk-section coordinates are bounded to [0, 15] on every axis, and all writes update the non-air count exactly once.

## Current test content

- `asteria:air`: no state properties, one possible state; default block-registry fallback and air classification.
- `asteria:registry_test`: `powered=false|true` and `stage=0..2`, producing six possible states. Its explicit default state is `powered=false,stage=1`.

The checks also use a temporary in-memory block/state map to exercise linear-to-hash local-palette growth and direct/global conversion without adding hundreds of dummy blocks to game content.

## Not implemented yet

This stage does not implement biome palettes, fluid-state palettes, block collision/outline/occlusion shapes, block entities, rendering/model lookup, sounds, drops, loot, update-shape propagation, neighbour caches, random ticking, chunk serialization, compression, or persistence codecs. These systems should consume the block-state and section-storage models rather than duplicate them.
