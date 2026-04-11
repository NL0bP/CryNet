# Deferred files (compile errors — not blocking)

Per user rules: if a file does not compile when translated literally from C++,
let it break. Record it here and move on. Do NOT stub or work around.

Format:
`- <path>.cs — <error count> — <short reason / dep>`

## Phase 1 — deferred to later phases

- `AIMemStats.cpp` — depende de CAISystem, Puppet, AIVehicle, AIPlayer, GoalPipe, GoalOp, AStarSolver, WorldOctree, PerceptionManager, ObjectContainer, NavigationSystem (todas Phase 2/3/5/7/11). Defer to Phase 11.
- `AIDebugDrawHelpers.{h,cpp}` — depende de IRenderAuxGeom + IAIDebugRenderer + Cry_Camera + Vec3 helpers (~400L). Defer to Phase 11.
- `CodeCoverageManager.{h,cpp}` / `CodeCoverageTracker.{h,cpp}` / `CodeCoverageGUI.{h,cpp}` — debug-only. Defer to Phase 11.

## Phase 2 — Actor hierarchy

### ✅ RESOLVED 2026-04-11 (session 3)
- `AICollision.cs` — literal port now compiles. All deps (CryCommon physinterface subset, primitives, Vec3Constants, OBB, Matrix34, Lineseg, RayCastRequest/Result, IRayCaster, IWalkabilityCacheManager, IPhysicalWorld, gEnv.pPhysicalWorld, AIConsoleVariables fields, CDebugDrawContext draw methods) ported literally. CryPhysics.Sharp patched (logged in cryphysics_patches.md). `<Compile Remove>` lifted from csproj.

### Original entry (kept for history)
- `AICollision.cs` — **literal port complete on disk (1666L C++ -> 1213L C#), build-excluded via csproj `<Compile Remove>`. 169 errors when included.**
  - Sessão 2026-04-11 (turn 2). File at `src/CryAISystem.Core/AICollision.cs`.
  - Faltam (todas exigem ports literais CryCommon/CryPhysics adicionais):
    - `primitives.sphere/box/cylinder/capsule` com campos lowercase (.center/.r/.axis/.hh/.size/.Basis/.bOriented/.type) — patch CryPhysics.Sharp para expor aliases C++-style ou port literal de primitives.h
    - `intersection_params` com campos bNoBorder/bNoAreaContacts/bNoIntersection/bStopAtFirstTri/bThreadSafe — patch CryPhysics.Sharp/Geometry/IntersectionParams.cs para expor nomes C++-style
    - `ray_hit`, `geom_contact`, `geom_world_data`, `pe_status_nparts`, `pe_status_pos.BBox` — patches CryPhysics.Sharp ou shims CryCommon/PhysicsShims.cs adicionais
    - `Vec3Constants.fVec3_Zero/fVec3_OneZ/fVec3_OneX/fVec3_OneY` — port literal de Cry_Vector3.h `Vec3Constants<float>` template
    - `IPhysicalWorld.PrimitiveWorldIntersection`, `.GetEntitiesInBox`, `.RayTraceEntity`, `.CollideEntityWithBeam`, `.CollideEntityWithPrimitive`, `.GetGeomManager()`, `.GetPhysUtils().DeletePointer` — port literal de IPhysicalWorld interface (physinterface.h)
    - `IPhysicalWorld.SPWIParams` (struct interno) — port literal
    - `gEnv.pPhysicalWorld` — adicionar campo no SSystemGlobalEnvironment shim
    - `gAIEnv.pWalkabilityCacheManager` — promover shell para campo em gAIEnv
    - `gAIEnv.CVars.DebugCheckWalkability` / `.CheckWalkabilityOptimalSectionLength` — adicionar campos no AIConsoleVariablesShell
    - `CDebugDrawContext.DrawCylinder/.DrawCone/.DrawLine/.DrawOBB` — port literal de DebugDrawContext.h métodos
    - `Lineseg(start, end)` ctor — port literal de Cry_Geo.h Lineseg
    - `AABB.RESET`, `AABB.IsIntersectBox`, `AABB(AABB.RESET)` — patch Cry_Geo CryPhysics.Sharp
    - `Vec2(Vec3)` ctor — patch CryPhysics.Sharp PhysVector2
    - `PhysVector3.NormalizeSafe`, `.GetLengthSquared2D`, `.IsZero`, `.IsValid`, `.IsEquivalent`, `.len2`, `.Set`, `.Cross`, `.Dot`, `.GetNormalizedSafe`, `.Normalize` — patch CryPhysics.Sharp PhysVector3
    - `PhysMatrix33.TransformVector`, `.Transpose`, `Matrix33(Matrix34)` ctor — patch CryPhysics.Sharp
    - `Matrix34.GetTranslation`, `.GetColumn1`, `.CreateTranslationMat` — port literal de Cry_Matrix34.h
    - `OBB.CreateOBB`, `OBB.SetOBB` — port literal de Cry_Geo.h
    - `RayCastRequest`, `RayCastResult`, `IRayCaster.Cast(RayCastRequest)` returning `RayCastResult` — port literal de IRayCaster (CryAISystem.h ou CryCommon)
    - `WriteLockCond` — port literal de MultiThread.h
    - `Overlap.Lineseg_Polygon2D` — port literal de Cry_GeoOverlap.h
    - `IGeometry`, `CAISystem.m_walkabilityGeometryBox` — campo no CAISystem
    - `EBoundingBoxDrawStyle.eBBD_Faceted` — port literal enum
  - **Plano Plano-B**: ao invés de fixar tudo de uma vez, próxima sessão escolhe sub-grupos: (a) patches CryPhysics.Sharp para expor aliases C++-style; (b) port literal parcial de physinterface.h (struct primitives + IPhysicalWorld interface subset); (c) port literal de Cry_Vector3.h Vec3Constants. Cada sub-grupo destrava um lote de erros em AICollision.cs. Quando todos resolvidos, remover `<Compile Remove>` no csproj.
