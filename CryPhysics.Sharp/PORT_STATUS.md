# CryPhysics.Sharp — Port Status

Tracker of REVIEW_REPORT.md issues. Legend: ⬜ pending · 🟡 in-progress · ✅ done · 🔴 deferred.

**Build**: 0 errors (2026-04-14). **Last commit**: `2420b9d` (WIP #42).

---

## Tier 1 — Trivial bugs (0/9)

| ID | File:line | Issue | Status |
|----|-----------|-------|--------|
| #1  | Math/Polynomial.cs:33 | Constructor coloca em `this[0]`, C++ em `data[degree]` | ⬜ |
| #2  | Math/MathUtils.cs:68 | `Sgn(+0.0f)` retorna 1, deve retornar 0 | ⬜ |
| #8  | Entities/ArticulatedEntity.cs:65 | `AeJoint.Flags=0x3F`, C++ usa `0x07` | ⬜ |
| #9  | Entities/ArticulatedEntity.cs:220 | `SetJointParams` usa `Op0` index; C++ faz lookup por body ID em `Op1` | ⬜ |
| #13 | World/PhysicalWorld.cs:209 | `SimulateExplosion` precedencia `&& ∥` | ⬜ |
| #16 | Events/PhysicsEvents.cs | 8 de 12 event TypeIds errados | ⬜ |
| #36 | Entities/ParticleEntity.cs:91 | `Dim=Size`, C++ usa `size*0.5f` | ⬜ |
| #44 | World/PhysicalWorld.cs:24-43 | PhysicsVars: MaxWorldStep/Gravity/TimeGranularity errados | ⬜ |
| #77 | Params/PhysicsParams.cs:298-299 | SubmergedFraction + TimeIdle sao `int`, devem ser `float` | ⬜ |

## Tier 2 — Wrong physics formulas (0/9)

| ID | File:line | Issue | Status |
|----|-----------|-------|--------|
| #7  | Dynamics/RigidBody.cs:107-116 | Quaternion Taylor 1a ordem vs exponential map | ⬜ |
| #17 | Geometry/TriMeshGeometry.cs:99-117 | CalcPhysicalProperties usa AABB inercia | ⬜ |
| #18 | Entities/SoftEntity.cs:208 | `diff*0.5*Ks*dt` mistura stiffness+timestep | ⬜ |
| #32 | Entities/LivingEntity.cs:131-134 | Air control formula simplificada | ⬜ |
| #34 | Entities/LivingEntity.cs:41-42 | Slope angles graus, nao convertidos, defaults errados | ⬜ |
| #35 | Entities/ParticleEntity.cs:189-194 | Drag formula inventada | ⬜ |
| #40 | Entities/RopeEntity.cs:210 | Wind como aceleracao constante, nao drag | ⬜ |
| #41 | Geometry/BoxGeometry.cs:57-60 | Inercia sem multiplicador de volume | ⬜ |
| #46 | Algorithms/WaterManager.cs:160-163 | Wave eq aplica `dt` duas vezes | ⬜ |

## Tier 3 — Missing core (0/7 + 1 WIP)

| ID | File | Issue | Status |
|----|------|-------|--------|
| #6  | Dynamics/RigidBody.cs:86-121 | Step separacao forca/posicao | ⬜ |
| #10 | Entities/LivingEntity.cs:120-152 | Ground detection | ⬜ |
| #11 | Entities/ParticleEntity.cs:168-230 | Collision raycast+bounce | ⬜ |
| #12 | World/PhysicalWorld.cs:116-139 | TimeStep pipeline completo | ⬜ |
| #26 | Entities/PhysicalEntity.cs:209-220 | ComputeBBox transform OBB | ⬜ |
| #30 | Entities/RigidEntity.cs:198-205 | RecomputeMassProperties Steiner | ⬜ |
| #42 | Geometry/GeometryBase.cs:92-95 | BVTree-driven Intersect | 🟡 WIP |

## Tier 4 — Advanced (0/5)

| ID | Area | Issue | Status |
|----|------|-------|--------|
| #31 | WheeledVehicleEntity | DoStep suspension/tires/engine | ⬜ |
| #37 | ArticulatedEntity | Featherstone solver | ⬜ |
| —   | RopeEntity/SoftEntity | Collision + subdivision | ⬜ |
| —   | Contact solver | PGS completo | ⬜ |
| —   | World | Spatial grid + ray world | ⬜ |

## HIGH/MEDIUM/LOW remaining

Issues #3-5, #14-15, #19-25, #27-29, #33, #38-39, #43, #45, #47-94: listed in REVIEW_REPORT.md, tracked individually as each tier completes.
