# Direction and axis primitives

## Minecraft 26.3 reference

This increment ports the discrete direction model used by spatial and world algorithms:

- The six canonical directions retain enum declaration order, 3D data IDs, horizontal 2D IDs, opposite indexes, names and unit integer vectors.
- The 2D lookup order is SOUTH, WEST, NORTH, EAST; 3D lookup is sorted by the 3D data value. Negative lookup values use absolute remainder indexing, not positive modulo.
- Axis keeps X/Y/Z order, positive/negative directions, plane membership, axis predicates and choose overloads.
- AxisDirection retains signed steps and opposite relationships.
- Plane retains the HORIZONTAL face order NORTH/EAST/SOUTH/WEST and VERTICAL face order UP/DOWN, their axis arrays, predicate, stream and enumerable behavior.
- Yaw conversion uses the reference floor-after-half rounding, and direction rotations preserve the exact cardinal lookup tables and exception behavior.
- GetApproximateNearest preserves float conversion and enum iteration order, including its NORTH fallback when all dot products are zero. GetNearest preserves strict greater-than comparisons and Java integer absolute-value overflow behavior.
- Direction retains its corresponding real-valued unit vector. `AxisStepOrder(Vec3)` returns the immutable YXZ or YZX order selected by the strict absolute-X/absolute-Z comparison.

The C# representation is a set of singleton objects rather than a C# enum. That is necessary because each direction carries vector and metadata fields and provides instance methods; canonical object identity remains stable. This does not introduce per-call direction instances.

## Numeric dependency

Mth currently contains the dependency closure needed by the ported spatial operations:
- 65,536-entry sine lookup table generated with the reference scale and float narrowing.
- Quantized Sin and offset Cos.
- Floor with Java-compatible NaN and integer saturation behavior.
- Double `Lerp`, `Square`, `LengthSquared` and `Length` helpers used by `Vec3`.

Other numeric methods from the 26.3 utility class remain outside this increment.

## Real-valued vector

`Position` and `Vec3` are now present. The implemented vector path covers block-position factories, arithmetic, dot/cross products, distance predicates, normalization, lengths, quantized rotations, direction-from-rotation, axis alignment/replacement, relative offsets, projection, equality/hash and finite checks. Double formatting preserves Java's plain/scientific notation thresholds. `Direction.GetApproximateNearest(Vec3)`, `GetUnitVec3()` and `AxisStepOrder(Vec3)` use this type.

The C# `Position` contract uses `GetX/GetY/GetZ` methods in place of Java's `x/y/z` method names; the returned coordinate values and call semantics are unchanged.

## Explicitly deferred

The remaining `Direction` and `Vec3` members require real additional systems:
- `orderedByNearest` and `getFacingAxis` need an entity/view-rotation model.
- Matrix rotation, quaternion and `Vector3f` accessors need the linear-algebra integration used by the renderer.
- Random vector offsets and random direction/axis/plane selection need the game's random-source contract.
- `Vec2`-based direction rotation output/input needs the two-dimensional vector type.
- Codecs and stream codecs need the serialization/network domains.

## Reference source

- [Direction — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/Direction/)
- [Mth — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/util/Mth/)
