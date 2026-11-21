# Coordinate primitives

## Reference

The coordinate types follow the Mojang-mapped Minecraft Java 26.3 implementations:
- `Vec3i`: integer vector base used by block and section positions.
- `BlockPos`: absolute block coordinates.
- `ChunkPos`: horizontal chunk coordinates.
- `SectionPos`: section coordinates and block/section conversions.

The source files are linked at the end of this document. Asteria keeps the types in its own namespaces and does not add a dependency on the reference game.

## Structure

- `Asteria.Core.Vec3i` is the shared integer-vector base for `BlockPos` and `SectionPos`. Equality compares coordinates across derived types; comparison order is Y, then Z, then X; the hash formula follows the reference implementation.
- `Asteria.Core.BlockPos` is an immutable-by-default class. Its nested `MutableBlockPos` provides explicit mutable reuse and returns immutable values from the position-producing operations ported here.
- `Asteria.World.ChunkPos` is a value record containing X/Z chunk coordinates.
- `Asteria.Core.SectionPos` is a section-coordinate type derived from `Vec3i`.

## Packed representations

These formats are intentionally separate:

| Type | X | Y | Z |
|---|---:|---:|---:|
| `BlockPos` | 26 bits at 38 | 12 bits at 0 | 26 bits at 12 |
| `ChunkPos` | low 32 bits | — | high 32 bits |
| `SectionPos` | 22 bits at 42 | 20 bits at 0 | 22 bits at 20 |

Packed values preserve the source's truncation and signed-field decoding behavior. They are not validated against world limits when encoded.

## Coordinate semantics

- Block-to-section and block-to-chunk conversion use arithmetic right shifts by four. This is essential for negative coordinates: block `-1` belongs to section/chunk `-1`; block `-17` belongs to `-2`.
- Double coordinates are floored before conversion.
- Region coordinates use arithmetic shifts by five; region-local coordinates use the low five bits.
- Section-relative positions use a 16-bit short with X in bits 8–11, Z in bits 4–7, and Y in bits 0–3.
- Closed-box traversal advances X first, then Y, then Z, matching the reference cursor order.

## Deliberate scope boundary

This increment ports the coordinate and packing contracts needed by chunk/world construction. It does not introduce direction, rotation, vectors of floating-point positions, entity access, chunk lifecycle/status, chunk codecs, network buffers, or serialization codecs. In particular, `ChunkPos.isValid` depends on the generation/loading chunk-pyramid limits, which do not exist in Asteria yet; it is deferred rather than reimplemented with a guessed constant.

## Reference source

- [Vec3i — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/vec3i/)
- [BlockPos — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/blockpos/)
- [ChunkPos — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunkpos/)
- [SectionPos — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/sectionpos/)
- [ChunkPyramid — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/status/chunkpyramid/)
