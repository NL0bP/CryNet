// Port of CryPhysics wheeledvehicleentity.h - vehicle physics
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Dynamics;
using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

/// <summary>Maximum number of wheels per vehicle.</summary>
public static class VehicleConstants
{
    public const int MaxWheels = 24;
}

/// <summary>
/// Suspension/wheel data. Port of suspension_point from CryEngine.
/// </summary>
public class SuspensionPoint
{
    // Transform
    public PhysVector3 Pos;
    public PhysQuaternion Q = PhysQuaternion.Identity;
    public float Scale = 1f;
    public PhysVector3[] BBox = new PhysVector3[2];

    // Suspension geometry
    public float Tscale = 1f;
    public PhysVector3 Pt;
    public float FullLen;
    public float KStiffness;
    public float KStiffnessWeight;
    public float KDamping;
    public float KDamping0;
    public float Len0;
    public float Mpt;
    public PhysQuaternion Q0 = PhysQuaternion.Identity;
    public PhysVector3 Pos0;
    public PhysVector3 Ptc0;
    public float IInv;

    // Friction
    public float MinFriction;
    public float MaxFriction;
    public float KLatFriction;
    public int Flags0;
    public int FlagsCollider0;

    // Bitfield flags
    public bool Driving;
    public bool CanBrake = true;
    public bool Blocked;
    public bool RayCast;
    public bool Slip;
    public bool SlipPull;
    public bool Contact;
    public bool CanSteer;

    // Configuration
    public int Axle;
    public int Buddy = -1;
    public int IPart;

    // Wheel properties
    public float Radius;
    public float RadiusInv;
    public float Width;
    public float CurLen;
    public float Steer;
    public float Rot;
    public float W;       // Angular velocity
    public float Wa;      // Angular acceleration
    public float T;       // Torque
    public float PrevTdt;
    public float PrevW;

    // Contact data
    public PhysVector3 NContact;
    public PhysVector3 PtContact;
    public int[] SurfaceIdx = new int[2];
    public PhysVector3 VRel;
    public PhysVector3 RWorld;
    public float VWorld;
    public float PN;
    public IPhysicalEntity? ContactEnt;
    public float Unproj;
}

/// <summary>
/// Wheeled vehicle entity. Port of CWheeledVehicleEntity from CryEngine.
/// Simulates vehicle suspension, wheels, engine, and drivetrain.
/// Extends RigidEntity with per-wheel suspension springs, tire friction,
/// steering/throttle/brake input, and ground contact detection.
/// </summary>
public class WheeledVehicleEntity : RigidEntity
{
    public const int SnapshotVersionVehicle = 1;
    public override PhysicsEntityType Type => PhysicsEntityType.WheeledVehicle;

    // Wheels/suspension
    public SuspensionPoint[] Susp = new SuspensionPoint[VehicleConstants.MaxWheels];
    public int NWheels;

    // Engine
    public float EnginePower;
    public float MaxSteer;
    public float EngineMaxW;
    public float EngineMinW;
    public float EngineIdleW;
    public float EngineShiftUpW;
    public float EngineShiftDownW;
    public float GearDirSwitchW;
    public float EngineStartW;
    public float AxleFriction;
    public float BrakeTorque;
    public float ClutchSpeed;
    public float MinBrakingFriction;
    public float MaxBrakingFriction;
    public float KDynFriction;
    public float SlipThreshold;
    public float KStabilizer;

    // Driving state
    public float EnginePedal;
    public float SteerAngle;
    public float AckermanOffset;
    public float Clutch;
    public float WEngine;
    public float[] Gears = new float[12];
    public int NGears;
    public int CurGear;
    public int MaxGear;
    public int MinGear;
    public bool HandBrake;
    public int NHullParts;
    public bool HasContacts;
    public bool KeepTractionWhenTilted;
    public int NContacts;

    // Derived
    public float KSteerToTrack;
    public float EminRigid;
    public float EminVehicle;
    public float MaxAllowedStepVehicle;
    public float MaxAllowedStepRigid;
    public float DampingVehicle;
    public float TimeNoContacts;
    public float PullTilt;
    public float DrivingTorque;
    public float MaxTilt;

    public WheeledVehicleEntity()
    {
        for (int i = 0; i < VehicleConstants.MaxWheels; i++)
            Susp[i] = new SuspensionPoint();
    }

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        if (parameters is ParamsCar pc)
        {
            if (pc.EnginePower.HasValue) EnginePower = pc.EnginePower.Value;
            if (pc.MaxSteer.HasValue) MaxSteer = pc.MaxSteer.Value;
            if (pc.EngineMaxRPM.HasValue) EngineMaxW = pc.EngineMaxRPM.Value * (MathF.PI / 30f);
            if (pc.EngineMinRPM.HasValue) EngineMinW = pc.EngineMinRPM.Value * (MathF.PI / 30f);
            if (pc.EngineIdleRPM.HasValue) EngineIdleW = pc.EngineIdleRPM.Value * (MathF.PI / 30f);
            if (pc.EngineShiftUpRPM.HasValue) EngineShiftUpW = pc.EngineShiftUpRPM.Value * (MathF.PI / 30f);
            if (pc.EngineShiftDownRPM.HasValue) EngineShiftDownW = pc.EngineShiftDownRPM.Value * (MathF.PI / 30f);
            if (pc.BrakeTorque.HasValue) BrakeTorque = pc.BrakeTorque.Value;
            if (pc.AxleFriction.HasValue) AxleFriction = pc.AxleFriction.Value;
            if (pc.ClutchSpeed.HasValue) ClutchSpeed = pc.ClutchSpeed.Value;
            if (pc.MinBrakingFriction.HasValue) MinBrakingFriction = pc.MinBrakingFriction.Value;
            if (pc.MaxBrakingFriction.HasValue) MaxBrakingFriction = pc.MaxBrakingFriction.Value;
            if (pc.KStabilizer.HasValue) KStabilizer = pc.KStabilizer.Value;
            if (pc.NGears.HasValue) NGears = pc.NGears.Value;
            if (pc.GearRatios != null)
                Array.Copy(pc.GearRatios, Gears, System.Math.Min(pc.GearRatios.Length, Gears.Length));
            if (pc.MaxGear.HasValue) MaxGear = pc.MaxGear.Value;
            if (pc.MinGear.HasValue) MinGear = pc.MinGear.Value;
            if (pc.SlipThreshold.HasValue) SlipThreshold = pc.SlipThreshold.Value;
            if (pc.KDynFriction.HasValue) KDynFriction = pc.KDynFriction.Value;
            if (pc.SteerTrackNeutralTurn.HasValue) KSteerToTrack = pc.SteerTrackNeutralTurn.Value;
            if (pc.PullTilt.HasValue) PullTilt = pc.PullTilt.Value;
            if (pc.MaxTilt.HasValue) MaxTilt = pc.MaxTilt.Value;
            if (pc.KeepTractionWhenTilted.HasValue) KeepTractionWhenTilted = pc.KeepTractionWhenTilted.Value;
            if (pc.NWheels.HasValue) NWheels = pc.NWheels.Value;
            if (pc.GearDirSwitchRPM.HasValue) GearDirSwitchW = pc.GearDirSwitchRPM.Value * (MathF.PI / 30f);
            if (pc.EngineStartRPM.HasValue) EngineStartW = pc.EngineStartRPM.Value * (MathF.PI / 30f);
            return 1;
        }
        if (parameters is ParamsWheel pw)
        {
            int iw = pw.IWheel ?? 0;
            if (iw < 0 || iw >= VehicleConstants.MaxWheels) return 0;
            var s = Susp[iw];
            if (pw.Driving.HasValue) s.Driving = pw.Driving.Value;
            if (pw.Axle.HasValue) s.Axle = pw.Axle.Value;
            if (pw.CanBrake.HasValue) s.CanBrake = pw.CanBrake.Value;
            if (pw.CanSteer.HasValue) s.CanSteer = pw.CanSteer.Value;
            if (pw.SuspLenMax.HasValue) s.FullLen = pw.SuspLenMax.Value;
            if (pw.SuspLenInitial.HasValue) s.Len0 = pw.SuspLenInitial.Value;
            if (pw.MinFriction.HasValue) s.MinFriction = pw.MinFriction.Value;
            if (pw.MaxFriction.HasValue) s.MaxFriction = pw.MaxFriction.Value;
            if (pw.KStiffness.HasValue) s.KStiffness = pw.KStiffness.Value;
            if (pw.KDamping.HasValue) s.KDamping = pw.KDamping.Value;
            if (pw.KLatFriction.HasValue) s.KLatFriction = pw.KLatFriction.Value;
            if (pw.Tscale.HasValue) s.Tscale = pw.Tscale.Value;
            if (pw.BRayCast.HasValue) s.RayCast = pw.BRayCast.Value;
            if (pw.Blocked.HasValue) s.Blocked = pw.Blocked.Value;
            if (pw.W.HasValue) s.W = pw.W.Value;
            if (iw >= NWheels) NWheels = iw + 1;
            return 1;
        }
        return base.SetParams(parameters, threadSafe);
    }

    public override int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        if (action is ActionDrive drv)
        {
            if (drv.Pedal.HasValue) EnginePedal = drv.Pedal.Value;
            if (drv.DPedal.HasValue) EnginePedal += drv.DPedal.Value;
            if (drv.Steer.HasValue) SteerAngle = drv.Steer.Value;
            if (drv.DSteer.HasValue) SteerAngle += drv.DSteer.Value;
            if (drv.Clutch.HasValue) Clutch = drv.Clutch.Value;
            if (drv.HandBrake.HasValue) HandBrake = drv.HandBrake.Value;
            if (drv.IGear.HasValue) CurGear = drv.IGear.Value;
            if (drv.AckermanOffset.HasValue) AckermanOffset = drv.AckermanOffset.Value;
            // Clamp pedal and steer
            EnginePedal = MathF.Max(-1f, MathF.Min(1f, EnginePedal));
            SteerAngle = MathF.Max(-MaxSteer, MathF.Min(MaxSteer, SteerAngle));
            Awake();
            return 1;
        }
        return base.DoAction(action, threadSafe);
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        if (status is StatusVehicle sv)
        {
            sv.Steer = SteerAngle;
            sv.Pedal = EnginePedal;
            sv.HandBrake = HandBrake;
            sv.Vel = Body.V;
            sv.CurGear = CurGear;
            sv.EngineRPM = WEngine * (30f / MathF.PI);
            sv.Clutch = Clutch;
            sv.DrivingTorque = DrivingTorque;
            sv.NActiveColliders = NContacts;
            sv.WheelContact = HasContacts;
            return 1;
        }
        if (status is StatusWheel sw)
        {
            int iw = sw.IWheel;
            if (iw < 0 || iw >= VehicleConstants.MaxWheels) return 0;
            var s = Susp[iw];
            sw.Contact = s.Contact;
            sw.W = s.W;
            sw.Slip = s.Slip;
            sw.SuspLen = s.CurLen;
            sw.SuspLenFull = s.FullLen;
            sw.R = s.Radius;
            sw.Steer = s.Steer;
            sw.PtContact = s.PtContact;
            sw.NormContact = s.NContact;
            sw.ContactEnt = s.ContactEnt;
            sw.Torque = s.T;
            sw.VelSlip = s.VRel;
            return 1;
        }
        return base.GetStatus(status);
    }

    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (!IsAwake) return 0;

        float dt = timeInterval;

        // --- Apply steering to steerable wheels ---
        for (int i = 0; i < NWheels; i++)
        {
            if (Susp[i].CanSteer)
            {
                // Ackerman steering: inner wheel steers more than outer
                float ackerman = AckermanOffset;
                float steerSign = Susp[i].Pt.X > 0 ? 1f : -1f;
                Susp[i].Steer = SteerAngle + ackerman * steerSign * SteerAngle;
            }
        }

        // --- Ground contact detection per wheel ---
        // Simplified ray-cast downward from suspension top point
        var axisZ = Orientation.Rotate(PhysVector3.UnitZ);
        var bodyVel = Body.V;

        HasContacts = false;
        NContacts = 0;

        for (int i = 0; i < NWheels; i++)
        {
            var s = Susp[i];
            if (s.FullLen <= 0 && s.Radius <= 0) continue;

            // Compute wheel world position from suspension point in body frame
            s.RWorld = Orientation.Rotate(s.Pt) + Position - Body.Pos;
            s.VWorld = (bodyVel + (Body.W ^ s.RWorld)).Dot(axisZ);

            // Simplified ground contact: cast downward from suspension mount
            var wheelWorldPos = Position + Orientation.Rotate(s.Pt);
            float groundZ = 0f; // Simplified ground plane at z=0

            float suspTop = wheelWorldPos.Z;
            float wheelBottom = suspTop - s.FullLen - s.Radius;

            if (wheelBottom <= groundZ)
            {
                // Contact detected
                s.Contact = true;
                s.CurLen = MathF.Max(0f, suspTop - groundZ - s.Radius);
                s.CurLen = MathF.Min(s.CurLen, s.FullLen);
                s.PtContact = new PhysVector3(wheelWorldPos.X, wheelWorldPos.Y, groundZ);
                s.NContact = PhysVector3.UnitZ;
                s.ContactEnt = null; // Would be ground entity
                HasContacts = true;
                NContacts++;

                // Compute relative velocity at contact point
                var contactWorldPos = s.PtContact;
                var rContact = contactWorldPos - Body.Pos;
                s.VRel = Body.V + (Body.W ^ rContact);
            }
            else
            {
                s.Contact = false;
                s.CurLen = s.FullLen;
                s.ContactEnt = null;
            }
        }

        // --- Compute driving torque from engine ---
        DrivingTorque = ComputeDrivingTorque(dt);

        // --- Suspension spring forces + tire friction ---
        var totalForce = PhysVector3.Zero;
        var totalTorque = PhysVector3.Zero;

        for (int i = 0; i < NWheels; i++)
        {
            var s = Susp[i];
            if (!s.Contact || s.Axle < 0) continue;

            // --- Suspension spring force (C++: fN = (fullen - curlen) * kStiffness - vworld * kDamping) ---
            float springForce = (s.FullLen - s.CurLen) * s.KStiffness;
            springForce -= s.VWorld * s.KDamping;

            // Stabilizer bar: penalizes difference between buddy wheels
            if (s.Buddy >= 0 && s.Buddy < NWheels && KStabilizer > 0)
                springForce -= (s.CurLen - Susp[s.Buddy].CurLen) * s.KStiffness * KStabilizer;

            s.PN = MathF.Max(0f, springForce * dt);

            // Apply suspension impulse to body
            var suspImpulse = axisZ * s.PN;
            totalForce = totalForce + suspImpulse;
            totalTorque = totalTorque + (s.RWorld ^ suspImpulse);

            // --- Wheel torques ---
            // Axle friction always opposes rotation
            s.T = -MathF.Sign(s.W) * AxleFriction;

            // Brake torque when pedal is in opposing direction to gear
            if (EnginePedal * MathF.Sign(CurGear - 1) <= 0)
                s.T += BrakeTorque * EnginePedal;

            // Driving torque for driven wheels
            if (s.Driving)
            {
                float t = DrivingTorque;
                // Track steering: reduce torque on inner track side
                if (KSteerToTrack != 0 && MathF.Abs(SteerAngle) > 0.01f && SteerAngle * s.Pt.X > 0)
                {
                    t *= MathF.Max(-1f, 1f - MathF.Abs(SteerAngle * KSteerToTrack));
                }
                s.T += t * s.Tscale;
            }

            // Handbrake / blocked
            if ((HandBrake && s.CanBrake) || s.Blocked)
            {
                s.W = 0;
            }

            s.PrevTdt = s.T * dt;
            s.PrevW = s.W;

            // --- Tire friction model (longitudinal + lateral) ---
            // Compute wheel rotation axis in world space
            float cosSteer = MathF.Cos(s.Steer);
            float sinSteer = MathF.Sin(s.Steer);
            var localAxis = new PhysVector3(
                MathF.Sign(s.Pt.X) * cosSteer,
                -MathF.Sign(s.Pt.X) * sinSteer,
                0f);
            var wheelAxis = Orientation.Rotate(localAxis);

            // Pull direction: perpendicular to wheel axis and contact normal
            var pullDir = (s.NContact ^ wheelAxis);
            float pullDirLen = pullDir.Length();
            if (pullDirLen > 1e-6f)
                pullDir = pullDir / pullDirLen;
            else
                pullDir = Orientation.Rotate(PhysVector3.UnitY);

            // Ground velocity at wheel contact in pull direction
            float wGround = s.VRel.Dot(pullDir) * s.RadiusInv;

            // Slip velocity
            var velSlip = s.VRel - pullDir * (s.W * s.Radius);
            s.Slip = velSlip.LengthSq() > SlipThreshold * SlipThreshold;

            // Friction coefficient
            float friction = MathF.Max(s.MinFriction, MathF.Min(s.MaxFriction, 1f));
            if (s.Slip)
                friction *= KDynFriction > 0 ? KDynFriction : 0.7f;

            float N = s.PN * friction;

            // Longitudinal friction: drive force along pullDir
            float fpull = s.T * (s.RadiusInv > 0 ? s.RadiusInv : 1f) * dt;
            s.SlipPull = false;
            if (MathF.Abs(fpull) > N && s.Driving)
            {
                s.SlipPull = true;
                fpull = MathF.Sign(fpull) * N;
            }

            var longImpulse = pullDir * fpull;
            totalForce = totalForce + longImpulse;
            totalTorque = totalTorque + (s.RWorld ^ longImpulse);

            // Lateral friction: resist sideways sliding
            var latDir = wheelAxis;
            float latVel = s.VRel.Dot(latDir);
            float latFriction = friction * (s.KLatFriction > 0 ? s.KLatFriction : 1f);
            float latImpulseMag = MathF.Min(MathF.Abs(latVel) * Body.M * 0.5f, N * latFriction);
            var latImpulse = latDir * (-MathF.Sign(latVel) * latImpulseMag);
            totalForce = totalForce + latImpulse;
            totalTorque = totalTorque + (s.RWorld ^ latImpulse);

            // --- Update wheel angular velocity ---
            // w += (T - frictionTorque) * dt / I
            // Simplified: wheel inertia ~ mass * r^2, but we use a simple model
            float wheelInertia = s.Radius > 0 ? s.Radius * s.Radius : 1f;
            if (!(HandBrake && s.CanBrake) && !s.Blocked)
            {
                s.W += s.T / MathF.Max(0.01f, wheelInertia) * dt;
                // Also let ground friction affect wheel speed: blend toward ground speed
                if (s.Contact && !s.SlipPull)
                    s.W = s.W * 0.9f + wGround * 0.1f;
            }

            // Update wheel rotation angle
            s.Rot += s.W * dt;
            if (s.Rot > MathF.PI * 2f) s.Rot -= MathF.PI * 2f;
            if (s.Rot < -MathF.PI * 2f) s.Rot += MathF.PI * 2f;
        }

        // --- Apply accumulated suspension + friction impulses to rigid body ---
        Body.P = Body.P + totalForce;
        Body.L = Body.L + totalTorque;

        // --- Engine RPM tracking ---
        UpdateEngineRPM(dt);

        // --- Auto gear shifting ---
        AutoShiftGears();

        // --- Let RigidEntity do its base step (gravity, integration, constraints) ---
        int result = base.DoStep(timeInterval, callerIndex);

        // --- Post-step: track contacts ---
        if (NContacts > 0)
            TimeNoContacts = 0;
        else
            TimeNoContacts += dt;

        // Clear wheel W when asleep
        if (!IsAwake)
        {
            for (int i = 0; i < NWheels; i++)
                Susp[i].W = 0;
        }

        return result;
    }

    /// <summary>
    /// Compute driving torque from engine pedal, gear ratio, and engine RPM.
    /// Port of CWheeledVehicleEntity::ComputeDrivingTorque.
    /// </summary>
    private float ComputeDrivingTorque(float dt)
    {
        if (NGears == 0 || CurGear < 0 || CurGear >= NGears)
            return 0;

        float gearRatio = Gears[CurGear];
        if (MathF.Abs(gearRatio) < 1e-6f) return 0; // Neutral

        // Engine torque = power / omega, but clamped
        float engineW = MathF.Max(EngineIdleW, WEngine);
        float torque = 0;
        if (engineW > 1e-3f)
            torque = EnginePower * EnginePedal / engineW;

        // Apply gear ratio
        torque *= gearRatio;

        // Clutch modulation
        float clutchFactor = 1f - Clutch;
        torque *= clutchFactor;

        return torque;
    }

    /// <summary>
    /// Update engine RPM from wheel speeds and clutch state.
    /// </summary>
    private void UpdateEngineRPM(float dt)
    {
        if (NGears == 0 || CurGear < 0 || CurGear >= NGears) return;

        float gearRatio = Gears[CurGear];
        if (MathF.Abs(gearRatio) < 1e-6f)
        {
            // Neutral: engine idles
            WEngine += (EngineIdleW - WEngine) * MathF.Min(1f, dt * 5f);
            return;
        }

        // Average driven wheel speed
        float avgW = 0;
        int nDriving = 0;
        for (int i = 0; i < NWheels; i++)
        {
            if (Susp[i].Driving)
            {
                avgW += Susp[i].W;
                nDriving++;
            }
        }
        if (nDriving > 0) avgW /= nDriving;

        // Engine speed from wheel speed through gear ratio
        float targetW = MathF.Abs(avgW * gearRatio);

        // Blend engine speed based on clutch
        float clutchFactor = 1f - Clutch;
        float blendRate = ClutchSpeed > 0 ? ClutchSpeed : 5f;
        WEngine += (targetW * clutchFactor + EngineIdleW * Clutch - WEngine) * MathF.Min(1f, blendRate * dt);

        // Clamp engine RPM
        WEngine = MathF.Max(EngineMinW > 0 ? EngineMinW : EngineIdleW * 0.5f,
                            MathF.Min(EngineMaxW > 0 ? EngineMaxW : 1000f, WEngine));
    }

    /// <summary>
    /// Automatic gear shifting based on engine RPM thresholds.
    /// </summary>
    private void AutoShiftGears()
    {
        if (NGears <= 1) return;

        // Shift up when engine exceeds shift-up RPM
        if (EngineShiftUpW > 0 && WEngine >= EngineShiftUpW && CurGear < MaxGear && CurGear < NGears - 1)
        {
            CurGear++;
        }
        // Shift down when engine falls below shift-down RPM
        else if (EngineShiftDownW > 0 && WEngine <= EngineShiftDownW && CurGear > MinGear && CurGear > 0)
        {
            CurGear--;
        }
    }
}

// ============================================================================
// Vehicle-specific params/actions/status
// ============================================================================

public class ParamsCar : PhysicsParamsBase
{
    public override int TypeId => 16;
    public float? AxleFriction { get; set; }
    public float? EnginePower { get; set; }
    public float? MaxSteer { get; set; }
    public float? EngineMaxRPM { get; set; }
    public float? BrakeTorque { get; set; }
    public float? EngineMinRPM { get; set; }
    public float? EngineIdleRPM { get; set; }
    public float? EngineShiftUpRPM { get; set; }
    public float? EngineShiftDownRPM { get; set; }
    public float? EngineStartRPM { get; set; }
    public float? ClutchSpeed { get; set; }
    public int? NGears { get; set; }
    public float[]? GearRatios { get; set; }
    public int? MaxGear { get; set; }
    public int? MinGear { get; set; }
    public float? MinBrakingFriction { get; set; }
    public float? MaxBrakingFriction { get; set; }
    public float? KDynFriction { get; set; }
    public float? SlipThreshold { get; set; }
    public float? KStabilizer { get; set; }
    public int? NWheels { get; set; }
    public float? GearDirSwitchRPM { get; set; }
    public float? SteerTrackNeutralTurn { get; set; }
    public float? PullTilt { get; set; }
    public float? MaxTilt { get; set; }
    public bool? KeepTractionWhenTilted { get; set; }
}

public class ParamsWheel : PhysicsParamsBase
{
    public override int TypeId => 17;
    public int? IWheel { get; set; }
    public bool? Driving { get; set; }
    public int? Axle { get; set; }
    public bool? CanBrake { get; set; }
    public bool? Blocked { get; set; }
    public bool? CanSteer { get; set; }
    public float? SuspLenMax { get; set; }
    public float? SuspLenInitial { get; set; }
    public float? MinFriction { get; set; }
    public float? MaxFriction { get; set; }
    public int? SurfaceIdx { get; set; }
    public bool? BRayCast { get; set; }
    public float? KStiffness { get; set; }
    public float? KStiffnessWeight { get; set; }
    public float? KDamping { get; set; }
    public float? KLatFriction { get; set; }
    public float? Tscale { get; set; }
    public float? W { get; set; }
}

public class ActionDrive : PhysicsActionBase
{
    public override int TypeId => 2;
    public float? Pedal { get; set; }
    public float? DPedal { get; set; }
    public float? Steer { get; set; }
    public float? AckermanOffset { get; set; }
    public float? DSteer { get; set; }
    public float? Clutch { get; set; }
    public bool? HandBrake { get; set; }
    public int? IGear { get; set; }
}

public class StatusVehicle : PhysicsStatusBase
{
    public override int TypeId => 5;
    public float Steer { get; set; }
    public float Pedal { get; set; }
    public bool HandBrake { get; set; }
    public float Footbrake { get; set; }
    public PhysVector3 Vel { get; set; }
    public bool WheelContact { get; set; }
    public int CurGear { get; set; }
    public float EngineRPM { get; set; }
    public float Clutch { get; set; }
    public float DrivingTorque { get; set; }
    public int NActiveColliders { get; set; }
}

public class StatusWheel : PhysicsStatusBase
{
    public override int TypeId => 6;
    public int IWheel { get; set; }
    public int PartId { get; set; }
    public bool Contact { get; set; }
    public PhysVector3 PtContact { get; set; }
    public PhysVector3 NormContact { get; set; }
    public float W { get; set; }
    public bool Slip { get; set; }
    public PhysVector3 VelSlip { get; set; }
    public int ContactSurfaceIdx { get; set; }
    public float Friction { get; set; }
    public float SuspLen { get; set; }
    public float SuspLenFull { get; set; }
    public float SuspLen0 { get; set; }
    public float R { get; set; }
    public float Torque { get; set; }
    public float Steer { get; set; }
    public IPhysicalEntity? ContactEnt { get; set; }
}
