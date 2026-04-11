// Port of CryPhysics structural breakability from physicalentity.h/cpp
// Original: Copyright Crytek GMBH, used under license
// Structures: SStructuralJoint, SStructureInfo, SPartInfo
// Functions: UpdateStructure, GenerateJoints

using CryPhysics.Geometry;
using CryPhysics.Math;

namespace CryPhysics.Entities;

// ============================================================================
// Structural Joint (port of SStructuralJoint from physicalentity.h)
// ============================================================================

/// <summary>
/// Structural joint connecting two entity parts that can break under stress.
/// Port of SStructuralJoint from physicalentity.h.
/// When forces/torques exceed the max thresholds, the joint breaks and the
/// connected parts separate.
/// </summary>
public class StructuralJoint
{
    /// <summary>Unique joint identifier.</summary>
    public int Id;

    /// <summary>Indices of the two connected parts in the entity's Parts list.</summary>
    public int[] IPart = { -1, -1 };

    /// <summary>Joint attachment point in entity local space.</summary>
    public PhysVector3 Point;

    /// <summary>Joint normal direction (push/pull axis).</summary>
    public PhysVector3 Normal;

    /// <summary>Joint tangent X axis (for shear/bend decomposition).</summary>
    public PhysVector3 AxisX;

    // ---- Force/torque break thresholds (port of max* fields) ----

    /// <summary>Maximum compressive force before breaking.</summary>
    public float MaxForcePush = 4e5f;

    /// <summary>Maximum tensile force before breaking.</summary>
    public float MaxForcePull = 1e5f;

    /// <summary>Maximum shear force before breaking.</summary>
    public float MaxForceShift = 2e5f;

    /// <summary>Maximum bending torque before breaking.</summary>
    public float MaxTorqueBend = 1e5f;

    /// <summary>Maximum twisting torque before breaking.</summary>
    public float MaxTorqueTwist = 2e5f;

    // ---- Damage accumulation ----

    /// <summary>Accumulated damage from sub-threshold forces.</summary>
    public float DamageAccum;

    /// <summary>Damage threshold before the joint breaks.</summary>
    public float DamageAccumThresh = 1f;

    // ---- Constraint parameters ----

    /// <summary>Constraint limits (x=push, y=pull, z=shift).</summary>
    public PhysVector3 LimitConstr;

    /// <summary>Constraint damping factor.</summary>
    public float DampingConstr;

    // ---- State ----

    /// <summary>Whether this joint is breakable at all.</summary>
    public bool IsBreakable = true;

    /// <summary>Whether this joint is currently broken.</summary>
    public bool IsBroken;

    /// <summary>Sensor size for contact detection.</summary>
    public float Size = 0.05f;

    /// <summary>Current tension ratio (0 = no stress, >= 1 = breaking).</summary>
    public float Tension;

    /// <summary>Tension iteration index for convergence tracking.</summary>
    public int TensionIter;

    /// <summary>Accumulated linear impulse passing through the joint.</summary>
    public PhysVector3 PAccum;

    /// <summary>Accumulated angular impulse passing through the joint.</summary>
    public PhysVector3 LAccum;
}

// ============================================================================
// Per-Part Info for structural simulation (port of SPartInfo)
// ============================================================================

/// <summary>
/// Per-part structural simulation data.
/// Port of SPartInfo from physicalentity.h.
/// Tracks external forces and initial velocities for structural update.
/// </summary>
public class StructuralPartInfo
{
    /// <summary>External linear impulse accumulated this frame.</summary>
    public PhysVector3 PExt;

    /// <summary>External angular impulse accumulated this frame.</summary>
    public PhysVector3 LExt;

    /// <summary>External force (continuous).</summary>
    public PhysVector3 FExt;

    /// <summary>External torque (continuous).</summary>
    public PhysVector3 TExt;

    /// <summary>Override initial velocity for clients.</summary>
    public PhysVector3 InitialVel;

    /// <summary>Override initial angular velocity for clients.</summary>
    public PhysVector3 InitialAngVel;

    /// <summary>Parent part index (-1 for root parts, 0+ for parented).</summary>
    public int ParentPart = -1;

    /// <summary>Original flags before structural modification.</summary>
    public uint Flags0;

    /// <summary>Original collider flags before structural modification.</summary>
    public uint FlagsCollider0;
}

// ============================================================================
// Structure Info (port of SStructureInfo from physicalentity.h)
// ============================================================================

/// <summary>
/// Complete structural integrity information for an entity.
/// Port of SStructureInfo from physicalentity.h.
/// Contains all joints, per-part force data, and break state.
/// </summary>
public class StructureInfo
{
    /// <summary>Array of structural joints connecting parts.</summary>
    public StructuralJoint[] Joints = Array.Empty<StructuralJoint>();

    /// <summary>Per-part structural data.</summary>
    public StructuralPartInfo[] Parts = Array.Empty<StructuralPartInfo>();

    /// <summary>Current number of active joints.</summary>
    public int JointCount;

    /// <summary>Allocated capacity for joints array.</summary>
    public int JointsAlloc;

    /// <summary>Number of joints broken in the last update.</summary>
    public int LastBrokenJointCount;

    /// <summary>Whether the structure was modified since last check.</summary>
    public bool IsModified;

    /// <summary>Part ID of the breakage epicenter.</summary>
    public int BreakEpicenterPartId = -1;

    /// <summary>Number of parts allocated.</summary>
    public int PartsAlloc;

    /// <summary>Time of last structural update.</summary>
    public float TimeLastUpdate;

    /// <summary>Previous joint count (for detecting changes).</summary>
    public int PrevJointCount;

    /// <summary>Number of joints used in last iteration.</summary>
    public int LastUsedJointCount;

    /// <summary>Previous timestep.</summary>
    public float PrevDt;

    /// <summary>Minimum time between structural snapshots.</summary>
    public float MinSnapshotTime;

    /// <summary>Distance threshold for auto-detaching parts.</summary>
    public float AutoDetachmentDist;

    /// <summary>Position of last explosion affecting this structure.</summary>
    public PhysVector3 LastExplPos;

    /// <summary>Explosion linear impulse per part.</summary>
    public PhysVector3[]? PExpl;

    /// <summary>Explosion angular impulse per part.</summary>
    public PhysVector3[]? LExpl;

    /// <summary>Whether this is a test run (no actual breaking).</summary>
    public bool IsTestRun;

    /// <summary>Whether any joints can break directly (without force accumulation).</summary>
    public bool HasDirectBreaks;
}

// ============================================================================
// Per-part helper for structural solver (port of SPartHelper)
// ============================================================================

/// <summary>
/// Temporary per-part data used during structural update iteration.
/// Port of SPartHelper from physicalentity.h.
/// </summary>
internal class StructuralPartHelper
{
    public int Idx;
    public float Minv;
    public PhysMatrix33 Iinv = PhysMatrix33.Identity;
    public PhysVector3 V;
    public PhysVector3 W;
    public PhysVector3 Origin;
    public bool Processed;
    public int JointStart;
    public int Island;
    public PhysicalEntity? Entity;
}

/// <summary>
/// Temporary per-joint data used during structural solver.
/// Port of SStructuralJointHelper from physicalentity.h.
/// </summary>
internal class StructuralJointHelper
{
    public int Idx;
    public PhysVector3 R0, R1;           // Vectors from part origins to joint point
    public PhysMatrix33 VKinv = PhysMatrix33.Identity;  // Velocity constraint inverse
    public PhysMatrix33 WKinv = PhysMatrix33.Identity;  // Angular constraint inverse
    public PhysVector3 P, L;             // Impulse through this joint
    public bool Broken;
}

// ============================================================================
// Structural Breakability Extension Methods
// ============================================================================

/// <summary>
/// Extension methods for PhysicalEntity providing structural breakability.
/// Port of CPhysicalEntity::UpdateStructure and GenerateJoints from
/// physicalentity.cpp.
/// </summary>
public static class StructuralBreakability
{
    /// <summary>
    /// Allocate structural info on an entity if not already present.
    /// Port of CPhysicalEntity::AllocStructureInfo.
    /// </summary>
    public static StructureInfo EnsureStructureInfo(PhysicalEntity entity)
    {
        if (entity.Structure != null)
            return entity.Structure;

        var info = new StructureInfo();
        info.PartsAlloc = entity.Parts.Count;
        info.Parts = new StructuralPartInfo[System.Math.Max(info.PartsAlloc, 4)];
        for (int i = 0; i < info.Parts.Length; i++)
            info.Parts[i] = new StructuralPartInfo();

        info.JointsAlloc = 16;
        info.Joints = new StructuralJoint[info.JointsAlloc];
        for (int i = 0; i < info.JointsAlloc; i++)
            info.Joints[i] = new StructuralJoint();

        entity.Structure = info;
        return info;
    }

    /// <summary>
    /// Auto-generate structural joints by detecting overlapping/touching parts.
    /// Port of CPhysicalEntity::GenerateJoints from physicalentity.cpp.
    /// For each pair of parts that intersect, creates a joint at their contact
    /// point with break thresholds derived from sample joints or defaults.
    /// Returns the number of joints created.
    /// </summary>
    public static int GenerateJoints(PhysicalEntity entity)
    {
        var structure = EnsureStructureInfo(entity);

        // Default break thresholds (from C++ sampleJoint)
        float defMaxForcePull = 1e5f;
        float defMaxForcePush = 4e5f;
        float defMaxForceShift = 2e5f;
        float defMaxTorqueBend = 1e5f;
        float defMaxTorqueTwist = 2e5f;
        float defSize = 0.05f;

        // Remove previously-broken joints from the active list
        int activeJoints = 0;
        for (int i = 0; i < structure.JointCount; i++)
        {
            if (!structure.Joints[i].IsBroken)
            {
                if (activeJoints != i)
                    structure.Joints[activeJoints] = structure.Joints[i];
                activeJoints++;
            }
        }
        structure.JointCount = activeJoints;

        // For each pair of parts, check for overlap and create joints
        for (int i = 0; i < entity.Parts.Count; i++)
        {
            var partI = entity.Parts[i];
            if (partI.Mass <= 0) continue;

            for (int j = 0; j < i; j++)
            {
                var partJ = entity.Parts[j];
                if (partJ.Mass <= 0 && partI.Mass <= 0) continue;

                // Check if a joint already exists between these two parts
                bool exists = false;
                for (int k = 0; k < structure.JointCount; k++)
                {
                    int p0 = structure.Joints[k].IPart[0];
                    int p1 = structure.Joints[k].IPart[1];
                    if ((p0 == i && p1 == j) || (p0 == j && p1 == i))
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists) continue;

                // Simple AABB overlap check between parts
                if (!PartsOverlap(partI, partJ))
                    continue;

                // Create joint at the midpoint between part centers
                var ptI = partI.Offset;
                var ptJ = partJ.Offset;
                var jointPt = (ptI + ptJ) * 0.5f;
                var jointN = (ptJ - ptI);
                float nLen = jointN.Length();
                if (nLen > 1e-10f)
                    jointN = jointN * (1f / nLen);
                else
                    jointN = PhysVector3.UnitZ;

                // Ensure capacity
                if (structure.JointCount >= structure.JointsAlloc)
                {
                    structure.JointsAlloc += 8;
                    Array.Resize(ref structure.Joints, structure.JointsAlloc);
                    for (int k = structure.JointCount; k < structure.JointsAlloc; k++)
                        structure.Joints[k] = new StructuralJoint();
                }

                var joint = structure.Joints[structure.JointCount];
                joint.Id = structure.JointCount;
                joint.IPart[0] = i;
                joint.IPart[1] = j;
                joint.Point = jointPt;
                joint.Normal = jointN;
                joint.AxisX = GetOrthogonal(jointN);
                joint.MaxForcePush = defMaxForcePush;
                joint.MaxForcePull = defMaxForcePull;
                joint.MaxForceShift = defMaxForceShift;
                joint.MaxTorqueBend = defMaxTorqueBend;
                joint.MaxTorqueTwist = defMaxTorqueTwist;
                joint.Size = defSize;
                joint.IsBreakable = true;
                joint.IsBroken = false;
                joint.DamageAccum = 0;
                joint.Tension = 0;
                joint.PAccum = PhysVector3.Zero;
                joint.LAccum = PhysVector3.Zero;

                structure.JointCount++;
            }
        }

        return structure.JointCount;
    }

    /// <summary>
    /// Update structural integrity, check forces on joints, and break them.
    /// Port of CPhysicalEntity::UpdateStructure from physicalentity.cpp.
    /// Iterates over all structural joints, computes forces/torques from
    /// part velocities and external impulses, and breaks joints that exceed
    /// their thresholds.
    /// Returns the number of joints that broke this frame.
    /// </summary>
    /// <param name="entity">Entity to update.</param>
    /// <param name="dt">Time step.</param>
    /// <param name="gravity">Gravity vector.</param>
    /// <returns>Number of newly broken joints.</returns>
    public static int UpdateStructure(PhysicalEntity entity, float dt, in PhysVector3 gravity)
    {
        var structure = entity.Structure;
        if (structure == null || structure.JointCount == 0 || entity.Parts.Count == 0)
            return 0;

        int nParts = entity.Parts.Count;
        int nJoints = structure.JointCount;
        int nBroken = 0;

        // Ensure part info is sized correctly
        if (structure.Parts.Length < nParts)
        {
            Array.Resize(ref structure.Parts, nParts);
            for (int i = 0; i < nParts; i++)
                structure.Parts[i] ??= new StructuralPartInfo();
        }

        // Prepare per-part helpers
        var parts = new StructuralPartHelper[nParts];
        float totalMass = 0;
        for (int i = 0; i < nParts; i++)
        {
            parts[i] = new StructuralPartHelper
            {
                Idx = i,
                Origin = entity.Parts[i].Offset,
                Processed = false,
                Entity = entity
            };

            float mass = entity.Parts[i].Mass;
            if (mass > 0)
            {
                parts[i].Minv = 1f / mass;
                // Approximate inverse inertia from mass and part bounding box
                var sz = entity.Parts[i].BBoxMax - entity.Parts[i].BBoxMin;
                float scale2 = entity.Parts[i].Scale * entity.Parts[i].Scale;
                if (sz.LengthSq() > 1e-20f)
                {
                    parts[i].Iinv = PhysMatrix33.Diagonal(
                        12f / (mass * scale2 * (sz.Y * sz.Y + sz.Z * sz.Z + 1e-10f)),
                        12f / (mass * scale2 * (sz.X * sz.X + sz.Z * sz.Z + 1e-10f)),
                        12f / (mass * scale2 * (sz.X * sz.X + sz.Y * sz.Y + 1e-10f))
                    );
                }
                else
                {
                    parts[i].Iinv = PhysMatrix33.Identity * (6f / (mass + 1e-20f));
                }
            }
            else
            {
                parts[i].Minv = 0;
                parts[i].Iinv = new PhysMatrix33();
            }

            // Compute velocity from impulses
            parts[i].V = (structure.Parts[i].PExt * dt * 100f + structure.Parts[i].FExt) * parts[i].Minv;
            var angImpulse = structure.Parts[i].LExt * dt * 100f + structure.Parts[i].TExt;
            parts[i].W = parts[i].Iinv * angImpulse;

            // Add gravity contribution
            parts[i].V = parts[i].V + gravity * dt;

            totalMass += mass;
        }

        // Prepare per-joint helpers
        var jointHelpers = new StructuralJointHelper[nJoints];
        for (int i = 0; i < nJoints; i++)
        {
            var jnt = structure.Joints[i];
            if (jnt.IsBroken || !jnt.IsBreakable)
            {
                jointHelpers[i] = new StructuralJointHelper { Idx = i, Broken = true };
                continue;
            }

            int i0 = jnt.IPart[0], i1 = jnt.IPart[1];
            if (i0 < 0 || i0 >= nParts || i1 < 0 || i1 >= nParts)
            {
                jointHelpers[i] = new StructuralJointHelper { Idx = i, Broken = true };
                continue;
            }

            var helper = new StructuralJointHelper
            {
                Idx = i,
                R0 = parts[i0].Origin - jnt.Point,
                R1 = parts[i1].Origin - jnt.Point,
                Broken = false
            };

            // Compute effective mass matrix at joint point
            float minvSum = parts[i0].Minv + parts[i1].Minv;
            helper.VKinv = PhysMatrix33.Diagonal(minvSum, minvSum, minvSum);
            helper.WKinv = parts[i0].Iinv + parts[i1].Iinv;

            helper.P = jnt.PAccum;
            helper.L = jnt.LAccum;

            jointHelpers[i] = helper;
        }

        // Iterative constraint solver (simplified port of the C++ breadth-first solver)
        // Compute tension on each joint from relative velocity of connected parts
        int nIter = System.Math.Min(nJoints * 2, 50);
        for (int iter = 0; iter < nIter; iter++)
        {
            for (int ji = 0; ji < nJoints; ji++)
            {
                if (jointHelpers[ji].Broken) continue;

                var jnt = structure.Joints[ji];
                int i0 = jnt.IPart[0], i1 = jnt.IPart[1];

                // Relative velocity at joint point
                var dv = (parts[i0].V + (parts[i0].W ^ jointHelpers[ji].R0))
                       - (parts[i1].V + (parts[i1].W ^ jointHelpers[ji].R1));

                // Compute impulse to resolve velocity difference
                // P = K^-1 * dv (simplified - uses diagonal approximation)
                float kDiag = parts[i0].Minv + parts[i1].Minv;
                if (kDiag < 1e-20f) continue;

                var impulse = dv * (1f / kDiag) * 0.5f;

                // Apply impulse to parts
                parts[i0].V = parts[i0].V - impulse * parts[i0].Minv;
                parts[i1].V = parts[i1].V + impulse * parts[i1].Minv;

                // Accumulate for tension calculation
                jointHelpers[ji].P = jointHelpers[ji].P + impulse;
            }
        }

        // Evaluate break conditions
        for (int ji = 0; ji < nJoints; ji++)
        {
            if (jointHelpers[ji].Broken) continue;

            var jnt = structure.Joints[ji];
            var P = jointHelpers[ji].P;
            var L = jointHelpers[ji].L;

            // Decompose force into normal and tangential components
            float Pn = P.Dot(jnt.Normal);
            var Pt = P - jnt.Normal * Pn;

            // Compute tension ratio
            float tensionPush = Pn > 0 ? Pn / jnt.MaxForcePush : 0;
            float tensionPull = Pn < 0 ? -Pn / jnt.MaxForcePull : 0;
            float tensionShear = Pt.Length() / jnt.MaxForceShift;

            float Ln = L.Dot(jnt.Normal);
            var Lt = L - jnt.Normal * Ln;
            float tensionTwist = MathF.Abs(Ln) / jnt.MaxTorqueTwist;
            float tensionBend = Lt.Length() / jnt.MaxTorqueBend;

            float maxTension = MathF.Max(MathF.Max(tensionPush, tensionPull),
                               MathF.Max(tensionShear,
                               MathF.Max(tensionTwist, tensionBend)));

            jnt.Tension = maxTension;
            jnt.TensionIter++;

            // Accumulate damage
            if (maxTension > 0.5f)
                jnt.DamageAccum += maxTension * dt;

            // Check break condition
            if (maxTension >= 1f || jnt.DamageAccum >= jnt.DamageAccumThresh)
            {
                jnt.IsBroken = true;
                jointHelpers[ji].Broken = true;
                nBroken++;
            }

            // Update accumulated impulses
            jnt.PAccum = jointHelpers[ji].P;
            jnt.LAccum = jointHelpers[ji].L;
        }

        // Clear external impulses after processing
        for (int i = 0; i < nParts; i++)
        {
            structure.Parts[i].PExt = PhysVector3.Zero;
            structure.Parts[i].LExt = PhysVector3.Zero;
            structure.Parts[i].FExt = PhysVector3.Zero;
            structure.Parts[i].TExt = PhysVector3.Zero;
        }

        structure.LastBrokenJointCount = nBroken;
        structure.IsModified = nBroken > 0;
        structure.LastUsedJointCount = nJoints;
        structure.PrevDt = dt;

        return nBroken;
    }

    /// <summary>
    /// Apply an impulse to a specific part for structural calculation.
    /// The impulse will be processed in the next UpdateStructure call.
    /// </summary>
    public static void ApplyStructuralImpulse(PhysicalEntity entity, int partIndex,
                                               in PhysVector3 impulse, in PhysVector3 point)
    {
        var structure = entity.Structure;
        if (structure == null || partIndex < 0 || partIndex >= structure.Parts.Length)
            return;

        structure.Parts[partIndex].PExt = structure.Parts[partIndex].PExt + impulse;
        var r = point - entity.Parts[partIndex].Offset;
        structure.Parts[partIndex].LExt = structure.Parts[partIndex].LExt + (r ^ impulse);
    }

    /// <summary>
    /// Get all broken joints from the structure.
    /// </summary>
    public static StructuralJoint[] GetBrokenJoints(PhysicalEntity entity)
    {
        var structure = entity.Structure;
        if (structure == null) return Array.Empty<StructuralJoint>();

        var broken = new List<StructuralJoint>();
        for (int i = 0; i < structure.JointCount; i++)
        {
            if (structure.Joints[i].IsBroken)
                broken.Add(structure.Joints[i]);
        }
        return broken.ToArray();
    }

    /// <summary>
    /// Reset all damage accumulation and unbreak all joints.
    /// </summary>
    public static void ResetStructure(PhysicalEntity entity)
    {
        var structure = entity.Structure;
        if (structure == null) return;

        for (int i = 0; i < structure.JointCount; i++)
        {
            structure.Joints[i].IsBroken = false;
            structure.Joints[i].DamageAccum = 0;
            structure.Joints[i].Tension = 0;
            structure.Joints[i].PAccum = PhysVector3.Zero;
            structure.Joints[i].LAccum = PhysVector3.Zero;
        }
        structure.LastBrokenJointCount = 0;
        structure.IsModified = false;
    }

    // ---- Private helpers ----

    private static bool PartsOverlap(EntityGeom a, EntityGeom b)
    {
        // Simple AABB overlap test
        return a.BBoxMin.X <= b.BBoxMax.X && a.BBoxMax.X >= b.BBoxMin.X &&
               a.BBoxMin.Y <= b.BBoxMax.Y && a.BBoxMax.Y >= b.BBoxMin.Y &&
               a.BBoxMin.Z <= b.BBoxMax.Z && a.BBoxMax.Z >= b.BBoxMin.Z;
    }

    private static PhysVector3 GetOrthogonal(in PhysVector3 n)
    {
        if (MathF.Abs(n.X) < 0.9f)
            return (PhysVector3.UnitX ^ n).Normalized();
        return (PhysVector3.UnitY ^ n).Normalized();
    }
}

