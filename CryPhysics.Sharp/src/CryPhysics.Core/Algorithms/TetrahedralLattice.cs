// Port of CryPhysics tetrlattice.h/cpp - tetrahedral mesh for deformation/fracture
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Algorithms;

/// <summary>Tension types for structural checking.</summary>
public enum TensionType { Pull, Push, Shift, Twist, Bend }

/// <summary>Vertex flags for lattice processing.</summary>
[Flags]
public enum LatticeVtxFlags
{
    None = 0,
    Removed = 1,
    RemovedNew = 2,
    Processed = 4,
    InextLog2 = 8
}

/// <summary>Tetrahedron flags.</summary>
[Flags]
public enum LatticeTetFlags
{
    None = 0,
    Removed = 1,
    RemovedNew = 2,
    Processed = 4,
    InextLog2 = 8
}

/// <summary>
/// Tetrahedron element. Port of STetrahedron from CryEngine.
/// </summary>
public class Tetrahedron
{
    public int Flags;
    public float M;
    public float Minv;
    public float Vinv;
    public PhysMatrix33 IInv = PhysMatrix33.Identity;
    public PhysVector3 PExt;
    public PhysVector3 LExt;
    public float Area;
    public int[] IVtx = new int[4];
    public int[] IBuddy = new int[4];   // Neighboring tetrahedra per face
    public float[] FracFace = new float[4];
    public int[] IdxFace = new int[4];
    public int Idx;
}

/// <summary>
/// Conjugate gradient solver tetrahedron data. Port of SCGTetr.
/// </summary>
public struct CGTetr
{
    public PhysVector3 DP;
    public PhysVector3 DL;
    public float Minv;
    public PhysMatrix33 IInv;
}

/// <summary>
/// Conjugate gradient solver face data. Port of SCGFace.
/// </summary>
public class CGFace
{
    public int ITet;
    public int IFace;
    public PhysVector3 Rv;
    public PhysVector3 Rw;
    public PhysVector3 Dv;
    public PhysVector3 Dw;
    public PhysVector3 DP;
    public PhysVector3 DL;
    public PhysVector3 P;
    public PhysVector3 L;
    public CGTetr[] Tet = new CGTetr[2];
    public PhysVector3 R0;
    public PhysVector3 R1;
    public PhysMatrix33 VKInv = PhysMatrix33.Identity;
    public PhysMatrix33 WKInv = PhysMatrix33.Identity;
    public int Flags;
}

/// <summary>
/// Tetrahedral lattice for structural deformation and fracture simulation.
/// Port of CTetrLattice from CryEngine.
/// Used for breakable objects to determine when/where structural failure occurs.
/// </summary>
public class TetrahedralLattice
{
    // Mesh reference
    public PhysVector3[] Vertices = Array.Empty<PhysVector3>();
    public int NVtx;
    public Tetrahedron[] Tetrahedra = Array.Empty<Tetrahedron>();
    public int NTetr;
    public int[] VtxFlags = Array.Empty<int>();

    // Material properties
    public int IdMat;
    public float MaxForcePush;
    public float MaxForcePull;
    public float MaxForceShift;
    public float MaxTorqueTwist;
    public float MaxTorqueBend;
    public float CrackWeaken = 0.4f;
    public float Density = 1000f;
    public int NMaxCracks;

    // State
    public int NRemovedTets;
    public int[] VtxRemap = Array.Empty<int>();
    public int Flags;
    public float MaxTension;
    public int MaxTensionType;
    public float LastImpulseTime;

    // Grid acceleration
    public PhysMatrix33 RGrid = PhysMatrix33.Identity;
    public PhysVector3 PosGrid;
    public PhysVector3 StepGrid;
    public PhysVector3 RStepGrid;
    public (int X, int Y, int Z) SzGrid;
    public (int X, int Y, int Z) StrideGrid;
    public int[] GridTet0 = Array.Empty<int>();
    public int[] Grid = Array.Empty<int>();

    /// <summary>Create lattice from vertices and tetrahedra indices.</summary>
    public void CreateLattice(PhysVector3[] vtx, int nVtx, int[] tets, int nTets)
    {
        Vertices = new PhysVector3[nVtx];
        Array.Copy(vtx, Vertices, nVtx);
        NVtx = nVtx;
        VtxFlags = new int[nVtx];

        NTetr = nTets;
        Tetrahedra = new Tetrahedron[nTets];
        for (int i = 0; i < nTets; i++)
        {
            Tetrahedra[i] = new Tetrahedron
            {
                IVtx = { [0] = tets[i * 4], [1] = tets[i * 4 + 1], [2] = tets[i * 4 + 2], [3] = tets[i * 4 + 3] },
                Idx = i,
                IBuddy = { [0] = -1, [1] = -1, [2] = -1, [3] = -1 }
            };

            // Compute volume and mass
            var v0 = Vertices[Tetrahedra[i].IVtx[0]];
            var e1 = Vertices[Tetrahedra[i].IVtx[1]] - v0;
            var e2 = Vertices[Tetrahedra[i].IVtx[2]] - v0;
            var e3 = Vertices[Tetrahedra[i].IVtx[3]] - v0;
            float vol = MathF.Abs(e1.Dot(e2 ^ e3)) / 6f;
            Tetrahedra[i].M = vol * Density;
            Tetrahedra[i].Minv = Tetrahedra[i].M > 0 ? 1f / Tetrahedra[i].M : 0;
            Tetrahedra[i].Vinv = vol > 0 ? 1f / vol : 0;
        }

        // Build neighbor connectivity
        BuildConnectivity();
    }

    /// <summary>
    /// Check structural integrity under forces.
    /// Returns number of broken elements.
    /// Port of CTetrLattice::CheckStructure.
    /// </summary>
    public int CheckStructure(float timeInterval, in PhysVector3 gravity, int maxIters = 100000)
    {
        int nBroken = 0;
        MaxTension = 0;

        for (int i = 0; i < NTetr; i++)
        {
            var tet = Tetrahedra[i];
            if ((tet.Flags & (int)LatticeTetFlags.Removed) != 0) continue;

            // Compute internal forces from gravity load
            var center = GetTetrCenter(i);
            var gForce = gravity * tet.M;

            // Check each face for tension/compression
            for (int f = 0; f < 4; f++)
            {
                if (tet.IBuddy[f] < 0) continue;

                float tension = MathF.Abs(gForce.Dot(GetFaceNormal(i, f)));

                if (tension > MaxTension)
                {
                    MaxTension = tension;
                    MaxTensionType = (int)TensionType.Pull;
                }

                // Check break conditions
                if (tension > MaxForcePull || tension > MaxForcePush)
                {
                    tet.Flags |= (int)LatticeTetFlags.Removed;
                    nBroken++;
                    NRemovedTets++;
                    break;
                }
            }
        }

        return nBroken;
    }

    /// <summary>Apply impulse at a point.</summary>
    public int AddImpulse(in PhysVector3 pt, in PhysVector3 impulse, in PhysVector3 momentum, in PhysVector3 gravity, float worldTime)
    {
        LastImpulseTime = worldTime;

        // Find nearest tetrahedron
        int closest = -1;
        float minDist = float.MaxValue;
        for (int i = 0; i < NTetr; i++)
        {
            if ((Tetrahedra[i].Flags & (int)LatticeTetFlags.Removed) != 0) continue;
            float d = (GetTetrCenter(i) - pt).LengthSq();
            if (d < minDist) { minDist = d; closest = i; }
        }

        if (closest >= 0)
        {
            Tetrahedra[closest].PExt = Tetrahedra[closest].PExt + impulse;
            Tetrahedra[closest].LExt = Tetrahedra[closest].LExt + momentum;
            return 1;
        }
        return 0;
    }

    /// <summary>Defragment removed tetrahedra.</summary>
    public int Defragment()
    {
        int nRemoved = 0;
        int dst = 0;
        for (int i = 0; i < NTetr; i++)
        {
            if ((Tetrahedra[i].Flags & (int)LatticeTetFlags.Removed) != 0)
            {
                nRemoved++;
                continue;
            }
            if (dst != i)
                Tetrahedra[dst] = Tetrahedra[i];
            dst++;
        }
        NTetr = dst;
        NRemovedTets = 0;
        return nRemoved;
    }

    public PhysVector3 GetTetrCenter(int i)
    {
        var tet = Tetrahedra[i];
        return (Vertices[tet.IVtx[0]] + Vertices[tet.IVtx[1]] +
                Vertices[tet.IVtx[2]] + Vertices[tet.IVtx[3]]) * 0.25f;
    }

    private PhysVector3 GetFaceNormal(int iTet, int iFace)
    {
        var tet = Tetrahedra[iTet];
        int[] faceVtx = iFace switch
        {
            0 => new[] { 1, 2, 3 },
            1 => new[] { 0, 3, 2 },
            2 => new[] { 0, 1, 3 },
            3 => new[] { 0, 2, 1 },
            _ => new[] { 0, 1, 2 }
        };
        var v0 = Vertices[tet.IVtx[faceVtx[0]]];
        var v1 = Vertices[tet.IVtx[faceVtx[1]]];
        var v2 = Vertices[tet.IVtx[faceVtx[2]]];
        return ((v1 - v0) ^ (v2 - v0)).Normalized();
    }

    private void BuildConnectivity()
    {
        // Build face-to-face neighbor mapping
        var faceMap = new Dictionary<(int, int, int), (int TetIdx, int FaceIdx)>();
        for (int i = 0; i < NTetr; i++)
        {
            var tet = Tetrahedra[i];
            int[][] faces = {
                new[] { tet.IVtx[1], tet.IVtx[2], tet.IVtx[3] },
                new[] { tet.IVtx[0], tet.IVtx[2], tet.IVtx[3] },
                new[] { tet.IVtx[0], tet.IVtx[1], tet.IVtx[3] },
                new[] { tet.IVtx[0], tet.IVtx[1], tet.IVtx[2] }
            };

            for (int f = 0; f < 4; f++)
            {
                var sorted = faces[f].OrderBy(x => x).ToArray();
                var key = (sorted[0], sorted[1], sorted[2]);
                if (faceMap.TryGetValue(key, out var neighbor))
                {
                    tet.IBuddy[f] = neighbor.TetIdx;
                    Tetrahedra[neighbor.TetIdx].IBuddy[neighbor.FaceIdx] = i;
                }
                else
                {
                    faceMap[key] = (i, f);
                }
            }
        }
    }
}
