# CryAISystem.Sharp — Port Status

Source: `dev/Code/CryEngine/CryAISystem/` (397 files, ~91,229 LOC)
Target: `CryAISystem.Sharp/src/CryAISystem.Core/`
Methodology: literal line-by-line C++ → C# port. Math types consumed from `CryPhysics.Sharp`.

## Phase tracker

| Phase | Description | Files | LOC | Status |
|-------|-------------|-------|-----|--------|
| 0 | Scaffolding | — | — | ✅ done |
| 1 | Leaves (Factions, PolygonSetOps, Hash, Log, etc.) | ~25 | ~3000 | ✅ done (3 deferred) |
| 2 | Actor hierarchy (AIObject → Puppet) | ~25 | ~25000 | 🟡 all .h literal ports done; .cpp impls (~12k LOC) + AICollision pending |
| 3 | Navigation (root + MNM + NavSys + Walkability) | ~80 | ~21000 | 🟡 .h shells in place; .cpp impls pending |
| 4 | Movement + CollisionAvoidance | ~33 | ~4700 | 🟡 .h shells in place; .cpp impls pending |
| 5 | Perception | ~16 | ~5000 | 🟡 .h shells in place; .cpp impls pending |
| 6 | Cover + TPS + TargetSelection | ~32 | ~16400 | pending |
| 7 | Goals & Pipes | ~20 | ~10400 | 🟡 AIPIDController done; rest pending |
| 8 | BehaviorTree + SelectionTree | ~37 | ~12000 | pending |
| 9 | Group dynamics | ~12 | ~5600 | pending |
| 10 | Communication + Sequence + Mannequin + AIBubbles | ~26 | ~7000 | pending |
| 11 | CAISystem coordinator + DebugDraw + AIRecorder | ~25 | ~15000 | 🟡 AISignalCRCs done; CAISystem shell only; rest pending |
| 12 | GameSpecific + FlowNodes | ~14 | ~7500 | pending |
| 13 | ScriptBind_AI | 2 | ~12000 | pending |

## Current build state
**`dotnet build CryAISystem.Sharp.sln --no-incremental` → 0 errors, 414 warnings.**

Warnings cresceram de 196 → 414 com a inclusão de AICollision.cs e dos novos CryCommon ports (vários campos shell ainda não usados; o número cai naturalmente quando os call-sites Phase 2/3 chegarem). Sem warnings novos críticos — todos do tipo CS0414/CS0649 (campo atribuído mas não usado / never assigned), pré-existentes em shells.

**Build-deferred files**: nenhum. AICollision.cs **agora compila** e foi removida do `<Compile Remove>` no csproj.

## Files ported (real literal ports — verified compiling)

### Phase 1 — Leaves (CryAISystem)
- [x] `Configuration.cs` — 43L C++
- [x] `StdAfx.cs` — 174L C++
- [x] `AIHash.cs` — 109L C++
- [x] `Reference.cs` — 1019L C++ (h+inl)
- [x] `AIQuadTree.cs` — 567L C++
- [x] `AILog.cs` — 475L C++
- [x] `StatsManager.cs` — 191L C++
- [x] `BlackBoard.cs` — 66L C++
- [x] `HashSpace.cs` — 572L C++
- [x] `ClusterDetector.cs` — 340L C++
- [x] `CalculationStopper.cs` — 174L C++
- [x] `Factions/FactionMap.cs` — 404L C++
- [x] `PolygonSetOps/Polygon2d.cs`
- [x] `PolygonSetOps/LineSeg.cs`
- [x] `PolygonSetOps/BiDirMap.cs`
- [x] `PolygonSetOps/Utils.cs`
- [x] `AIDbgRecorder.cs` — ~150L C++
- [x] `NullAIDebugRenderer.cs` — ~100L C++
- [x] `ValueHistory.cs` — ~100L C++
- [x] `XMLUtils.cs` — ~80L C++

### Phase 2 — Actor hierarchy headers (.h ported, .cpp impls deferred)
- [x] `Adapters.cs` — 59L C++
- [x] `PersonalLog.cs` — h+cpp literal
- [x] `ActorLookUp.cs`
- [x] `AIObject.cs` — 1438L C++ (h + cpp)
- [x] `ObjectContainer.cs` — **594L cpp port complete (session 4)** + `SAIObjectCreationHelper` literal port from AIObjectManager.cpp lines 34-93
- [x] `CryCommon/IDMap.cs` — literal port of IDMap.h template (session 4)
- [x] `AIObjectManager.cs` — **.h + ~85% of .cpp literal port (sessions 10-12)**: ctor, dtor, Init, Reset, **CreateAIObject** (full literal w/ AIOBJECT_* switch + AIObjectParams literal port), RemoveObject, GetAIObject, GetAIObjectByName (2 overloads), CreateDummyObject (2 overloads), OnObjectRemoved (Phase 9/11-dep branches stubbed), RemoveObjectFromAllOfType, ReleasePooledObject, OnEntityPreparedFromPool, OnEntityReturnedToPool, OnPoolDefinitionsLoaded, **OnBookmarkEntitySerialize**. **Deferred**: GetFirstAIObject/InRange (need iterator templates `SAIObjectMapIter*<>` from Phase 11). AIObjectOwners/AIObjects multimap helpers literal-port'd. AIObjectParams literal port from IAgent.h lines 692-727. IEntity shell extended w/ HasAI/IsFromPool/GetId/GetAIObjectID/SetAIObjectID. TSerialize.BeginOptionalGroup added.
- [x] `PostureManager.cs` — .h port; **simpler .cpp methods literal port complete (session 5)**: ctor, dtor, CancelRays, ResetPostures, AddDefaultPostures, AddPosture, SetPosture, GetPosture, GetPostureID, GetPostureByName, SetPosturePriority, GetPosturePriority. QueryPosture and ray-completion methods deferred (need CoverSystem + RayCaster + ActorLookUp full ports).
- [x] `HideSpot.cs` — **.h+.cpp literal port complete (session 5)**: SHideSpotInfo, SHideSpot ctor/IsSecondary
- [x] `VertexList.cs` — extended `ObstacleData` with literal port from GraphStructures.h (SetCollidable/SetHideable/IsCollidable/IsHideable/SetApproxHeight/GetApproxHeight)
- [x] `AIActor.cs` — AIActor.h literal port (3093L cpp deferred)
- [x] `AIPlayer.cs` — AIPlayer.h literal port (1367L cpp deferred)
- [x] `AIVehicle.cs` — .h literal port + ctor/dtor/RecalculateAccuracy/PredictMovingTarget literal cpp port (session 12). Other methods deferred — heavy CPuppet state/firing/cover deps.
- [x] `AIFlyingVehicle.cs` — **49L .h + 249L .cpp literal port complete (session 6)** — full SetObserver, OnVisionChanged, Serialize, PostSerialize, SetSignal, AISendSignal helper
- [x] `PipeUser.cs` — PipeUser.h literal port (5673L cpp deferred)
- [x] `Puppet.cs` — Puppet.h literal port (6168L cpp deferred)
- [x] `AICollision.cs` — 1666L C++ → 1213L C# **literal port complete and compiling** (sessão 3) — todas deps CryCommon/CryPhysics aterrissaram
- [ ] AIObject.cpp / AIActor.cpp / AIPlayer.cpp / AIVehicle.cpp / PipeUser.cpp / Puppet.cpp / PuppetPhys.cpp / PuppetRateOfDeath / Environment.cpp — all `.cpp` impls deferred (~12k LOC total)

### Phase 3 — Navigation (.h shells, .cpp impls pending)
- [x] `Walkability/FloorHeightCache.cs` — **78L .h + 90L .cpp literal port complete (session 9)** — Reset/SetHeight/GetHeight/GetCellCenter/GetAABB/Draw/GetMemoryUsage with full SortedSet bit-packed cell key port
- [shell] `Walkability/WalkabilityCache.cs` — .h port; .cpp impl (601L) deferred (heavy duplication of AICollision logic + needs HashFromUInt/Vec3/Quat helpers + AABB.ContainsBox/GetVolume/Expand)
- [x] `Walkability/WalkabilityCacheManager.cs` — **70L .h + 317L .cpp literal port complete (session 9)** — full Reset/PreUpdate/PostUpdate/Draw/EnableActor/PrepareActor/IsFloorCached/FindFloor/CheckWalkability (both overloads)
- [shell] `Shape.cs` / `Shape2.cs` / `VertexList.cs` / `HideSpot.cs` / `AIHideObject.cs` / `AIDynHideObjectManager.cs`
- [shell] `Navigation/MNM/MNM.cs`
- [shell] `Navigation/NavigationSystem/NavigationSystem.cs`
- [shell] `Navigation/NavigationSystem/NavigationSystemSubManagers.cs`
- [shell] `Navigation/CustomNavRegion.cs`
- [shell] `AllNodesContainer.cs` / `GraphStructures.cs` / `GraphNodeManager.cs` / `Graph.cs`
- [shell] `CTriangulator.cs` / `Navigation.cs` / `NavPath.cs` / `PathFollower.cs` / `SmartPathFollower.cs`
- [x] `PathMarker.cs` — **77L .h + 407L .cpp literal port complete (session 8)** — full ctor, Update, GetPointAtDistance, GetPointAtDistanceFromNewestPoint, GetDirectionAtDistance, GetDirectionAtDistanceFromNewestPoint, GetMoveDirectionAtDistance (with CSteeringDebugInfo), GetDistanceToPoint, Init, Serialize, DebugDraw
- [shell] `MNMPathfinder.cs` / `PathObstacles.cs` / `FlightNavRegion2.cs` / `Free2DNavRegion.cs`

### Phase 4 — Movement + CollisionAvoidance
- [shell] `Movement/MovementSystem.cs`
- [shell] `CollisionAvoidance/CollisionAvoidanceSystem.cs`

### Phase 5 — Perception
- [shell] `Perception/PerceptionShells.cs`

### Phase 7 — Goals & Pipes
- [x] `AIPIDController.cs` — 102L C++ (h+cpp) — **NEW (this session)**

### Phase 11 — CAISystem coordinator
- [x] `AISignalCRCs.cs` — 299L C++ (h+cpp) — **NEW (this session)**
- [shell] `Environment.cs` — gAIEnv shell
- [shell] `DebugDrawContext.cs`
- [shell] `CryPhysicsAliases.cs`

### CryCommon (literal ports of CryEngine headers as the AI port pulled them in)
- [x] `CryCommon/IFactionMap.cs` — IFactionMap.h, 46L
- [x] `CryCommon/IBlackBoard.cs` — 28L
- [x] `CryCommon/TimeValue.cs` — 182L
- [x] `CryCommon/CryAssert.cs`
- [x] `CryCommon/ITimer.cs`
- [x] `CryCommon/ILog.cs`
- [x] `CryCommon/IConsole.cs`
- [x] `CryCommon/IValidator.cs`
- [x] `CryCommon/IXml.cs`
- [x] `CryCommon/ICryPak.cs`
- [x] `CryCommon/ISerialize.cs`
- [x] `CryCommon/IScriptSystem.cs`
- [x] `CryCommon/ISystem.cs`
- [x] `CryCommon/StlUtils.cs`
- [x] `CryCommon/CryString.cs`
- [x] `CryCommon/IAIObject.cs` — IAgent.h forward decl
- [x] `CryCommon/IAgent_Enums.cs` / `IAgent_Types.cs`
- [x] `CryCommon/Cry_Geo.cs`
- [x] `CryCommon/IClusterDetector.cs`
- [x] `CryCommon/PhysicsShims.cs` — pe_status_living/dynamics shims
- [x] `CryCommon/CryCrc32.cs` — 214L C++ — **NEW (this session)**
- [x] `CryCommon/IAISystem_Collision.cs` — partial port of IAISystem.h (EAICollisionEntities) + physinterface.h (entity_query_flags subset) — **session 2**
- [x] `CryCommon/Primitives.cs` — literal port of primitives.h (primitives namespace, prim_inters, contact, geom_contact, geom_contact_area) — **session 3**
- [x] `CryCommon/PhysInterface.cs` — partial literal port of physinterface.h (intersection_params, ray_hit, geom_world_data, pe_status_nparts, IPhysicalWorld interface, IGeometry, IGeomManager, IPhysUtils, WriteLockCond, IPhysicalWorld.SPWIParams) — **session 3**
- [x] `CryCommon/Vec3Constants.cs` — literal port of Cry_Vector3.h Vec3Constants&lt;float&gt; instantiation — **session 3**
- [x] `CryCommon/Cry_Geo.cs` — extended with `AABB.Add(Vec3, float)`, `AABB.IsIntersectBox`, `AABB.RESET`, `Lineseg(Vec3, Vec3)`, `OBB`/`OBB.CreateOBB`, `Matrix34`, `EBoundingBoxDrawStyle`, `Overlap.Lineseg_Polygon2D` — **session 3**

## CryPhysics.Sharp patches (logged in cryphysics_patches.md)
- 2026-04-11: PhysVector3 lowercase x/y/z aliases
- 2026-04-11: PhysQuaternion v/w aliases
- 2026-04-11: StatusPos pos/q/scale aliases

## Global aliases (GlobalUsings.cs)
uint8/uint16/uint32/uint64/int8/int16/int32/int64/f32/f64 — and `size_t = System.UInt64` (added 2026-04-11 for CryCrc32 port).

## Next targets (rigid plan order)
1. **AIActor.cpp impl** (2628L) — Phase 2 passo 10. Hierarchia atrás: AIObject.cpp já em AIObject.cs.
2. **AIPlayer.cpp** (1367L), **AIVehicle.cpp** (1308L), **AIFlyingVehicle** (49L)
3. **PipeUser.cpp** (5673L), **Puppet.cpp** (6168L), **PuppetPhys.cpp** (728L), **PuppetRateOfDeath**
4. Phase 3 .cpp impls (Walkability → MNM → NavigationSystem → Pathfinding)
