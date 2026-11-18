# Block registry

## Scope

The registry system is implemented in Asteria. Minecraft is the behavioral and architectural reference; Minecraft block definitions, assets, names, and content data are not imported.

The only registered block at this stage is `asteria:registry_test`, a temporary Asteria-owned block used to exercise the bootstrap path.

## Model

- `ResourceLocation` identifies content with a lowercase `namespace:path` key.
- `RegistryKey<T>` identifies an entry together with the registry that owns it.
- `RegistryEntry<T>` is the stable reference to a value and exposes its runtime numeric ID.
- `Registry<T>` maintains lookups by namespaced key, runtime ID, and object identity.
- `BuiltInRegistries` declares the registries available to the game.
- `RegistryBootstrap` registers built-in content once and freezes the built-in registries.

## Invariants

1. A key is unique inside its registry. The same key may exist in a different registry.
2. A specific object instance can only be registered once in a registry.
3. Runtime IDs are assigned in registration order and are contiguous from zero.
4. Bootstrap order must be deterministic anywhere runtime IDs are used for network or in-memory lookup.
5. A frozen registry rejects further registration.
6. Runtime numeric IDs are not persistent identifiers. Persisted content references use namespaced keys.

## Block types and block states

The block registry identifies block types, not every possible state of a block. A block-state definition and its immutable state combinations are a separate layer and must not be conflated with the block type's registry ID.

Block properties currently contain only a small physical baseline. Shapes, state properties, state enumeration, collision, rendering, sounds, drops, and interactions are not implemented by this registry task.

## Startup

The main scene calls `RegistryBootstrap.Initialize()`. Built-in entries are registered before the registry is frozen. The test block is then resolved by its namespaced key and its runtime ID is printed as a startup sanity check.
