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
- `ForAllEdges` preserves the sector-parity test and optional contiguous-edge merge; `ForAllFaces` emits boundary transitions in all three axis cycles.
- `AxisCycle` includes axis-specific integer and double coordinate selection, along with axis and value-cycle transforms matching the reference.

## Continuous shapes and Boolean joins

- `VoxelShape` provides bounds, per-axis min/max, coordinate-aware min/max, move, optimize, merged box and edge traversal, AABB materialization, closest-point lookup, cached face extraction, per-axis AABB collision clipping, and segment clipping to `BlockHitResult`.
- `SliceShape` uses `SubShape` to restrict occupancy to a one-cell-thick plane while retaining the original in-plane coordinate lists. Empty and full-cube results canonicalize to `Shapes.Empty()` and `Shapes.Block()`.
- `VoxelShape.ForAllEdges` maps the discrete edge traversal back to the continuous axis coordinate lists. `VoxelShape.Collide` ports the reference swept-AABB axis scan, including transverse-range clipping, axis-cycle transforms and the `1e-7` contact epsilon.
- `VoxelShape.Clip` checks the reference inside-point fast path (`0.001` segment offset) and otherwise delegates to the multi-AABB ray clip. `AABB.Clip(IEnumerable<AABB>, ...)` selects the nearest face intersection and produces `BlockHitResult`.
- `CubeVoxelShape` uses uniform `CubePointRange` coordinates and the exact clamped floor-based coordinate-to-cell mapping. `ArrayVoxelShape` preserves explicit lists and validates that every axis has exactly dimension-plus-one coordinates.
- `Shapes` provides canonical empty/block shapes, `box`/`create` factories, the 1/2/4/8-part grid-fit test, `INFINITY`, `join`, `joinUnoptimized`, `joinIsNotEmpty`, variadic OR, shape equivalence, discrete/continuous octahedral rotations, axis/direction rotation tables, collection collision and block/merged/volume face occlusion.
- `joinUnoptimized` builds X/Y/Z mergers in sequence, fills the output grid via `BooleanOp`, and selects cube storage only when all three mergers are discrete-cube mergers. `join` then optimizes by rebuilding from merged boxes.
- `Shapes.Rotate` transforms discrete occupancy and continuous coordinates around an arbitrary pivot, preserving the source signed-axis permutation and coordinate-list reversal.
- `Shapes.BlockOccludes`, `MergedFaceOccludes` and `FaceShapeOccludes` use the reference face slicing and Boolean difference algorithms. `Shapes.Collide` applies candidate shapes in sequence and returns the clipped movement.
- `Shapes.Rotate` transforms both discrete occupancy and coordinate lists around an arbitrary rotation point; `FlipAxisIfNeeded` reverses/flips coordinate order exactly when the signed axis permutation requires it.
- `Shapes.BlockOccludes`, `MergedFaceOccludes` and `FaceShapeOccludes` use the source face-slice/Boolean-difference tests. `Shapes.Collide` applies each candidate shape in iteration order and preserves the collision epsilon.
- `AxisCycle` supplies identity/forward/backward axis and coordinate permutations used by coordinate-aware shape bounds.

## Deferred dependencies

- `SliceShape`, block-hit-result clipping, collision contexts, swept collision and renderer integrations still require their respective models; no placeholders were introduced.

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
- [SubShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/subshape/)
- [SliceShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/sliceshape/)
- [VoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/voxelshape/)
- [AABB — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/AABB.html)
- [HitResult — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/HitResult.html)
- [BlockHitResult — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/BlockHitResult.html)
- [CubeVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/cubevoxelshape/)
- [ArrayVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/arrayvoxelshape/)
- [Shapes — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/shapes/)
- [AttachFace — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/level/block/state/properties/AttachFace.html)
