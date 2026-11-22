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
