# CryPhysics.Sharp — Skeleton Completion Plan

> **Audience**: downstream agent completing the literal C++ → C# port of `dev/Code/CryEngine/CryPhysics/` into `CryPhysics.Sharp/`. The structural skeleton already exists (55 files, ~24.7k LOC, 0 build errors) but **most simulation logic is simplified, missing, or wrong** — see `REVIEW_REPORT.md` (94 issues).

---

## 0. Hard rules (NON-NEGOTIABLE)

1. **Literal line-by-line C++ → C# translation.** Sem refactor, sem melhorias, sem idiomas C# "mais limpos", sem error handling adicional, sem renomear simbolos, sem null checks novos. Se o C++ faz X, o C# faz X. Cada `class`/`struct` com seus membros, cada metodo statement-por-statement.
2. **Se nao compilar, deixe quebrar.** Proibido criar stubs, placeholders, fake glue. Arquivo quebrado vai para `deferred.md` e segue-se em frente.
3. **Sem logica inventada.** Se a formula nao existe no C++, nao invente. Se nao sabe — vai para `deferred.md` com citacao do arquivo/linha C++.
4. **Revisao linha-a-linha apos cada fix.** Side-by-side C# vs C++. Checklist:
   - (a) todas as variaveis presentes com tipos exatos
   - (b) corpo statement-por-statement
   - (c) constantes exatas (nao arredondar `9.81` para `9.8`)
   - (d) zero validacao adicional
5. **Verificacao build-only.** 0 errors obrigatorio. Warnings CS0649/CS0169 sobre campos nunca usados sao OK se o C++ declara o campo. Sem runtime tests.
6. **Commit por tier/issue**. Um commit por issue do REVIEW_REPORT ou por grupo tematico pequeno. Mensagem: `CryPhysics #<issue>: <short desc>` com ref ao arquivo/linha C++.

---

## 1. Sources

- **C++ origem**: `c:/Users/Taka/Downloads/lumberyard_1-0_fr_432777/dev/Code/CryEngine/CryPhysics/`
  - Principais: `physicalworld.cpp` (19k L), `rigidbody.cpp`, `articulatedentity.cpp` (6k L), `livingentity.cpp`, `particleentity.cpp`, `geometry.cpp`, `aabbtree.cpp`, `obbtree.cpp`, `heightfieldgeom.cpp`, `trimesh.cpp`, `overlapchecks.cpp`, `intersectionchecks.cpp`, `linunprojectionchecks.cpp`, `matrixnm.cpp`, `polynomial.h`, `quotient.h`.
- **C# destino**: `CryPhysics.Sharp/src/CryPhysics.Core/`
- **Issue tracker**: `REVIEW_REPORT.md` (94 issues numerados)
- **Status**: `PORT_STATUS.md`
- **Erros nao resolvidos**: `deferred.md`

---

## 2. Execution order (by tier)

Seguir estritamente por tier. NAO pular. Cada tier tem dependencias dos anteriores.

### Tier 1 — Trivial bugs (9 issues)
Bugs de constante/semantica, geralmente 1-10 linhas cada. Nenhuma dependencia arquitetural.

- #1 Polynomial.cs constructor — `data[degree]` nao `data[0]`
- #2 MathUtils.Sgn — `+0.0f` deve retornar 0
- #8 ArticulatedEntity.cs AeJoint.Flags — `0x07` nao `0x3F`
- #9 ArticulatedEntity.cs SetJointParams — lookup por body ID
- #13 PhysicalWorld.cs SimulateExplosion — precedencia `&&`/`||`
- #16 PhysicsEvents.cs TypeIds — corrigir 8 de 12
- #36 ParticleEntity.cs Dim — `size * 0.5` nao `size`
- #44 PhysicsVars defaults — MaxWorldStep=0.2, Gravity.Z=-9.8, TimeGranularity=0.0001
- #77 StatusDynamics types — SubmergedFraction e TimeIdle sao float

### Tier 2 — Wrong physics formulas (9 issues)
Formulas erradas ou simplificadas. Requerem leitura do .cpp original para copiar a formula exata.

- #18 SoftEntity constraint — velocity solver Gauss-Seidel
- #7 RigidBody quaternion integration — exponential map
- #40 RopeEntity wind drag model
- #35 ParticleEntity drag formula
- #32 LivingEntity air control — `KInertia * dt * KAirControl`
- #34 LivingEntity slope angles — defaults 36/54 + conversao graus→cos
- #41 BoxGeometry + afins — inercia vezes volume
- #17 TriMeshGeometry inercia — integral de superficie
- #46 WaterManager wave — corrigir duplo-`dt`

### Tier 3 — Missing core functionality (7 issues)
Features faltantes centrais ao pipeline. Cada uma pode ser 200-800 linhas.

- #26 PhysicalEntity.ComputeBBox — transform OBB por rotacao
- #30 RigidEntity.RecomputeMassProperties — parallel axis
- #6 RigidBody.Step — separar forca/posicao (ja WIP via commit atual)
- #42 Geometry.Intersect + BVTree traversal (JA EM PROGRESSO no commit 2420b9d)
- #10 LivingEntity ground detection
- #11 ParticleEntity collision raycast+bounce
- #12 PhysicalWorld.TimeStep — broadphase/narrowphase/solver/islands

### Tier 4 — Advanced simulation (5 systems)
Sistemas completos. Multi-sessao. Ler .cpp inteiro antes de tocar.

- #31 WheeledVehicleEntity.DoStep — suspension/tires/engine
- #37 ArticulatedEntity Featherstone — StepJoint/CalcBodyZa/CalcBodyIa/SyncBodyWithJoint
- Rope/Soft collision
- Contact constraint solver PGS completo
- Deferred issues da Tier 3 acima (spatial grid, ray world, etc)

---

## 3. Per-issue workflow

Para cada issue:

1. **Ler** REVIEW_REPORT.md entry (numero, file:line, descricao).
2. **Abrir** o arquivo C++ original citado (e.g. `dev/Code/CryEngine/CryPhysics/particleentity.cpp`).
3. **Localizar** o metodo/bloco exato no C++ via Grep.
4. **Ler** o C# atual.
5. **Reescrever** o bloco C# como traducao literal do C++. Se o C++ usa `a.x*b.x + a.y*b.y + a.z*b.z`, o C# usa exatamente isso (nao substituir por `Vec3.Dot`).
6. **Build**: `dotnet build CryPhysics.Sharp/CryPhysics.Sharp.sln --nologo` deve dar 0 errors.
7. **Commit** isoladamente.
8. **Atualizar** `PORT_STATUS.md` marcando a issue como concluida.
9. Se nao conseguir traduzir por falta de dependencia → registrar em `deferred.md` com citacao C++ e motivo.

---

## 4. Conventions (same as existing skeleton)

- `class CFoo` em .h + .cpp → **um arquivo `Foo.cs` (sem prefixo `C`)**
- `struct foo` → `struct Foo` ou `class Foo` se tem virtual/herança
- Campos publicos C++ → campos/`internal` C# (nao privatizar)
- `Vec3`, `Vec2`, `Matrix33`, `Quat`, `Diag33`, `AABB` → `PhysVector3`, `PhysVector2`, `PhysMatrix33`, `PhysQuaternion`, `Diag33`, `AABB` no namespace `CryPhysics.Math`
- `primitives::sphere` etc. → `CryPhysics.Primitives.Sphere` etc
- Ponteiros nao-owning C++ → referencia C#
- `float*` arrays → `float[]`
- bitfields → campos `int`/`uint` individuais (C# nao tem bitfield)
- `const T&` parametros → `T` direto (ou `in` apenas se proibido por semantica — default: sem `in`)
- `_smart_ptr<T>` → referencia C# direta
- C++ `assert` → remover (nao portar)
- Macros `IF_UNLIKELY`/`UNUSED_PARAM` etc. → remover

---

## 5. Current state (2026-04-14)

- Build: **0 errors**, 32 warnings (pre-existentes, nao bloqueantes)
- Issue #42 (Geometry.Intersect + BVTree): **WIP em progresso** (commit `2420b9d`)
  - Adicionado: `BVTree.PrepareForIntersectionTest`, `BVTree.MaxPrimsInNode`, `GeometryBase.PrepareForIntersectionTest`, `GeometryUnderTest` sweep/primbuf fields, `PrimInters.NBorderSz`.
  - Falta: implementar traversal recursivo `CGeometry::Intersect` de `geometry.cpp`.
- Todas as outras 93 issues: PENDENTES.
