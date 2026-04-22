#ifndef CRYPHYSICS_API_H
#define CRYPHYSICS_API_H

#include <stdint.h>
#include <stddef.h>

// Use a distinct symbol from LY's internal CRYPHYSICS_API (defined in LY's
// IPhysics.h / CryPhysics.h) — silences the redefinition warning in
// translation units (src/api.cpp) that pull both headers in.
#if defined(_WIN32)
    #define CRYPHYSICS_EXPORT __declspec(dllexport)
    #define CRYPHYSICS_CALL __cdecl
#else
    #define CRYPHYSICS_EXPORT __attribute__((visibility("default")))
    #define CRYPHYSICS_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct CryPhysicsWorld*  WorldH;
typedef struct CryPhysicsEntity* EntityH;
typedef struct CryPhysicsGeom*   GeomH;

typedef struct { float x, y, z; }       CpVec3;
typedef struct { float w, x, y, z; }    CpQuat;
typedef struct { float m[9]; }          CpMat33;

typedef struct {
    float   gravity_z;
    float   max_time_step;
    int32_t single_threaded;
} WorldCfg;

typedef struct {
    int32_t id;
    CpVec3  pos;
    CpQuat  rot;
    float   mass;
} EntityCfg;

typedef struct {
    CpVec3  pivot;
    CpQuat  rot;
    float   mass;
    int32_t surface_id;
} PartParams;

typedef struct {
    int32_t id;
    float   level_z;
    CpVec3  flow;
    float   density;
    float   resistance;
} WaterAreaDesc;

// Typed payload for entity_apply_impulse — mirrors the subset of pe_action_impulse
// the ship driver actually uses. Optional fields are gated by `has_*` flags so
// the ABI doesn't have to share LY's MARK_UNUSED sentinel (which depends on
// NaN bit-patterns and is brittle across P/Invoke).
typedef struct {
    CpVec3  impulse;         // linear impulse, world-space (N·s)
    CpVec3  ang_impulse;     // angular impulse, world-space (kg·m²/s); applied only if has_ang_impulse != 0
    CpVec3  point;            // application point, world-space; applied only if has_point != 0
    int32_t has_ang_impulse;
    int32_t has_point;
    int32_t i_apply_time;    // 0=immediate, 1=before next step, 2=after next step (LY default)
    int32_t part_id;         // receiver part id; -1 = whole entity (ipart/partid unused)
} CpImpulse;

typedef void (CRYPHYSICS_CALL *EventCB)(int32_t event_type, const void* payload, size_t len, void* user);

// Ship-relevant dynamics snapshot (mass, linear/angular velocity, submerged
// fraction). Typed so the ABI never has to replicate pe_status_dynamics byte-
// exact; the ship driver consumes it every tick.
typedef struct {
    float   mass;
    CpVec3  v;
    CpVec3  w;
    float   submerged_fraction;
    int32_t n_contacts;
} CpDynamics;

// Per-entity tunables mirroring pe_simulation_params. Use negative/zero to
// keep the LY default for a given field.
typedef struct {
    float   max_time_step;   // <=0 → leave as-is
    float   damping;         // <0  → leave as-is (0 is valid)
    float   max_rot_vel;     // <=0 → leave as-is
    float   mass;            // <=0 → leave as-is
} CpSimParams;

// Per-entity buoyancy scaling (ship-level overrides on top of the global
// water area). Use <=0 to leave any field at its default.
typedef struct {
    float water_density_scale;     // <=0 → leave as-is
    float water_resistance_scale;  // <=0 → leave as-is
    float water_damping;           // <0  → leave as-is
} CpBuoyancyParams;

enum CpResult {
    CP_OK                    = 0,
    CP_ERR_INVALID_HANDLE    = -1,
    CP_ERR_INVALID_ARG       = -2,
    CP_ERR_NATIVE_EXCEPTION  = -3,
    CP_ERR_NOT_IMPLEMENTED   = -4,
};

CRYPHYSICS_EXPORT uint32_t CRYPHYSICS_CALL cryphysics_abi_version(void);
CRYPHYSICS_EXPORT size_t   CRYPHYSICS_CALL cryphysics_abi_sizeof_vec3(void);
CRYPHYSICS_EXPORT size_t   CRYPHYSICS_CALL cryphysics_abi_sizeof_quat(void);

// Diagnostic accessor: returns the number of times the heightfield height
// callback has been invoked since DLL load, and the last (ix, iy, h) sampled.
// Nulls for any out param are tolerated. Used by the driver-side log to
// distinguish "broadphase never queries terrain" (count stays 0) from
// "narrowphase rejects contact" (count rises while nContacts stays 0).
CRYPHYSICS_EXPORT void     CRYPHYSICS_CALL cryphysics_debug_get_hf_stats(
    uint64_t* out_call_count,
    int32_t* out_last_ix, int32_t* out_last_iy, float* out_last_h,
    int32_t* out_min_ix, int32_t* out_min_iy,
    int32_t* out_max_ix, int32_t* out_max_iy);

// SEH crash post-mortem: reports how many access violations world_timestep
// has survived, plus the code + address of the most recent one. 0/0/0 means
// we never faulted.
CRYPHYSICS_EXPORT void     CRYPHYSICS_CALL cryphysics_debug_get_seh_stats(
    uint64_t* out_count, uint32_t* out_last_code, uintptr_t* out_last_addr);

CRYPHYSICS_EXPORT WorldH CRYPHYSICS_CALL world_create(const WorldCfg* cfg);
CRYPHYSICS_EXPORT void   CRYPHYSICS_CALL world_destroy(WorldH world);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_timestep(WorldH world, float dt, int32_t flags);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_setup_entity_grid(WorldH world, CpVec3 origin, int32_t nx, int32_t ny, float step_x, float step_y);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_register_surface_type(WorldH world, int32_t id, float friction, float bounciness);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_set_event_sink(WorldH world, EventCB cb, void* user);

CRYPHYSICS_EXPORT EntityH CRYPHYSICS_CALL entity_create_rigid(WorldH world, const EntityCfg* cfg);
CRYPHYSICS_EXPORT void    CRYPHYSICS_CALL entity_destroy(EntityH entity);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_params(EntityH entity, int32_t params_type, const void* blob, size_t len);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_do_action(EntityH entity, int32_t action_type, const void* blob, size_t len);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_status(EntityH entity, int32_t status_type, void* out, size_t len);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_add_geometry(EntityH entity, GeomH geom, const PartParams* part);

// Typed actions. `imp` / `pos` / `rot` / `v` / `w` are never NULL except where
// the doc says otherwise; pass identity or zero vectors when you want that
// component left untouched (see has_* flags on CpImpulse for the impulse case).
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_apply_impulse(EntityH entity, const CpImpulse* imp);
// Teleport / reorient. Pass non-null pos and/or rot to update them; the other
// stays as-is. Internally routed through pe_params_pos, so bbox + inertia
// tensor get re-derived (RecalcBounds=1).
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_pose(EntityH entity, const CpVec3* pos, const CpQuat* rot);
// Hard velocity override. Pass non-null lin_vel / ang_vel to set each; caller
// is responsible for matching the pair the sim expects (e.g. upright spring
// should not blow away angular survivors — prefer entity_apply_impulse for
// gentle corrections).
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_velocity(EntityH entity, const CpVec3* lin_vel, const CpVec3* ang_vel);

// Typed accessors for the ship-critical status queries — equivalent to
// entity_get_status(ePE_status_pos/ePE_status_dynamics) but without exposing
// the LY struct shapes across the ABI. Both return CP_OK / CP_ERR_*.
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_pos(EntityH entity, CpVec3* out_pos);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_quat(EntityH entity, CpQuat* out_quat);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_velocity(EntityH entity, CpVec3* out_linvel, CpVec3* out_angvel);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_dynamics(EntityH entity, CpDynamics* out_dyn);

// Typed param setters for ship init / runtime tweaks. Each reads only the
// fields whose sentinel (<= 0 or < 0) is NOT set, so callers can update a
// single property without clobbering the rest.
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_simulation_params(EntityH entity, const CpSimParams* params);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_buoyancy_params(EntityH entity, const CpBuoyancyParams* params);
// flags_or is applied first (pe_params_flags::flagsOR), then flags_and is
// applied as a mask (flagsAND). Use flags_or=0 / flags_and=~0 to leave flags
// unchanged.
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_flags(EntityH entity, uint32_t flags_or, uint32_t flags_and);

// Geometry registration is per-world: LY's CPhysicalWorld is itself the
// IGeomManager, so every phys_geometry is tied to the world it was registered
// against. Geoms created against world A must not be attached to entities in
// world B.
CRYPHYSICS_EXPORT GeomH CRYPHYSICS_CALL geom_create_box(WorldH world, CpVec3 size, CpVec3 pivot);
CRYPHYSICS_EXPORT GeomH CRYPHYSICS_CALL geom_create_trimesh(WorldH world, const float* verts, const int32_t* indices, int32_t tri_count, const int32_t* surface_ids);
// Height query callback — invoked by LY narrowphase each time it needs a
// cell height during a heightfield intersect. The DLL owns NO height data:
// the managed side services every query live. This matches AAEmu's cell-
// streamed heightmap (cells load/unload on demand), eliminates the 1.4 GB
// float buffer the old bake would have allocated per world, and keeps the
// single source of truth in C#. Called on the same thread that invoked
// world_timestep (we force numThreads=1 in world_create). `user` is whatever
// opaque value was passed to geom_create_heightfield and is echoed back
// unchanged — use it to identify which world's terrain is being queried
// when the managed side shares one delegate across worlds.
typedef float (CRYPHYSICS_CALL *HeightCallback)(int32_t ix, int32_t iy, void* user);

// Registers the world's singleton heightfield. `w`/`h` are grid extents
// (inclusive range that LY will clamp queries to), `step` is world units per
// cell (metres). `cb` is invoked for each cell LY needs during intersect;
// must be non-null and outlive the geometry. Destroying the returned handle
// (geom_destroy) calls SetHeightfieldData(nullptr) and releases the slot.
CRYPHYSICS_EXPORT GeomH CRYPHYSICS_CALL geom_create_heightfield(WorldH world, HeightCallback cb, void* user, int32_t w, int32_t h, float step);
CRYPHYSICS_EXPORT void  CRYPHYSICS_CALL geom_destroy(WorldH world, GeomH geom);

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL water_area_set(WorldH world, const WaterAreaDesc* desc);
CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL water_area_clear(WorldH world, int32_t id);

#ifdef __cplusplus
}
#endif

#endif
