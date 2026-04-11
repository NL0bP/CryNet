// Port of physinterface.h - pe_params_*, pe_action_*, pe_status_* structs
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Params;

/// <summary>Physics entity type. Port of pe_type enum.</summary>
public enum PhysicsEntityType
{
    None = 0,
    Static = 1,
    Rigid = 2,
    WheeledVehicle = 3,
    Living = 4,
    Particle = 5,
    Articulated = 6,
    Rope = 7,
    Soft = 8,
    Area = 9
}

/// <summary>Simulation class. Port of sim_class enum.</summary>
public enum SimClass
{
    Static = 0,
    SleepingRigid = 1,
    ActiveRigid = 2,
    Living = 3,
    Independent = 4,
    Trigger = 6,
    Deleted = 7
}

// ============================================================================
// Base classes
// ============================================================================

/// <summary>Base class for all physics parameter structs.</summary>
public abstract class PhysicsParamsBase
{
    public abstract int TypeId { get; }
}

/// <summary>Base class for all physics action structs.</summary>
public abstract class PhysicsActionBase
{
    public abstract int TypeId { get; }
}

/// <summary>Base class for all physics status structs.</summary>
public abstract class PhysicsStatusBase
{
    public abstract int TypeId { get; }
}

// ============================================================================
// Parameter types (27 types)
// ============================================================================

/// <summary>Position/orientation parameters. Port of pe_params_pos.</summary>
public class ParamsPos : PhysicsParamsBase
{
    public override int TypeId => 0;

    public PhysVector3? Position { get; set; }
    public PhysQuaternion? Orientation { get; set; }
    public float? Scale { get; set; }
    public int? SimClass { get; set; }
    public bool RecalcBounds { get; set; } = true;
    public PhysMatrix33? BasisMatrix { get; set; }
    public int? ISimClass { get; set; }
}

/// <summary>Bounding box parameters. Port of pe_params_bbox.</summary>
public class ParamsBBox : PhysicsParamsBase
{
    public override int TypeId => 1;
    public PhysVector3? BBoxMin { get; set; }
    public PhysVector3? BBoxMax { get; set; }
}

/// <summary>Part geometry parameters. Port of pe_params_part.</summary>
public class ParamsPart : PhysicsParamsBase
{
    public override int TypeId => 2;

    public int PartId { get; set; } = -1;
    public int? IsPart { get; set; }
    public PhysVector3? Position { get; set; }
    public PhysQuaternion? Orientation { get; set; }
    public float? Scale { get; set; }
    public float? Mass { get; set; }
    public float? Density { get; set; }
    public int? FlagsAnd { get; set; }
    public int? FlagsOr { get; set; }
    public int? FlagsColliderAnd { get; set; }
    public int? FlagsColliderOr { get; set; }
    public float? MinContactDist { get; set; }
    public int? IdMaterial { get; set; }
}

/// <summary>Entity flags. Port of pe_params_flags.</summary>
public class ParamsFlags : PhysicsParamsBase
{
    public override int TypeId => 5;
    public uint? FlagsAnd { get; set; }
    public uint? FlagsOr { get; set; }
}

/// <summary>Simulation parameters. Port of pe_simulation_params.</summary>
public class SimulationParams : PhysicsParamsBase
{
    public override int TypeId => 7;

    public float? MaxTimeStep { get; set; }
    public float? MinEnergy { get; set; }
    public PhysVector3? Gravity { get; set; }
    public float? GravityFreefall { get; set; }
    public float? Damping { get; set; }
    public float? DampingFreefall { get; set; }
    public int? MaxLoggedCollisions { get; set; }
    public float? MaxRotVel { get; set; }
    public int? DisablePreCG { get; set; }
    public float? MaxFriction { get; set; }
    public int? ISoftness { get; set; }
}

/// <summary>Player dimensions. Port of pe_player_dimensions.</summary>
public class PlayerDimensions : PhysicsParamsBase
{
    public override int TypeId => 8;

    public float? HeightPivot { get; set; }
    public float? HeightEye { get; set; }
    public PhysVector3? SizeCollider { get; set; }
    public float? HeightCollider { get; set; }
    public float? HeadRadius { get; set; }
    public float? HeightHead { get; set; }
    public PhysVector3? DirUnproj { get; set; }
    public float? MaxUnproj { get; set; }
    public bool? UseCapsule { get; set; }
    public float? GroundContactEps { get; set; }
}

/// <summary>Player dynamics. Port of pe_player_dynamics.</summary>
public class PlayerDynamics : PhysicsParamsBase
{
    public override int TypeId => 9;

    public float? KInertia { get; set; }
    public float? KInertiaAccel { get; set; }
    public float? KAirControl { get; set; }
    public float? KAirResistance { get; set; }
    public PhysVector3? Gravity { get; set; }
    public float? NodSpeed { get; set; }
    public bool? IsSwimming { get; set; }
    public float? Mass { get; set; }
    public int? Surface_idx { get; set; }
    public float? MinSlideAngle { get; set; }
    public float? MaxClimbAngle { get; set; }
    public float? MaxJumpAngle { get; set; }
    public float? MinFallAngle { get; set; }
    public float? MaxVelGround { get; set; }
}

/// <summary>Buoyancy parameters. Port of pe_params_buoyancy.</summary>
public class ParamsBuoyancy : PhysicsParamsBase
{
    public override int TypeId => 15;

    public PhysVector3? WaterPlaneNormal { get; set; }
    public PhysVector3? WaterPlaneOrigin { get; set; }
    public float? WaterDensity { get; set; }
    public float? WaterDamping { get; set; }
    public float? WaterResistance { get; set; }
    public PhysVector3? WaterFlow { get; set; }
    public float? WaterEmin { get; set; }
}

/// <summary>Foreign data parameters. Port of pe_params_foreign_data.</summary>
public class ParamsForeignData : PhysicsParamsBase
{
    public override int TypeId => 6;

    public object? ForeignData { get; set; }
    public int? IForeignData { get; set; }
    public int? ForeignFlags { get; set; }
}

// ============================================================================
// Action types (19 types)
// ============================================================================

/// <summary>Apply impulse action. Port of pe_action_impulse.</summary>
public class ActionImpulse : PhysicsActionBase
{
    public override int TypeId => 0;

    public PhysVector3? Impulse { get; set; }
    public PhysVector3? AngImpulse { get; set; }
    public PhysVector3? Point { get; set; }
    public int PartId { get; set; } = -1;
    public int? IApplyTime { get; set; } // 0=instant, 1=next step, 2=over step
    public int? ISource { get; set; }
}

/// <summary>Reset action. Port of pe_action_reset.</summary>
public class ActionReset : PhysicsActionBase
{
    public override int TypeId => 1;
    public int? BResetType { get; set; }
}

/// <summary>Set velocity action. Port of pe_action_set_velocity.</summary>
public class ActionSetVelocity : PhysicsActionBase
{
    public override int TypeId => 5;

    public PhysVector3? Velocity { get; set; }
    public PhysVector3? AngularVelocity { get; set; }
    public int PartId { get; set; } = -1;
}

/// <summary>Move action (for living entities). Port of pe_action_move.</summary>
public class ActionMove : PhysicsActionBase
{
    public override int TypeId => 3;
    public PhysVector3? Dir { get; set; }
    public int? IJump { get; set; }
    public float? DT { get; set; }
}

/// <summary>Awake action. Port of pe_action_awake.</summary>
public class ActionAwake : PhysicsActionBase
{
    public override int TypeId => 4;
    public bool? Awake { get; set; }
    public float? MinAwakeTime { get; set; }
}

/// <summary>Add constraint action. Port of pe_action_add_constraint.</summary>
public class ActionAddConstraint : PhysicsActionBase
{
    public override int TypeId => 6;

    public int? Id { get; set; }
    public object? PBuddy { get; set; } // IPhysicalEntity reference
    public PhysVector3? Pt0 { get; set; }
    public PhysVector3? Pt1 { get; set; }
    public int PartId0 { get; set; } = -1;
    public int PartId1 { get; set; } = -1;
    public PhysQuaternion? QFrame0 { get; set; }
    public PhysQuaternion? QFrame1 { get; set; }
    public float? MaxPullForce { get; set; }
    public float? MaxBendTorque { get; set; }
    public uint? Flags { get; set; }
}

// ============================================================================
// Status types (27 types)
// ============================================================================

/// <summary>Position/velocity status. Port of pe_status_pos.</summary>
public class StatusPos : PhysicsStatusBase
{
    public override int TypeId => 0;

    // Input
    public int PartId { get; set; } = -1;
    public int? Flags { get; set; }

    // Output
    public PhysVector3 Position { get; set; }
    public PhysQuaternion Orientation { get; set; }
    public float Scale { get; set; } = 1f;
    public PhysVector3 BBoxMin { get; set; }
    public PhysVector3 BBoxMax { get; set; }
    public int SimClass { get; set; }

    // C++ field-name aliases (pe_status_pos uses `pos`, `q`, `scale`, `BBox[0]/[1]`).
    // Patched in by the CryAISystem.Sharp port to preserve literal C++ field-access.
    public PhysVector3 pos { get => Position; set => Position = value; }
    public PhysQuaternion q { get => Orientation; set => Orientation = value; }
    public float scale { get => Scale; set => Scale = value; }

    /// Port of pe_status_pos's `Vec3 BBox[2]` array — returns a 2-element array view.
    /// Read accessors mirror the C++ `status.BBox[0]` / `status.BBox[1]` syntax.
    public PhysVector3[] BBox
    {
        get { return new PhysVector3[] { BBoxMin, BBoxMax }; }
    }
}

/// <summary>Dynamics status. Port of pe_status_dynamics.</summary>
public class StatusDynamics : PhysicsStatusBase
{
    public override int TypeId => 3;

    // Input
    public int PartId { get; set; } = -1;

    // Output
    public PhysVector3 Velocity { get; set; }
    public PhysVector3 AngularVelocity { get; set; }
    public PhysVector3 Acceleration { get; set; }
    public PhysVector3 AngularAcceleration { get; set; }
    public float Mass { get; set; }
    public float Energy { get; set; }
    public PhysVector3 CenterOfMass { get; set; }
    public float SubmergedFraction { get; set; }  // 0..1 fraction, not int
    public float TimeIdle { get; set; }  // seconds, not int
}

/// <summary>Living entity status. Port of pe_status_living.</summary>
public class StatusLiving : PhysicsStatusBase
{
    public override int TypeId => 4;

    // Output
    public bool IsFlying { get; set; }
    public float TimeFlying { get; set; }
    public PhysVector3 CamOffset { get; set; }
    public PhysVector3 Velocity { get; set; }
    public PhysVector3 VelRequested { get; set; }
    public PhysVector3 VelGround { get; set; }
    public PhysVector3 GroundSlope { get; set; }
    public int GroundSurfaceIdx { get; set; }
    public float GroundHeight { get; set; }
    public object? GroundEntity { get; set; }  // IPhysicalEntity reference
}

/// <summary>Collision events status. Port of pe_status_collisions.</summary>
public class StatusCollisions : PhysicsStatusBase
{
    public override int TypeId => 8;

    public int? MaxCollisions { get; set; }

    // Output: filled by GetStatus
    public PhysVector3[] ContactPoints { get; set; } = Array.Empty<PhysVector3>();
    public PhysVector3[] ContactNormals { get; set; } = Array.Empty<PhysVector3>();
    public float[] ContactImpulses { get; set; } = Array.Empty<float>();
    public int CollisionCount { get; set; }
}
