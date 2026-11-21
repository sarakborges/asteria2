# Chunk storage and heightmaps

## Minecraft 26.3 reference

This increment follows the storage-related portion of `ChunkAccess`, the `LevelHeightAccessor` contract, the ordinary block-state read path from `LevelChunk.getBlockState`, and the heightmap lifecycle.

- `LevelHeightAccessor` defines inclusive build-height bounds, vertical section bounds, and block/section-to-array-index conversion.
- `Chunk` retains a `ChunkPos`, a height accessor, and a vertical array of `ChunkSection` instances.
- Supplied section arrays are copied only when their length matches the section count; missing entries become distinct empty sections.
- `GetHighestFilledSectionIndex()` scans from the top of the array and returns `-1` when every section contains only air.
- `GetHighestSectionPosition()` retains the deprecated vanilla fallback: minimum build Y for an all-air chunk, otherwise the world block Y at the highest non-air section.
- `IsYSpaceEmpty(start, end)` clamps to build bounds and checks sections in 16-block increments.
- `GetBlockState(BlockPos)` maps Y to a section-array index, returns the built-in air state when the index is outside the array or the section contains only air, and otherwise reads the section using `x & 15`, `y & 15`, and `z & 15`. Like `LevelChunk.getBlockState`, this storage lookup does not validate that X/Z belong to the chunk.
- `HeightmapType` currently models the four block-state/block-tag-only variants: world surface and ocean floor, including their world-generation variants. The fluid-aware motion-blocking variants remain pending until the relevant fluid/block predicates are ported.
- `ChunkSection` derives `FluidState` from its stored `BlockState`. It tracks `fluidCount` and `tickingFluidCount` separately, exposes `HasFluid`, `IsRandomlyTickingFluids` and `GetFluidState`, and aggregates block/fluid random ticking. Fluid counts are updated only for non-air block states, as in `LevelChunkSection`.
- `Chunk` owns heightmaps by type. `GetOrCreateHeightmapUnprimed`, `HasPrimedHeightmap`, and `GetHeight` follow `ChunkAccess` map-presence and lazy-priming behavior.
- `PrimeHeightmaps(types)` creates requested maps and invokes one shared top-down scan, matching `Heightmap.primeHeightmaps` rather than rescanning each column separately for every map.
- Heightmap type IDs, serialization names, client visibility, and post-world-generation retention flags follow the reference metadata for the supported variants.

## Mutation boundary

The chunk-level `setBlockState` method is deliberately not exposed yet. In Minecraft 26.3, `LevelChunk.setBlockState` also updates the four live heightmaps, light-engine section status, sky-light sources, block entities, and world neighbor/place effects. Asteria currently has no complete Level/world, lighting, complete fluid simulation, or block-entity models to support those side effects. `ChunkSection.SetBlockState` remains the lower-level section-storage primitive; it is not presented as a complete chunk mutation operation.

The read path also omits Minecraft's debug-level override and crash-report wrapping because Asteria's `Chunk` has no `Level` or crash-reporting context.

## Explicitly deferred

- Flowing-fluid simulation and fluid-sensitive heightmap variants. Core `FluidState` and section fluid counts now exist; there is no separate fluid palette because fluid states are obtained from block states, as in the reference.
- Biome model and biome storage in chunk sections.
- Complete level lifecycle, chunk source, lighting engine, sky-light sources and block entities.
- Heightmap persistence codecs and warning-level diagnostics for malformed serialized data.
- Full `ProtoChunk` and `LevelChunk` behavior.

The current factory creates block-state storage only; the section delegates fluid lookup through each block state. Biome storage and its holder/container factory path must be introduced before biome section APIs are ported.

## Reference source

- [LevelChunk — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunk/)
- [ChunkAccess — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/chunkaccess/)
- [Heightmap — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/heightmap/)
- [LevelHeightAccessor — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelheightaccessor/)
- [LevelChunkSection — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunksection/)

## Heightmap raw-data loading

- `Chunk.SetHeightmap(type, data)` creates or reuses the map and delegates to `Heightmap.SetRawData`.
- When the supplied word count matches the map's backing storage, all words are copied into the existing backing array. The caller's array is not retained.
- When the word count differs, the serialized data is discarded and that heightmap type is primed again from the owning chunk, as in Minecraft 26.3. No partial copy is performed.
- The backing array returned by `GetRawData()` intentionally remains mutable, matching the reference storage accessor.

