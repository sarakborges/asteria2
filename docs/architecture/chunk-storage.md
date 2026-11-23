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
- `HeightmapType` models all six reference variants, preserving IDs, serialized names, client visibility, world-generation retention, and the out-of-range ID-zero fallback. `MOTION_BLOCKING` tests `blocks_motion_in_heightmap` or a non-empty fluid state; `MOTION_BLOCKING_NO_LEAVES` tests `blocks_motion_in_heightmap_no_leaves` or a non-empty fluid state.
- `ChunkSection` stores both the 16³ block-state palette and the 4³ (`BIOME_CONTAINER_BITS = 2`) biome-holder palette. `GetNoiseBiome` reads quart coordinates; `FillBiomesFromNoise` recreates the palette, resolves all 64 cells with world quart coordinates and swaps in the filled container. `GetBiomes` exposes a read-only container view, and `Copy` duplicates both palettes.
- The biome container uses `PalettedContainerStrategy<Holder<Biome>>.CreateForBiomes` and the stable holder-ID adapter from the built-in biome registry. `BiomeContainerFactory` captures the registry's global ID width after bootstrap.
- `ChunkSection` derives `FluidState` from its stored `BlockState`. It tracks `fluidCount` and `tickingFluidCount` separately, exposes `HasFluid`, `IsRandomlyTickingFluids` and `GetFluidState`, and aggregates block/fluid random ticking. Fluid counts are updated only for non-air block states, as in `LevelChunkSection`.
- `Chunk` owns heightmaps by type. `GetOrCreateHeightmapUnprimed`, `HasPrimedHeightmap`, and `GetHeight` follow `ChunkAccess` map-presence and lazy-priming behavior.
- `PrimeHeightmaps(types)` creates requested maps and invokes one shared top-down scan, matching `Heightmap.primeHeightmaps` rather than rescanning each column separately for every map.
- Heightmap type IDs, serialization names, client visibility, and post-world-generation retention flags follow the reference metadata for all six variants.

## Mutation boundary

The chunk-level `setBlockState` method is deliberately not exposed yet. In Minecraft 26.3, `LevelChunk.setBlockState` also updates the four live heightmaps, light-engine section status, sky-light sources, block entities, and world neighbor/place effects. Asteria currently has no complete Level/world, lighting, complete fluid simulation, or block-entity models to support those side effects. `ChunkSection.SetBlockState` remains the lower-level section-storage primitive; it is not presented as a complete chunk mutation operation.

The read path also omits Minecraft's debug-level override and crash-report wrapping because Asteria's `Chunk` has no `Level` or crash-reporting context.

## Explicitly deferred

- Flowing-fluid simulation, water/lava and world-dependent fluid behavior. Fluid-sensitive heightmap predicates are now implemented; there is no separate fluid palette because fluid states are obtained from block states, as in the reference.
- Biome special effects, mob spawn settings, biome generation features/carvers, position-dependent temperature noise modifiers and climate/weather queries.
- Complete level lifecycle, chunk source, lighting engine, sky-light sources and block entities.
- Heightmap persistence codecs.
- Full `ProtoChunk` and `LevelChunk` behavior.

The current Biome model covers the registry-backed climate payload (precipitation flag, base temperature, temperature-modifier identifier and downfall), plus the built-in Plains and Desert definitions. BiomeResolver and the section palette path are implemented. The remaining biome effects/spawn/worldgen/noise layers stay deferred until their own dependencies are ported. Packet FriendlyByteBuf read/write APIs are deliberately omitted because multiplayer/network play is outside the project scope; local save/load serialization remains a separate requirement.

## Reference source

- [LevelChunk — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunk/)
- [ChunkAccess — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/chunkaccess/)
- [Heightmap — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/heightmap/)
- [LevelHeightAccessor — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelheightaccessor/)
- [LevelChunkSection — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunksection/)
- [Biome — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biome/)
- [BiomeResolver — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biomeresolver/)

## Heightmap raw-data loading

- `Chunk.SetHeightmap(type, data)` creates or reuses the map and delegates to `Heightmap.SetRawData`.
- When the supplied word count matches the map's backing storage, all words are copied into the existing backing array. The caller's array is not retained.
- When the word count differs, the serialized data is discarded and that heightmap type is primed again from the owning chunk, as in Minecraft 26.3. No partial copy is performed.
- The backing array returned by `GetRawData()` intentionally remains mutable, matching the reference storage accessor.


## Chunk lifecycle status metadata — Minecraft Java 26.3

- Portados os dez `ChunkStatus` em ordem de registro: `empty`, `structure_starts`, `structure_references`, `biomes`, `terrain`, `features`, `initialize_light`, `light`, `spawn` e `full`.
- `ChunkType` distingue os nove primeiros estágios como `PROTOCHUNK` e o estágio final como `LEVELCHUNK`.
- `heightmapsAfter` mantém os mesmos dois conjuntos compartilhados da referência: dois heightmaps de worldgen até `biomes` e quatro heightmaps finais a partir de `terrain`.
- A registry `chunk_status` é parte do root registry e fornece o codec by-name. O metadado não executa tarefas: `ChunkStep` e `ChunkPyramid` ainda são os responsáveis pelo pipeline e por suas dependências espaciais.

## Chunk dependencies — Minecraft Java 26.3

- Portada a classe `ChunkDependencies`, que armazena uma lista imutável de status indexada por distância Chebyshev a partir do chunk central.
- `Size` e `GetRadius` expõem a largura da janela; janela vazia tem raio `-1`.
- `GetRadiusOf` inverte as garantias por distância: retorna o último anel no qual o status consultado, ou um status posterior, está garantido; retorna `-1` se nenhum anel o garante.
- O teste reproduz a janela acumulada do estágio `FULL` da pirâmide de geração 26.3: radius 11, 23×23 chunks, com BIOMES até raio 3, TERRAIN até raio 2 e FEATURES até raio 1.
- Isso implementa apenas a estrutura de dependências; as tabelas `ChunkStep`/`ChunkPyramid` e a execução assíncrona permanecem pendentes.

## StaticCache2D — Minecraft Java 26.3

- Portada `StaticCache2D<T>` como cache retangular com limites inferiores inclusivos e superiores exclusivos.
- `Create` inicializa todos os valores em ordem X externa/Z interna; `Map` cria outro cache com os mesmos limites; `ForEach` percorre a ordem linear do armazenamento.
- `Contains`, `Get`, indexação X-major/Z-minor e diagnóstico de acesso fora da área seguem o contrato 26.3.
- O teste cobre inicialização, ordem de iteração, transformação, limites, exceção e cache de raio zero. Essa estrutura será usada na montagem das dependências espaciais do scheduler.
