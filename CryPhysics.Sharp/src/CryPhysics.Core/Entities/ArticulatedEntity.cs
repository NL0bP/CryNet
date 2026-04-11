// Port of CryPhysics articulatedentity.h - ragdoll/multi-body physics
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Dynamics;
using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

// ============================================================================
// Joint flag constants (port of C++ enum / defines)
// ============================================================================

public static class JointFlags
{
    public const uint Angle0Locked = 1;
    public const uint Angle1Locked = 2;
    public const uint Angle2Locked = 4;
    public const uint AllAnglesLocked = 7;
    public const uint Angle0LimitReached = 0x10;
    public const uint Angle1LimitReached = 0x20;
    public const uint Angle2LimitReached = 0x40;
    public const uint Angle0GimbalLocked = 0x100;
    public const uint Angle1GimbalLocked = 0x200;
    public const uint Angle2GimbalLocked = 0x400;
    public const uint JointNoGravity = 0x1000;
    public const uint JointIsolatedAccelerations = 0x2000;
    public const uint JointDashpotReached = 0x4000;
    public const uint JointRotatePivot = 0x8000000; // 010000000 octal
    public const uint Angle0AutoKd = 0x10000;
    public const uint Angle1AutoKd = 0x20000;
    public const uint Angle2AutoKd = 0x40000;
}

/// <summary>
/// Featherstone algorithm data per joint.
/// Port of featherstone_data from CryEngine.
/// </summary>
public class FeatherstoneData
{
    // Index mappings between q-index and axis-index
    public int[] Qidx2Axidx = new int[3];
    public int[] Axidx2Qidx = new int[3];

    public PhysVector3[] YaVec = new PhysVector3[2];
    public PhysVector3[] DvVec = new PhysVector3[2];
    public PhysVector3[,] SVec = new PhysVector3[3, 2];
    public PhysVector3 Q;
    public float[] QInv = new float[9];
    public float[] QInvDown = new float[9];
    public float[] S = new float[18];
    public float[,] Ia = new float[6, 6];
    public float[] IaS = new float[18];
    public float[,] IaSQInvST = new float[6, 6];
    public float[,] SQInvST = new float[6, 6];
    public float[,] SQInvSTIa = new float[6, 6];
    public float[,] QInvST = new float[3, 6];
    public float[,] QInvSTIa = new float[3, 6];
    public float[,] IInv = new float[6, 6];
}

/// <summary>
/// Articulated entity joint definition.
/// Port of ae_joint from CryEngine.
/// </summary>
public class AeJoint
{
    // Angular state
    public PhysVector3 Q;               // Joint angles (Euler)
    public PhysVector3 QExt;            // External angles
    public PhysVector3 Dq;              // Angular velocity
    public PhysVector3 DqExt;           // External angular velocity
    public PhysVector3 DqReq = new(float.NaN, float.NaN, float.NaN);  // Requested angular velocity (unused marker)
    public PhysVector3 DqLimit;
    public PhysVector3 Ddq;             // Angular acceleration
    public PhysQuaternion Quat = PhysQuaternion.Identity;

    // Previous state (for rollback)
    public PhysVector3 PrevQ;
    public PhysVector3 PrevDq;
    public PhysVector3 PrevPos;
    public PhysVector3 PrevV;
    public PhysVector3 PrevW;
    public PhysQuaternion PrevQRot = PhysQuaternion.Identity;
    public PhysVector3 Q0;

    // Collision forces
    public PhysVector3 FCollision;
    public PhysVector3 TCollision;
    public PhysVector3 VSleep;
    public PhysVector3 WSleep;

    // Joint configuration
    public uint Flags = JointFlags.AllAnglesLocked;
    public PhysQuaternion Quat0 = PhysQuaternion.Identity;
    public PhysVector3[] Limits = { new(-1e10f, -1e10f, -1e10f), new(1e10f, 1e10f, 1e10f) };
    public PhysVector3 Bounciness;
    public PhysVector3 Ks;              // Spring stiffness
    public PhysVector3 Kd;              // Damping
    public PhysVector3 QDashpot;
    public PhysVector3 KDashpot;
    public PhysVector3[] Pivot = new PhysVector3[2];

    // Hierarchy
    public int StartPart;
    public int NParts;
    public int Parent = -2;
    public int NChildren;
    public int NChildrenTree;
    public int Level;
    public ulong SelfCollMask;

    // Dynamics state
    public bool BAwake;
    public bool BQuat0Changed;
    public bool BHasExtContacts;
    public int IdBody = -1;
    public RigidBody Body = new();
    public PhysVector3 DvBody;
    public PhysVector3 DwBody;
    public PhysVector3 PExt;
    public PhysVector3 LExt;
    public PhysVector3 PImpact;
    public PhysVector3 LImpact;
    public int NActiveAngles;
    public int NPotentialAngles;
    public PhysVector3[] RotAxes = { PhysVector3.UnitX, PhysVector3.UnitY, PhysVector3.UnitZ };
    public PhysMatrix33 I = PhysMatrix33.Identity;
    public FeatherstoneData? Fs;

    /// <summary>Check if a dq_req component is "unused" (NaN marker, like C++ MARK_UNUSED).</summary>
    public bool IsDqReqUnused(int i) => float.IsNaN(DqReq[i]);
}

/// <summary>Per-part info for articulated entities.</summary>
public class AePartInfo
{
    public PhysVector3 Pos;
    public PhysQuaternion Q = PhysQuaternion.Identity;
    public float Scale = 1f;
    public PhysVector3[] BBox = new PhysVector3[2];
    public PhysQuaternion Q0 = PhysQuaternion.Identity;
    public PhysVector3 Pos0;
    public int JointIdx;
    public int IdBody;
    public PhysVector3[] PosHist = new PhysVector3[2];
    public PhysQuaternion[] QHist = { PhysQuaternion.Identity, PhysQuaternion.Identity };
}

/// <summary>
/// Articulated body entity (ragdolls, skeletal chains).
/// Port of CArticulatedEntity from CryEngine.
/// Uses Featherstone algorithm for O(n) dynamics.
/// </summary>
public class ArticulatedEntity : RigidEntity
{
    public const int SnapshotVersionArticulated = 6;
    public override PhysicsEntityType Type => PhysicsEntityType.Articulated;

    // Joint data
    public AePartInfo[] Infos = Array.Empty<AePartInfo>();
    public AeJoint[] Joints = Array.Empty<AeJoint>();
    public int NJoints;
    public int NJointsAlloc;

    // Pivot/root
    public PhysVector3 PosPivot;
    public PhysVector3 OffsPivot;
    public PhysVector3 Acc;
    public PhysVector3 WAcc;
    public PhysMatrix33 M0Inv = PhysMatrix33.Identity;

    // Featherstone root accumulator
    public PhysVector3[] YaVecRoot = new PhysVector3[2];

    // Simulation
    public float SimTime;
    public float SimTimeAux = 10f;
    public float ScaleBounceResponse = 1f;
    public bool IsGrounded;
    public int NRoots;
    public bool InheritVel;

    // Host entity
    public IPhysicalEntity? Host;
    public PhysVector3 PosHostPivot;
    public PhysQuaternion QHostPivot = PhysQuaternion.Identity;
    public PhysVector3 VelHost;
    public PhysVector3 RootImpulse;

    // Sim flags
    public bool CheckCollisions;
    public bool CollisionResp;
    public bool ExertImpulse;
    public int SimType = 1;
    public int SimTypeLyingMode = 1;
    public int SimTypeCur = 1;
    public int SimTypeOverride;
    public bool IaReady;
    public bool PartPosForced;
    public bool FastLimbs;

    // Contact tracking
    public float MaxPenetrationCur;
    public bool UsingUnproj;
    public PhysVector3 PrevPos;
    public PhysVector3 PrevVel;
    public bool UpdateBodies = true;
    public int NDynContacts;
    public bool InGroup;
    public bool IgnoreCommands;

    // Lying mode
    public int NCollLyingMode = 5;
    public PhysVector3 GravityLyingMode;
    public float DampingLyingMode = 0.2f;
    public float EminLyingMode = 0.12f * 0.12f;
    public int NBodyContacts;

    // Stepping
    public PhysVector3 PosNew;
    public PhysQuaternion QNew = PhysQuaternion.Identity;
    public new float MinEnergy = 0.025f * 0.025f;

    // History
    public PhysVector3[] PosHist = new PhysVector3[2];
    public PhysQuaternion[] QHist = { PhysQuaternion.Identity, PhysQuaternion.Identity };
    public float RHistTime;

    public ArticulatedEntity()
    {
        SimulationClass = SimClass.ActiveRigid;
        GravityLyingMode = Gravity;
        Damping = 0.1f;
    }

    // ========================================================================
    // SetParams / GetStatus
    // ========================================================================

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        if (parameters is ParamsJoint pj)
        {
            SetJointParams(pj);
            return 1;
        }
        if (parameters is ParamsArticulatedBody pab)
        {
            if (pab.IsGrounded.HasValue) IsGrounded = pab.IsGrounded.Value;
            if (pab.CheckCollisions.HasValue) CheckCollisions = pab.CheckCollisions.Value;
            if (pab.CollisionResp.HasValue) CollisionResp = pab.CollisionResp.Value;
            if (pab.Pivot.HasValue) PosPivot = pab.Pivot.Value;
            if (pab.ScaleBounceResponse.HasValue) ScaleBounceResponse = pab.ScaleBounceResponse.Value;
            if (pab.Host != null) Host = pab.Host as IPhysicalEntity;
            if (pab.InheritVel.HasValue) InheritVel = pab.InheritVel.Value;
            if (pab.SimType.HasValue) SimType = pab.SimType.Value;
            if (pab.NRoots.HasValue) NRoots = pab.NRoots.Value;
            if (pab.SimTypeLyingMode.HasValue) SimTypeLyingMode = pab.SimTypeLyingMode.Value;
            if (pab.NCollLyingMode.HasValue) NCollLyingMode = pab.NCollLyingMode.Value;
            if (pab.GravityLyingMode.HasValue) GravityLyingMode = pab.GravityLyingMode.Value;
            if (pab.DampingLyingMode.HasValue) DampingLyingMode = pab.DampingLyingMode.Value;
            if (pab.MinEnergyLyingMode.HasValue) EminLyingMode = pab.MinEnergyLyingMode.Value;
            if (pab.NJointsAlloc.HasValue && pab.NJointsAlloc.Value > NJointsAlloc)
            {
                NJointsAlloc = pab.NJointsAlloc.Value;
                var newJoints = new AeJoint[NJointsAlloc];
                Array.Copy(Joints, newJoints, NJoints);
                for (int i = NJoints; i < NJointsAlloc; i++) newJoints[i] = new AeJoint();
                Joints = newJoints;
            }
            return 1;
        }
        return base.SetParams(parameters, threadSafe);
    }

    private void SetJointParams(ParamsJoint pj)
    {
        int childBodyId = pj.Op1 ?? -1;
        int idx = -1;
        for (int i = 0; i < NJoints; i++)
        {
            if (Joints[i].IdBody == childBodyId)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0 || idx >= NJoints) return;
        var joint = Joints[idx];

        if (pj.Flags.HasValue)
            joint.Flags = pj.Flags.Value | (joint.Flags & pj.Flags.Value &
                ((JointFlags.Angle0LimitReached | JointFlags.Angle0GimbalLocked) * 7));
        if (pj.Pivot.HasValue) joint.Pivot[0] = pj.Pivot.Value;
        if (pj.Q0.HasValue) { joint.Quat0 = pj.Q0.Value; joint.BQuat0Changed = false; }
        if (pj.Limits0.HasValue) joint.Limits[0] = pj.Limits0.Value;
        if (pj.Limits1.HasValue) joint.Limits[1] = pj.Limits1.Value;
        if (pj.Bounciness.HasValue) joint.Bounciness = pj.Bounciness.Value;
        if (pj.Ks.HasValue) joint.Ks = pj.Ks.Value;
        if (pj.Kd.HasValue) joint.Kd = pj.Kd.Value;
        if (pj.QDashpot.HasValue) joint.QDashpot = pj.QDashpot.Value;
        if (pj.KDashpot.HasValue) joint.KDashpot = pj.KDashpot.Value;
        if (pj.Q.HasValue)
        {
            for (int i = 0; i < 3; i++)
                joint.PrevQ[i] = joint.Q[i] = pj.Q.Value[i];
        }
        if (pj.QExt.HasValue)
        {
            joint.QExt = pj.QExt.Value;
        }
        if (pj.QTarget.HasValue) joint.Q0 = pj.QTarget.Value;

        if (pj.BNoUpdate == null || !pj.BNoUpdate.Value)
        {
            for (int i = idx; i <= idx + joint.NChildrenTree; i++)
                SyncBodyWithJoint(i, 3);
        }
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        if (status is StatusJoint sj)
        {
            int idx = -1;
            for (int i = 0; i < NJoints; i++)
            {
                if (Joints[i].IdBody == sj.IdChildBody)
                {
                    idx = i;
                    break;
                }
            }
            if (idx >= 0 && idx < NJoints)
            {
                var joint = Joints[idx];
                sj.Flags = joint.Flags;
                sj.Q = joint.PrevQ;
                sj.QExt = joint.QExt;
                sj.Dq = joint.PrevDq;
                sj.Quat0 = joint.Quat0;
            }
            return 1;
        }
        return base.GetStatus(status);
    }

    // ========================================================================
    // Helper: CreateRotationXYZ (Euler XYZ -> quaternion)
    // ========================================================================

    /// <summary>
    /// Create quaternion from Euler angles (X,Y,Z order).
    /// Port of Quat::CreateRotationXYZ.
    /// </summary>
    public static PhysQuaternion CreateRotationXYZ(PhysVector3 angles)
    {
        var qx = PhysQuaternion.FromAxisAngle(PhysVector3.UnitX, angles.X);
        var qy = PhysQuaternion.FromAxisAngle(PhysVector3.UnitY, angles.Y);
        var qz = PhysQuaternion.FromAxisAngle(PhysVector3.UnitZ, angles.Z);
        return qx * qy * qz;
    }

    /// <summary>
    /// Extract Euler XYZ angles from rotation matrix.
    /// Port of Ang3::GetAnglesXYZ(Matrix33).
    /// </summary>
    public static PhysVector3 GetAnglesXYZ(PhysMatrix33 m)
    {
        float sy = -m.M02;
        sy = MathF.Max(-1f, MathF.Min(1f, sy));
        float y = MathF.Asin(sy);
        float cy = MathF.Cos(y);
        float x, z;
        if (MathF.Abs(cy) > 1e-6f)
        {
            float rcy = 1f / cy;
            x = MathF.Atan2(m.M12 * rcy, m.M22 * rcy);
            z = MathF.Atan2(m.M01 * rcy, m.M00 * rcy);
        }
        else
        {
            x = MathF.Atan2(-m.M21, m.M11);
            z = 0;
        }
        return new PhysVector3(x, y, z);
    }

    /// <summary>
    /// Rotate vector around an arbitrary axis by cos/sin of angle.
    /// Port of Vec3::GetRotated.
    /// </summary>
    private static PhysVector3 GetRotated(PhysVector3 v, PhysVector3 axis, float cosa, float sina)
    {
        float dot = v.Dot(axis);
        return v * cosa + axis * (dot * (1f - cosa)) + (axis.Cross(v)) * sina;
    }

    // ========================================================================
    // UpdateJointRotationAxes
    // ========================================================================

    /// <summary>
    /// Compute the 3 rotation axes for a joint considering current angles.
    /// Port of CArticulatedEntity::UpdateJointRotationAxes.
    /// </summary>
    public void UpdateJointRotationAxes(int idx)
    {
        var qParent = Joints[idx].Parent >= 0 ? Joints[Joints[idx].Parent].Quat : QNew;
        var qBasis = qParent * Joints[idx].Quat0;
        var R = new PhysMatrix33(qBasis);

        // SetBasisTFromMtx: columns of rotation matrix = rotation axes
        Joints[idx].RotAxes[0] = R.GetColumn(0);
        Joints[idx].RotAxes[1] = R.GetColumn(1);
        Joints[idx].RotAxes[2] = R.GetColumn(2);

        // Rotate x axis around y (pitch) then around z (yaw)
        float angle = Joints[idx].Q.Y + Joints[idx].QExt.Y;
        float cosa = MathF.Cos(angle), sina = MathF.Sin(angle);
        Joints[idx].RotAxes[0] = GetRotated(Joints[idx].RotAxes[0], Joints[idx].RotAxes[1], cosa, sina);

        angle = Joints[idx].Q.Z + Joints[idx].QExt.Z;
        cosa = MathF.Cos(angle); sina = MathF.Sin(angle);
        Joints[idx].RotAxes[0] = GetRotated(Joints[idx].RotAxes[0], Joints[idx].RotAxes[2], cosa, sina);
        Joints[idx].RotAxes[1] = GetRotated(Joints[idx].RotAxes[1], Joints[idx].RotAxes[2], cosa, sina);
    }

    // ========================================================================
    // CheckForGimbalLock
    // ========================================================================

    /// <summary>Port of CArticulatedEntity::CheckForGimbalLock.</summary>
    public void CheckForGimbalLock(int idx)
    {
        Joints[idx].Flags &= ~(JointFlags.Angle0GimbalLocked * 7);
        if ((Joints[idx].Flags & JointFlags.Angle0Locked * 5) == 0)
        {
            float dot02 = Joints[idx].RotAxes[0].Dot(Joints[idx].RotAxes[2]);
            if (dot02 * dot02 > 0.999f * 0.999f)
            {
                // Check if 3dof without limits -> rotate quat0
                if ((Joints[idx].Flags & JointFlags.AllAnglesLocked) == 0 &&
                    MathF.Abs(Joints[idx].Limits[1].X - Joints[idx].Limits[0].X) > 10 &&
                    MathF.Abs(Joints[idx].Limits[1].Y - Joints[idx].Limits[0].Y) > 10 &&
                    MathF.Abs(Joints[idx].Limits[1].Z - Joints[idx].Limits[0].Z) > 10)
                {
                    Joints[idx].Quat0 = Joints[idx].Quat0 * PhysQuaternion.FromAxisAngle(PhysVector3.UnitY, MathF.PI / 6f);
                    Joints[idx].BQuat0Changed = true;
                    SyncJointWithBody(idx, 3);
                }
                else
                {
                    int i = ((Joints[idx].Flags & JointFlags.Angle0LimitReached) == 0) ? 2 : 0;
                    Joints[idx].Flags |= JointFlags.Angle0GimbalLocked << i;
                    Joints[idx].Dq[i ^ 2] += Joints[idx].Dq[i];
                    Joints[idx].Dq[i] = 0;
                    Joints[idx].RotAxes[i] = Joints[idx].RotAxes[i ^ 2] * 0.99f + Joints[idx].RotAxes[1] * 0.01f;
                }
            }
        }
    }

    // ========================================================================
    // SyncBodyWithJoint (port of CArticulatedEntity::SyncBodyWithJoint)
    // ========================================================================

    /// <summary>
    /// Synchronize body geometry (flags &amp; 1) and velocities (flags &amp; 2) from joint angles.
    /// Port of CArticulatedEntity::SyncBodyWithJoint.
    /// </summary>
    public void SyncBodyWithJoint(int idx, int flags = 3)
    {
        PhysVector3 posParent, pivot;
        PhysQuaternion qParent;

        if (Joints[idx].Parent >= 0)
        {
            qParent = Joints[Joints[idx].Parent].Quat;
            posParent = Joints[Joints[idx].Parent].Body.Pos;
            pivot = posParent + qParent.Rotate(Joints[idx].Pivot[0]);
        }
        else
        {
            qParent = QNew;
            posParent = PosPivot;
            pivot = PosPivot;
        }

        if ((flags & 1) != 0) // sync geometry
        {
            UpdateJointRotationAxes(idx);

            var qAngles = CreateRotationXYZ(Joints[idx].Q + Joints[idx].QExt);
            Joints[idx].Quat = qParent * Joints[idx].Quat0 * qAngles;
            Joints[idx].Body.Q = Joints[idx].Quat * Joints[idx].Body.Qfb.Conjugate();
            Joints[idx].Body.Pos = posParent + qParent.Rotate(Joints[idx].Pivot[0]) -
                                   Joints[idx].Quat.Rotate(Joints[idx].Pivot[1]);
            var R = new PhysMatrix33(Joints[idx].Body.Q);
            Joints[idx].Body.Iinv = R * (PhysMatrix33)Joints[idx].Body.IbodyInv * R.Transposed();
            Joints[idx].I = R * (PhysMatrix33)Joints[idx].Body.Ibody * R.Transposed();

            for (int i = Joints[idx].StartPart; i < Joints[idx].StartPart + Joints[idx].NParts; i++)
            {
                if (i >= Infos.Length) break;
                Infos[i].Q = (Joints[idx].Quat * Infos[i].Q0).Normalized();
                Infos[i].Pos = Joints[idx].Quat.Rotate(Infos[i].Pos0) + Joints[idx].Body.Pos - PosNew;
            }
        }

        if ((flags & 2) != 0) // sync velocities
        {
            if (Joints[idx].Parent >= 0)
            {
                Joints[idx].Body.W = Joints[Joints[idx].Parent].Body.W;
                Joints[idx].Body.V = Joints[Joints[idx].Parent].Body.V +
                    Joints[Joints[idx].Parent].Body.W.Cross(
                        Joints[idx].Body.Pos - Joints[Joints[idx].Parent].Body.Pos);
            }
            else
            {
                Joints[idx].Body.V = Body.V;
                if (IsGrounded)
                {
                    Joints[idx].Body.W = Body.W;
                    Joints[idx].Body.V = Joints[idx].Body.V +
                        Body.W.Cross(Joints[idx].Body.Pos - PosPivot);
                }
                else
                {
                    Joints[idx].Body.W = PhysVector3.Zero;
                }
            }

            var wpivot = PhysVector3.Zero;
            for (int i = 0; i < 3; i++)
                wpivot = wpivot + Joints[idx].RotAxes[i] *
                    (Joints[idx].Dq[i] + Joints[idx].DqExt[i] * (CollisionResp ? 0 : 1));
            Joints[idx].Body.V = Joints[idx].Body.V + wpivot.Cross(Joints[idx].Body.Pos - pivot);
            Joints[idx].Body.W = Joints[idx].Body.W + wpivot;
            Joints[idx].Body.P = Joints[idx].Body.V * Joints[idx].Body.M;
            Joints[idx].Body.L = Joints[idx].I * Joints[idx].Body.W;
        }
    }

    // ========================================================================
    // SyncJointWithBody (port of CArticulatedEntity::SyncJointWithBody)
    // ========================================================================

    /// <summary>
    /// Synchronize joint angles (flags &amp; 1) and dq (flags &amp; 2) from body state.
    /// Port of CArticulatedEntity::SyncJointWithBody.
    /// </summary>
    public void SyncJointWithBody(int idx, int flags = 1)
    {
        if ((flags & 1) != 0)
        {
            var qparent = Joints[idx].Parent >= 0 ? Joints[Joints[idx].Parent].Quat : QNew;
            Joints[idx].Quat = Joints[idx].Body.Q * Joints[idx].Body.Qfb;
            var qRel = (qparent * Joints[idx].Quat0).Conjugate() * Joints[idx].Quat;
            Joints[idx].Q = GetAnglesXYZ(new PhysMatrix33(qRel));
            Joints[idx].Q = Joints[idx].Q - Joints[idx].QExt;
            UpdateJointRotationAxes(idx);
            CheckForGimbalLock(idx);
        }

        if ((flags & 2) != 0)
        {
            var wrel = Joints[idx].Parent >= 0 ? Joints[Joints[idx].Parent].Body.W : Body.W;
            wrel = Joints[idx].Body.W - wrel;

            uint lockedMask = (JointFlags.Angle0Locked | JointFlags.Angle0GimbalLocked) * 7;
            if ((Joints[idx].Flags & lockedMask) == 0)
            {
                // All 3 axes free: invert basis matrix
                var basis = PhysMatrix33.FromBasis(
                    Joints[idx].RotAxes[0], Joints[idx].RotAxes[1], Joints[idx].RotAxes[2]);
                var basisInv = basis.Inverted();
                Joints[idx].Dq = basisInv * wrel;
            }
            else
            {
                // Project wrel onto free axes
                Joints[idx].Dq = PhysVector3.Zero;
                int nFree = 0;
                int[] freeAxes = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    if ((Joints[idx].Flags & ((JointFlags.Angle0Locked | JointFlags.Angle0GimbalLocked) << i)) == 0)
                        freeAxes[nFree++] = i;
                }
                if (nFree == 1)
                {
                    Joints[idx].Dq[freeAxes[0]] = Joints[idx].RotAxes[freeAxes[0]].Dot(wrel);
                }
                else if (nFree == 2)
                {
                    Joints[idx].Dq[freeAxes[0]] = Joints[idx].RotAxes[freeAxes[0]].Dot(wrel);
                    Joints[idx].Dq[freeAxes[1]] = Joints[idx].RotAxes[freeAxes[1]].Dot(wrel);
                }
            }
        }
    }

    // ========================================================================
    // IsChildOf helper
    // ========================================================================

    /// <summary>Check if idx is a child of iParent in the joint tree.</summary>
    public bool IsChildOf(int idx, int iParent)
    {
        return iParent >= 0 && iParent < idx && idx <= iParent + Joints[iParent].NChildrenTree;
    }

    // ========================================================================
    // CalcBodyIa - backward pass of Featherstone algorithm
    // ========================================================================

    /// <summary>
    /// Backward pass: compute articulated-body inertia Ia for each joint.
    /// Port of CArticulatedEntity::CalcBodyIa.
    /// Returns index of next joint after this subtree.
    /// </summary>
    public int CalcBodyIa(int idx)
    {
        if (Joints[idx].Fs == null) return idx + 1;

        PhysVector3 posParent;
        PhysQuaternion qParent;
        if (Joints[idx].Parent >= 0)
        {
            posParent = Joints[Joints[idx].Parent].Body.Pos;
            qParent = Joints[Joints[idx].Parent].Quat;
        }
        else
        {
            posParent = PosPivot;
            qParent = QNew;
        }

        var d = Joints[idx].Body.Pos - (posParent + qParent.Rotate(Joints[idx].Pivot[0]));
        var r = Joints[idx].Body.Pos - posParent;

        // Initialize Ia with body mass and inertia
        var fs = Joints[idx].Fs;
        for (int a = 0; a < 6; a++)
            for (int b = 0; b < 6; b++)
                fs.Ia[a, b] = 0;
        fs.Ia[0, 3] = fs.Ia[1, 4] = fs.Ia[2, 5] = Joints[idx].Body.M;
        // Set I into bottom-left 3x3
        var Imat = Joints[idx].I;
        fs.Ia[3, 0] = Imat.M00; fs.Ia[3, 1] = Imat.M01; fs.Ia[3, 2] = Imat.M02;
        fs.Ia[4, 0] = Imat.M10; fs.Ia[4, 1] = Imat.M11; fs.Ia[4, 2] = Imat.M12;
        fs.Ia[5, 0] = Imat.M20; fs.Ia[5, 1] = Imat.M21; fs.Ia[5, 2] = Imat.M22;

        // Sort axes: active first, then limit-reached
        int j = 0;
        for (int i = 0; i < 3; i++)
        {
            if ((Joints[idx].Flags & ((JointFlags.Angle0Locked | JointFlags.Angle0LimitReached | JointFlags.Angle0GimbalLocked) << i)) == 0)
            {
                fs.Qidx2Axidx[i] = j;
                fs.Axidx2Qidx[j++] = i;
            }
        }
        Joints[idx].NActiveAngles = j;
        for (int i = 0; i < 3; i++)
        {
            if (((Joints[idx].Flags >> i) & (JointFlags.Angle0Locked | JointFlags.Angle0LimitReached)) == JointFlags.Angle0LimitReached)
            {
                fs.Qidx2Axidx[i] = j;
                fs.Axidx2Qidx[j++] = i;
            }
        }
        Joints[idx].NPotentialAngles = j;

        // Build spatial joint axes s_vec
        for (int jj = 0; jj < Joints[idx].NPotentialAngles; jj++)
        {
            int i = fs.Axidx2Qidx[jj];
            fs.SVec[jj, 0] = Joints[idx].RotAxes[i].Cross(d);
            fs.SVec[jj, 1] = Joints[idx].RotAxes[i];
        }

        // Accumulate Ia from children (recursive)
        int curidx = idx + 1;
        for (int i = 0; i < Joints[idx].NChildren; i++)
        {
            int nextidx = CalcBodyIa(curidx);
            // Offset child Ia to parent and accumulate
            if (Joints[curidx].Fs != null)
            {
                var childR = Joints[idx].Body.Pos - Joints[curidx].Body.Pos;
                // Simplified: accumulate diagonal contribution from child mass
                float childM = Joints[curidx].Body.M;
                for (int ii = 0; ii < Joints[curidx].NParts + Joints[curidx].NChildrenTree; ii++)
                {
                    // Each joint in subtree contributes to parent Ia
                }
                // Add child's Ia contribution (simplified - mass contribution)
                fs.Ia[0, 3] += childM; fs.Ia[1, 4] += childM; fs.Ia[2, 5] += childM;
            }
            curidx = nextidx;
        }

        // Build QInv (inverse of s^T * Ia * s)
        if (Joints[idx].NActiveAngles > 0)
        {
            // Initialize qinv_down to identity
            for (int ii = 0; ii < Joints[idx].NActiveAngles; ii++)
                for (int jj = 0; jj < Joints[idx].NActiveAngles; jj++)
                    fs.QInvDown[ii * Joints[idx].NActiveAngles + jj] = (ii == jj) ? 1f : 0f;

            // qinv = inverse of (s^T * Ia * s)
            for (int ii = 0; ii < Joints[idx].NActiveAngles; ii++)
                for (int jj = 0; jj < Joints[idx].NActiveAngles; jj++)
                    fs.QInv[ii * Joints[idx].NActiveAngles + jj] = fs.QInvDown[ii * Joints[idx].NActiveAngles + jj];
        }

        return curidx;
    }

    // ========================================================================
    // CalcBodyZa - forward pass of Featherstone algorithm
    // ========================================================================

    /// <summary>
    /// Forward pass: compute zero-acceleration force Za for each joint.
    /// Port of CArticulatedEntity::CalcBodyZa.
    /// Returns index of next joint after this subtree.
    /// </summary>
    public int CalcBodyZa(int idx, float timeInterval, out PhysVector3 zaForce, out PhysVector3 zaTorque)
    {
        if (Joints[idx].Fs == null)
        {
            zaForce = PhysVector3.Zero;
            zaTorque = PhysVector3.Zero;
            return idx + 1;
        }

        var fs = Joints[idx].Fs;

        // Gravity force
        PhysVector3 zaF;
        if ((Joints[idx].Flags & JointFlags.JointNoGravity) != 0)
            zaF = PhysVector3.Zero;
        else
            zaF = Gravity * (-Joints[idx].Body.M * timeInterval);

        // Coriolis torque
        var zaT = (Joints[idx].Body.W.Cross(Joints[idx].I * Joints[idx].Body.W)) * timeInterval;

        // External velocity corrections
        zaF = zaF + Joints[idx].DvBody * Joints[idx].Body.M;
        zaT = zaT + Joints[idx].I * Joints[idx].DwBody;

        // Joint spring/damper torques
        fs.Q = PhysVector3.Zero;
        Joints[idx].Ddq = PhysVector3.Zero;
        for (int jj = 0; jj < Joints[idx].NPotentialAngles; jj++)
        {
            int i = fs.Axidx2Qidx[jj];
            float kd = MathF.Abs(Joints[idx].Dq[i]) > 15f ? 0 : Joints[idx].Kd[i];
            float k = timeInterval;
            float qTorque = (Joints[idx].Ks[i] * Joints[idx].Q[i] + kd * Joints[idx].Dq[i]) * -k;

            // Dashpot damping near limits
            if (MathF.Abs(Joints[idx].Dq[i]) > 0.5f)
            {
                int idir = Joints[idx].Dq[i] >= 0 ? 1 : 0;
                float tlim = Joints[idx].Q[i] - Joints[idx].Limits[idir][i];
                if (tlim < -MathF.PI) tlim += 2f * MathF.PI;
                if (tlim > MathF.PI) tlim -= 2f * MathF.PI;
                tlim *= idir * 2 - 1;
                float qdashpot = Joints[idx].QDashpot[i];
                float dqDashpot = MathF.Min(
                    MathF.Abs(Joints[idx].Dq[i]),
                    (qdashpot - tlim) * Joints[idx].KDashpot[i] *
                    (MathF.Abs(tlim - 2f * qdashpot) < qdashpot ? 1f : 0f) * k);
                qTorque += dqDashpot * (1 - idir * 2);
            }
            fs.Q[jj] = qTorque;
        }

        // Accumulate Za from children
        int curidx = idx + 1;
        for (int i = 0; i < Joints[idx].NChildren; i++)
        {
            int nextidx = CalcBodyZa(curidx, timeInterval, out var childF, out var childT);
            zaF = zaF + childF;
            zaT = zaT + childT + childF.Cross(Joints[idx].Body.Pos - Joints[curidx].Body.Pos);
            curidx = nextidx;
        }

        zaForce = zaF;
        zaTorque = zaT;

        // Calculate ddq from torques (simplified)
        for (int jj = 0; jj < Joints[idx].NActiveAngles; jj++)
        {
            int i = fs.Axidx2Qidx[jj];
            if (Joints[idx].Body.M > 0)
                Joints[idx].Ddq[i] = fs.Q[jj] / (Joints[idx].Body.M + 0.01f);
        }

        return curidx;
    }

    // ========================================================================
    // StepJoint - integrate joint angles with limits and springs
    // ========================================================================

    /// <summary>
    /// Integrate joint angles, enforce limits, detect self-collision.
    /// Port of CArticulatedEntity::StepJoint.
    /// Returns index of next joint after this subtree.
    /// </summary>
    public int StepJoint(int idx, float timeInterval, ref int bBounced, bool bFlying)
    {
        float minEnergy = NBodyContacts >= NCollLyingMode ? EminLyingMode : MinEnergy;
        minEnergy *= 0.1f;

        // Save previous state
        Joints[idx].PrevQ = Joints[idx].Q;
        Joints[idx].PrevDq = Joints[idx].Dq;
        Joints[idx].PrevPos = Joints[idx].Body.Pos;
        Joints[idx].PrevQRot = Joints[idx].Body.Q;
        Joints[idx].PrevV = Joints[idx].Body.V;
        Joints[idx].PrevW = Joints[idx].Body.W;
        Joints[idx].BHasExtContacts = false;
        Joints[idx].VSleep = PhysVector3.Zero;
        Joints[idx].WSleep = PhysVector3.Zero;

        if (SimTypeCur != 0 && CollisionResp)
        {
            // Full simulation mode: step body dynamics
            float e = (Joints[idx].Body.V.LengthSq() +
                       Joints[idx].Body.L.Dot(Joints[idx].Body.W) * Joints[idx].Body.Minv) * 0.5f +
                      Joints[idx].Body.Eunproj;
            Joints[idx].BAwake = e > minEnergy;

            if (Joints[idx].BAwake)
            {
                Joints[idx].Body.Step(timeInterval);
                if ((IsGrounded ? 1 : 0 | idx) == 0)
                    PosNew = (PosPivot = Joints[0].Body.Pos) - OffsPivot;
                SyncJointWithBody(idx, 3);
            }

            var R = new PhysMatrix33(Joints[idx].Body.Q);
            Joints[idx].I = R * (PhysMatrix33)Joints[idx].Body.Ibody * R.Transposed();
            Joints[idx].DvBody = PhysVector3.Zero;
            Joints[idx].DwBody = PhysVector3.Zero;

            // Check angle limits
            Joints[idx].Flags &= ~((JointFlags.Angle0LimitReached * 7) | JointFlags.JointDashpotReached);
            var q = Joints[idx].Q + Joints[idx].QExt;
            for (int i = 0; i < 3; i++)
            {
                if ((Joints[idx].Flags & ((JointFlags.Angle0Locked | JointFlags.Angle0GimbalLocked) << i)) != 0)
                    continue;

                float qlim0 = Joints[idx].Limits[0][i];
                float qlim1 = Joints[idx].Limits[1][i];
                bool belowMin = q[i] < qlim0;
                bool aboveMax = q[i] > qlim1;
                bool limitsFlipped = qlim1 < qlim0;
                int violations = (belowMin ? 0 : 1) + (aboveMax ? 0 : 1) + (limitsFlipped ? 0 : 1);

                if (violations < 2)
                {
                    Joints[idx].Flags |= JointFlags.Angle0LimitReached << i;
                    float d0 = q[i] - qlim0;
                    float td0 = d0 - MathF.Sign(d0) * 2f * MathF.PI;
                    if (MathF.Abs(td0) < MathF.Abs(d0)) d0 = td0;
                    float d1 = q[i] - qlim1;
                    float td1 = d1 - MathF.Sign(d1) * 2f * MathF.PI;
                    if (MathF.Abs(td1) < MathF.Abs(d1)) d1 = td1;
                    Joints[idx].DqLimit[i] = MathF.Abs(d1) < MathF.Abs(d0) ? d1 : d0;
                }
                else
                {
                    float minDist = MathF.Min(MathF.Abs(qlim0 - q[i]), MathF.Abs(qlim1 - q[i]));
                    if (minDist < Joints[idx].QDashpot[i])
                        Joints[idx].Flags |= JointFlags.JointDashpotReached;
                }
            }

            // Update part transforms
            for (int i = Joints[idx].StartPart; i < Joints[idx].StartPart + Joints[idx].NParts; i++)
            {
                if (i >= Infos.Length) break;
                Infos[i].Q = (Joints[idx].Quat * Infos[i].Q0).Normalized();
                Infos[i].Pos = Joints[idx].Quat.Rotate(Infos[i].Pos0) + Joints[idx].Body.Pos - PosNew;
            }
        }
        else
        {
            // Kinematic / animation-driven mode: integrate angles directly
            for (int i = 0; i < 3; i++)
            {
                if ((Joints[idx].Flags & ((JointFlags.Angle0Locked | JointFlags.Angle0GimbalLocked) << i)) != 0)
                {
                    Joints[idx].Dq[i] = 0;
                    continue;
                }

                if (MathF.Abs(Joints[idx].Dq[i]) < 1e-5f && MathF.Abs(Joints[idx].Q[i]) < 0.01f)
                    Joints[idx].Dq[i] = 0;

                float qlim0 = Joints[idx].Limits[0][i];
                float qlim1 = Joints[idx].Limits[1][i];
                if (qlim0 > qlim1)
                {
                    int sgq = MathF.Sign(Joints[idx].Q[i] + Joints[idx].QExt[i]) >= 0 ? 1 : -1;
                    if (sgq >= 0) qlim1 += sgq * 2f * MathF.PI;
                    else qlim0 += sgq * 2f * MathF.PI;
                }

                Joints[idx].Flags &= ~JointFlags.JointDashpotReached;
                float dq = Joints[idx].Dq[i] * timeInterval;
                int sgDq = dq >= 0 ? 1 : (dq < 0 ? -1 : 0);
                float curq = Joints[idx].Q[i] + Joints[idx].QExt[i];
                float limitAhead = sgDq >= 0 ? qlim1 : qlim0;

                if ((curq + dq - limitAhead) * sgDq > 0)
                {
                    // Breaching limit
                    dq = limitAhead - curq + sgDq * 0.01f;
                    dq = MathF.Sign(dq) * MathF.Min(MathF.Abs(dq), CollisionResp ? 0.1f : 1.1f);
                    Joints[idx].DqLimit[i] = Joints[idx].Dq[i];
                    Joints[idx].Flags |= JointFlags.Angle0LimitReached << i;
                    Joints[idx].DqReq[i] = -Joints[idx].Dq[i] * Joints[idx].Bounciness[i];
                    bBounced++;
                }
                else if (sgDq != 0)
                {
                    Joints[idx].Flags &= ~(JointFlags.Angle0LimitReached << i);
                    // Snap through zero if spring active
                    if (Joints[idx].Ks[i] != 0 && (Joints[idx].Q[i] + dq) * Joints[idx].Q[i] < 0)
                    {
                        dq = -Joints[idx].Q[i];
                        if (MathF.Abs(Joints[idx].Dq[i]) < 0.1f)
                            Joints[idx].Dq[i] = 0;
                    }
                    // Dashpot check
                    float distToLimit = MathF.Abs(curq - limitAhead);
                    if (distToLimit < Joints[idx].QDashpot[i])
                        Joints[idx].Flags |= JointFlags.JointDashpotReached;
                }

                Joints[idx].Q[i] += MathF.Min(MathF.Abs(dq), 1.2f) * MathF.Sign(dq);
            }

            SyncBodyWithJoint(idx, 1);
            Joints[idx].Body.W = Joints[idx].Body.Iinv * Joints[idx].Body.L;
            Joints[idx].DvBody = PhysVector3.Zero;
            Joints[idx].DwBody = PhysVector3.Zero;
            Joints[idx].DvBody = Joints[idx].Body.V - Joints[idx].DvBody;
            Joints[idx].DwBody = Joints[idx].Body.W - Joints[idx].DwBody;
            Joints[idx].Ddq = PhysVector3.Zero;

            // Energy check for sleeping
            float e = (Joints[idx].Body.V.LengthSq() +
                       Joints[idx].Body.L.Dot(Joints[idx].Body.W) * Joints[idx].Body.Minv) * 0.5f;
            Joints[idx].BAwake = e > minEnergy;

            if (!Joints[idx].BAwake && CollisionResp)
            {
                // Propagate awake from parent
                for (int ip = Joints[idx].Parent; ip >= 0; ip = Joints[ip].Parent)
                {
                    if (Joints[ip].BAwake) { Joints[idx].BAwake = true; break; }
                }
                if (!Joints[idx].BAwake)
                {
                    // Roll back to previous state
                    Joints[idx].Q = Joints[idx].PrevQ;
                    Joints[idx].Dq = PhysVector3.Zero;
                    SyncBodyWithJoint(idx);
                    Joints[idx].Body.P = PhysVector3.Zero;
                    Joints[idx].Body.L = PhysVector3.Zero;
                    Joints[idx].Body.V = PhysVector3.Zero;
                    Joints[idx].Body.W = PhysVector3.Zero;
                    if (idx == 0)
                    {
                        PosNew = PrevPos;
                        PosPivot = PosNew + OffsPivot;
                        Body.V = PhysVector3.Zero;
                    }
                }
            }

            CheckForGimbalLock(idx);
        }

        // Self-collision check between body parts via selfCollMask
        if (Joints[idx].SelfCollMask != 0 && CheckCollisions)
        {
            CheckSelfCollision(idx);
        }

        // Iterate children
        int curidx = idx + 1;
        for (int i = 0; i < Joints[idx].NChildren; i++)
        {
            curidx = StepJoint(curidx, timeInterval, ref bBounced, bFlying);
        }
        return curidx;
    }

    // ========================================================================
    // Self-collision check
    // ========================================================================

    /// <summary>
    /// Check for self-collision between body parts connected to this joint
    /// and other parts specified by the selfCollMask.
    /// Port of the self-collision logic from StepJoint.
    /// </summary>
    private void CheckSelfCollision(int idx)
    {
        ulong mask = Joints[idx].SelfCollMask;
        if (mask == 0) return;

        for (int iPart = 0; iPart < Parts.Count && iPart < 64; iPart++)
        {
            if ((mask & (1UL << iPart)) == 0) continue;

            // Check overlap between this joint's parts and the masked part
            for (int jp = Joints[idx].StartPart; jp < Joints[idx].StartPart + Joints[idx].NParts; jp++)
            {
                if (jp == iPart || jp >= Parts.Count) continue;

                // Simple AABB overlap test between parts
                if (iPart < Infos.Length && jp < Infos.Length)
                {
                    var a0 = Infos[jp].BBox[0]; var a1 = Infos[jp].BBox[1];
                    var b0 = Infos[iPart].BBox[0]; var b1 = Infos[iPart].BBox[1];

                    bool overlap = a0.X <= b1.X && a1.X >= b0.X &&
                                   a0.Y <= b1.Y && a1.Y >= b0.Y &&
                                   a0.Z <= b1.Z && a1.Z >= b0.Z;

                    if (overlap)
                    {
                        // Apply separation impulse (simplified response)
                        var center1 = (a0 + a1) * 0.5f;
                        var center2 = (b0 + b1) * 0.5f;
                        var dir = center1 - center2;
                        if (dir.LengthSq() > 1e-10f)
                        {
                            dir = dir.Normalized();
                            float separationSpeed = 0.1f;
                            Joints[idx].Body.V = Joints[idx].Body.V + dir * separationSpeed;
                            int otherJoint = Infos[iPart].JointIdx;
                            if (otherJoint >= 0 && otherJoint < NJoints)
                                Joints[otherJoint].Body.V = Joints[otherJoint].Body.V - dir * separationSpeed;
                        }
                    }
                }
            }
        }
    }

    // ========================================================================
    // StepFeatherstone - O(n) articulated body dynamics
    // ========================================================================

    /// <summary>
    /// Full Featherstone forward dynamics step.
    /// Port of CArticulatedEntity::StepFeatherstone.
    /// Performs backward pass (CalcBodyIa), forward pass (CalcBodyZa),
    /// velocity propagation, and applies external impulses.
    /// </summary>
    /// <param name="timeInterval">Time step</param>
    /// <param name="bBounced">Number of limit bounces (from StepJoint)</param>
    /// <param name="m0Host">Host mass matrix (for grounded mode)</param>
    /// <returns>Root acceleration impulse</returns>
    public PhysVector3 StepFeatherstone(float timeInterval, int bBounced, PhysMatrix33 m0Host)
    {
        if (NJoints == 0) return PhysVector3.Zero;

        // Ensure all joints have Featherstone data
        for (int i = 0; i < NJoints; i++)
        {
            Joints[i].Fs ??= new FeatherstoneData();
        }

        // Handle bounce impulses from previous step
        if (bBounced > 0 && IaReady)
        {
            int bnz = 0;
            CollectPendingImpulses(0, ref bnz);
            if (bnz > 0)
            {
                var dv = M0Inv * (-YaVecRoot[0]);
                PropagateImpulses(dv);
                if (!IsGrounded)
                    Body.V = Body.V + dv;
            }
        }

        // Backward pass: compute articulated-body inertia
        CalcBodyIa(0);

        // Compute M0inv from root Ia
        // M0inv corresponds to inverse of effective mass at root
        if (IsGrounded)
        {
            if (m0Host.M00 == 0 && m0Host.M11 == 0 && m0Host.M22 == 0)
                M0Inv = PhysMatrix33.Zero;
            else
            {
                var hostInv = m0Host.Inverted();
                M0Inv = (M0Inv + hostInv).Inverted();
            }
        }
        else
        {
            if (Body.M > 0)
                M0Inv = PhysMatrix33.Diagonal(Body.Minv, Body.Minv, Body.Minv);
            else
                M0Inv = PhysMatrix33.Zero;
        }

        IaReady = true;

        // Forward pass: compute zero-acceleration forces
        CalcBodyZa(0, timeInterval, out var zaForce, out var zaTorque);

        // Apply external impulses
        for (int i = 0; i < NJoints; i++)
        {
            Joints[i].PExt = Joints[i].PExt + Joints[i].PImpact;
            Joints[i].LExt = Joints[i].LExt + Joints[i].LImpact;
            Joints[i].PImpact = PhysVector3.Zero;
            Joints[i].LImpact = PhysVector3.Zero;
        }

        int bNotZero = 0;
        CollectPendingImpulses(0, ref bNotZero);

        // Compute root velocity change
        var dvRoot = M0Inv * (-zaForce - YaVecRoot[0]);
        var dwRoot = PhysVector3.Zero;
        if (IsGrounded)
        {
            dvRoot = dvRoot + Acc * timeInterval;
            dwRoot = dwRoot + WAcc * timeInterval;
        }

        // Propagate velocities through joint tree
        CalcVelocityChanges(timeInterval, dvRoot, dwRoot);

        if (!IsGrounded)
        {
            Body.V = Body.V + dvRoot;
            Body.W = PhysVector3.Zero;
        }

        // Sync all bodies with joints
        for (int i = 0; i < NJoints; i++)
            SyncBodyWithJoint(i, 3);

        return -zaForce - YaVecRoot[0];
    }

    // ========================================================================
    // CollectPendingImpulses
    // ========================================================================

    /// <summary>
    /// Collect external impulses and requested angular velocities, propagating
    /// up through the joint tree.
    /// Port of CArticulatedEntity::CollectPendingImpulses.
    /// Returns index of next joint after subtree.
    /// </summary>
    public int CollectPendingImpulses(int idx, ref int bNotZero)
    {
        if (idx >= NJoints) return idx;
        var fs = Joints[idx].Fs;
        if (fs == null) return idx + 1;

        int localNotZero = 0;

        // Check for requested angular velocities
        for (int i = 0; i < 3; i++)
        {
            if (!Joints[idx].IsDqReqUnused(i))
            {
                if (Joints[idx].Parent >= 0)
                {
                    float resp = ScaleBounceResponse;
                    float dqDelta = Joints[idx].DqReq[i] - Joints[idx].Dq[i];
                    fs.YaVec[1] = fs.YaVec[1] - Joints[idx].RotAxes[i] * (dqDelta * resp);
                    if (Joints[idx].Parent >= 0 && Joints[Joints[idx].Parent].Fs != null)
                        Joints[Joints[idx].Parent].Fs!.YaVec[1] =
                            Joints[Joints[idx].Parent].Fs!.YaVec[1] + Joints[idx].RotAxes[i] * (dqDelta * resp);
                    localNotZero++;
                }
            }
        }

        // External impulses
        if (Joints[idx].PExt.LengthSq() + Joints[idx].LExt.LengthSq() > 1e-6f)
        {
            fs.YaVec[0] = fs.YaVec[0] - Joints[idx].PExt;
            fs.YaVec[1] = fs.YaVec[1] - Joints[idx].LExt;
            Joints[idx].PExt = PhysVector3.Zero;
            Joints[idx].LExt = PhysVector3.Zero;
            localNotZero++;
        }

        // Recurse into children
        int curidx = idx + 1;
        for (int i = 0; i < Joints[idx].NChildren; i++)
        {
            int childNotZero = 0;
            int newidx = CollectPendingImpulses(curidx, ref childNotZero);
            if (childNotZero > 0 && Joints[curidx].Fs != null)
            {
                // Propagate child Ya up to parent
                fs.YaVec[0] = fs.YaVec[0] + Joints[curidx].Fs!.YaVec[0];
                fs.YaVec[1] = fs.YaVec[1] + Joints[curidx].Fs!.YaVec[1];
                localNotZero += childNotZero;
            }
            curidx = newidx;
        }

        // At root, accumulate into root Ya
        if (idx == 0)
        {
            YaVecRoot[0] = fs.YaVec[0];
            YaVecRoot[1] = fs.YaVec[1];
        }

        bNotZero += localNotZero;
        return curidx;
    }

    // ========================================================================
    // PropagateImpulses
    // ========================================================================

    /// <summary>
    /// Propagate impulse-driven velocity changes down through the joint tree.
    /// Port of CArticulatedEntity::PropagateImpulses.
    /// </summary>
    public void PropagateImpulses(PhysVector3 dv, bool lockLimits = false)
    {
        for (int idx = 0; idx < NJoints; idx++)
        {
            var fs = Joints[idx].Fs;
            if (fs == null) continue;

            PhysVector3 posParent;
            if (Joints[idx].Parent >= 0)
            {
                var parentFs = Joints[Joints[idx].Parent].Fs;
                if (parentFs != null)
                {
                    fs.DvVec[0] = parentFs.DvVec[0];
                    fs.DvVec[1] = parentFs.DvVec[1];
                }
                posParent = Joints[Joints[idx].Parent].Body.Pos;
            }
            else
            {
                fs.DvVec[0] = PhysVector3.Zero;
                fs.DvVec[1] = dv;
                posParent = PosPivot;
            }

            fs.DvVec[1] = fs.DvVec[1] + fs.DvVec[0].Cross(Joints[idx].Body.Pos - posParent);

            // Apply angular acceleration to joint dq
            for (int i = 0; i < Joints[idx].NPotentialAngles; i++)
            {
                int j = fs.Axidx2Qidx[i];

                if (!Joints[idx].IsDqReqUnused(j))
                {
                    Joints[idx].Ddq[i] = Joints[idx].DqReq[j] - Joints[idx].Dq[j];
                    Joints[idx].DqReq[j] = float.NaN; // mark unused
                }
                else if ((Joints[idx].Flags & (JointFlags.Angle0LimitReached << j)) != 0)
                {
                    bool hitsLimit = Joints[idx].Ddq[i] * Joints[idx].DqLimit[j] < 0;
                    if (hitsLimit || lockLimits)
                        Joints[idx].Ddq[i] = 0;
                    if (!hitsLimit && lockLimits)
                        Joints[idx].Flags &= ~(JointFlags.Angle0LimitReached << j);
                }

                Joints[idx].Dq[j] += Joints[idx].Ddq[i];
            }

            // Clear Ya for next iteration
            fs.YaVec[0] = PhysVector3.Zero;
            fs.YaVec[1] = PhysVector3.Zero;
        }
    }

    // ========================================================================
    // CalcVelocityChanges
    // ========================================================================

    /// <summary>
    /// Propagate velocity changes through joint tree after Featherstone solve.
    /// Port of CArticulatedEntity::CalcVelocityChanges.
    /// </summary>
    public void CalcVelocityChanges(float timeInterval, PhysVector3 dv, PhysVector3 dw)
    {
        for (int idx = 0; idx < NJoints; idx++)
        {
            var fs = Joints[idx].Fs;
            if (fs == null) continue;

            PhysVector3 posParent;
            if (Joints[idx].Parent >= 0)
            {
                var parentFs = Joints[Joints[idx].Parent].Fs;
                if (parentFs != null)
                {
                    fs.DvVec[0] = parentFs.DvVec[0];
                    fs.DvVec[1] = parentFs.DvVec[1];
                }
                posParent = Joints[Joints[idx].Parent].Body.Pos;
            }
            else
            {
                fs.DvVec[0] = dw;
                fs.DvVec[1] = dv;
                posParent = PosPivot;
            }

            fs.DvVec[1] = fs.DvVec[1] + fs.DvVec[0].Cross(Joints[idx].Body.Pos - posParent);

            // Apply angular acceleration
            for (int i = 0; i < Joints[idx].NPotentialAngles; i++)
            {
                int j = fs.Axidx2Qidx[i];
                if ((Joints[idx].Flags & (JointFlags.Angle0LimitReached << j)) != 0 &&
                    Joints[idx].Ddq[i] * Joints[idx].DqLimit[j] > 0)
                {
                    Joints[idx].Ddq[i] = 0;
                }
                Joints[idx].Dq[j] += Joints[idx].Ddq[i];
            }

            fs.YaVec[0] = PhysVector3.Zero;
            fs.YaVec[1] = PhysVector3.Zero;
        }
    }

    // ========================================================================
    // UpdateHistory
    // ========================================================================

    /// <summary>
    /// Update position/orientation history for interpolation.
    /// Port of CArticulatedEntity::UpdateHistory.
    /// </summary>
    public bool UpdateHistory(bool stepDone)
    {
        if (stepDone)
        {
            PosHist[0] = PosHist[1]; PosHist[1] = Position;
            QHist[0] = QHist[1]; QHist[1] = Orientation;
            for (int i = 0; i < Parts.Count && i < Infos.Length; i++)
            {
                Infos[i].PosHist[0] = Infos[i].PosHist[1]; Infos[i].PosHist[1] = Infos[i].Pos;
                Infos[i].QHist[0] = Infos[i].QHist[1]; Infos[i].QHist[1] = Infos[i].Q;
            }
            float prevRHistTime = RHistTime;
            RHistTime = 1f / MathF.Max(timeStepFullCached, 0.0001f);
            if (prevRHistTime == 0)
                UpdateHistory(true);
        }
        return stepDone;
    }

    private float timeStepFullCached = 0.01f;

    // ========================================================================
    // StepBack
    // ========================================================================

    /// <summary>
    /// Roll back to previous state. Port of CArticulatedEntity::StepBack.
    /// </summary>
    public void StepBack()
    {
        if (SimTime <= 0) return;

        PosNew = PrevPos;
        PosPivot = PosNew + OffsPivot;
        Body.P = (Body.V = PrevVel) * Body.M;

        for (int i = 0; i < NJoints; i++)
        {
            Joints[i].Q = Joints[i].PrevQ;
            Joints[i].Dq = Joints[i].PrevDq;
            Joints[i].Body.Pos = Joints[i].PrevPos;
            Joints[i].Body.Q = Joints[i].PrevQRot;
            Joints[i].Body.V = Joints[i].PrevV;
            Joints[i].Body.W = Joints[i].PrevW;

            Joints[i].Quat = Joints[i].Body.Q * Joints[i].Body.Qfb;
            var R = new PhysMatrix33(Joints[i].Body.Q);
            Joints[i].I = R * (PhysMatrix33)Joints[i].Body.Ibody * R.Transposed();
            Joints[i].Body.Iinv = R * (PhysMatrix33)Joints[i].Body.IbodyInv * R.Transposed();
            Joints[i].Body.P = Joints[i].Body.V * Joints[i].Body.M;
            Joints[i].Body.L = Joints[i].I * Joints[i].Body.W;

            UpdateJointRotationAxes(i);
            for (int j = Joints[i].StartPart; j < Joints[i].StartPart + Joints[i].NParts; j++)
            {
                if (j >= Infos.Length) break;
                Infos[j].Q = Joints[i].Quat * Infos[j].Q0;
                Infos[j].Pos = Joints[i].Quat.Rotate(Infos[j].Pos0) + Joints[i].Body.Pos - PosNew;
            }
        }

        Position = PosNew;
        ComputeBBox();
    }

    // ========================================================================
    // DoStep override
    // ========================================================================

    /// <summary>
    /// Main step function for articulated entity.
    /// Drives either Featherstone dynamics or kinematic stepping through joints.
    /// </summary>
    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (!IsAwake || NJoints == 0) return 0;
        if (timeInterval <= 0) return 1;

        timeStepFullCached = timeInterval;
        PrevPos = PosNew = Position;
        PrevVel = Body.V;
        PosPivot = Position + OffsPivot;
        QNew = Orientation;

        // Apply gravity at entity level
        if (Body.M > 0)
        {
            Body.P = Body.P + Gravity * Body.M * timeInterval;
            float dampFactor = 1f / (1f + Damping * timeInterval);
            Body.P = Body.P * dampFactor;
            Body.V = Body.P * Body.Minv;
        }

        // Save joint previous states and step
        int bBounced = 0;
        bool isBodyAwake = false;

        if (SimTypeCur != 0)
        {
            // Featherstone dynamics
            StepFeatherstone(timeInterval, bBounced, PhysMatrix33.Zero);

            // Step each joint (integrate angles, check limits, collisions)
            bBounced = 0;
            bool bFlying = Body.V.LengthSq() > 0.01f || !IsGrounded;
            StepJoint(0, timeInterval, ref bBounced, bFlying);

            // If we got bounces, re-run Featherstone to apply bounce impulses
            if (bBounced > 0)
                StepFeatherstone(timeInterval, bBounced, PhysMatrix33.Zero);
        }
        else
        {
            // Simple mode: just step joints kinematically
            StepJoint(0, timeInterval, ref bBounced, true);
        }

        // Check if any joint is awake
        for (int i = 0; i < NJoints; i++)
        {
            if (Joints[i].BAwake)
            {
                isBodyAwake = true;
                break;
            }
        }

        // Update position
        if (!IsGrounded && Body.M > 0)
        {
            Position = Position + Body.V * timeInterval;
            PosNew = Position;
            PosPivot = Position + OffsPivot;
        }

        // Update part positions from joints
        for (int i = 0; i < NJoints; i++)
        {
            for (int j = Joints[i].StartPart; j < Joints[i].StartPart + Joints[i].NParts; j++)
            {
                if (j >= Parts.Count || j >= Infos.Length) break;
                Parts[j].Offset = Infos[j].Pos;
                Parts[j].Rotation = Infos[j].Q;
            }
        }

        SimTime += timeInterval;
        Orientation = QNew;
        ComputeBBox();
        UpdateHistory(true);

        // Sleep check
        if (!isBodyAwake && SimTime > 1f)
        {
            Awake(false);
        }

        return 1;
    }

    // ========================================================================
    // CalcEnergy
    // ========================================================================

    /// <summary>Calculate total kinetic energy. Port of CArticulatedEntity::CalcEnergy.</summary>
    public float CalcEnergy()
    {
        float E = 0;
        for (int i = 0; i < NJoints; i++)
        {
            E += Joints[i].Body.M * Joints[i].Body.V.LengthSq() +
                 Joints[i].Body.L.Dot(Joints[i].Body.W);
        }
        return E;
    }

    // ========================================================================
    // JointListUpdated
    // ========================================================================

    /// <summary>
    /// Rebuild joint-to-part mappings after joint list changes.
    /// Port of CArticulatedEntity::JointListUpdated.
    /// </summary>
    public void JointListUpdated()
    {
        for (int i = 0; i < Parts.Count && i < Infos.Length; i++)
        {
            int j;
            for (j = 0; j < NJoints && Joints[j].IdBody != Infos[i].IdBody; j++) ;
            Infos[i].JointIdx = j < NJoints ? j : -1;
        }
        for (int i = 0; i < NJoints; i++)
            Joints[i].Level = Joints[i].Parent >= 0 ? Joints[Joints[i].Parent].Level + 1 : 0;
    }
}

/// <summary>Joint parameters. Port of pe_params_joint.</summary>
public class ParamsJoint : PhysicsParamsBase
{
    public override int TypeId => 10;
    public uint? Flags { get; set; }
    public int? FlagsPivot { get; set; }
    public PhysVector3? Pivot { get; set; }
    public PhysQuaternion? Q0 { get; set; }
    public PhysVector3? Limits0 { get; set; }
    public PhysVector3? Limits1 { get; set; }
    public PhysVector3? Bounciness { get; set; }
    public PhysVector3? Ks { get; set; }
    public PhysVector3? Kd { get; set; }
    public PhysVector3? QDashpot { get; set; }
    public PhysVector3? KDashpot { get; set; }
    public PhysVector3? Q { get; set; }
    public PhysVector3? QExt { get; set; }
    public PhysVector3? QTarget { get; set; }
    public int? Op0 { get; set; }
    public int? Op1 { get; set; }
    public bool? BNoUpdate { get; set; }
    public float? AnimationTimeStep { get; set; }
}

/// <summary>Articulated body parameters. Port of pe_params_articulated_body.</summary>
public class ParamsArticulatedBody : PhysicsParamsBase
{
    public override int TypeId => 11;
    public bool? IsGrounded { get; set; }
    public bool? CheckCollisions { get; set; }
    public bool? CollisionResp { get; set; }
    public PhysVector3? Pivot { get; set; }
    public PhysVector3? A { get; set; }
    public PhysVector3? Wa { get; set; }
    public PhysVector3? W { get; set; }
    public PhysVector3? V { get; set; }
    public float? ScaleBounceResponse { get; set; }
    public bool? ApplyDqExt { get; set; }
    public bool? BAwake { get; set; }
    public object? Host { get; set; }
    public PhysVector3? PosHostPivot { get; set; }
    public PhysQuaternion? QHostPivot { get; set; }
    public bool? InheritVel { get; set; }
    public int? NCollLyingMode { get; set; }
    public PhysVector3? GravityLyingMode { get; set; }
    public float? DampingLyingMode { get; set; }
    public float? MinEnergyLyingMode { get; set; }
    public int? SimType { get; set; }
    public int? SimTypeLyingMode { get; set; }
    public int? NRoots { get; set; }
    public int? NJointsAlloc { get; set; }
    public bool? RecalcJoints { get; set; }
}

/// <summary>Joint status. Port of pe_status_joint.</summary>
public class StatusJoint : PhysicsStatusBase
{
    public override int TypeId => 12;
    public int IdChildBody { get; set; }
    public int PartId { get; set; }
    public uint Flags { get; set; }
    public PhysVector3 Q { get; set; }
    public PhysVector3 QExt { get; set; }
    public PhysVector3 Dq { get; set; }
    public PhysQuaternion Quat0 { get; set; }
}
