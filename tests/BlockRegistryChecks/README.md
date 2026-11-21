# Block registry checks

Run the content-system checks without launching Godot:

```sh
dotnet run --project tests/BlockRegistryChecks/BlockRegistryChecks.csproj
```

The project links the production files under `src/Asteria/Content`; it does not keep a test-only copy of the registry implementation.


The console harness also runs state, direction, vector, AABB, discrete voxel, index-merger and voxel-shape/Boolean-join parity checks against Minecraft Java 26.3.
