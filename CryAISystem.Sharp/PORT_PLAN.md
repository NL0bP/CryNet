# CryAISystem.Sharp — Comprehensive Execution Plan

> **Audience**: a downstream porting agent that will execute the literal C++ → C# port of the CryAISystem module from Lumberyard. This document is self-contained — read it once and you have everything you need to start.

---

## 0. Hard rules (NON-NEGOTIABLE — user instructions)

1. **Literal line-by-line C++ → C# translation.** Sem refactor, sem melhorias, sem idiomas C# "mais limpos", sem error handling adicional, sem renomear símbolos, sem null checks novos. Se o C++ faz X, o C# faz X. Cada `class` vira `class`, cada `struct` vira `struct`, cada método com seu corpo statement-por-statement.
2. **Se não compilar, deixe quebrar.** Proibido criar stubs, placeholders, fake glue ou improvisar para "tapar buraco". Arquivo quebrado vai para `deferred.md` e segue-se em frente.
3. **Plano B aprovado pelo usuário (2026-04-11):** quando um arquivo do CryAISystem precisar de tipos do CryCommon, **traduzir literalmente o header CryCommon também**. Translação literal de header NÃO é stub — é parte do port. Headers CryCommon traduzidos vão em `src/CryAISystem.Core/CryCommon/`.
4. **Math/física vem do CryPhysics.Sharp.** O sibling em `c:/Users/Taka/Downloads/lumberyard_1-0_fr_432777/CryPhysics.Sharp/` já tem `PhysVector3` (Vec3), `PhysVector2` (Vec2), `Vector2i`, `PhysMatrix33` (Matrix33), `PhysQuaternion` (Quat), `Diag33`, `AABB`, `primitives::sphere`, `primitives::box`, `ray_hit`, etc. Use `using` aliases no topo de cada arquivo para preservar os nomes C++ literais (`using Vec3 = CryPhysics.Math.PhysVector3;`).
5. **Reconciliação CryPhysics.Sharp.** Antes de portar um arquivo, abrir cada tipo CryPhysics.Sharp que ele consome e diff campo-a-campo / método-a-método contra o header C++ original (`Cry_Vector3.h`, `Cry_Math.h`, `Cry_Geo.h`, `primitives.h`). Se houver divergência → **patchar CryPhysics.Sharp para bater com o C++** (nunca contornar no lado AI). Registrar cada patch em `cryphysics_patches.md`.
6. **Revisão linha-a-linha após cada arquivo.** Side-by-side C# vs C++. Checklist obrigatório:
   - (a) todos os membros presentes
   - (b) corpo de cada método statement-por-statement
   - (c) constantes/enums exatos
   - (d) zero validação adicional
   - (e) zero símbolo renomeado
7. **Verificação build-only.** Sem testes de runtime. Erros de compilação **não são bloqueantes** — registrar arquivo + contagem em `deferred.md` e continuar.

---

## 1. Project layout (já existe — não recriar)

```
c:/Users/Taka/Downloads/lumberyard_1-0_fr_432777/
├── CryPhysics.Sharp/                          # sibling — math/physics types ja portadas
│   └── src/CryPhysics.Core/
│       └── Math/{PhysVector3,PhysMatrix33,PhysQuaternion,...}.cs
└── CryAISystem.Sharp/                         # ESTE projeto
    ├── CryAISystem.Sharp.sln                  # ja inclui CryPhysics.Core
    ├── PORT_PLAN.md                           # ESTE arquivo
    ├── PORT_STATUS.md                         # tracker fase-a-fase
    ├── deferred.md                            # arquivos com erro de compilacao
    ├── cryphysics_patches.md                  # patches feitos no CryPhysics.Sharp
    └── src/CryAISystem.Core/
        ├── CryAISystem.Core.csproj            # net8.0, ProjectReference -> CryPhysics.Core
        ├── GlobalUsings.cs                    # uint8/uint16/uint32/uint64/int8/int16/int32/int64/f32/f64 aliases
        ├── CryCommon/                         # PORTS LITERAIS de headers CryCommon (sob demanda)
        │   └── IFactionMap.cs                 # ja portado como exemplo
        └── Factions/
            └── FactionMap.cs                  # ja portado como exemplo (404L C++ -> ~340L C#, 57 erros aguardando CryCommon)
```

**Sources:**
- C++ CryAISystem: `c:/Users/Taka/Downloads/lumberyard_1-0_fr_432777/dev/Code/CryEngine/CryAISystem/`
- C++ CryCommon: `c:/Users/Taka/Downloads/lumberyard_1-0_fr_432777/dev/Code/CryEngine/CryCommon/`

**Estrutura de namespaces no port C#:**
- Raiz: `CryAISystem`
- Subnamespaces espelhando subdirs C++: `.Factions`, `.Cover`, `.Communication`, `.Sequence`, `.Group`, `.Movement`, `.Navigation`, `.Navigation.MNM`, `.Navigation.NavigationSystem`, `.SelectionTree`, `.TacticalPointSystem`, `.TargetSelection`, `.BehaviorTree`, `.PolygonSetOps`, `.Walkability`, `.GameSpecific`, `.GoalOps`, `.FlowNodes`, `.Mannequin`, `.AIBubblesSystem`, `.CollisionAvoidance`
- CryCommon ports: `CryAISystem.CryCommon`

**Convenções de mapeamento C++ → C#:**
- `class CFoo` em .h + impl em .cpp → **um único arquivo `CFoo.cs`** (C# não separa header/impl)
- `enum class` → `enum`; `enum` C-style → `enum`
- `struct` → `struct` (preferir; só virar `class` se o original tiver herança ou métodos virtuais)
- ponteiros não-owning → referência direta ao tipo C# (campo/parâmetro)
- ponteiros owning → ainda referência direta (GC cuida)
- `const T&` → `T` (struct) ou `T` (class) — não usar `in` modifier
- `T&` (out) → `ref T` ou `out T` conforme semântica
- templates → generics quando trivial; instanciar concreto quando o template envolve specialization
- macros do CryEngine (`AIWarning`, `IF_UNLIKELY`, `CONST_TEMP_STRING`, `CRY_ASSERT`) → traduzir literalmente como chamadas a métodos estáticos em uma classe `CryMacros`/`AILog`
- `stl::hash_map<K,V>` → `Dictionary<K,V>`
- `std::vector<T>` → `List<T>` (com semântica equivalente — `push_back` → `Add`, `size()` → `Count`, `clear()` → `Clear`)
- `std::set<T>` → `HashSet<T>`
- `std::map<K,V>` → `SortedDictionary<K,V>` (chave ordenada)
- `std::pair<A,B>` → `KeyValuePair<A,B>` ou tuple `(A,B)`
- `std::unique_ptr<T>` / `std::shared_ptr<T>` → referência direta (GC)
- `string` (CryString) → `string` C#
- `_smart_ptr<T>` → referência direta
- iteradores → `foreach` ou índice manual quando o original usa iterator arithmetic

---

## 2. Current state (updated 2026-04-13)

| Item | Status |
|---|---|
| Full mapping of 179 .cpp files / 91k LOC | ✅ (this document) |
| CryCommon deps mapping (172 headers, ~52k LOC) | ✅ (this document) |
| Scaffolding (sln + csproj + GlobalUsings + tracking files) | ✅ |
| CryPhysics.Sharp reconciliation | ✅ patches applied (PhysVector3, PhysMatrix33, PhysQuaternion) |
| Phase 0 — Scaffolding | ✅ done |
| Phase 1 — Leaves (~3,000 LOC) | ✅ done |
| Phase 2 — Actor hierarchy (~25,000 LOC) | ✅ done — full .h+.cpp literal ports (AIActor, AIPlayer, AIVehicle, PipeUser, Puppet) |
| Phase 3 — Navigation (~21,000 LOC) | ✅ done — MNM navmesh, TileGenerator, Voxelizer, MeshGrid A*, NavigationSystem, pathfinding |
| Phase 4 — Movement + CollisionAvoidance (~4,700 LOC) | ✅ done — MovementSystem + 10 blocks + ORCA |
| Phase 5 — Perception (~5,000 LOC) | 🟡 partial — VisionMap ray-cast pipeline done |
| Phase 6 — Cover + TPS + TargetSelection (~16,400 LOC) | ❌ not started |
| Phase 7 — Goals & Pipes (~10,400 LOC) | 🟡 partial — GoalOpTrace + GoalOpStick + AIPIDController |
| Phase 8 — BehaviorTree + SelectionTree (~12,000 LOC) | ❌ not started |
| Phase 9 — Group dynamics (~5,600 LOC) | ❌ not started |
| Phase 10 — Communication + Sequence + Mannequin (~7,000 LOC) | ❌ not started |
| Phase 11 — CAISystem coordinator (~15,000 LOC) | 🟡 mostly done — CAISystem + UpdateLoop + DebugDrawHelpers + MemStats + Environment |
| Phase 12 — GameSpecific + FlowNodes (~7,500 LOC) | ❌ not started |
| Phase 13 — ScriptBind_AI (~12,000 LOC) | ❌ not started |
| **Total ported** | **~92,000 lines C# across 113 files** |
| **Build** | **0 errors, 96 warnings** |

---

## 3. Inventário CryCommon — todas as headers necessárias

Os 172 headers CryCommon abaixo são incluídos por algum arquivo do CryAISystem. Total **~52.000 linhas** de C++ a traduzir literalmente conforme cada arquivo do AI for portado. **Não portar todos de uma vez** — porte sob demanda, mas siga a ordem topológica desta seção quando houver escolha.

### 3.1 Tier 0 — fundação absoluta (portar primeiro, ~1.500 LOC)

| Header | LOC | Pasta destino | Função |
|---|---|---|---|
| platform.h | 819 | `CryCommon/Platform.cs` | macros de plataforma — maioria vira no-op em C# |
| BaseTypes.h | ~30 | já em `GlobalUsings.cs` | uint8/uint16/uint32/etc. |
| CompileTimeAssert.h | ~20 | `CryCommon/CompileTimeAssert.cs` | trivial |
| smartptr.h | ~250 | `CryCommon/SmartPtr.cs` | `_smart_ptr<T>` — vira referência direta |
| BitFiddling.h | 512 | `CryCommon/BitFiddling.cs` | helpers de bits |
| CryAssert.h | ~80 | `CryCommon/CryAssert.cs` | macros CRY_ASSERT |
| CryModuleDefs.h | 49 | ignorar (macros C-only) |
| CryWindows.h | 49 | ignorar |

### 3.2 Tier 1 — infraestrutura core (~3.500 LOC)

| Header | LOC | Notas |
|---|---|---|
| ILog.h | 268 | logger interface |
| IConsole.h | 683 | ICVar/IConsole/IConsoleCmd |
| IValidator.h | ~100 | sistema de assertion |
| ITimer.h | 215 | tempo de frame |
| ISystem.h | **2195** | bottleneck — 68 includes no AI; gEnv depende disto |
| IEngineModule.h | 42 | base de modulo |
| ISystemScheduler.h | ~50 | scheduler |
| CryVersion.h | ~30 | versão |

**Atenção**: `ISystem.h` puxa transitivamente `ITimer`, `IConsole`, `ILog`, `IValidator`, `IFileSystem`. Pode ser preciso quebrar em arquivos menores.

### 3.3 Tier 2 — strings e containers (~7.500 LOC)

| Header | LOC | Mapeamento |
|---|---|---|
| CryString.h | **2372** | `string` C#; `stack_string` → `string` ou `StringBuilder`; `CryStringT<T>` → traduzir literal |
| CryArray.h | 1209 | `List<T>` |
| StringUtils.h | 1453 | `CryCommon/StringUtils.cs` |
| StlUtils.h | 925 | `stl::push_back_unique`, `stl::find`, etc. — port literal |
| CryListenerSet.h | 601 | `CListenerSet<T>` |
| CryFile.h | 435 | wrapper de arquivo |
| VectorMap.h | 493 | `SortedDictionary<K,V>` ou tradução literal |

### 3.4 Tier 3 — math/geo (~5.200 LOC) — **MUITO DESTES JÁ EXISTEM EM CryPhysics.Sharp**

| Header | LOC | Status no CryPhysics.Sharp |
|---|---|---|
| Cry_Math.h | 498 | parcial — verificar `MathUtils.cs` |
| Cry_Vector3.h | 1236 | ✅ `PhysVector3` (verificar fidelidade) |
| Cry_Geo.h | 954 | parcial — verificar `Primitives/` |
| Cry_GeoDistance.h | 1460 | ❓ provável faltar — vai ser patch CryPhysics.Sharp grande |
| FixedPoint.h | 977 | ❌ não tem — port novo |
| Cry_Camera.h | ~600 | ❌ não tem — port novo |

**Workflow obrigatório aqui**: para cada tipo, abrir o C# em `CryPhysics.Sharp/src/CryPhysics.Core/Math/` e diff vs o C++. Patch CryPhysics.Sharp se houver discrepância. Adicionar `using` aliases no AI:
```csharp
using Vec3 = CryPhysics.Math.PhysVector3;
using Vec2 = CryPhysics.Math.PhysVector2;
using Matrix33 = CryPhysics.Math.PhysMatrix33;
using Quat = CryPhysics.Math.PhysQuaternion;
```

### 3.5 Tier 4 — entity/game (~3.700 LOC)

| Header | LOC |
|---|---|
| IEntity.h | 1441 |
| IEntitySystem.h | 910 |
| IGameFramework.h | 1043 |
| IGame.h | 243 |
| IEntityPoolManager.h | 102 |

### 3.6 Tier 5 — render/3D/physics (~7.300 LOC)

| Header | LOC |
|---|---|
| I3DEngine.h | **3630** |
| IRenderer.h | **2715** |
| IRenderAuxGeom.h | 781 |
| IPhysics.h | 63 (interface fina; implementação no CryPhysics.Sharp) |
| IntersectionTestQueue.h | 278 |

### 3.7 Tier 6 — AI public interfaces (~6.500 LOC, **CRÍTICO**)

| Header | LOC | Notas |
|---|---|---|
| IAISystem.h | 1003 | interface central — base de tudo |
| IAgent.h | **1959** | interface de agente — base de CAIObject/CAIActor |
| IPathfinder.h | 830 | interface de pathfinding |
| INavigationSystem.h | 253 | navmesh |
| IMovementSystem.h | 181 | movimento |
| IMovementActor.h | 36 |
| MovementRequest.h | 214 |
| MovementUpdateContext.h | 56 |
| MovementStyle.h | 159 |
| IAIActor.h | 150 |
| IAIObjectManager.h | 55 |
| IAIGroup.h | 153 |
| IAIGroupProxy.h | 53 |
| IAIRecorder.h | 176 |
| IAIDebugRenderer.h | 174 |
| IAIAction.h | 102 |
| IAIActionSequence.h | 120 |
| IGoalPipe.h | 245 |
| ITacticalPointSystem.h | 574 |
| ITargetTrackManager.h | 119 |
| ICommunicationManager.h | 226 |
| IFactionMap.h | 45 | ✅ JÁ PORTADO |
| IBlackBoard.h | 28 |
| IPerceptionHandler.h | 177 |
| IVisionMap.h | 274 |
| VisionMapTypes.h | 29 |
| ICoverSystem.h | 246 |
| ICollisionAvoidanceSystem.h | 93 |
| ISmartObjectManager.h | ~400 |
| ISelectionTreeManager.h | 65 |
| IClusterDetector.h | 79 |
| IInterestSystem.h | 71 |
| IOffMeshNavigationManager.h | ~100 |
| IMNM.h | ~200 |
| IPuppet.h | ~150 |
| AIFormationDescriptor.h | 81 |
| AISystemListener.h | 40 |

### 3.8 Tier 7 — serialização/XML/IO (~2.700 LOC)

| Header | LOC |
|---|---|
| ISerialize.h | 1058 |
| SerializeFwd.h | 26 |
| TimeValue.h | 181 |
| IXml.h | 691 |
| ICryPak.h | 996 |
| XMLAttrReader.h | 80 |

### 3.9 Tier 8 — opcional/avançado (~7.000 LOC)

| Header | LOC | Pode ser deferido |
|---|---|---|
| IFlowSystem.h | **2552** | sim |
| IScriptSystem.h | 1191 | sim |
| ScriptHelpers.h | 836 | sim |
| ICryAnimation.h | 1858 | sim |
| IJobManager_JobDelegator.h | **2513** | sim |

### 3.10 Tier 9 — utilities adicionais (~3.000 LOC)

HashGrid.h (594), TypeInfo_impl.h (211), CrySizer.h (554), IDMap.h (424), PoolAllocator.h (490), FixedAllocator.h (326), Random.h (74), CryExtension/* (462), MultiThread, CryThread, CryFlags, CryHalf, CryEndian, CryHash, CryColor, MTPseudoRandom, LCGRandom, IndexedString, ProjectDefines.

### 3.11 BehaviorTree headers (CryCommon)

| Header | LOC |
|---|---|
| BehaviorTree/IBehaviorTree.h | 854 |
| BehaviorTree/Node.h | 307 |
| BehaviorTree/Action.h | 56 |

---

## 4. Inventário CryAISystem — fase a fase

> Para cada arquivo abaixo: **um arquivo .cs por arquivo C++** (combinar .h+.cpp+.inl no mesmo .cs). Pasta espelha o subdir C++. Métodos listados são os públicos principais — porte TODOS os métodos (públicos, protegidos e privados).

### Fase 1 — Folhas (sem deps intra-AI) — ~3.000 LOC

**Lê primeiro**: `StdAfx.h` (160L) para entender quais headers o módulo inteiro pré-compila.

| Arquivo C++ | LOC | Deps CryCommon mínimas |
|---|---|---|
| Configuration.h | 43 | platform |
| StdAfx.h + StdAfx.cpp | 174 | platform, ISystem |
| AIHash.h | 109 | StringUtils |
| Reference.h + Reference.inl | 436 | smartptr |
| AIQuadTree.h + AIQuadTree.inl | 567 | Cry_Vector3, Cry_Geo |
| AILog.h + AILog.cpp | 475 | ILog, ISystem |
| StatsManager.h + StatsManager.cpp | 191 | ISystem |
| BlackBoard.h + BlackBoard.cpp | 66 | IBlackBoard |
| HashSpace.h | 572 | Cry_Vector3 (template-heavy) |
| AIMemStats.cpp | 361 | CrySizer |
| ClusterDetector.cpp | 340 | IClusterDetector, Cry_Vector3 |
| CalculationStopper.h + .cpp | 174 | ITimer |
| Factions/FactionMap.{h,cpp} | **404** | ✅ **JÁ PORTADO** (deferido até CryCommon completar) |
| PolygonSetOps/Polygon2d.h | ~120 | Cry_Vector3 |
| PolygonSetOps/LineSeg.h | ~80 | Cry_Vector3 |
| PolygonSetOps/BiDirMap.h | ~100 | std containers |
| PolygonSetOps/Utils.h | ~80 | Cry_Vector3 |
| AIDebugDrawHelpers.h + .cpp | ~400 | IRenderAuxGeom, IAIDebugRenderer |
| AIDbgRecorder.h + .cpp | ~150 | TimeValue, ILog |
| NullAIDebugRenderer.h | ~100 | IAIDebugRenderer |
| DebugDrawContext.h | 40 | IRenderAuxGeom |
| ValueHistory.h | ~100 | (template) |
| XMLUtils.h | ~80 | IXml |

### Fase 2 — Hierarquia de atores base — ~25.000 LOC

**Ordem rígida** (cada arquivo depende do anterior):

1. **AIObject.h + AIObject.cpp** (1438L) — `class CAIObject : public IAIObject`
   - Public: `Reset, Release, IsUpdatedOnce, IsEnabled, Event, GetAIObjectID, GetVisionID, SetObservable, IsObservable, GetObservablePositions, GetObservableTypeMask, GetPhysicalSkipEntities, SetName, GetName, GetAIType, GetSubType, SetType, SetPos, GetPos, GetPosInNavigationMesh, SetRadius, GetRadius, GetBodyDir, SetBodyDir, GetViewDir, SetViewDir, IsPointInFOV, GetEntityDir, SetEntityDir, GetMoveDir, SetMoveDir, GetVelocity, GetNavNodeIndex, SetEntityID, GetEntityID, GetEntity, Serialize, PostSerialize, SetFirePos, GetFirePos, GetBlackBoard, GetFactionID, SetFactionID, SetGroupId, GetGroupId`
   - Deps: VisionMap.h (Phase 5), Reference.h (Phase 1)
2. **ObjectContainer.h + .cpp** (~800L)
3. **ActorLookUp.h** (~150L)
4. **AIObjectManager.h + .cpp** (966L)
5. **PostureManager.h + .cpp** (~900L)
6. **PersonalLog.h + .cpp** (~200L)
7. **Adapters.h + .cpp** (59L)
8. **AICollision.h + .cpp** (1666L)
9. **Environment.h + .cpp** (~300L)
10. **AIActor.h + AIActor.cpp** (3093L) — `class CAIActor : public CAIObject, IAIActor`
    - Public: `CanDamageTarget, CanDamageTargetWithMelee, GetPhysics, SetBehaviorVariable, GetBehaviorVariable, GetBehaviorSelectionTree, GetBehaviorSelectionVariables, ResetBehaviorSelectionTree, ProcessBehaviorSelectionTreeSignal, UpdateBehaviorSelectionTree, ResetModularBehaviorTree, QueryBodyInfo, GetBodyInfo, GetPathAgentEntity, GetPathAgentName, GetPathAgentType, GetPathAgentPassRadius, GetPathAgentPos, GetPathAgentVelocity, GetPathAgentNavigationBlockers, GetPathFollower, GetPathAgentLastNavNode, SetPathAgentLastNavNode, SetPathToFollow, SetPathAttributeToFollow, SetPFBlockerRadius, CanTargetPointBeReached, UseTargetPointRequest, GetValidPositionNearby, GetTeleportPosition, IsPointValidForAgent, SetPos, OnObjectRemoved, GetState`
    - Inner struct: `SAIDamagePart`
11. **AIPlayer.h + .cpp** (1367L) — `class CAIPlayer : public CAIActor`
12. **AIVehicle.h + .cpp** (1308L) — `class CAIVehicle : public CAIActor`
13. **AIFlyingVehicle.h + .cpp** (49L) — `class CAIFlyingVehicle : public CAIVehicle`
14. **PipeUser.h + PipeUser.cpp** (5673L) — `class CPipeUser : public CAIActor, CPipeUserAdapter`
    - Inner struct: `CoverUsageInfo` (lowLeft, lowCenter, lowRight, highLeft, highCenter, highRight, lowCompromised, highCompromised)
    - Inner enum: `EAimState` { AI_AIM_NONE, AI_AIM_WAITING, AI_AIM_OBSTRUCTED, AI_AIM_READY, AI_AIM_FORCED }
    - Public: `Event, ParseParameters, RecordEvent, RecordSnapshot, Reset, SetName, GetStateFromActiveGoals, GetGoalPipe, RemoveActiveGoal, SetAttentionTarget, ClearPotentialTargets, SetLastOpResult, NavigateAroundObjects, CancelRequestedPath, HandlePathDecision, AdjustPath, OnMNMPathResult, AdjustPathAroundObstacles, GetPathFollower, GetPathFollowerParams, GetPendingSmartObjectID, RequestPathTo` (e mais — porte tudo)
15. **PuppetRateOfDeath.h + .cpp** (~400L)
16. **PuppetPhys.cpp** (728L)
17. **Puppet.h + Puppet.cpp** (6168L) — `class CPuppet : public CPipeUser, IPuppet`
    - Inner: `SShootingStatus`, `SSoundPerceptionDescriptor`
    - 100+ métodos públicos, ver inventário detalhado na seção 6

### Fase 3 — Navigation (root + MNM + NavigationSystem + Walkability) — ~21.000 LOC

**Ordem por dependências:**

**3a) Walkability (1219L)** — depende só de Cry_Vector3, AABB
- Walkability/FloorHeightCache.h + .cpp
- Walkability/WalkabilityCache.h + .cpp
- Walkability/WalkabilityCacheManager.h + .cpp

**3b) Shape primitives (~2100L)**
- Shape.h + Shape.cpp (863L)
- Shape2.h + Shape2.cpp (~1200L)
- VertexList.h + VertexList.cpp (~400L)
- HideSpot.h + HideSpot.cpp (~600L)
- AIHideObject.h + AIHideObject.cpp (1490L)
- AIDynHideObjectManager.h + AIDynHideObjectManager.cpp (~500L)

**3c) Navigation/MNM/* (~10.000L)** — núcleo do navmesh
- Navigation/MNM/MNM.h
- Navigation/MNM/MNM_Type_info.h
- Navigation/MNM/HashComputer.h
- Navigation/MNM/FixedVec2.h
- Navigation/MNM/FixedVec3.h
- Navigation/MNM/FixedAABB.h
- Navigation/MNM/OpenList.h
- Navigation/MNM/Profiler.h
- Navigation/MNM/BoundingVolume.h + .cpp
- Navigation/MNM/CompactSpanGrid.h + .cpp
- Navigation/MNM/DynamicSpanGrid.h + .cpp
- Navigation/MNM/Voxelizer.h + .cpp (1332L)
- Navigation/MNM/Tile.h + .cpp
- Navigation/MNM/MeshGrid.h + .cpp (3211L) — `class CMeshGrid` (50+ public methods)
- Navigation/MNM/IslandConnections.h + .cpp
- Navigation/MNM/OffGridLinks.h + .cpp
- Navigation/MNM/TileGenerator.h + .cpp (3556L) — `class CTileGenerator`
- Navigation/MNM/TileGeneratorDraw.cpp

**3d) Navigation/NavigationSystem/* (~5.000L)**
- Navigation/NavigationSystem/NavigationSystem.h + .cpp (4971L) — `class CNavigationSystem : public INavigationSystem` (80+ methods)
- Navigation/NavigationSystem/IslandConnectionsManager.h + .cpp
- Navigation/NavigationSystem/OffMeshNavigationManager.h + .cpp
- Navigation/NavigationSystem/VolumesManager.h + .cpp
- Navigation/NavigationSystem/WorldMonitor.h + .cpp

**3e) Pathfinding root (~5.000L)**
- AllNodesContainer.h + .cpp
- GraphStructures.h + .cpp (767L)
- GraphNodeManager.h + .cpp
- Graph.h + Graph.cpp (1849L) — `class CGraphNodeManager`
- CTriangulator.h + .cpp
- Navigation.h + Navigation.cpp (1672L) — `class CNavigation : public INavigation`
- NavPath.h + NavPath.cpp (2342L) — `class CNavPath` (50+ methods)
- PathFollower.h + PathFollower.cpp (1057L) — `class CPathFollower : public IPathFollower`
- SmartPathFollower.h + SmartPathFollower.cpp (1761L) — `class CSmartPathFollower`
- PathMarker.h + .cpp
- PathHolder.h
- MNMPathfinder.h + MNMPathfinder.cpp (1219L) — `class CMNMPathfinder : public IMNMPathfinder`
- PathObstacles.h + PathObstacles.cpp (1666L) — `class CPathObstacles`
- FlightNavRegion2.h + .cpp (1434L)
- Free2DNavRegion.h + .cpp
- CustomNavRegion.h + .cpp

### Fase 4 — Movement + CollisionAvoidance — ~4.700 LOC

**Movement/* (3482L):**
- Movement/MovementHelpers.h + .cpp
- Movement/MovementSystem.h + .cpp — `class CMovementSystem : public IMovementSystem`
- Movement/MovementSystemCreator.h + .cpp
- Movement/MovementActor.h + .cpp
- Movement/MovementPlan.h + .cpp — `class CMovementPlan`
- Movement/MovementPlanner.h + .cpp
- Movement/MoveOp.h + .cpp
- Movement/MovementBlock_DefaultEmpty.h
- Movement/MovementBlock_FollowPath.h + .cpp
- Movement/MovementBlock_HarshStop.h + .cpp
- Movement/MovementBlock_TurnTowardsPosition.h + .cpp
- Movement/MovementBlock_UseExactPositioningBase.h + .cpp
- Movement/MovementBlock_UseExactPositioning.h + .cpp
- Movement/MovementBlock_UseSmartObject.h + .cpp
- Movement/MovementBlock_SetupPipeUserCoverInformation.h + .cpp
- Movement/MovementBlock_InstallAgentInCover.h + .cpp
- Movement/MovementBlock_UninstallAgentFromCover.h + .cpp

**CollisionAvoidance/* (1228L):**
- CollisionAvoidance/CollisionAvoidanceSystem.h + .cpp — `class CCollisionAvoidanceSystem : public ICollisionAvoidanceSystem`

### Fase 5 — Perception — ~5.000 LOC

- VisionMap.h + .cpp (1618L) — `class CVisionMap : public IVisionMap` (50+ methods)
- AIRadialOcclusion.h + .cpp (1012L) — `class CAIRadialOcclusion`
- AIRadialOcclusionRaycast.h + .cpp
- PerceptionManager.h + .cpp (2003L) — `class CPerceptionManager : public IPerceptionManager`
- GlobalPerceptionScaleHandler.h + .cpp
- CentralInterestManager.h + .cpp (1116L) — `class CCentralInterestManager : public ICentralInterestManager`
- PersonalInterestManager.h + .cpp
- MissLocationSensor.h + .cpp
- AILightManager.h + .cpp (828L)

### Fase 6 — Cover + TPS + TargetSelection — ~16.400 LOC

**Cover/* (5497L):**
- Cover/Cover.h
- Cover/CoverSystem.h + .cpp (1107L) — `class CCoverSystem : public ICoverSystem`
- Cover/CoverSurface.h + .cpp (1270L) — `class CCoverSurface`
- Cover/CoverSampler.h + .cpp (1132L) — `class CCoverSampler`
- Cover/CoverScorer.h + .cpp
- Cover/CoverPath.h + .cpp
- Cover/CoverUser.h + .cpp
- Cover/EntityCoverSampler.h + .cpp
- Cover/DynamicCoverManager.h + .cpp

**TacticalPointSystem/* (5691L):**
- TacticalPointSystem/TacticalPointQueryEnum.h
- TacticalPointSystem/TacticalPointQuery.h + .cpp
- TacticalPointSystem/TacticalPointSystem.h + .cpp (4920L) — `class CTacticalPointSystem : public ITacticalPointSystem`
- TacticalPointSystem/FlyHelpers_Tactical.h
- TacticalPointSystem/FlyHelpers_TacticalPointLanguageExtender.h + .cpp

**TargetSelection/* (5154L):**
- TargetSelection/TargetTrackCommon.h + .cpp
- TargetSelection/TargetTrackModifiers.h + .cpp
- TargetSelection/TargetTrack.h + .cpp
- TargetSelection/TargetTrackGroup.h + .cpp
- TargetSelection/TargetTrackManager.h + .cpp (2490L) — `class CTargetTrackManager : public ITargetTrackManager`

### Fase 7 — Goals & Pipes — ~10.400 LOC

- AIPIDController.h + .cpp
- FireCommand.h + FireCommand.cpp (2201L)
- GoalPipeXMLReader.h + .cpp
- GoalOpFactory.h + .cpp
- GoalPipe.h + GoalPipe.cpp (1542L) — `class CGoalPipe`
- GoalOp.h + GoalOp.cpp (7210L) — `class CGoalOp` + 50+ derived classes (CGoalOpJump, CGoalOpFire, CGoalOpWait, CGoalOpMovementPlugin, CGoalOpKnockDown, CGoalOpSmartObject, CGoalOpWalkNavGraph, etc.)
- GoalOpStick.h + .cpp (1360L) — `class CGoalOpStick`
- GoalOpTrace.h + .cpp (1978L) — `class CGoalOpTrace`
- GoalOps/ShootOp.h + .cpp
- GoalOps/TeleportOp.h + .cpp
- PipeManager.h + .cpp
- SmartObjects.h + SmartObjects.cpp (6183L) — `class CSmartObject`, `class CSmartObjectManager : public ISmartObjectManager`

### Fase 8 — BehaviorTree + SelectionTree — ~12.000 LOC

**BehaviorTree/* (9050L):**
- BehaviorTree/IBehaviorTreeGraft.h
- BehaviorTree/BehaviorTreeManager.h + .cpp — `class BehaviorTree::BehaviorTreeManager : public IBehaviorTreeManager`
- BehaviorTree/BehaviorTreeGraft.h + .cpp
- BehaviorTree/BehaviorTreeNodeRegistration.h + .cpp
- BehaviorTree/BehaviorTreeNodes_Core.h + .cpp (2890L) — 25+ core node types (Parallel, Selector, Sequence, Wait, Delay, Loop, RandomSelector, etc.)
- BehaviorTree/BehaviorTreeNodes_AI.h + .cpp (4098L) — 40+ AI node types (AI_ModifyAnimationLayer, AI_PlaySound, AI_GoTo, AI_Jump, AI_MeleAttack, AI_ShootAt, AI_UseSmartObject, AI_UpdateFormation, AI_TargetTracking, etc.)
- BehaviorTree/BehaviorTreeNodes_Helicopter.h + .cpp
- BehaviorTree/TreeVisualizer.h + .cpp
- BehaviorTree/ExecutionStackFileLogger.h + .cpp

**SelectionTree/* (3092L):**
- SelectionTree/BlockyXml.h + .cpp
- SelectionTree/SelectionContext.h
- SelectionTree/SelectionVariables.h + .cpp
- SelectionTree/SelectionSignalVariables.h + .cpp
- SelectionTree/SelectionCondition.h + .cpp
- SelectionTree/SelectionTreeNode.h + .cpp
- SelectionTree/SelectionTreeTemplate.h + .cpp
- SelectionTree/SelectionTreeManager.h + .cpp
- SelectionTree/SelectionTreeDebugger.h + .cpp
- SelectionTree/SelectionTranslator.h + .cpp
- SelectionTree/SelectionTree.h + .cpp — `class CSelectionTree`

### Fase 9 — Group dynamics — ~5.600 LOC

- AIGroup.h + AIGroup.cpp (1884L) — `class CAIGroup`
- Formation.h + Formation.cpp (1858L) — `class CFormation`
- Leader.h + Leader.cpp (1124L) — `class CLeader`
- LeaderAction.h + LeaderAction.cpp (2243L) — `class CLeaderAction`
- UnitAction.h + .cpp
- UnitImg.h + .cpp
- Group/Group.h + .cpp
- Group/GroupManager.h + .cpp

### Fase 10 — Communication + Sequence + Mannequin + AIBubbles — ~7.000 LOC

**Communication/* (3749L):**
- Communication/Communication.h
- Communication/CommunicationChannel.h + .cpp
- Communication/CommunicationChannelManager.h + .cpp
- Communication/CommunicationPlayer.h + .cpp
- Communication/CommunicationManager.h + .cpp (2396L) — `class CCommunicationManager : public ICommunicationManager`
- Communication/CommunicationTestManager.h + .cpp

**Sequence/* (2550L):**
- Sequence/SequenceAgent.h
- Sequence/Sequence.h + .cpp
- Sequence/SequenceManager.h + .cpp — `class CSequenceManager : public AIActionSequence::ISequenceManager`
- Sequence/SequenceFlowNodes.h + .cpp

**Mannequin/* (214L):**
- Mannequin/MannequinGoalOp.h + .cpp

**AIBubblesSystem/* (609L):**
- AIBubblesSystem/IAIBubblesSystem.h
- AIBubblesSystem/AIBubblesSystem.h + .cpp
- AIBubblesSystem/AIBubblesNotifier.h + .cpp
- AIBubblesSystem/AIBubblesNotifierLibrary.cpp

### Fase 11 — CAISystem coordinator central — ~15.000 LOC

- AIActions.h + AIActions.cpp (1593L) — `class CAIAction`, `class CAnimationAction`, `class CAIActionManager : public IAIActionManager`
- AISignal.h + AISignal.cpp
- AISignalCRCs.h + AISignalCRCs.cpp
- AStarOpenList.h
- GenericAStarSolver.h
- AIConsoleVariables.h + .cpp (1644L) — `struct AIConsoleVars` (100+ CVars)
- DebugDrawContext.h
- DebugDraw.cpp (5421L) — código de visualização
- AIRecorder.h + .cpp (2091L) — `class CAIRecorder`
- CodeCoverageManager.h + .cpp
- CodeCoverageTracker.h + .cpp
- CodeCoverageGUI.h + .cpp
- CAISystem.h + CAISystem.cpp (8333L) — `class CAISystem : public IAISystem, ISystemEventListener` (100+ public methods, organizar por grupo de função)
- CAISystemUpdate.cpp
- CAISystemPhys.cpp
- CryAISystem.h + CryAISystem.cpp — `CreateAISystem(ISystem*)` factory + DLL export

### Fase 12 — GameSpecific + FlowNodes — ~7.500 LOC

**GameSpecific/* (7300L):**
- GameSpecific/GoalOp_Crysis2.h + .cpp
- GameSpecific/GoalOp_G02.h + .cpp
- GameSpecific/GoalOp_G04.h + .cpp
- GameSpecific/FlyHelpers_Path.h + .cpp
- GameSpecific/FlyHelpers_PathFollower.h + .cpp
- GameSpecific/FlyHelpers_PathLocation.h
- GameSpecific/FlyHelpers_PathFollowerHelpers.h
- GameSpecific/FlyHelpers_Debug.h

**FlowNodes/* (230L):**
- FlowNodes/AIFlowBaseNode.h + .cpp

### Fase 13 — ScriptBind_AI — ~12.000 LOC (último, possivelmente deferido)

- ScriptBind_AI.h + ScriptBind_AI.cpp (11.6k linhas) — Lua bindings
- **Decisão**: pode ser inteiramente deferido se Lua não for desejado no .NET. Marcar todo bloco em `deferred.md` se assim for.

---

## 5. Workflow por arquivo (aplicar a TODOS, sem exceção)

**Pré-flight (antes de tocar o teclado):**
1. Abrir o `.h` C++ correspondente
2. Abrir o `.cpp` C++ correspondente (e `.inl` se existir)
3. Listar todos os `#include <...>` (CryCommon) e `#include "..."` (intra-AI)
4. Para cada include CryCommon:
   - Já portado em `CryAISystem.Sharp/src/CryAISystem.Core/CryCommon/`? ✅ continuar
   - Não portado? → **portar literal o header CryCommon agora** (ver Tier order na seção 3)
5. Para cada include intra-AI:
   - Já portado? ✅ continuar
   - Não portado mas é da MESMA fase? → **continuar mesmo assim**, tipos forward-declarados resolvem entre arquivos no mesmo build
   - Não portado e é de fase ANTERIOR? → revisar PORT_STATUS.md, pode haver bug na ordem
6. Para cada tipo de math/geom (`Vec3`, `Matrix33`, `Quat`, `AABB`, etc.) consumido:
   - Abrir o equivalente em `CryPhysics.Sharp/src/CryPhysics.Core/Math/` ou `Primitives/`
   - Diff campo-a-campo + método-a-método contra o header C++ original
   - Se divergir → **patch CryPhysics.Sharp para bater com C++** + registrar em `cryphysics_patches.md` no formato:
     ```
     - **CryPhysics.Sharp/src/CryPhysics.Core/Math/PhysVector3.cs** ← Cry_Vector3.h
       - faltava método `GetLengthSquared2D()` (linha 412 do Cry_Vector3.h)
       - adicionado port literal
     ```

**Port:**
7. Criar o arquivo `.cs` no caminho que espelha o C++ (`CryAISystem/Foo/Bar.h` → `CryAISystem.Sharp/src/CryAISystem.Core/Foo/Bar.cs`)
8. Adicionar `using` aliases de math no topo:
   ```csharp
   using Vec3 = CryPhysics.Math.PhysVector3;
   using Vec2 = CryPhysics.Math.PhysVector2;
   using Matrix33 = CryPhysics.Math.PhysMatrix33;
   using Matrix34 = CryPhysics.Math.PhysMatrix34; // se existir; senão patchar
   using Quat = CryPhysics.Math.PhysQuaternion;
   ```
9. Traduzir LITERAL:
   - Comentários do C++: copiar (mantém valor histórico)
   - `class C : public B` → `class C : B`
   - `class C : public IA` → `class C : IA`
   - `virtual void Foo() override` → `public override void Foo()`
   - `void Foo() const` → `public void Foo()` (C# não tem const methods)
   - `static void Foo()` → `public static void Foo()`
   - Métodos privados: `private`. Protegidos: `protected`. Públicos: `public`.
   - Membros: mesma visibilidade, mesma ordem
   - Loops: `for(int i=0; i<n; ++i)` mantém — não converter para `foreach` indevidamente
   - `if(ptr)` → `if(ptr != null)`
   - `nullptr`/`NULL`/`0` (em ponteiro) → `null`
   - Operadores: traduzir literal (`==`, `!=`, `<`, etc.)
   - **NÃO adicionar** `try/catch`, `?.`, `??`, `using var`, LINQ, expression-bodied members
10. Para macros:
    - `AIWarning("...", a, b)` → `AILog.AIWarning("...", a, b)` (assumindo `AILog.cs` portado na Fase 1)
    - `IF_UNLIKELY(cond)` → `if (cond)` (drop hint)
    - `CRY_ASSERT(cond)` → `CryAssert.Check(cond)` (assumindo helper)
    - `CONST_TEMP_STRING(s)` → `s` (no-op em C#)
    - `gEnv->pCryPak->IsFileExist(f)` → `gEnv.pCryPak.IsFileExist(f)` (gEnv vira static)

**Build:**
11. `dotnet build CryAISystem.Sharp.sln`
12. Se erros vierem do arquivo recém-portado:
    - Erros causados por **tipo CryCommon ainda não portado** → portar o header CryCommon agora (recursivo)
    - Erros causados por **arquivo intra-AI de fase posterior** → registrar em `deferred.md` como dependency-pending e continuar
    - Erros causados por **divergência CryPhysics.Sharp vs C++** → patchar CryPhysics.Sharp + registrar em `cryphysics_patches.md`
    - Erros causados por **bug de tradução** → corrigir
    - Erros causados por **nada disso** (sintaxe, etc.) → registrar em `deferred.md` com a contagem

**Review linha-a-linha:**
13. Side-by-side: abrir o .cs ao lado do .cpp+.h C++
14. Para cada método: confirmar
    - (a) toda variável local presente
    - (b) toda statement presente, na ordem
    - (c) toda constante numérica idêntica
    - (d) toda chamada de método presente com mesmos argumentos
    - (e) zero linha extra "para arrumar"
15. Marcar checkbox em `PORT_STATUS.md`:
    ```
    - [x] Factions/FactionMap.cs — 404L C++ → 340L C# — reviewed — 57 errors deferred
    ```

---

## 6. Inventário de classes/funções principais (referência rápida)

### Hierarquia central
```
IAIObject (CryCommon/IAgent.h)
  └ CAIObject (AIObject.h, 1438L)
      ├ CAIPlayer (AIPlayer.h, 1367L)
      └ CAIActor (AIActor.h, 3093L)
          ├ CAIVehicle (AIVehicle.h, 1308L)
          │   └ CAIFlyingVehicle (AIFlyingVehicle.h, 49L)
          └ CPipeUser (PipeUser.h, 5673L)
              └ CPuppet (Puppet.h, 6168L)
```

### Coordenadores (singletons no original — usar `static` no C# ou pattern Instance)
- `CAISystem` (8333L) — pai de tudo
- `CAIObjectManager` (966L)
- `CNavigationSystem` (4971L)
- `CMovementSystem` (~1100L)
- `CCoverSystem` (1107L)
- `CTacticalPointSystem` (4920L)
- `CTargetTrackManager` (2490L)
- `CCommunicationManager` (2396L)
- `CPerceptionManager` (2003L)
- `CVisionMap` (1618L)
- `BehaviorTreeManager`
- `CSelectionTreeManager`
- `CSequenceManager`
- `CSmartObjectManager` (no SmartObjects.cpp, 6183L)
- `CAIActionManager`
- `CAIRecorder` (2091L)
- `CFactionMap` ✅ portado
- `CCentralInterestManager` (1116L)
- `CCollisionAvoidanceSystem` (1229L)

### Goal-pipe ops (50+ classes derivadas de CGoalOp em GoalOp.cpp)
CGoalOpJump, CGoalOpFire, CGoalOpWait, CGoalOpTrace, CGoalOpStick, CGoalOpMovementPlugin, CGoalOpKnockDown, CGoalOpSmartObject, CGoalOpWalkNavGraph, CGoalOpHide, CGoalOpAcquireTarget, CGoalOpUseObject, CGoalOpAdjustAim, CGoalOpAimAroundWhileUsingASmartObject, CGoalOpAnimation, CGoalOpAnimTarget, CGoalOpApproach, CGoalOpBackoff, CGoalOpBodyPos, CGoalOpBranch, CGoalOpClearStance, CGoalOpClose, CGoalOpClosenessReaction, CGoalOpContinuous, CGoalOpDeValue, CGoalOpDistanceLookAround, CGoalOpFollowPath, CGoalOpFormation, CGoalOpHideMove, CGoalOpIgnoreAll, CGoalOpLook, CGoalOpLookAround, CGoalOpLookAt, CGoalOpMoveTowards, CGoalOpPathFind, CGoalOpProximity, CGoalOpRun, CGoalOpScript, CGoalOpSeekCover, CGoalOpSignal, CGoalOpSpeed, CGoalOpStrafe, CGoalOpSurprise, CGoalOpTacticalPos, CGoalOpTimeout, CGoalOpTurn, CGoalOpUseHidespot, CGoalOpWaitSignal, CGoalOpAdjustPos.

### Behavior tree nodes
- **Core (BehaviorTreeNodes_Core.cpp, 2890L)**: Parallel, Selector, Sequence, Wait, Delay, Loop, LoopUntilSuccess, RandomSelector, IfTime, Priority, StateMachine, Timeout, Monitor, MonitorQueryResult, etc.
- **AI (BehaviorTreeNodes_AI.cpp, 4098L)**: AI_ModifyAnimationLayer, AI_PlaySound, AI_GoTo, AI_GoToCover, AI_Jump, AI_MeleAttack, AI_ShootAt, AI_UseSmartObject, AI_UpdateFormation, AI_TargetTracking, AI_Aim, AI_AimAroundWhileUsingASmartObject, AI_LookAt, AI_StopMoving, AI_TurnTowards, AI_AnimationTagWrapper, AI_StartAnimation, AI_StopAnimation, AI_PlayAnimation, AI_QueryTPS, AI_DropTarget, AI_ExecuteLua, AI_Bubble, etc.
- **Helicopter (BehaviorTreeNodes_Helicopter.cpp)**: nodes específicos de heli

---

## 7. Tracking files (já existem, manter atualizados)

- **PORT_STATUS.md** — checklist por fase + por arquivo, com LOC e flags (reviewed/compiles)
- **deferred.md** — arquivos com erro de compilação, formato `- <path>.cs — <error count> — <reason>`
- **cryphysics_patches.md** — patches feitos no CryPhysics.Sharp para fidelidade ao C++

---

## 8. Verificação final

- **Por arquivo**: `dotnet build` + checkbox de review linha-a-linha em `PORT_STATUS.md`
- **Por fase**: tabela atualizada em PORT_STATUS.md (arquivos portados / total + LOC)
- **Por sessão de trabalho**: commit no `deferred.md` se algum arquivo entrou; commit no `cryphysics_patches.md` se houve patch

**Sem testes de runtime. Sem validação comportamental.** O critério é: "isto é uma tradução literal do C++?" — yes/no por arquivo.

---

## 9. Pontos de atenção / armadilhas conhecidas

1. **Templates pesados** (HashSpace, AIQuadTree, BehaviorTree node factories): C# generics não cobrem tudo. Quando o template C++ usa SFINAE ou specialization complexa, **instanciar concreto** para os tipos que o CryAISystem realmente usa, e marcar com comentário `// generic specialization for X`.
2. **Multiple inheritance**: C# só permite uma classe base. Quando o C++ usa multi-inherit (ex. `CPuppet : CPipeUser, IPuppet`), promover a 2ª base para `interface` ou usar composição (último recurso). **Documentar a decisão** em comentário.
3. **Friend classes**: traduzir como `internal` (assembly-friend) ou métodos públicos com nome convencional `__friend_X`.
4. **Ponteiros para função / function pointers**: usar `delegate` ou `Action`/`Func`.
5. **Unions**: usar `[StructLayout(LayoutKind.Explicit)]` com `[FieldOffset(...)]` — port literal possível.
6. **`reinterpret_cast`**: usar `Unsafe.As<T>()` ou `MemoryMarshal.Cast`.
7. **Bitfields**: traduzir como propriedades sobre um campo `uint`/`ulong` com get/set bit-mask. Comentar offset.
8. **`enum class`**: `enum E : int` (C# enums já são "strong typed").
9. **`std::sort` com comparator**: `List.Sort(Comparer<T>.Create((a,b) => ...))` — manter o lambda fiel ao functor C++.
10. **Iteração com erase**: C# `List` não permite remover durante `foreach`. Usar `for(int i = list.Count-1; i>=0; --i)` — **documentar** que é tradução literal de pattern erase-iterator.
11. **Lifetime / RAII**: destrutores C++ (`~CFoo()`) → traduzir para `Dispose()` somente se houver recursos não-GC; senão omitir e comentar `// dtor body was empty/managed-only`.
12. **Static initialization order**: C# usa lazy static init. Se o C++ depende de ordem específica, usar `static CFoo() { ... }` constructor. Documentar.

---

## 10. Quando pedir confirmação ao usuário

- ❓ Decidir entre `class` e `struct` quando o C++ é um struct com 50+ membros
- ❓ Promover 2ª base de multi-inherit para interface vs composição
- ❓ Adiar inteiramente Fase 13 (ScriptBind_AI) — Lua bindings de 12k linhas
- ❓ Patch grande no CryPhysics.Sharp (>200 linhas adicionadas)
- ❓ Header CryCommon que parece massivo (>2000L) — perguntar se porte completo ou só seção usada

Tudo o resto: **agir, traduzir, registrar, seguir**.
