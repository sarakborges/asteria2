# Block registry and block states

## Scope

The registry and block-state systems are implemented in Asteria. Minecraft is the architectural and behavioural reference; Minecraft block definitions, assets, names, and content data are not imported.

The registered block definitions are Asteria-owned:
- `asteria:air` is the required default/fallback block for the block registry.
- `asteria:registry_test` is a temporary block used to exercise registration and block-state behaviour.

## Named registries

- `ResourceLocation` identifies content with a lowercase `namespace:path` key.
- `RegistryKey<T>` identifies an entry together with the registry that owns it.
- `RegistryEntry<T>` is the stable reference to a value and exposes its runtime numeric ID.
- `Registry<T>` maintains lookups by namespaced key, runtime ID, and object identity.
- `DefaultedRegistry<T>` resolves missing keys and invalid numeric IDs to a configured default entry. Exact lookups remain available through `TryGetEntry` and `TryGetValue`.
- `BuiltInRegistries` declares the named registries available to the game.
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

## Current test content

- `asteria:air`: no state properties, one possible state; default block-registry fallback.
- `asteria:registry_test`: `powered=false|true` and `stage=0..2`, producing six possible states. Its explicit default state is `powered=false,stage=1`.

The main scene runs startup assertions for default values, state count, state transitions, state instance reuse, global ID round-trip, invalid value rejection, default-block fallback, and registry freezing.

## Not implemented yet

This stage does not implement block collision/outline/occlusion shapes, fluid states, block entities, rendering/model lookup, sounds, drops, loot, update-shape propagation, neighbour caches, random ticking, or persistence codecs. These systems should consume the block-state model rather than duplicate it.
