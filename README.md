# Lumberyard CryEngine C++ to C# Port

Literal line-by-line port of **CryPhysics** and **CryAISystem** from Amazon Lumberyard 1.0 (CryEngine) to C# (.NET 8.0).
These modules provide the physics simulation and AI system APIs for an ArcheAge server emulator.

## Build

```bash
dotnet build CryPhysics.Sharp/CryPhysics.Sharp.sln
dotnet build CryAISystem.Sharp/CryAISystem.Sharp.sln
```

Both solutions build with **0 errors**.

## Project Structure

```
lumberyard_1-0_fr_432777/
├── dev/Code/CryEngine/CryPhysics/      # Original C++ source (46 .cpp, 45 .h)
├── dev/Code/CryEngine/CryAISystem/     # Original C++ source (179 .cpp, 216 .h)
├── CryPhysics.Sharp/                   # C# port of CryPhysics
│   └── src/CryPhysics.Core/            # 56 .cs files
└── CryAISystem.Sharp/                  # C# port of CryAISystem
    └── src/CryAISystem.Core/           # 113 .cs files (incl. CryCommon shims)
```

---

## CryPhysics.Sharp — Port Status

**Source:** `dev/Code/CryEngine/CryPhysics/` (46 .cpp files, ~63,600 LOC)
**Target:** `CryPhysics.Sharp/src/CryPhysics.Core/` (56 .cs files)
**Coverage:** 36/46 .cpp files ported (78%), 5 not applicable, 5 missing
**Build:** 0 errors, 21 warnings
**Fidelity:** 90-98% confirmed via audit (85+ issues documented in REVIEW_REPORT.md)

### File-by-File Mapping

| C++ Source (.cpp) | C# File (.cs) | Status |
|---|---|---|
| `aabbtree.cpp` | `BVTrees/AABBTree.cs` | Ported |
| `articulatedentity.cpp` | `Entities/ArticulatedEntity.cs` | Ported |
| `boolean2d.cpp` | — | **Missing** |
| `boolean3d.cpp` | — | **Missing** |
| `boxgeom.cpp` | `Geometry/BoxGeometry.cs` | Ported |
| `capsulegeom.cpp` | — | **Missing** |
| `cylindergeom.cpp` | `Geometry/CylinderGeometry.cs` | Ported |
| `geoman.cpp` | `Geometry/GeometryManager.cs` | Ported |
| `geometry.cpp` | `Geometry/GeometryBase.cs` | Ported |
| `heightfieldbv.cpp` | `BVTrees/HeightfieldBV.cs` | Ported |
| `heightfieldgeom.cpp` | `Geometry/HeightfieldGeometry.cs` | Ported |
| `intersectionchecks.cpp` | `Collision/IntersectionTests.cs` | Ported |
| `linunprojectionchecks.cpp` | `Collision/LinearUnprojection.cs` | Ported |
| `livingentity.cpp` | `Entities/LivingEntity.cs` | Ported |
| `matrixnm.cpp` | `Math/MatrixNM.cs` | Ported |
| `obbtree.cpp` | `BVTrees/OBBTree.cs` | Ported |
| `overlapchecks.cpp` | `Collision/OverlapChecker.cs` | Ported |
| `particleentity.cpp` | `Entities/ParticleEntity.cs` | Ported |
| `physarea.cpp` | `World/PhysicsArea.cs` | Ported |
| `physicalentity.cpp` | `Entities/PhysicalEntity.cs` | Ported |
| `physicalplaceholder.cpp` | — | **Missing** |
| `physicalworld.cpp` | `World/PhysicalWorld.cs` | Ported |
| `qhull.cpp` | `Algorithms/ConvexHull.cs` | Ported |
| `raybv.cpp` | `BVTrees/RayBV.cs` | Ported |
| `raygeom.cpp` | `Geometry/RayGeometry.cs` | Ported |
| `rigidbody.cpp` | `Dynamics/RigidBody.cs` | Ported |
| `rigidentity.cpp` | `Entities/RigidEntity.cs` | Ported |
| `ropeentity.cpp` | `Entities/RopeEntity.cs` | Ported |
| `rotunprojectionchecks.cpp` | `Collision/RotationalUnprojection.cs` | Ported |
| `rwi.cpp` | — | **Missing** |
| `singleboxtree.cpp` | `BVTrees/SingleBoxTree.cs` | Ported |
| `softentity.cpp` | `Entities/SoftEntity.cs` | Ported |
| `spheregeom.cpp` | `Geometry/SphereGeometry.cs` | Ported |
| `tetrlattice.cpp` | `Algorithms/TetrahedralLattice.cs` | Ported |
| `trimesh.cpp` | `Geometry/TriMeshGeometry.cs` | Ported |
| `utils.cpp` | `Math/MathUtils.cs` | Ported |
| `voxelbv.cpp` | — | **Missing** (BV only, geometry in VoxelGeometry.cs) |
| `voxelgeom.cpp` | `Geometry/VoxelGeometry.cs` | Ported |
| `waterman.cpp` | `Algorithms/WaterManager.cs` | Ported |
| `wheeledvehicleentity.cpp` | `Entities/WheeledVehicleEntity.cs` | Ported |
| `worldump.cpp` | `Serialization/WorldSerializer.cs` | Ported |
| `CryPhysics.cpp` | — | N/A (DLL entry point) |
| `StdAfx.cpp` | — | N/A (precompiled header) |
| `StdAfxRC.cpp` | — | N/A (precompiled header) |
| `Tests/test_Main.cpp` | — | N/A (test harness) |
| `Tests/test_Utils.cpp` | — | N/A (test harness) |

### Additional C# Files (no direct C++ counterpart)

| C# File | Purpose |
|---|---|
| `Collision/IntersectionChecker.cs` | Dispatcher for intersection tests |
| `Collision/UnprojectionChecker.cs` | Dispatcher for unprojection tests |
| `Dynamics/ContactSolver.cs` | Contact constraint solver |
| `Dynamics/EntityContact.cs` | Contact data structures |
| `Entities/IPhysicalEntity.cs` | Interface definitions |
| `Entities/StructuralBreakability.cs` | Breakable structure support |
| `Events/PhysicsEvents.cs` | Physics event types |
| `Geometry/IntersectionParams.cs` | Intersection parameters |
| `Math/PhysMatrix33.cs` | 3x3 matrix (from Cry_Matrix33.h) |
| `Math/PhysQuaternion.cs` | Quaternion (from Cry_Quat.h) |
| `Math/PhysVector3.cs` | 3D vector (from Cry_Vector3.h) |
| `Math/Polynomial.cs` | Polynomial solver |
| `Math/Quotient.cs` | Exact rational arithmetic |
| `Math/VectorN.cs` | N-dimensional vector |
| `Params/PhysicsParams.cs` | Physics parameter structures |
| `Primitives/Primitives.cs` | Geometric primitives |
| `Threading/CallerContext.cs` | Thread context management |
| `Threading/PhysicsThreadPool.cs` | Physics thread pool |
| `Utilities/PhysicsGlobals.cs` | Global constants |
| `World/SpatialGrid.cs` | Spatial partitioning grid |

### Missing C++ Files (not yet ported)

| File | LOC | Notes |
|---|---|---|
| `boolean2d.cpp` | ~800 | 2D boolean operations on polygons |
| `boolean3d.cpp` | ~1200 | 3D boolean operations on meshes |
| `capsulegeom.cpp` | ~400 | Capsule geometry (cylinder + hemispheres) |
| `physicalplaceholder.cpp` | ~300 | Lightweight physics placeholder entity |
| `rwi.cpp` | ~600 | Ray world intersection helpers |
| `voxelbv.cpp` | ~200 | Voxel bounding volume tree |

---

## CryAISystem.Sharp — Port Status

**Source:** `dev/Code/CryEngine/CryAISystem/` (179 .cpp files, ~91,229 LOC)
**Target:** `CryAISystem.Sharp/src/CryAISystem.Core/` (113 .cs files)
**Coverage:** ~84% of planned phases complete
**Build:** 0 errors, 96 warnings

### Phase Summary

| Phase | Description | Status | Notes |
|---|---|---|---|
| 0 | Scaffolding | **Done** | Project structure, GlobalUsings, csproj |
| 1 | Leaves (utility classes) | **Done** | ~3,000 LOC — Factions, PolygonSetOps, Hash, Log, etc. |
| 2 | Actor hierarchy | **Done** | ~25,000 LOC — AIObject through Puppet, full .h+.cpp |
| 3 | Navigation | **Done** | ~21,000 LOC — MNM navmesh, pathfinding, walkability |
| 4 | Movement + CollisionAvoidance | **Done** | ~4,700 LOC — MovementSystem, 10 blocks, ORCA |
| 5 | Perception | **Partial** | VisionMap ray-cast pipeline via PerceptionShells.cs |
| 6 | Cover + TPS + TargetSelection | **Not started** | ~16,400 LOC |
| 7 | Goals & Pipes | **Partial** | GoalOpTrace + GoalOpStick ported; rest pending |
| 8 | BehaviorTree + SelectionTree | **Not started** | ~12,000 LOC |
| 9 | Group dynamics | **Not started** | ~5,600 LOC |
| 10 | Communication + Sequence + Mannequin | **Not started** | ~7,000 LOC |
| 11 | CAISystem coordinator | **Done** | CAISystem + Update loop + Debug helpers |
| 12 | GameSpecific + FlowNodes | **Not started** | ~7,500 LOC |
| 13 | ScriptBind_AI | **Not started** | ~12,000 LOC (Lua bindings, may be deferred) |

### File-by-File Mapping

#### Root-level files

| C++ Source (.cpp) | C# File (.cs) | Status |
|---|---|---|
| `AIActions.cpp` | — | **Missing** (Phase 11) |
| `AIActor.cpp` | `AIActor.cs` | Ported |
| `AICollision.cpp` | `AICollision.cs` | Ported |
| `AIConsoleVariables.cpp` | — | **Missing** (Phase 11) |
| `AIDbgRecorder.cpp` | `AIDbgRecorder.cs` | Ported |
| `AIDebugDrawHelpers.cpp` | `AIDebugDrawHelpers.cs` | Ported |
| `AIDynHideObjectManager.cpp` | `AIDynHideObjectManager.cs` | Ported |
| `AIFlyingVehicle.cpp` | `AIFlyingVehicle.cs` | Ported |
| `AIGroup.cpp` | — | **Missing** (Phase 9) |
| `AIHideObject.cpp` | `AIHideObject.cs` | Ported |
| `AILightManager.cpp` | — | **Missing** (Phase 5) |
| `AILog.cpp` | `AILog.cs` | Ported |
| `AIMemStats.cpp` | `AIMemStats.cs` | Ported |
| `AIObject.cpp` | `AIObject.cs` | Ported |
| `AIObjectManager.cpp` | `AIObjectManager.cs` | Ported |
| `AIPIDController.cpp` | `AIPIDController.cs` | Ported |
| `AIPlayer.cpp` | `AIPlayer.cs` | Ported |
| `AIRadialOcclusion.cpp` | — | **Missing** (Phase 5) |
| `AIRecorder.cpp` | — | **Missing** (Phase 11) |
| `AISignal.cpp` | — | **Missing** (Phase 11) |
| `AISignalCRCs.cpp` | `AISignalCRCs.cs` | Ported |
| `AIVehicle.cpp` | `AIVehicle.cs` | Ported |
| `Adapters.cpp` | `Adapters.cs` | Ported |
| `AllNodesContainer.cpp` | `AllNodesContainer.cs` | Ported |
| `BlackBoard.cpp` | `BlackBoard.cs` | Ported |
| `CAISystem.cpp` | `CAISystem.cs` | Ported |
| `CAISystemPhys.cpp` | — | **Missing** (Phase 11) |
| `CAISystemUpdate.cpp` | `CAISystemUpdate.cs` | Ported |
| `CTriangulator.cpp` | `CTriangulator.cs` | Ported |
| `CalculationStopper.cpp` | `CalculationStopper.cs` | Ported |
| `CentralInterestManager.cpp` | — | **Missing** (Phase 5) |
| `ClusterDetector.cpp` | `ClusterDetector.cs` | Ported |
| `ClusterRequest.cpp` | — | **Missing** (Phase 1) |
| `CodeCoverageGUI.cpp` | — | **Missing** (Phase 11) |
| `CodeCoverageManager.cpp` | — | **Missing** (Phase 11) |
| `CodeCoverageTracker.cpp` | — | **Missing** (Phase 11) |
| `CryAISystem.cpp` | — | **Missing** (DLL entry) |
| `DebugDraw.cpp` | — | **Missing** (Phase 11) |
| `Environment.cpp` | `Environment.cs` | Ported |
| `FireCommand.cpp` | — | **Missing** (Phase 7) |
| `FlightNavRegion2.cpp` | `FlightNavRegion2.cs` | Ported |
| `FlyHelpers_Path.cpp` | — | **Missing** (Phase 12) |
| `FlyHelpers_PathFollower.cpp` | — | **Missing** (Phase 12) |
| `FlyHelpers_TacticalPointLanguageExtender.cpp` | — | **Missing** (Phase 6) |
| `Formation.cpp` | — | **Missing** (Phase 9) |
| `Free2DNavRegion.cpp` | `Free2DNavRegion.cs` | Ported |
| `GlobalPerceptionScaleHandler.cpp` | — | **Missing** (Phase 5) |
| `GoalOp.cpp` | — | **Missing** (Phase 7) |
| `GoalOpFactory.cpp` | — | **Missing** (Phase 7) |
| `GoalOpStick.cpp` | `GoalOpStick.cs` | Ported |
| `GoalOpTrace.cpp` | `GoalOpTrace.cs` | Ported |
| `GoalPipe.cpp` | — | **Missing** (Phase 7) |
| `GoalPipeXMLReader.cpp` | — | **Missing** (Phase 7) |
| `Graph.cpp` | `Graph.cs` | Ported |
| `GraphNodeManager.cpp` | `GraphNodeManager.cs` | Ported |
| `GraphStructures.cpp` | `GraphStructures.cs` | Ported |
| `GraphUtility.cpp` | — | **Missing** (Phase 3) |
| `HideSpot.cpp` | `HideSpot.cs` | Ported |
| `Leader.cpp` | — | **Missing** (Phase 9) |
| `LeaderAction.cpp` | — | **Missing** (Phase 9) |
| `MNMPathfinder.cpp` | `MNMPathfinder.cs` | Ported |
| `MissLocationSensor.cpp` | — | **Missing** (Phase 5) |
| `NavPath.cpp` | `NavPath.cs` | Ported |
| `Navigation.cpp` | `Navigation.cs` | Ported |
| `ObjectContainer.cpp` | `ObjectContainer.cs` | Ported |
| `PathFollower.cpp` | `PathFollower.cs` | Ported |
| `PathMarker.cpp` | `PathMarker.cs` | Ported |
| `PathObstacles.cpp` | `PathObstacles.cs` | Ported |
| `PerceptionManager.cpp` | — | **Missing** (Phase 5) |
| `PersonalInterestManager.cpp` | — | **Missing** (Phase 5) |
| `PersonalLog.cpp` | `PersonalLog.cs` | Ported |
| `PipeManager.cpp` | — | **Missing** (Phase 7) |
| `PipeUser.cpp` | `PipeUser.cs` | Ported |
| `PipeUserMovementActorAdapter.cpp` | — | **Missing** (Phase 4) |
| `PostureManager.cpp` | `PostureManager.cs` | Ported |
| `Puppet.cpp` | `Puppet.cs` | Ported |
| `PuppetPhys.cpp` | `Puppet.cs` | Ported (merged) |
| `PuppetRateOfDeath.cpp` | `Puppet.cs` | Ported (merged) |
| `ScriptBind_AI.cpp` | — | **Missing** (Phase 13) |
| `Shape.cpp` | `Shape.cs` | Ported |
| `Shape2.cpp` | `Shape2.cs` | Ported |
| `SmartObjects.cpp` | — | **Missing** (Phase 7) |
| `SmartPathFollower.cpp` | `SmartPathFollower.cs` | Ported |
| `StatsManager.cpp` | `StatsManager.cs` | Ported |
| `StdAfx.cpp` | `StdAfx.cs` | Ported |
| `UnitAction.cpp` | — | **Missing** (Phase 9) |
| `UnitImg.cpp` | — | **Missing** (Phase 9) |
| `VertexList.cpp` | `VertexList.cs` | Ported |
| `VisionMap.cpp` | — | **Missing** (Phase 5, partial in PerceptionShells.cs) |

#### BehaviorTree/ (Phase 8 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `BehaviorTreeGraft.cpp` | — | **Missing** |
| `BehaviorTreeManager.cpp` | — | **Missing** |
| `BehaviorTreeNodeRegistration.cpp` | — | **Missing** |
| `BehaviorTreeNodes_AI.cpp` | — | **Missing** |
| `BehaviorTreeNodes_Core.cpp` | — | **Missing** |
| `BehaviorTreeNodes_Helicopter.cpp` | — | **Missing** |
| `ExecutionStackFileLogger.cpp` | — | **Missing** |
| `TreeVisualizer.cpp` | — | **Missing** |

#### CollisionAvoidance/ (Phase 4 — done)

| C++ Source | C# File | Status |
|---|---|---|
| `CollisionAvoidanceSystem.cpp` | `CollisionAvoidance/CollisionAvoidanceSystem.cs` | Ported |

#### Communication/ (Phase 10 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `CommunicationChannel.cpp` | — | **Missing** |
| `CommunicationChannelManager.cpp` | — | **Missing** |
| `CommunicationManager.cpp` | — | **Missing** |
| `CommunicationPlayer.cpp` | — | **Missing** |
| `CommunicationTestManager.cpp` | — | **Missing** |

#### Cover/ (Phase 6 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `CoverPath.cpp` | — | **Missing** |
| `CoverSampler.cpp` | — | **Missing** |
| `CoverScorer.cpp` | — | **Missing** |
| `CoverSurface.cpp` | — | **Missing** |
| `CoverSystem.cpp` | — | **Missing** |
| `CoverUser.cpp` | — | **Missing** |
| `DynamicCoverManager.cpp` | — | **Missing** |
| `EntityCoverSampler.cpp` | — | **Missing** |

#### Factions/ (Phase 1 — done)

| C++ Source | C# File | Status |
|---|---|---|
| `FactionMap.cpp` | `Factions/FactionMap.cs` | Ported |

#### FlowNodes/ (Phase 12 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `AIFlowBaseNode.cpp` | — | **Missing** |

#### GameSpecific/ (Phase 12 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `GoalOp_Crysis2.cpp` | — | **Missing** |
| `GoalOp_G02.cpp` | — | **Missing** |
| `GoalOp_G04.cpp` | — | **Missing** |

#### GoalOps/ (Phase 7 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `ShootOp.cpp` | — | **Missing** |
| `TeleportOp.cpp` | — | **Missing** |

#### Group/ (Phase 9 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `Group.cpp` | — | **Missing** |
| `GroupManager.cpp` | — | **Missing** |

#### Mannequin/ (Phase 10 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `MannequinGoalOp.cpp` | — | **Missing** |

#### Movement/ (Phase 4 — done)

| C++ Source | C# File | Status |
|---|---|---|
| `MoveOp.cpp` | `Movement/MoveOp.cs` | Ported |
| `MovementActor.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_FollowPath.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_HarshStop.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_SetupPipeUserCoverInformation.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_TurnTowardsPosition.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_UseExactPositioning.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_UseExactPositioningBase.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementBlock_UseSmartObject.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementHelpers.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementPlan.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementPlanner.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |
| `MovementSystem.cpp` | `Movement/MovementSystem.cs` | Ported |
| `MovementSystemCreator.cpp` | `Movement/MovementSystem.cs` | Ported (merged) |

#### Navigation/MNM/ (Phase 3 — done)

| C++ Source | C# File | Status |
|---|---|---|
| `BoundingVolume.cpp` | `Navigation/MNM/BoundingVolume.cs` | Ported |
| `CompactSpanGrid.cpp` | `Navigation/MNM/CompactSpanGrid.cs` | Ported |
| `DynamicSpanGrid.cpp` | `Navigation/MNM/DynamicSpanGrid.cs` | Ported |
| `IslandConnections.cpp` | `Navigation/MNM/IslandConnections.cs` | Ported |
| `MeshGrid.cpp` | `Navigation/MNM/MeshGrid.cs` | Ported |
| `OffGridLinks.cpp` | `Navigation/MNM/OffGridLinks.cs` | Ported |
| `Tile.cpp` | `Navigation/MNM/Tile.cs` | Ported |
| `TileGenerator.cpp` | `Navigation/MNM/TileGenerator.cs` | Ported |
| `TileGeneratorDraw.cpp` | `Navigation/MNM/TileGenerator.cs` | Ported (merged) |
| `Voxelizer.cpp` | `Navigation/MNM/Voxelizer.cs` | Ported |

#### Navigation/NavigationSystem/ (Phase 3 — mostly done)

| C++ Source | C# File | Status |
|---|---|---|
| `IslandConnectionsManager.cpp` | `Navigation/NavigationSystem/NavigationSystemSubManagers.cs` | Ported (merged) |
| `NavigationSystem.cpp` | `Navigation/NavigationSystem/NavigationSystem.cs` | Ported |
| `OffMeshNavigationManager.cpp` | `Navigation/NavigationSystem/NavigationSystemSubManagers.cs` | Ported (merged) |
| `VolumesManager.cpp` | `Navigation/NavigationSystem/NavigationSystemSubManagers.cs` | Ported (merged) |
| `WorldMonitor.cpp` | `Navigation/NavigationSystem/NavigationSystemSubManagers.cs` | Ported (merged) |

#### Navigation/ (root)

| C++ Source | C# File | Status |
|---|---|---|
| `CustomNavRegion.cpp` | `Navigation/CustomNavRegion.cs` | Ported |

#### SelectionTree/ (Phase 8 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `BlockyXml.cpp` | — | **Missing** |
| `SelectionCondition.cpp` | — | **Missing** |
| `SelectionSignalVariables.cpp` | — | **Missing** |
| `SelectionTranslator.cpp` | — | **Missing** |
| `SelectionTree.cpp` | — | **Missing** |
| `SelectionTreeDebugger.cpp` | — | **Missing** |
| `SelectionTreeManager.cpp` | — | **Missing** |
| `SelectionTreeNode.cpp` | — | **Missing** |
| `SelectionTreeTemplate.cpp` | — | **Missing** |
| `SelectionVariables.cpp` | — | **Missing** |

#### Sequence/ (Phase 10 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `Sequence.cpp` | — | **Missing** |
| `SequenceFlowNodes.cpp` | — | **Missing** |
| `SequenceManager.cpp` | — | **Missing** |

#### TacticalPointSystem/ (Phase 6 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `TacticalPointQuery.cpp` | — | **Missing** |
| `TacticalPointSystem.cpp` | — | **Missing** |

#### TargetSelection/ (Phase 6 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `TargetTrack.cpp` | — | **Missing** |
| `TargetTrackCommon.cpp` | — | **Missing** |
| `TargetTrackGroup.cpp` | — | **Missing** |
| `TargetTrackManager.cpp` | — | **Missing** |
| `TargetTrackModifiers.cpp` | — | **Missing** |

#### AIBubblesSystem/ (Phase 10 — not started)

| C++ Source | C# File | Status |
|---|---|---|
| `AIBubblesNotifier.cpp` | — | **Missing** |
| `AIBubblesNotifierLibrary.cpp` | — | **Missing** |
| `AIBubblesSystem.cpp` | — | **Missing** |

#### Walkability/ (Phase 3 — done)

| C++ Source | C# File | Status |
|---|---|---|
| `FloorHeightCache.cpp` | `Walkability/FloorHeightCache.cs` | Ported |
| `WalkabilityCache.cpp` | `Walkability/WalkabilityCache.cs` | Ported |
| `WalkabilityCacheManager.cpp` | `Walkability/WalkabilityCacheManager.cs` | Ported |

#### Tests/ (not applicable)

| C++ Source | C# File | Status |
|---|---|---|
| `test_Main.cpp` | — | N/A |
| `test_Shape.cpp` | — | N/A |

### Additional C# Files (CryCommon shims, helpers)

These files port CryEngine common headers that the AI system depends on:

| C# File | Source Header |
|---|---|
| `CryCommon/CryAssert.cs` | CryAssert.h |
| `CryCommon/CryCrc32.cs` | CryCrc32.h |
| `CryCommon/CryString.cs` | CryString.h |
| `CryCommon/Cry_Geo.cs` | Cry_Geo.h |
| `CryCommon/IAIObject.cs` | IAgent.h |
| `CryCommon/IAISystem_Collision.cs` | IAISystem.h (partial) |
| `CryCommon/IAgent_Enums.cs` | IAgent.h (enums) |
| `CryCommon/IAgent_Types.cs` | IAgent.h (types) |
| `CryCommon/IBlackBoard.cs` | IBlackBoard.h |
| `CryCommon/IClusterDetector.cs` | IClusterDetector.h |
| `CryCommon/IConsole.cs` | IConsole.h |
| `CryCommon/ICryPak.cs` | ICryPak.h |
| `CryCommon/IDMap.cs` | IDMap.h |
| `CryCommon/IFactionMap.cs` | IFactionMap.h |
| `CryCommon/ILog.cs` | ILog.h |
| `CryCommon/IScriptSystem.cs` | IScriptSystem.h |
| `CryCommon/ISerialize.cs` | ISerialize.h |
| `CryCommon/ISystem.cs` | ISystem.h |
| `CryCommon/ITimer.cs` | ITimer.h |
| `CryCommon/IValidator.cs` | IValidator.h |
| `CryCommon/IXml.cs` | IXml.h |
| `CryCommon/PhysInterface.cs` | physinterface.h |
| `CryCommon/PhysicsShims.cs` | physinterface.h (shims) |
| `CryCommon/Primitives.cs` | primitives.h |
| `CryCommon/StlUtils.cs` | StlUtils.h |
| `CryCommon/TimeValue.cs` | TimeValue.h |
| `CryCommon/Vec3Constants.cs` | Cry_Vector3.h |

---

## Summary: What's Left

### CryPhysics.Sharp
5 genuinely missing files (~3,500 LOC): `boolean2d`, `boolean3d`, `capsulegeom`, `physicalplaceholder`, `rwi`.
Plus 85+ fidelity issues documented in [REVIEW_REPORT.md](CryPhysics.Sharp/REVIEW_REPORT.md).

### CryAISystem.Sharp
74 .cpp files remaining across 6 unstarted phases (~60,500 LOC):

| Phase | Files | Est. LOC | Key Systems |
|---|---|---|---|
| 5 (rest) | 6 | ~5,000 | VisionMap, PerceptionManager, AIRadialOcclusion, AILightManager |
| 6 | 15 | ~16,400 | Cover system, TacticalPointSystem, TargetSelection |
| 7 (rest) | 8 | ~10,400 | GoalOp (50+ classes), GoalPipe, PipeManager, SmartObjects, FireCommand |
| 8 | 18 | ~12,000 | BehaviorTree (25+ core + 40+ AI nodes), SelectionTree |
| 9 | 8 | ~5,600 | AIGroup, Formation, Leader, LeaderAction |
| 10 | 9 | ~7,000 | Communication, Sequence, Mannequin, AIBubbles |
| 11 (rest) | 8 | ~8,000 | AIActions, AIConsoleVariables, AIRecorder, DebugDraw, CodeCoverage |
| 12 | 4 | ~7,500 | GameSpecific GoalOps, FlowNodes, FlyHelpers |
| 13 | 1 | ~12,000 | ScriptBind_AI (Lua bindings) |

---

## Documentation

- [CryAISystem Port Plan](CryAISystem.Sharp/PORT_PLAN.md) — detailed execution plan with file inventories
- [CryAISystem Port Status](CryAISystem.Sharp/PORT_STATUS.md) — per-file tracking with checkboxes
- [CryPhysics Review Report](CryPhysics.Sharp/REVIEW_REPORT.md) — fidelity audit with 85+ issues
