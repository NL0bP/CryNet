# CryPhysics.Sharp — Deferred Items

Per gold rule 2: items that cannot be ported literally from C++ because the C# side lacks required infrastructure. Each entry cites the C++ source file:line and the missing C# infrastructure.

---

## 2026-04-14 (UPDATED — most deferrals resolved)

The session 2026-04-14 (commits below) added the foundational infrastructure that previously
forced these files to be deferred. Status is updated per file.

### `physicalplaceholder.cpp` — ✅ **PORTED** (Entities/PhysicalPlaceholder.cs)

All required infrastructure landed:
- `PhysicsForeignData` struct (Entities/PhysicsForeignData.cs)
- `PhysicalEntity.TimeIdle/MaxTimeIdle` fields
- `EventPhysStateChange.TimeIdle` field
- `StatusPlaceholder` + `ActionRemoveAllParts` types
- `PhysicalWorld.IsPlaceholder/RegisterPlaceholder/RepositionEntity/LockGrid/StaticPhysicalEntity/PhysicsStreamer`
- `PhysWorldsRegistry` (g_pPhysWorlds equivalent)
- `IPhysicsStreamer` interface (World/IPhysicsStreamer.cs)
- `TSerialize/CStream/SerializeScopedBeginGroup/ISerialize` (Serialization/ISerialize.cs)
- Three-phase step methods on `IPhysicalEntity`

Forward methods that depended on per-entity snapshot serialization (`GetStateSnapshot`/`SetStateFromSnapshot`/`GetStateChecksum`/`SetNetworkAuthority`) currently return 0 / no-op on the placeholder because the buddy entity types have not yet implemented snapshot serialization. The placeholder API surface is complete.

### `rwi.cpp` async queue — ✅ **PORTED** (World/RWIQueue.cs)

`RayWorldIntersectionAsync(...)` enqueues a `SRwiRequest`; `TracePendingRays(bool)` drains the queue and dispatches `EventPhysRWIResult`. The PWI (PrimitiveWorldIntersection) async branch from rwi.cpp:689-720 is not yet ported because the immediate-mode `PrimitiveWorldIntersection` does not yet exist in the C# port — when it does, the same queue pattern applies.

### Original entry below (kept for history)

### `physicalplaceholder.cpp` (227 LOC) + `physicalplaceholder.h` (111 LOC) — superseded above

C++ ref: `dev/Code/CryEngine/CryPhysics/physicalplaceholder.{h,cpp}`.

`CPhysicalPlaceholder` is a lightweight forwarding entity whose buddy (`m_pEntBuddy`) is created on demand by `CPhysicalWorld::m_pPhysicsStreamer`. The literal port requires several pieces of C# infrastructure that do not exist:

| Missing C# piece | C++ symbol | Where used |
|---|---|---|
| `IPhysicalEntity.SetParams(pe_params*, int)` byte-id dispatch | `_params->type==pe_params_bbox::type_id` | `SetParams`/`GetParams` |
| `IPhysicalWorld.IsPlaceholder(IPhysicalEntity*)` | `pWorld->IsPlaceholder(this)` | `GetWorld`, `GetEntity` |
| `IPhysicalWorld.RepositionEntity` | `pWorld->RepositionEntity(this,1)` | `SetParams(bbox)` |
| `IPhysicalWorld.m_lockGrid` + `AtomicAdd` | grid lock counter | `SetParams(bbox)` |
| `IPhysicalWorld.m_pPhysicsStreamer` + `IPhysicsStreamer.CreatePhysicalEntity` | streamer pattern | `GetEntity` lazy creation |
| `g_pPhysWorlds` / `g_StaticPhysicalEntity` globals | global world list | `GetWorld`, `GetEntity` fallback |
| `EventPhysStateChange.timeIdle` field | event payload | `SetParams(bbox)` change event |
| `IPhysicalEntity.StartStep`/`Step`/`StepBack` virtuals (separate from `DoStep`) | three-phase step | `Step`/`StartStep`/`StepBack` |
| `CStream` / `TSerialize` snapshot APIs | binary/serialize snapshots | `GetStateSnapshot`/`SetStateFromSnapshot` (6 overloads) |
| `IPhysicalEntity.SetNetworkAuthority`, `GetStateChecksum` | net replication API | placeholder forwards both |
| `pe_status_placeholder`, `pe_action_remove_all_parts` types | status/action union members | `GetStatus`, `Action` |
| `PhysicsForeignData` union pointer type | typedef in physinterface.h | `m_pForeignData` field |
| Bitfield-packed `m_id : 23`, `m_iSimClass : 8`, `vec2dpacked.x:16` | byte packing | placeholder + `pe_gridthunk` |

This is ~250 LOC of literal port that cannot be written without first porting ~1000+ LOC of supporting infrastructure (streamer, snapshot serialization, world->placeholder lookup, three-phase step protocol). Defer entire file.

### `boolean2d.cpp` (~800 LOC) + `boolean3d.cpp` (~1200 LOC) — **deferred**

C++ ref: `dev/Code/CryEngine/CryPhysics/boolean2d.cpp`, `boolean3d.cpp`.

Boolean polygon/mesh operations (CSG). Self-contained algorithms but volume is large and there is no caller in the existing C# port — `TriMeshGeometry` does NOT call `CTriMesh::Boolean` paths. Defer until a consumer needs it.

### `rwi.cpp` (~600 LOC) — **deferred**

C++ ref: `dev/Code/CryEngine/CryPhysics/rwi.cpp`.

Ray-world-intersection multi-thread queue + deferred-result handling. Current `PhysicalWorld.RayWorldIntersection` does the immediate-mode path. The deferred queue path (`PhysicalWorld::RayWorldIntersectionAsync`) requires the full event queue, `EventPhysRWIResult` dispatch from worker threads, ICrySizer memory tracking, and `g_pLockIntersect` global. Defer.

### `voxelbv.cpp` (~200 LOC) — **partial**

C++ ref: `dev/Code/CryEngine/CryPhysics/voxelbv.cpp`.

`CVoxelBV` BV adapter — voxel-grid bounding volume. Functionality is currently inlined into `Geometry/VoxelGeometry.cs` rather than living in a separate `BVTrees/VoxelBV.cs`. Marked as "ported (merged)" by README but no standalone class exists. Splitting is cosmetic — defer until needed.

---

### `CGeometry::Intersect` BVTree traversal — **API expanded; recursive traversal still pending**

Step toward full resolution landed in the 2026-04-14 session:
- New `BV` class hierarchy literal-ported from bvtree.h:22-44 (BVTrees/BV.cs): `BV` base, `BVPrimitive`, `BBox`, `BVHeightfield`, `BVVoxelgrid`, `BVRay`.
- `BVTreeTypes` enum (matches C++ `BVtreetypes`).
- `BVTree` base expanded with all 4 `GetNodeBVRef` overloads, `GetNodeChildrenBVs` (3 overloads), 6-arg `GetNodeContents`, `GetNodeContentsIdx`, `MarkUsedTriangle`, `BuildFromGeom`, `SetGeomConvex`, `PrepareForIntersectionTestBV`, `CleanupAfterIntersectionTest`, `ReleaseLastBVs`, `ReleaseLastSweptBVs`, `SplitPriority`, `GetMaxSkipDim`, `GetTypeId` — all defaulting to no-op matching C++ default bodies.

Still pending for a complete literal port of `CGeometry::Intersect`:
- Per-subclass `GetNodeBVRef` + 6-arg `GetNodeContents` overrides on `AABBTree`, `OBBTree`, `SingleBoxTree`, `HeightfieldBV`, `RayBV`, voxel BV.
- Global scratch pool: `g_Contacts[]`, `g_AreaBuf[]`, `g_AreaPtBuf[]`, `g_BrdPtBuf[]`, `g_nTotContacts`, `g_Overlapper.Init()`, `ResetGlobalPrimsBuffers(iCaller)`, per-thread iCaller-keyed.
- Helper `GTestPrepPartDeux(gtest, offsetWorld)` (geometry.cpp:280-287).
- Recursive `IntersectBVs(gtest, pBV1, pBV2)` (the actual traversal).
- Sweep-test branch (geometry.cpp:454+).
- Hash-based contact merging.

Estimated remaining: ~700-1000 LOC across 6 BVTree subclasses + 2 helpers + the recursive driver. Single-file effort; tracked as Tier 3 #42.

### `CGeometry::Intersect` BVTree traversal — original entry below (kept for history)

C++ ref: `dev/Code/CryEngine/CryPhysics/geometry.cpp:289-450`.

The fully-literal port requires:
- Overloaded `BVTree.GetNodeBV(BV*, int iCaller)` with sweepdir + scale-relative variants
- `BVTree.GetNodeContents(int iNode, BV* pBV2, int bNodeUsed, int iCaller, GUT*, GUT*)` — significantly different signature than current C# `GetNodeContents(int, ref int[])`
- `OverlapBV(const BV*, const BV*)` virtual on each tree subclass
- Recursion stack with per-thread iCaller-keyed scratch (started in WIP commit `2420b9d`)
- Hash-based contact merging (`HashContacts` from C++)
- Sweep variant branch (geometry.cpp:454+)

WIP scaffold landed in commit `2420b9d`. Full traversal still pending — needs API expansion across `AABBTree`, `OBBTree`, `HeightfieldBV`, `RayBV`, `SingleBoxTree`. Tracked as PORT_STATUS Tier 3 #42.

---

## Capsule completion (2026-04-14) — **NOT deferred**

`capsulegeom.cpp` overrides ported in commit (this session): `CalcPhysicalProperties`, `PointInsideStatus`, `PrepareForIntersectionTest`. Remaining capsule overrides — `CalculateBuoyancy`, `CalculateMediumResistance`, `DrawToOcclusionCubemap`, `UnprojectSphere`, `GetUnprojectionCandidates` — fall back to inherited `CylinderGeometry` behaviour. These cylinder-vs-capsule physics divergences are real fidelity bugs and remain to be ported (each is self-contained, no missing infra).
