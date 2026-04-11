// Port of CryPhysics rigidbody.h/cpp - rigid body dynamics state
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;
using CryPhysics.Math;

namespace CryPhysics.Dynamics;

/// <summary>
/// Rigid body state and dynamics computation.
/// Faithful port of RigidBody class from CryEngine rigidbody.h/.cpp.
/// </summary>
public class RigidBody
{
    // State
    public PhysVector3 Pos;                                   // Position
    public PhysQuaternion Q = PhysQuaternion.Identity;        // Orientation
    public PhysVector3 P;                                     // Linear momentum
    public PhysVector3 L;                                     // Angular momentum
    public PhysVector3 W;                                     // Angular velocity
    public PhysVector3 V;                                     // Linear velocity

    // Mass properties
    public float M;                                           // Total mass
    public float Minv;                                        // 1/mass (0 for static)
    public float Vol;                                         // Volume (C++ name: V, renamed to avoid clash)
    public Diag33 Ibody;                                      // Diagonalized body-space inertia tensor
    public Diag33 IbodyInv;                                   // Inverse of Ibody
    public PhysQuaternion Qfb = PhysQuaternion.Identity;      // Frame->body rotation
    public PhysVector3 Offsfb;                                // Frame->body offset
    public int Integrator;                                    // 0 = explicit Euler, else Runge-Kutta

    // Derived
    public PhysMatrix33 Iinv = PhysMatrix33.Identity;         // World-space inverse inertia I^-1(t)

    // Collision accumulators
    public PhysVector3 Fcollision;                            // Collision force
    public PhysVector3 Tcollision;                            // Collision torque
    public float Eunproj;                                     // Unprojection energy

    // Per-thread processing flags (single-threaded: just one element)
    public int BProcessed;

    /// <summary>
    /// Constructor. Matches C++ RigidBody::RigidBody() - zeroes everything.
    /// </summary>
    public RigidBody()
    {
        Zero();
    }

    /// <summary>
    /// Initialize rigid body from mesh properties.
    /// Port of RigidBody::Create from rigidbody.cpp.
    /// </summary>
    /// <param name="center">Center of mass in world space</param>
    /// <param name="ibody0">Diagonalized inertia tensor (un-scaled by density)</param>
    /// <param name="q0">Body orientation</param>
    /// <param name="volume">Volume of the body</param>
    /// <param name="mass">Total mass</param>
    /// <param name="qframe">Frame quaternion (entity frame)</param>
    /// <param name="posframe">Frame position (entity position)</param>
    public void Create(in PhysVector3 center, in PhysVector3 ibody0, in PhysQuaternion q0,
                       float volume, float mass, in PhysQuaternion qframe, in PhysVector3 posframe)
    {
        float density = mass / volume;
        Vol = volume; M = mass; Q = q0; Pos = center;

        if (M > 0)
        {
            Ibody = new Diag33(ibody0.X * density, ibody0.Y * density, ibody0.Z * density);
            IbodyInv = Ibody.Inverted();
            Minv = 1.0f / M;
        }
        else
        {
            Ibody = new Diag33(0, 0, 0);
            IbodyInv = new Diag33(0, 0, 0);
            Minv = 0;
        }

        // qfb = !q * qframe  (inverse of q, then qframe)
        Qfb = Q.Conjugate() * qframe;
        // offsfb = (pos - posframe) * qframe
        Offsfb = qframe.InverseRotate(Pos - posframe);

        UpdateState();
    }

    /// <summary>
    /// Add another body's mass and inertia via parallel axis theorem + Jacobi re-diagonalization.
    /// Port of RigidBody::Add from rigidbody.cpp.
    /// </summary>
    public void Add(in PhysVector3 center, in PhysVector3 ibodyOp, in PhysQuaternion qOp,
                    float volume, float mass)
    {
        if (mass == 0f) return;
        if (MathF.Abs(M + mass) < M * 0.0001f) { Zero(); return; }

        float density = mass / volume;

        // Build the other body's inertia in this body's frame
        var ropQ = Q.Conjugate() * qOp;
        var rop = new PhysMatrix33(ropQ);
        var ibodyOpMtx = PhysMatrix33.Diagonal(ibodyOp.X * density, ibodyOp.Y * density, ibodyOp.Z * density);
        var iop = rop * ibodyOpMtx * rop.Transposed();

        // New center of mass
        var posNew = (Pos * M + center * mass) / (M + mass);

        // Offset both inertia tensors to new COM using parallel axis theorem
        PhysMatrix33 ibodyMtx = Ibody; // implicit conversion Diag33 -> PhysMatrix33
        MathUtils.OffsetInertiaTensor(ref ibodyMtx, Q.InverseRotate(posNew - Pos), M);
        MathUtils.OffsetInertiaTensor(ref iop, Q.InverseRotate(posNew - center), mass);

        M += mass; Vol += volume; ibodyMtx = ibodyMtx + iop; Minv = 1.0f / M;

        // Update frame offset
        var qframe = Q * Qfb;
        Offsfb = Offsfb + qframe.InverseRotate(posNew - Pos);

        // Re-diagonalize combined inertia via Jacobi eigenvalue decomposition
        using var eigenMtx = new MatrixNM(3, 3, MatrixFlags.Symmetric);
        eigenMtx[0, 0] = ibodyMtx.M00; eigenMtx[0, 1] = ibodyMtx.M01; eigenMtx[0, 2] = ibodyMtx.M02;
        eigenMtx[1, 0] = ibodyMtx.M10; eigenMtx[1, 1] = ibodyMtx.M11; eigenMtx[1, 2] = ibodyMtx.M12;
        eigenMtx[2, 0] = ibodyMtx.M20; eigenMtx[2, 1] = ibodyMtx.M21; eigenMtx[2, 2] = ibodyMtx.M22;

        using var evec = new MatrixNM(3, 3);
        float[] eval = new float[3];
        eigenMtx.JacobiTransformation(evec, eval);

        Ibody = new Diag33(eval[0], eval[1], eval[2]);
        IbodyInv = Ibody.Inverted();

        // Build rotation from eigenvectors
        var rbasis = new PhysMatrix33(
            evec[0, 0], evec[0, 1], evec[0, 2],
            evec[1, 0], evec[1, 1], evec[1, 2],
            evec[2, 0], evec[2, 1], evec[2, 2]
        );
        var qb2nb = new PhysQuaternion(rbasis);
        Q = Q * qb2nb.Conjugate();
        Qfb = qb2nb * Qfb;
        Pos = posNew;

        UpdateState();
    }

    /// <summary>
    /// Zero all state. Port of RigidBody::zero().
    /// </summary>
    public void Zero()
    {
        M = Minv = Vol = 0;
        Ibody = default; IbodyInv = default;
        P = PhysVector3.Zero; L = PhysVector3.Zero;
        V = PhysVector3.Zero; W = PhysVector3.Zero;
        Pos = PhysVector3.Zero;
        Q = PhysQuaternion.Identity;
        Qfb = PhysQuaternion.Identity;
        Offsfb = PhysVector3.Zero;
        Iinv = PhysMatrix33.Zero;
        Fcollision = PhysVector3.Zero; Tcollision = PhysVector3.Zero;
        Eunproj = 0;
        Integrator = 0;
        BProcessed = 0;
    }

    /// <summary>
    /// Update world-space inverse inertia and velocities from momenta.
    /// Port of RigidBody::UpdateState from rigidbody.cpp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateState()
    {
        var R = new PhysMatrix33(Q);
        Iinv = R * (PhysMatrix33)IbodyInv * R.Transposed();
        if (Minv > 0)
        {
            V = P * Minv;
            W = Iinv * L;
        }
    }

    /// <summary>
    /// Integrate rigid body forward by dt.
    /// Port of RigidBody::Step from rigidbody.cpp.
    /// NOTE: This method does NOT apply forces/damping - that is done at the entity level.
    /// It only integrates position and orientation, and optionally uses RK4 for rotation.
    /// </summary>
    public void Step(float dt)
    {
        UpdateState();

        // Integrate position
        Pos = Pos + V * dt;

        // Integrate orientation
        float E0 = 0;
        PhysQuaternion dq;

        if (Integrator == 0)
        {
            // Explicit Euler
            dq = WDt(W, dt);
        }
        else
        {
            // 4th-order Runge-Kutta for orientation
            E0 = L.Dot(W);
            var w1 = W;
            var q2 = WDt(w1, dt * 0.5f) * Q; var w2 = q2.Rotate(IbodyInv * q2.InverseRotate(L));
            var q3 = WDt(w2, dt * 0.5f) * Q; var w3 = q3.Rotate(IbodyInv * q3.InverseRotate(L));
            var q4 = WDt(w3, dt) * Q;         var w4 = q4.Rotate(IbodyInv * q4.InverseRotate(L));
            dq = WDt((w1 + w4 + (w2 + w3) * 2f) * (1.0f / 6f), dt);
        }

        Q = dq * Q;
        Q.Normalize();

        // Re-derive world-space inertia and angular velocity from new orientation
        if (Minv > 0)
        {
            var R = new PhysMatrix33(Q);
            Iinv = R * (PhysMatrix33)IbodyInv * R.Transposed();
            W = Iinv * L;
        }

        // Energy conservation correction for RK4
        float E1 = L.Dot(W);
        if (E1 * Integrator > 0.001f)
            W = W * (E0 / E1);
    }

    /// <summary>
    /// Accumulate contact matrix K for impulse response.
    /// Port of RigidBody::GetContactMatrix from rigidbody.cpp.
    /// K -= [r]x * Iinv * [r]x; K(i,i) += Minv
    /// Note: modifies K in place (accumulates), matching C++ semantics.
    /// </summary>
    public void GetContactMatrix(in PhysVector3 r, ref PhysMatrix33 K)
    {
        var rmtx = PhysMatrix33.CrossProductMatrix(r);
        var temp = rmtx * Iinv * rmtx;
        K = K - temp;
        K.M00 += Minv; K.M11 += Minv; K.M22 += Minv;
    }

    /// <summary>Apply an impulse at a world-space point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyImpulse(in PhysVector3 impulse, in PhysVector3 point)
    {
        P = P + impulse;
        L = L + (point - Pos).Cross(impulse);
    }

    /// <summary>Apply a pure angular impulse.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyAngularImpulse(in PhysVector3 angImpulse)
    {
        L = L + angImpulse;
    }

    /// <summary>Compute kinetic energy: 0.5 * (m*v^2 + w.L).</summary>
    public float Energy => (V.LengthSq() * M + W.Dot(L)) * 0.5f;

    /// <summary>Get velocity at a world point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetVelocityAt(in PhysVector3 point)
    {
        return V + W.Cross(point - Pos);
    }

    /// <summary>
    /// Construct quaternion from angular velocity * dt using exponential map.
    /// Port of inline w_dt() from rigidbody.cpp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PhysQuaternion WDt(in PhysVector3 w, float dt)
    {
        float wlen = w.Length();
        if (wlen < 0.001f)
        {
            float halfDt = dt * 0.5f;
            return new PhysQuaternion(MathF.Cos(wlen * halfDt), w.X * halfDt, w.Y * halfDt, w.Z * halfDt);
        }
        float halfAngle = wlen * dt * 0.5f;
        float sinHA = MathF.Sin(halfAngle) / wlen;
        return new PhysQuaternion(MathF.Cos(halfAngle), w.X * sinHA, w.Y * sinHA, w.Z * sinHA);
    }
}

/// <summary>
/// Contact flags matching C++ contactflags enum from rigidbody.h.
/// </summary>
[Flags]
public enum ContactFlags
{
    CountMask = 0x3F,
    New = 0x40,
    Verified2b = 0x80,
    Angular = 0x100,
    Constraint3Dof = 0x200,
    Constraint2Dof = 0x400,
    Constraint1Dof = 0x800,
    SolveFor = 0x1000,
    Constraint = Constraint3Dof | Constraint2Dof | Constraint1Dof,
    BodyIdx = 0x2000,
    MaintainCount = 0x4000,
    Wheel = 0x8000,
    UseC1Dof = 0x10000,
    UseC2Dof = 0x20000,
    UseC = UseC1Dof | UseC2Dof,
    Inexact = 0x40000,
    Last = 0x80000,
    Remove = 0x100000,
    Archived = 0x200000,
    Rope = 0x400000,
    PreservePspare = 0x800000,
    Area = 0x1000000,
    RopeStretchy = 0x2000000,
}
