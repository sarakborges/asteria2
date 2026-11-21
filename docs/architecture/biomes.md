# Biome data and section palette

## Minecraft 26.3 reference

This increment ports the climate/holder portion needed by LevelChunkSection and the local world-generation pipeline:

- Biome stores ClimateSettings: precipitation flag, base temperature, temperature-modifier identifier and downfall.
- The built-in defaulted biome registry contains Plains (0.8, precipitation, downfall 0.4) and Desert (2.0, no precipitation, downfall 0.0), identified by stable registry keys and holders.
- BiomeResolver resolves Holder<Biome> using world quart coordinates.
- BiomeContainerFactory creates PalettedContainer<Holder<Biome>> using the reference biome strategy: 2 bits per axis, 4x4x4 cells and a global palette backed by the registry holder ID map.
- ChunkSection stores the biome palette beside the block-state palette. GetNoiseBiome uses local quart coordinates; FillBiomesFromNoise recreates and fills all 64 entries from world quart coordinates. Section copies duplicate both palettes.
- GetBiomes returns a read-only IPalettedContainerReadOnly<Holder<Biome>> view; mutation stays behind the section's biome-fill path.

## Explicitly deferred

This is a deliberately partial Biome model, not a placeholder. Its climate data and holder identity are real inputs for local world generation, but the following source domains have not yet been ported:

- BiomeSpecialEffects and environmental attributes / local rendering colors.
- MobSpawnSettings and entity-type/spawn-cost domains.
- BiomeGenerationSettings, carver and placed-feature registries.
- Position-dependent frozen temperature modification and the noise dependencies it uses.
- Local save/load codec for biome data and section palettes.

FriendlyByteBuf packet read/write methods are not included: multiplayer/network play is explicitly outside Asteria's scope. Local disk persistence remains in scope and will be implemented independently of network packet IO.

## Reference source

- [Biome — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biome/)
- [BiomeResolver — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biomeresolver/)
- [LevelChunkSection — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/levelchunksection/)
- [PalettedContainer — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/palettedcontainer/)
