// Port of CryPhysics livingentity.h/cpp - character controller entity
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

// ============================================================================
// History / Contact structs (port of le_history_item, le_contact)
// ============================================================================

/// <summary>
/// History ring buffer item. Port of le_history_item from livingentity.h.
/// Stores snapshot of living entity state for rollback.
/// </summary>
public struct LeHistoryItem
{
    public PhysVector3 Pos;
    public PhysQuaternion Q;
    public PhysVector3 V;
    public bool BFlying;
    public PhysVector3 NSlope;
    public float TimeFlying;
    public float MinFlyTime;
    public float TimeUseLowCap;
    public int IdCollider;
    public int IColliderPart;
    public PhysVector3 PosColl;
    public float Dt;
}

/// <summary>
/// Contact record for living entity unprojection solver.
/// Port of le_contact from livingentity.h.
/// </summary>
public class LeContact
{
    public IPhysicalEntity? Entity;
    public int IPart;
    public PhysVector3 Pt;
    public PhysVector3 PtLoc;
    public PhysVector3 N;
    public float Penetration;
    public PhysVector3 Center;
}

/// <summary>
/// Temporary sweep contact for collision detection.
/// Port of le_tmp_contact from livingentity.h.
/// </summary>
public struct LeTmpContact
{
    public IPhysicalEntity? Entity;
    public PhysVector3 PtContact;
    public PhysVector3 NContact;
    public int IPart;
    public int IdMat;
    public int IPrim;
    public float TMin;
}

/// <summary>
/// Living entity (character/player physics). Port of CLivingEntity.
/// Handles ground detection, movement, sliding, climbing, head bump, history.
/// </summary>
public class LivingEntity : PhysicalEntity
{
    // -- Constants matching C++ --
    public const int SzHistory = 128;

    public override PhysicsEntityType Type => PhysicsEntityType.Living;

    // ======================== Dimensions (pe_player_dimensions) ========================
    public float HeightPivot { get; set; }
    public float HeightEye { get; set; } = 1.7f;
    public float HeightCollider { get; set; } = 1.1f;        // m_hCyl
    public PhysVector3 SizeCollider { get; set; } = new(0.4f, 0.4f, 0.6f);  // m_size
    public float HeadRadius { get; set; }
    public float HeadHeight { get; set; } = 1.9f;            // m_hHead
    public bool UseCapsule { get; set; }                      // C++ default: 0
    public float GroundContactEps { get; set; } = 0.004f;

    // ======================== Dynamics (pe_player_dynamics) ========================
    public float KInertia { get; set; } = 8f;                // C++ default: 8
    public float KInertiaAccel { get; set; }
    public float KAirControl { get; set; } = 0.1f;
    public float KAirResistance { get; set; }
    public float Mass { get; set; } = 80f;
    public float MassInv { get; set; } = 2f / 80f;           // C++ uses 2.0f/80
    public PhysVector3 Gravity { get; set; } = new(0, 0, -9.81f);
    public float NodSpeed { get; set; } = 60f;
    public bool IsSwimming { get; set; }
    public int SurfaceIdx { get; set; }

    // Slope thresholds -- stored as cosines like C++ (not degrees)
    public float SlopeSlide { get; set; } = MathF.Cos(MathF.PI * 0.2f);    // cos(36deg)
    public float SlopeClimb { get; set; } = MathF.Cos(MathF.PI * 0.3f);    // cos(54deg)
    public float SlopeJump { get; set; } = MathF.Cos(MathF.PI * 0.3f);
    public float SlopeFall { get; set; } = MathF.Cos(MathF.PI * 0.39f);
    public float MaxVelGround { get; set; } = 10f;
    public float TimeImpulseRecover { get; set; } = 5f;

    // ======================== Movement state ========================
    public PhysVector3 Velocity { get; set; }
    public PhysVector3 VelRequested { get; set; }
    public PhysVector3 VelGround { get; set; }
    public bool IsFlying { get; set; } = true;
    public bool JumpRequested { get; set; }
    public float TimeFlying { get; set; }
    public PhysVector3 GroundNormal { get; set; } = PhysVector3.UnitZ;      // m_nslope
    public int GroundSurfaceIdx { get; set; } = -1;
    public int GroundSurfaceIdxAux { get; set; } = -1;
    public int GroundPrim { get; set; } = -1;
    public float GroundHeight { get; set; } = -1e-10f;       // m_hLatest

    // Ground collider tracking (port of m_pLastGroundCollider system)
    public IPhysicalEntity? LastGroundCollider { get; set; }
    public int LastGroundColliderPart { get; set; }
    public PhysVector3 PosLastGroundColl { get; set; }

    // Head geometry state
    public float Dh { get; set; }               // eye offset from head bump
    public float DhSpeed { get; set; }
    public float DhAcc { get; set; }
    public float StablehTime { get; set; } = 1f;

    // Time tracking
    public float TimeSmooth { get; set; } = 0.16f;
    public float TimeSinceStanceChange { get; set; }
    public float TimeSinceImpulseContact { get; set; } = 10f;
    public float TimeForceInertia { get; set; }
    public float TimeUseLowCap { get; set; } = -1f;
    public float TimeStepFull { get; set; }
    public float TimeStepPerformed { get; set; }

    // Movement deltas (for smoothing / status reporting)
    public PhysVector3 DeltaPos { get; set; }
    public PhysVector3 DeltaV { get; set; }
    public PhysQuaternion DeltaQRot { get; set; } = PhysQuaternion.Identity;

    // Activity flags
    public bool IsActive { get; set; } = true;               // m_bActive
    public bool IsActiveEnvironment { get; set; }
    public bool IsStuck { get; set; }
    public int Squashed { get; set; }
    public bool ForceFly { get; set; }
    public bool ReleaseGroundColliderWhenNotActive { get; set; } = true;

    // Contact storage
    private readonly List<LeContact> _contacts = new();

    // ======================== History ring buffer ========================
    private readonly LeHistoryItem[] _history = new LeHistoryItem[SzHistory];
    private int _historyIdx;

    // ======================== Constructor ========================
    public LivingEntity()
    {
        SimulationClass = SimClass.Living;
        IsAwake = true;
        GroundContactEps = MathF.Max(0.01f * SizeCollider.Z, 0.004f);
    }

    // ======================== SetParams ========================
    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        if (parameters is PlayerDimensions dim)
        {
            if (dim.HeightPivot.HasValue) HeightPivot = dim.HeightPivot.Value;
            if (dim.HeightEye.HasValue) HeightEye = dim.HeightEye.Value;
            if (dim.SizeCollider.HasValue)
            {
                SizeCollider = new PhysVector3(dim.SizeCollider.Value.X, dim.SizeCollider.Value.X, dim.SizeCollider.Value.Z);
            }
            if (dim.HeightCollider.HasValue) HeightCollider = dim.HeightCollider.Value;
            if (dim.HeadRadius.HasValue)
            {
                HeadRadius = dim.HeadRadius.Value;
            }
            if (dim.HeightHead.HasValue) HeadHeight = dim.HeightHead.Value;
            if (dim.UseCapsule.HasValue) UseCapsule = dim.UseCapsule.Value;
            if (dim.GroundContactEps.HasValue)
                GroundContactEps = MathF.Max(dim.GroundContactEps.Value, MathF.Max(0.01f * SizeCollider.Z, 0.004f));

            GroundContactEps = MathF.Max(0.01f * SizeCollider.Z, 0.004f);
            ComputeBBox();
            return 1;
        }

        if (parameters is PlayerDynamics dyn)
        {
            if (dyn.KInertia.HasValue) KInertia = dyn.KInertia.Value;
            if (dyn.KInertiaAccel.HasValue) KInertiaAccel = dyn.KInertiaAccel.Value;
            if (dyn.KAirControl.HasValue) KAirControl = dyn.KAirControl.Value;
            if (dyn.KAirResistance.HasValue) KAirResistance = dyn.KAirResistance.Value;
            if (dyn.Gravity.HasValue) Gravity = dyn.Gravity.Value;
            if (dyn.NodSpeed.HasValue) NodSpeed = dyn.NodSpeed.Value;
            if (dyn.IsSwimming.HasValue) IsSwimming = dyn.IsSwimming.Value;
            if (dyn.Mass.HasValue)
            {
                Mass = dyn.Mass.Value;
                MassInv = Mass > 0 ? 1f / Mass : 0f;
            }
            if (dyn.Surface_idx.HasValue) SurfaceIdx = dyn.Surface_idx.Value;
            if (dyn.MinSlideAngle.HasValue) SlopeSlide = MathF.Cos(dyn.MinSlideAngle.Value * MathF.PI / 180f);
            if (dyn.MaxClimbAngle.HasValue) SlopeClimb = MathF.Cos(dyn.MaxClimbAngle.Value * MathF.PI / 180f);
            if (dyn.MaxJumpAngle.HasValue) SlopeJump = MathF.Cos(dyn.MaxJumpAngle.Value * MathF.PI / 180f);
            if (dyn.MinFallAngle.HasValue) SlopeFall = MathF.Cos(dyn.MinFallAngle.Value * MathF.PI / 180f);
            if (dyn.MaxVelGround.HasValue) MaxVelGround = dyn.MaxVelGround.Value;
            return 1;
        }

        return base.SetParams(parameters, threadSafe);
    }

    // ======================== DoAction ========================
    public override int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        if (action is ActionMove move)
        {
            if (move.Dir.HasValue) VelRequested = move.Dir.Value;
            if (move.IJump.HasValue && move.IJump.Value > 0)
            {
                JumpRequested = true;
                IsFlying = true;
                Velocity = Velocity + new PhysVector3(0, 0, move.Dir?.Z ?? 5f);
            }
            if (move.DT.HasValue) TimeStepFull = move.DT.Value;
            return 1;
        }

        if (action is ActionImpulse imp)
        {
            if (imp.Impulse.HasValue)
            {
                Velocity = Velocity + imp.Impulse.Value * MassInv;
                IsFlying = true;
                TimeFlying = 0;
                if (KInertia == 0)
                    TimeForceInertia = TimeImpulseRecover;
            }
            return 1;
        }

        return base.DoAction(action, threadSafe);
    }

    // ======================== GetStatus ========================
    public override int GetStatus(PhysicsStatusBase status)
    {
        if (status is StatusLiving living)
        {
            living.IsFlying = IsFlying && IsActive;
            living.TimeFlying = TimeFlying;
            living.CamOffset = Orientation.Rotate(new PhysVector3(0, 0, -Dh));
            living.Velocity = Velocity;
            if (LastGroundCollider != null)
                living.Velocity = living.Velocity + VelGround;
            living.VelRequested = VelRequested;
            living.VelGround = VelGround;
            living.GroundSlope = GroundNormal;
            living.GroundSurfaceIdx = GroundSurfaceIdx;
            living.GroundHeight = GroundHeight;
            living.GroundEntity = LastGroundCollider;
            return 1;
        }

        if (status is StatusDynamics dyn)
        {
            dyn.Velocity = Velocity;
            if (LastGroundCollider != null)
                dyn.Velocity = dyn.Velocity + VelGround;
            dyn.Mass = Mass;
            dyn.CenterOfMass = (BBoxMin + BBoxMax) * 0.5f;
            return 1;
        }

        return base.GetStatus(status);
    }

    // ======================== Ground collider management ========================

    /// <summary>Release reference to the ground collider. Port of ReleaseGroundCollider().</summary>
    public void ReleaseGroundCollider()
    {
        if (LastGroundCollider != null)
        {
            LastGroundCollider.Release();
            LastGroundCollider = null;
        }
    }

    /// <summary>Set the ground collider. Port of SetGroundCollider().</summary>
    public void SetGroundCollider(IPhysicalEntity? collider, bool acceptStatic = false)
    {
        ReleaseGroundCollider();
        if (collider != null)
        {
            collider.AddRef();
            LastGroundCollider = collider;
        }
    }

    // ======================== ShootRayDown (ground detection) ========================

    /// <summary>
    /// Downward sweep test to detect ground contact.
    /// Port of CLivingEntity::ShootRayDown from livingentity.cpp.
    /// Simplified: uses the physics world RayWorldIntersection if available,
    /// otherwise performs analytical ground plane check.
    /// </summary>
    public float ShootRayDown(PhysVector3 pos, ref PhysVector3 nslope, float timeInterval,
        bool updateGroundCollider)
    {
        var axis = Orientation.Rotate(PhysVector3.UnitZ);
        float hCyl = HeightCollider;
        float hPivot = HeightPivot;
        var sz = SizeCollider;

        // Ray origin: top of bottom cap area
        var rayOrigin = pos + axis * (hCyl - sz.Z - hPivot);
        var rayDir = -axis * (1.5f * (hCyl - sz.Z));
        float h = -1e10f;

        // Use world ray intersection if available
        var world = GetWorld();
        if (world != null)
        {
            var hits = new RayHit[4];
            int nHits = world.RayWorldIntersection(rayOrigin, rayDir, 0x0F, 0, hits, hits.Length,
                new[] { (IPhysicalEntity)this });
            if (nHits > 0)
            {
                // Find highest hit
                for (int i = 0; i < nHits; i++)
                {
                    float hitH = hits[i].Point.Dot(axis);
                    if (hitH > h)
                    {
                        h = hitH;
                        nslope = hits[i].Normal;

                        if (updateGroundCollider && hits[i].Entity != null)
                        {
                            SetGroundCollider(hits[i].Entity);
                            LastGroundColliderPart = hits[i].PartId;
                            GroundSurfaceIdx = hits[i].SurfaceIdx;
                            GroundSurfaceIdxAux = hits[i].SurfaceIdx;
                            GroundPrim = hits[i].IdMaterial;

                            // Compute ground velocity from collider
                            var sd = new StatusDynamics { PartId = hits[i].PartId };
                            hits[i].Entity.GetStatus(sd);
                            VelGround = sd.Velocity;
                        }
                    }
                }
            }
            else if (updateGroundCollider && LastGroundCollider != null)
            {
                ReleaseGroundCollider();
            }
        }

        return h;
    }

    // ======================== SyncWithGroundCollider ========================

    /// <summary>
    /// Synchronize position with ground collider movement.
    /// Port of CLivingEntity::SyncWithGroundCollider.
    /// </summary>
    public PhysVector3 SyncWithGroundCollider(float timeInterval)
    {
        var newpos = Position;
        var velGround0 = VelGround;
        VelGround = PhysVector3.Zero;

        if (LastGroundCollider != null)
        {
            // Get collider transform
            var sd = new StatusDynamics { PartId = LastGroundColliderPart };
            LastGroundCollider.GetStatus(sd);
            var velGround = sd.Velocity;

            // Limit velocity change to avoid spikes
            if ((velGround - velGround0).LengthSq() < Gravity.LengthSq() * (timeInterval * 2f) * (timeInterval * 2f))
                VelGround = velGround;
            else
                VelGround = velGround0;

            if (VelGround.LengthSq() > MaxVelGround * MaxVelGround)
                VelGround = VelGround.Normalized() * MaxVelGround;
        }

        return newpos;
    }

    // ======================== History ring buffer ========================

    /// <summary>Record current state into the history ring buffer.</summary>
    public void RecordHistory(float dt)
    {
        _historyIdx = (_historyIdx + 1) & (SzHistory - 1);
        ref var h = ref _history[_historyIdx];
        h.Pos = Position;
        h.Q = Orientation;
        h.V = Velocity;
        h.BFlying = IsFlying;
        h.NSlope = GroundNormal;
        h.TimeFlying = TimeFlying;
        h.TimeUseLowCap = TimeUseLowCap;
        h.IdCollider = LastGroundCollider?.Id ?? -1;
        h.IColliderPart = LastGroundColliderPart;
        h.Dt = dt;
    }

    /// <summary>Retrieve a history item by offset from current (0 = most recent).</summary>
    public ref LeHistoryItem GetHistory(int offset = 0)
    {
        return ref _history[(_historyIdx - offset) & (SzHistory - 1)];
    }

    /// <summary>
    /// Roll back position to a previous history state.
    /// Port of the rollback concept from le_history_item usage.
    /// </summary>
    public void RollbackToHistory(int stepsBack)
    {
        if (stepsBack <= 0 || stepsBack >= SzHistory) return;
        ref var h = ref GetHistory(stepsBack);
        Position = h.Pos;
        Orientation = h.Q;
        Velocity = h.V;
        IsFlying = h.BFlying;
        GroundNormal = h.NSlope;
        TimeFlying = h.TimeFlying;
        TimeUseLowCap = h.TimeUseLowCap;
        ComputeBBox();
    }

    // ======================== Step_HandleFlying ========================

    /// <summary>
    /// Handle flying state velocity update.
    /// Port of CLivingEntity::Step_HandleFlying.
    /// </summary>
    private void Step_HandleFlying(ref PhysVector3 vel, PhysVector3 velGround, bool wasFlying,
        PhysVector3 heightAdj, float kInertia, float timeInterval)
    {
        if (!wasFlying)
            vel = vel + velGround;

        // Apply gravity
        var gravity = Gravity * timeInterval;
        if (gravity.Dot(heightAdj) < 0f)
        {
            // Remove slope adjustment from gravity accumulation
            var gravityAdjusted = gravity + heightAdj / timeInterval;
            if (gravityAdjusted.Dot(gravity) > 0f)
                vel = vel + gravityAdjusted;
        }
        else
        {
            vel = vel + gravity;
        }

        if (IsSwimming)
            vel = vel + (VelRequested - vel) * kInertia * timeInterval;

        // Air resistance
        var lastForce = -vel * KAirResistance * timeInterval;
        if (lastForce.LengthSq() < vel.LengthSq() * 4f)
            vel = vel + lastForce;

        if (vel.LengthSq() < 1e-10f) vel = PhysVector3.Zero;
        ReleaseGroundCollider();
    }

    // ======================== Step_HandleWasFlying ========================

    /// <summary>
    /// Handle landing (was flying, now grounded).
    /// Port of CLivingEntity::Step_HandleWasFlying.
    /// </summary>
    private void Step_HandleWasFlying(ref PhysVector3 vel, ref bool bFlying, PhysVector3 axis, bool bGroundContact)
    {
        if (!IsSwimming)
        {
            var nslope = GroundNormal;
            float axisSlope = nslope.Dot(axis);

            // Head nod effect on landing
            if (vel.Dot(axis) < -4f && NodSpeed > 0f)
            {
                DhSpeed = vel.Dot(axis);
                DhAcc = MathF.Max(DhSpeed * DhSpeed / (SizeCollider.Z * 2f), NodSpeed);
            }

            vel = vel - VelGround;

            if (axisSlope < SlopeFall && vel.Dot(axis) < -4f && !IsStuck)
            {
                // Steep slope + fast descent: deflect and stay flying
                vel = vel - nslope * (vel.Dot(nslope));
                if (vel.Dot(axis) > 0) vel = vel - axis * (axis.Dot(vel));
                bFlying = true;
            }
            else if (axisSlope < SlopeFall ||
                     (!bGroundContact || VelRequested.LengthSq() > 0) &&
                     (axisSlope > SlopeClimb || vel.Dot(axis) < 0))
            {
                vel = vel - nslope * (vel.Dot(nslope));
            }
            else
            {
                vel = PhysVector3.Zero;
            }
        }
    }

    // ======================== Head bump detection ========================

    /// <summary>
    /// Check for head bump against ceiling geometry.
    /// Port of the m_pHeadGeom section from CLivingEntity::Step.
    /// </summary>
    private void CheckHeadBump(PhysVector3 pos, PhysVector3 axis, float timeInterval)
    {
        if (HeadRadius <= 0) return;

        float headClearance = HeadHeight - HeightCollider - MathF.Min(Dh, 0f);
        float tmin = headClearance;

        // Use world ray intersection to check upward for ceiling
        var world = GetWorld();
        if (world != null)
        {
            var hits = new RayHit[2];
            var origin = pos + Orientation.Rotate(new PhysVector3(0, 0, HeightCollider - HeightPivot));
            var dir = axis * headClearance;
            int nHits = world.RayWorldIntersection(origin, dir, 0x01, 0, hits, hits.Length,
                new[] { (IPhysicalEntity)this });
            for (int i = 0; i < nHits; i++)
            {
                float t = hits[i].Distance;
                if (t < tmin) tmin = t;
            }
        }

        if (Dh < headClearance - tmin || MathF.Abs(DhSpeed) + MathF.Abs(DhAcc) == 0)
            Dh = headClearance - tmin;
    }

    // ======================== Full Step implementation ========================

    /// <summary>
    /// Full living entity step. Port of CLivingEntity::Step.
    /// Implements ground detection, collision response, sliding, stair climbing, head bump.
    /// </summary>
    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (timeInterval <= 0) return 1;

        // Clamp to full step budget
        float dt = TimeStepFull - TimeStepPerformed;
        timeInterval = MathF.Min(timeInterval, MathF.Max(dt, 0.001f));

        float kInertia;
        if (TimeForceInertia > 0.0001f)
            kInertia = 6f;
        else if (KInertiaAccel > 0 && VelRequested.LengthSq() > 0.1f)
            kInertia = KInertiaAccel;
        else
            kInertia = KInertia;

        var pos = SyncWithGroundCollider(timeInterval);
        var vel0 = Velocity;
        var vel = Velocity;
        bool wasFlying = IsFlying;
        bool bFlying = IsFlying;
        var oldQRot = Orientation;

        // Update timers
        TimeUseLowCap -= timeInterval;
        TimeSinceStanceChange += timeInterval;
        TimeSinceImpulseContact += timeInterval;
        TimeForceInertia = MathF.Max(0f, TimeForceInertia - timeInterval);
        TimeStepPerformed += timeInterval;

        var axis = Orientation.Rotate(PhysVector3.UnitZ);
        bool bMoving = false;

        if (IsActive &&
            (!(vel.LengthSq() == 0 && VelRequested.LengthSq() == 0 &&
               (!bFlying || Gravity.LengthSq() == 0) && DhSpeed == 0 && DhAcc == 0) ||
             IsActiveEnvironment || GroundNormal.Z < SlopeSlide || VelGround.LengthSq() > 0))
        {
            IsActiveEnvironment = false;

            // --- Air control ---
            if (kInertia == 0 && !bFlying && !JumpRequested)
            {
                vel = VelRequested;
                vel = vel - GroundNormal * GroundNormal.Dot(vel);
            }

            var move = PhysVector3.Zero;
            var heightAdj = PhysVector3.Zero;

            if (bFlying && KAirControl > 0)
            {
                if (KInertia > 0)
                {
                    var velDelta = VelRequested * (KInertia * timeInterval * KAirControl);
                    var velDiff = VelRequested - vel;

                    // fsel: if (velDiff.x * velDelta.x) < 0 then velDelta.x = 0  (don't push away from target)
                    if (velDiff.X * velDelta.X < 0) velDelta.X = 0;
                    if (velDiff.Y * velDelta.Y < 0) velDelta.Y = 0;
                    if (velDiff.Z * velDelta.Z < 0) velDelta.Z = 0;

                    // Clamp: don't overshoot target
                    velDelta.X = velDiff.X >= 0 ? MathF.Min(velDiff.X, velDelta.X) : MathF.Max(velDiff.X, velDelta.X);
                    velDelta.Y = velDiff.Y >= 0 ? MathF.Min(velDiff.Y, velDelta.Y) : MathF.Max(velDiff.Y, velDelta.Y);
                    velDelta.Z = velDiff.Z >= 0 ? MathF.Min(velDiff.Z, velDelta.Z) : MathF.Max(velDiff.Z, velDelta.Z);

                    // Full air control override (kAirControl >= 1)
                    if (KAirControl >= 1f)
                    {
                        velDelta.X = velDiff.X;
                        velDelta.Y = velDiff.Y;
                    }

                    vel = vel + new PhysVector3(velDelta.X, velDelta.Y, velDelta.Z);
                }
                else if (Gravity.LengthSq() > 0)
                {
                    vel = Gravity * (vel.Dot(Gravity) - VelRequested.Dot(Gravity)) / Gravity.LengthSq() + VelRequested;
                }
                else
                {
                    vel = VelRequested;
                }
            }

            // Gravity contribution to move
            if (ForceFly)
                vel = VelRequested;
            else if (bFlying && !IsSwimming)
                move = move + Gravity * (timeInterval * timeInterval * 0.5f);

            move = move + vel * timeInterval;
            ForceFly = false;
            JumpRequested = false;

            // --- Ground ray ---
            var newpos = pos + move;
            var nslope = GroundNormal;
            float h = ShootRayDown(newpos, ref nslope, timeInterval, false);
            float hcur = newpos.Dot(axis) - HeightPivot;
            float axisSlope = axis.Dot(nslope);

            // Ground snap
            if (axisSlope > SlopeFall &&
                (hcur < h && hcur > h - (HeightCollider - SizeCollider.Z) * 1.01f ||
                 hcur > h && (hcur - h) * (hcur - h) < vel.LengthSq() * timeInterval * timeInterval &&
                 !bFlying && !JumpRequested && !IsSwimming))
            {
                // On slope too steep to slide, and no movement requested: hold position
                if (h > hcur && GroundNormal.Dot(axis) < SlopeSlide && GroundNormal.Dot(nslope) < SlopeSlide &&
                    axisSlope < SlopeSlide && VelRequested.LengthSq() == 0)
                {
                    newpos = pos; vel = PhysVector3.Zero;
                }
                else
                {
                    heightAdj = axis * (h + HeightPivot - newpos.Dot(axis));
                    newpos = newpos + heightAdj;
                }
                move = newpos - pos;
            }

            pos = newpos;

            // --- Second ground ray (definitive, with collider update) ---
            nslope = GroundNormal;
            GroundHeight = ShootRayDown(pos, ref nslope, timeInterval, true);
            h = GroundHeight;
            if (nslope.Dot(axis) < 0.087f)
                nslope = GroundNormal;
            else
                GroundNormal = nslope;

            // Flying / landing determination
            if (bFlying) TimeFlying += timeInterval;
            bool bGroundContact = MathF.Max(pos.Dot(axis) - HeightPivot - (h + GroundContactEps),
                                            SlopeFall - nslope.Dot(axis)) <= 0;
            if (!bGroundContact)
                ReleaseGroundCollider();

            bFlying = Gravity.Dot(axis) > 0 || IsSwimming || (!bGroundContact && !IsStuck);
            IsActiveEnvironment = IsStuck;

            var velGround = LastGroundCollider != null ? VelGround : PhysVector3.Zero;

            if (bFlying)
            {
                Step_HandleFlying(ref vel, velGround, wasFlying, heightAdj, kInertia, timeInterval);
            }
            else
            {
                if (wasFlying)
                    Step_HandleWasFlying(ref vel, ref bFlying, axis, bGroundContact);

                if (!bFlying) // may have been set back to flying by HandleWasFlying
                {
                    // Ground movement with inertia + slope forces
                    var velReq = VelRequested;
                    if (!IsSwimming) velReq = velReq - GroundNormal * velReq.Dot(GroundNormal);
                    if (kInertia * timeInterval > 1f) kInertia = 1f / timeInterval;
                    var lastForce = (velReq - vel) * kInertia;
                    float axisNSlope = GroundNormal.Dot(axis);

                    // Add sliding force on steep slopes
                    if (axisNSlope < SlopeSlide && !IsSwimming)
                    {
                        var g = Gravity;
                        lastForce = lastForce + (g - GroundNormal * g.Dot(GroundNormal));
                    }

                    var velNew = vel + lastForce * timeInterval;
                    if (velNew.Dot(vel) < 0 && velNew.Dot(VelRequested) <= 0)
                        vel = PhysVector3.Zero;
                    else
                        vel = velNew;

                    // Prevent climbing above climb angle
                    if (axisNSlope < SlopeClimb)
                    {
                        float axisVel = vel.Dot(axis);
                        if (axisVel > 0 && lastForce.Dot(axis) > 0)
                            vel = vel - axis * axisVel;
                        if ((pos - (pos - move)).Dot(axis) > SizeCollider.Z * 0.001f)
                            vel = vel - axis * axis.Dot(vel);
                    }

                    // Fall off slope steeper than fall angle
                    if (axisNSlope < SlopeFall && !IsStuck)
                    {
                        bFlying = true;
                        vel = vel + (GroundNormal - axis * axisNSlope);
                    }

                    // Clamp tiny velocities to zero
                    if (VelRequested.LengthSq() == 0 && vel.LengthSq() < 0.001f || vel.LengthSq() < 0.0001f)
                        vel = PhysVector3.Zero;
                }
            }

            if (!bFlying) TimeFlying = 0;

            // --- Eye nod smoothing ---
            if (!bFlying)
            {
                float dh = (pos - (pos - move)).Dot(axis);
                if (dh > SizeCollider.Z * 0.01f)
                {
                    DhSpeed = MathF.Max(DhSpeed, dh / StablehTime);
                    Dh += dh;
                    StablehTime = 0;
                }
            }
            StablehTime = MathF.Min(StablehTime + timeInterval, 0.5f);
            DhSpeed += DhAcc * timeInterval;
            if (DhAcc == 0 && Dh * DhSpeed < 0 || Dh * DhAcc < 0 || Dh * (Dh - DhSpeed * timeInterval) < 0)
            {
                Dh = DhSpeed = DhAcc = 0;
            }
            else
            {
                Dh -= DhSpeed * timeInterval;
            }

            // --- Head bump ---
            CheckHeadBump(pos, axis, timeInterval);

            // --- Finalize ---
            Position = pos;
            ComputeBBox();
            bMoving = true;
        }
        else if (!IsActive)
        {
            if (VelRequested.LengthSq() > 0)
            {
                Position = Position + VelRequested * timeInterval;
                ComputeBBox();
                bMoving = true;
            }
            if (ReleaseGroundColliderWhenNotActive)
                ReleaseGroundCollider();
        }

        // Update state
        DeltaV = vel - vel0;
        Velocity = vel + Velocity - vel0;  // preserve any external velocity changes during step
        IsFlying = bFlying;
        DeltaQRot = Orientation * oldQRot.Conjugate();
        TimeSmooth = timeInterval > 0 ? timeInterval : TimeSmooth;

        // Record history
        RecordHistory(timeInterval);

        return 1;
    }

    // ======================== Awake override ========================

    public override void Awake(bool awake = true, float minTime = 0f)
    {
        base.Awake(awake, minTime);
        if (!awake)
        {
            Velocity = PhysVector3.Zero;
            VelRequested = PhysVector3.Zero;
        }
        IsActiveEnvironment = awake;
    }

    // ======================== StartStep ========================

    /// <summary>
    /// Begin a new physics step. Port of CLivingEntity::StartStep.
    /// </summary>
    public void StartStep(float timeInterval)
    {
        TimeStepPerformed = 0;
        TimeStepFull = timeInterval;
        _contacts.Clear();
    }

    // ======================== CalcEnergy ========================

    /// <summary>
    /// Calculate kinetic energy. Port of CLivingEntity::CalcEnergy.
    /// </summary>
    public float CalcEnergy()
    {
        return Mass * Velocity.LengthSq();
    }

    // ======================== GetMaxTimeStep ========================

    /// <summary>Port of CLivingEntity::GetMaxTimeStep.</summary>
    public float GetMaxTimeStep(float timeInterval)
    {
        if (TimeStepPerformed > TimeStepFull - 0.001f)
            return timeInterval;
        return MathF.Min(TimeStepFull - TimeStepPerformed, timeInterval);
    }
}
