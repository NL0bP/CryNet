// Global type aliases mapping CryEngine C++ math/physics names to the
// already-ported CryPhysics.Sharp types. CryPhysics.Sharp renamed several
// types with a "Phys" prefix during its own port; this file restores the
// C++ names so that the literal CryAISystem port can write `Vec3`, `Quat`,
// `pe_status_pos`, etc. without per-file aliases.
//
// Discrepancy logged in cryphysics_patches.md (CryPhysics.Sharp diverges from
// the C++ naming; ideally CryPhysics.Sharp should be renamed to use the C++
// names directly, but that is a much larger change deferred for now).

global using Vec3 = CryPhysics.Math.PhysVector3;
global using Vec2 = CryPhysics.Math.PhysVector2;
global using Vec2i = CryPhysics.Math.Vector2i;
global using Matrix33 = CryPhysics.Math.PhysMatrix33;
global using Quat = CryPhysics.Math.PhysQuaternion;
global using IPhysicalEntity = CryPhysics.Entities.IPhysicalEntity;
global using pe_status_pos = CryPhysics.Params.StatusPos;
global using pe_params_bbox = CryPhysics.Params.ParamsBBox;
global using pe_params_flags = CryPhysics.Params.ParamsFlags;
