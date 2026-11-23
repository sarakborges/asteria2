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


## DensityBuffer — Minecraft Java 26.3

- Armazenamento linear de floats com `set/get`, `setRange`, `addTo`, `fill`, `copyFrom`, `capacity` e `size`.
- A instância criada por `createUnpooled` mantém tamanho fixo; o buffer não introduz alocação ou pool customizado.
- A implementação de `DensityVolume` e o método default `Noise.addToVolume` aguardam o contrato `BoundingBox` necessário para interseção volumétrica.


## BoundingBox — Minecraft Java 26.3

- Bounds de estrutura com coordenadas inclusivas, construção por cantos, orientação por direção, interseção, operações de encapsulamento/movimento/inflação, contenção, spans, centro e enumeração de oito vértices.
- `IntersectingChunks` enumera todos os chunks tocados pela caixa em ordem X-major/Z-minor. A implementação também expõe a caixa infinita e valores de limites individuais.
- CODEC de disco e stream codec de rede continuam pendentes (persistência adiada; rede fora do escopo).


## DensityVolume — Minecraft Java 26.3

- Volume amostrado em uma grade XYZ, com passo positivo independente por eixo e coordenadas mínimas em blocos.
- Layout linear é Y-minor, X intermediário, Z major; `IndexOfBlock` aceita somente coordenadas que pertençam ao grid, incluindo alinhamento dos passos.
- Bounding/intersection usa limites inclusivos; volumes podem representar tanto blocos contíguos quanto amostragens esparsas.


## Density sampling runtime — Minecraft Java 26.3

- `DensitySampler` exposes point and volume evaluation. Its naive volume traversal is Z-major, X-middle, Y-minor, matching the linear order of `DensityVolume`.
- `SamplerContext` owns typed user fields, a buffer arena and optional per-cache-id point/volume cells.
- `ScopedDensityBuffer` follows the acquire/restore/close lifecycle; the default global arena allocates buffers directly and has a no-op release, as in vanilla.
- The first stage of the density evaluation graph is now present; `DensityFunction` nodes and `NoiseRouter` are still pending.


## DensityFunction graph foundation — Minecraft Java 26.3

- The base density-function contract is present: coordinate-axis bit masks, sampler compilation, child rewriting, interval range, and domain-axis reporting.
- The runtime-only first node is `ConstantFunction`, including its exact interval, coordinate-independent sampler, and active-buffer fill.
- `DensityFunctions.Zero` returns the shared vanilla zero node; `Constant` creates a new node per call.
- Context and rewrite contracts are present. Operator nodes, SliceFunction, holder-reference inlining and density codecs remain separate follow-up ports; codecs are deferred with local persistence and no stand-ins are registered.


## Unary density operators — Minecraft Java 26.3

- The eleven unary operators are represented by `UnaryFunction`: abs, square, cube, sqrt, half_negative, quarter_negative, reciprocal, negate, squeeze, log and sign.
- Each operation compiles to its corresponding sampler record; both leaky-ReLU variants share the parameterized vanilla sampler. Volume sampling delegates to the child, then transforms the active buffer.
- Range calculations use the existing interval primitives, and the node inherits its child's domain-axis mask.
- Type factories and the default `DensityFunction` operator methods are now wired. Operator codecs are deferred with local persistence and not replaced with placeholders.


## Binary density operators — Minecraft Java 26.3

- Binary density nodes now support add, sub, mul, div, min and max with vanilla operand order and interval propagation.
- Sampler compilation selects the specialized constant-left/right variants and min/max range-pruning variants from the 26.3 source.
- Generic volume samplers allocate the right-hand scratch buffer from `SamplerContext` and release it when complete. Scalar mul/div retain the vanilla left-zero short-circuit; volume paths evaluate both child buffers.
- Density function type codecs remain deferred with local persistence; no operator placeholder codecs were registered.


## Lerp density function — Minecraft Java 26.3

- `LerpFunction` interpolates between first and second through an alpha density input, with exact interval propagation and the union of all three coordinate domains.
- Compilation uses the vanilla constant-first and constant-second sampler specializations; otherwise it uses the generic sampler. Point and volume paths preserve alpha 0/1 selection branches.
- Volume evaluation owns scoped scratch buffers for the non-output branches and releases them at method exit.
- Function codecs remain deferred with local persistence; no placeholder codec is registered.


## Clamp density function — Minecraft Java 26.3

- `ClampFunction` compiles its child once and clamps scalar/volume samples with the float-specific `Mth.clamp` operation.
- Range propagation uses `Interval.clamp`; the node inherits its input's domain axes and preserves identity when child rewrite does not change the input.
- Runtime constructor does not enforce min/max ordering; vanilla validates this in its codec layer, which remains deferred with local persistence.


## RangeChoice density function — Minecraft Java 26.3

- `RangeChoiceFunction` chooses the in-range branch for `input >= minInclusive && input < maxExclusive`; the upper limit is exclusive.
- The constant sampler is selected only when both output functions are constant nodes. Otherwise, the generic sampler evaluates both branches into separate buffers and selects values by input.
- The range encloses only the two output ranges; domain axes union input and both branches. Rewrite traverses all three children.
- Runtime factory is wired; codec is deferred with local persistence.


## Power density function — Minecraft Java 26.3

- `PowFunction` evaluates base raised to exponent, with range propagation through `Interval.pow` and the union of both child axis masks.
- Compilation preserves vanilla specialization precedence: constant base first; otherwise constant exponent. Absolute exponents 0.5, 1, 2 and 3 reuse square-root, identity, square and cube samplers. Negative values wrap the selected operation in reciprocal.
- General volume evaluation uses a scoped exponent buffer. No additional zero or domain short-circuit is introduced.
- Runtime factory is wired; codec remains deferred with local persistence.


## Noise density function — Minecraft Java 26.3

- `NoiseFunction` resolves its `NormalNoise` holder through the compile context and samples the resulting `Noise` implementation.
- The no-shift sampler uses the noise volume accumulation path. Shifted variants preserve the vanilla X/Z-only and X/Y/Z coordinate paths and Z/X/Y volume traversal.
- Domain axes remove Y for zero Y scale and X/Z for zero horizontal scale, then union all shift child axes.
- Noise and shifted-noise runtime factories are wired; codecs remain deferred with local persistence.


## ShiftNoiseFunction — Minecraft Java 26.3

- `ShiftA`, `ShiftB` and `Shift` preserve the vanilla coordinate factor 0.25 and output factor 4.
- ShiftA samples 2D horizontal noise through the regular NoiseFunction sampler. Shift uses the same sampler with vertical scaling enabled.
- ShiftB transposes the requested volume into a Z/X plane with the corresponding block steps, samples it once, then replicates each value along the original Y dimension using `DensityBuffer.SetRange`.
- The transposed scratch buffer is scoped to the evaluation and released. Domain axes are X/Z for ShiftA/B and X/Y/Z for Shift.


## Gradient density function — Minecraft Java 26.3

- Added `TilingMode` with clamp-to-edge, repeat and mirrored-repeat behaviors.
- `GradientFunction` maps one axis coordinate linearly between two values, preserving reversed endpoints. Repeat modes use Java floor division/modulo behavior for negative coordinates.
- Volume sampling has axis-specific paths that write ranges or planes directly, matching the 26.3 layout and avoiding general point traversal.
- `Gradient` and `YClampedGradient` runtime factories are wired. The equal-endpoint validation remains part of the deferred codec layer.


## Round density function — Minecraft Java 26.3

- `RoundFunction` implements floor, Java round, ceil and truncation toward zero with a variable multiple.
- The integer-multiple sampler is selected only when the multiple is exactly the constant 1. Other multiples use a generic sampler with a scoped scratch buffer. A zero multiple returns the input unchanged.
- Java `Math.round(float)` behavior is preserved, including ties toward positive infinity and saturation to the integer range.
- Interval propagation follows the vanilla divide → monotonic round → multiply formula. Runtime factories for round/floor/ceil/truncate are wired; codec remains deferred with local persistence.


## IntervalSelect density function — Minecraft Java 26.3

- `IntervalSelectFunction` selects the first output whose threshold is greater than the input; equality advances to the next output, and values above all thresholds select the last output.
- One threshold compiles to a two-output specialized sampler. Other threshold counts use the generic sampler and compile every output.
- Point evaluation samples only the selected output; volume evaluation samples all outputs into scoped buffers. The generic implementation releases every acquired buffer in a `finally` path if any child throws.
- Range encloses output ranges only; domain axes include the input and every output. Runtime factory is wired; count/order validation remains in the deferred codec layer.
