# CryPhysics.Sharp - Relatorio de Revisao C++ vs C#

## Resumo Executivo

Revisao completa de 42 arquivos C# comparados com os fontes C++ originais em `dev/Code/CryEngine/CryPhysics/`. O port cobre ~13% do codigo C++ original (~8,500 linhas C# vs ~63,600 linhas C++). A maioria dos arquivos sao esqueletos estruturais com dados corretos mas logica de simulacao faltando ou simplificada.

**Total de issues encontrados: 85+**
- CRITICAL: 18
- HIGH: 22  
- MEDIUM: 28
- LOW: 17+

---

## ISSUES CRITICOS (18)

### Math

| # | Arquivo:Linha | Descricao |
|---|---------------|-----------|
| 1 | `Math/Polynomial.cs:33` | Constructor coloca constante em `this[0]` (termo constante). C++ coloca em `data[degree]` (coeficiente lider). Semantica invertida. |
| 2 | `Math/MathUtils.cs:68` | `Sgn(+0.0f)` retorna 1, deveria retornar 0. Formula `IsNeg(-x) - IsNeg(x)` falha para zero porque `-0.0f` tem sign bit. |
| 3 | `Math/MatrixNM.cs:373` | Jacobi eigenvalue: seleciona raiz `t` diferente do C++ (menor vs maior). Produz eigenvectors diferentes. |
| 4 | `Math/MatrixNM.cs:393-398` | Jacobi eigenvectors armazenados como colunas, C++ usa linhas. Convencao transposta. |
| 5 | `Math/MatrixNM.cs:367-368` | Jacobi convergencia com `prec=0` nunca converge (compara `MathF.Abs >= 0`, sempre true). |

### Dynamics

| # | Arquivo:Linha | Descricao |
|---|---------------|-----------|
| 6 | `Dynamics/RigidBody.cs:86-121` | `Step()` mistura force integration + damping + position integration em um so metodo. C++ `Step()` so integra posicao/orientacao; forcas sao aplicadas externamente pelo solver. Semantica fundamentalmente diferente. |
| 7 | `Dynamics/RigidBody.cs:107-116` | Quaternion integration usa Taylor de 1a ordem (`Q += dq*0.5*dt`). C++ usa exponential map exata (`cos/sin` de `|w|*dt/2`). Drift de energia e instabilidade em altas velocidades angulares. |

### Entities

| # | Arquivo:Linha | Descricao |
|---|---------------|-----------|
| 8 | `Entities/ArticulatedEntity.cs:65` | `AeJoint.Flags = 0x3F` (63). C++ `all_angles_locked = 7` (0x07). Bits extras marcam limits como atingidos incorretamente. |
| 9 | `Entities/ArticulatedEntity.cs:220` | `SetJointParams` usa `Op0` como indice de array. C++ faz lookup por body ID em `Op1` (child). Modifica joint errado. |
| 10 | `Entities/LivingEntity.cs:120-152` | `IsFlying` nunca e resetado por colisao. Sem ground detection. Personagem que comeca voando voa para sempre. |
| 11 | `Entities/ParticleEntity.cs:168-230` | Sem colisao/bounce. Particulas atravessam toda geometria. C++ faz raycast + bounce + friction + sliding. |

### World

| # | Arquivo:Linha | Descricao |
|---|---------------|-----------|
| 12 | `World/PhysicalWorld.cs:116-139` | `TimeStep` e um loop vazio - chama `DoStep` sem broadphase, narrowphase, constraint solving, islands, ou qualquer deteccao de colisao. |
| 13 | `World/PhysicalWorld.cs:209` | `SimulateExplosion` bug de precedencia: `(A && B \|\| C)` faz check de `IsAwake` irrelevante. |
| 14 | `World/PhysicalWorld.cs:204-223` | Formula de impulso de explosao completamente errada. C++ usa inverse-square + volumetric pressure + occlusion. C# usa falloff linear simples. |
| 15 | `World/PhysicalWorld.cs:145-182` | `RayWorldIntersection` retorna centro do AABB como hit point, normal hardcoded Z-up. Sem intersecao com geometria real. |

### Events

| # | Arquivo:Linha | Descricao |
|---|---------------|-----------|
| 16 | `Events/PhysicsEvents.cs` | 8 de 12 event TypeIds estao errados. Ex: Collision=0 (deveria ser 2), PostStep=1 (deveria ser 4), StateChange=2 (deveria ser 8). Quebra todo roteamento de eventos. |

### Geometry

| # | Arquivo:Linha | Descricao |
|---|---------------|-----------|
| 17 | `Geometry/TriMeshGeometry.cs:99-117` | `CalcPhysicalProperties` usa inercia de AABB em vez de integral de superficie do mesh. Centro de massa e inercia errados. |
| 18 | `Entities/SoftEntity.cs:208` | Constraint formula `diff * 0.5 * Ks * dt` mistura stiffness com timestep em correcao de posicao. Comportamento varia com dt. C++ usa solver de velocidade Gauss-Seidel. |

---

## ISSUES HIGH (22)

### Math

| # | Arquivo | Descricao |
|---|---------|-----------|
| 19 | `Math/Polynomial.cs:149-158` | Quadratic range-check usa bounds ao quadrado diferente do C++. Semantica de range alterada. |
| 20 | `Math/Quotient.cs:148` | `QuotientD.FixSign()` usa `Math.Sign` que nao detecta `-0.0`. C++ usa bit manipulation. |
| 21 | `Math/MatrixNM.cs:120-127` | `Transpose()` para matrizes nao-quadradas vaza memoria do ArrayPool. |
| 22 | `Math/MatrixNM.cs:339-405` | Jacobi max iterations = 100 fixo. C++ usa `n*n*10`. Para matrizes 20x20, C++ permite 4000, C# so 100. |

### Dynamics

| # | Arquivo | Descricao |
|---|---------|-----------|
| 23 | `Dynamics/RigidBody.cs:155-160` | `GetContactMatrix` API retorna matriz nova em vez de acumular por referencia como C++. |
| 24 | `Dynamics/RigidBody.cs` | Missing: integrator RK4 para orientacao, correcao de energia pos-rotacao. |
| 25 | `Dynamics/RigidBody.cs` | Missing: `qfb`/`offsfb` (frame-to-body transform), `Create()`/`Add()` methods. |

### Entities

| # | Arquivo | Descricao |
|---|---------|-----------|
| 26 | `Entities/PhysicalEntity.cs:209-220` | `ComputeBBox` nao transforma OBB por rotacao da parte. BBox incorreto para partes rotacionadas. |
| 27 | `Entities/PhysicalEntity.cs:75` | `Release()` nao faz cleanup quando refcount chega a 0. Sem unregister geometria, sem disposal. |
| 28 | `Entities/PhysicalEntity.cs:147-165` | `AddGeometry` sem ref counting de geometria, sem check de ID duplicado, sem calculo de massa por densidade. |
| 29 | `Entities/PhysicalEntity.cs:89-91` | `SetParams(ParamsFlags)` faltando assignment direto de `flags` (so tem AND/OR). |
| 30 | `Entities/RigidEntity.cs:198-205` | `RecomputeMassProperties` sem Parallel Axis Theorem (Steiner term). Inercia errada para partes com offset. |
| 31 | `Entities/WheeledVehicleEntity.cs` | Sem `DoStep` override. Sem simulacao de rodas, suspensao, motor, ou fricao. |
| 32 | `Entities/LivingEntity.cs:131-134` | Air control formula errada. C# usa `KAirControl * dt`. C++ usa `KInertia * dt * KAirControl` com clamping direcional. |
| 33 | `Entities/LivingEntity.cs:83-87` | Jump usa `Dir.Z` como velocidade com fallback hardcoded `5f`. C++ tem 3 modos de jump (replace/add/force-fly). |
| 34 | `Entities/LivingEntity.cs:41-42` | Angulos de slope em graus nunca convertidos para cossenos. Defaults errados: 70/45 vs C++ 36/54. |
| 35 | `Entities/ParticleEntity.cs:189-194` | Formula de drag inventada (`KAirResistance * Dim / Mass`). C++ usa `(medium_vel - vel) * kAirResistance * dt`. |
| 36 | `Entities/ParticleEntity.cs:91` | `Dim = Size` direto. C++ usa `m_dim = size * 0.5` (half-size). BBox sera 2x grande demais. |
| 37 | `Entities/ArticulatedEntity.cs` | Missing entire Featherstone solver, StepJoint, CalcBodyZa, CalcBodyIa, SyncBodyWithJoint. |
| 38 | `Entities/ArticulatedEntity.cs:239-252` | `StatusJoint` usa IdChildBody como indice de array em vez de fazer lookup por body ID. Retorna `Q` atual em vez de `PrevQ`. |
| 39 | `Entities/WheeledVehicleEntity.cs:179` | `KSteerToTrack` assignment direto. C++ transforma: `2.0/value` quando `value > 0`. |
| 40 | `Entities/RopeEntity.cs:210` | Wind force como aceleracao constante. C++ usa drag model: `(wind - vel) * clamp(airRes * dt, 0, 1)`. |

### Geometry

| # | Arquivo | Descricao |
|---|---------|-----------|
| 41 | `Geometry/BoxGeometry.cs:57-60` | Inercia sem multiplicador de volume. C++ armazena `V * inertia`. Afeta todas as geometrias (sphere, cylinder tambem). |
| 42 | `Geometry/GeometryBase.cs:92-95` | `Intersect()` nao override em nenhuma subclasse. Sem BVTree traversal. Core da colisao faltando. |
| 43 | `Geometry/TriMeshGeometry.cs:225` | `RayTriangleIntersect` tem `t < 1f` limitando ray a segmento finito. Deveria ser `t > 0`. |

### World

| # | Arquivo | Descricao |
|---|---------|-----------|
| 44 | `World/PhysicalWorld.cs:24-43` | `PhysicsVars` defaults errados: `MaxWorldStep=0.02` (C++ usa 0.2, 10x), `Gravity.Z=-9.81` (C++ usa -9.8), `TimeGranularity=0.001` (C++ usa 0.0001). |
| 45 | `World/PhysicalWorld.cs:91-95` | Gravity so atribuida a RigidEntity. C++ aplica a todos os tipos dinamicos. |

### Algorithms

| # | Arquivo | Descricao |
|---|---------|-----------|
| 46 | `Algorithms/WaterManager.cs:160-163` | Wave equation duplo-aplica `dt`. `c2 = speed^2*dt^2` + `h += vel*dt` = proporcional a `dt^3`. |

---

## ISSUES MEDIUM (28)

| # | Arquivo | Descricao |
|---|---------|-----------|
| 47 | `Math/PhysMatrix33.cs` | Missing `Vec3 * Matrix33` pre-multiply operator (v como row vector). |
| 48 | `Math/PhysMatrix33.cs` | Missing `Matrix33 * Diag33` operator (column scaling). |
| 49 | `Math/PhysQuaternion.cs` | Missing `q * v`, `v * q`, `q + q`, `q - q`, `q * scalar`, `q / scalar`, `Dot()` operators. |
| 50 | `Math/Quotient.cs` | Missing `sgn_safe`, `sgnnz_safe`, `isneg_safe` functions. |
| 51 | `Math/MatrixNM.cs:270-281` | Determinant sign calc diferente (swap counter vs C++ LUidx check). Internamente consistente. |
| 52 | `Math/MatrixNM.cs` | Missing: `conjugate_gradient`, `biconjugate_gradient`, `minimum_residual`, `LPsimplex`. |
| 53 | `Math/VectorN.cs:123-140` | Missing stride support para `vec * matrix` product. |
| 54 | `Math/MatrixNM.cs:299-319` | `Add/Subtract/Scale` nao atualizam flags (symmetric, identity). |
| 55 | `Dynamics/RigidBody.cs:137-145` | `Energy` property cria 2 matrizes temporarias. Deveria usar `W.Dot(L)` como C++. |
| 56 | `Dynamics/RigidBody.cs:27` | `IBody` e full Matrix33 (9 floats). C++ usa Diag33 (3 floats). Desperdicio e nao expressa invariante. |
| 57 | `Entities/PhysicalEntity.cs:81-87` | `SetParams(ParamsPos)` com scale nao propaga para parts (mass, position). |
| 58 | `Entities/PhysicalEntity.cs:149` | `AddGeometry` part ID: se caller passa `id=100`, prox auto-ID pode ser 0 (colisao). |
| 59 | `Entities/PhysicalEntity.cs:15-29` | `EntityGeom` faltando: `pPhysGeomProxy`, `pMatMapping`, `maxdim`, `idmatBreakable`. |
| 60 | `Entities/PhysicalEntity.cs` | Missing virtual methods: `StartStep`, `GetMaxTimeStep`, `StepBack`, `RegisterContacts`, `CalcEnergy`, `GetDamping`, etc. |
| 61 | `Entities/RigidEntity.cs:147-157` | Sleep logic usa timeout de 0.5s fixo. C++ usa frame-counting + accumulated energy. |
| 62 | `Entities/WheeledVehicleEntity.cs:199` | `KDamping` direto. C++ converte negativo para fracao de critical damping: `-value * sqrt(4*k*m)`. |
| 63 | `Entities/WheeledVehicleEntity.cs:19-87` | `SuspensionPoint` faltando `pCollEvent`, `pbody`, `entity_contact`. |
| 64 | `Entities/LivingEntity.cs:140-143` | Ground movement sem slope projection, sem slide/climb limits, sem fall-off. |
| 65 | `Entities/ParticleEntity.cs:174-197` | Integration em steps separados (thrust, lift, gravity, drag, position). C++ aplica tudo junto. |
| 66 | `Entities/ParticleEntity.cs:168-230` | Missing water/medium handling. `WaterGravity`/`KWaterResistance` armazenados mas nunca usados. |
| 67 | `Entities/ArticulatedEntity.cs:190-193` | ~10 defaults errados: `CheckCollisions=true` (C++: false), `NRoots=1` (C++: 0), `SimType=0` (C++: 1), etc. |
| 68 | `Entities/ArticulatedEntity.cs:203-213` | `SetParams(ParamsArticulatedBody)` faltando A/Wa/W/V, BAwake, lying mode params, `bGrounded==100` sentinel. |
| 69 | `Entities/RopeEntity.cs:220-246` | Constraint solving e Jakobsen simples. C++ usa propagacao sequencial + solver de velocidade + friction. |
| 70 | `Entities/SoftEntity.cs/RopeEntity.cs` | Missing data structures: `rope_solver_vtx`, `m_vtx1` (subdivision), `SoftEntity.m_pCore`, `check_part` structs. |
| 71 | `World/PhysicalWorld.cs:58` | `SurfaceParams[256]`. C++ usa `NSURFACETYPES=512`. Faltando dynamic friction. |
| 72 | `World/PhysicalWorld.cs:184-202` | `GetEntitiesInBox` ignora `objectTypes` parameter. Brute-force O(n). |
| 73 | `Geometry/GeometryManager.cs` | Missing: serialization, cloning, deferred release, thread safety, pooled allocation, `AddRefGeometry`. |
| 74 | `BVTrees/AABBTree.cs:12-22` | Nodes usam full Vector3 min/max. C++ usa bytes quantizados (6x menos memoria). Formato incompativel. |
| 75 | `Algorithms/ConvexHull.cs:121-122` | Novas faces apos expansion podem ter winding errado. Falta verificacao de normal vs centroid. |
| 76 | `Algorithms/TetrahedralLattice.cs:169-208` | `CheckStructure` sem CG solver, sem explosion forces, sem ground planes. |
| 77 | `Params/PhysicsParams.cs:298-299` | `StatusDynamics.SubmergedFraction` e `TimeIdle` declarados como `int`, deveriam ser `float`. |

---

## ISSUES LOW (17+)

| # | Arquivo | Descricao |
|---|---------|-----------|
| 78 | `Math/PhysQuaternion.cs` | Missing: `Slerp`, `Nlerp`, `GetInverted()` alias, `SetRotationXYZ`. |
| 79 | `Math/PhysMatrix33.cs` | Missing: `Adjoint()`, `SetRotationXYZ`, Euler angle constructor. |
| 80 | `Math/MathUtils.cs:75` | `SgnNZ` doc misleading para `-0.0f` (retorna -1 mas doc diz "1 se x >= 0"). |
| 81 | `Math/MathUtils.cs:194` | `Cubert(double)` cast to float para sign check perde precisao. Matches C++. |
| 82 | `Math/Polynomial.cs:139` | Raiz linear `c0/c1` sem negacao. Bug original do C++ fielmente portado. |
| 83 | `Math/Polynomial.cs:343` | Subtracao corrige bug do C++ (nega termos de grau superior). Divergencia intencional. |
| 84 | `Entities/PhysicalEntity.cs:37` | `_refCount = 1` (C++ inicia em 0). Off-by-one em ref counting. |
| 85 | `Entities/WheeledVehicleEntity.cs:194` | `SuspLenMax` set sem clampar `len0`/`curlen` dependentes. |
| 86 | `Geometry/TriMeshGeometry.cs:187-204` | `PointInsideStatus` brute-force +Z ray crossing vs C++ hash-grid closest-hit normal. |
| 87 | `Algorithms/TetrahedralLattice.cs:235-253` | `Defragment` nao remapeia `IBuddy` neighbor indices. |
| 88 | `Threading/CallerContext.cs:14` | `MaxPhysThreads=4` sem pool management de iCaller indices. |
| 89 | `Events/PhysicsEvents.cs` | Missing 4 event types: `PWIResult`, `RevealEntityPart`, `EntityDeleted`, `PostPump`. |
| 90 | `World/PhysicalWorld.cs:244` | `RayIntersectsAABB` usa `tmax=1` (ray finito). OK se `dir` e vetor completo, mas nao documentado. |
| 91 | `World/PhysicalWorld.cs:106-110` | `DestroyPhysicalEntity` sem mode parameter, sem deferred deletion, sem protecao contra mutation durante iteracao. |
| 92 | `Algorithms/WaterManager.cs:40-44` | `WaterTile.Zero()` nao limpa `MomentumVec`/`Mass`. Matches C++ bug. |
| 93 | `Dynamics/RigidBody.cs:75-80` | `UpdateState` sem guard `MassInv > 0`. Pode produzir NaN para static bodies com IBodyInv lixo. |
| 94 | `Serialization/WorldSerializer.cs` | Quaternion component order pode nao ser compativel com formato binario C++. |

---

## Prioridades de Fix

### Tier 1 - Bugs que quebram funcionalidade basica
1. **Event TypeIds** (#16) - quebra todo roteamento de eventos
2. **Polynomial constructor** (#1) - inverte semantica de criacao de polinomios  
3. **AeJoint.Flags** (#8) - constante errada afeta todos os joints articulados
4. **SetJointParams lookup** (#9) - modifica joint errado
5. **SimulateExplosion ||** (#13) - bug de logica trivial de corrigir
6. **MathUtils.Sgn** (#2) - retorna 1 para zero
7. **PhysicsVars defaults** (#44) - MaxWorldStep 10x errado
8. **ParticleEntity Dim** (#36) - half-size vs full-size
9. **StatusDynamics types** (#77) - int vs float

### Tier 2 - Formulas fisicas erradas
10. **SoftEntity constraint** (#18) - `Ks * dt` em correcao de posicao
11. **RigidBody quaternion integration** (#7) - Taylor vs exponential map
12. **Rope wind force** (#40) - aceleracao constante vs drag model
13. **Particle drag** (#35) - formula inventada
14. **Living air control** (#32) - formula simplificada
15. **Living slope angles** (#34) - defaults + nunca convertidos
16. **Inertia volume multiplier** (#41) - afeta box/sphere/cylinder
17. **TriMesh inertia** (#17) - AABB approximation
18. **WaterManager wave** (#46) - dt^3

### Tier 3 - Funcionalidade core faltando
19. **ComputeBBox rotation** (#26)
20. **RecomputeMassProperties Steiner** (#30)
21. **RigidBody.Step separation** (#6)
22. **Geometry Intersect** (#42) - sem BVTree traversal
23. **Living ground detection** (#10)
24. **Particle collision** (#11)
25. **PhysicalWorld TimeStep** (#12) - sem collision pipeline

### Tier 4 - Simulacao avancada faltando  
26. WheeledVehicle DoStep (#31)
27. Articulated Featherstone (#37)
28. Rope/Soft collision (#Issue 5 Onda 4)
29. PhysicalWorld spatial grid
30. Contact constraint solver
