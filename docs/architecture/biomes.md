# Biome data and section palette

## Minecraft 26.3 reference

Implemented runtime slice:

- Registry-backed biome climate settings and stable holders.
- Biome special effects: required water color, optional foliage/dry-foliage/grass overrides and grass color modifier.
- `BiomeSpecialEffects.Builder` requires water color and preserves the reference missing-water failure.
- Grass modifier serialized names and exact integer behavior for NONE and DARK_FOREST.
- SWAMP grass color threshold and output colors use the fixed-seed BIOME_INFO_NOISE sampler (seed 2345); the shared two-dimensional noise dependency is implemented.
- Each `Biome` stores an immutable typed `EnvironmentAttributeMap`, defaults to the shared empty map, and exposes it through `GetAttributes`; modifiers are applied to the value supplied by the lower-priority environment layer.
- A 4x4x4 quart-resolution biome palette in each chunk section.

Base overworld Plains and Desert use the reference normal water color 4159204.

## Deferred dependencies

- Environment attribute source layering/resolution across dimensions, biome position, timelines and weather; the biome-local map is now stored but is not yet consumed by a world-level resolver.
- MobSpawnSettings and entity-type/spawn-cost domains.
- BiomeGenerationSettings, carver and placed-feature registries.
- Position-dependent frozen temperature modification and its noise dependencies.
- Local save/load codecs for biome data and section palettes.

Network packet codecs are out of scope. Local disk persistence remains in scope.

## Reference source

- [Biome — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biome/)
- [BiomeSpecialEffects — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biomespecialeffects/)
- [OverworldBiomes — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/data/worldgen/biome/overworldbiomes/)
- [LevelChunkSection — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunksection/)
- [PalettedContainer — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/palettedcontainer/)

## Temperature and precipitation

- The `FROZEN` temperature modifier combines the three-layer frozen-temperature noise (seed 3456) with the biome-info noise at the reference scales and thresholds.
- The altitude correction uses the separate temperature noise (seed 1234) above `seaLevel + 17`.
- `GetHeightAdjustedTemperature`, rain/snow selection, warm-enough-to-rain, cold-enough-to-snow and frozen-ocean iceberg melt threshold are implemented.
- The 1024-entry per-thread temperature cache is implemented with packed block-position keys and FIFO eviction. As in the reference, sea level is not part of the cache key; callers must treat a biome cache as scoped to a stable level configuration.
- Level-dependent freeze/snow checks remain pending because they require a real level reader, block/fluid state queries, light access and snow survival behavior.


## Interval arithmetic — Minecraft Java 26.3

- Portada a aritmética de intervalos fechados usada por Density Functions, incluindo `NaI`, `INFINITE`, criação canônica, encapsulamento, soma, subtração, produto, recíproco, divisão, min/max, clamp, abs, square, potência, log, mapeamento monotônico, lerp, sign, interseção e igualdade.
- A semântica de `NaI` usa identidade com singleton, enquanto intervalos numéricos não aceitam limites NaN. Operações de produto e interpolação preservam as regras especiais para zero e infinito da referência.
- Testes cobrem limites invertidos/NaN, ranges canônicos, aritmética com sinal, zero e infinito, potência, log, lerp, mapeamento monotônico, comparação e valores NaI.
- Essa primitiva é pré-requisito para o contrato `Noise.range()` e para os limites intervalares do grafo de Density Functions; ainda não executa geração de chunks.


## Ruído simplex 3D — Minecraft Java 26.3

- O modelo `SimplexNoise` agora oferece `Get(x,y,z)` com escala 3D, quatro gradientes de canto e ranking das coordenadas locais por tetraedro.
- `GradientNoise` centraliza a tabela de gradientes, permutação e wrap de coordenadas 2^25 usados pelas variantes Simplex e Perlin.
- A variante 2D continua disponível para o modificador de cor de bioma e as funções ambientais existentes.


## Noise contract and Perlin sampling — Minecraft Java 26.3

- The shared `Noise` contract now exposes its output interval, 2D/3D point samples and default volume traversal. `SimplexNoise` implements the contract without changing its existing 2D/3D algorithms.
- `PerlinNoise` provides gradient lattice interpolation, derivative accumulation, and the optimized volume path from the 26.3 source. Volume evaluation keeps its distinct Z/X/Y traversal and float rounding.
- The float `Mth` helpers used by Perlin use the same smoothstep curve and nested X/Y/Z lerp order as the reference.
- Multi-octave construction through `NormalNoise` and `NoiseStack.Perlin` remains a separate stage.
