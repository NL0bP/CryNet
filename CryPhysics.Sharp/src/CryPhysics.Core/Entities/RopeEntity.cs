// Port of CryPhysics ropeentity.h - rope/cable physics
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

/// <summary>Rope vertex data. Port of rope_vtx from CryEngine.</summary>
public class RopeVertex
{
    public PhysVector3 Pt;
    public PhysVector3 Pt0;
    public PhysVector3 Vel;
    public PhysVector3 Dir;
    public PhysVector3 NContact;
    public PhysVector3 VContact;
    public float DP;
    public IPhysicalEntity? ContactEnt;
    public int ContactPart;
}

/// <summary>Rope segment data. Port of rope_segment from CryEngine.</summary>
public class RopeSegment : RopeVertex
{
    public PhysVector3 VelExt;
    public PhysVector3 PtDst;
    public bool RecheckContact;
    public bool RecalcDir;
    public int CheckPart;
    public float TContact;
    public float VReq;
    public int IPrim;
    public int IFeature;
    public int IVtx0;
    public float KDP;
}

/// <summary>
/// Rope/cable physics entity. Port of CRopeEntity from CryEngine.
/// Simulates ropes, cables, and similar 1D deformable objects.
/// Uses PBD (Position Based Dynamics) with distance constraints,
/// swept-sphere collision, attachment points, stiffness animation,
/// and wind/water resistance.
/// </summary>
public class RopeEntity : PhysicalEntity
{
    public const int SnapshotVersion = 8;
    public override PhysicsEntityType Type => PhysicsEntityType.Rope;

    // Simulation params
    public PhysVector3 Gravity = new(0, 0, -9.81f);
    public PhysVector3 Gravity0;
    public float Damping = 0.5f;
    public float MaxAllowedStep = 0.02f;
    public float Emin = 0.004f;
    public float TimeStepPerformed;
    public float TimeStepFull;
    public int NSlowFrames;
    public float LastTimeStep;
    public bool HasContacts;

    // Rope geometry
    public float Length = 1f;
    public int NSegs = 10;
    public RopeSegment[] Segments = Array.Empty<RopeSegment>();
    public float TimeLastActive;
    public bool TargetPoseActive;

    // Host tracking
    public PhysVector3 LastPosHost;
    public PhysQuaternion LastQHost = PhysQuaternion.Identity;

    // Physical properties
    public float Mass = 1f;
    public float CollDist;
    public int SurfaceIdx;
    public float Friction = 0.5f;
    public float Stiffness;
    public float StiffnessAnim;
    public float DampingAnim;
    public float StiffnessDecayAnim;

    // Fluid interaction
    public PhysVector3 Wind;
    public PhysVector3 Wind0;
    public PhysVector3 Wind1;
    public float AirResistance;
    public float WindVariance;
    public float WindTimer;
    public float WaterResistance;
    public float RDensity;

    // Joint/constraint
    public float JointLimit;
    public float JointLimitDecay;
    public float SzSensor;
    public float MaxForce;
    public int FlagsCollider;
    public int CollTypes = -1;
    public float PenaltyScale = 1f;
    public int MaxIters = 20;
    public float AttachmentZone;
    public float MinSegLen;
    public float UnprojLimit;
    public float NoCollDist;
    public float FrictionPull;

    // Attachment
    public IPhysicalEntity?[] TiedTo = new IPhysicalEntity?[2];
    public PhysVector3[] PtTiedLoc = new PhysVector3[2];
    public int[] TiedPart = new int[2];
    public int IdConstraint;

    // Subdivision
    public RopeVertex[] Vtx = Array.Empty<RopeVertex>();
    public int NVtx;
    public int NVtxAlloc;
    public int NVtx0;
    public int NFragments;
    public int NMaxSubVtx;

    // State
    public bool Strained;
    public float Energy;
    public PhysVector3[] CollBBox = new PhysVector3[2];

    // Collision state per segment (for swept sphere tests)
    private float _collRadius;

    // Random for wind variance
    private readonly Random _rng = new();

    public RopeEntity()
    {
        SimulationClass = SimClass.ActiveRigid;
        IsAwake = true;
    }

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        if (parameters is ParamsRope rp)
        {
            if (rp.Length.HasValue) Length = rp.Length.Value;
            if (rp.Mass.HasValue) Mass = rp.Mass.Value;
            if (rp.CollDist.HasValue) { CollDist = rp.CollDist.Value; _collRadius = CollDist; }
            if (rp.SurfaceIdx.HasValue) SurfaceIdx = rp.SurfaceIdx.Value;
            if (rp.Friction.HasValue) Friction = rp.Friction.Value;
            if (rp.FrictionPull.HasValue) FrictionPull = rp.FrictionPull.Value;
            if (rp.Stiffness.HasValue) Stiffness = rp.Stiffness.Value;
            if (rp.StiffnessAnim.HasValue) StiffnessAnim = rp.StiffnessAnim.Value;
            if (rp.DampingAnim.HasValue) DampingAnim = rp.DampingAnim.Value;
            if (rp.StiffnessDecayAnim.HasValue) StiffnessDecayAnim = rp.StiffnessDecayAnim.Value;
            if (rp.TargetPoseActive.HasValue) TargetPoseActive = rp.TargetPoseActive.Value;
            if (rp.Wind.HasValue) { Wind = rp.Wind.Value; Wind0 = Wind; Wind1 = Wind; }
            if (rp.WindVariance.HasValue) WindVariance = rp.WindVariance.Value;
            if (rp.AirResistance.HasValue) AirResistance = rp.AirResistance.Value;
            if (rp.WaterResistance.HasValue) WaterResistance = rp.WaterResistance.Value;
            if (rp.Density.HasValue) RDensity = rp.Density.Value > 0 ? 1f / rp.Density.Value : 0;
            if (rp.JointLimit.HasValue) JointLimit = rp.JointLimit.Value;
            if (rp.JointLimitDecay.HasValue) JointLimitDecay = rp.JointLimitDecay.Value;
            if (rp.SensorRadius.HasValue) SzSensor = rp.SensorRadius.Value;
            if (rp.MaxForce.HasValue) MaxForce = rp.MaxForce.Value;
            if (rp.PenaltyScale.HasValue) PenaltyScale = rp.PenaltyScale.Value;
            if (rp.MaxIters.HasValue) MaxIters = rp.MaxIters.Value;
            if (rp.AttachmentZone.HasValue) AttachmentZone = rp.AttachmentZone.Value;
            if (rp.MinSegLen.HasValue) MinSegLen = rp.MinSegLen.Value;
            if (rp.UnprojLimit.HasValue) UnprojLimit = rp.UnprojLimit.Value;
            if (rp.NoCollDist.HasValue) NoCollDist = rp.NoCollDist.Value;
            if (rp.NSegments.HasValue) InitSegments(rp.NSegments.Value);
            if (rp.FlagsCollider.HasValue) FlagsCollider = rp.FlagsCollider.Value;
            if (rp.CollTypes.HasValue) CollTypes = rp.CollTypes.Value;
            if (rp.NMaxSubVtx.HasValue) NMaxSubVtx = rp.NMaxSubVtx.Value;
            return 1;
        }
        if (parameters is SimulationParams sim)
        {
            if (sim.Gravity.HasValue) { Gravity = sim.Gravity.Value; Gravity0 = Gravity; }
            if (sim.Damping.HasValue) Damping = sim.Damping.Value;
            if (sim.MinEnergy.HasValue) Emin = sim.MinEnergy.Value;
            if (sim.MaxTimeStep.HasValue) MaxAllowedStep = sim.MaxTimeStep.Value;
            return 1;
        }
        return base.SetParams(parameters, threadSafe);
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        if (status is StatusRope rs)
        {
            rs.NSegments = NSegs;
            rs.BTargetPoseActive = TargetPoseActive;
            rs.StiffnessAnim = StiffnessAnim;
            rs.BStrained = Strained;
            rs.TimeLastActive = TimeLastActive;
            rs.NVtx = NVtx;
            // Fill point/velocity arrays if provided
            if (rs.Points != null && rs.Points.Length >= NSegs + 1)
            {
                for (int i = 0; i <= NSegs && i < Segments.Length; i++)
                    rs.Points[i] = Segments[i].Pt;
            }
            if (rs.Velocities != null && rs.Velocities.Length >= NSegs + 1)
            {
                for (int i = 0; i <= NSegs && i < Segments.Length; i++)
                    rs.Velocities[i] = Segments[i].Vel;
            }
            // Count collision contacts
            int nCollStat = 0, nCollDyn = 0;
            for (int i = 0; i < NSegs && i < Segments.Length; i++)
            {
                if (Segments[i].ContactEnt != null)
                {
                    if (Segments[i].ContactEnt is PhysicalEntity pe && pe.SimulationClass >= SimClass.ActiveRigid)
                        nCollDyn++;
                    else
                        nCollStat++;
                }
            }
            rs.NCollStat = nCollStat;
            rs.NCollDyn = nCollDyn;
            return 1;
        }
        return base.GetStatus(status);
    }

    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (!IsAwake || NSegs == 0) return 0;
        TimeStepFull = timeInterval;

        float segLen = Length / NSegs;
        float segMass = Mass / NSegs;
        float dt = MathF.Min(timeInterval, MaxAllowedStep);

        // --- Update wind variance (C++: m_windTimer += dt*4; blend between wind0/wind1) ---
        WindTimer += dt * 4f;
        if (WindTimer > 1f)
        {
            WindTimer = 0;
            Wind0 = Wind1;
            float a = WindVariance * (MathF.Abs(Wind.X) + MathF.Abs(Wind.Y) + MathF.Abs(Wind.Z));
            Wind1 = Wind + new PhysVector3(
                (float)_rng.NextDouble() * a - a * 0.5f,
                (float)_rng.NextDouble() * a - a * 0.5f,
                (float)_rng.NextDouble() * a - a * 0.5f);
        }
        var currentWind = Wind0 * WindTimer + Wind1 * (1f - WindTimer);

        // --- Update tied-end positions and velocities ---
        for (int end = 0; end < 2; end++)
        {
            if (TiedTo[end] == null) continue;
            int segIdx = end == 0 ? 0 : NSegs;
            // Get position from tied entity
            var tiedEnt = TiedTo[end] as PhysicalEntity;
            if (tiedEnt != null)
            {
                var ptWorld = tiedEnt.Position + tiedEnt.Orientation.Rotate(PtTiedLoc[end]);
                Segments[segIdx].Pt = ptWorld;
                // Estimate velocity from the tied body (finite difference from Pt0)
                if (dt > 1e-8f)
                    Segments[segIdx].Vel = (Segments[segIdx].Pt - Segments[segIdx].Pt0) / dt;
            }
        }

        // --- Check for overstretched rope between two attachment points ---
        if (TiedTo[0] != null && TiedTo[1] != null)
        {
            var span = Segments[NSegs].Pt - Segments[0].Pt;
            float spanLen = span.Length();
            if (spanLen > Length)
            {
                // Straighten the rope along the span
                Strained = true;
                var dir = span / spanLen;
                float newSegLen = spanLen / NSegs;
                float rnSegs = 1f / (NSegs + 1);
                for (int i = 1; i < NSegs; i++)
                {
                    Segments[i].Dir = dir;
                    Segments[i].Pt = Segments[0].Pt + dir * (newSegLen * i);
                    Segments[i].Vel = Segments[0].Vel * ((NSegs + 1 - i) * rnSegs)
                                    + Segments[NSegs].Vel * (i * rnSegs);
                }
                // Update Pt0 and compute BBox, then return early
                for (int i = 0; i < NSegs; i++)
                    Segments[i].Pt0 = Segments[i].Pt;
                TimeStepPerformed = dt;
                ComputeRopeBBox();
                return 1;
            }
            else
            {
                Strained = false;
            }
        }

        // --- Apply forces and integrate segments ---
        int iStart = TiedTo[0] != null ? 1 : 0;
        int iEnd = TiedTo[1] != null ? NSegs - 1 : NSegs;

        for (int i = iStart; i <= iEnd; i++)
        {
            var seg = Segments[i];
            seg.Pt0 = seg.Pt;

            // Gravity
            seg.Vel = seg.Vel + Gravity * dt;

            // Air resistance: C++ uses (wind-vel)*clamp(airRes*dt,0,1)
            if (AirResistance > 0)
                seg.Vel = seg.Vel + (currentWind - seg.Vel) * MathF.Min(1f, AirResistance * dt);

            // Water resistance: if density set and position below some plane
            // Simplified: apply water drag if WaterResistance > 0 and vertex below z=0 (water surface)
            if (WaterResistance > 0 && seg.Pt.Z < 0)
            {
                float depth = -seg.Pt.Z;
                float rcollDist = CollDist > 0 ? 1f / CollDist : 1f;
                float submerged = MathF.Min(1f, depth * rcollDist);
                seg.Vel = seg.Vel + (-seg.Vel) * MathF.Min(1f, WaterResistance * dt * submerged);
                // Buoyancy: counteract gravity proportional to submersion and density ratio
                if (RDensity > 0)
                    seg.Vel = seg.Vel - Gravity * (RDensity * submerged * dt);
            }

            // Damping (including animation damping when active)
            float effectiveDamping = Damping;
            if (TargetPoseActive && DampingAnim > 0)
                effectiveDamping = MathF.Max(effectiveDamping, DampingAnim);
            seg.Vel = seg.Vel * MathF.Max(0f, 1f - effectiveDamping * dt);

            // Integrate position
            seg.Pt = seg.Pt + seg.Vel * dt;
        }

        // --- Target pose stiffness animation ---
        // C++: if bTargetPoseActive==1 and stiffnessAnim>0, blend positions toward target (ptdst)
        if (TargetPoseActive && StiffnessAnim > 0)
        {
            float kAnim = MathF.Min(1f, StiffnessAnim * dt);
            for (int i = iStart; i <= iEnd; i++)
            {
                var seg = Segments[i];
                // Decay stiffness along the rope from attachment point
                float decay = 1f;
                if (StiffnessDecayAnim > 0 && NSegs > 0)
                    decay = MathF.Max(0f, 1f - StiffnessDecayAnim * ((float)i / NSegs));

                var target = seg.PtDst;
                if (target.LengthSq() > 1e-10f) // only if target is set
                {
                    var diff = target - seg.Pt;
                    seg.Pt = seg.Pt + diff * (kAnim * decay);
                }
            }
        }

        // --- Stiffness: apply bending resistance (angular springs between consecutive segments) ---
        if (Stiffness > 0 && NSegs >= 2)
        {
            float kBend = Stiffness * dt;
            for (int i = 1; i < NSegs; i++)
            {
                var s0 = Segments[i - 1];
                var s1 = Segments[i];
                var s2 = Segments[i + 1 < Segments.Length ? i + 1 : i];

                var d1 = s1.Pt - s0.Pt;
                var d2 = s2.Pt - s1.Pt;
                float l1 = d1.Length();
                float l2 = d2.Length();
                if (l1 < 1e-8f || l2 < 1e-8f) continue;

                var n1 = d1 / l1;
                var n2 = d2 / l2;
                // The "straightening" force pushes toward a straight configuration
                var correction = (n1 - n2) * (kBend * 0.5f);

                bool fixed0 = (i - 1 == 0 && TiedTo[0] != null);
                bool fixed2 = (i + 1 == NSegs && TiedTo[1] != null);

                if (!fixed0)
                    s0.Pt = s0.Pt - correction;
                // Middle vertex gets pushed by both
                s1.Pt = s1.Pt + correction * 2f;
                if (!fixed2 && i + 1 < Segments.Length)
                    s2.Pt = s2.Pt - correction;
            }
        }

        // --- PBD distance constraint enforcement ---
        for (int iter = 0; iter < MaxIters; iter++)
        {
            for (int i = 0; i < NSegs; i++)
            {
                var s0 = Segments[i];
                var s1 = Segments[i + 1];
                var delta = s1.Pt - s0.Pt;
                float len = delta.Length();
                if (len < 1e-10f) continue;

                float diff = (len - segLen) / len;
                var correction = delta * (diff * 0.5f);

                bool fixed0 = (i == 0 && TiedTo[0] != null);
                bool fixed1 = (i + 1 == NSegs && TiedTo[1] != null);

                if (!fixed0 && !fixed1)
                {
                    s0.Pt = s0.Pt + correction;
                    s1.Pt = s1.Pt - correction;
                }
                else if (!fixed0)
                    s0.Pt = s0.Pt + correction * 2f;
                else if (!fixed1)
                    s1.Pt = s1.Pt - correction * 2f;
            }
        }

        // --- Swept sphere collision with environment ---
        // For each segment, check if the vertex is below the collision ground plane (z=0 simplified)
        // or within CollDist of any static geometry. This is a simplified version of the C++ swept sphere.
        if (CollDist > 0)
        {
            for (int i = iStart; i <= iEnd; i++)
            {
                var seg = Segments[i];
                // Simplified ground collision: project out of ground plane at z = 0
                if (seg.Pt.Z < CollDist)
                {
                    float penetration = CollDist - seg.Pt.Z;
                    seg.Pt = new PhysVector3(seg.Pt.X, seg.Pt.Y, CollDist);
                    seg.NContact = PhysVector3.UnitZ;
                    seg.VContact = PhysVector3.Zero;
                    HasContacts = true;

                    // Apply friction: reduce tangential velocity
                    if (Friction > 0)
                    {
                        float vn = seg.Vel.Dot(seg.NContact);
                        var vTang = seg.Vel - seg.NContact * vn;
                        float vTangLen = vTang.Length();
                        if (vTangLen > 1e-6f)
                        {
                            float frictionImpulse = MathF.Abs(vn) * Friction;
                            if (frictionImpulse >= vTangLen)
                                seg.Vel = seg.NContact * MathF.Max(0f, vn);
                            else
                                seg.Vel = seg.Vel - vTang * (frictionImpulse / vTangLen);
                        }
                    }
                }
                // Could add world raycasts here if World is available
            }
        }

        // --- Update velocities from position changes ---
        float rdt = dt > 0 ? 1f / dt : 0;
        for (int i = 0; i <= NSegs && i < Segments.Length; i++)
        {
            var seg = Segments[i];
            seg.Vel = (seg.Pt - seg.Pt0) * rdt;
            // Clamp velocity to avoid explosion
            if (seg.Vel.LengthSq() > 100f * 100f)
                seg.Vel = seg.Vel.Normalized() * 100f;
        }

        // --- Update segment directions ---
        for (int i = 0; i < NSegs; i++)
        {
            var d = Segments[i + 1].Pt - Segments[i].Pt;
            float dLen = d.Length();
            Segments[i].Dir = dLen > 1e-8f ? d / dLen : PhysVector3.UnitZ;
        }
        if (NSegs > 0)
            Segments[NSegs].Dir = Segments[NSegs - 1].Dir;

        // --- Compute kinetic energy ---
        Energy = 0;
        for (int i = 0; i <= NSegs && i < Segments.Length; i++)
            Energy += Segments[i].Vel.LengthSq() * segMass;
        Energy *= 0.5f;

        TimeStepPerformed = dt;
        LastTimeStep = dt;
        ComputeRopeBBox();

        // Sleep check
        if (Energy < Emin)
        {
            NSlowFrames++;
            if (NSlowFrames > 10)
                Awake(false);
        }
        else
            NSlowFrames = 0;

        return 1;
    }

    /// <summary>
    /// Set a tied-to entity for one end of the rope.
    /// end: 0 = start, 1 = end.
    /// </summary>
    public void SetTiedTo(int end, IPhysicalEntity? entity, PhysVector3 localPt, int partId = 0)
    {
        if (end < 0 || end > 1) return;
        TiedTo[end] = entity;
        PtTiedLoc[end] = localPt;
        TiedPart[end] = partId;
        if (entity != null)
            Awake();
    }

    private void InitSegments(int nSegs)
    {
        NSegs = nSegs;
        // Allocate nSegs+1 entries (one per vertex, including both endpoints)
        Segments = new RopeSegment[nSegs + 1];
        float segLen = Length / nSegs;
        for (int i = 0; i <= nSegs; i++)
        {
            Segments[i] = new RopeSegment
            {
                Pt = Position + new PhysVector3(0, 0, -segLen * i),
                Pt0 = Position + new PhysVector3(0, 0, -segLen * i),
            };
        }
    }

    private void ComputeRopeBBox()
    {
        if (Segments.Length == 0) { BBoxMin = BBoxMax = Position; return; }
        var min = Segments[0].Pt;
        var max = Segments[0].Pt;
        for (int i = 1; i < Segments.Length; i++)
        {
            min = PhysVector3.Min(min, Segments[i].Pt);
            max = PhysVector3.Max(max, Segments[i].Pt);
        }
        float pad = MathF.Max(CollDist * 2f, 0.05f);
        var padV = new PhysVector3(pad, pad, pad);
        BBoxMin = min - padV;
        BBoxMax = max + padV;
        CollBBox[0] = BBoxMin;
        CollBBox[1] = BBoxMax;
    }
}

/// <summary>Rope parameters. Port of pe_params_rope.</summary>
public class ParamsRope : PhysicsParamsBase
{
    public override int TypeId => 12;
    public float? Length { get; set; }
    public float? Mass { get; set; }
    public float? CollDist { get; set; }
    public int? SurfaceIdx { get; set; }
    public float? Friction { get; set; }
    public float? FrictionPull { get; set; }
    public float? Stiffness { get; set; }
    public float? StiffnessAnim { get; set; }
    public float? StiffnessDecayAnim { get; set; }
    public float? DampingAnim { get; set; }
    public bool? TargetPoseActive { get; set; }
    public PhysVector3? Wind { get; set; }
    public float? WindVariance { get; set; }
    public float? AirResistance { get; set; }
    public float? WaterResistance { get; set; }
    public float? Density { get; set; }
    public float? JointLimit { get; set; }
    public float? JointLimitDecay { get; set; }
    public float? SensorRadius { get; set; }
    public float? MaxForce { get; set; }
    public float? PenaltyScale { get; set; }
    public float? AttachmentZone { get; set; }
    public float? MinSegLen { get; set; }
    public float? UnprojLimit { get; set; }
    public float? NoCollDist { get; set; }
    public int? MaxIters { get; set; }
    public int? NSegments { get; set; }
    public int? FlagsCollider { get; set; }
    public int? CollTypes { get; set; }
    public int? NMaxSubVtx { get; set; }
}

/// <summary>Rope status. Port of pe_status_rope.</summary>
public class StatusRope : PhysicsStatusBase
{
    public override int TypeId => 7;
    public int NSegments { get; set; }
    public PhysVector3[]? Points { get; set; }
    public PhysVector3[]? Velocities { get; set; }
    public int NCollStat { get; set; }
    public int NCollDyn { get; set; }
    public bool BTargetPoseActive { get; set; }
    public float StiffnessAnim { get; set; }
    public bool BStrained { get; set; }
    public float TimeLastActive { get; set; }
    public int NVtx { get; set; }
}
