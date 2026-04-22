# CryPhysics.Native

CryPhysics.Native is a small C# wrapper plus C ABI facade for running Lumberyard's C++ CryPhysics as a native library from .NET.

The managed API keeps Lumberyard/CryEngine internals behind opaque handles, so consumers can create worlds, rigid entities, geometries, water areas, heightfield callbacks, apply impulses, and step simulation without depending on the full engine.

This directory is intentionally self-contained and can be promoted to its own public repository. ArcheAze consumes it from `external/CryPhysics.Native` today; game-specific adapters live outside this library under `AAEmu.Game/Physics/Native`.

## Layout

```text
CryPhysicsNative/
  CryPhysicsNative.csproj        C# wrapper assembly, outputs CryPhysics.Native.dll
  NativeMethods.cs               P/Invoke declarations for the C ABI
  NativePhysicalWorld.cs         High-level managed world wrapper
  NativeRigidEntity.cs           High-level managed rigid entity wrapper
  NativeGeometry.cs              Managed geometry lifetime wrapper
  Marshal/AbiStructs.cs          Structs shared with native ABI
  Handles/                       SafeHandle wrappers
  include/cryphysics_api.h       Public C ABI
  src/api.cpp                    C facade over Lumberyard CryPhysics
  src/azcore_stubs.cpp           Headless AzCore symbols needed by CryPhysics
  stubs/                         Minimal renderer/debug stubs
  scripts/fetch-lumberyard.*     Sparse checkout helper for Lumberyard sources
  CMakeLists.txt                 Native CryPhysics.dll/libCryPhysics.so build
```

## What Belongs Here

Keep this library generic:

- world lifecycle and timestep
- rigid entity lifecycle
- primitive/trimesh/heightfield geometry creation
- surface and water registration
- typed actions/status needed by common servers
- ABI/version/smoke-test helpers

Keep game-specific code in the host project:

- ArcheAge/AAEmu ship model lookup
- packet broadcasting and movement serialization
- world/cell streaming adapters
- project-specific logging policy
- gameplay tuning values

## Build

From this directory:

```powershell
.\scripts\fetch-lumberyard.ps1
cmake -B build -A x64
cmake --build build --config Release
dotnet build CryPhysicsNative.csproj
```

Linux/macOS style:

```bash
./scripts/fetch-lumberyard.sh
cmake -B build
cmake --build build --config Release
dotnet build CryPhysicsNative.csproj
```

The native build writes:

- Windows: `bin/win-x64/CryPhysics.dll`
- Linux: `bin/linux-x64/libCryPhysics.so`

The C# project copies those binaries into the normal .NET runtime asset layout when they exist:

```text
runtimes/win-x64/native/CryPhysics.dll
runtimes/linux-x64/native/libCryPhysics.so
```

## Minimal C# Example

```csharp
using CryPhysics.Native;
using CryPhysics.Native.Marshal;

NativeLibraryResolver.Register();

using var world = NativePhysicalWorld.Create(new WorldCfg
{
    GravityZ = -9.81f,
    MaxTimeStep = 0.02f,
    SingleThreaded = 1,
});

world.SetupEntityGrid(new CpVec3(-64f, -64f, 0f), nx: 16, ny: 16, stepX: 8f, stepY: 8f);

using var box = world.CreateBoxGeometry(new CpVec3(0.5f, 0.5f, 0.5f), new CpVec3(0f, 0f, 0f));
using var entity = world.CreateRigidEntity(new EntityCfg
{
    Id = 1,
    Pos = new CpVec3(0f, 0f, 10f),
    Rot = CpQuat.Identity,
    Mass = 10f,
});

entity.AddGeometry(box, new PartParams
{
    Pivot = new CpVec3(0f, 0f, 0f),
    Rot = CpQuat.Identity,
    Mass = 10f,
    SurfaceId = 0,
});

world.TimeStep(1f / 60f);
var pos = entity.GetPos();
```

## Consuming From ArcheAze

ArcheAze currently references the project directly:

```xml
<ProjectReference Include="..\external\CryPhysics.Native\CryPhysicsNative.csproj" />
```

After publishing this as a standalone GitHub repo or NuGet package, ArcheAze can switch to either:

```xml
<ProjectReference Include="..\external\CryPhysics.Native\CryPhysicsNative.csproj" />
```

or:

```xml
<PackageReference Include="CryPhysics.Native" Version="0.1.0-alpha" />
```

The adapters in `AAEmu.Game/Physics/Native` should stay in ArcheAze because they translate AAEmu world, terrain, water, ship, and packet concepts into this generic API.

## License

This wrapper/facade is intended to be published under Apache-2.0. Lumberyard source files fetched by `scripts/fetch-lumberyard.*` retain their original Amazon Lumberyard notices and license.
