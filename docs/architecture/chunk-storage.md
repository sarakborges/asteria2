# Chunk storage and vertical height access

## Minecraft 26.3 reference

This increment follows the storage-related portion of `ChunkAccess`, the complete `LevelHeightAccessor` contract, and the ordinary block-state read path from `LevelChunk.getBlockState`, while keeping missing game domains out of Asteria.

- `LevelHeightAccessor` defines inclusive build-height bounds, vertical section bounds, and block/section-to-array-index conversion.
- `Chunk` retains a `ChunkPos`, a height accessor, and a vertical array of `ChunkSection` instances.
- `BlockStateContainerFactory` is the block-state-only part of Minecraft's `PalettedContainerFactory`. It supplies the default block state and palette strategy used to create missing empty sections.

## Ported behavior

- The section array length is computed from `GetSectionsCount()`.
- A supplied section array is copied only when its length exactly matches the required length. On a mismatch, the input is ignored and the chunk is initialized with empty sections. Vanilla logs a warning for this case; Asteria has no logging abstraction in the content layer yet.
- Every missing section is created independently, with the default air state in a zero-bit palette.
- `GetSections()` exposes the backing array, matching `ChunkAccess.getSections()`; callers must not insert null entries.
- `GetSection(index)` uses direct section-array indexing.
- `GetHighestFilledSectionIndex()` scans from the top of the array and returns `-1` when every section contains only air.
- `GetHighestSectionPosition()` retains the deprecated vanilla fallback: minimum build Y for an all-air chunk, otherwise the world block Y at the highest non-air section.
- `IsYSpaceEmpty(start, end)` clamps to build bounds and checks sections in 16-block increments, preserving the reference implementation.
- `GetBlockState(BlockPos)` maps Y to a section-array index, returns the factory's default state when the index is outside the array or the section contains only air, and otherwise reads the section using `x & 15`, `y & 15`, and `z & 15`. Like `LevelChunk.getBlockState`, this storage lookup does not validate that X/Z belong to the chunk.

## Mutation boundary

The chunk-level `setBlockState` method is deliberately not exposed yet. In Minecraft 26.3, `LevelChunk.setBlockState` is not just a palette write: it also updates heightmaps, light-engine section status, sky-light sources, block entities, and world neighbor/place effects. Asteria currently has no complete Level/world, heightmap, lighting, or block-entity models to support those side effects. `ChunkSection.SetBlockState` remains the lower-level section-storage primitive; it is not presented as a complete chunk mutation operation.

The read path also omits Minecraft's debug-level override and crash-report wrapping because Asteria's `Chunk` has no `Level` or crash-reporting context.

## Explicitly deferred

This is not yet a full `ChunkAccess`, `ProtoChunk`, or `LevelChunk` implementation. It does not add block entities, entities, heightmaps, structures, post-processing, inhabited time, saved/unsaved lifecycle, chunk status, ticks, light sources, fluid state, biome storage, codecs, NBT, or networking. Those types and systems are deferred until their domain models exist.

The block-state-only factory intentionally does not manufacture a biome holder or a dummy biome palette.

## Reference source

- [LevelChunk — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunk/)
- [ChunkAccess — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/chunkaccess/)
- [LevelHeightAccessor — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelheightaccessor/)
- [LevelChunkSection — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunksection/)
- [PalettedContainerFactory — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/palettedcontainerfactory/)
