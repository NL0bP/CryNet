// Port of CryPhysics physicalworld.h/cpp - main physics world
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Entities;
using CryPhysics.Events;
using CryPhysics.Geometry;
using CryPhysics.Math;
using CryPhysics.Params;
using CryPhysics.Threading;

namespace CryPhysics.World;

/// <summary>
/// Number of surface types. Matches C++ NSURFACETYPES = 512.
/// </summary>
public static class SurfaceConstants
{
    public const int NSurfaceTypes = 512;
}

/// <summary>
/// Surface material parameters.
/// Port of per-surface tables from CPhysicalWorld (m_BouncinessTable, m_FrictionTable, etc.).
/// </summary>
public struct SurfaceParameters
{
    public float Bounciness;
    public float Friction;
    public float DynFriction;
    public float DamageReduction;
    public float RicochetAngle;
    public float RicochetDamReduction;
    public float RicochetVelReduction;
    public uint Flags;
}

/// <summary>
/// Global physics variables. Port of PhysicsVars from physinterface.h.
/// </summary>
public class PhysicsVars
{
    public PhysVector3 Gravity = new(0, 0, -9.8f);  // C++ default is -9.8, not -9.81
    public float MaxWorldStep = 0.2f;  // C++ default is 0.2 (not 0.02)
    public int MaxSubSteps = 5;        // C++ default is 5 (not 10)
    public float FixedTimeStep;
    public float TimeGranularity = 0.0001f;  // C++ default is 0.0001 (not 0.001)

    // Contact solving
    public float MaxContactGap = 0.01f;
    public float MaxContactGapPlayer = 0.01f;
    public float MaxUnprojVel = 2.5f;  // C++ default is 2.5 (not 10)

    // Sleeping
    public float MinSleepEnergy = 0.004f;
    public float SleepSpeedThreshold = 0.04f;

    // Damping
    public float GlobalDamping;
}

/// <summary>
/// Callback delegate for physics event handlers.
/// Port of the EventPhysHandlerCallback typedef from physicalworld.h.
/// Return 1 to indicate the event was handled, 0 otherwise.
/// </summary>
public delegate int EventPhysHandler(EventPhys evt);

/// <summary>
/// Event client registration. Port of SEventClient from physicalworld.h (lines 255-280).
/// Each client has a handler callback, a priority, and an optional filter on event type IDs.
/// Clients are sorted by descending priority; higher-priority clients are invoked first.
/// </summary>
public class EventClient
{
    /// <summary>Callback invoked when a matching event is dispatched.</summary>
    public EventPhysHandler Handler { get; }

    /// <summary>
    /// Priority for dispatch ordering. Higher = called first.
    /// Port of m_priority in SEventClient.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Bitmask of accepted event type IDs.
    /// Each bit corresponds to an event TypeId (0..31).
    /// If 0xFFFFFFFF, all event types are accepted (default).
    /// Port of m_bLogged / per-type filter bits from CPhysicalWorld.
    /// </summary>
    public uint EventTypeMask { get; }

    /// <summary>
    /// Create an event client that receives all event types.
    /// </summary>
    public EventClient(EventPhysHandler handler, int priority = 0)
    {
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Priority = priority;
        EventTypeMask = 0xFFFFFFFF;
    }

    /// <summary>
    /// Create an event client that receives only specific event types.
    /// </summary>
    /// <param name="handler">Callback delegate.</param>
    /// <param name="priority">Dispatch priority (higher = first).</param>
    /// <param name="eventTypeMask">
    /// Bitmask: set bit N to accept events with TypeId == N.
    /// E.g. (1u &lt;&lt; 2) | (1u &lt;&lt; 4) accepts collision (2) and post-step (4) events.
    /// </param>
    public EventClient(EventPhysHandler handler, int priority, uint eventTypeMask)
    {
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Priority = priority;
        EventTypeMask = eventTypeMask;
    }

    /// <summary>
    /// Check whether this client accepts events of the given type ID.
    /// </summary>
    public bool AcceptsEventType(int typeId)
    {
        if (typeId < 0 || typeId >= 32) return EventTypeMask == 0xFFFFFFFF;
        return (EventTypeMask & (1u << typeId)) != 0;
    }
}

/// <summary>
/// Main physics world. Creates/manages entities, steps simulation, handles queries.
/// Port of CPhysicalWorld from CryEngine.
/// Supports both single-threaded and multi-threaded stepping.
/// </summary>
public class PhysicalWorld : IPhysicalWorld
{
    private readonly List<IPhysicalEntity> _entities = new();
    private readonly Dictionary<int, IPhysicalEntity> _entityById = new();
    private int _nextEntityId = 1;
    private float _physicsTime;

    // Spatial hash grid - port of m_entgrid from CPhysicalWorld
    private SpatialGrid? _grid;

    // Threading infrastructure - port of m_nWorkerThreads, PhysicsWorkerThread from physicalworld.cpp
    private PhysicsThreadPool? _threadPool;
    private int _numThreads = 1; // 1 = single-threaded (main thread only)

    // Physics areas - port of m_pAreas linked list from CPhysicalWorld
    private readonly List<PhysicsArea> _areas = new();

    // Event system - port of m_pEventClients / m_pLoggedEvents from physicalworld.h
    private readonly List<EventClient> _eventClients = new();
    private readonly List<EventPhys> _loggedEvents = new();
    private readonly object _eventLock = new();

    public PhysicsVars Vars { get; } = new();
    public GeometryManager GeomManager { get; } = new();
    public SurfaceParameters[] SurfaceParams { get; } = new SurfaceParameters[SurfaceConstants.NSurfaceTypes];

    public float PhysicsTime => _physicsTime;

    /// <summary>All entities in the world.</summary>
    public IReadOnlyList<IPhysicalEntity> Entities => _entities;

    /// <summary>Whether multi-threaded stepping is enabled.</summary>
    public bool IsMultiThreaded => _threadPool != null;

    /// <summary>
    /// Set the number of physics threads.
    /// Port of the m_vars.numThreads change handling in CPhysicalWorld::TimeStep.
    /// Pass 1 for single-threaded (default), >1 to enable the thread pool.
    /// </summary>
    public void SetNumThreads(int numThreads)
    {
        numThreads = System.Math.Clamp(numThreads, 1, PhysicsThreading.MaxPhysThreads);
        if (numThreads == _numThreads) return;

        // Dispose existing pool
        _threadPool?.Dispose();
        _threadPool = null;

        _numThreads = numThreads;
        if (numThreads > 1)
        {
            _threadPool = new PhysicsThreadPool(numThreads - 1); // -1 because main thread participates
        }

        // Mark main thread as physics thread 0
        PhysicsThreading.MarkAsPhysThread(0);
    }

    // ============================================================================
    // Entity Management
    // ============================================================================

    public IPhysicalEntity CreatePhysicalEntity(PhysicsEntityType type,
        PhysicsParamsBase? initialParams = null, object? foreignData = null, int foreignDataType = 0, int id = -1)
    {
        PhysicalEntity entity = type switch
        {
            PhysicsEntityType.Static => new PhysicalEntity(),
            PhysicsEntityType.Rigid => new RigidEntity(),
            PhysicsEntityType.WheeledVehicle => new WheeledVehicleEntity(),
            PhysicsEntityType.Living => new LivingEntity(),
            PhysicsEntityType.Particle => new ParticleEntity(),
            PhysicsEntityType.Articulated => new ArticulatedEntity(),
            PhysicsEntityType.Rope => new RopeEntity(),
            PhysicsEntityType.Soft => new SoftEntity(),
            _ => new PhysicalEntity()
        };

        if (id < 0) id = _nextEntityId++;
        entity.Id = id;
        entity.World = this;
        entity.ForeignData = foreignData;
        entity.ForeignDataType = foreignDataType;

        // Apply gravity to rigid entities
        if (entity is RigidEntity rigid)
        {
            rigid.Gravity = Vars.Gravity;
        }

        if (initialParams != null)
            entity.SetParams(initialParams);

        _entities.Add(entity);
        _entityById[id] = entity;
        _grid?.AddEntity(entity);

        return entity;
    }

    public void DestroyPhysicalEntity(IPhysicalEntity entity)
    {
        _grid?.RemoveEntity(entity);
        _entities.Remove(entity);
        _entityById.Remove(entity.Id);
    }

    /// <summary>
    /// Setup the spatial entity grid.
    /// Port of CPhysicalWorld::SetupEntityGrid from physicalworld.cpp.
    /// </summary>
    public void SetupEntityGrid(PhysVector3 origin, int nx, int ny, float stepX, float stepY)
    {
        _grid = new SpatialGrid(origin, nx, ny, stepX, stepY);
        // Re-add all existing entities
        foreach (var ent in _entities)
            _grid.AddEntity(ent);
    }

    // ============================================================================
    // Simulation
    // ============================================================================

    /// <summary>
    /// TimeStep flags for controlling simulation mode.
    /// Port of ent_flagsX from physinterface.h.
    /// </summary>
    public const int TimeStepFlagThreaded = 1 << 16;

    public void TimeStep(float dt, int flags = 0)
    {
        if (dt <= 0) return;

        float maxStep = Vars.MaxWorldStep;
        float remaining = dt;

        while (remaining > 0)
        {
            float step = MathF.Min(remaining, maxStep);

            if (_threadPool != null)
            {
                TimeStepThreaded(step, flags);
            }
            else
            {
                TimeStepSingleThreaded(step, flags);
            }

            remaining -= step;
            _physicsTime += step;
        }
    }

    /// <summary>
    /// Single-threaded simulation step. Original behavior preserved.
    /// </summary>
    private void TimeStepSingleThreaded(float step, int flags)
    {
        // Apply area effects (buoyancy/gravity) to rigid entities before stepping
        if (_areas.Count > 0)
        {
            ApplyAreaEffects(step);
        }

        // Step all awake entities (C++ processes by type: static, rigid, living, etc.)
        foreach (var entity in _entities)
        {
            if (entity.IsAwake)
            {
                entity.DoStep(step);

                // Update spatial grid after entity moves
                _grid?.UpdateEntity(entity);
            }
        }

        // Dispatch queued events at end of step
        PumpLoggedEvents();
    }

    /// <summary>
    /// Multi-threaded simulation step.
    /// Port of the phased parallel stepping from CPhysicalWorld::TimeStep in physicalworld.cpp.
    ///
    /// Phase 1: StartStep - prepare entities for stepping (parallel per entity)
    /// Phase 2: CheckForNewContacts - detect contacts using thread-safe grid queries (parallel)
    /// Phase 3: Contact solving - sequential (solver handles multi-body coherence)
    /// Phase 4: Update positions and grid (parallel)
    /// </summary>
    private void TimeStepThreaded(float step, int flags)
    {
        var pool = _threadPool!;

        // Collect awake entities for this step
        var awakeEntities = new List<IPhysicalEntity>();
        foreach (var entity in _entities)
        {
            if (entity.IsAwake)
                awakeEntities.Add(entity);
        }

        if (awakeEntities.Count == 0) return;

        // Apply area effects before stepping (sequential - reads shared area list)
        if (_areas.Count > 0)
        {
            ApplyAreaEffects(step);
        }

        int iCaller = PhysicsThreading.GetICaller();

        // Phase 1: StartStep - advance entity state (parallel per entity)
        // Port of the pent->StartStep(time_interval) loop in physicalworld.cpp
        pool.ParallelFor(0, awakeEntities.Count, i =>
        {
            awakeEntities[i].DoStep(step, PhysicsThreading.GetICaller());
        });

        // Phase 2: Update spatial grid (sequential - write lock)
        // Grid updates must be serialized since they mutate shared state.
        // In C++ this is protected by m_lockGrid.
        foreach (var entity in awakeEntities)
        {
            _grid?.UpdateEntity(entity);
        }

        // Phase 3: Contact solving would happen here (sequential)
        // The contact solver runs on the main thread after contacts are merged.
        // This is a hook point for future integration with ContactSolver.MergeThreadContacts.

        // Phase 4: Post-step finalization (parallel)
        // In a full implementation this would apply solver results back to entities.
        // Currently DoStep handles the full integration, so this phase is a no-op.

        // Dispatch queued events at end of step
        PumpLoggedEvents();
    }

    /// <summary>
    /// Apply area effects (gravity overrides and buoyancy) to rigid entities.
    /// Port of the area-checking loop from CPhysicalWorld::TimeStep in physicalworld.cpp,
    /// which calls CheckAreas for each rigid body and then ApplyBuoyancy.
    /// </summary>
    private void ApplyAreaEffects(float step)
    {
        foreach (var entity in _entities)
        {
            if (!entity.IsAwake) continue;
            if (entity is not RigidEntity rigid) continue;

            var pos = rigid.Position;
            var buoyancy = CheckAreas(pos, out var gravity);

            // Apply gravity override from areas
            rigid.Gravity = gravity;

            // Apply buoyancy if inside a water area
            if (buoyancy != null)
            {
                var buoyArr = new ParamsBuoyancy[] { buoyancy };
                rigid.ApplyBuoyancy(step, gravity, buoyArr, 1);
            }
        }
    }

    // ============================================================================
    // Queries
    // ============================================================================

    public int RayWorldIntersection(in PhysVector3 origin, in PhysVector3 dir, int objectTypes,
        uint flags, RayHit[] hits, int maxHits, IPhysicalEntity[]? skipEntities = null)
    {
        int nHits = 0;
        var skipSet = skipEntities != null ? new HashSet<IPhysicalEntity>(skipEntities) : null;

        // Use grid if available for spatial acceleration
        IPhysicalEntity[] candidates;
        int nCandidates;
        if (_grid != null)
        {
            candidates = new IPhysicalEntity[256];
            nCandidates = _grid.RayQuery(origin, dir, candidates, 256, skipSet);
        }
        else
        {
            candidates = _entities.ToArray();
            nCandidates = candidates.Length;
        }

        var ray = new Primitives.Ray(origin, dir);
        var pinters = new Primitives.PrimInters();

        for (int i = 0; i < nCandidates && nHits < maxHits; i++)
        {
            var entity = candidates[i];
            if (skipSet?.Contains(entity) == true) continue;
            if (entity is not PhysicalEntity pe) continue;

            if (!RayIntersectsAABB(origin, dir, pe.BBoxMin, pe.BBoxMax))
                continue;

            // Test ray against each geometry part
            foreach (var part in pe.Parts)
            {
                if (part.PhysGeom?.Geometry == null) continue;
                var geom = part.PhysGeom.Geometry;
                var prim = geom.GetPrimitive();
                if (prim == null) continue;

                int result = Collision.IntersectionChecker.Instance.Check(
                    Primitives.Ray.Type, prim.TypeId, ray, prim, pinters);

                if (result > 0)
                {
                    hits[nHits] = new RayHit
                    {
                        Entity = entity,
                        Point = pinters.Pt0,
                        Normal = pinters.Normal,
                        Distance = (pinters.Pt0 - origin).Length(),
                        SurfaceIdx = part.PhysGeom.SurfaceIdx,
                        PartId = part.Id
                    };
                    nHits++;
                    break; // One hit per entity
                }
            }
        }

        // Sort by distance
        Array.Sort(hits, 0, nHits, Comparer<RayHit>.Create((a, b) => a.Distance.CompareTo(b.Distance)));

        return nHits;
    }

    public int GetEntitiesInBox(in PhysVector3 min, in PhysVector3 max, IPhysicalEntity[] entities, int objectTypes)
    {
        if (_grid != null)
            return _grid.GetEntitiesInBox(min, max, entities, entities.Length);

        // Fallback: linear scan
        int count = 0;
        foreach (var entity in _entities)
        {
            if (count >= entities.Length) break;
            if (entity is PhysicalEntity pe)
            {
                if (pe.BBoxMax.X >= min.X && pe.BBoxMin.X <= max.X &&
                    pe.BBoxMax.Y >= min.Y && pe.BBoxMin.Y <= max.Y &&
                    pe.BBoxMax.Z >= min.Z && pe.BBoxMin.Z <= max.Z)
                {
                    entities[count++] = entity;
                }
            }
        }
        return count;
    }

    public void SimulateExplosion(in PhysVector3 epicenter, float radius, float pressure)
    {
        float r2 = radius * radius;
        foreach (var entity in _entities)
        {
            if (entity is RigidEntity re)
            {
                var dir = re.Position - epicenter;
                float dist2 = dir.LengthSq();
                if (dist2 < r2 && dist2 > 1e-6f)
                {
                    float dist = MathF.Sqrt(dist2);
                    float falloff = 1f - dist / radius;
                    var impulse = dir * (pressure * falloff / dist);
                    re.DoAction(new ActionImpulse { Impulse = impulse, Point = re.Position });
                }
            }
        }
    }

    public void SetSurfaceParameters(int surfIdx, float bounciness, float friction, uint flags = 0)
    {
        if (surfIdx >= 0 && surfIdx < SurfaceParams.Length)
        {
            SurfaceParams[surfIdx] = new SurfaceParameters
            {
                Bounciness = bounciness,
                Friction = friction,
                DynFriction = friction, // default: same as static friction
                Flags = flags
            };
        }
    }

    /// <summary>
    /// Extended surface parameter setter matching C++ SetSurfaceParameters overload.
    /// </summary>
    public void SetSurfaceParameters(int surfIdx, float bounciness, float friction,
        float dynFriction, float damageReduction, float ricochetAngle,
        float ricochetDamReduction, float ricochetVelReduction, uint flags)
    {
        if (surfIdx >= 0 && surfIdx < SurfaceParams.Length)
        {
            SurfaceParams[surfIdx] = new SurfaceParameters
            {
                Bounciness = bounciness,
                Friction = friction,
                DynFriction = dynFriction,
                DamageReduction = damageReduction,
                RicochetAngle = ricochetAngle,
                RicochetDamReduction = ricochetDamReduction,
                RicochetVelReduction = ricochetVelReduction,
                Flags = flags
            };
        }
    }

    /// <summary>
    /// Get averaged friction between two surface materials.
    /// Port of GetFriction from physicalworld.h.
    /// </summary>
    public float GetFriction(int imat0, int imat1, bool bDynamic = false)
    {
        int mask = SurfaceConstants.NSurfaceTypes - 1;
        ref var s0 = ref SurfaceParams[imat0 & mask];
        ref var s1 = ref SurfaceParams[imat1 & mask];
        float f0 = bDynamic ? s0.DynFriction : s0.Friction;
        float f1 = bDynamic ? s1.DynFriction : s1.Friction;
        return MathF.Max(0f, (f0 + f1) * 0.5f);
    }

    /// <summary>
    /// Get averaged bounciness between two surface materials.
    /// Port of GetBounciness from physicalworld.h.
    /// </summary>
    public float GetBounciness(int imat0, int imat1)
    {
        int mask = SurfaceConstants.NSurfaceTypes - 1;
        return (SurfaceParams[imat0 & mask].Bounciness + SurfaceParams[imat1 & mask].Bounciness) * 0.5f;
    }

    // ============================================================================
    // Physics Areas (port of CPhysArea management from physicalworld.cpp)
    // ============================================================================

    /// <summary>All registered physics areas.</summary>
    public IReadOnlyList<PhysicsArea> Areas => _areas;

    /// <summary>
    /// Register a physics area with the world.
    /// Port of CPhysicalWorld::AddArea from physicalworld.cpp.
    /// Areas are kept sorted by descending priority so higher-priority areas
    /// are evaluated first during CheckAreas.
    /// </summary>
    public void AddArea(PhysicsArea area)
    {
        _areas.Add(area);
        // Keep sorted by descending priority (highest first)
        _areas.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    /// <summary>
    /// Remove a physics area from the world.
    /// Port of CPhysicalWorld::RemoveArea.
    /// </summary>
    public void RemoveArea(PhysicsArea area)
    {
        _areas.Remove(area);
    }

    /// <summary>
    /// Check which areas contain the given point and return combined buoyancy
    /// parameters from the highest-priority water area that contains it.
    /// Port of CPhysicalWorld::CheckAreas from physicalworld.cpp.
    /// </summary>
    /// <param name="pos">World-space position to test.</param>
    /// <returns>Buoyancy parameters if the point is in a water area, null otherwise.</returns>
    public ParamsBuoyancy? CheckAreas(PhysVector3 pos)
    {
        return CheckAreas(pos, out _);
    }

    /// <summary>
    /// Check which areas contain the given point, returning buoyancy and gravity.
    /// Port of CPhysicalWorld::CheckAreas from physicalworld.cpp.
    /// The highest-priority matching area wins for gravity override.
    /// The highest-priority matching water area provides buoyancy params.
    /// </summary>
    /// <param name="pos">World-space position to test.</param>
    /// <param name="gravityOverride">
    /// Set to the custom gravity of the highest-priority area containing the point,
    /// or to <see cref="PhysicsVars.Gravity"/> if no area overrides gravity.
    /// </param>
    /// <returns>Buoyancy parameters from the highest-priority water area, or null.</returns>
    public ParamsBuoyancy? CheckAreas(PhysVector3 pos, out PhysVector3 gravityOverride)
    {
        gravityOverride = Vars.Gravity;
        ParamsBuoyancy? buoyancy = null;
        bool gravitySet = false;

        // Areas are sorted by descending priority
        for (int i = 0; i < _areas.Count; i++)
        {
            var area = _areas[i];
            float blend = area.CheckPoint(pos);
            if (blend <= 0f) continue;

            // Gravity override: first (highest priority) area wins
            if (!gravitySet && area.Gravity.HasValue)
            {
                if (blend >= 1f)
                {
                    gravityOverride = area.Gravity.Value;
                }
                else
                {
                    // Blend gravity with world default
                    gravityOverride = Vars.Gravity * (1f - blend) + area.Gravity.Value * blend;
                }
                gravitySet = true;
            }

            // Buoyancy: first (highest priority) water area wins
            if (buoyancy == null && area.IsWaterVolume)
            {
                buoyancy = area.ToBuoyancyParams();
            }

            // If we have both overrides, stop searching
            if (gravitySet && buoyancy != null) break;
        }

        return buoyancy;
    }

    // ============================================================================
    // Event Dispatcher (port of event client system from physicalworld.h/cpp)
    // ============================================================================

    /// <summary>
    /// Register an event client to receive physics events.
    /// Port of CPhysicalWorld::AddEventClient from physicalworld.cpp.
    /// Clients are sorted by descending priority.
    /// </summary>
    public void AddEventClient(EventClient client)
    {
        lock (_eventLock)
        {
            _eventClients.Add(client);
            _eventClients.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>
    /// Unregister an event client.
    /// Port of CPhysicalWorld::RemoveEventClient.
    /// </summary>
    public void RemoveEventClient(EventClient client)
    {
        lock (_eventLock)
        {
            _eventClients.Remove(client);
        }
    }

    /// <summary>
    /// Queue an event for deferred dispatch at end of TimeStep.
    /// Port of CPhysicalWorld::OnEvent / logging path from physicalworld.cpp.
    /// Thread-safe: can be called from worker threads during stepping.
    /// </summary>
    public void LogEvent(EventPhys evt)
    {
        lock (_eventLock)
        {
            _loggedEvents.Add(evt);
        }
    }

    /// <summary>
    /// Dispatch an event immediately to all matching event clients.
    /// Port of CPhysicalWorld::DispatchEvent (inline dispatch path).
    /// Returns true if any client handled the event (returned 1).
    /// </summary>
    public bool DispatchEvent(EventPhys evt)
    {
        // Take a snapshot under lock to avoid issues if a handler modifies the list
        EventClient[] clients;
        lock (_eventLock)
        {
            clients = _eventClients.ToArray();
        }

        bool handled = false;
        int typeId = evt.TypeId;

        for (int i = 0; i < clients.Length; i++)
        {
            var client = clients[i];
            if (!client.AcceptsEventType(typeId)) continue;

            int result = client.Handler(evt);
            if (result != 0) handled = true;
        }

        return handled;
    }

    /// <summary>
    /// Dispatch all queued (logged) events and clear the queue.
    /// Port of CPhysicalWorld::PumpLoggedEvents from physicalworld.cpp.
    /// Called at the end of each TimeStep sub-step.
    /// </summary>
    public void PumpLoggedEvents()
    {
        // Swap out the queue so new events logged during dispatch go to a fresh list
        EventPhys[] pending;
        lock (_eventLock)
        {
            if (_loggedEvents.Count == 0) return;
            pending = _loggedEvents.ToArray();
            _loggedEvents.Clear();
        }

        for (int i = 0; i < pending.Length; i++)
        {
            DispatchEvent(pending[i]);
        }
    }

    // ============================================================================
    // Utility
    // ============================================================================

    private static bool RayIntersectsAABB(in PhysVector3 origin, in PhysVector3 dir, in PhysVector3 min, in PhysVector3 max)
    {
        float tmin = 0, tmax = 1;

        for (int i = 0; i < 3; i++)
        {
            float o = origin[i], d = dir[i];
            float lo = min[i], hi = max[i];

            if (MathF.Abs(d) < 1e-10f)
            {
                if (o < lo || o > hi) return false;
                continue;
            }

            float invD = 1f / d;
            float t1 = (lo - o) * invD;
            float t2 = (hi - o) * invD;
            if (t1 > t2) (t1, t2) = (t2, t1);

            tmin = MathF.Max(tmin, t1);
            tmax = MathF.Min(tmax, t2);
            if (tmin > tmax) return false;
        }

        return true;
    }
}
