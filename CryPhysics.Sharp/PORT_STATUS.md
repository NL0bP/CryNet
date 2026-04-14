# CryPhysics.Sharp — Port Status

Tracker of REVIEW_REPORT.md issues. Legend: ⬜ pending · 🟡 in-progress · ✅ done · 🔴 deferred · ❓ stale (REVIEW_REPORT entry no longer matches current code).

**Build**: 0 errors (2026-04-14). **Last commit**: `be7d241` (tracking docs).

---

## Audit result (2026-04-14)

REVIEW_REPORT.md was written against an older snapshot. Verified against current code:
**most Tier 1 + several Tier 2/3 issues are already fixed.** Below is the actual state.

### Tier 1 — Trivial bugs (9/9 ALREADY FIXED ❓)

| ID | File:line | Original issue | Current state | Status |
|----|-----------|----------------|---------------|--------|
| #1  | Math/Polynomial.cs:34 | Constructor coloca em `[0]` | `this[degree] = leadingCoeff` | ❓ already correct |
| #2  | Math/MathUtils.cs:66-71 | `Sgn(+0.0f)=1` | Bit-conversion port `(i>>31)+((i-1)>>31)+1` matches C++ | ❓ already correct |
| #8  | Entities/ArticulatedEntity.cs:19 | `Flags=0x3F` | `JointFlags.AllAnglesLocked = 7` matches C++ `all_angles_locked=7` | ❓ already correct |
| #9  | Entities/ArticulatedEntity.cs:279-289 | `Op0` index lookup | Faz lookup por `IdBody == childBodyId` (matches C++ `m_joints[i].idbody!=params->idbody`) | ❓ already correct |
| #13 | World/PhysicalWorld.cs:492 | `&&\|\|` precedence + IsAwake | Codigo nao tem o `IsAwake&&\|\|` flagged; falloff linear ainda existe (tracked as Tier 2 #14) | ❓ literal bug fixed |
| #16 | Events/PhysicsEvents.cs | 8 TypeIds errados | Todos com TypeId correto + comentario `// C++ EventPhysX id = N` | ❓ already correct |
| #36 | Entities/ParticleEntity.cs:105 | `Dim=Size` | `Dim = pp.Size.Value * 0.5f` matches C++ `m_dim = size*0.5f` | ❓ already correct |
| #44 | World/PhysicalWorld.cs:42-46 | PhysicsVars defaults | `Gravity.Z=-9.8`, `MaxWorldStep=0.2`, `TimeGranularity=0.0001` correct | ❓ already correct |
| #77 | Params/PhysicsParams.cs:317-318 | int vs float | `public float SubmergedFraction`, `public float TimeIdle` | ❓ already correct |

### Tier 2 — Wrong physics formulas (audit)

| ID | File:line | Status |
|----|-----------|--------|
| #7  | Dynamics/RigidBody.cs:191-233 | ❓ Already correct — Step() uses RK4 + WDt exp map + energy correction, separated from forces |
| #17 | Geometry/TriMeshGeometry.cs | ⬜ Needs verification |
| #18 | Entities/SoftEntity.cs | ⬜ Needs verification (DoStep at :212 uses different model than reported) |
| #32 | Entities/LivingEntity.cs | ⬜ Needs verification |
| #34 | Entities/LivingEntity.cs:97-100 | ❓ Already correct — slopes stored as cosines `MathF.Cos(MathF.PI*0.2f)` for 36deg matches C++ |
| #35 | Entities/ParticleEntity.cs | ⬜ Needs verification |
| #40 | Entities/RopeEntity.cs | ⬜ Needs verification |
| #41 | Geometry/BoxGeometry.cs:50-63 | ❓ Already correct — `(sy2+sz2)/12f * v` has volume multiplier |
| #46 | Algorithms/WaterManager.cs:155-163 | ⬜ Needs verification — c2 definition not yet checked |

### Tier 3 — Missing core (audit)

| ID | File | Status |
|----|------|--------|
| #6  | Dynamics/RigidBody.cs | ❓ Already done (see #7 audit) |
| #10 | Entities/LivingEntity.cs | ⬜ Needs verification — file is 600+ LOC, may already have ground detection |
| #11 | Entities/ParticleEntity.cs | ⬜ Needs verification |
| #12 | World/PhysicalWorld.cs | ⬜ Needs verification — TimeStep pipeline |
| #26 | Entities/PhysicalEntity.cs | ⬜ ComputeBBox transform OBB |
| #30 | Entities/RigidEntity.cs | ⬜ RecomputeMassProperties Steiner — but RigidBody.Add already does parallel-axis (line 113-114), may apply to entity too |
| #42 | Geometry/GeometryBase.cs | 🟡 WIP commit `2420b9d` — BVTree-driven Intersect scaffold |

### Tier 4 — Advanced

Same status as before — not yet audited.

---

## REAL pending work (high confidence) — updated 2026-04-14

| C++ file | LOC | Status |
|----------|-----|--------|
| `boolean2d.cpp` | ~800 | 🔴 deferred — no consumer in C# port (see deferred.md) |
| `boolean3d.cpp` | ~1200 | 🔴 deferred — no consumer in C# port |
| `capsulegeom.cpp` | ~280 | 🟡 partial — `CalcPhysicalProperties`/`PointInsideStatus`/`PrepareForIntersectionTest` ported this session; `CalculateBuoyancy`/`MediumResistance`/`DrawToOcclusionCubemap`/`UnprojectSphere`/`GetUnprojectionCandidates` still inherit cylinder behaviour |
| `physicalplaceholder.cpp` | ~227 | 🔴 deferred — depends on missing infra (streamer, snapshot serialization, three-phase step, IsPlaceholder/RepositionEntity on world) |
| `rwi.cpp` | ~600 | 🔴 deferred — async deferred-result queue not present in C# |
| `voxelbv.cpp` | ~200 | 🔴 deferred — functionality inlined in VoxelGeometry.cs |

Plus issue #42 (BVTree-driven `CGeometry::Intersect`) — WIP commit `2420b9d`, full traversal deferred (see deferred.md).

---

## Next steps (suggested order)

1. **Re-audit Tier 2/3 ❓ entries** — verify against current code, mark stale or pending.
2. **Finish issue #42** — implement `CGeometry::Intersect` BVTree traversal literally from `geometry.cpp`.
3. **Port the 5 missing .cpp files**, smallest first: `physicalplaceholder.cpp` → `capsulegeom.cpp` → `rwi.cpp` → `boolean2d.cpp` → `boolean3d.cpp`.
4. After missing files done, re-audit remaining REVIEW_REPORT issues against the now-canonical code.
