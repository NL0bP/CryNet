// Port of CryPhysics softentity.h - soft body/deformable physics
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

/// <summary>Soft body vertex data. Port of se_vertex from CryEngine.</summary>
public class SoftVertex
{
    public PhysVector3 Pos;
    public PhysVector3 Vel;
    public float MassInv;
    public float Mass;
    public PhysVector3 Normal;
    public PhysVector3 NContact;
    public int Idx;
    public int Idx0;
    public float Area;
    public int StartEdge;
    public int EndEdge;
    public int Attached;
    public bool FullFan;
    public int CheckPart;
    public int ContactPart;
    public float RnEdges;
    public IPhysicalEntity? ContactEnt;
    public int ContactNode;
    public PhysVector3 VContact;
    public float VReq;
    public int SurfaceIdx;
    public float Angle0;
    public PhysVector3 NMesh;
    // Extended fields from se_vertex
    public PhysVector3 PtAttach;
    public PhysVector3 PosOrg;
    public int Sorted;
}

/// <summary>Soft body spring edge. Port of se_edge from CryEngine.</summary>
public struct SoftEdge
{
    public int IVtx0, IVtx1;
    public float Len0;
    public float Len;
    public float RLen;
    public float KMass;
    public float Angle0_0, Angle0_1;
}

/// <summary>
/// Soft body / deformable entity. Port of CSoftEntity from CryEngine.
/// Simulates deformable meshes with vertex-based dynamics, edge spring constraints,
/// bending resistance via dihedral angle springs, environment collision,
/// pinned attachment vertices, and wind/water forces.
/// </summary>
public class SoftEntity : PhysicalEntity
{
    public const int SnapshotVersion = 10;
    public override PhysicsEntityType Type => PhysicsEntityType.Soft;

    // Mesh data
    public SoftVertex[] Vertices = Array.Empty<SoftVertex>();
    public SoftEdge[] Edges = Array.Empty<SoftEdge>();
    public int[] VtxEdges = Array.Empty<int>();
    public int NVtx;
    public int NEdges;
    public int NConnectedVtx;
    public int NAttachedVtx;

    // Host tracking
    public PhysVector3 Offs0;
    public PhysQuaternion QRot0 = PhysQuaternion.Identity;
    public bool MeshUpdated;
    public PhysVector3 LastPosHost;
    public PhysQuaternion LastQHost = PhysQuaternion.Identity;
    public PhysVector3 LastPos;

    // Simulation parameters
    public float TimeStepFull;
    public float TimeStepPerformed;
    public PhysVector3 Gravity = new(0, 0, -9.81f);
    public float Emin = 0.01f;
    public float MaxAllowedStep = 0.02f;
    public int NSlowFrames;
    public float Damping = 0.3f;
    public float Accuracy = 0.01f;
    public int NMaxIters = 20;
    public float PrevTimeInterval;

    // Material properties
    public float MaxMove;
    public float MaxAllowedDist;
    public float Thickness;
    public float Ks = 10f;          // Spring stiffness
    public float MaxSafeStep = 0.02f;
    public float Density = 1000f;
    public float Coverage = 1f;
    public float Friction = 0.5f;
    public float ImpulseScale = 1f;
    public float ExplosionScale = 1f;
    public float CollImpulseScale = 1f;
    public float MaxCollImpulse = 1e10f;
    public int CollTypes = -1;
    public float MassDecay;
    public float KShapeStiffnessNorm;
    public float KShapeStiffnessTang;
    public float VtxVol;

    // Animation
    public float StiffnessAnim;
    public float StiffnessDecayAnim;
    public float DampingAnim;
    public float MaxDistAnim;
    public float KRigid;
    public float MaxLevelDenom;

    // Fluid interaction
    public float WaterResistance;
    public float AirResistance;
    public PhysVector3 Wind;
    public PhysVector3 Wind0;
    public PhysVector3 Wind1;
    public float WindTimer;
    public float WindVariance;

    // Internal state
    private float _energy;
    private readonly Random _rng = new();

    public SoftEntity()
    {
        SimulationClass = SimClass.ActiveRigid;
        IsAwake = true;
    }

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        if (parameters is ParamsSoftBody sb)
        {
            if (sb.Thickness.HasValue) Thickness = sb.Thickness.Value;
            if (sb.MaxSafeStep.HasValue) MaxSafeStep = sb.MaxSafeStep.Value;
            if (sb.Ks.HasValue) Ks = sb.Ks.Value;
            if (sb.Friction.HasValue) Friction = sb.Friction.Value;
            if (sb.WaterResistance.HasValue) WaterResistance = sb.WaterResistance.Value;
            if (sb.AirResistance.HasValue) AirResistance = sb.AirResistance.Value;
            if (sb.Wind.HasValue) { Wind = sb.Wind.Value; Wind0 = Wind; Wind1 = Wind; }
            if (sb.WindVariance.HasValue) WindVariance = sb.WindVariance.Value;
            if (sb.NMaxIters.HasValue) NMaxIters = sb.NMaxIters.Value;
            if (sb.Accuracy.HasValue) Accuracy = sb.Accuracy.Value;
            if (sb.ImpulseScale.HasValue) ImpulseScale = sb.ImpulseScale.Value;
            if (sb.ExplosionScale.HasValue) ExplosionScale = sb.ExplosionScale.Value;
            if (sb.CollisionImpulseScale.HasValue) CollImpulseScale = sb.CollisionImpulseScale.Value;
            if (sb.MaxCollisionImpulse.HasValue) MaxCollImpulse = sb.MaxCollisionImpulse.Value;
            if (sb.CollTypes.HasValue) CollTypes = sb.CollTypes.Value;
            if (sb.MassDecay.HasValue) MassDecay = sb.MassDecay.Value;
            if (sb.ShapeStiffnessNorm.HasValue) KShapeStiffnessNorm = sb.ShapeStiffnessNorm.Value;
            if (sb.ShapeStiffnessTang.HasValue) KShapeStiffnessTang = sb.ShapeStiffnessTang.Value;
            if (sb.StiffnessAnim.HasValue) StiffnessAnim = sb.StiffnessAnim.Value;
            if (sb.StiffnessDecayAnim.HasValue) StiffnessDecayAnim = sb.StiffnessDecayAnim.Value;
            if (sb.DampingAnim.HasValue) DampingAnim = sb.DampingAnim.Value;
            if (sb.MaxDistAnim.HasValue) MaxDistAnim = sb.MaxDistAnim.Value;
            return 1;
        }
        if (parameters is SimulationParams sim)
        {
            if (sim.Gravity.HasValue) Gravity = sim.Gravity.Value;
            if (sim.Damping.HasValue) Damping = sim.Damping.Value;
            if (sim.MinEnergy.HasValue) Emin = sim.MinEnergy.Value;
            if (sim.MaxTimeStep.HasValue) MaxAllowedStep = sim.MaxTimeStep.Value;
            return 1;
        }
        return base.SetParams(parameters, threadSafe);
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        if (status is StatusSoftBody ssb)
        {
            ssb.NVtx = NVtx;
            ssb.NEdges = NEdges;
            ssb.NAttached = NAttachedVtx;
            return 1;
        }
        return base.GetStatus(status);
    }

    public override int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        if (action is ActionImpulse imp && imp.Impulse.HasValue)
        {
            // Apply impulse distributed across all free vertices
            if (NVtx > 0)
            {
                int nFree = 0;
                for (int i = 0; i < NVtx; i++)
                    if (Vertices[i].Attached == 0 && Vertices[i].MassInv > 0) nFree++;
                if (nFree > 0)
                {
                    var dv = imp.Impulse.Value / (nFree > 0 ? nFree : 1);
                    for (int i = 0; i < NVtx; i++)
                        if (Vertices[i].Attached == 0 && Vertices[i].MassInv > 0)
                            Vertices[i].Vel = Vertices[i].Vel + dv * Vertices[i].MassInv;
                }
            }
            return 1;
        }
        return base.DoAction(action, threadSafe);
    }

    public override int DoStep(float timeInterval, int callerIndex = 0)
    {
        if (!IsAwake || NVtx == 0) return 0;
        TimeStepFull = timeInterval;

        float dt = MathF.Min(timeInterval, MaxSafeStep);

        // --- Update wind variance ---
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

        // --- Calculate edge lengths ---
        for (int i = 0; i < NEdges; i++)
        {
            ref var edge = ref Edges[i];
            var l = Vertices[edge.IVtx0].Pos - Vertices[edge.IVtx1].Pos;
            edge.Len = l.Length();
            edge.RLen = edge.Len > 1e-4f ? 1f / edge.Len : 0f;
        }

        // --- Calculate vertex normals and areas ---
        float totalArea = 0f;
        for (int i = 0; i < NVtx; i++)
        {
            var vtx = Vertices[i];
            vtx.Normal = PhysVector3.Zero;

            // Accumulate face normals from edge fan
            for (int j = vtx.StartEdge; j < vtx.EndEdge; j++)
            {
                if (j < 0 || j >= VtxEdges.Length) break;
                int j1 = j + 1;
                if (j1 > vtx.EndEdge)
                {
                    if (vtx.FullFan) j1 = vtx.StartEdge;
                    else break;
                }
                if (j1 < 0 || j1 >= VtxEdges.Length) break;

                int ei0 = VtxEdges[j];
                int ei1 = VtxEdges[j1];
                if (ei0 < 0 || ei0 >= NEdges || ei1 < 0 || ei1 >= NEdges) break;

                ref var e0 = ref Edges[ei0];
                ref var e1 = ref Edges[ei1];
                int oi0 = e0.IVtx0 + e0.IVtx1 - i;
                int oi1 = e1.IVtx0 + e1.IVtx1 - i;
                if (oi0 < 0 || oi0 >= NVtx || oi1 < 0 || oi1 >= NVtx) continue;

                var edge0 = Vertices[oi0].Pos - vtx.Pos;
                var edge1 = Vertices[oi1].Pos - vtx.Pos;
                vtx.Normal = vtx.Normal + (edge0 ^ edge1);
            }

            float nLen = vtx.Normal.Length();
            if (nLen > 1e-10f)
            {
                vtx.Area = nLen * Coverage * 0.5f;
                vtx.Normal = vtx.Normal / nLen;
                totalArea += vtx.Area;
            }
            else
            {
                vtx.Area = 0;
            }
        }
        float areaScale = totalArea > 0 ? NVtx / totalArea : 0f;

        // --- Apply gravity, wind, water forces ---
        for (int i = 0; i < NVtx; i++)
        {
            var vtx = Vertices[i];
            if (vtx.Attached > 0) continue;

            // Determine if underwater (simplified: below z=0)
            float waterDepth = -(vtx.Pos.Z + Position.Z);
            float rThickness = Thickness > 1e-8f ? 1f / Thickness : 1f;

            if (waterDepth > 0 && WaterResistance > 0)
            {
                // Underwater: buoyancy + water drag
                float submerged = MathF.Min(1f, waterDepth * rThickness);
                vtx.Vel = vtx.Vel - Gravity * (submerged * VtxVol * dt);

                // Water flow resistance (wind acts as current underwater)
                float windage = vtx.Area * areaScale * WaterResistance;
                var flowDiff = currentWind - vtx.Vel;
                vtx.Vel = vtx.Vel + vtx.Normal * (vtx.Normal.Dot(flowDiff) * windage * dt);
            }
            else
            {
                // Air: wind drag force projected onto vertex normal (C++ cloth wind model)
                // F_wind = normal * (normal . (wind - vel)) * area * areaScale * airResistance
                if (AirResistance > 0 && vtx.Area > 0)
                {
                    float windage = vtx.Area * areaScale * AirResistance;
                    var windDiff = currentWind - vtx.Vel;
                    float normalProj = vtx.Normal.Dot(windDiff);
                    // C++ adds a windage factor from edge alignment; simplified here
                    float rnEdges = vtx.RnEdges > 0 ? vtx.RnEdges : 1f;
                    vtx.Vel = vtx.Vel + vtx.Normal * (normalProj * windage * (rnEdges + 1f) * dt);
                }
            }

            // Gravity
            vtx.Vel = vtx.Vel + Gravity * dt;

            // Damping
            vtx.Vel = vtx.Vel * MathF.Max(0f, 1f - Damping * dt);
        }

        // --- Shape stiffness (bending resistance via dihedral angle springs) ---
        // C++: kShapeStiffnessTang applies torques based on angle deviation between edge fan triangles
        // C++: kShapeStiffnessNorm applies forces to restore cone angles
        if ((KShapeStiffnessTang + KShapeStiffnessNorm) > 0)
        {
            for (int i = 0; i < NVtx; i++)
            {
                var vtx = Vertices[i];
                if (vtx.Attached > 0) continue;

                float angleSum = 0;
                // Tangential stiffness: torque from dihedral angle deviation
                for (int j = vtx.StartEdge; j <= vtx.EndEdge && j < VtxEdges.Length; j++)
                {
                    int j1 = j + 1;
                    if (j1 > vtx.EndEdge)
                    {
                        if (vtx.FullFan) j1 = vtx.StartEdge;
                        else break;
                    }
                    if (j1 < 0 || j1 >= VtxEdges.Length) break;

                    int ei0 = VtxEdges[j];
                    int ei1 = VtxEdges[j1];
                    if (ei0 < 0 || ei0 >= NEdges || ei1 < 0 || ei1 >= NEdges) continue;

                    ref var e0 = ref Edges[ei0];
                    ref var e1 = ref Edges[ei1];
                    int oi0 = e0.IVtx0 + e0.IVtx1 - i;
                    int oi1 = e1.IVtx0 + e1.IVtx1 - i;
                    if (oi0 < 0 || oi0 >= NVtx || oi1 < 0 || oi1 >= NVtx) continue;

                    var edge0 = Vertices[oi0].Pos - vtx.Pos;
                    var edge1 = Vertices[oi1].Pos - vtx.Pos;

                    // Accumulate cone angle for normal stiffness
                    if (e0.RLen > 0)
                        angleSum += 1f - vtx.Normal.Dot(edge0) * e0.RLen;

                    if (KShapeStiffnessTang > 0)
                    {
                        var cross = edge0 ^ edge1;
                        float crossLen = cross.Length();
                        if (crossLen > 1e-10f)
                        {
                            int side = i == e0.IVtx1 ? 1 : 0;
                            float angle0 = side == 0 ? e0.Angle0_0 : e0.Angle0_1;
                            float sg = edge1.Dot(edge0) >= 0 ? 1f : -1f;
                            float correction = (angle0 - 1f + sg) / crossLen - e0.RLen * e1.RLen * sg;
                            var torque = cross * (correction * dt * KShapeStiffnessTang);

                            if (oi0 >= 0 && oi0 < NVtx && Vertices[oi0].Attached == 0)
                                Vertices[oi0].Vel = Vertices[oi0].Vel - (torque ^ edge0);
                            if (oi1 >= 0 && oi1 < NVtx && Vertices[oi1].Attached == 0)
                                Vertices[oi1].Vel = Vertices[oi1].Vel + (torque ^ edge1);
                        }
                    }
                }

                // Normal stiffness: restore cone angle
                if (KShapeStiffnessNorm > 0)
                {
                    float angleDiff = angleSum - vtx.Angle0;
                    for (int j = vtx.StartEdge; j <= vtx.EndEdge && j < VtxEdges.Length; j++)
                    {
                        int ei = VtxEdges[j];
                        if (ei < 0 || ei >= NEdges) continue;

                        ref var e = ref Edges[ei];
                        int oi = e.IVtx0 + e.IVtx1 - i;
                        if (oi < 0 || oi >= NVtx) continue;

                        var edgeVec = Vertices[oi].Pos - vtx.Pos;
                        float edgeLenSq = edgeVec.LengthSq();
                        var projected = vtx.Normal * edgeLenSq - edgeVec * vtx.Normal.Dot(edgeVec);
                        var force = projected * (e.RLen * angleDiff * dt * KShapeStiffnessNorm);

                        if (Vertices[oi].Attached == 0)
                            Vertices[oi].Vel = Vertices[oi].Vel + force;
                        if (vtx.Attached == 0)
                            vtx.Vel = vtx.Vel - force;
                    }
                }
            }
        }

        // --- Animation stiffness: pull vertices toward target pose ---
        if (StiffnessAnim > 0)
        {
            float denom = MaxLevelDenom > 0 ? MaxLevelDenom : 1f;
            for (int j = NAttachedVtx; j < NConnectedVtx && j < NVtx; j++)
            {
                int i = Vertices[j].Idx;
                if (i < 0 || i >= NVtx) continue;
                var vtx = Vertices[i];

                var target = LastQHost.Rotate(vtx.PtAttach) + LastPosHost;
                var diff = target - vtx.Pos - Position - Offs0;
                float decay = 1f - StiffnessDecayAnim * vtx.Sorted * denom;
                var v = diff * (StiffnessAnim * MathF.Max(0f, decay));

                float t = MathF.Max(0f, 1f - DampingAnim * dt);
                vtx.Vel = vtx.Vel * t + v * (1f - t);
            }
        }

        // --- Integrate positions ---
        MaxMove = 0;
        for (int i = 0; i < NVtx; i++)
        {
            var vtx = Vertices[i];
            if (vtx.Attached > 0 || vtx.MassInv <= 0) continue;

            var pos0 = vtx.Pos;
            vtx.Pos = vtx.Pos + vtx.Vel * dt;

            // Clamp to max allowed distance
            if (MaxAllowedDist > 0 && vtx.Pos.LengthSq() > MaxAllowedDist * MaxAllowedDist)
            {
                vtx.Pos = vtx.Pos.Normalized() * MaxAllowedDist;
                vtx.Vel = PhysVector3.Zero;
            }

            float moveSq = (pos0 - vtx.Pos).LengthSq();
            if (moveSq > MaxMove) MaxMove = moveSq;
        }

        // --- Collision with environment (simplified ground plane at z=0) ---
        if (Thickness > 0)
        {
            for (int i = 0; i < NVtx; i++)
            {
                var vtx = Vertices[i];
                if (vtx.Attached > 0 || vtx.MassInv <= 0) continue;

                // Ground collision
                float groundZ = Thickness;
                if (vtx.Pos.Z + Position.Z < groundZ)
                {
                    vtx.Pos = new PhysVector3(vtx.Pos.X, vtx.Pos.Y, groundZ - Position.Z);
                    vtx.NContact = PhysVector3.UnitZ;
                    vtx.VContact = PhysVector3.Zero;
                    vtx.VReq = MathF.Max(0f, Thickness - (vtx.Pos.Z + Position.Z - groundZ)) * 10f;

                    // Mark contact (simplified - no actual entity tracking)
                    if (vtx.ContactEnt == null)
                    {
                        // Could track ground entity here
                    }
                }
            }
        }

        // --- Contact constraint enforcement within edge spring iterations ---
        // Enforce spring constraints using velocity-based Gauss-Seidel (matching C++ softentity.cpp)
        for (int iter = 0; iter < NMaxIters; iter++)
        {
            float rmax = 0;
            for (int i = 0; i < NEdges; i++)
            {
                ref var edge = ref Edges[i];
                var v0 = Vertices[edge.IVtx0];
                var v1 = Vertices[edge.IVtx1];
                var delta = v1.Pos - v0.Pos;
                float len = delta.Length();
                if (len < 1e-10f) continue;

                var dir = delta / len;
                float vreq = (len - edge.Len0) * Ks;
                float relVel = (v0.Vel - v1.Vel).Dot(dir);
                float impulse = MathF.Min(100f, relVel - vreq);
                var F = dir * (-impulse);

                if (v0.MassInv > 0 && v0.Attached == 0)
                    v0.Vel = v0.Vel + F * (v0.MassInv * edge.KMass);
                if (v1.MassInv > 0 && v1.Attached == 0)
                    v1.Vel = v1.Vel - F * (v1.MassInv * edge.KMass);

                rmax = MathF.Max(rmax, -vreq);
            }

            // Enforce contact constraints within iterations (C++ pattern)
            for (int i = 0; i < NVtx; i++)
            {
                var vtx = Vertices[i];
                if (vtx.ContactEnt == null || vtx.Attached > 0) continue;

                float vn = vtx.NContact.Dot(vtx.Vel - vtx.VContact) - vtx.VReq;
                if (vn < 0)
                {
                    vtx.Vel = vtx.Vel - vtx.NContact * vn;
                    rmax = MathF.Max(rmax, -vn);

                    // Apply friction
                    if (Friction > 0 && vn < 0)
                    {
                        var v = vtx.Vel - vtx.VContact;
                        v = v - vtx.NContact * vtx.NContact.Dot(v);
                        float tangSpeed = v.Length();
                        float frictionForce = MathF.Abs(vn) * Friction;
                        if (tangSpeed > 0 && frictionForce > 0)
                        {
                            if (frictionForce >= tangSpeed)
                                vtx.Vel = vtx.VContact;
                            else
                                vtx.Vel = vtx.Vel - v * (frictionForce / tangSpeed);
                        }
                    }
                }
            }

            if (rmax < Accuracy) break;
        }

        // --- Clamp velocities ---
        for (int i = 0; i < NVtx; i++)
        {
            if (Vertices[i].Vel.LengthSq() > 20f * 20f)
                Vertices[i].Vel = Vertices[i].Vel.Normalized() * 20f;
        }

        // --- Compute energy for sleep check ---
        _energy = 0;
        for (int i = 0; i < NVtx; i++)
        {
            if (Vertices[i].Attached == 0 && Vertices[i].Mass > 0)
                _energy += Vertices[i].Vel.LengthSq() * Vertices[i].Mass;
        }
        _energy *= 0.5f;

        PrevTimeInterval = dt;
        TimeStepPerformed = dt;
        ComputeBBox();

        // Sleep check
        if (_energy < Emin)
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
    /// Pin (attach) a vertex so it doesn't move during simulation.
    /// </summary>
    public void AttachVertex(int vtxIdx)
    {
        if (vtxIdx >= 0 && vtxIdx < NVtx)
        {
            Vertices[vtxIdx].Attached = 1;
            Vertices[vtxIdx].Vel = PhysVector3.Zero;
        }
    }

    /// <summary>
    /// Unpin (detach) a vertex so it participates in simulation.
    /// </summary>
    public void DetachVertex(int vtxIdx)
    {
        if (vtxIdx >= 0 && vtxIdx < NVtx)
            Vertices[vtxIdx].Attached = 0;
    }

    protected override void ComputeBBox()
    {
        if (NVtx == 0) { BBoxMin = BBoxMax = Position; return; }
        var min = Vertices[0].Pos + Position;
        var max = min;
        for (int i = 1; i < NVtx; i++)
        {
            var p = Vertices[i].Pos + Position;
            min = PhysVector3.Min(min, p);
            max = PhysVector3.Max(max, p);
        }
        float pad = MathF.Max(Thickness, 0.05f);
        var padV = new PhysVector3(pad, pad, pad);
        BBoxMin = min - padV;
        BBoxMax = max + padV;
    }
}

/// <summary>Soft body parameters. Port of pe_params_softbody.</summary>
public class ParamsSoftBody : PhysicsParamsBase
{
    public override int TypeId => 14;
    public float? Thickness { get; set; }
    public float? MaxSafeStep { get; set; }
    public float? Ks { get; set; }
    public float? KdRatio { get; set; }
    public float? Friction { get; set; }
    public float? WaterResistance { get; set; }
    public float? AirResistance { get; set; }
    public PhysVector3? Wind { get; set; }
    public float? WindVariance { get; set; }
    public int? NMaxIters { get; set; }
    public float? Accuracy { get; set; }
    public float? ImpulseScale { get; set; }
    public float? ExplosionScale { get; set; }
    public float? CollisionImpulseScale { get; set; }
    public float? MaxCollisionImpulse { get; set; }
    public int? CollTypes { get; set; }
    public float? MassDecay { get; set; }
    public float? ShapeStiffnessNorm { get; set; }
    public float? ShapeStiffnessTang { get; set; }
    public float? StiffnessAnim { get; set; }
    public float? StiffnessDecayAnim { get; set; }
    public float? DampingAnim { get; set; }
    public float? MaxDistAnim { get; set; }
    public float? HostSpaceSim { get; set; }
}

/// <summary>Soft body status. Port of pe_status_softbody.</summary>
public class StatusSoftBody : PhysicsStatusBase
{
    public override int TypeId => 14;
    public int NVtx { get; set; }
    public int NEdges { get; set; }
    public int NAttached { get; set; }
}
