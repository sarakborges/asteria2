# Block registry, block states, and chunk-section storage

## Reference and scope

Minecraft Java Edition 26.3 is the reference for architecture, contracts, and behavior. Asteria keeps its own block content; this port follows vanilla systems rather than substituting project-specific designs.

Block-color contracts are checked against the 26.3 sources for [MapColor](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/mapcolor/), [ARGB](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/util/argb/), [BlockBehaviour](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/blockbehaviour/) and the `Block` default-map-color accessor.

This stage covers only systems already introduced in Asteria: registries and holders, blocks and block states, block-state IDs, palette storage, and the block-state portion of chunk sections. Fluid, biome, data-component, network, and disk-persistence behavior are deferred until their corresponding domain systems are introduced.

## Registry and holder model

- `BuiltInRegistries.Root` is the root registry of registries. It owns the `asteria:root` identity; each child registry is registered there under its own identifier.
- Each built-in registry is registered with an ordered bootstrap loader. Bootstrap executes those loaders in declaration order before freezing the root and its child registries.
- `Registry<T>` follows `MappedRegistry<T>`: location, registry-key, value-identity, numeric-ID, and holder indexes share stable `Holder.Reference<T>` instances.
- Built-in values use the vanilla static `Registry.Register` helper overloads. These accept a concrete value subtype of the registry's declared value type; `RegisterForHolder` likewise accepts subtype values and returns the registry's base-typed reference holder.
- The root registry freezes first. Each child registry then binds all bootstrapped tags to empty, freezes, and validates its default entry when it is a `DefaultedRegistry`.
- `Count`/registry size includes standalone key placeholders. Numeric ID lookup remains separate from key lookup.
- `AsHolderIdMap` exposes registry holders through the vanilla `IdMap<Holder<T>>` view defined by the `Registry` interface.
- Registration records `RegistrationInfo` and aggregates `Lifecycle`; ordinary registration uses the built-in stable metadata.
- Registry freezing binds values, rejects unbound standalone references and unregistered intrusive holders, verifies tag binding, and publishes the bound tag set.
- Intrusive registries require an intrusive holder before registration. A block creates its intrusive holder before its state definition, as vanilla `Block` does.
- `Holder<T>` supports direct and reference variants, key/predicate/tag membership, ownership checks, unwrap/unwrap-key, registered names, and bound/unbound tag behavior. `Holder.Reference<T>` remains subclassable through its protected typed constructor and exposes public standalone/intrusive factories; the intrusive factory is deprecated.
- `HolderSet<T>` supports direct and named sets, the vanilla direct-set factories, ordered contents, membership, random selection, unwrap/unwrap-key, and serialization ownership. Direct sets compare ordered contents and derive their hash codes from those contents; named and other `ListBacked` variants retain object-identity equality. Direct-set membership uses holder equality.
- `HolderGetter<T>` exposes optional and throwing key/tag lookup plus random element selection. Registration lookups create stable standalone placeholders.
- `HolderLookup<T>` and `RegistryLookup` expose element/tag enumeration, registry key and lifecycle metadata, and vanilla-style element filtering.
- `DefaultedRegistry<T>` applies fallback semantics only to the vanilla defaulted lookup paths; exact holder and optional lookups remain exact.
- `HolderGetterProvider` and `HolderLookupProvider` provide typed cross-registry lookup, global element/tag retrieval, registry enumeration, and lifecycle aggregation. The complete tag-loader pipeline and codec serialization context remain deferred.

## Identifiers and resource keys

- `ResourceLocation` follows vanilla identifier parsing, custom separators, validation, ordering, path transformations, and language/debug naming helpers. Its project default namespace is `asteria` (not Minecraft's `minecraft` namespace). `ResolveAgainst` also normalizes and guards the resolved path against escaping its root.
- `RegistryKey<T>` has a private constructor and exposes the vanilla `Create`/`CreateRegistryKey` factories, typed casts, and dependent resource-key creation. `IsFor` compares only the registry identifier, independent of the root key. Its value equality preserves the observable contract of vanilla's weakly interned keys. `TagKey<T>` is a record with a public deprecated canonical constructor and a preferred `Create` factory; it retains the full registry key, compares the entire key for registry membership, and supports typed casts.

## Blocks and block states

- `BlockProperties` is a mutable fluent builder for the modeled `BlockBehaviour.Properties` values: destroy time, explosion resistance, friction, speed/jump factors, bounce restitution, fall-distance reduction, collision/occlusion, random ticking, dynamic shape, `forceSolidOn`, deprecated `forceSolidOff`, air/tool flags, lava ignition, the deprecated liquid compatibility flag, replaceability, terrain particles, map color, piston push reaction, light/emissive behavior, deterministic offset, suffocation, view blocking and post-process position. Map color accepts either one fixed palette entry or a state-dependent selector. Defaults and setter behavior follow the reference contract. Redstone conductor/signal properties are deliberately omitted from this offline product.
- `Block` snapshots block-level values at construction: explosion resistance, friction, speed/jump factors, bounce restitution, fall-distance reduction, collision, random ticking, and dynamic-shape flags. `GetDefaultMapColor` evaluates the configured map-color selector against the block's current default state. `GetDefaultDestroyTime` reads the retained mutable `BlockProperties.DestroyTimeValue` each time, while each `BlockState.DestroySpeed` keeps the value captured when that state was constructed. It creates its intrusive holder before building states and calls the virtual `CreateBlockStateDefinition` extension point.
- `StateProperty<T>` exposes a name, value type, ordered possible values, parsing, serialization, typed `Property.Value` wrappers, `GetAllValues`, and overridable public hash generation. Hashing follows the reference class-identity/string formula, including ordered integer/enum value-list hashing, and `hashCode` caches its generated result.
- `BlockState.GetProperties` exposes the immutable, sorted property collection. `BlockState.GetValues` enumerates typed property/value pairs in the same state-definition order.
- `StateDefinition` sorts property names, materializes the Cartesian product in reference order, and links each state to precomputed neighbor states.
- The generic `StateHolder` retains reference identity: `Equals` is sealed and delegates to `Object.Equals`, while `GetHashCode` delegates to `Object.GetHashCode` and remains overridable, matching the vanilla `equals`/ `hashCode` declarations.
- `SupportType` preserves the `FULL`, `CENTER`, `RIGID` order and the 26.3 face-support geometry. `BlockState.IsFaceSturdy` uses the precomputed six-face/support-type cache for static shapes and evaluates the predicate directly for dynamic shapes.
- `BlockState.SetValue`, `TrySetValue`, and `Cycle` resolve through the precomputed neighbor tables; `TryGetValue` provides the optional-value lookup path. `Block.WithPropertiesOf` starts from the target block's default state and copies only source properties shared by identity.
- `BlockState.Is(tag, predicate)` combines holder tag membership with a caller predicate and evaluates the predicate only after membership succeeds, preserving the reference's short-circuit behavior.
- `PathComputationType` preserves the 26.3 `LAND`, `WATER`, `AIR` order. `BlockState.IsPathfindable` delegates to the owning block: `LAND`/`AIR` require a non-full collision shape using `EmptyBlockGetter` at `BlockPos.Zero`, `WATER` checks the fluid state's `FluidTags.Water` membership, and unknown values return false. `BlockState.SkipRendering` delegates the neighboring state and face direction to the block's virtual hook; the base implementation returns false. `BlockState.CanBeReplaced(Fluid)` follows the separate fluid-placement rule: the state is replaceable when its replaceable flag is set or legacy `IsSolid` is false; the fluid argument does not alter that predicate. `BlockState.GetSeed(BlockPos)` delegates to the owning block, whose default uses the exact `Mth.GetSeed(x, y, z)` coordinate hash.
- A newly constructed `BlockState` starts with `Fluids.EMPTY.DefaultFluidState` before `initCache`; `initCache` then replaces that initial value with the owning block's fluid state. Reads before cache initialization therefore return the empty fluid state rather than throwing.
- Each `BlockState` snapshots the currently modeled state-level `BlockStateBase` values at construction: air, destroy speed, correct-tool requirement, occlusion, random ticking, lava ignition, liquid, replaceability, terrain-particle behavior, map color, piston reaction, light/emissive configuration, offset and the generic suffocation/view-blocking/post-process delegates. The default view-blocking delegate deliberately retains the vanilla closure over the mutable `BlockProperties` instance and therefore observes a later replacement of `IsSuffocatingValue`; an explicit `IsViewBlocking` override replaces that closure. The 62 populated entries of the 64-slot `MapColor` table, brightness values, ARGB calculation and packed-byte mapping follow 26.3; IDs 62 and 63 remain empty and resolve to `NONE`. Redstone-only signal-source, analog-output, wire-connection, signal-level and conductor APIs are not part of Asteria's local gameplay contract.

- `BlockState.InitCache` follows `BlockStateBase.initCache`: every call refreshes fluid state, random-ticking status, the static collision/face-support cache, legacy solidity, occlusion, skylight propagation and light dampening. It is not guarded by an idempotence flag; reinitialization must recompute derived values.
- Static state-cache construction follows `BlockStateBase.Cache`: after computing the collision shape, it rejects a non-empty shape combined with an offset function unless `dynamicShape` is enabled. The exception uses the block's registered key in its diagnostic; blocks without collision and blocks marked dynamic skip that rejection path.
- `BlockState.IsSolid` preserves the deprecated legacy calculation from 26.3, distinct from `IsSolidRender`: `forceSolidOn` wins first, followed by `forceSolidOff`; otherwise a missing collision cache or empty shape is non-solid, a bounds average size of at least `0.7291666666666666` is solid, and a Y extent of at least one block is the final solid condition. The `SolidDebugger` port retains the seven source reasons, their ordinals/messages, first-reason-per-holder behavior keyed by the registered `Holder<Block>`, conflicting-state warning, and grouped dump using `getRegisteredName()` around block-state cache initialization. Test-only unregistered blocks use one stable direct holder. The .NET adaptations are explicit: Java `ScopedValue` is represented by `AsyncLocal`; the Java reason enum is represented by immutable singleton values retaining the same names, ordinals, flags and messages; diagnostic logger output uses `System.Diagnostics.Trace`; and Java system properties are read from `MC_DEBUG_ENABLED` and `MC_DEBUG_CALCULATE_SOLID` environment variables once when `SolidDebugger` is initialized.
- Generic sound/instrument behavior remains excluded with audio. Loot metadata and feature-flag gating have not yet been ported; remaining state-cache work must be compared against the 26.3 methods that are relevant to the local world simulation.
- `Block.BlockStateRegistry` is the global `IdMapper<BlockState>` equivalent. Each state receives its insertion-order ID in its constructor, as soon as `StateDefinition` materializes it; the expected-size hint preallocates both forward and reverse indexes. This map is independent of the built-in block registry and is not frozen with it. `Block.GetId` maps null or unknown states to the air ID (zero), and `Block.StateById` resolves missing IDs to the default air state.

## Paletted storage

- `PalettedContainerStrategy<T>` follows the vanilla cubic dimensions and coordinate layout. Block-state containers use 16³ entries and 4–8 bit local palettes. The 4³ biome strategy exists as a sizing strategy only; biome values and biome storage are deferred.
- Configuration selection follows vanilla single-value, linear, hash-map, and global modes, including distinct resident-memory and packed-storage bit widths for global mode.
- `PalettedContainer<T>` exposes coordinate-based get, set, get-and-set, unchecked mutation, palette enumeration, counting, copying, and recreation.
- Resize builds or reuses the matching configuration data, remaps old palette values, then publishes the resulting snapshot.
- `Acquire`/`Release` use a one-writer detector. Unchecked writes are for callers that already hold the write permit.
- `SingleValuePalette`, `LinearPalette`, `HashMapPalette`, and `GlobalPalette` follow their corresponding vanilla palette contracts.
- `PackedBitStorage` follows `SimpleBitStorage` word packing, per-word indexing, fill, get-all, unpack, and raw-array access. `ZeroBitStorage` represents zero-bit palettes without allocating a packed array.
- Missing palette entries use the dedicated exception type.
- Data codecs, network buffer read/write, NBT packing, and disk serialization are not yet introduced.

## Chunk sections

- `ChunkSection` receives a prebuilt block-state `PalettedContainer` and recalculates its initial non-air count without imposing an additional size check on the supplied container.
- Block-state access is coordinate-based. The flattening formula remains the vanilla `(y << 8) | (z << 4) | x` through the strategy.
- The currently applicable cached counters are non-air block count and randomly-ticking block count. Both counters include only non-air block states; the ticking counter does not count an air state even if its modeled flags also mark it as randomly ticking. `IsRandomlyTickingBlocks` and `IsRandomlyTicking` expose the block portion of the reference section's ticking gate. The section exposes its state container, can recalculate both counts, and copies its state container and cached counters.
- Fluid states and their two counters, biome holder storage and resolution, and section serialization are deferred until those matching domain systems and codecs exist.

## Parity gate

This document records implemented scope; it does not certify total parity. The stage remains open until every behavior and structural contract within the systems above has been compared with Minecraft 26.3 and the relevant checks/build pass. Future systems remain deferred rather than being counted as current-stage defects. No later development stage may begin while a current-scope divergence remains.


## BlockStateBase constructor ordering

The state constructor preserves Minecraft 26.3 callback evaluation order: light emission, shape-for-light-occlusion, then map color, followed by the remaining property snapshots and delegates. Each callback receives the partially initialized state. BlockBehaviorChecks verifies callback order and the values visible during those callbacks.


## IntegerProperty index and parse contracts

`IntegerProperty.IndexOf` mirrors `getInternalIndex`: values above the maximum return -1; all others use unchecked 32-bit `value - minimum`, preserving Java integer overflow. Parsing intentionally does not use this index as a range predicate: Minecraft `getValue(String)` checks `minimum <= value <= maximum` directly, so out-of-range textual values remain rejected even when the index subtraction wraps.


## Block-state codec lifecycle

The full block-state form applies Minecraft 26.3 `FULL_CODEC.stable()` semantics to both successful and failed encode/decode results. When the compact string form is enabled, that branch preserves the registry name codec lifecycle; object-form states retain the stable full-codec lifecycle.


## StateDefinition construction boundary

The generic `StateDefinition` constructor is protected-internal rather than public. This matches Minecraft 26.3's protected constructor while retaining access for the in-assembly validated builder. External callers must create definitions through `StateDefinitionBuilder`, so property name, value-count, serialized-value, and duplicate-name validation cannot be bypassed accidentally.


## Redstone conductor predicate

`BlockProperties.IsRedstoneConductor` and `BlockState.IsRedstoneConductor(level, position)` port the generic `BlockBehaviour.Properties.isRedstoneConductor` contract. The default predicate delegates to the state's full-collision-shape test; a configured predicate is retained by each state and evaluated with the query's level and position.
