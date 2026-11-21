# Discrete voxel occupancy

## Minecraft 26.3 reference

The first discrete storage increment ports the occupancy substrate of the shape system:

- `DiscreteVoxelShape` stores cell dimensions in X/Y/Z order and exposes dimensions, bounds-checked wide occupancy and per-axis first/last occupied cell queries.
- `BitSetDiscreteVoxelShape` stores occupancy in a flat bit array using `(x * ySize + y) * zSize + z`, with cached minimum and exclusive maximum occupied bounds.
- Copy construction duplicates storage and reconstructs the cached bounds through the source shape's first/last methods.
- `WithFilledBounds` fills half-open cell ranges and retains the supplied cached bounds.
- `IsInterior` requires the cell and its six face-adjacent cells to be occupied, with the center cell strictly inside the grid boundary.
- `ForAllBoxes` uses the reference Y/X/Z traversal and greedy Z-strip, X-span, then Y-span merging routine. Unmerged mode emits one box per occupied cell.
- `BooleanOp` contains the sixteen two-input truth functions used by the later set-algebra layer.
- Coordinate sequences now have a read-only `DoubleList` abstraction, plus uniform `CubePointRange` and offset views.
- `IndexMerger` has the four 26.3 strategies: identical coordinate lists, separated ranges (including operand swap), indirect merged coordinates with the `1e-7` duplicate tolerance, and uniform subdivision grids selected by least common multiple.
- `IndexMergers.Create` preserves the source decision order and the `cost * lcm <= 256` threshold.
- `SymmetricGroup3` ports the six axis permutations, Cayley table and inverse table. `OctahedralGroup` ports all 48 signed permutations, the source Cayley/inverse tables, serialized names, block-rotation aliases, direction and front/top rotations, and the 3×3 transformation matrix.
- `DiscreteVoxelShape.Rotate` applies the signed permutation to grid dimensions and occupied cells, including the negative-axis coordinate fixup. Each transform is tested against its inverse.

## Continuous shapes and Boolean joins

- `VoxelShape` now provides bounds, per-axis min/max, coordinate-aware min/max, move, optimize, merged box traversal, AABB materialization and closest-point lookup.
- `CubeVoxelShape` uses uniform `CubePointRange` coordinates and the exact clamped floor-based coordinate-to-cell mapping. `ArrayVoxelShape` preserves explicit lists and validates that every axis has exactly dimension-plus-one coordinates.
- `Shapes` provides canonical empty/block shapes, `box`/`create` factories, the 1/2/4/8-part grid-fit test, `INFINITY`, `join`, `joinUnoptimized`, `joinIsNotEmpty`, and variadic OR.
- `joinUnoptimized` builds X/Y/Z mergers in sequence, fills the output grid via `BooleanOp`, and selects cube storage only when all three mergers are discrete-cube mergers. `join` then optimizes by rebuilding from merged boxes.
- `AxisCycle` supplies identity/forward/backward axis and coordinate permutations used by coordinate-aware shape bounds.

## Deferred dependencies

- Edge/face extraction, `SliceShape`, block-hit-result clipping, collision contexts, swept collision and renderer integrations still require their respective models; no placeholders were introduced.
- Edge/face extraction, `SliceShape`, block-hit-result clipping, collision contexts, swept collision and renderer integrations still require their respective models; no placeholders were introduced.

## Reference source

- [OctahedralGroup — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/com/mojang/math/octahedralgroup/)
- [SymmetricGroup3 — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/com/mojang/math/symmetricgroup3/)
- [FrontAndTop — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/frontandtop/)
- [DiscreteVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/discretevoxelshape/)
- [BitSetDiscreteVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/bitsetdiscretevoxelshape/)
- [BooleanOp — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/booleanop/)
- [CubePointRange — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/cubepointrange/)
- [IndexMerger — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/indexmerger/)
- [IdenticalMerger — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/identicalmerger/)
- [NonOverlappingMerger — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/nonoverlappingmerger/)
- [IndirectMerger — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/indirectmerger/)
- [DiscreteCubeMerger — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/discretecubemerger/)
- [VoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/voxelshape/)
- [CubeVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/cubevoxelshape/)
- [ArrayVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/arrayvoxelshape/)
- [Shapes — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/shapes/)
