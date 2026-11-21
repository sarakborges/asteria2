# Chunk storage, height access, and heightmaps

## Minecraft 26.3 reference

This increment follows the storage-related portion of `ChunkAccess`, the complete `LevelHeightAccessor` contract, the ordinary block-state read path from `LevelChunk.getBlockState`, and the non-air world-surface heightmap algorithm. Missing game domains remain out of Asteria.

- `LevelHeightAccessor` defines inclusive build-height bounds, vertical section bounds, and block/section-to-array-index conversion.
- `Chunk` retains a `ChunkPos`, a height accessor, and a vertical array of `ChunkSection` instances.
- `BlockStateContainerFactory` is the block-state-only part of the palette factory. It supplies the default block state and palette strategy used to create missing empty sections.
- `Heightmap` stores 256 first-available Y values in packed storage, with a caller-supplied block-state predicate.

## Ported behavior

- The section array length is computed from `GetSectionsCount()`.
- A supplied section array is copied only when its length exactly matches the required length. On a mismatch, the input is ignored and the chunk is initialized with empty sections. Vanilla logs a warning for this case; Asteria has no logging abstraction in the content layer yet.
- Every missing section is created independently, with the default air state in a zero-bit palette.
- `GetSections()` exposes the backing array, matching `ChunkAccess.getSections()`; callers must not insert null entries.
- `GetSection(index)` uses direct section-array indexing.
- `GetHighestFilledSectionIndex()` scans from the top of the array and returns `-1` when every section contains only air.
- `GetHighestSectionPosition()` retains the deprecated vanilla fallback: minimum build Y for an all-air chunk, otherwise the world block Y at the highest non-air section.
- `IsYSpaceEmpty(start, end)` clamps to build bounds and checks sections in 16-block increments.
- `GetBlockState(BlockPos)` maps Y to a section-array index, returns the factory's default state when the index is outside the array or the section contains only air, and otherwise reads the section using `x & 15`, `y & 15`, and `z & 15`.
- `Heightmap.CreateWorldSurface` uses the reference `!state.isAir()` predicate. Its priming pass scans each local X/Z column downward from the highest non-empty section; its incremental update advances new top blocks and rescans a column when its current top block is removed.
- Height values store the first available Y (one above the highest occupied block) relative to minimum build Y. With height 384, each entry uses 9 bits and all 256 columns occupy 37 packed 64-bit words.
- Heightmap packed storage reuses Asteria's non-straddling fixed-width storage, matching the reference SimpleBitStorage packing layout.

## Predicate boundary

Minecraft 26.3 defines six heightmap types. The two world-surface variants use the non-air predicate and are supported by this increment. The other four depend on block tags, fluid state, and leaf classification, which Asteria does not yet model. They are deliberately not represented by approximations based on collision or occlusion flags.

The heightmap is currently a standalone chunk-bound storage object. Chunk ownership, the vanilla heightmap type map, lazy priming, and serialized-data recovery will be added when the world/chunk lifecycle exists. `ChunkSection.SetBlockState` remains a low-level storage operation; it does not automatically notify a heightmap.

## Mutation boundary

The chunk-level `setBlockState` method is deliberately not exposed yet. In Minecraft 26.3, `LevelChunk.setBlockState` also updates heightmaps, light-engine section status, sky-light sources, block entities, and world neighbor/place effects. Asteria currently has no complete Level/world, lighting, or block-entity models to support those side effects.

## Explicitly deferred

This is not yet a full `ChunkAccess`, `ProtoChunk`, or `LevelChunk` implementation. It does not add block entities, entities, heightmap type tags, structures, post-processing, inhabited time, saved/unsaved lifecycle, chunk status, ticks, light sources, fluid state, biome storage, codecs, NBT, or networking. Those types and systems are deferred until their domain models exist.

The block-state-only factory intentionally does not manufacture a biome holder or a dummy biome palette.

## Reference source

- [Heightmap — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/heightmap/)
- [LevelChunk — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunk/)
- [ChunkAccess — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/chunkaccess/)
- [LevelHeightAccessor — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelheightaccessor/)
- [LevelChunkSection — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunksection/)
