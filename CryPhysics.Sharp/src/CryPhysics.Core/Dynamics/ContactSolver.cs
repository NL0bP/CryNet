// Port of contact solver from rigidbody.cpp (InvokeContactSolver, InitContactSolver, RegisterContact)
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Dynamics;

/// <summary>
/// Solver settings. Port of SolverSettings from CryEngine.
/// </summary>
public class SolverSettings
{
    public float AccuracyMC = 0.005f;
    public float AccuracyLCPCG = 0.005f;
    public int NMinMCiters = 4;
    public int NMaxMCiters = 6000;
    public int NMaxLCPCGiters = 40;
    public int MaxLCPCGContacts = 100;
    public float MaxMCMassRatio = 50f;
    public float MaxMCVel = 15f;
    public float MinSeparationSpeed = 0.02f;
}

/// <summary>
/// Per-contact helper data for the PGS solver.
/// Port of contact_helper from rigidbody.cpp.
/// </summary>
public struct ContactHelper
{
    public PhysVector3 R0, R1;            // Contact point offsets from body COM
    public PhysMatrix33 K;                // Contact mass matrix
    public PhysVector3 N;                 // Contact normal
    public PhysVector3 Vreq;              // Required velocity at contact
    public float Pspare;                  // Spare impulse (friction budget)
    public float Friction;
    public int Flags;
    public int IBody0, IBody1;            // Indices into Bodies array
    public int ICount;
    public int ICountDst;
    public float Pn;                      // Accumulated normal impulse
}

/// <summary>
/// Per-body helper data for the solver.
/// Port of body_helper from rigidbody.cpp.
/// </summary>
public struct BodyHelper
{
    public PhysVector3 V, W;              // Velocity, angular velocity
    public float Minv, M;                 // Inverse mass, mass
    public PhysMatrix33 Iinv;             // World-space inverse inertia
    public PhysVector3 L;                 // Angular momentum
}

/// <summary>
/// Per-contact constraint helper.
/// Port of contact_helper_constraint from rigidbody.cpp.
/// </summary>
public struct ContactConstraintHelper
{
    public PhysMatrix33 C;                // Constraint projection matrix
    public PhysMatrix33 Kinv;             // Inverse contact mass in constraint space
}

/// <summary>
/// Per-contact CG solver data.
/// Port of contact_helper_CG from rigidbody.cpp.
/// </summary>
public struct ContactCGHelper
{
    public PhysVector3 DP, P;             // Search direction, accumulated impulse
    public float DPn;                     // Normal component of P
    public PhysVector3 Vrel;              // Relative velocity at contact
    public PhysVector3 R;                 // Residual
}

/// <summary>
/// Sandwich triplet: two "bread" bodies on the outside squeezing a "middle" body.
/// Port of contact_sandwich from rigidbody.cpp (line 181).
/// Used by the LCP-CG solver to detect velocity conflicts between bodies.
/// </summary>
public class ContactSandwich
{
    public int IMiddle;
    public int IBread0, IBread1;
    public ContactSandwich? Next;
    public bool BProcessed;
}

/// <summary>
/// Buddy info: records that body iBody is a neighbor of the owning body,
/// with an integral contact-normal direction vreq.
/// Port of buddy_info from rigidbody.cpp (line 187).
/// </summary>
public class BuddyInfo
{
    public int IBody;
    public PhysVector3 Vreq;
    public int Flags;
    public BuddyInfo? Next;
}

/// <summary>
/// Follower thunk for unprojection route traversal.
/// Port of follower_thunk from rigidbody.cpp (line 193).
/// </summary>
public class FollowerThunk
{
    public int IBody;
    public FollowerThunk? Next;
}

/// <summary>
/// Per-body extended info for the LCP-CG unprojection solver.
/// Port of body_info from rigidbody.cpp (line 197).
/// </summary>
public class BodyInfo
{
    public BuddyInfo? PBuddy;
    public ContactSandwich? PSandwich;
    public FollowerThunk? PFollower;
    public float Minv;
    public int ILevel = -1;
    public int IdUpdate;
    public int Idx;
    public PhysVector3 VUnproj, WUnproj;
    public PhysVector3 Fcollision, Tcollision;
}

/// <summary>
/// Global multibody contact solver.
/// Faithful port of the PGS (Projected Gauss-Seidel) solver from rigidbody.cpp.
/// Thread safety: the solver itself runs sequentially on the main thread.
/// Per-thread contact collection is handled via SolverThreadData in CallerContext,
/// then merged into the solver via MergeThreadContacts before solving.
/// </summary>
public class ContactSolver
{
    public const int MaxContacts = 9984;

    // Solver state
    private int _nContacts;
    private int _nBodies;
    private readonly EntityContact[] _contacts = new EntityContact[MaxContacts];
    private readonly RigidBody[] _bodies = new RigidBody[MaxContacts];
    private readonly ContactHelper[] _contactsRB = new ContactHelper[MaxContacts];
    private readonly ContactCGHelper[] _contactsCG = new ContactCGHelper[MaxContacts];
    private readonly ContactConstraintHelper[] _contactsC = new ContactConstraintHelper[MaxContacts];
    private readonly BodyHelper[] _bodyHelpers = new BodyHelper[MaxContacts];
    private bool _usePreCG = true;
    private readonly object _registerLock = new();

    /// <summary>
    /// Initialize solver for a new frame.
    /// Port of InitContactSolver from rigidbody.cpp.
    /// </summary>
    public void Init()
    {
        _nContacts = 0;
        _nBodies = 0;
        _usePreCG = true;
    }

    /// <summary>
    /// Register a contact for solving.
    /// Port of RegisterContact from rigidbody.cpp.
    /// </summary>
    public void RegisterContact(EntityContact contact)
    {
        if (_nContacts >= MaxContacts - 1) return;
        _contacts[_nContacts++] = contact;
    }

    /// <summary>
    /// Thread-safe contact registration for use during parallel contact detection.
    /// </summary>
    public void RegisterContactThreadSafe(EntityContact contact)
    {
        lock (_registerLock)
        {
            RegisterContact(contact);
        }
    }

    /// <summary>
    /// Merge pending contacts from all worker thread SolverThreadData into the solver.
    /// Called on the main thread after the parallel contact detection phase completes.
    /// Port of the contact merging step from CPhysicalWorld::TimeStep.
    /// </summary>
    public void MergeThreadContacts(IReadOnlyList<Threading.SolverThreadData> threadData)
    {
        foreach (var td in threadData)
        {
            for (int i = 0; i < td.NPendingContacts; i++)
            {
                RegisterContact(td.PendingContacts[i]);
            }
            td.Reset();
        }
    }

    public void DisablePreCG() { _usePreCG = false; }

    /// <summary>
    /// Main solver entry point.
    /// Port of InvokeContactSolver from rigidbody.cpp.
    /// Returns number of bodies processed.
    /// </summary>
    public int Solve(float timeInterval, SolverSettings settings, float eBefore,
                     out EntityContact[] contactsOut, out int nContactsOut)
    {
        contactsOut = _contacts;
        nContactsOut = _nContacts;
        if (_nContacts == 0) return 0;

        int i, j, iop, nBodies = 0;
        float e = settings.AccuracyMC;

        // Phase 1: Assign body indices and build contact matrices
        var bodyIndex = new Dictionary<RigidBody, int>();

        for (i = 0; i < _nContacts; i++)
        {
            var contact = _contacts[i];
            for (iop = 0; iop < 2; iop++)
            {
                var body = iop == 0 ? contact.PBody0! : contact.PBody1!;
                if (!bodyIndex.TryGetValue(body, out j))
                {
                    _bodies[nBodies] = body;
                    bodyIndex[body] = nBodies;
                    j = nBodies++;
                }
                if (iop == 0)
                    _contactsRB[i].IBody0 = j;
                else
                    _contactsRB[i].IBody1 = j;
            }

            contact.ICount = 0;
            contact.Pspare = (contact.Flags & (int)ContactFlags.PreservePspare) != 0 ? contact.Pspare : 0;

            _contactsC[i].C = PhysMatrix33.Identity;
            _contactsCG[i].P = PhysVector3.Zero;
            contact.BProcessed = i;

            // Build contact mass matrix K
            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                _contactsRB[i].K = PhysMatrix33.Zero;
                contact.PBody0!.GetContactMatrix(contact.Pt0 - contact.PBody0.Pos, ref _contactsRB[i].K);
                contact.PBody1!.GetContactMatrix(contact.Pt1 - contact.PBody1.Pos, ref _contactsRB[i].K);
            }
            else
            {
                _contactsRB[i].K = contact.PBody0!.Iinv + contact.PBody1!.Iinv;
            }

            // Constraint Kinv setup
            if ((contact.Flags & (int)ContactFlags.Constraint3Dof) != 0)
            {
                _contactsC[i].Kinv = _contactsRB[i].K.Inverted();
            }
            else if ((contact.Flags & (int)ContactFlags.Constraint2Dof) != 0)
            {
                float t = contact.N.Dot(_contactsRB[i].K * contact.N);
                _contactsC[i].Kinv = DotProductMatrix(contact.N, contact.N);
                if (MathF.Abs(t) > 1e-20f)
                    _contactsC[i].Kinv = _contactsC[i].Kinv * (1f / t);
                _contactsC[i].C = DotProductMatrix(contact.N, contact.N);
            }

            _contactsRB[i].Pn = 0;
        }

        _nBodies = nBodies;

        // Phase 2: Copy body state into solver helpers
        for (i = 0; i < nBodies; i++)
        {
            _bodies[i].Eunproj = 0;
        }

        // Phase 3: Setup solver contact helpers
        int nMaxIters = System.Math.Max(settings.NMinMCiters * _nContacts, settings.NMaxMCiters);

        for (i = 0; i < _nContacts; i++)
        {
            var contact = _contacts[i];
            _contactsRB[i].R0 = contact.Pt0 - contact.PBody0!.Pos;
            _contactsRB[i].R1 = contact.Pt1 - contact.PBody1!.Pos;
            _contactsRB[i].N = contact.N;
            _contactsRB[i].Vreq = contact.Vreq;
            _contactsRB[i].Pspare = contact.Pspare;
            _contactsRB[i].Flags = contact.Flags;
            _contactsRB[i].Friction = contact.Friction;
            _contactsRB[i].ICount = contact.ICount;
            _contactsRB[i].ICountDst = i; // simplified
            _contactsRB[i].Pn = 0;
        }

        for (i = 0; i < nBodies; i++)
        {
            _bodies[i].Fcollision = _bodies[i].P;
            _bodies[i].Tcollision = _bodyHelpers[i].L = _bodies[i].L;
            _bodyHelpers[i].V = _bodies[i].V;
            _bodyHelpers[i].W = _bodies[i].W;
            _bodyHelpers[i].Minv = _bodies[i].Minv;
            _bodyHelpers[i].Iinv = _bodies[i].Iinv;
            _bodyHelpers[i].M = _bodies[i].M;
        }

        // Phase 3b: Pre-CG pass (conjugate gradient before PGS)
        if (_usePreCG && _nContacts < 16)
        {
            int preCGResult = SolvePreCG(_nContacts, nBodies, e, eBefore, timeInterval);
            if (preCGResult > 0)
            {
                // PreCG yielded acceptable results, apply and return
                for (i = 0; i < _nContacts; i++)
                    _contacts[i].Pspare = _contactsCG[i].P.Dot(_contacts[i].N);

                return _nBodies;
            }
        }

        // Phase 4: Run PGS solver
        int bBounced = SolveMC(_nContacts, nBodies, eBefore, nMaxIters, e, settings.MinSeparationSpeed);

        // Phase 5: Apply results back to bodies
        for (i = 0; i < nBodies; i++)
        {
            _bodies[i].V = _bodyHelpers[i].V;
            _bodies[i].P = _bodyHelpers[i].V * _bodies[i].M;
            _bodies[i].W = _bodyHelpers[i].W;
            _bodies[i].L = _bodies[i].Q.Rotate(
                _bodies[i].Ibody * _bodies[i].Q.InverseRotate(_bodyHelpers[i].W));
        }

        for (i = 0; i < _nContacts; i++)
            _contacts[i].Pspare = _contactsRB[i].Pspare;

        return _nBodies;
    }

    /// <summary>
    /// Pre-CG (Conjugate Gradient) solver pass.
    /// Port of the PreCG block from InvokeContactSolver in rigidbody.cpp (lines 798-900).
    /// Tries to solve contacts via conjugate gradient before falling back to PGS.
    /// Returns positive value if CG produced acceptable results (caller should return early),
    /// or 0 if PGS fallback is needed.
    /// </summary>
    private int SolvePreCG(int nContacts, int nBodies, float e, float eBefore, float timeInterval)
    {
        int i;
        float r2 = 0, vmax = 0;
        PhysVector3 dp, r0, r1;

        // Step 1: Compute initial residuals r = vreq - C*relvel for each contact
        for (i = 0; i < nContacts; i++)
        {
            var contact = _contacts[i];
            var body0 = contact.PBody0!;
            var body1 = contact.PBody1!;

            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                r0 = contact.Pt0 - body0.Pos;
                r1 = contact.Pt1 - body1.Pos;
                dp = body0.V + body0.W.Cross(r0) - body1.V - body1.W.Cross(r1);
            }
            else
            {
                dp = body0.W - body1.W;
            }

            _contactsCG[i].R = _contactsCG[i].DP = contact.Vreq - _contactsC[i].C * dp;
            _contactsCG[i].P = PhysVector3.Zero;
            r2 += _contactsCG[i].R.LengthSq();
            vmax = MathF.Max(vmax, _contactsCG[i].R.LengthSq());
        }

        int iter = nContacts * 6;

        // Step 2: CG iterations
        do
        {
            // Zero out Fcollision/Tcollision on all bodies
            for (i = 0; i < nBodies; i++)
            {
                _bodies[i].Fcollision = PhysVector3.Zero;
                _bodies[i].Tcollision = PhysVector3.Zero;
            }

            // Accumulate search direction impulses onto bodies
            for (i = 0; i < nContacts; i++)
            {
                var contact = _contacts[i];
                var body0 = contact.PBody0!;
                var body1 = contact.PBody1!;

                if ((contact.Flags & (int)ContactFlags.Angular) == 0)
                {
                    r0 = contact.Pt0 - body0.Pos;
                    r1 = contact.Pt1 - body1.Pos;
                    body0.Fcollision += _contactsCG[i].DP;
                    body0.Tcollision += r0.Cross(_contactsCG[i].DP);
                    body1.Fcollision -= _contactsCG[i].DP;
                    body1.Tcollision -= r1.Cross(_contactsCG[i].DP);
                }
                else
                {
                    body0.Tcollision += _contactsCG[i].DP;
                    body1.Tcollision -= _contactsCG[i].DP;
                }
            }

            // Compute A*p (velocity change from search direction impulses)
            for (i = 0; i < nContacts; i++)
            {
                var contact = _contacts[i];
                var body0 = contact.PBody0!;
                var body1 = contact.PBody1!;

                if ((contact.Flags & (int)ContactFlags.Angular) == 0)
                {
                    r0 = contact.Pt0 - body0.Pos;
                    r1 = contact.Pt1 - body1.Pos;
                    dp = body0.Fcollision * body0.Minv + (body0.Iinv * body0.Tcollision).Cross(r0);
                    dp -= body1.Fcollision * body1.Minv + (body1.Iinv * body1.Tcollision).Cross(r1);
                }
                else
                {
                    dp = body0.Iinv * body0.Tcollision - body1.Iinv * body1.Tcollision;
                }

                _contactsCG[i].Vrel = _contactsC[i].C * dp;
            }

            // Compute step size: a = r2 / (p^T A p)
            float pAp = 0;
            for (i = 0; i < nContacts; i++)
                pAp += _contactsCG[i].Vrel.Dot(_contactsCG[i].DP);

            float a = MathF.Min(50f, r2 / MathF.Max(1e-10f, MathF.Abs(pAp)));

            // Update solution and residual
            float r2new = 0;
            for (i = 0; i < nContacts; i++)
            {
                _contactsCG[i].R -= _contactsCG[i].Vrel * a;
                r2new += _contactsCG[i].R.LengthSq();
                _contactsCG[i].P += _contactsCG[i].DP * a;
            }

            r2new = MathF.Max(1e-10f, r2new);
            if (r2new > r2 * 500 || r2new > 1e8f)
                break;

            // Update search direction: d = r + (r2new/r2)*d
            float b = r2new / MathF.Max(r2new * 0.001f, r2);
            r2 = r2new;
            vmax = 0;
            for (i = 0; i < nContacts; i++)
            {
                _contactsCG[i].DP = _contactsCG[i].DP * b + _contactsCG[i].R;
                vmax = MathF.Max(vmax, _contactsCG[i].R.LengthSq());
            }
        } while (--iter > 0 && vmax > e * e);

        // Step 3: Apply accumulated CG impulses to Fcollision/Tcollision
        for (i = 0; i < nBodies; i++)
        {
            _bodies[i].Fcollision = PhysVector3.Zero;
            _bodies[i].Tcollision = PhysVector3.Zero;
        }

        for (i = 0; i < nContacts; i++)
        {
            var contact = _contacts[i];
            var body0 = contact.PBody0!;
            var body1 = contact.PBody1!;

            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                body0.Fcollision += _contactsCG[i].P;
                body0.Tcollision += (contact.Pt0 - body0.Pos).Cross(_contactsCG[i].P);
                body1.Fcollision -= _contactsCG[i].P;
                body1.Tcollision -= (contact.Pt1 - body1.Pos).Cross(_contactsCG[i].P);
            }
            else
            {
                body0.Tcollision += _contactsCG[i].P;
                body1.Tcollision -= _contactsCG[i].P;
            }
        }

        // Step 4: Compute post-CG energy
        float eAfter = 0;
        for (i = 0; i < nBodies; i++)
        {
            eAfter += (_bodies[i].P + _bodies[i].Fcollision).Dot(
                        _bodies[i].V + _bodies[i].Fcollision * _bodies[i].Minv)
                    + (_bodies[i].L + _bodies[i].Tcollision).Dot(
                        _bodies[i].W + _bodies[i].Iinv * _bodies[i].Tcollision);
        }

        // Step 5: Check energy and friction constraints
        PhysVector3 n;
        float dPn, dPtang;
        for (i = 0; i < nContacts; i++)
        {
            var contact = _contacts[i];
            n = contact.N;
            dPn = _contactsCG[i].P.Dot(n);
            dPtang = (_contactsCG[i].P - n * dPn).LengthSq();
            float kmass = contact.PBody0!.Minv + contact.PBody1!.Minv;

            float vdiff;
            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                dp = _contactsCG[i].P * kmass;
                vdiff = -0.004f;
            }
            else
            {
                dp = contact.PBody0!.Iinv * _contactsCG[i].P + contact.PBody1!.Iinv * _contactsCG[i].P;
                vdiff = -0.015f;
            }

            // Check non-constraint contacts for violations
            bool violated = false;
            if ((contact.Flags & (int)ContactFlags.Constraint) == 0)
            {
                float frictionBudget = (dPn * contact.Friction + contact.Pspare) * kmass + 0.05f;
                violated = dp.Dot(n) < -0.05f ||
                           dPtang * kmass * kmass > frictionBudget * frictionBudget ||
                           (_contactsCG[i].R + contact.Vreq).Dot(n) < vdiff;
            }
            // Check constraint contacts with preserved Pspare
            else if ((contact.Flags & (int)ContactFlags.PreservePspare) != 0)
            {
                violated = dPn > contact.Pspare * 1.01f;
            }

            if (violated)
                break;
        }

        // Step 6: If all contacts passed checks, apply and return
        if (i == nContacts && eAfter < eBefore * 1.5f && vmax < 0.01f * 0.01f)
        {
            float rTimeInterval = timeInterval > 0 ? 1f / timeInterval : 0f;
            for (i = 0; i < nBodies; i++)
            {
                if (_bodies[i].M > 0)
                {
                    _bodies[i].P += _bodies[i].Fcollision;
                    _bodies[i].L += _bodies[i].Tcollision;
                    _bodies[i].V = _bodies[i].P * _bodies[i].Minv;
                    _bodies[i].W = _bodies[i].Iinv * _bodies[i].L;
                }
                _bodies[i].Fcollision *= rTimeInterval;
                _bodies[i].Tcollision *= rTimeInterval;
            }
            return nBodies; // Signal success
        }

        return 0; // Fall through to PGS
    }

    /// <summary>
    /// Projected Gauss-Seidel solver with friction.
    /// Port of InvokeContactSolverMC from rigidbody.cpp.
    /// </summary>
    private int SolveMC(int nContacts, int nBodies, float eBefore, int nMaxIters, float e, float minSeparationSpeed)
    {
        int i, bBounced, nBounces = 0;
        int bContactBounced;
        float vrel, dPn, dPtang, eAfter;
        PhysVector3 r0, r1, dp, dP = PhysVector3.Zero, n, Kdp;

        do
        {
            bBounced = 0;

            for (i = 0; i < nContacts; i++)
            {
                if (_contactsRB[i].ICount >= (_contactsRB[i].Flags & (int)ContactFlags.CountMask))
                {
                    ref var hbody0 = ref _bodyHelpers[_contactsRB[i].IBody0];
                    ref var hbody1 = ref _bodyHelpers[_contactsRB[i].IBody1];
                    r0 = _contactsRB[i].R0; r1 = _contactsRB[i].R1; n = _contactsRB[i].N;

                    // Compute relative velocity at contact
                    if ((_contactsRB[i].Flags & (int)ContactFlags.Angular) == 0)
                    {
                        dp = hbody0.V + hbody0.W.Cross(r0) - hbody1.V - hbody1.W.Cross(r1);
                    }
                    else
                    {
                        dp = hbody0.W - hbody1.W;
                    }
                    dp = dp - _contactsRB[i].Vreq;

                    if ((_contactsRB[i].Flags & (int)ContactFlags.UseC) != 0)
                        dp = _contactsC[i].C * dp;

                    bContactBounced = 0;

                    if ((_contactsRB[i].Flags & (int)ContactFlags.Constraint) != 0)
                    {
                        // Constraint contact
                        if ((_contactsC[i].C * dp).LengthSq() >
                            MathF.Max(e * e, _contactsRB[i].Vreq.LengthSq() * 0.0025f))
                        {
                            dP = _contactsC[i].Kinv * (-dp);
                            dPn = dP.Dot(n);

                            if (MathF.Min(_contactsRB[i].Pspare,
                                MathF.Abs(_contactsRB[i].Pn + dPn) - _contactsRB[i].Pspare * 1.01f) > 1e-5f)
                            {
                                float t = (_contactsRB[i].Pspare * 1.01f - MathF.Abs(_contactsRB[i].Pn)) /
                                          MathF.Abs(dPn);
                                dP = dP * t; dPn *= t;
                                bContactBounced = MathUtils.IsNeg(0.001f - t);
                            }
                            else
                            {
                                bContactBounced = 1;
                            }
                            _contactsRB[i].Pn += dPn;
                        }
                    }
                    else
                    {
                        // Normal contact with friction
                        vrel = dp.Dot(n);
                        if (vrel < 0 && (MathF.Abs(vrel) > e ||
                            (_contactsRB[i].Pspare > 0.0001f &&
                             (dp - n * vrel).LengthSq() > minSeparationSpeed * minSeparationSpeed)))
                        {
                            if (_contactsRB[i].Friction > 0.01f)
                            {
                                // Friction contact
                                float dpLen2 = dp.LengthSq();
                                float dpKdp = dp.Dot(_contactsRB[i].K * dp);
                                if (MathF.Abs(dpKdp) > 1e-20f)
                                {
                                    dP = dp * (-dpLen2 / dpKdp);
                                }
                                else
                                {
                                    dP = n * (-vrel / MathF.Max(1e-10f, n.Dot(_contactsRB[i].K * n)));
                                }

                                _contactsRB[i].Pn += dPn = dP.Dot(n);
                                _contactsRB[i].Pspare += dPn * _contactsRB[i].Friction;
                                dPtang = MathF.Sqrt(MathF.Max(0f, dP.LengthSq() - dP.Dot(n) * dP.Dot(n)));
                                _contactsRB[i].Pspare -= dPtang;

                                if (_contactsRB[i].Pspare < 0)
                                {
                                    // Friction cannot stop sliding - reduce tangential component
                                    if (dPtang > 1e-10f)
                                        dp = dp + (dp - n * vrel) * (_contactsRB[i].Pspare / dPtang);
                                    Kdp = _contactsRB[i].K * dp;
                                    float nKn = n.Dot(_contactsRB[i].K * n);
                                    if (n.Dot(Kdp) * n.Dot(Kdp) < Kdp.LengthSq() * 0.001f)
                                        dP = n * (-vrel / MathF.Max(1e-10f, nKn));
                                    else
                                    {
                                        float nKdp = n.Dot(Kdp);
                                        if (MathF.Abs(nKdp) > 1e-10f)
                                            dP = dp * (-vrel / nKdp);
                                        else
                                            dP = n * (-vrel / MathF.Max(1e-10f, nKn));
                                    }
                                    _contactsRB[i].Pspare = 0;
                                }
                            }
                            else
                            {
                                // Frictionless contact
                                float nKn = n.Dot(_contactsRB[i].K * n);
                                dPn = -vrel / MathF.Max(1e-10f, nKn);
                                dP = n * dPn;
                                _contactsRB[i].Pn += dPn;
                            }
                            bContactBounced = 1;
                        }
                    }

                    // Apply impulse to bodies
                    if (bContactBounced != 0)
                    {
                        if ((_contactsRB[i].Flags & (int)ContactFlags.UseC) != 0)
                            dP = _contactsC[i].C * dP;

                        if ((_contactsRB[i].Flags & (int)ContactFlags.Angular) == 0)
                        {
                            hbody0.V = hbody0.V + dP * hbody0.Minv;
                            dp = r0.Cross(dP);
                            hbody0.W = hbody0.W + hbody0.Iinv * dp;
                            hbody0.L = hbody0.L + dp;

                            hbody1.V = hbody1.V - dP * hbody1.Minv;
                            dp = r1.Cross(dP);
                            hbody1.W = hbody1.W - hbody1.Iinv * dp;
                            hbody1.L = hbody1.L - dp;
                        }
                        else
                        {
                            hbody0.W = hbody0.W + hbody0.Iinv * dP;
                            hbody0.L = hbody0.L + dP;
                            hbody1.W = hbody1.W - hbody1.Iinv * dP;
                            hbody1.L = hbody1.L - dP;
                        }

                        _contactsRB[_contactsRB[i].ICountDst].ICount++;
                        bBounced++; nBounces++;
                    }
                }
                _contactsRB[i].ICount = 0;
            }

            // Compute post-solve energy
            eAfter = 0f;
            for (i = 0; i < nBodies; i++)
                eAfter += _bodyHelpers[i].V.LengthSq() * _bodyHelpers[i].M +
                          _bodyHelpers[i].L.Dot(_bodyHelpers[i].W);

            nBounces += nContacts - bBounced >> 4;

        } while (bBounced != 0 && nBounces < nMaxIters && eAfter < eBefore * 3.0f);

        return bBounced;
    }

    // ========================================================================
    // Sandwich / buddy detection (port of lines 752-789 in rigidbody.cpp)
    // ========================================================================

    private readonly BodyInfo[] _bodyInfos = new BodyInfo[MaxContacts];
    private readonly ContactSandwich[] _sandwichBuf = new ContactSandwich[MaxContacts];
    private readonly BuddyInfo[] _buddyBuf = new BuddyInfo[MaxContacts];
    private readonly FollowerThunk[] _followerBuf = new FollowerThunk[MaxContacts];
    private int _nFollowers;
    private int _idLastUpdate;
    private int _nUnprojLoops;

    /// <summary>
    /// Detect whether the LCP-CG solver is needed by looking for velocity conflicts.
    /// Port of the bNeedLCPCG detection logic from rigidbody.cpp lines 752-789.
    ///
    /// The algorithm builds per-body buddy lists (neighbors via contacts) with
    /// integral normal directions, then checks every pair of buddies for
    /// conflicting required-velocity directions (dot product &lt; 0 means the
    /// PGS solver alone will oscillate).
    /// </summary>
    /// <returns>True if LCP-CG pass is needed.</returns>
    public bool DetectNeedLCPCG(SolverSettings settings)
    {
        if (settings.NMaxLCPCGiters <= 0)
            return false;

        int nBodies = _nBodies;
        bool bNeedLCPCG = false;

        // Initialize per-body info
        for (int i = 0; i < nBodies; i++)
        {
            _bodyInfos[i] ??= new BodyInfo();
            _bodyInfos[i].PBuddy = null;
            _bodyInfos[i].ILevel = -1;
            _bodyInfos[i].PFollower = null;
            _bodyInfos[i].PSandwich = null;
            _bodyInfos[i].IdUpdate = 0;

            // Check for fast-moving bodies
            if (_bodies[i].V.LengthSq() > settings.MaxMCVel * settings.MaxMCVel)
                bNeedLCPCG = true;
        }

        if (settings.MaxMCMassRatio <= 1.0f)
            bNeedLCPCG = true;

        if (bNeedLCPCG)
            return true;

        // Build buddy lists by iterating contacts grouped by body0
        int nBuddies = 0;
        int istart = 0;
        while (istart < _nContacts)
        {
            int iend = istart + 1;
            int bidx0 = _contactsRB[istart].IBody0;
            while (iend < _nContacts && _contactsRB[iend].IBody0 == bidx0)
                iend++;

            // Sub-group by body1 and accumulate integral normal
            int i = istart;
            while (i < iend)
            {
                int bidx1 = _contactsRB[i].IBody1;
                var n = PhysVector3.Zero;
                int j = i;
                while (j < iend && _contactsRB[j].IBody1 == bidx1)
                {
                    n = n + _contactsRB[j].N;
                    j++;
                }

                float minv0 = _bodies[bidx0].Minv;
                float minv1 = _bodies[bidx1].Minv;

                // If body0 is much lighter than body1, record body1 as buddy of body0
                if (minv0 > minv1 * settings.MaxMCMassRatio && nBuddies < MaxContacts)
                {
                    _buddyBuf[nBuddies] ??= new BuddyInfo();
                    _buddyBuf[nBuddies].Next = _bodyInfos[bidx0].PBuddy;
                    _buddyBuf[nBuddies].IBody = bidx1;
                    _buddyBuf[nBuddies].Vreq = n;
                    _bodyInfos[bidx0].PBuddy = _buddyBuf[nBuddies];
                    nBuddies++;
                }

                // Symmetric: if body1 is much lighter than body0
                if (minv1 > minv0 * settings.MaxMCMassRatio && nBuddies < MaxContacts)
                {
                    _buddyBuf[nBuddies] ??= new BuddyInfo();
                    _buddyBuf[nBuddies].Next = _bodyInfos[bidx1].PBuddy;
                    _buddyBuf[nBuddies].IBody = bidx0;
                    _buddyBuf[nBuddies].Vreq = -n;
                    _bodyInfos[bidx1].PBuddy = _buddyBuf[nBuddies];
                    nBuddies++;
                }

                i = j;
            }

            istart = iend;
        }

        // Check every pair of buddies for conflicting vreq directions
        for (int i = 0; i < nBodies && !bNeedLCPCG; i++)
        {
            for (var pbuddy0 = _bodyInfos[i].PBuddy; pbuddy0 != null; pbuddy0 = pbuddy0.Next)
            {
                for (var pbuddy1 = pbuddy0.Next; pbuddy1 != null; pbuddy1 = pbuddy1.Next)
                {
                    if (pbuddy0.Vreq.Dot(pbuddy1.Vreq) < 0)
                    {
                        bNeedLCPCG = true;
                        break;
                    }
                }
                if (bNeedLCPCG) break;
            }
        }

        return bNeedLCPCG;
    }

    // ========================================================================
    // Unprojection route traversal helpers
    // (port of trace_unproj_route, update_followers from rigidbody.cpp)
    // ========================================================================

    /// <summary>
    /// Recursively update follower levels after a level assignment.
    /// Port of update_followers from rigidbody.cpp (line 327).
    /// </summary>
    private void UpdateFollowers(int iBody, int idUpdate)
    {
        if (_bodyInfos[iBody].IdUpdate == idUpdate)
        {
            _nUnprojLoops++;
            return;
        }
        _bodyInfos[iBody].IdUpdate = idUpdate;
        for (var pfollower = _bodyInfos[iBody].PFollower; pfollower != null; pfollower = pfollower.Next)
        {
            if (_bodyInfos[pfollower.IBody].ILevel <= _bodyInfos[iBody].ILevel)
            {
                _bodyInfos[pfollower.IBody].ILevel = _bodyInfos[iBody].ILevel + 1;
                UpdateFollowers(pfollower.IBody, idUpdate);
            }
        }
    }

    /// <summary>
    /// Add a follower relationship and recursively trace the unprojection route.
    /// Port of add_route_follower from rigidbody.cpp (line 343).
    /// </summary>
    private void AddRouteFollower(int iBody, int iFollower)
    {
        // Check if already a follower
        for (var pf = _bodyInfos[iBody].PFollower; pf != null; pf = pf.Next)
        {
            if (pf.IBody == iFollower) return;
        }

        if (_nFollowers >= MaxContacts) return;

        _followerBuf[_nFollowers] ??= new FollowerThunk();
        _followerBuf[_nFollowers].IBody = iFollower;
        _followerBuf[_nFollowers].Next = _bodyInfos[iBody].PFollower;
        _bodyInfos[iBody].PFollower = _followerBuf[_nFollowers];
        _nFollowers++;

        TraceUnprojRoute(iFollower, iBody);
    }

    /// <summary>
    /// Trace unprojection route: given two bodies (1 inside, 1 outside),
    /// find all possible second-outside bodies and recurse.
    /// Port of trace_unproj_route from rigidbody.cpp (line 363).
    /// </summary>
    private void TraceUnprojRoute(int iMiddle, int iBread)
    {
        for (var psandwich = _bodyInfos[iMiddle].PSandwich; psandwich != null; psandwich = psandwich.Next)
        {
            if (psandwich.IMiddle != iMiddle) continue;

            int iop;
            if (psandwich.IBread0 == iBread)
                iop = 1; // other bread is IBread1
            else if (psandwich.IBread1 == iBread)
                iop = 0; // other bread is IBread0
            else
                continue;

            psandwich.BProcessed = true;
            int otherBread = iop == 0 ? psandwich.IBread0 : psandwich.IBread1;
            int newLevel = _bodyInfos[iMiddle].ILevel + 1;
            if (_bodyInfos[otherBread].ILevel < newLevel)
            {
                _bodyInfos[otherBread].ILevel = newLevel;
                UpdateFollowers(otherBread, ++_idLastUpdate);
            }
            AddRouteFollower(iMiddle, otherBread);
        }
    }

    // ========================================================================
    // MINRES unprojection solver (port of ComputeRc from rigidbody.cpp)
    // ========================================================================

    /// <summary>
    /// Compute the MINRES residual correlation for a single body's contacts.
    /// Port of ComputeRc from rigidbody.cpp (lines 373-399).
    ///
    /// This is used by the LCP-CG deep-penetration solver: for a body that
    /// is being unprojected, it accumulates the search-direction impulses
    /// onto the body, computes the resulting velocity change at each contact,
    /// and returns r^T * A * p (the denominator for the CG step size).
    /// </summary>
    /// <param name="body0">The body being unprojected.</param>
    /// <param name="contactIndices">Indices into _contacts for this body's contacts.</param>
    /// <param name="nAngContacts">Number of leading angular contacts.</param>
    /// <param name="nContacts">Total number of contacts (angular + positional).</param>
    /// <returns>r^T * rc product for the MINRES step.</returns>
    public float ComputeRc(RigidBody body0, int[] contactIndices, int nAngContacts, int nContacts)
    {
        float rT_x_rc = 0f;

        body0.Fcollision = PhysVector3.Zero;
        body0.Tcollision = PhysVector3.Zero;

        // Angular contacts: accumulate torque from search direction
        for (int i = 0; i < nAngContacts; i++)
        {
            int idx = contactIndices[i];
            int ci = _contacts[idx].BProcessed;
            float kinv00 = 1f; // simplified: Kinv(0,0) scalar for 1-DOF
            body0.Tcollision = body0.Tcollision +
                _contactsCG[ci].DP * (_contactsCG[ci].R.X * kinv00);
        }

        // Positional contacts: accumulate force and torque
        for (int i = nAngContacts; i < nContacts; i++)
        {
            int idx = contactIndices[i];
            int ci = _contacts[idx].BProcessed;
            int bodyIdx = (_contacts[idx].Flags >> 13) & 1; // contact_bidx_log2 = 13
            var r0 = (bodyIdx == 0 ? _contacts[idx].Pt0 : _contacts[idx].Pt1) - body0.Pos;
            float kinv00 = 1f;
            var dP = _contactsCG[ci].DP * (_contactsCG[ci].R.X * kinv00);
            body0.Fcollision = body0.Fcollision + dP;
            body0.Tcollision = body0.Tcollision + r0.Cross(dP);
        }

        // Compute velocity changes (A*p) at each contact
        for (int i = 0; i < nAngContacts; i++)
        {
            int idx = contactIndices[i];
            int ci = _contacts[idx].BProcessed;
            _contactsCG[ci].Vrel = body0.Iinv * body0.Tcollision;
        }

        for (int i = nAngContacts; i < nContacts; i++)
        {
            int idx = contactIndices[i];
            int ci = _contacts[idx].BProcessed;
            int bodyIdx = (_contacts[idx].Flags >> 13) & 1;
            var r0 = (bodyIdx == 0 ? _contacts[idx].Pt0 : _contacts[idx].Pt1) - body0.Pos;
            _contactsCG[ci].Vrel = body0.Fcollision * body0.Minv +
                                    (body0.Iinv * body0.Tcollision).Cross(r0);
        }

        // Accumulate r^T * A * p
        for (int i = 0; i < nContacts; i++)
        {
            int idx = contactIndices[i];
            int ci = _contacts[idx].BProcessed;
            float kinv00 = 1f;
            rT_x_rc += _contactsCG[ci].DP.Dot(_contactsCG[ci].Vrel) *
                        _contactsCG[ci].R.X * kinv00;
        }

        return rT_x_rc;
    }

    /// <summary>
    /// Run the LCP-CG (MINRES) unprojection solver after PGS.
    /// Port of the bNeedLCPCG block in InvokeContactSolver (rigidbody.cpp line 934+).
    ///
    /// This handles deep penetration resolution that PGS alone cannot solve.
    /// It iterates over contacts that still have non-zero residual velocity,
    /// grouping them by type (angular constraints, positional constraints,
    /// frictionless, etc.) and running a per-body CG solver to resolve them.
    /// </summary>
    /// <param name="settings">Solver settings.</param>
    /// <param name="e">Accuracy threshold.</param>
    /// <returns>Number of contacts that were solved.</returns>
    public int SolveLCPCG(SolverSettings settings, float e)
    {
        if (_nContacts == 0 || _nContacts >= settings.MaxLCPCGContacts)
            return 0;

        int nSolved = 0;
        int cgiter = settings.NMaxLCPCGiters;
        PhysVector3 dp, r0, r1;

        // Prepare: compute initial residuals for each contact
        for (int i = 0; i < _nContacts; i++)
        {
            var contact = _contacts[i];
            var body0 = contact.PBody0!;
            var body1 = contact.PBody1!;
            contact.BProcessed = i;

            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                r0 = contact.Pt0 - body0.Pos;
                r1 = contact.Pt1 - body1.Pos;
                dp = body0.V + body0.W.Cross(r0) - body1.V - body1.W.Cross(r1);
            }
            else
            {
                dp = body0.W - body1.W;
            }

            if ((contact.Flags & (int)ContactFlags.UseC) != 0)
                dp = _contactsC[i].C * dp;

            // Store residual: only solve contacts that still violate
            bool isConstraint = (contact.Flags & (int)ContactFlags.Constraint) != 0;
            float vn = (dp - contact.Vreq).Dot(contact.N);
            if (isConstraint || vn < 0)
            {
                if ((dp - contact.Vreq).LengthSq() > e * e)
                {
                    contact.Flags |= (int)ContactFlags.SolveFor;
                    nSolved++;
                }
            }
            contact.Vrel = (dp - contact.Vreq).Dot(contact.N);
        }

        if (nSolved == 0)
            return 0;

        // CG iterations over active contacts
        float r2 = 0, r2new;
        for (int i = 0; i < _nContacts; i++)
        {
            if ((_contacts[i].Flags & (int)ContactFlags.SolveFor) == 0) continue;

            var contact = _contacts[i];
            var body0 = contact.PBody0!;
            var body1 = contact.PBody1!;

            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                r0 = contact.Pt0 - body0.Pos;
                r1 = contact.Pt1 - body1.Pos;
                dp = body0.V + body0.W.Cross(r0) - body1.V - body1.W.Cross(r1);
            }
            else
            {
                dp = body0.W - body1.W;
            }

            // Initial residual
            _contactsCG[i].R = contact.Vreq - dp;
            _contactsCG[i].DP = _contactsCG[i].R;
            _contactsCG[i].P = PhysVector3.Zero;
            r2 += _contactsCG[i].R.LengthSq();
        }

        int iter = cgiter;
        while (--iter > 0 && r2 > e * e)
        {
            // Zero body accumulators
            for (int i = 0; i < _nBodies; i++)
            {
                _bodies[i].Fcollision = PhysVector3.Zero;
                _bodies[i].Tcollision = PhysVector3.Zero;
            }

            // Accumulate search direction impulses
            for (int i = 0; i < _nContacts; i++)
            {
                if ((_contacts[i].Flags & (int)ContactFlags.SolveFor) == 0) continue;
                var contact = _contacts[i];

                if ((contact.Flags & (int)ContactFlags.Angular) == 0)
                {
                    r0 = contact.Pt0 - contact.PBody0!.Pos;
                    r1 = contact.Pt1 - contact.PBody1!.Pos;
                    contact.PBody0.Fcollision = contact.PBody0.Fcollision + _contactsCG[i].DP;
                    contact.PBody0.Tcollision = contact.PBody0.Tcollision + r0.Cross(_contactsCG[i].DP);
                    contact.PBody1!.Fcollision = contact.PBody1.Fcollision - _contactsCG[i].DP;
                    contact.PBody1.Tcollision = contact.PBody1.Tcollision - r1.Cross(_contactsCG[i].DP);
                }
                else
                {
                    contact.PBody0!.Tcollision = contact.PBody0.Tcollision + _contactsCG[i].DP;
                    contact.PBody1!.Tcollision = contact.PBody1.Tcollision - _contactsCG[i].DP;
                }
            }

            // Compute A*p
            float pAp = 0;
            for (int i = 0; i < _nContacts; i++)
            {
                if ((_contacts[i].Flags & (int)ContactFlags.SolveFor) == 0) continue;
                var contact = _contacts[i];

                if ((contact.Flags & (int)ContactFlags.Angular) == 0)
                {
                    r0 = contact.Pt0 - contact.PBody0!.Pos;
                    r1 = contact.Pt1 - contact.PBody1!.Pos;
                    dp = contact.PBody0.Fcollision * contact.PBody0.Minv +
                         (contact.PBody0.Iinv * contact.PBody0.Tcollision).Cross(r0);
                    dp = dp - contact.PBody1!.Fcollision * contact.PBody1.Minv -
                         (contact.PBody1.Iinv * contact.PBody1.Tcollision).Cross(r1);
                }
                else
                {
                    dp = contact.PBody0!.Iinv * contact.PBody0.Tcollision -
                         contact.PBody1!.Iinv * contact.PBody1.Tcollision;
                }

                _contactsCG[i].Vrel = dp;
                pAp += _contactsCG[i].Vrel.Dot(_contactsCG[i].DP);
            }

            float a = MathF.Min(20f, r2 / MathF.Max(1e-10f, MathF.Abs(pAp)));

            // Update solution and residual
            r2new = 0;
            for (int i = 0; i < _nContacts; i++)
            {
                if ((_contacts[i].Flags & (int)ContactFlags.SolveFor) == 0) continue;
                _contactsCG[i].R = _contactsCG[i].R - _contactsCG[i].Vrel * a;
                r2new += _contactsCG[i].R.LengthSq();
                _contactsCG[i].P = _contactsCG[i].P + _contactsCG[i].DP * a;
            }

            float b = MathF.Min(1f, r2new / MathF.Max(1e-10f, r2));
            r2 = r2new;

            for (int i = 0; i < _nContacts; i++)
            {
                if ((_contacts[i].Flags & (int)ContactFlags.SolveFor) == 0) continue;
                _contactsCG[i].DP = _contactsCG[i].DP * b + _contactsCG[i].R;
            }
        }

        // Apply accumulated CG impulses
        for (int i = 0; i < _nBodies; i++)
        {
            _bodies[i].Fcollision = PhysVector3.Zero;
            _bodies[i].Tcollision = PhysVector3.Zero;
        }

        for (int i = 0; i < _nContacts; i++)
        {
            if ((_contacts[i].Flags & (int)ContactFlags.SolveFor) == 0) continue;
            var contact = _contacts[i];

            if ((contact.Flags & (int)ContactFlags.Angular) == 0)
            {
                contact.PBody0!.Fcollision = contact.PBody0.Fcollision + _contactsCG[i].P;
                contact.PBody0.Tcollision = contact.PBody0.Tcollision +
                    (contact.Pt0 - contact.PBody0.Pos).Cross(_contactsCG[i].P);
                contact.PBody1!.Fcollision = contact.PBody1.Fcollision - _contactsCG[i].P;
                contact.PBody1.Tcollision = contact.PBody1.Tcollision -
                    (contact.Pt1 - contact.PBody1.Pos).Cross(_contactsCG[i].P);
            }
            else
            {
                contact.PBody0!.Tcollision = contact.PBody0.Tcollision + _contactsCG[i].P;
                contact.PBody1!.Tcollision = contact.PBody1.Tcollision - _contactsCG[i].P;
            }
        }

        // Apply impulses to body state
        for (int i = 0; i < _nBodies; i++)
        {
            if (_bodies[i].M > 0)
            {
                _bodies[i].P = _bodies[i].P + _bodies[i].Fcollision;
                _bodies[i].L = _bodies[i].L + _bodies[i].Tcollision;
                _bodies[i].V = _bodies[i].P * _bodies[i].Minv;
                _bodies[i].W = _bodies[i].Iinv * _bodies[i].L;
            }
        }

        // Clear SolveFor flags
        for (int i = 0; i < _nContacts; i++)
            _contacts[i].Flags &= ~(int)ContactFlags.SolveFor;

        return nSolved;
    }

    /// <summary>
    /// Construct outer product matrix: M = a * b^T (each element M[i,j] = a[i]*b[j]).
    /// Port of dotproduct_matrix from CryEngine.
    /// </summary>
    private static PhysMatrix33 DotProductMatrix(in PhysVector3 a, in PhysVector3 b)
    {
        return new PhysMatrix33(
            a.X * b.X, a.X * b.Y, a.X * b.Z,
            a.Y * b.X, a.Y * b.Y, a.Y * b.Z,
            a.Z * b.X, a.Z * b.Y, a.Z * b.Z
        );
    }
}
