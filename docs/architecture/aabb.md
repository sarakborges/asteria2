# Axis-aligned bounding boxes

## Minecraft 26.3 reference

The AABB core is ported against the 26.3 source:

- Bounds are normalized independently on each axis at construction; the six coordinates are immutable.
- Constructors/factories cover a block position, two vector endpoints, a unit cube at a lower corner, and inclusive full-block encapsulation between two block positions.
- Contract and ExpandTowards alter only the face selected by each delta sign. Inflate/Deflate update all faces symmetrically or per-axis.
- Intersect and MinMax use component-wise max/min respectively. The constructor's normalization behavior is retained even when the inputs do not overlap.
- Move supports integer block positions, real-valued vectors, and .NET float vectors.
- Intersection uses strict overlap; containment is half-open: minimum faces included, maximum faces excluded.
- Segment clipping preserves the reference face test order (X, then Y, then Z), 1e-7 direction threshold, strict segment-scale ordering, and 1e-7 face-boundary tolerance.
- CollidedAlongVector expands shape-part bounds by half the moving box size minus 1e-7, checks both endpoints, and then clips the center segment.
- Point/box squared distances, arithmetic mean GetSize, center/bottom-center, min/max positions, NextDeflated, NaN detection, Java-compatible double equality/hash and string formatting are included.
- Builder accumulates float coordinates and rejects Build before any point is included.

## Adaptations and deferred integrations

- Java's JOML Vector3fc builder input and Vector3f movement overload are adapted to .NET System.Numerics.Vector3, preserving float component precision without introducing a runtime dependency on JOML.
- The overload taking the world-generation BoundingBox type is deferred until the structure/world-generation domain exists.
- The multi-AABB clipping overload returning BlockHitResult is deferred until the block hit-result model exists. The per-AABB segment clip used by the query and collision paths is implemented.
- VoxelShape, discrete voxel shape storage, shape Boolean operations, collision contexts and renderer integrations are not introduced as placeholders. They can be ported on top of this AABB implementation as their actual dependencies are added.

## Reference source

- [AABB — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/AABB/)
- [Vec3 — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/Vec3/)
- [Direction — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/Direction/)
