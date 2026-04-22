// C ABI facade over Lumberyard CryPhysics. Keeps the engine's internal types
// (IPhysicalWorld, IPhysicalEntity, phys_geometry, pe_params*) behind opaque
// handles so the managed side (CryPhysicsNative.csproj) never has to marshal
// engine structs directly — only the POD typedefs in cryphysics_api.h.
//
// Every entry point is `extern "C"` + try/catch so an LY assert/throw can't
// escape into the .NET runtime (which would crash the process uncleanly).

#include "cryphysics_api.h"

#include <atomic>
#include <cstdio>
#include <cstring>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

// LY headers. StdAfx wires the CRT/platform macros every other LY cpp expects.
// Order mirrors CryPhysics.cpp — physicalworld.h has inline methods that
// deref CPhysicalEntity members (m_BBox, m_flags), so the entity headers
// must come first.
#include "StdAfx.h"
#include "IPhysics.h"
#include "geoman.h"
#include "bvtree.h"
#include "geometry.h"
#include "rigidbody.h"
#include "physicalplaceholder.h"
#include "physicalentity.h"
#include "physicalworld.h"

// Forward declaration from CryPhysics.cpp — LY's published factory.
extern "C" CRYPHYSICS_EXPORT IPhysicalWorld* CreatePhysicalWorld(ISystem* pSystem);

// Bumped 2026-04-20: geom_create_heightfield signature changed from
// (world, heights*, w, h, step) to (world, cb, user, w, h, step). Old DLL
// with new managed side will crash/UB since the first pointer arg goes
// from "float*" to "HeightCallback*".
#define CRYPHYSICS_ABI_VERSION 0x00010002u

#define CP_GUARD_BEGIN try {
#define CP_GUARD_END(ret_on_fail) } catch (...) { return (ret_on_fail); }
#define CP_GUARD_END_VOID } catch (...) { return; }

namespace {
    // Last SEH crash info captured by the VEH. Read by world_timestep's
    // __except filter so the managed log line can point at the native
    // instruction that faulted.
    std::atomic<uint32_t> g_lastSehCode{0};
    std::atomic<uintptr_t> g_lastSehAddr{0};
    std::atomic<uint64_t> g_sehCount{0};
}

#ifdef _WIN32
// __try/__except can't live in a function that also needs C++ destructors,
// so the timestep body is split off. The wrapper does nothing but SEH.
static int32_t world_timestep_impl(IPhysicalWorld* world, float dt, int32_t flags) {
    if (flags == 0) {
        world->TimeStep(dt);
    } else {
        world->TimeStep(dt, flags);
    }
    return CP_OK;
}

static int sehFilter(EXCEPTION_POINTERS* info) {
    const EXCEPTION_RECORD* rec = info->ExceptionRecord;
    g_lastSehCode.store(rec->ExceptionCode, std::memory_order_relaxed);
    g_lastSehAddr.store(reinterpret_cast<uintptr_t>(rec->ExceptionAddress),
                        std::memory_order_relaxed);
    g_sehCount.fetch_add(1, std::memory_order_relaxed);
    // Only swallow the access violations we're trying to survive; let other
    // fatal SEH codes (stack overflow, illegal instruction) keep killing the
    // process so we find them instead of silently limping.
    if (rec->ExceptionCode == EXCEPTION_ACCESS_VIOLATION ||
        rec->ExceptionCode == EXCEPTION_INT_DIVIDE_BY_ZERO ||
        rec->ExceptionCode == EXCEPTION_FLT_DIVIDE_BY_ZERO) {
        fprintf(stderr,
                "[CryPhysicsNative] SEH 0x%08lX at %p inside world_timestep "
                "(count=%llu)\n",
                static_cast<unsigned long>(rec->ExceptionCode),
                rec->ExceptionAddress,
                static_cast<unsigned long long>(g_sehCount.load()));
        fflush(stderr);
        return EXCEPTION_EXECUTE_HANDLER;
    }
    return EXCEPTION_CONTINUE_SEARCH;
}
#endif

namespace {
    inline IPhysicalWorld*  ToWorld(WorldH h)   { return reinterpret_cast<IPhysicalWorld*>(h); }
    inline IPhysicalEntity* ToEntity(EntityH h) { return reinterpret_cast<IPhysicalEntity*>(h); }
    inline phys_geometry*   ToGeom(GeomH h)     { return reinterpret_cast<phys_geometry*>(h); }

    inline Vec3 V(const CpVec3& v)       { return Vec3(v.x, v.y, v.z); }
    inline quaternionf Q(const CpQuat& q) { return quaternionf(q.w, q.x, q.y, q.z); }

    // CryPhysics reads gEnv->bMultiplayer from TimeStep (physicalworld.cpp:3684)
    // and a few other flags elsewhere. platform_impl.h leaves gEnv = NULL when
    // CreatePhysicalWorld is called with a null ISystem*, so we provide a
    // zero-initialised stub. All pointer members stay null — any LY codepath
    // that derefs gEnv->pSomething must be disabled via a compile flag
    // (e.g. USE_IMPROVED_RIGID_ENTITY_SYNCHRONISATION=0 already handled).
    SSystemGlobalEnvironment s_stubEnv{};
    void EnsureStubEnv() {
        if (!gEnv) {
            gEnv = &s_stubEnv;
        }
    }

    // Heightfield slots. LY's primitives::heightfield uses bare
    // `float (*)(int,int)` callbacks with no user_data, so we can't route
    // multiple worlds through a single shared callback — we hand out one of
    // MAX_HF_SLOTS fixed callbacks (HfGetHeight0..N) at register time. Each
    // slot stores a managed-side delegate (cb + user) that services the
    // actual height query live. The DLL owns NO height buffer: the managed
    // side's cell-streamed heightmap is the single source of truth.
    constexpr int MAX_HF_SLOTS = 16;
    struct HfSlot {
        HeightCallback cb = nullptr;
        void*          user = nullptr;
        int32_t        w = 0;
        int32_t        h = 0;
        std::atomic<uint64_t> callCount{0};
        std::atomic<int32_t>  lastIx{0};
        std::atomic<int32_t>  lastIy{0};
        std::atomic<float>    lastH{0.0f};
        std::atomic<int32_t>  minIx{INT32_MAX};
        std::atomic<int32_t>  minIy{INT32_MAX};
        std::atomic<int32_t>  maxIx{INT32_MIN};
        std::atomic<int32_t>  maxIy{INT32_MIN};
    };
    HfSlot s_hfSlots[MAX_HF_SLOTS];

    inline void AtomicMin(std::atomic<int32_t>& slot, int32_t v) {
        int32_t cur = slot.load(std::memory_order_relaxed);
        while (v < cur && !slot.compare_exchange_weak(cur, v, std::memory_order_relaxed)) {}
    }
    inline void AtomicMax(std::atomic<int32_t>& slot, int32_t v) {
        int32_t cur = slot.load(std::memory_order_relaxed);
        while (v > cur && !slot.compare_exchange_weak(cur, v, std::memory_order_relaxed)) {}
    }

    inline float HfGetHeightFromSlot(int slotIdx, int ix, int iy) {
        HfSlot& s = s_hfSlots[slotIdx];
        if (!s.cb || s.w <= 0 || s.h <= 0) {
            return 0.0f;
        }
        // Delegate to the managed side. LY can (and does) overshoot the grid
        // during broadphase sweep — forward the raw index and let the managed
        // side decide on the OOB fallback (typically oceanFallback).
        const float h = s.cb(ix, iy, s.user);
        s.callCount.fetch_add(1, std::memory_order_relaxed);
        s.lastIx.store(ix, std::memory_order_relaxed);
        s.lastIy.store(iy, std::memory_order_relaxed);
        s.lastH.store(h, std::memory_order_relaxed);
        AtomicMin(s.minIx, ix);
        AtomicMin(s.minIy, iy);
        AtomicMax(s.maxIx, ix);
        AtomicMax(s.maxIy, iy);
        return h;
    }

    // One dedicated callback per slot — LY's fpGetHeightCallback has no
    // user_data, so we can't share a single function and disambiguate at
    // call time. Macro keeps the 16 definitions in sync.
#define HF_CB(N) float HfGetHeight##N(int ix, int iy) { return HfGetHeightFromSlot(N, ix, iy); }
    HF_CB(0)  HF_CB(1)  HF_CB(2)  HF_CB(3)
    HF_CB(4)  HF_CB(5)  HF_CB(6)  HF_CB(7)
    HF_CB(8)  HF_CB(9)  HF_CB(10) HF_CB(11)
    HF_CB(12) HF_CB(13) HF_CB(14) HF_CB(15)
#undef HF_CB

    typedef float (*HfCallback)(int, int);
    const HfCallback s_hfCallbacks[MAX_HF_SLOTS] = {
        &HfGetHeight0,  &HfGetHeight1,  &HfGetHeight2,  &HfGetHeight3,
        &HfGetHeight4,  &HfGetHeight5,  &HfGetHeight6,  &HfGetHeight7,
        &HfGetHeight8,  &HfGetHeight9,  &HfGetHeight10, &HfGetHeight11,
        &HfGetHeight12, &HfGetHeight13, &HfGetHeight14, &HfGetHeight15,
    };

    unsigned char HfGetSurfType(int /*ix*/, int /*iy*/) {
        return 0;
    }

    // Handles that point to IPhysicalEntity* (heightfield singleton, water
    // areas) rather than phys_geometry*. geom_destroy uses this to route
    // destruction to the right unwinding path instead of UnregisterGeometry
    // (which would corrupt memory if passed an IPhysicalEntity*).
    // Value is the slot index (as void* cast) so geom_destroy can free the
    // right grid; +1 offset so slot 0 isn't NULL.
    std::unordered_map<void*, int32_t> s_hfHandleToSlot;
}

extern "C" {

CRYPHYSICS_EXPORT uint32_t CRYPHYSICS_CALL cryphysics_abi_version(void) {
    return CRYPHYSICS_ABI_VERSION;
}

CRYPHYSICS_EXPORT size_t CRYPHYSICS_CALL cryphysics_abi_sizeof_vec3(void) {
    return sizeof(CpVec3);
}

CRYPHYSICS_EXPORT size_t CRYPHYSICS_CALL cryphysics_abi_sizeof_quat(void) {
    return sizeof(CpQuat);
}

CRYPHYSICS_EXPORT void CRYPHYSICS_CALL cryphysics_debug_get_hf_stats(
    uint64_t* out_call_count,
    int32_t* out_last_ix, int32_t* out_last_iy, float* out_last_h,
    int32_t* out_min_ix, int32_t* out_min_iy,
    int32_t* out_max_ix, int32_t* out_max_iy) {
    // Aggregate across all active slots. "last*" reports the most recently
    // touched slot (the one with the highest callCount), which is typically
    // the slot stepping right now. Min/Max are world-maxed across all slots.
    uint64_t totalCalls = 0;
    uint64_t maxCalls = 0;
    int32_t  bestSlot = -1;
    int32_t  minIx = INT32_MAX, minIy = INT32_MAX;
    int32_t  maxIx = INT32_MIN, maxIy = INT32_MIN;
    for (int i = 0; i < MAX_HF_SLOTS; ++i) {
        HfSlot& s = s_hfSlots[i];
        if (s.cb == nullptr) continue;
        const uint64_t c = s.callCount.load(std::memory_order_relaxed);
        totalCalls += c;
        if (c >= maxCalls) { maxCalls = c; bestSlot = i; }
        const int32_t sMinIx = s.minIx.load(std::memory_order_relaxed);
        const int32_t sMinIy = s.minIy.load(std::memory_order_relaxed);
        const int32_t sMaxIx = s.maxIx.load(std::memory_order_relaxed);
        const int32_t sMaxIy = s.maxIy.load(std::memory_order_relaxed);
        if (sMinIx < minIx) minIx = sMinIx;
        if (sMinIy < minIy) minIy = sMinIy;
        if (sMaxIx > maxIx) maxIx = sMaxIx;
        if (sMaxIy > maxIy) maxIy = sMaxIy;
    }
    if (out_call_count) { *out_call_count = totalCalls; }
    if (bestSlot >= 0) {
        HfSlot& s = s_hfSlots[bestSlot];
        if (out_last_ix)  { *out_last_ix = s.lastIx.load(std::memory_order_relaxed); }
        if (out_last_iy)  { *out_last_iy = s.lastIy.load(std::memory_order_relaxed); }
        if (out_last_h)   { *out_last_h  = s.lastH.load(std::memory_order_relaxed); }
    } else {
        if (out_last_ix)  { *out_last_ix = 0; }
        if (out_last_iy)  { *out_last_iy = 0; }
        if (out_last_h)   { *out_last_h  = 0.0f; }
    }
    if (out_min_ix)     { *out_min_ix = minIx; }
    if (out_min_iy)     { *out_min_iy = minIy; }
    if (out_max_ix)     { *out_max_ix = maxIx; }
    if (out_max_iy)     { *out_max_iy = maxIy; }
}

CRYPHYSICS_EXPORT WorldH CRYPHYSICS_CALL world_create(const WorldCfg* cfg) {
    CP_GUARD_BEGIN
    // ModuleInitISystem (called from CreatePhysicalWorld) early-outs on null
    // pSystem and leaves gEnv null. Install the stub env first so TimeStep's
    // `gEnv->bMultiplayer` read finds a valid (zero) struct.
    EnsureStubEnv();
    IPhysicalWorld* world = CreatePhysicalWorld(nullptr);
    if (!world) {
        return nullptr;
    }
    // Force single-thread mode. LY's default numThreads=2 makes TimeStep
    // spawn worker tasks via GetISystem()->GetIThreadTaskManager(), and
    // GetISystem() returns null on this headless build — deref = crash.
    // With numThreads == FIRST_WORKER_THREAD (== 1) the worker-spawn block
    // is skipped entirely and the step runs on the calling thread.
    if (PhysicsVars* vars = world->GetPhysVars()) {
        vars->numThreads = 1;
        if (cfg) {
            vars->gravity.Set(0, 0, cfg->gravity_z);
            if (cfg->max_time_step > 0.0f) {
                vars->maxWorldStep = cfg->max_time_step;
            }
        }
    }
    return reinterpret_cast<WorldH>(world);
    CP_GUARD_END(nullptr)
}

CRYPHYSICS_EXPORT void CRYPHYSICS_CALL world_destroy(WorldH h) {
    CP_GUARD_BEGIN
    if (IPhysicalWorld* world = ToWorld(h)) {
        world->Release();
    }
    CP_GUARD_END_VOID
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_timestep(WorldH h, float dt, int32_t flags) {
    IPhysicalWorld* world = ToWorld(h);
    if (!world) {
        return CP_ERR_INVALID_HANDLE;
    }
#ifdef _WIN32
    // LY code occasionally dereferences stale contact/entity pointers during
    // narrowphase when the broadphase state is transiently inconsistent.
    // Convert the access violation to an error code so the .NET runtime
    // doesn't tear down the process; the managed layer logs and keeps
    // ticking. sehFilter records the fault address for post-mortem.
    __try {
        return world_timestep_impl(world, dt, flags);
    } __except (sehFilter(GetExceptionInformation())) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
#else
    CP_GUARD_BEGIN
    if (flags == 0) {
        world->TimeStep(dt);
    } else {
        world->TimeStep(dt, flags);
    }
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
#endif
}

CRYPHYSICS_EXPORT void CRYPHYSICS_CALL cryphysics_debug_get_seh_stats(
    uint64_t* out_count, uint32_t* out_last_code, uintptr_t* out_last_addr) {
    if (out_count)     { *out_count = g_sehCount.load(std::memory_order_relaxed); }
    if (out_last_code) { *out_last_code = g_lastSehCode.load(std::memory_order_relaxed); }
    if (out_last_addr) { *out_last_addr = g_lastSehAddr.load(std::memory_order_relaxed); }
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_setup_entity_grid(WorldH h, CpVec3 origin, int32_t nx, int32_t ny, float step_x, float step_y) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world) {
        return CP_ERR_INVALID_HANDLE;
    }
    // axisz=2 = world Z-up; log2PODscale=0, bCyclic=0 match AAEmu's call from
    // PhysicsManager.Initialize. A valid entity grid is a precondition for
    // TimeStep touching any moving entity.
    world->SetupEntityGrid(2, V(origin), nx, ny, step_x, step_y, 0, 0);
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_register_surface_type(WorldH h, int32_t id, float friction, float bounciness) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world) {
        return CP_ERR_INVALID_HANDLE;
    }
    // LY's SetSurfaceParameters(id, bounciness, friction) — note the argument
    // order difference from our ABI (friction, bounciness). Matching the
    // published SetSurfaceParameters signature.
    world->SetSurfaceParameters(id, bounciness, friction);
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL world_set_event_sink(WorldH, EventCB, void*) {
    CP_GUARD_BEGIN
    return CP_ERR_NOT_IMPLEMENTED;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT EntityH CRYPHYSICS_CALL entity_create_rigid(WorldH h, const EntityCfg* cfg) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world || !cfg) {
        return nullptr;
    }
    // pe_params_pos seeds initial pose; mass is a per-part property in LY,
    // so cfg.mass is picked up by entity_add_geometry via PartParams.
    pe_params_pos pp;
    pp.pos = V(cfg->pos);
    pp.q   = Q(cfg->rot);
    IPhysicalEntity* ent = world->CreatePhysicalEntity(PE_RIGID, &pp, nullptr, 0, cfg->id);
    return reinterpret_cast<EntityH>(ent);
    CP_GUARD_END(nullptr)
}

CRYPHYSICS_EXPORT void CRYPHYSICS_CALL entity_destroy(EntityH h) {
    CP_GUARD_BEGIN
    if (IPhysicalEntity* ent = ToEntity(h)) {
        if (IPhysicalWorld* world = ent->GetWorld()) {
            world->DestroyPhysicalEntity(ent);
        }
    }
    CP_GUARD_END_VOID
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_params(EntityH, int32_t, const void*, size_t) {
    CP_GUARD_BEGIN
    return CP_ERR_NOT_IMPLEMENTED;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_do_action(EntityH, int32_t, const void*, size_t) {
    CP_GUARD_BEGIN
    return CP_ERR_NOT_IMPLEMENTED;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_status(EntityH, int32_t, void*, size_t) {
    CP_GUARD_BEGIN
    return CP_ERR_NOT_IMPLEMENTED;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_pos(EntityH h, CpVec3* out) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || !out) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_status_pos sp;
    if (!ent->GetStatus(&sp)) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    out->x = sp.pos.x; out->y = sp.pos.y; out->z = sp.pos.z;
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_quat(EntityH h, CpQuat* out) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || !out) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_status_pos sp;
    if (!ent->GetStatus(&sp)) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    out->w = sp.q.w; out->x = sp.q.v.x; out->y = sp.q.v.y; out->z = sp.q.v.z;
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_velocity(EntityH h, CpVec3* out_linvel, CpVec3* out_angvel) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_status_dynamics sd;
    if (!ent->GetStatus(&sd)) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    if (out_linvel) { out_linvel->x = sd.v.x; out_linvel->y = sd.v.y; out_linvel->z = sd.v.z; }
    if (out_angvel) { out_angvel->x = sd.w.x; out_angvel->y = sd.w.y; out_angvel->z = sd.w.z; }
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_get_dynamics(EntityH h, CpDynamics* out) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || !out) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_status_dynamics sd;
    if (!ent->GetStatus(&sd)) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    out->mass = sd.mass;
    out->v.x = sd.v.x; out->v.y = sd.v.y; out->v.z = sd.v.z;
    out->w.x = sd.w.x; out->w.y = sd.w.y; out->w.z = sd.w.z;
    out->submerged_fraction = sd.submergedFraction;
    out->n_contacts = sd.nContacts;
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_simulation_params(EntityH h, const CpSimParams* params) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || !params) {
        return CP_ERR_INVALID_HANDLE;
    }
    // pe_simulation_params ctor MARK_UNUSED every field; we only write the
    // ones the caller explicitly asked for (positive sentinel).
    pe_simulation_params sp;
    if (params->max_time_step > 0.0f) { sp.maxTimeStep = params->max_time_step; }
    if (params->damping       >= 0.0f) { sp.damping      = params->damping; }
    if (params->max_rot_vel   > 0.0f) { sp.maxRotVel    = params->max_rot_vel; }
    if (params->mass          > 0.0f) { sp.mass         = params->mass; }
    return ent->SetParams(&sp) > 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_buoyancy_params(EntityH h, const CpBuoyancyParams* params) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || !params) {
        return CP_ERR_INVALID_HANDLE;
    }
    // Entity-level overrides live on the same struct used for areas, but LY
    // stores the scale fields (kwaterDensity / kwaterResistance) when applied
    // to an entity — waterDensity itself is area-only (physinterface.h:771).
    pe_params_buoyancy pb;
    if (params->water_density_scale    > 0.0f) { pb.kwaterDensity    = params->water_density_scale; }
    if (params->water_resistance_scale > 0.0f) { pb.kwaterResistance = params->water_resistance_scale; }
    if (params->water_damping          >= 0.0f) { pb.waterDamping     = params->water_damping; }
    return ent->SetParams(&pb) > 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_flags(EntityH h, uint32_t flags_or, uint32_t flags_and) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent) {
        return CP_ERR_INVALID_HANDLE;
    }
    // pe_params_flags::flagsOR and flagsAND are the ship-side toggles (no full
    // overwrite of `flags` — leaving it MARK_UNUSED means LY keeps the current
    // value and applies `flagsNew = flagsOld & flagsAND | flagsOR`).
    pe_params_flags pf;
    pf.flagsOR  = flags_or;
    pf.flagsAND = flags_and;
    return ent->SetParams(&pf) > 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_apply_impulse(EntityH h, const CpImpulse* imp) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || !imp) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_action_impulse act; // ctor sets type, zeros impulse, MARK_UNUSED the rest.
    act.impulse = V(imp->impulse);
    if (imp->has_ang_impulse) {
        act.angImpulse = V(imp->ang_impulse);
    }
    if (imp->has_point) {
        act.point = V(imp->point);
    }
    act.iApplyTime = imp->i_apply_time;
    if (imp->part_id >= 0) {
        act.partid = imp->part_id;
    }
    // Action returns >0 on success (see CPhysicalEntity::Action — returns 1/0).
    return ent->Action(&act) > 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_pose(EntityH h, const CpVec3* pos, const CpQuat* rot) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || (!pos && !rot)) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_params_pos pp; // ctor MARK_UNUSED pos / q — only the ones we set get applied.
    if (pos) {
        pp.pos = V(*pos);
    }
    if (rot) {
        pp.q = Q(*rot);
    }
    return ent->SetParams(&pp) > 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_set_velocity(EntityH h, const CpVec3* lin_vel, const CpVec3* ang_vel) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(h);
    if (!ent || (!lin_vel && !ang_vel)) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_action_set_velocity act;
    if (lin_vel) { act.v = V(*lin_vel); }
    if (ang_vel) { act.w = V(*ang_vel); }
    return ent->Action(&act) > 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL entity_add_geometry(EntityH eh, GeomH gh, const PartParams* part) {
    CP_GUARD_BEGIN
    IPhysicalEntity* ent = ToEntity(eh);
    phys_geometry*   pg  = ToGeom(gh);
    if (!ent || !pg || !part) {
        return CP_ERR_INVALID_HANDLE;
    }
    pe_geomparams gp;
    gp.pos  = V(part->pivot);
    gp.q    = Q(part->rot);
    gp.mass = part->mass;
    if (part->surface_id >= 0) {
        gp.surface_idx = part->surface_id;
    }
    int partId = ent->AddGeometry(pg, &gp);
    return partId >= 0 ? CP_OK : CP_ERR_NATIVE_EXCEPTION;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT GeomH CRYPHYSICS_CALL geom_create_box(WorldH h, CpVec3 size, CpVec3 pivot) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world) {
        return nullptr;
    }
    // primitives::box uses half-extents in `size`; `center` is the local pivot.
    // bOriented=0 + Basis=Identity means an axis-aligned box in the part's
    // local frame — any part-level rotation lives on pe_geomparams::q.
    primitives::box b;
    b.Basis.SetIdentity();
    b.bOriented = 0;
    b.center = V(pivot);
    b.size   = V(size);
    IGeomManager* gm = world->GetGeomManager();
    IGeometry* g = gm->CreatePrimitive(primitives::box::type, &b);
    if (!g) {
        return nullptr;
    }
    phys_geometry* pg = gm->RegisterGeometry(g);
    // RegisterGeometry bumps the geom's refcount to match; drop our local ref
    // so only the phys_geometry holds ownership.
    g->Release();
    return reinterpret_cast<GeomH>(pg);
    CP_GUARD_END(nullptr)
}

CRYPHYSICS_EXPORT GeomH CRYPHYSICS_CALL geom_create_trimesh(WorldH h, const float* verts, const int32_t* indices, int32_t tri_count, const int32_t* /*surface_ids*/) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world || !verts || !indices || tri_count <= 0) {
        return nullptr;
    }
    // LY's CreateMesh takes ushort indices. Convert int32 → ushort; the mesh
    // can't address more than 65535 vertices anyway — up to the caller to
    // tile big meshes.
    const int nIdx = tri_count * 3;
    std::vector<unsigned short> idx16((size_t)nIdx);
    for (int i = 0; i < nIdx; ++i) {
        idx16[(size_t)i] = (unsigned short)indices[i];
    }
    // Vec3 is float[3] packed — stride == sizeof(Vec3), which is the default.
    strided_pointer<const Vec3>    pVerts(reinterpret_cast<const Vec3*>(verts));
    strided_pointer<unsigned short> pIdx(idx16.data());
    // mesh_SingleBB: one OBB tree for the whole mesh (sufficient for our
    // static shapes). No mesh_shared_* flags → LY copies both buffers, so
    // the local idx16 / caller's verts can be freed after return.
    IGeometry* g = world->GetGeomManager()->CreateMesh(
        pVerts, pIdx, /*pIds=*/nullptr, /*pForeignIdx=*/nullptr, tri_count,
        mesh_SingleBB, /*approx_tolerance=*/0.0f);
    if (!g) {
        return nullptr;
    }
    phys_geometry* pg = world->GetGeomManager()->RegisterGeometry(g);
    g->Release();
    return reinterpret_cast<GeomH>(pg);
    CP_GUARD_END(nullptr)
}

CRYPHYSICS_EXPORT GeomH CRYPHYSICS_CALL geom_create_heightfield(WorldH h, HeightCallback cb, void* user, int32_t w, int32_t hgt, float step) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world || !cb || w <= 0 || hgt <= 0 || step <= 0.0f) {
        return nullptr;
    }
    // Find a free slot. Each world owns one — process-globals got trampled
    // when multiple PhysicsManagers registered terrain (main_world then
    // arche_mall_world); dedicated slots keep them isolated.
    int slotIdx = -1;
    for (int i = 0; i < MAX_HF_SLOTS; ++i) {
        if (s_hfSlots[i].cb == nullptr) { slotIdx = i; break; }
    }
    if (slotIdx < 0) {
        return nullptr; // all slots in use — bump MAX_HF_SLOTS if we ever hit this
    }
    HfSlot& slot = s_hfSlots[slotIdx];
    slot.cb = cb;
    slot.user = user;
    slot.w = w;
    slot.h = hgt;
    slot.callCount.store(0, std::memory_order_relaxed);
    slot.lastIx.store(0, std::memory_order_relaxed);
    slot.lastIy.store(0, std::memory_order_relaxed);
    slot.lastH.store(0.0f, std::memory_order_relaxed);
    slot.minIx.store(INT32_MAX, std::memory_order_relaxed);
    slot.minIy.store(INT32_MAX, std::memory_order_relaxed);
    slot.maxIx.store(INT32_MIN, std::memory_order_relaxed);
    slot.maxIy.store(INT32_MIN, std::memory_order_relaxed);

    primitives::heightfield phf;
    phf.Basis.SetIdentity();
    phf.bOriented = 0;
    phf.origin.Set(0.0f, 0.0f, 0.0f);
    phf.step.x = step;
    phf.step.y = step;
    phf.stepr.x = 1.0f / step;
    phf.stepr.y = 1.0f / step;
    phf.size.set(w, hgt);
    phf.stride.set(1, w);
    phf.bCyclic = 0;
    phf.heightscale = 1.0f;
    // typemask MUST be non-zero: heightfieldgeom.cpp:38 derives typepower via
    // `(typemask ^ typemask-1) + 1 >> 1` then loops until the lowest bit — with
    // typemask==0 that reduces to `i=0`, and the shift loop never terminates.
    // 0xFF covers a full 8-bit surface id (typepower becomes 0).
    phf.typemask = 0xFF;
    phf.typehole = 127;
    phf.typepower = 0;
    phf.fpGetHeightCallback = s_hfCallbacks[slotIdx];
    phf.fpGetSurfTypeCallback = &HfGetSurfType;

    IPhysicalEntity* pHF = world->SetHeightfieldData(&phf);
    if (!pHF) {
        // Roll back slot on failure so it's reusable.
        slot.cb = nullptr;
        slot.user = nullptr;
        slot.w = slot.h = 0;
        return nullptr;
    }
    // Stash the handle + slot so geom_destroy knows (a) to route to
    // SetHeightfieldData(0) rather than UnregisterGeometry, and (b) which
    // grid to free.
    s_hfHandleToSlot[reinterpret_cast<void*>(pHF)] = slotIdx;
    return reinterpret_cast<GeomH>(pHF);
    CP_GUARD_END(nullptr)
}

CRYPHYSICS_EXPORT void CRYPHYSICS_CALL geom_destroy(WorldH h, GeomH gh) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world || !gh) {
        return;
    }
    // Heightfield handle → tear down the terrain entity + its owned slot grid.
    void* raw = reinterpret_cast<void*>(gh);
    auto it = s_hfHandleToSlot.find(raw);
    if (it != s_hfHandleToSlot.end()) {
        const int32_t slotIdx = it->second;
        s_hfHandleToSlot.erase(it);
        world->SetHeightfieldData(nullptr);
        if (slotIdx >= 0 && slotIdx < MAX_HF_SLOTS) {
            HfSlot& slot = s_hfSlots[slotIdx];
            slot.cb = nullptr;
            slot.user = nullptr;
            slot.w = slot.h = 0;
        }
        return;
    }
    world->GetGeomManager()->UnregisterGeometry(ToGeom(gh));
    CP_GUARD_END_VOID
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL water_area_set(WorldH h, const WaterAreaDesc* desc) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world || !desc) {
        return CP_ERR_INVALID_HANDLE;
    }
    // AddGlobalArea is idempotent — returns the same CPhysArea on subsequent
    // calls. WaterAreaDesc.id is ignored for now: the ship server needs one
    // world water plane (with optional flow), not multiple named areas.
    IPhysicalEntity* pArea = world->AddGlobalArea();
    if (!pArea) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    pe_params_buoyancy pb;
    pb.iMedium = 0; // water
    pb.waterPlane.n.Set(0.0f, 0.0f, 1.0f);
    pb.waterPlane.origin.Set(0.0f, 0.0f, desc->level_z);
    pb.waterDensity    = desc->density > 0.0f ? desc->density : 1000.0f;
    pb.waterResistance = desc->resistance > 0.0f ? desc->resistance : 1000.0f;
    pb.waterDamping    = 0.0f;
    pb.waterFlow       = V(desc->flow);
    if (pArea->SetParams(&pb) <= 0) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

CRYPHYSICS_EXPORT int32_t CRYPHYSICS_CALL water_area_clear(WorldH h, int32_t /*id*/) {
    CP_GUARD_BEGIN
    IPhysicalWorld* world = ToWorld(h);
    if (!world) {
        return CP_ERR_INVALID_HANDLE;
    }
    // Disable buoyancy on the global area rather than destroying it — LY has
    // no public "remove global area" path, and other code may still point at
    // it. density=0 + resistance=0 ⇒ no force.
    IPhysicalEntity* pArea = world->AddGlobalArea();
    if (!pArea) {
        return CP_ERR_NATIVE_EXCEPTION;
    }
    pe_params_buoyancy pb;
    pb.iMedium = 0;
    pb.waterPlane.n.Set(0.0f, 0.0f, 1.0f);
    pb.waterPlane.origin.Set(0.0f, 0.0f, -1e10f); // below any reachable entity
    pb.waterDensity    = 0.0f;
    pb.waterResistance = 0.0f;
    pb.waterDamping    = 0.0f;
    pb.waterFlow.Set(0.0f, 0.0f, 0.0f);
    (void)pArea->SetParams(&pb);
    return CP_OK;
    CP_GUARD_END(CP_ERR_NATIVE_EXCEPTION)
}

}
