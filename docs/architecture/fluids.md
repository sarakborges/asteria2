# Fluid domain

## Minecraft 26.3 reference

The current increment ports the first functional layer of the fluid subsystem:

- A generic `StateHolder<TOwner, TState>` and `StateDefinition<TOwner, TState>` are shared by block and fluid states. State values use the same identity-based property lookup and precomputed neighbor transitions.
- `Fluid` owns its state definition, default state and built-in registry holder. The model exposes the source, amount, own-height, empty and random-tick contracts needed by `FluidState`.
- `FluidState` delegates source checks, emptiness, random ticking, amount and own height to its owner. `IsSourceOfType` uses owner identity before checking source state; `IsFull` compares amount with 8.
- `EmptyFluid` reports empty, non-source, amount zero and own height zero.
- `FluidIds` contains the five reference identifiers. The runtime registry currently registers only `EMPTY`; water and lava types are not registered prematurely.
- The built-in bootstrap registers each fluid and adds all of its possible states to the fluid-state ID map.
- `Block.GetFluidState` defaults to the empty state; individual blocks can override it. `BlockState.GetFluidState` delegates to the block.
- `ChunkSection` exposes fluid-state lookup and tracks total non-empty fluids and randomly-ticking fluids separately. Its aggregate random-ticking flag combines block and fluid counts.

## Explicitly deferred

This is not full fluid parity. The following reference behavior still requires dependency domains that are not yet present:

- `FlowingFluid` update ordering, source conversion, neighbor inspection, slope-distance search, spread maps, scheduled updates and tick delays.
- Water/lava source and flowing variants, their state definitions, block conversion and their environment-specific behavior.
- World interfaces (`BlockGetter`, `LevelReader`, `Level`, server level) and scheduled tick containers.
- Fluid flow algorithms still need world access, scheduled ticks and collision/renderer integration. `Vec3`, `AABB`, discrete occupancy, coordinate mergers and the core `VoxelShape`/`Shapes` Boolean geometry are now present.
- `MOTION_BLOCKING` and `MOTION_BLOCKING_NO_LEAVES` heightmaps now consume the corresponding block tags plus non-empty fluid states, matching the 26.3 predicates.
- Legacy-block conversion for all fluid types, buckets/items, particles, sounds, entities and block effects.
- Fluid tags and serialization codecs.

The fluid test implementation is not registered in runtime content. It exists only to verify generic fluid-state generation and the section counter behavior. No water/lava placeholders or fake world APIs were introduced.

## Reference source

- [FluidState — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/FluidState/)
- [Fluids — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/Fluids/)
- [FluidIds — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/FluidIds/)
- [EmptyFluid — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/EmptyFluid/)
- [FlowingFluid — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/FlowingFluid/)
- [LevelChunkSection — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/LevelChunkSection/)
