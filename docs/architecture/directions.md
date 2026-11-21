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

The C# representation is a set of singleton objects rather than a C# enum. That is necessary because each direction carries vector and metadata fields and provides instance methods; canonical object identity remains stable. This does not introduce per-call direction instances.

## Numeric dependency

Mth currently contains only the dependency closure needed by the ported methods:
- 65,536-entry sine lookup table generated with the reference scale and float narrowing.
- Quantized Sin and offset Cos.
- Floor with Java-compatible NaN and integer saturation behavior.

Other numeric methods from the 26.3 utility class remain outside this increment.

## Explicitly deferred

The rest of Direction requires additional systems that are not represented by placeholders:
- orderedByNearest and getFacingAxis need an entity/view-rotation model.
- rotate and quaternion/normal-vector accessors need a matrix/vector math layer.
- Random direction/axis/plane selection needs the game's random-source contract.
- Codecs and stream codecs need the serialization/network domains.
- axisStepOrder and vector overloads need the real-valued Vec3 domain.

## Reference source

- [Direction — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/Direction/)
- [Mth — Minecraft 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/util/Mth/)
