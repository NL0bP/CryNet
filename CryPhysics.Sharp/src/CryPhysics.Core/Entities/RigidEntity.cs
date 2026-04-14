// Port of CryPhysics rigidentity.h/cpp - rigid body entity
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Dynamics;
using CryPhysics.Geometry;
using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

// ============================================================================
// Supporting types ported from rigidentity.h
// ============================================================================

/// <summary>
/// Constraint info flags. Port of constr_info_flags from rigidentity.h.
/// </summary>
[Flags]
public enum ConstraintInfoFlags
{
    Limited1Axis = 1,
    Limited2Axes = 2,
    Rope = 4,
    Broken = 0x10000,
}

/// <summary>
/// Per-constraint metadata. Port of constraint_info from rigidentity.h.
/// </summary>
public class ConstraintInfo
{
    public int Id;
    public PhysQuaternion[] QFrameRel = { PhysQuaternion.Identity, PhysQuaternion.Identity };
    public PhysVector3[] PtLoc = new PhysVector3[2];
    public float[] Limits = new float[2];
    public uint Flags;
    public float Damping;
    public float SensorRadius = 0.05f;
    public PhysicalEntity? PConstraintEnt;
    public int BActive;
    public PhysQuaternion[] QPrev = { PhysQuaternion.Identity, PhysQuaternion.Identity };
    public float Limit;
}

/// <summary>
/// Collider tracking data for CheckForNewContacts. Port of g_CurColliders/g_CurCollParts.
/// </summary>
internal struct ColliderRecord
{
    public PhysicalEntity? Entity;
    public int Part0;
    public int Part1;
    public CryPhysics.Geometry.GeomContact? Contact;  // raw geom_contact from Intersect
}

// ============================================================================
// RigidEntity
// ============================================================================

/// <summary>
/// Rigid body physical entity. Port of CRigidEntity from CryEngine.
/// Supports dynamics, impulses, constraints, collision detection, and contact solving.
/// </summary>
public class RigidEntity : PhysicalEntity
{
    // Mask constants - port of masktype / NMASKBITS
    private const int NMASKBITS = 64;
    private static ulong GetMask(int i) => 1UL << i;

    public override PhysicsEntityType Type => PhysicsEntityType.Rigid;

    public RigidBody Body { get; } = new();

    // ========================================================================
    // Entity-level simulation parameters (from CRigidEntity members)
    // ========================================================================

    // TimeIdle now lives on PhysicalEntity (port of CPhysicalEntity::m_timeIdle).
    // RigidEntity reads/writes via the inherited property.
    public float MaxTimeStep { get; set; } = 0.02f;  // m_maxAllowedStep
    public float MinEnergy { get; set; } = 0.07f * 0.07f; // m_Emin = sqr(0.07)
    public float MinEnergyWater { get; set; } = 0.01f * 0.01f;
    public PhysVector3 Gravity;
    public PhysVector3 GravityFreefall;
    public float Damping;
    public float DampingFreefall = 0.1f;
    public float DampingEx;
    public float MaxAngVel = 20.0f;   // m_maxw

    // Water/buoyancy
    public float WaterDamping;
    public float KWaterDensity = 1.0f;
    public float KWaterResistance = 1.0f;
    public float SubmergedFraction;

    // Timing
    public float TimeStepFull = 0.01f;
    public float TimeStepPerformed;
    public float LastTimeStep;
    public float NextTimeStep;
    public float MinAwakeTime;

    // Sleep tracking
    private int _awakeState = 1;   // m_bAwake (8 bits in C++)
    private int _sleepFrames;      // m_nSleepFrames
    private int _futileUnprojFrames;
    private PhysVector3 _vAccum;
    private PhysVector3 _wAccum;
    private PhysVector3 _vSleep;
    private PhysVector3 _wSleep;
    private uint _restMask;

    // Previous state (for StepBack)
    private PhysVector3 _prevPos;
    private PhysQuaternion _prevQ;
    private PhysVector3 _prevV;
    private PhysVector3 _prevW;
    private float _e0;

    // External impulse accumulators (m_Pext, m_Lext)
    private PhysVector3 _pExt;
    private PhysVector3 _lExt;
    private PhysVector3 _pSoft;
    private PhysVector3 _lSoft;

    // Position tracking
    private PhysVector3 _posNew;
    private PhysQuaternion _qNew = PhysQuaternion.Identity;

    // Velocity/size for sweep check
    private float _velFastDir;
    private float _sizeFastDir;
    private PhysVector3 _forcedMove;

    // Contact state
    private float _minFriction = 0.1f;
    private float _maxFriction = 100.0f;
    private EntityContact? _stableContact;
    private int _stable;
    private bool _steppedBack;
    private int _stepBackCount;
    private bool _hadSeverePenetration;
    private bool _bFloating;
    private bool _disablePreCG;

    // ========================================================================
    // Collider tracking (port of m_pColliders, m_pColliderContacts, m_pColliderConstraints)
    // ========================================================================

    private readonly List<PhysicalEntity> _colliders = new();
    private readonly List<EntityContact?> _colliderContacts = new();
    private readonly List<ulong> _colliderConstraints = new();
    private int _nPrevColliders;
    private int _nContacts;
    private EntityContact? _contactStart;  // linked list head

    // ========================================================================
    // Constraint system (port of m_pConstraints, m_pConstraintInfos, m_constraintMask)
    // ========================================================================

    private readonly List<EntityContact> _constraints = new();
    private readonly List<ConstraintInfo> _constraintInfos = new();
    private ulong _constraintMask;

    // Temporary buffer for contact detection
    private readonly ColliderRecord[] _curColliders = new ColliderRecord[128];
    private int _nLastContacts;                  // g_nLastContacts — valid entries in _curColliders
    private int _maxEntityContacts = 128;        // m_pWorld->m_vars.nMaxEntityContacts equivalent

    // ========================================================================
    // Constructor
    // ========================================================================

    public RigidEntity()
    {
        SimulationClass = SimClass.ActiveRigid;
        IsAwake = true;
        Gravity = new PhysVector3(0, 0, -9.81f);
        GravityFreefall = Gravity;
    }

    // ========================================================================
    // SetParams / GetParams / DoAction / GetStatus
    // ========================================================================

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        int result = base.SetParams(parameters, threadSafe);
        if (result > 0 && parameters is ParamsPos)
        {
            Body.Pos = Position;
            Body.Q = Orientation;
        }

        if (parameters is SimulationParams sim)
        {
            if (sim.MaxTimeStep.HasValue) MaxTimeStep = sim.MaxTimeStep.Value;
            if (sim.MinEnergy.HasValue) MinEnergy = sim.MinEnergy.Value;
            if (sim.Gravity.HasValue) Gravity = sim.Gravity.Value;
            if (sim.Damping.HasValue) Damping = sim.Damping.Value;
            if (sim.DampingFreefall.HasValue) DampingFreefall = sim.DampingFreefall.Value;
            if (sim.MaxRotVel.HasValue) MaxAngVel = sim.MaxRotVel.Value;
            if (sim.MaxFriction.HasValue) _maxFriction = sim.MaxFriction.Value;
            if (sim.DisablePreCG.HasValue) _disablePreCG = sim.DisablePreCG.Value != 0;
            return 1;
        }

        if (parameters is ParamsBuoyancy buoy)
        {
            if (buoy.WaterDamping.HasValue) WaterDamping = buoy.WaterDamping.Value;
            if (buoy.WaterDensity.HasValue) KWaterDensity = buoy.WaterDensity.Value;
            if (buoy.WaterResistance.HasValue) KWaterResistance = buoy.WaterResistance.Value;
            if (buoy.WaterEmin.HasValue) MinEnergyWater = buoy.WaterEmin.Value;
            return 1;
        }

        return result;
    }

    public override int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        switch (action)
        {
            case ActionImpulse impulse:
                if (impulse.Impulse.HasValue)
                {
                    if (impulse.Point.HasValue)
                        Body.ApplyImpulse(impulse.Impulse.Value, impulse.Point.Value);
                    else
                        Body.P = Body.P + impulse.Impulse.Value;
                }
                if (impulse.AngImpulse.HasValue)
                    Body.ApplyAngularImpulse(impulse.AngImpulse.Value);
                Awake();
                return 1;

            case ActionSetVelocity setVel:
                if (setVel.Velocity.HasValue)
                {
                    Body.V = setVel.Velocity.Value;
                    Body.P = Body.V * Body.M;
                }
                if (setVel.AngularVelocity.HasValue)
                {
                    Body.W = setVel.AngularVelocity.Value;
                    var R = new PhysMatrix33(Body.Q);
                    Body.L = R * (PhysMatrix33)Body.Ibody * R.Transposed() * Body.W;
                }
                return 1;

            case ActionAwake awake:
                Awake(awake.Awake ?? true, awake.MinAwakeTime ?? 0f);
                return 1;

            case ActionReset:
                Body.V = PhysVector3.Zero;
                Body.W = PhysVector3.Zero;
                Body.P = PhysVector3.Zero;
                Body.L = PhysVector3.Zero;
                Body.Fcollision = PhysVector3.Zero;
                Body.Tcollision = PhysVector3.Zero;
                return 1;

            case ActionAddConstraint addC:
            {
                var pt0 = addC.Pt0 ?? PhysVector3.Zero;
                var pt1 = addC.Pt1 ?? PhysVector3.Zero;
                var buddy = addC.PBuddy as PhysicalEntity;
                int idx = RegisterConstraint(pt0, pt1, addC.PartId0, buddy, addC.PartId1,
                    (int)(addC.Flags ?? 0), 0);
                if (idx >= 0 && addC.Id.HasValue)
                    _constraintInfos[idx].Id = addC.Id.Value;
                if (idx >= 0 && addC.QFrame0.HasValue)
                    _constraintInfos[idx].QFrameRel[0] = addC.QFrame0.Value;
                if (idx >= 0 && addC.QFrame1.HasValue)
                    _constraintInfos[idx].QFrameRel[1] = addC.QFrame1.Value;
                if (idx >= 0 && addC.MaxPullForce.HasValue)
                    _constraintInfos[idx].Limits[0] = addC.MaxPullForce.Value;
                if (idx >= 0 && addC.MaxBendTorque.HasValue)
                    _constraintInfos[idx].Limits[1] = addC.MaxBendTorque.Value;
                return idx >= 0 ? 1 : 0;
            }

            default:
                return base.DoAction(action, threadSafe);
        }
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        switch (status)
        {
            case StatusDynamics dyn:
                dyn.Velocity = Body.V;
                dyn.AngularVelocity = Body.W;
                dyn.Mass = Body.M;
                dyn.Energy = Body.Energy;
                dyn.CenterOfMass = Body.Pos;
                dyn.SubmergedFraction = SubmergedFraction;
                dyn.TimeIdle = TimeIdle;
                return 1;

            default:
                return base.GetStatus(status);
        }
    }

    // ========================================================================
    // Geometry management
    // ========================================================================

    public override int AddGeometry(PhysGeometry geometry, ParamsPart parameters, int id = -1, bool threadSafe = false)
    {
        id = base.AddGeometry(geometry, parameters, id, threadSafe);
        RecomputeMassProperties();
        return id;
    }

    // ========================================================================
    // Collider management (port of AddCollider/RemoveCollider)
    // ========================================================================

    /// <summary>
    /// Add a collider to this entity's collider list.
    /// Port of CRigidEntity::AddCollider / AddColliderNoLock.
    /// Returns the index of the collider in the list.
    /// </summary>
    public int AddCollider(PhysicalEntity collider)
    {
        // Check if already present
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] == collider)
                return i;
        }

        int idx = _colliders.Count;
        _colliders.Add(collider);
        _colliderContacts.Add(null);
        _colliderConstraints.Add(0);
        return idx;
    }

    /// <summary>
    /// Remove a collider from this entity's collider list.
    /// Port of CRigidEntity::RemoveCollider / RemoveColliderNoLock.
    /// </summary>
    public int RemoveCollider(PhysicalEntity collider, bool removeAlways = true)
    {
        int i;
        for (i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] == collider)
                break;
        }
        if (i == _colliders.Count)
            return -1;

        if (!removeAlways)
        {
            // Don't remove if there are still contacts or constraints
            if (_colliderContacts[i] != null || _colliderConstraints[i] != 0)
                return i;
        }

        // Clear constraint mask bits for this collider
        _constraintMask &= ~_colliderConstraints[i];

        _colliders.RemoveAt(i);
        _colliderContacts.RemoveAt(i);
        _colliderConstraints.RemoveAt(i);

        return i;
    }

    /// <summary>
    /// Check whether this entity has contacts with pent.
    /// Port of CRigidEntity::HasContactsWith.
    /// </summary>
    public int HasContactsWith(PhysicalEntity pent)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] == pent)
                return (_colliderContacts[i] != null || _colliderConstraints[i] != 0) ? 1 : 0;
        }
        return 0;
    }

    /// <summary>
    /// Check if there are collision (non-constraint) contacts with pent.
    /// Port of CRigidEntity::HasCollisionContactsWith.
    /// </summary>
    public int HasCollisionContactsWith(PhysicalEntity pent)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] == pent)
                return _colliderContacts[i] != null ? 1 : 0;
        }
        return 0;
    }

    /// <summary>
    /// Check if there are constraint contacts with pent.
    /// Port of CRigidEntity::HasConstraintContactsWith.
    /// </summary>
    public int HasConstraintContactsWith(PhysicalEntity pent, int flagsIgnore = 0)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] != pent) continue;
            ulong mask = _colliderConstraints[i];
            for (int j = 0; j < NMASKBITS && GetMask(j) <= mask; j++)
            {
                if ((mask & GetMask(j)) != 0 &&
                    (_constraintInfos[j].Flags & (uint)flagsIgnore) == 0)
                    return 1;
            }
        }
        return 0;
    }

    // ========================================================================
    // GetPotentialColliders (port of CRigidEntity::GetPotentialColliders)
    // ========================================================================

    /// <summary>
    /// Query the world for entities that may collide with this one.
    /// Simplified port of CRigidEntity::GetPotentialColliders.
    /// </summary>
    public int GetPotentialColliders(PhysicalEntity[] output, float dt = 0)
    {
        if (Body.Minv + Body.V.LengthSq() + Body.W.LengthSq() <= 0)
            return 0;

        var world = World;
        if (world == null) return 0;

        // Expand BBox by velocity
        var move = Body.V * TimeStepFull;
        var gap = new PhysVector3(0.04f, 0.04f, 0.04f); // maxContactGap * 4
        var bboxMin = BBoxMin + PhysVector3.Min(PhysVector3.Zero, move) - gap;
        var bboxMax = BBoxMax + PhysVector3.Max(PhysVector3.Zero, move) + gap;

        // Query world for overlapping entities
        var tempList = new IPhysicalEntity[256];
        int nEnts = world.GetEntitiesInBox(bboxMin, bboxMax, tempList, 0xFF);

        int j = 0;
        for (int i = 0; i < nEnts && j < output.Length; i++)
        {
            if (tempList[i] is not PhysicalEntity pe) continue;
            if (pe == this) continue;
            if (pe.SimulationClass == SimClass.Deleted) continue;

            // For sleeping/static entities in same group, may skip
            if (Body.Minv <= 0)
            {
                pe.Awake();
                continue;
            }

            // AABB overlap check (entities with zero-size BBox always pass)
            var entSize = pe.BBoxMax - pe.BBoxMin;
            if (entSize.LengthSq() > 0 && !AABBOverlap(bboxMin, bboxMax, pe.BBoxMin, pe.BBoxMax))
                continue;

            output[j++] = pe;
        }

        return j;
    }

    // ========================================================================
    // Constraint system (port of RegisterConstraint / RemoveConstraint)
    // ========================================================================

    /// <summary>
    /// Register a new constraint between this entity and a buddy.
    /// Port of CRigidEntity::RegisterConstraint.
    /// Returns constraint index, or -1 on failure.
    /// </summary>
    public int RegisterConstraint(in PhysVector3 pt0, in PhysVector3 pt1,
        int ipart0, PhysicalEntity? buddy, int ipart1, int flags, int flagsInfo = 0)
    {
        // Find first free slot in constraint mask
        int i;
        for (i = 0; i < NMASKBITS && (_constraintMask & GetMask(i)) != 0; i++) ;
        if (i == NMASKBITS) return -1;

        // Grow arrays if needed
        while (i >= _constraints.Count)
        {
            _constraints.Add(new EntityContact());
            _constraintInfos.Add(new ConstraintInfo());
        }

        // Register buddy as collider and set constraint bit
        int buddyIdx = buddy != null ? AddCollider(buddy) : -1;
        if (buddyIdx >= 0)
            _colliderConstraints[buddyIdx] |= GetMask(i);
        _constraintMask |= GetMask(i);

        // Fill constraint contact
        var constraint = _constraints[i];
        var nv = PhysVector3.UnitZ;
        constraint.Pt0 = pt0;
        constraint.Pt1 = pt1;
        constraint.Nloc = nv;
        constraint.N = nv;
        constraint.INormal = 0;
        constraint.IPart0 = ipart0;
        constraint.IPart1 = ipart1;
        constraint.PBody0 = Body;
        constraint.PBody1 = buddy is RigidEntity re ? re.Body : null;
        constraint.Vrel = 0;
        constraint.Friction = 0;
        constraint.Flags = flags;
        constraint.IConstraint = i + 1;
        constraint.BConstraint = true;
        constraint.Pspare = 0;
        constraint.Vreq = PhysVector3.Zero;

        // Compute local attachment points
        var info = _constraintInfos[i];
        info.PtLoc[0] = Glob2Loc(pt0, ipart0, this);
        info.PtLoc[1] = buddy != null ? Glob2Loc(pt1, ipart1, buddy) : pt1;
        info.Flags = (uint)flagsInfo;
        info.Damping = 0;
        info.PConstraintEnt = null;
        info.BActive = 0;
        info.SensorRadius = 0.05f;
        info.Limit = 0;

        return i;
    }

    /// <summary>
    /// Remove a constraint by index.
    /// Port of CRigidEntity::RemoveConstraint.
    /// </summary>
    public int RemoveConstraint(int iConstraint)
    {
        // Find which collider owns this constraint
        int i;
        for (i = 0; i < _colliders.Count; i++)
        {
            if ((_colliderConstraints[i] & GetMask(iConstraint)) != 0)
                break;
        }
        if (i == _colliders.Count)
            return 0;

        _constraintMask &= ~GetMask(iConstraint);
        _colliderConstraints[i] &= ~GetMask(iConstraint);

        // If no more contacts or constraints, remove the collider
        if (_colliderConstraints[i] == 0 && _colliderContacts[i] == null)
        {
            var collider = _colliders[i];
            if (collider is RigidEntity re && re.HasContactsWith(this) == 0)
                RemoveCollider(collider);
        }

        return 1;
    }

    /// <summary>
    /// Enforce position-level constraint corrections.
    /// Port of CRigidEntity::EnforceConstraints.
    /// (The C++ version was commented out / returns 0. We reproduce that.)
    /// </summary>
    public int EnforceConstraints(float timeInterval)
    {
        // C++ source: this method returns 0 immediately (the real logic is commented out).
        // We reproduce the same behavior.
        return 0;
    }

    /// <summary>
    /// Update constraint world-space data from local attachment points.
    /// Port of CRigidEntity::UpdateConstraints.
    /// </summary>
    private void UpdateConstraints(float timeInterval)
    {
        for (int i = 0; i < NMASKBITS && GetMask(i) <= _constraintMask; i++)
        {
            if ((_constraintMask & GetMask(i)) == 0) continue;
            if (i >= _constraints.Count) break;

            var c = _constraints[i];
            var info = _constraintInfos[i];

            // Recompute world-space constraint points from local coords
            c.Pt0 = Loc2Glob(info.PtLoc[0], c.IPart0, this);
            // For buddy, we'd need the buddy entity, but we store just the body.
            // Simplified: keep pt1 as-is if no body1.
            if (c.PBody1 != null)
            {
                // Approximate: pt1 = body1.Pos + body1.Q.Rotate(ptloc1)
                c.Pt1 = c.PBody1.Pos + c.PBody1.Q.Rotate(info.PtLoc[1]);
            }

            // Compute required velocity to close the gap
            var diff = c.Pt1 - c.Pt0;
            if ((c.Flags & (int)ContactFlags.Constraint3Dof) != 0)
            {
                float maxUnprojVel = 2.5f; // m_pWorld->m_vars.maxUnprojVel
                float gap = diff.Length();
                if (gap > 1e-6f)
                {
                    c.N = diff * (1f / gap);
                    c.Vreq = c.N * MathF.Min(maxUnprojVel, gap * 10f);
                }
                else
                {
                    c.Vreq = PhysVector3.Zero;
                }
            }

            info.BActive = 1;
        }
    }

    // ========================================================================
    // CheckForNewContacts (port of CRigidEntity::CheckForNewContacts)
    // ========================================================================

    /// <summary>
    /// Iterate potential colliders, test geometry intersection per part pair.
    /// Simplified port of CRigidEntity::CheckForNewContacts.
    /// Returns number of new contacts found.
    /// </summary>
    public int CheckForNewContacts(out int itmax)
    {
        itmax = -1;
        var pentlist = new PhysicalEntity[128];
        int nEnts = GetPotentialColliders(pentlist);
        int nTotContacts = 0;

        for (int iPart = 0; iPart < Parts.Count; iPart++)
        {
            var myPart = Parts[iPart];
            if (myPart.PhysGeom?.Geometry == null) continue;
            if (myPart.FlagsCollider == 0) continue;

            // Setup world data for this part
            var gwd0 = new GeomWorldData
            {
                Offset = Position + Orientation.Rotate(myPart.Offset),
                R = new PhysMatrix33(Orientation * myPart.Rotation),
                Scale = myPart.Scale,
                V = Body.V,
                W = Body.W,
                CenterOfMass = Body.Pos
            };

            // Compute part AABB for early-out
            var bbox = new Primitives.Box();
            myPart.PhysGeom.Geometry.GetBBox(ref bbox);
            var basisR = bbox.Basis * gwd0.R.Transposed();
            var sz = new PhysVector3(
                (MathF.Abs(basisR.M00) * bbox.Size.X + MathF.Abs(basisR.M01) * bbox.Size.Y + MathF.Abs(basisR.M02) * bbox.Size.Z) * myPart.Scale,
                (MathF.Abs(basisR.M10) * bbox.Size.X + MathF.Abs(basisR.M11) * bbox.Size.Y + MathF.Abs(basisR.M12) * bbox.Size.Z) * myPart.Scale,
                (MathF.Abs(basisR.M20) * bbox.Size.X + MathF.Abs(basisR.M21) * bbox.Size.Y + MathF.Abs(basisR.M22) * bbox.Size.Z) * myPart.Scale
            );
            var partCenter = gwd0.Offset + gwd0.R * (bbox.Center * myPart.Scale);
            var partBBMin = partCenter - sz;
            var partBBMax = partCenter + sz;

            for (int iEnt = 0; iEnt < nEnts; iEnt++)
            {
                var collider = pentlist[iEnt];
                for (int jPart = 0; jPart < collider.Parts.Count; jPart++)
                {
                    var otherPart = collider.Parts[jPart];
                    if (otherPart.PhysGeom?.Geometry == null) continue;

                    // Part AABB overlap check
                    var otherSize = otherPart.BBoxMax - otherPart.BBoxMin;
                    if (otherSize.LengthSq() > 0 &&
                        !AABBOverlap(partBBMin, partBBMax, otherPart.BBoxMin, otherPart.BBoxMax))
                        continue;

                    // Setup world data for other part
                    var gwd1 = new GeomWorldData
                    {
                        Offset = collider.Position + collider.Orientation.Rotate(otherPart.Offset),
                        R = new PhysMatrix33(collider.Orientation * otherPart.Rotation),
                        Scale = otherPart.Scale,
                    };

                    // Body velocity for dynamic colliders
                    if (collider is RigidEntity otherRigid)
                    {
                        gwd1.V = otherRigid.Body.V;
                        gwd1.W = otherRigid.Body.W;
                        gwd1.CenterOfMass = otherRigid.Body.Pos;
                    }

                    // Intersect geometries
                    int nCont = myPart.PhysGeom.Geometry.Intersect(
                        otherPart.PhysGeom.Geometry, gwd0, gwd1, null, out var contacts);

                    for (int ic = 0; ic < nCont && nTotContacts < _curColliders.Length; ic++)
                    {
                        _curColliders[nTotContacts] = new ColliderRecord
                        {
                            Entity = collider,
                            Part0 = iPart,
                            Part1 = jPart,
                            Contact = contacts[ic],
                        };

                        if (contacts[ic].T >= 0 && (itmax < 0 || contacts[ic].T > _curColliders[itmax].Contact!.T))
                            itmax = nTotContacts;

                        nTotContacts++;
                    }
                }
            }
        }

        _nLastContacts = nTotContacts;
        return nTotContacts;
    }

    // ========================================================================
    // AttachContact / RegisterContactPoint / PromoteCurrentContacts
    // (port of CRigidEntity::AttachContact and CRigidEntity::RegisterContactPoint —
    //  the bridge from raw g_CurColliders/pcontacts data into m_pColliderContacts[i])
    // ========================================================================

    /// <summary>
    /// Splice a newly allocated EntityContact into the linked list at _colliderContacts[i].
    /// Port of CRigidEntity::AttachContact (rigidentity.cpp:146).
    /// </summary>
    private void AttachContact(EntityContact pContact, int i, PhysicalEntity pCollider)
    {
        // Resolve collider index if stale
        if (i >= _colliders.Count || _colliders[i] != pCollider)
        {
            for (i = 0; i < _colliders.Count && _colliders[i] != pCollider; i++) { }
            if (i == _colliders.Count) return;
        }

        var head = _colliderContacts[i];
        if (head == null)
        {
            _colliderContacts[i] = pContact;
            pContact.Next = null;
            pContact.Prev = null;
            pContact.Flags |= (int)ContactFlags.Last;
        }
        else
        {
            pContact.Next = head;
            pContact.Prev = null;
            head.Prev = pContact;
            _colliderContacts[i] = pContact;
        }
        _nContacts++;
    }

    /// <summary>
    /// Promote a raw geom_contact (at _curColliders[idx]) into an EntityContact and attach it.
    /// Port of CRigidEntity::RegisterContactPoint (rigidentity.cpp:1574).
    /// Simplified: skips the min_dist2 merge search and iPrimCode de-dup.
    /// </summary>
    private EntityContact? RegisterContactPoint(int idx, PhysVector3 pt, int iPrim0, int iFeature0,
                                                 int iPrim1, int iFeature1, int flags, float penetration,
                                                 PhysVector3 nloc)
    {
        var rec = _curColliders[idx];
        var collider = rec.Entity;
        var gc = rec.Contact;
        if (collider == null || gc == null) return null;

        // TODO: skip contact when either part has geom_no_coll_response flag — enum not yet ported

        // Cap entity-wide contact count
        if (_nContacts >= _maxEntityContacts) return null;

        int i = AddCollider(collider);

        var pContact = new EntityContact
        {
            Pt0 = pt,
            Pt1 = pt,
            N = -gc.N,                               // C++: pContact->n = -pcontacts[idx].n
            PEnt0 = this,
            PEnt1 = collider,
            IPart0 = rec.Part0,
            IPart1 = rec.Part1,
            IPrim0 = iPrim0,
            IPrim1 = iPrim1,
            IFeature0 = iFeature0,
            IFeature1 = iFeature1,
            PBody0 = Body,
            PBody1 = (collider as RigidEntity)?.Body,
            Penetration = penetration,
            Nloc = nloc,
            Flags = flags | (int)ContactFlags.New,
            Id0 = gc.Id[0],
            Id1 = gc.Id[1],
        };

        AttachContact(pContact, i, collider);
        return pContact;
    }

    /// <summary>
    /// Walk _curColliders[0.._nLastContacts] and promote each raw contact into an EntityContact.
    /// Mirrors the post-CheckForNewContacts loop in CRigidEntity::Step (rigidentity.cpp:2570-2610).
    /// Simplified: handles the common path (center or pt) — skips parea border loops for now.
    /// </summary>
    private void PromoteCurrentContacts()
    {
        for (int i = 0; i < _nLastContacts; i++)
        {
            var gc = _curColliders[i].Contact;
            if (gc == null) continue;

            // Area contact: spawn a contact per border point
            if (gc.PArea != null && gc.PArea.Npt > 0)
            {
                for (int j = 0; j < gc.PArea.Npt; j++)
                {
                    RegisterContactPoint(i, gc.PArea.Pt[j],
                        gc.PArea.PiPrim[0] != null && j < gc.PArea.PiPrim[0].Length ? gc.PArea.PiPrim[0][j] : gc.IPrim[0],
                        gc.PArea.PiFeature[0] != null && j < gc.PArea.PiFeature[0].Length ? gc.PArea.PiFeature[0][j] : gc.IFeature[0],
                        gc.PArea.PiPrim[1] != null && j < gc.PArea.PiPrim[1].Length ? gc.PArea.PiPrim[1][j] : gc.IPrim[1],
                        gc.PArea.PiFeature[1] != null && j < gc.PArea.PiFeature[1].Length ? gc.PArea.PiFeature[1][j] : gc.IFeature[1],
                        0, gc.T, gc.N);
                }
                continue;
            }

            // Single-point contact (prim-prim or center)
            RegisterContactPoint(i, gc.Pt,
                gc.IPrim[0], gc.IFeature[0],
                gc.IPrim[1], gc.IFeature[1],
                0, gc.T, gc.N);
        }
    }

    // ========================================================================
    // RegisterContacts (port of CRigidEntity::RegisterContacts)
    // ========================================================================

    /// <summary>
    /// Register collision and constraint contacts with the solver.
    /// Port of CRigidEntity::RegisterContacts.
    /// </summary>
    public int RegisterContacts(float timeInterval, int nMaxPlaneContacts, ContactSolver solver)
    {
        Body.Fcollision = PhysVector3.Zero;
        Body.Tcollision = PhysVector3.Zero;

        if (_nPrevColliders != _colliders.Count)
            _restMask = 0;
        _nPrevColliders = _colliders.Count;

        // Update constraint world-space data
        UpdateConstraints(timeInterval);

        // Register collision contacts
        for (int i = 0; i < _colliders.Count; i++)
        {
            var contact = _colliderContacts[i];
            while (contact != null)
            {
                solver.RegisterContact(contact);
                contact = contact.Next;
            }

            // Register active constraints for this collider
            ulong cmask = _colliderConstraints[i];
            for (int j = 0; j < NMASKBITS && GetMask(j) <= cmask; j++)
            {
                if ((cmask & GetMask(j)) != 0 && j < _constraintInfos.Count &&
                    _constraintInfos[j].BActive != 0)
                {
                    solver.RegisterContact(_constraints[j]);
                }
            }
        }

        if (SubmergedFraction > 0 || _disablePreCG)
            solver.DisablePreCG();

        return 1;
    }

    /// <summary>
    /// Count contacts for solver allocation.
    /// Port of CRigidEntity::GetContactCount.
    /// </summary>
    public int GetContactCount(int nMaxPlaneContacts)
    {
        int nTotContacts = 0;
        for (int i = 0; i < _colliders.Count; i++)
        {
            var contact = _colliderContacts[i];
            int nContacts = 0;
            while (contact != null)
            {
                nContacts++;
                contact = contact.Next;
            }
            nTotContacts += System.Math.Min(nContacts, nMaxPlaneContacts);
        }
        return nTotContacts;
    }

    // ========================================================================
    // StartStep (port of CRigidEntity::StartStep)
    // ========================================================================

    /// <summary>
    /// Begin a new simulation step. Clear accumulators and set the full time step.
    /// Port of CRigidEntity::StartStep.
    /// </summary>
    public void StartStep(float timeInterval)
    {
        TimeStepPerformed = 0;
        TimeStepFull = timeInterval;
    }

    // ========================================================================
    // GetMaxTimeStep (port of CRigidEntity::GetMaxTimeStep)
    // ========================================================================

    /// <summary>
    /// Compute the maximum safe substep based on velocity vs. geometry size.
    /// Port of CRigidEntity::GetMaxTimeStep.
    /// </summary>
    public float GetMaxTimeStep(float timeInterval)
    {
        if (TimeStepPerformed > TimeStepFull - 0.001f)
            return timeInterval;

        // Find the largest part bounding box
        float bestVol = 0;
        int iBest = -1;
        for (int i = 0; i < Parts.Count; i++)
        {
            if (Parts[i].PhysGeom?.Geometry == null) continue;
            var b = new Primitives.Box();
            Parts[i].PhysGeom.Geometry.GetBBox(ref b);
            float vol = b.Size.X * b.Size.Y * b.Size.Z;
            if (vol > bestVol) { bestVol = vol; iBest = i; }
        }
        if (iBest < 0)
            return timeInterval;

        var partBox = new Primitives.Box();
        Parts[iBest].PhysGeom!.Geometry.GetBBox(ref partBox);
        var size = partBox.Size * Parts[iBest].Scale;

        // Project velocity onto bbox basis to find the fastest axis
        // C++: vloc = bbox.Basis*(m_body.v*(m_qrot*m_parts[iBest].q))
        // v*R in C++ means R^T * v, so: vloc = Basis * (R^T * v)
        var partR = new PhysMatrix33(Orientation * Parts[iBest].Rotation);
        var vloc = partBox.Basis * (partR.Transposed() * Body.V);
        var vsz = new PhysVector3(
            MathF.Abs(vloc.X) * size.Y * size.Z,
            MathF.Abs(vloc.Y) * size.X * size.Z,
            MathF.Abs(vloc.Z) * size.X * size.Y);

        int iMax = 0;
        if (vsz.Y > vsz[iMax]) iMax = 1;
        if (vsz.Z > vsz[iMax]) iMax = 2;

        _velFastDir = MathF.Abs(vloc[iMax]);
        _sizeFastDir = size[iMax];

        // Limit step so entity doesn't move more than 70% of its size per step
        if (timeInterval * _velFastDir > _sizeFastDir * 0.7f)
            timeInterval = MathF.Max(0.005f, _sizeFastDir * 1.7f / MathF.Max(1e-10f, _velFastDir));

        return MathF.Min(MathF.Min(TimeStepFull - TimeStepPerformed, MaxTimeStep), timeInterval);
    }

    // ========================================================================
    // Step (main simulation step - port of CRigidEntity::Step, simplified)
    // ========================================================================

    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (!IsAwake) return 0;

        // Save pre-step state for potential StepBack
        _prevPos = Body.Pos;
        _prevQ = Body.Q;
        _prevV = Body.V;
        _prevW = Body.W;
        _vSleep = PhysVector3.Zero;
        _wSleep = PhysVector3.Zero;
        _stableContact = null;

        DampingEx = 0;
        if (TimeStepPerformed > TimeStepFull - 0.001f || Parts.Count == 0)
            return 1;

        TimeStepPerformed += timeInterval;
        LastTimeStep = timeInterval;
        if (NextTimeStep > 1e-8f)
        {
            timeInterval = NextTimeStep;
            NextTimeStep = 0;
        }
        _minFriction = 0.1f;

        // Apply external impulses
        Body.P = Body.P + _pExt;
        _pExt = PhysVector3.Zero;
        Body.L = Body.L + _lExt;
        _lExt = PhysVector3.Zero;

        // Integrate body forward
        _pSoft = PhysVector3.Zero;
        _lSoft = PhysVector3.Zero;
        Body.Step(timeInterval);
        Body.Pos = Body.Pos + _forcedMove;
        _forcedMove = PhysVector3.Zero;
        CapBodyVel();

        _qNew = Body.Q * Body.Qfb;
        _posNew = Body.Pos - _qNew.Rotate(Body.Offsfb);

        // Check for new contacts (simplified)
        int itmax;
        int nContacts = CheckForNewContacts(out itmax);

        // Promote raw geom_contacts into EntityContacts linked in _colliderContacts[i].
        // Port of the post-CheckForNewContacts loop that calls RegisterContactPoint in
        // rigidentity.cpp:2500-2610. Without this, RegisterContacts iterates an empty list.
        PromoteCurrentContacts();

        // Apply gravity
        var gravity = _colliders.Count > 0 ? Gravity : GravityFreefall;
        Body.P = Body.P + gravity * (Body.M * timeInterval);
        Body.P = Body.P + _pSoft * timeInterval;
        Body.L = Body.L + _lSoft * timeInterval;
        Body.UpdateState();

        // Clear collision accumulators for solver
        Body.Fcollision = PhysVector3.Zero;
        Body.Tcollision = PhysVector3.Zero;
        Body.UpdateState();
        CapBodyVel();
        _prevV = Body.V;
        _prevW = Body.W;

        // Sync entity position from rigid body
        Position = _posNew;
        Orientation = _qNew;
        ComputeBBox();

        _steppedBack = false;
        return 1;
    }

    // ========================================================================
    // StepBack (port of CRigidEntity::StepBack)
    // ========================================================================

    /// <summary>
    /// Revert to pre-step state on solver failure.
    /// Port of CRigidEntity::StepBack.
    /// </summary>
    public void StepBack(float timeInterval)
    {
        if (timeInterval > 0)
        {
            Body.Pos = _prevPos;
            Body.Q = _prevQ;

            var R = new PhysMatrix33(Body.Q);
            Body.Iinv = R * (PhysMatrix33)Body.IbodyInv * R.Transposed();
            _qNew = Body.Q * Body.Qfb;
            _posNew = Body.Pos - _qNew.Rotate(Body.Offsfb);
            _steppedBack = true;

            Position = _posNew;
            Orientation = _qNew;
            ComputeBBox();
        }
        else
        {
            NextTimeStep = 0.001f;
        }

        Body.V = _prevV;
        Body.W = _prevW;
        Body.P = Body.V * Body.M;
        Body.L = Body.Q.Rotate(Body.Ibody * Body.Q.InverseRotate(Body.W));
    }

    // ========================================================================
    // Update (port of CRigidEntity::Update)
    // ========================================================================

    /// <summary>
    /// Post-solve update: apply damping, check sleep conditions.
    /// Port of CRigidEntity::Update.
    /// </summary>
    public int Update(float timeInterval, float damping)
    {
        _stepBackCount = (_steppedBack ? _stepBackCount + 1 : 0);
        CapBodyVel();

        // Apply damping to velocities and momenta
        Body.V = Body.V * damping;
        Body.W = Body.W * damping;
        Body.P = Body.P * damping;
        Body.L = Body.L * damping;

        // If the solver moved body position, update entity transforms
        if (Body.Eunproj > 0)
        {
            _qNew = Body.Q * Body.Qfb;
            _posNew = Body.Pos - Orientation.Rotate(Body.Offsfb);
            Position = _posNew;
            Orientation = _qNew;
            ComputeBBox();
        }

        // Remove broken constraints
        ulong cMask = 0;
        for (int i = 0; i < _colliders.Count; i++)
            cMask |= _colliderConstraints[i];
        _constraintMask = cMask;

        for (int i = NMASKBITS - 1; i >= 0; i--)
        {
            if ((_constraintMask & GetMask(i)) != 0 && i < _constraintInfos.Count &&
                (_constraintInfos[i].Flags & (uint)ConstraintInfoFlags.Broken) != 0)
            {
                RemoveConstraint(i);
            }
        }

        // Sleep detection
        float Emin = _bFloating && _colliders.Count + _nPrevColliders == 0
            ? MinEnergyWater : MinEnergy;

        // Check if lying on an awake rigid body - reduce sleep threshold
        int lyingOnAwake = 0;
        var pContact = _contactStart;
        int jCount = 0;
        while (pContact != null && jCount < 16 * (1 - lyingOnAwake))
        {
            float nDotG = pContact.N.Dot(Gravity);
            float g2 = Gravity.LengthSq();
            if (nDotG * nDotG + g2 * 0.5f < 0) // lies on top
            {
                // Check if the entity below is an awake rigid
                // (simplified check)
            }
            pContact = pContact.Next;
            jCount++;
        }

        MinAwakeTime = MathF.Max(MinAwakeTime, 0) - timeInterval;

        if (Body.Minv > 0)
        {
            float E = (Body.V.LengthSq() + Body.L.Dot(Body.W) * Body.Minv) * 0.5f + Body.Eunproj;
            TimeIdle = MathF.Max(0f, TimeIdle);

            if (_awakeState != 0)
            {
                if (E < Emin && (_colliders.Count + _nPrevColliders > 0 || _bFloating || Gravity.LengthSq() == 0)
                    && MinAwakeTime <= 0)
                {
                    _awakeState = 0;
                }
                else
                {
                    _sleepFrames = 0;
                }
                _vAccum = PhysVector3.Zero;
                _wAccum = PhysVector3.Zero;
            }
            else
            {
                _vAccum = _vAccum + Body.V;
                _wAccum = _wAccum + Body.W;
                var Laccum = Body.Q.Rotate(Body.Ibody * Body.Q.InverseRotate(_wAccum));
                float Eaccum = (_vAccum.LengthSq() + _wAccum.Dot(Laccum) * Body.Minv) * 0.5f + Body.Eunproj;

                if (Eaccum > Emin)
                {
                    _awakeState = 1;
                    _sleepFrames = 0;
                    _vAccum = PhysVector3.Zero;
                    _wAccum = PhysVector3.Zero;
                    if (SimulationClass == SimClass.SleepingRigid)
                        SimulationClass = SimClass.ActiveRigid;
                }
                else
                {
                    if (!_bFloating)
                    {
                        _vSleep = Body.V;
                        _wSleep = Body.W;
                        Body.P = PhysVector3.Zero;
                        Body.L = PhysVector3.Zero;
                        Body.V = PhysVector3.Zero;
                        Body.W = PhysVector3.Zero;
                    }
                    _sleepFrames++;
                    if (_sleepFrames >= 2 && SimulationClass == SimClass.ActiveRigid)
                    {
                        SimulationClass = SimClass.SleepingRigid;
                        _sleepFrames = 0;
                        _vAccum = PhysVector3.Zero;
                        _wAccum = PhysVector3.Zero;
                    }
                }
            }

            IsAwake = _awakeState != 0;
            _restMask = (_restMask << 1) | (uint)(_awakeState == 0 ? 1 : 0);
        }

        _stable = 0;
        Body.Integrator = _colliders.Count < 1 ? 1 : 0;

        return (_awakeState == 0 ? 1 : 0) | (TimeStepFull - TimeStepPerformed < 0.001f ? 1 : 0);
    }

    // ========================================================================
    // CalcEnergy (port of CRigidEntity::CalcEnergy)
    // ========================================================================

    /// <summary>
    /// Compute total energy including contact/constraint contributions for sleep detection.
    /// Port of CRigidEntity::CalcEnergy.
    /// </summary>
    public float CalcEnergy(float timeInterval)
    {
        var v = PhysVector3.Zero;
        float Emax = Body.M * MathUtils.Sqr(50f) * 2; // maxVel = 50
        float Econstr = 0;

        if (timeInterval > 0)
        {
            // Accumulate required velocity from contacts
            var pContact = _contactStart;
            while (pContact != null)
            {
                float nDotVreq = pContact.Vreq.Dot(pContact.N);
                float nDotVrel = MathF.Max(0f, pContact.Vrel);
                float correction = MathF.Max(0f, nDotVreq - nDotVrel) - pContact.N.Dot(v);
                if (correction > 0)
                    v = v + pContact.N * correction;
                pContact = pContact.Next;
            }

            // Energy contribution from constraints
            for (int i = 0; i < NMASKBITS && GetMask(i) <= _constraintMask; i++)
            {
                if ((_constraintMask & GetMask(i)) == 0) continue;
                if (i >= _constraints.Count) break;
                var c = _constraints[i];
                if ((c.Flags & (int)ContactFlags.Constraint3Dof) == 0 &&
                    (i >= _constraintInfos.Count || (_constraintInfos[i].Flags & (uint)ConstraintInfoFlags.Rope) == 0))
                    continue;

                float e0 = c.PBody0 != null
                    ? c.PBody0.M * MathUtils.Sqr(MathF.Abs(c.N.Dot(c.PBody0.V)) + MathF.Abs(c.N.Dot(c.Vreq)))
                    : 0;
                float e1 = c.PBody1 != null
                    ? c.PBody1.M * MathUtils.Sqr(MathF.Abs(c.N.Dot(c.PBody1.V)) + MathF.Abs(c.N.Dot(c.Vreq)))
                    : 0;
                Econstr += MathF.Max(e0, e1);
            }

            float Ev = (Body.V + v).LengthSq();
            float EvAlt = Body.V.LengthSq() + v.LengthSq();
            return MathF.Min(Body.M * MathF.Max(Ev, EvAlt) + Body.L.Dot(Body.W) + Econstr, Emax);
        }

        float Ev2 = (Body.V + v).LengthSq();
        float Ev2Alt = Body.V.LengthSq() + v.LengthSq();
        return Body.M * MathF.Max(Ev2, Ev2Alt) + Body.L.Dot(Body.W);
    }

    // ========================================================================
    // GetDamping (port of CRigidEntity::GetDamping)
    // ========================================================================

    /// <summary>
    /// Compute the effective damping multiplier for this step.
    /// Port of CRigidEntity::GetDamping.
    /// </summary>
    public float GetDamping(float timeInterval)
    {
        float d = MathF.Max(_colliders.Count > 0 ? Damping : DampingFreefall, DampingEx);
        return MathF.Max(0f, 1f - d * timeInterval);
    }

    // ========================================================================
    // ApplyBuoyancy (port of CRigidEntity::ApplyBuoyancy)
    // ========================================================================

    /// <summary>
    /// Apply per-part buoyancy and medium resistance forces.
    /// Faithful port of CRigidEntity::ApplyBuoyancy from rigidentity.cpp lines 4300-4376.
    /// Iterates each geometry part with geom_floats flag, calls exact geometry
    /// CalculateBuoyancy/CalculateMediumResistance, and applies impulses with correct torque.
    /// </summary>
    public void ApplyBuoyancy(float timeInterval, in PhysVector3 gravity,
        ParamsBuoyancy[] buoyancyParams, int nBuoys)
    {
        if (KWaterDensity == 0 || Body.Minv == 0)
        {
            SubmergedFraction = 0;
            return;
        }

        _bFloating = false;
        float waterFraction = 0;
        float g = gravity.Length();

        for (int ibuoy = 0; ibuoy < nBuoys; ibuoy++)
        {
            var pb = buoyancyParams[ibuoy];
            var planeN = pb.WaterPlaneNormal ?? PhysVector3.UnitZ;
            var planeOrigin = pb.WaterPlaneOrigin ?? PhysVector3.Zero;
            float density = (pb.WaterDensity ?? 1000f) * KWaterDensity;
            float resistance = (pb.WaterResistance ?? 0f) * KWaterResistance;
            float waterDamp = pb.WaterDamping ?? 0f;

            if (resistance + density + waterDamp == 0f)
                continue;

            // Quick AABB vs water plane rejection
            var sz = (BBoxMax - BBoxMin) * 0.5f;
            var center = (BBoxMax + BBoxMin) * 0.5f;
            float r = MathF.Abs(planeN.X * sz.X) + MathF.Abs(planeN.Y * sz.Y) + MathF.Abs(planeN.Z * sz.Z);
            float dist = (center - planeOrigin).Dot(planeN);
            if (dist > r) continue;

            float Vsubmerged = 0, Vfull = 0;
            var waterFlow = pb.WaterFlow ?? PhysVector3.Zero;

            // Per-part buoyancy: iterate all parts with geom_floats flag
            for (int i = 0; i < Parts.Count; i++)
            {
                var part = Parts[i];
                if ((part.Flags & GeomPartFlags.GeomFloats) == 0)
                    continue;

                if (part.PhysGeom?.Geometry == null)
                    continue;

                var geom = part.PhysGeom.Geometry;

                // Build per-part world transform
                var partR = new PhysMatrix33(Orientation * part.Rotation);
                var partOffset = Position + Orientation.Rotate(part.Offset);
                float partScale = part.Scale;

                // Velocity relative to water flow
                var relV = Body.V - waterFlow;
                var bodyW = Body.W;
                var bodyPos = Body.Pos;

                var impulse = PhysVector3.Zero;
                var angImpulse = PhysVector3.Zero;

                // Medium resistance
                float accelThresh = 0.01f;
                float partVol = part.PhysGeom.Volume;
                float velMag = relV.Length();
                if (MathUtils.Sqr(partVol) * MathUtils.Cube(MathUtils.Sqr(partScale) * velMag * resistance * Body.Minv) > MathUtils.Cube(accelThresh))
                {
                    geom.CalculateMediumResistance(planeN, planeOrigin,
                        partR, partOffset, partScale,
                        relV, bodyW, bodyPos,
                        out var resP, out var resL);

                    impulse = resP * (resistance * timeInterval);
                    angImpulse = resL * (resistance * timeInterval);
                }

                // Buoyancy force
                float liftThresh = 0.01f;
                float V = 0;
                if (partVol * MathUtils.Cube(partScale) * density * Body.Minv > liftThresh)
                {
                    if (dist > -r)
                    {
                        // Partially submerged: use exact geometry calculation
                        V = geom.CalculateBuoyancy(planeN, planeOrigin,
                            partR, partOffset, partScale, out var buoyCenter);
                        var dP = planeN * (g * density * V * timeInterval);
                        impulse = impulse + dP;
                        angImpulse = angImpulse + ((buoyCenter - bodyPos) ^ dP);
                    }
                    else
                    {
                        // Fully submerged: use full part volume
                        V = partVol * MathUtils.Cube(partScale);
                        impulse = impulse - gravity * (density * V * timeInterval);
                    }
                }

                // Apply the combined impulse
                if (impulse.LengthSq() + angImpulse.LengthSq() > 0)
                {
                    Body.P = Body.P + impulse;
                    Body.L = Body.L + angImpulse;
                }

                Vsubmerged += V;
                Vfull += partVol;
            }

            // Update damping and floating state
            if (Vfull * Vsubmerged > 0)
            {
                float submergedFraction = Vsubmerged < Vfull ? Vsubmerged / Vfull : 1.0f;
                DampingEx = MathF.Max(DampingEx,
                    Damping * (1f - submergedFraction) + MathF.Max(WaterDamping, waterDamp) * submergedFraction);
                waterFraction = MathF.Max(waterFraction, submergedFraction);
                if (Body.M < density * KWaterDensity * Body.Vol)
                    _bFloating = true;
            }
        }

        SubmergedFraction = waterFraction;
    }

    // ========================================================================
    // Awake (port of CRigidEntity::Awake)
    // ========================================================================

    public override void Awake(bool awake = true, float minTime = 0f)
    {
        base.Awake(awake, minTime);
        if (awake)
        {
            _awakeState = 1;
            SimulationClass = SimClass.ActiveRigid;
            TimeIdle = 0;
            _sleepFrames = 0;
            if (minTime > 0)
                MinAwakeTime = minTime;
        }
        else
        {
            _awakeState = 0;
            SimulationClass = SimClass.SleepingRigid;
        }
    }

    // ========================================================================
    // RigidBody accessors (matching C++ virtual interface)
    // ========================================================================

    /// <summary>Get the rigid body. Port of CRigidEntity::GetRigidBody.</summary>
    public RigidBody GetRigidBody(int ipart = -1) => Body;

    /// <summary>Get contact matrix at a world point. Port of CRigidEntity::GetContactMatrix.</summary>
    public void GetContactMatrix(in PhysVector3 pt, int ipart, ref PhysMatrix33 K)
    {
        Body.GetContactMatrix(pt - Body.Pos, ref K);
    }

    /// <summary>Get inverse mass. Port of CRigidEntity::GetMassInv.</summary>
    public float GetMassInv() => Body.Minv;

    /// <summary>Get sleep speed deltas. Port of CRigidEntity::GetSleepSpeedChange.</summary>
    public void GetSleepSpeedChange(int ipart, out PhysVector3 v, out PhysVector3 w)
    {
        v = _vSleep;
        w = _wSleep;
    }

    /// <summary>Get maximum friction among contacts. Port of CRigidEntity::GetMaxFriction.</summary>
    public float GetMaxFriction() => _maxFriction;

    // ========================================================================
    // UpdatePenaltyContacts (port of CRigidEntity::UpdatePenaltyContacts)
    // ========================================================================

    /// <summary>
    /// Walk all collision contacts and apply spring-based penalty impulses.
    /// Port of CRigidEntity::UpdatePenaltyContacts from rigidentity.cpp.
    /// Penalty contacts are continuous spring-based contacts that complement
    /// the impulse-based PGS solver, resolving shallow penetrations each frame.
    /// </summary>
    public void UpdatePenaltyContacts(float timeInterval)
    {
        _stable = System.Math.Max(_stable, _nContacts >= 2 ? 2 : 0);

        var pContact = _contactStart;
        while (pContact != null)
        {
            var next = pContact.Next;
            UpdatePenaltyContact(pContact, timeInterval);

            // Propagate stability to colliders using the simple solver
            if (_stable != 0 && pContact.PBody1 != null && pContact.PBody1.Minv > 0)
            {
                // Simplified: mark buddy as stable if it exists
            }

            pContact = next;
        }
    }

    /// <summary>
    /// Apply spring-based penalty impulse for a single contact.
    /// Port of CRigidEntity::UpdatePenaltyContact from rigidentity.cpp.
    ///
    /// Computes an impulse that resolves penetration (position error scaled by
    /// penaltyScale / dt) plus the current relative velocity at the contact point,
    /// then projects through the contact mass matrix K.  Friction is enforced by
    /// clamping the tangential component to mu * Pn.
    /// If the contact has separated beyond maxContactGap the contact is removed.
    /// </summary>
    /// <returns>1 if the contact was removed, 0 otherwise.</returns>
    private int UpdatePenaltyContact(EntityContact pContact, float timeInterval)
    {
        float maxContactGapSimple = 0.01f; // m_pWorld->m_vars.maxContactGapSimple
        float penaltyScale = 1.0f;         // m_pWorld->m_vars.penaltyScale
        float rTimeInterval = timeInterval > 0 ? 1f / timeInterval : 100f;

        // Recompute contact normal from local normal
        if (pContact.PBody0 == null || pContact.PBody1 == null)
            return 0;

        var normalBody = pContact.INormal == 0 ? pContact.PBody0 : pContact.PBody1;
        var n = normalBody.Q.Rotate(pContact.Nloc);
        pContact.N = n;

        // Penetration vector
        var dp = pContact.Pt0 - pContact.Pt1;
        float dpn = dp.Dot(n);
        float dptang2 = (dp - n * dpn).LengthSq();
        bool bRemoveContact = false;

        if (dpn < maxContactGapSimple)
        {
            // Scale penetration by penalty stiffness
            dp = dp * (rTimeInterval * penaltyScale);

            // Add relative velocity at contact
            var r0 = pContact.Pt0 - pContact.PBody0.Pos;
            var r1 = pContact.Pt1 - pContact.PBody1.Pos;
            var vrel = pContact.PBody0.V + pContact.PBody0.W.Cross(r0)
                     - pContact.PBody1.V - pContact.PBody1.W.Cross(r1);
            dp = dp + vrel;

            // Build contact mass matrix K
            var K = PhysMatrix33.Zero;
            for (int j = 0; j < 2; j++)
            {
                var body = j == 0 ? pContact.PBody0 : pContact.PBody1;
                var r = (j == 0 ? pContact.Pt0 : pContact.Pt1) - body.Pos;
                // K += Minv*I + r_cross * Iinv * r_cross
                body.GetContactMatrix(r, ref K);
            }

            // Compute impulse: dP = dp * (-|dp|^2 / (dp . K . dp))
            float dpKdp = dp.Dot(K * dp);
            PhysVector3 dP;
            if (MathF.Abs(dpKdp) > 1e-20f)
                dP = dp * (-dp.LengthSq() / dpKdp);
            else
                dP = PhysVector3.Zero;

            float dPn = dP.Dot(n);
            float dPtang2 = (dP - n * dPn).LengthSq();

            // Friction enforcement: clamp tangential impulse to mu * normal impulse
            if (dPtang2 > MathUtils.Sqr(MathF.Max(0f, dPn) * pContact.Friction))
            {
                float tangMag = MathF.Max(0f, dPn) * pContact.Friction;
                var tangDir = dP - n * dPn;
                float tangLen = tangDir.Length();
                if (tangLen > 1e-10f)
                    dP = tangDir * (tangMag / tangLen) + n * dPn;
                bRemoveContact = dptang2 > maxContactGapSimple * maxContactGapSimple;
            }

            // Only apply if impulse pushes bodies apart (dP . n > threshold)
            float prevPn = pContact.Vreq.Dot(n);
            if (dP.Dot(n) > MathF.Min(0f, prevPn * -2.1f))
            {
                pContact.Vreq = dP;
                // Apply impulse to body 0 (positive) and body 1 (negative)
                for (int j = 0; j < 2; j++)
                {
                    var body = j == 0 ? pContact.PBody0 : pContact.PBody1;
                    var r = (j == 0 ? pContact.Pt0 : pContact.Pt1) - body.Pos;
                    float sign = j == 0 ? 1f : -1f;
                    body.P = body.P + dP * sign;
                    body.L = body.L + r.Cross(dP * sign);
                    body.V = body.P * body.Minv;
                    body.W = body.Iinv * body.L;
                }
            }
            else
            {
                pContact.Vreq = PhysVector3.Zero;
            }
        }
        else
        {
            bRemoveContact = true;
        }

        if (bRemoveContact)
        {
            DetachContact(pContact);
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Detach a contact from the linked list of contacts.
    /// Port of CRigidEntity::DetachContact helper.
    /// </summary>
    private void DetachContact(EntityContact pContact)
    {
        if (pContact.Prev != null)
            pContact.Prev.Next = pContact.Next;
        else if (_contactStart == pContact)
            _contactStart = pContact.Next;

        if (pContact.Next != null)
            pContact.Next.Prev = pContact.Prev;

        pContact.Next = null;
        pContact.Prev = null;
        _nContacts--;
    }

    // ========================================================================
    // VerifyExistingContacts (port of CRigidEntity::VerifyExistingContacts)
    // ========================================================================

    /// <summary>
    /// Check if contacts from the previous frame are still valid.
    /// Port of CRigidEntity::VerifyExistingContacts from rigidentity.cpp.
    ///
    /// Walks every collider's contact list and marks non-new contacts
    /// for removal if the two bodies have separated (contact normal has
    /// flipped, or the gap exceeds maxdist).  Contacts flagged
    /// contact_new are left alone because they were just detected this frame.
    ///
    /// After marking, a second pass actually detaches the flagged contacts.
    /// This avoids redundant narrow-phase re-detection for contacts that
    /// are still in persistent overlap.
    /// </summary>
    public void VerifyExistingContacts(float maxdist)
    {
        float maxdist2 = maxdist * maxdist;
        int nRemoved = 0;

        // Pass 1: check each collider's contact chain and flag invalid ones
        for (int i = 0; i < _colliders.Count; i++)
        {
            var pContact = _colliderContacts[i];
            if (pContact == null) continue;

            var current = pContact;
            while (current != null)
            {
                bool isLast = (current.Flags & (int)ContactFlags.Last) != 0;

                if ((current.Flags & (int)ContactFlags.New) == 0)
                {
                    bool bConfirmed = false;

                    // Recompute the contact normal from stored local normal
                    if (current.PBody0 != null && current.PBody1 != null)
                    {
                        var normalBody = current.INormal == 0 ? current.PBody0 : current.PBody1;
                        var n = normalBody.Q.Rotate(current.Nloc);

                        // Check normal consistency: if the recomputed normal
                        // matches the stored normal closely, the contact is plausible.
                        if (current.N.Dot(n) > 0.98f)
                        {
                            // Check separation distance
                            float gap2 = (current.Pt0 - current.Pt1).LengthSq();
                            if (gap2 < maxdist2)
                                bConfirmed = true;
                        }
                    }

                    if (!bConfirmed)
                    {
                        current.Flags |= (int)ContactFlags.Remove;
                        nRemoved++;
                    }
                }

                // Clear per-frame flags
                current.Flags &= ~((int)ContactFlags.New |
                                    (int)ContactFlags.Verified2b |
                                    (int)ContactFlags.Archived);

                if (isLast) break;
                current = current.Next;
            }
        }

        // Pass 2: walk the contact linked list and detach flagged contacts
        if (nRemoved > 0)
        {
            var pContact = _contactStart;
            while (pContact != null)
            {
                var next = pContact.Next;
                if ((pContact.Flags & (int)ContactFlags.Remove) != 0)
                {
                    DetachContact(pContact);
                }
                pContact = next;
            }
        }
    }

    // ========================================================================
    // Internal helpers
    // ========================================================================

    /// <summary>
    /// Cap body velocity to prevent numerical explosions.
    /// Port of CRigidEntity::CapBodyVel.
    /// </summary>
    private void CapBodyVel()
    {
        float maxVel = 100f; // C++ uses m_pWorld->m_vars.maxVel
        if (Body.V.LengthSq() > maxVel * maxVel)
        {
            float s = maxVel / Body.V.Length();
            Body.V = Body.V * s;
            Body.P = Body.V * Body.M;
        }
        if (Body.W.LengthSq() > MaxAngVel * MaxAngVel)
        {
            float s = MaxAngVel / Body.W.Length();
            Body.W = Body.W * s;
            Body.L = Body.Q.Rotate(Body.Ibody * Body.Q.InverseRotate(Body.W));
        }
    }

    /// <summary>
    /// Transform a world point to a part's local coordinates.
    /// Port of Glob2Loc helper from rigidentity.h.
    /// </summary>
    private static PhysVector3 Glob2Loc(in PhysVector3 pt, int ipart, PhysicalEntity ent)
    {
        if (ipart < 0 || ipart >= ent.Parts.Count)
            return pt;
        var part = ent.Parts[ipart];
        return part.Rotation.InverseRotate(
            ent.Orientation.InverseRotate(pt - ent.Position) - part.Offset) * (1f / part.Scale);
    }

    /// <summary>
    /// Transform a part-local point to world coordinates.
    /// Port of Loc2Glob helper from rigidentity.h.
    /// </summary>
    private static PhysVector3 Loc2Glob(in PhysVector3 ptloc, int ipart, PhysicalEntity ent)
    {
        if (ipart < 0 || ipart >= ent.Parts.Count)
            return ptloc;
        var part = ent.Parts[ipart];
        return ent.Position + ent.Orientation.Rotate(
            part.Rotation.Rotate(ptloc) * part.Scale + part.Offset);
    }

    /// <summary>AABB overlap test (port of AABB_overlap macro).</summary>
    private static bool AABBOverlap(in PhysVector3 min0, in PhysVector3 max0,
        in PhysVector3 min1, in PhysVector3 max1)
    {
        return min0.X <= max1.X && max0.X >= min1.X &&
               min0.Y <= max1.Y && max0.Y >= min1.Y &&
               min0.Z <= max1.Z && max0.Z >= min1.Z;
    }

    /// <summary>
    /// Recompute mass distribution from parts.
    /// Port of CRigidEntity::RecomputeMassDistribution (simplified).
    /// </summary>
    private void RecomputeMassProperties()
    {
        float totalMass = 0;
        var centerOfMass = PhysVector3.Zero;

        foreach (var part in Parts)
        {
            totalMass += part.Mass;
            var partCenter = part.Offset + part.Rotation.Rotate(part.PhysGeom?.Geometry?.GetCenter() ?? PhysVector3.Zero) * part.Scale;
            centerOfMass = centerOfMass + partCenter * part.Mass;
        }

        if (totalMass > 1e-10f)
        {
            centerOfMass = centerOfMass / totalMass;
            Body.M = totalMass;
            Body.Minv = 1f / totalMass;

            var I = PhysMatrix33.Zero;
            foreach (var part in Parts)
            {
                if (part.PhysGeom?.Geometry == null) continue;
                var props = part.PhysGeom.Geometry.CalcPhysicalProperties();

                var Rpart = new PhysMatrix33(part.Rotation);
                float scale3 = part.Scale * part.Scale * part.Scale;
                var Ipart = Rpart * (props.InertiaTensor * scale3) * Rpart.Transposed();

                float density = props.Volume > 1e-20f ? part.Mass / props.Volume : 0f;
                Ipart = Ipart * (density / (scale3 > 1e-20f ? scale3 : 1f)) * scale3;

                var partCenter = part.Offset + part.Rotation.Rotate(props.CenterOfMass) * part.Scale;
                var d = partCenter - centerOfMass;
                MathUtils.OffsetInertiaTensor(ref Ipart, d, part.Mass);

                I = I + Ipart;
            }

            // Diagonalize inertia via Jacobi
            using var eigenMtx = new MatrixNM(3, 3, MatrixFlags.Symmetric);
            eigenMtx[0, 0] = I.M00; eigenMtx[0, 1] = I.M01; eigenMtx[0, 2] = I.M02;
            eigenMtx[1, 0] = I.M10; eigenMtx[1, 1] = I.M11; eigenMtx[1, 2] = I.M12;
            eigenMtx[2, 0] = I.M20; eigenMtx[2, 1] = I.M21; eigenMtx[2, 2] = I.M22;

            using var evec = new MatrixNM(3, 3);
            float[] eval = new float[3];
            eigenMtx.JacobiTransformation(evec, eval);

            Body.Ibody = new Diag33(eval[0], eval[1], eval[2]);
            Body.IbodyInv = Body.Ibody.Inverted();
        }
    }
}
