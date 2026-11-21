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

## Deferred dependencies

- Octahedral rotations wait for `OctahedralGroup` and signed-axis transforms.
- Axis-cycle transforms, face/edge extraction, voxel coordinate lists, index-merger strategies and full shape Boolean joins are subsequent increments.
- `VoxelShape`, `Shapes`, `SliceShape`, collision contexts and renderer integrations are not placeholder types in this increment.

## Reference source

- [DiscreteVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/discretevoxelshape/)
- [BitSetDiscreteVoxelShape — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/bitsetdiscretevoxelshape/)
- [BooleanOp — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/booleanop/)
