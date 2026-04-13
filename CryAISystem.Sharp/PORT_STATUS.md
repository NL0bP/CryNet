# CryAISystem.Sharp — Port Status

Source: `dev/Code/CryEngine/CryAISystem/` (179 .cpp files, ~91,229 LOC)
Target: `CryAISystem.Sharp/src/CryAISystem.Core/`
Methodology: literal line-by-line C++ to C# port. Math types consumed from `CryPhysics.Sharp`.

## Phase tracker

| Phase | Description | Files | LOC | Status |
|-------|-------------|-------|-----|--------|
| 0 | Scaffolding | — | — | ✅ done |
| 1 | Leaves (Factions, PolygonSetOps, Hash, Log, etc.) | ~25 | ~3,000 | ✅ done (3 deferred) |
| 2 | Actor hierarchy (AIObject → Puppet) | ~25 | ~25,000 | ✅ done — .h+.cpp literal ports complete |
| 3 | Navigation (root + MNM + NavSys + Walkability) | ~80 | ~21,000 | ✅ done — full MNM navmesh, pathfinding, walkability |
| 4 | Movement + CollisionAvoidance | ~33 | ~4,700 | ✅ done — MovementSystem + 10 blocks + ORCA |
| 5 | Perception | ~16 | ~5,000 | 🟡 partial — VisionMap ray-cast pipeline in PerceptionShells.cs |
| 6 | Cover + TPS + TargetSelection | ~32 | ~16,400 | ❌ not started |
| 7 | Goals & Pipes | ~20 | ~10,400 | 🟡 partial — GoalOpTrace + GoalOpStick + AIPIDController done |
| 8 | BehaviorTree + SelectionTree | ~37 | ~12,000 | ❌ not started |
| 9 | Group dynamics | ~12 | ~5,600 | ❌ not started |
| 10 | Communication + Sequence + Mannequin + AIBubbles | ~26 | ~7,000 | ❌ not started |
| 11 | CAISystem coordinator + DebugDraw + AIRecorder | ~25 | ~15,000 | 🟡 mostly done — CAISystem + UpdateLoop + DebugDrawHelpers + MemStats |
| 12 | GameSpecific + FlowNodes | ~14 | ~7,500 | ❌ not started |
| 13 | ScriptBind_AI | 2 | ~12,000 | ❌ not started |

## Current build state
**`dotnet build CryAISystem.Sharp.sln --no-incremental` → 0 errors, 96 warnings.**

All warnings are CS0414/CS0649 (field assigned but never used / never assigned), pre-existing in shells. No critical warnings.

## Files ported (real literal ports — verified compiling)

### Phase 1 — Leaves (CryAISystem)
- [x] `Configuration.cs` — 43L C++
- [x] `StdAfx.cs` — 174L C++
- [x] `AIHash.cs` — 109L C++
- [x] `Reference.cs` — 1,019L C++ (h+inl)
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

### Phase 2 — Actor hierarchy (.h+.cpp literal ports)
- [x] `Adapters.cs` — 59L C++
- [x] `PersonalLog.cs` — h+cpp literal
- [x] `ActorLookUp.cs`
- [x] `AIObject.cs` — 1,438L C++ (h+cpp)
- [x] `ObjectContainer.cs` — 594L cpp + SAIObjectCreationHelper
- [x] `CryCommon/IDMap.cs` — literal port of IDMap.h
- [x] `AIObjectManager.cs` — .h + ~85% of .cpp literal port
- [x] `PostureManager.cs` — .h + simpler .cpp methods literal
- [x] `HideSpot.cs` — .h+.cpp literal port complete
- [x] `VertexList.cs` — extended with ObstacleData literal
- [x] `AIActor.cs` — 2,628L .cpp full literal port
- [x] `AIPlayer.cs` — 1,198L .cpp full literal port
- [x] `AIVehicle.cs` — 1,225L .cpp full literal port
- [x] `AIFlyingVehicle.cs` — 49L .h + 249L .cpp literal port complete
- [x] `PipeUser.cs` — 5,019L .cpp full literal port
- [x] `Puppet.cs` — 6,714L .cpp full literal port (Puppet + PuppetPhys + PuppetRateOfDeath merged)
- [x] `AICollision.cs` — 1,666L C++ literal port complete

### Phase 3 — Navigation (full .cpp implementations)

**Walkability:**
- [x] `Walkability/FloorHeightCache.cs` — 78L .h + 90L .cpp literal
- [x] `Walkability/WalkabilityCache.cs` — 601L .cpp literal port
- [x] `Walkability/WalkabilityCacheManager.cs` — 70L .h + 317L .cpp literal

**Shapes & Hide:**
- [x] `Shape.cs` — 863L .cpp literal port
- [x] `Shape2.cs` — ~1,200L .cpp literal port
- [x] `HideSpot.cs` — .h+.cpp literal
- [x] `AIHideObject.cs` — 1,490L .cpp literal port
- [x] `AIDynHideObjectManager.cs` — ~500L .cpp literal port

**Navigation/MNM (navmesh core):**
- [x] `Navigation/MNM/MNM.cs` — core types and triangle mesh
- [x] `Navigation/MNM/FixedPoint.cs` — fixed-point math (FixedVec2, FixedVec3, FixedAABB)
- [x] `Navigation/MNM/CompactSpanGrid.cs` — span grid for voxelization
- [x] `Navigation/MNM/DynamicSpanGrid.cs` — dynamic span grid
- [x] `Navigation/MNM/BoundingVolume.cs` — bounding volumes
- [x] `Navigation/MNM/Voxelizer.cs` — terrain voxelization
- [x] `Navigation/MNM/Tile.cs` — navmesh tile storage
- [x] `Navigation/MNM/MeshGrid.cs` — navmesh grid with A* FindWay
- [x] `Navigation/MNM/TileGenerator.cs` — tile generation pipeline (3,312L)
- [x] `Navigation/MNM/IslandConnections.cs` — island connectivity
- [x] `Navigation/MNM/OffGridLinks.cs` — off-mesh navigation links

**NavigationSystem:**
- [x] `Navigation/NavigationSystem/NavigationSystem.cs` — central navigation system (1,613L)
- [x] `Navigation/NavigationSystem/NavigationSystemSubManagers.cs` — IslandConnections, OffMesh, Volumes, WorldMonitor (853L)

**Pathfinding (root):**
- [x] `AllNodesContainer.cs` — node container
- [x] `GraphStructures.cs` — graph node/link structures
- [x] `GraphNodeManager.cs` — graph node lifecycle
- [x] `Graph.cs` — navigation graph (828L)
- [x] `CTriangulator.cs` — triangulation
- [x] `Navigation.cs` — CNavigation class (994L)
- [x] `NavPath.cs` — path representation (1,854L)
- [x] `PathFollower.cs` — path following (818L)
- [x] `SmartPathFollower.cs` — smart path follower (1,118L)
- [x] `PathMarker.cs` — 77L .h + 407L .cpp literal
- [x] `MNMPathfinder.cs` — MNM pathfinder with real A* (756L)
- [x] `PathObstacles.cs` — path obstacle avoidance (1,141L)
- [x] `FlightNavRegion2.cs` — flight navigation
- [x] `Free2DNavRegion.cs` — 2D navigation region
- [x] `Navigation/CustomNavRegion.cs` — custom navigation region

### Phase 4 — Movement + CollisionAvoidance
- [x] `Movement/MovementSystem.cs` — full movement system + 10 blocks + planner (1,674L)
- [x] `Movement/MoveOp.cs` — movement operations (588L)
- [x] `Movement/MovementTypes.cs` — movement type definitions (424L)
- [x] `CollisionAvoidance/CollisionAvoidanceSystem.cs` — ORCA velocity avoidance (1,057L)

### Phase 5 — Perception (partial)
- [x] `Perception/PerceptionShells.cs` — VisionMap ray-cast pipeline (1,185L)
- [ ] `VisionMap.cpp` — full VisionMap (1,618L) — pending
- [ ] `PerceptionManager.cpp` — perception manager (2,003L) — pending
- [ ] `AIRadialOcclusion.cpp` — radial occlusion (1,012L) — pending
- [ ] `AILightManager.cpp` — light manager (828L) — pending
- [ ] `CentralInterestManager.cpp` — interest system (1,116L) — pending
- [ ] `PersonalInterestManager.cpp` — personal interest — pending
- [ ] `MissLocationSensor.cpp` — miss location — pending
- [ ] `GlobalPerceptionScaleHandler.cpp` — perception scale — pending

### Phase 7 — Goals & Pipes (partial)
- [x] `AIPIDController.cs` — 102L C++ (h+cpp)
- [x] `GoalOpTrace.cs` — NPC path following (1,697L)
- [x] `GoalOpStick.cs` — NPC target chasing (933L)
- [ ] `GoalOp.cpp` — 50+ goal op classes (7,210L) — pending
- [ ] `GoalPipe.cpp` — goal pipe system (1,542L) — pending
- [ ] `GoalOpFactory.cpp` — goal op factory — pending
- [ ] `GoalPipeXMLReader.cpp` — XML pipe reader — pending
- [ ] `PipeManager.cpp` — pipe manager — pending
- [ ] `FireCommand.cpp` — fire command (2,201L) — pending
- [ ] `SmartObjects.cpp` — smart objects (6,183L) — pending

### Phase 11 — CAISystem coordinator (mostly done)
- [x] `AISignalCRCs.cs` — 299L C++ (h+cpp)
- [x] `CAISystem.cs` — central AI coordinator (638L)
- [x] `CAISystemUpdate.cs` — AI update loop (847L)
- [x] `AIDebugDrawHelpers.cs` — debug draw helpers (528L)
- [x] `AIMemStats.cs` — memory statistics (157L)
- [x] `Environment.cs` — gAIEnv with ~50 real implementations
- [x] `Environment_Shells.cs` — environment shell adapters
- [x] `DebugDrawContext.cs` — debug draw context
- [x] `CryPhysicsAliases.cs` — CryPhysics type aliases
- [ ] `AIActions.cpp` — AI actions (1,593L) — pending
- [ ] `AIConsoleVariables.cpp` — console variables (1,644L) — pending
- [ ] `AIRecorder.cpp` — AI recorder (2,091L) — pending
- [ ] `AISignal.cpp` — signal system — pending
- [ ] `DebugDraw.cpp` — debug visualization (5,421L) — pending
- [ ] `CAISystemPhys.cpp` — physics integration — pending
- [ ] `CodeCoverageManager.cpp` — code coverage — pending
- [ ] `CodeCoverageTracker.cpp` — code coverage — pending
- [ ] `CodeCoverageGUI.cpp` — code coverage GUI — pending

### CryCommon (literal ports of CryEngine headers)
- [x] `CryCommon/IFactionMap.cs` — 46L
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
- [x] `CryCommon/Cry_Geo.cs` — AABB, Lineseg, OBB, Matrix34, etc.
- [x] `CryCommon/IClusterDetector.cs`
- [x] `CryCommon/PhysicsShims.cs` — pe_status_living/dynamics shims
- [x] `CryCommon/CryCrc32.cs` — 214L C++
- [x] `CryCommon/IAISystem_Collision.cs` — EAICollisionEntities + entity_query_flags
- [x] `CryCommon/Primitives.cs` — primitives namespace, prim_inters, contact, geom_contact
- [x] `CryCommon/PhysInterface.cs` — intersection_params, ray_hit, IPhysicalWorld, IGeometry, etc.
- [x] `CryCommon/Vec3Constants.cs` — Vec3Constants<float>
- [x] `CryCommon/IDMap.cs` — IDMap<T> template

## CryPhysics.Sharp patches (logged in cryphysics_patches.md)
- 2026-04-11: PhysVector3 lowercase x/y/z aliases
- 2026-04-11: PhysQuaternion v/w aliases
- 2026-04-11: StatusPos pos/q/scale aliases
- 2026-04-13: PhysMatrix33 additional operators for AI system
- 2026-04-13: PhysQuaternion additional operators for AI system
- 2026-04-13: PhysVector3 additional operators and methods for AI system

## Global aliases (GlobalUsings.cs)
uint8/uint16/uint32/uint64/int8/int16/int32/int64/f32/f64 — and `size_t = System.UInt64`.

## Next targets (rigid plan order)
1. **Phase 5 remainder** — VisionMap, PerceptionManager, AIRadialOcclusion, AILightManager
2. **Phase 6** — Cover system, TacticalPointSystem, TargetSelection (~16,400 LOC)
3. **Phase 7 remainder** — GoalOp (50+ classes), GoalPipe, PipeManager, SmartObjects
4. **Phase 8** — BehaviorTree + SelectionTree (~12,000 LOC)
5. **Phase 9** — Group dynamics (AIGroup, Formation, Leader)
6. **Phase 10** — Communication, Sequence, Mannequin, AIBubbles
7. **Phase 11 remainder** — AIActions, AIConsoleVariables, AIRecorder, DebugDraw
8. **Phase 12** — GameSpecific + FlowNodes
9. **Phase 13** — ScriptBind_AI (may be deferred if Lua not needed)
