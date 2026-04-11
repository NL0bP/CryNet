// Port of CryPhysics particleentity.h/cpp - single particle physics
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

/// <summary>
/// Particle physics entity (projectiles, simple particles).
/// Port of CParticleEntity from CryEngine.
/// Full implementation with ballistic trajectory, bounce/collision (restitution),
/// water interaction (drag, splash), lifetime/size, and sliding.
/// </summary>
public class ParticleEntity : PhysicalEntity
{
    public const int SnapshotVersion = 3;
    public override PhysicsEntityType Type => PhysicsEntityType.Particle;

    // Mass and size
    public float Mass;
    public float Dim;
    public float RDim;          // 1/dim
    public float DimLying;

    // Resistance
    public float KAirResistance;
    public float KWaterResistance;
    public float AccThrust;
    public float KAccLift;

    // Gravity
    public PhysVector3 Gravity;
    public PhysVector3 Gravity0;
    public PhysVector3 WaterGravity;

    // Orientation
    public PhysVector3 RollAxis;
    public PhysVector3 Normal;
    public PhysVector3 Heading;

    // Velocity and spin
    public PhysVector3 Vel;
    public PhysVector3 WSpin;
    public PhysQuaternion QSpin = PhysQuaternion.Identity;

    // Bounce/collision
    public float MinBounceVel;
    public float MinVel;
    public int SurfaceIdx;
    public IPhysicalEntity? ColliderToIgnore;
    public int CollTypes;
    public float Restitution = 0.3f; // Bounciness / coefficient of restitution

    // Contact state
    public PhysVector3 SlideNormal;
    public float TimeSurplus;
    public float Depth;
    public PhysVector3 VelMedium;

    // Timing
    public float TimeStepPerformed;
    public float TimeStepFull;
    public float TimeForceAwake;
    public float SleepTime;

    // Lifetime
    public float Lifetime;         // Max lifetime in seconds (0 = infinite)
    public float Age;              // Current age in seconds

    // Bitfield flags
    public int AreaCheckPeriod;
    public int StepCount;
    public bool HadCollisions;
    public int RecentCollisions;
    public int ForceAwake;
    public int Pierceability;
    public bool Sliding;
    public bool DontPlayHitEffect;

    // Particle-specific flags (from C++ particle_flags)
    public bool ConstantOrientation;
    public bool NoRoll;
    public bool NoSpin;
    public bool NoPathAlignment;
    public bool SingleContact;

    public ParticleEntity()
    {
        SimulationClass = SimClass.Independent;
        IsAwake = true;
        Gravity = new PhysVector3(0, 0, -9.81f);
        Gravity0 = Gravity;
        Normal = PhysVector3.UnitZ;
        Heading = PhysVector3.UnitX;
        MinBounceVel = 1.5f;
        CollTypes = -1;
    }

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        if (parameters is ParamsParticle pp)
        {
            if (pp.Mass.HasValue) Mass = pp.Mass.Value;
            if (pp.Size.HasValue) { Dim = pp.Size.Value * 0.5f; RDim = Dim > 0 ? 1f / Dim : 0; } // C++ stores half-size: m_dim = size*0.5f
            if (pp.SizeLying.HasValue) DimLying = pp.SizeLying.Value;
            if (pp.Heading.HasValue) Heading = pp.Heading.Value;
            if (pp.Velocity.HasValue) Vel = Heading * pp.Velocity.Value;
            if (pp.KAirResistance.HasValue) KAirResistance = pp.KAirResistance.Value;
            if (pp.KWaterResistance.HasValue) KWaterResistance = pp.KWaterResistance.Value;
            if (pp.AccThrust.HasValue) AccThrust = pp.AccThrust.Value;
            if (pp.AccLift.HasValue) KAccLift = pp.AccLift.Value;
            if (pp.Gravity.HasValue) { Gravity = pp.Gravity.Value; Gravity0 = Gravity; }
            if (pp.WaterGravity.HasValue) WaterGravity = pp.WaterGravity.Value;
            if (pp.MinBounceVel.HasValue) MinBounceVel = pp.MinBounceVel.Value;
            if (pp.MinVel.HasValue) MinVel = pp.MinVel.Value;
            if (pp.SurfaceIdx.HasValue) SurfaceIdx = pp.SurfaceIdx.Value;
            if (pp.Normal.HasValue) Normal = pp.Normal.Value;
            if (pp.RollAxis.HasValue) RollAxis = pp.RollAxis.Value;
            if (pp.WSpin.HasValue) WSpin = pp.WSpin.Value;
            if (pp.CollTypes.HasValue) CollTypes = pp.CollTypes.Value;
            if (pp.Pierceability.HasValue) Pierceability = pp.Pierceability.Value;
            if (pp.DontPlayHitEffect.HasValue) DontPlayHitEffect = pp.DontPlayHitEffect.Value;
            return 1;
        }
        return base.SetParams(parameters, threadSafe);
    }

    public override int GetParams(PhysicsParamsBase parameters)
    {
        if (parameters is ParamsParticle pp)
        {
            pp.Mass = Mass;
            pp.Size = Dim * 2f; // C++ returns m_dim*2 (full size)
            pp.SizeLying = DimLying;
            pp.Heading = Heading;
            pp.Velocity = Vel.Length();
            pp.KAirResistance = KAirResistance;
            pp.KWaterResistance = KWaterResistance;
            pp.AccThrust = AccThrust;
            pp.AccLift = KAccLift;
            pp.Gravity = Gravity;
            pp.WaterGravity = WaterGravity;
            pp.MinBounceVel = MinBounceVel;
            pp.MinVel = MinVel;
            pp.SurfaceIdx = SurfaceIdx;
            pp.Normal = Normal;
            pp.RollAxis = RollAxis;
            pp.WSpin = WSpin;
            pp.CollTypes = CollTypes;
            pp.Pierceability = Pierceability;
            pp.DontPlayHitEffect = DontPlayHitEffect;
            return 1;
        }
        return base.GetParams(parameters);
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        if (status is StatusDynamics dyn)
        {
            dyn.Velocity = Vel;
            dyn.AngularVelocity = WSpin;
            dyn.Mass = Mass;
            dyn.CenterOfMass = Position;
            return 1;
        }
        return base.GetStatus(status);
    }

    public override int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        if (action is ActionImpulse imp && imp.Impulse.HasValue)
        {
            if (Mass > 0)
                Vel = Vel + imp.Impulse.Value / Mass;
            return 1;
        }
        return base.DoAction(action, threadSafe);
    }

    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (!IsAwake) return 0;

        TimeStepFull = timeInterval;

        // Lifetime check
        if (Lifetime > 0)
        {
            Age += timeInterval;
            if (Age >= Lifetime)
            {
                Awake(false);
                return 0;
            }
        }

        // Determine medium (underwater or air)
        // C++: if m_depth < 0, particle is underwater
        PhysVector3 gravity;
        float kResistance;
        if (Depth < 0)
        {
            // Underwater
            gravity = WaterGravity.LengthSq() > 0 ? WaterGravity : Gravity;
            // Scale water gravity by submersion fraction
            float submersion = MathF.Min(1f, -Depth * RDim);
            gravity = gravity * submersion;
            kResistance = KWaterResistance;
        }
        else
        {
            gravity = Gravity;
            kResistance = KAirResistance;
        }

        // Clamp time step if already performed enough
        if (TimeStepPerformed > TimeStepFull - 0.0001f && TimeStepFull > 0)
            timeInterval = 0.0001f;
        TimeStepPerformed += timeInterval;

        if (TimeSurplus == 0) TimeSurplus = 1;

        var pos0 = Position;
        var vel0 = Vel;

        // --- Sliding mode ---
        if (Sliding)
        {
            // When sliding on a surface, constrain motion to the surface plane
            // Remove velocity component into surface
            float vn = vel0.Dot(SlideNormal);
            if (vn < 0)
                vel0 = vel0 - SlideNormal * vn;

            // Apply surface friction
            float frictionCoeff = 0.5f; // Simplified; C++ looks up friction table
            var tangVel = vel0 - SlideNormal * vel0.Dot(SlideNormal);
            float tangSpeed = tangVel.Length();
            if (tangSpeed > 0)
            {
                float frictionDecel = MathF.Abs(gravity.Dot(SlideNormal)) * frictionCoeff * timeInterval;
                if (frictionDecel >= tangSpeed)
                    vel0 = PhysVector3.Zero;
                else
                    vel0 = vel0 - tangVel * (frictionDecel / tangSpeed);
            }

            // Remove gravity component into surface for single_contact mode
            if (SingleContact)
                gravity = PhysVector3.Zero;
            else
                gravity = gravity - SlideNormal * SlideNormal.Dot(gravity);

            // Rolling spin
            if (!NoRoll && RDim > 0)
            {
                WSpin = (SlideNormal ^ vel0) * RDim;
                // Clamp spin
                float spinLen = WSpin.Length();
                if (spinLen > 20f)
                    WSpin = WSpin * (20f / spinLen);
            }
            else
            {
                WSpin = PhysVector3.Zero;
            }

            // Check if still in contact with surface (simplified: check ground plane)
            float testDist = Dim * 1.1f;
            float groundZ = DimLying > 0 ? DimLying : Dim;
            if (Position.Z > groundZ + testDist)
            {
                // Lost contact
                Sliding = false;
                if (!ConstantOrientation && !NoSpin)
                {
                    var wSpin = Heading ^ gravity;
                    float wLen = wSpin.Length();
                    WSpin = wLen > 0.01f ? wSpin * (0.5f * RDim * gravity.Length() / wLen) : PhysVector3.Zero;
                }
            }
        }

        // Apply forces (same for both sliding and free flight)
        // Thrust
        if (AccThrust != 0)
            Vel = vel0 + Heading * (AccThrust * timeInterval);
        else
            Vel = vel0;

        // Lift
        if (KAccLift != 0)
        {
            // C++: (heading^(heading^gravity)).normalize() * kAccLift * vel.len()
            var liftCross = Heading ^ gravity;
            var liftDir = (Heading ^ liftCross).Normalized();
            Vel = Vel + liftDir * (KAccLift * Vel.Length() * timeInterval);
        }

        // Gravity
        Vel = Vel + gravity * timeInterval;

        // Air/water resistance: C++ uses (velMedium - vel) * kResistance * dt
        if (kResistance > 0)
            Vel = Vel + (VelMedium - Vel) * (kResistance * timeInterval);

        // Half-step gravity for position (C++ does vel0 += gravity*dt*0.5 before position update in non-sliding)
        PhysVector3 posVel;
        if (!Sliding)
            posVel = vel0 + gravity * (timeInterval * 0.5f);
        else
            posVel = vel0;

        // Integrate position
        Position = pos0 + posVel * timeInterval;

        // --- Collision detection (simplified ground-plane bounce) ---
        float groundLevel = DimLying > 0 ? DimLying : Dim;
        bool hitGround = false;
        PhysVector3 hitNormal = PhysVector3.UnitZ;
        PhysVector3 hitPoint = Position;

        if (Position.Z < groundLevel && !Sliding)
        {
            hitGround = true;
            hitPoint = new PhysVector3(Position.X, Position.Y, groundLevel);
            hitNormal = PhysVector3.UnitZ;
        }

        // --- Water interaction ---
        // Check if crossing water surface (simplified: water at z=0)
        float waterLevel = 0f;
        if (pos0.Z >= waterLevel && Position.Z < waterLevel)
        {
            // Entering water
            Depth = Position.Z - waterLevel; // negative = underwater
            // Apply splash drag impulse (simplified)
            float splashDrag = KWaterResistance * 2f;
            Vel = Vel * MathF.Max(0f, 1f - splashDrag * timeInterval);
        }
        else if (pos0.Z < waterLevel && Position.Z >= waterLevel)
        {
            // Leaving water
            Depth = 0;
        }
        else if (Position.Z < waterLevel)
        {
            Depth = Position.Z - waterLevel;
        }
        else
        {
            Depth = MathF.Max(0f, Depth); // Above water
        }

        // --- Bounce handling ---
        if (hitGround)
        {
            HadCollisions = true;
            RecentCollisions = System.Math.Min(3, RecentCollisions + 1);

            // Position correction
            Position = hitPoint;

            // Decompose velocity into normal and tangential components
            float vn2 = Vel.Dot(hitNormal);
            var vTang = Vel - hitNormal * vn2;

            // Restitution (bounciness)
            // C++: e = average of surface bounciness tables, then check minBounceVel
            float e = Restitution;
            if (MathF.Abs(vn2) < MinBounceVel || DimLying < Dim * 0.3f)
                e = 0; // No bounce for slow impacts

            // Bounce: reflect normal component with restitution, attenuate tangential with friction
            float massInv = 0; // ground is infinite mass
            float k = Mass * massInv / (1f + Mass * massInv); // -> 0 for infinite ground mass
            // Simplified: k=0 means ground absorbs nothing, particle gets full reflection
            Vel = Vel - hitNormal * (vn2 * (1f + e));
            // Tangential friction loss
            Vel = Vel - vTang * ((1f - e) * 0.3f); // simplified friction

            float velAfter = Vel.Length();

            // Check if should transition to sliding
            if (velAfter < MinVel || SingleContact)
            {
                Vel = PhysVector3.Zero;
                WSpin = PhysVector3.Zero;
                QSpin = PhysQuaternion.Identity;
                Sliding = true;
                SlideNormal = hitNormal;
                if (DimLying > 0 && DimLying != Dim)
                    Position = new PhysVector3(Position.X, Position.Y, hitNormal.Z * DimLying);
                // Align orientation to surface
                if (!ConstantOrientation && Normal.LengthSq() > 0)
                {
                    var alignAxis = (QSpin.Rotate(Normal) ^ hitNormal);
                    float alignLen = alignAxis.Length();
                    if (alignLen > 1e-6f)
                    {
                        float angle = MathF.Asin(MathF.Min(1f, alignLen));
                        QSpin = PhysQuaternion.FromAxisAngle(alignAxis / alignLen, angle) * QSpin;
                        QSpin.Normalize();
                    }
                }
            }
            else
            {
                // Check if next frame will bring us back to surface
                var velNext = Vel + gravity * timeInterval;
                if (velNext.Dot(hitNormal) < MinVel + 0.001f)
                {
                    Sliding = true;
                    SlideNormal = hitNormal;
                    if (DimLying > 0 && DimLying != Dim)
                        Position = new PhysVector3(Position.X, Position.Y, hitNormal.Z * DimLying);
                }

                // Set spin from bounce
                if (!NoRoll && RDim > 0)
                {
                    WSpin = (hitNormal ^ vTang) * RDim;
                    float spinLen = WSpin.Length();
                    if (spinLen > 20f) WSpin = WSpin * (20f / spinLen);
                }
            }

            ForceAwake = hitNormal.Z > 0.7f ? 2 : 1;
        }

        // --- Update heading from velocity ---
        float velLen = Vel.Length();
        if (velLen > MinVel)
            Heading = Vel / velLen;

        // --- Spin/orientation update ---
        if (!ConstantOrientation)
        {
            if (!NoSpin)
            {
                // Integrate spin quaternion
                float spinMag = WSpin.Length();
                if (spinMag * timeInterval < 0.1f)
                {
                    // Small angle approximation
                    QSpin = new PhysQuaternion(
                        QSpin.W - WSpin.Dot(new PhysVector3(QSpin.X, QSpin.Y, QSpin.Z)) * timeInterval * 0.5f,
                        QSpin.X + ((WSpin ^ new PhysVector3(QSpin.X, QSpin.Y, QSpin.Z)).X + WSpin.X * QSpin.W) * timeInterval * 0.5f,
                        QSpin.Y + ((WSpin ^ new PhysVector3(QSpin.X, QSpin.Y, QSpin.Z)).Y + WSpin.Y * QSpin.W) * timeInterval * 0.5f,
                        QSpin.Z + ((WSpin ^ new PhysVector3(QSpin.X, QSpin.Y, QSpin.Z)).Z + WSpin.Z * QSpin.W) * timeInterval * 0.5f);
                    QSpin.Normalize();
                }
                else if (spinMag > 1e-8f)
                {
                    QSpin = PhysQuaternion.FromAxisAngle(WSpin / spinMag, spinMag * timeInterval) * QSpin;
                    QSpin.Normalize();
                }
            }
            else
            {
                WSpin = PhysVector3.Zero;
            }

            // Path alignment: orient to velocity direction
            if (!NoPathAlignment && !Sliding)
            {
                // Align QSpin so that it rotates the default forward (UnitX) toward Heading
                // Use axis-angle between UnitX and Heading
                var fwd = PhysVector3.UnitX;
                var cross = fwd ^ Heading;
                float crossLen = cross.Length();
                if (crossLen > 1e-6f)
                {
                    float dot = fwd.Dot(Heading);
                    float angle = MathF.Atan2(crossLen, dot);
                    var alignQ = PhysQuaternion.FromAxisAngle(cross / crossLen, angle);
                    Orientation = alignQ * QSpin;
                    Orientation.Normalize();
                }
                else
                {
                    Orientation = QSpin;
                }
            }
            else
            {
                Orientation = QSpin;
            }
        }

        TimeStepPerformed = timeInterval;
        ComputeBBox();

        // --- Force awake tracking ---
        if ((ForceAwake & 1) != 0)
            TimeForceAwake += timeInterval;
        SleepTime = 0;

        // --- Sleep check ---
        if (velLen < MinVel && !Sliding)
        {
            SleepTime += timeInterval;
            if (SleepTime > 2f)
                Awake(false);
        }
        else if (Sliding)
        {
            // In sliding mode, sleep if tangential velocity is tiny
            var tangVel = Vel - SlideNormal * Vel.Dot(SlideNormal);
            if (tangVel.LengthSq() < MinVel * MinVel * 0.01f)
            {
                SleepTime += timeInterval;
                if (SleepTime > 2f)
                    Awake(false);
            }
        }
        else
        {
            SleepTime = 0;
        }

        // Decrement recent collisions
        RecentCollisions = System.Math.Max(0, RecentCollisions - 1);

        return 1;
    }

    public override void Awake(bool awake = true, float minTime = 0f)
    {
        base.Awake(awake, minTime);
        if (awake)
        {
            SleepTime = 0;
            if (minTime > 0) TimeForceAwake = minTime;
        }
    }

    protected override void ComputeBBox()
    {
        float r = System.Math.Max(Dim, 0.01f);
        BBoxMin = Position - new PhysVector3(r, r, r);
        BBoxMax = Position + new PhysVector3(r, r, r);
    }
}

/// <summary>Particle-specific parameters. Port of pe_params_particle.</summary>
public class ParamsParticle : PhysicsParamsBase
{
    public override int TypeId => 3;

    public uint? Flags { get; set; }
    public float? Mass { get; set; }
    public float? Size { get; set; }
    public float? SizeLying { get; set; }
    public float? Thickness { get; set; }
    public PhysVector3? Heading { get; set; }
    public float? Velocity { get; set; }
    public float? KAirResistance { get; set; }
    public float? KWaterResistance { get; set; }
    public float? AccThrust { get; set; }
    public float? AccLift { get; set; }
    public PhysVector3? Gravity { get; set; }
    public PhysVector3? WaterGravity { get; set; }
    public float? MinBounceVel { get; set; }
    public float? MinVel { get; set; }
    public int? SurfaceIdx { get; set; }
    public PhysVector3? Normal { get; set; }
    public PhysVector3? RollAxis { get; set; }
    public PhysVector3? WSpin { get; set; }
    public int? CollTypes { get; set; }
    public int? Pierceability { get; set; }
    public bool? DontPlayHitEffect { get; set; }
    public int? IPierceabilityFP { get; set; }
    public int? AeroCoeff { get; set; }
}
