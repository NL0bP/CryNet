# CryPhysics.Sharp patches made during CryAISystem.Sharp port

When porting a CryAISystem file, if a type/function consumed from
CryPhysics.Sharp diverges from the C++ original (CryCommon / CryPhysics
headers), the FIX goes here in CryPhysics.Sharp — not a workaround in
CryAISystem.Sharp.

Format:
- **<C# file in CryPhysics.Sharp>** ← <C++ source ref>
  - what diverged
  - how it was fixed (link to commit / diff)

## 2026-04-11 — Phase 1, AIHash + Reference

- **CryPhysics.Sharp/src/CryPhysics.Core/Math/PhysVector3.cs** ← Cry_Vector3.h
  - C++ Vec3 exposes lowercase `x/y/z` fields (also `v[0]/v[1]/v[2]`); CryPhysics.Sharp had only `X/Y/Z`.
  - Added lowercase property aliases (`x`, `y`, `z`) that get/set `X/Y/Z` via inlined accessors.

- **CryPhysics.Sharp/src/CryPhysics.Core/Math/PhysQuaternion.cs** ← Cry_Quat.h
  - C++ Quat layout is `Vec3 v; float w;` — accessed as `q.v.x`, `q.v.y`, `q.v.z`, `q.w`.
  - CryPhysics.Sharp uses flat `W, X, Y, Z`. Added properties `v` (returns/sets a `PhysVector3`) and `w` (alias of `W`).

- **CryPhysics.Sharp/src/CryPhysics.Core/Params/PhysicsParams.cs (StatusPos)** ← physinterface.h pe_status_pos
  - C++ pe_status_pos has lowercase `pos`, `q`, `scale`. CryPhysics.Sharp uses `Position`, `Orientation`, `Scale`.
  - Added lowercase property aliases (`pos`, `q`, `scale`).

- **Naming-level discrepancy (logged, not fixed yet)**: CryPhysics.Sharp prefixes math types with `Phys` (`PhysVector3`, `PhysQuaternion`, `PhysMatrix33`) instead of the C++ `Vec3`/`Quat`/`Matrix33`. The CryAISystem.Sharp port works around this with `global using` aliases in `CryPhysicsAliases.cs`. A full rename of CryPhysics.Sharp to use the C++ names would be more faithful but is a much larger change deferred for later.

## 2026-04-11 — Session 3, AICollision lighting up

- **CryPhysics.Sharp/src/CryPhysics.Core/Math/PhysVector3.cs** ← Cry_Vector3.h
  - Added `NormalizeSafe()` / `NormalizeSafe(safe)` (port of Vec3::NormalizeSafe).
  - Added `GetNormalizedSafe()` / `GetNormalizedSafe(safe)` (port of Vec3::GetNormalizedSafe).
  - Added `GetLengthSquared()`, `GetLengthSquared2D()`, `len2()` (port of Vec3 length helpers).
  - Added `IsZero(epsilon=0)`, `IsValid()`, `IsEquivalent(other, epsilon=0.05f)` (port of Vec3 predicates).
  - Added `PhysVector3(PhysVector2 v)` ctor (port of Vec3::Vec3(const Vec2&) — Z=0).

- **CryPhysics.Sharp/src/CryPhysics.Core/Math/PhysVector3.cs (PhysVector2)** ← Cry_Vector2.h
  - Added lowercase property aliases `x`, `y`.
  - Added `PhysVector2(PhysVector3 v)` ctor (port of Vec2::Vec2(const Vec3&) — truncates Z).

- **CryPhysics.Sharp/src/CryPhysics.Core/Math/PhysMatrix33.cs** ← Cry_Math.h Matrix33
  - Added `Transpose()` (in-place, distinct from existing `Transposed()` which returns a copy).
  - Added `GetTransposed()` alias.
  - Added `TransformVector(in PhysVector3 v)` (matrix*vector).
  - Added `SetFromVectors(in vx, in vy, in vz)` (set columns from three vectors).

- **CryPhysics.Sharp/src/CryPhysics.Core/Params/PhysicsParams.cs (StatusPos)** ← physinterface.h pe_status_pos
  - Added `BBox` accessor returning `PhysVector3[2]` to match the C++ `Vec3 BBox[2]` array layout.

## 2026-04-11 — Session 4, ObjectContainer.cpp + SAIObjectCreationHelper

(no CryPhysics.Sharp patches required this session — all changes were in CryAISystem.Sharp)
