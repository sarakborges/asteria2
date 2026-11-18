# Block registry

## Scope

The registry system is implemented in Asteria. Minecraft is the behavioral and architectural reference; Minecraft block definitions, assets, names, and content data are not imported.

The registered block definitions are Asteria-owned:
- `asteria:air` is the required default/fallback block for the block registry.
- `asteria:registry_test` is a temporary block used to exercise registration and lookup.

## Model

- `ResourceLocation` identifies content with a lowercase `namespace:path` key.
- `RegistryKey<T>` identifies an entry together with the registry that owns it.
- `RegistryEntry<T>` is the stable reference to a value and exposes its runtime numeric ID.
- `Registry<T>` maintains lookups by namespaced key, runtime ID, and object identity.
- `DefaultedRegistry<T>` resolves missing keys and invalid numeric IDs to a configured default entry. Exact lookups remain available through `TryGetEntry` and `TryGetValue`.
- `BuiltInRegistries` declares the registries available to the game.
- `RegistryBootstrap` registers built-in content once and freezes the built-in registries.

## Invariants

1. A key is unique inside its registry. The same key may exist in a different registry.
2. A specific object instance can only be registered once in a registry.
3. Runtime IDs are assigned in registration order and are contiguous from zero.
4. Bootstrap order must be deterministic anywhere runtime IDs are used for network or in-memory lookup.
5. A frozen registry rejects further registration.
6. The block registry's default key must be registered before the registry can be frozen.
7. Runtime numeric IDs are not persistent identifiers. Persisted content references use namespaced keys.
8. Default fallback is not used by exact/optional lookups.

## Block types and block states

The block registry identifies block types, not every possible state of a block. A block-state definition and its immutable state combinations are a separate layer and must not be conflated with the block type's registry ID.

Block properties currently contain only a small physical baseline. Shapes, state properties, state enumeration, collision, rendering, sounds, drops, and interactions are not implemented by this registry task.

## Startup

The main scene calls `RegistryBootstrap.Initialize()`. The built-in air block is registered first, followed by the test block. The registry is then frozen. Startup resolves the test block by name and verifies that an unknown key falls back to `asteria:air`.
