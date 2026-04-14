// Port of CryPhysics geometry.h - base geometry class
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;
using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;
using CryPhysics.Collision;

namespace CryPhysics.Geometry;

/// <summary>
/// Geometry type constants matching CryEngine's geomtypes enum.
/// </summary>
public static class GeomTypes
{
    public const int TriMesh = 0;
    public const int Heightfield = 1;
    public const int Cylinder = 2;
    public const int Capsule = 3;
    public const int Ray = 4;
    public const int Sphere = 5;
    public const int Box = 6;
    public const int VoxelGrid = 7;
}

/// <summary>
/// Physical properties computed from geometry (mass, center of mass, inertia).
/// </summary>
public struct PhysicalProperties
{
    public float Volume;
    public PhysVector3 CenterOfMass;
    public PhysMatrix33 InertiaTensor;
}

/// <summary>
/// Base class for all collision geometries.
/// Port of CGeometry from CryEngine.
/// </summary>
public abstract class GeometryBase : IGeometry
{
    private int _refCount = 1;
    private readonly ReaderWriterLockSlim _lock = new();

    public abstract int GeomType { get; }
    public BVTree? Tree { get; protected set; }

    // Foreign data (user association)
    public object? ForeignData { get; set; }
    public int ForeignDataType { get; set; }

    // Surface properties
    public int SurfaceIdx { get; set; }
    public int[] MaterialMapping { get; set; } = Array.Empty<int>();

    /// <summary>Reference counting.</summary>
    public int AddRef() => Interlocked.Increment(ref _refCount);
    public int Release()
    {
        int rc = Interlocked.Decrement(ref _refCount);
        if (rc <= 0) Dispose();
        return rc;
    }

    /// <summary>Thread-safe read lock.</summary>
    public void LockRead() => _lock.EnterReadLock();
    public void UnlockRead() => _lock.ExitReadLock();

    /// <summary>Thread-safe write lock.</summary>
    public void LockWrite() => _lock.EnterWriteLock();
    public void UnlockWrite() => _lock.ExitWriteLock();

    /// <summary>Get axis-aligned bounding box.</summary>
    public abstract void GetBBox(ref Box bbox);

    /// <summary>Get volume.</summary>
    public abstract float GetVolume();

    /// <summary>Get center of mass.</summary>
    public abstract PhysVector3 GetCenter();

    /// <summary>Compute mass properties (volume, center of mass, inertia tensor).</summary>
    public abstract PhysicalProperties CalcPhysicalProperties();

    /// <summary>Find closest point on geometry to given point.</summary>
    public abstract float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal);

    /// <summary>Test if a point is inside the geometry.</summary>
    public abstract int PointInsideStatus(in PhysVector3 pt);

    /// <summary>Intersect with another geometry (simple interface).</summary>
    public virtual int Intersect(GeometryBase other, ref GeomContact[] contacts)
    {
        return 0; // Default: no intersection
    }

    /// <summary>
    /// Prepare this geometry for a collider-vs-this intersection test.
    /// Port of CGeometry::PrepareForIntersectionTest (virtual).
    /// Base implementation is a no-op — overridden by CTriMesh / CHeightfield.
    /// </summary>
    public virtual void PrepareForIntersectionTest(GeometryUnderTest pGTest, GeometryBase collider,
        GeometryUnderTest pGTestColl, bool bKeepPrevContacts)
    {
    }

    /// <summary>
    /// Full intersection test with world transforms and BVTree traversal.
    /// Port of CGeometry::Intersect from geometry.cpp.
    /// Returns number of contacts found.
    ///
    /// Thread safety: acquires read locks on both geometries during the test.
    /// Port of the lockIntersect pattern from geometry.h where intersection tests
    /// take read locks so multiple threads can test the same geometry concurrently.
    /// </summary>
    public int Intersect(GeometryBase collider, GeomWorldData? data1, GeomWorldData? data2,
                         IntersectionParams? iparams, out GeomContact[] contactsOut)
    {
        // Acquire read locks on both geometries - matches C++ lockIntersect pattern.
        // Lock in consistent order (by hash code) to avoid deadlocks.
        GeometryBase first, second;
        if (RuntimeHelpers.GetHashCode(this) <= RuntimeHelpers.GetHashCode(collider))
        { first = this; second = collider; }
        else
        { first = collider; second = this; }

        first.LockRead();
        try
        {
            if (!ReferenceEquals(first, second))
                second.LockRead();
            try
            {
                return IntersectInternal(collider, data1, data2, iparams, out contactsOut);
            }
            finally
            {
                if (!ReferenceEquals(first, second))
                    second.UnlockRead();
            }
        }
        finally
        {
            first.UnlockRead();
        }
    }

    private int IntersectInternal(GeometryBase collider, GeomWorldData? data1, GeomWorldData? data2,
                                  IntersectionParams? iparams, out GeomContact[] contactsOut)
    {
        var gtest = new GeometryUnderTest[2];
        gtest[0] = new GeometryUnderTest { Geometry = this, BVtree = this.Tree };
        gtest[1] = new GeometryUnderTest { Geometry = collider, BVtree = collider.Tree };

        var datas = new[] { data1 ?? new GeomWorldData(), data2 ?? new GeomWorldData() };
        iparams ??= new IntersectionParams();

        int nContacts = 0;
        var contacts = new GeomContact[64];

        // Setup world transforms
        for (int i = 0; i < 2; i++)
        {
            gtest[i].Offset = datas[i].Offset;
            gtest[i].R = datas[i].R;
            gtest[i].Scale = datas[i].Scale;
            gtest[i].Rscale = datas[i].Scale > 1e-20f ? 1f / datas[i].Scale : 1f;
            gtest[i].V = datas[i].V;
            gtest[i].W = datas[i].W;
            gtest[i].CenterOfMass = datas[i].CenterOfMass;
            gtest[i].CenterOfRotation = iparams.CenterOfRotation;
            gtest[i].Contacts = contacts;
            gtest[i].NContacts = 0;
            gtest[i].NMaxContacts = 64;
            gtest[i].BStopIntersection = false;
            gtest[i].Params = iparams;
        }

        // Compute relative transforms
        for (int i = 0; i < 2; i++)
        {
            int j = i ^ 1;
            gtest[i].OffsetRel = gtest[j].R.Transposed() * (gtest[i].Offset - gtest[j].Offset) * gtest[j].Rscale;
            gtest[i].RRel = gtest[j].R.Transposed() * gtest[i].R;
            gtest[i].ScaleRel = gtest[i].Scale * gtest[j].Rscale;
            gtest[i].RscaleRel = gtest[j].Scale * gtest[i].Rscale;
        }

        // BVTree-driven intersection
        if (gtest[0].BVtree != null && gtest[1].BVtree != null)
        {
            nContacts = IntersectBVTrees(gtest, contacts);
        }
        else
        {
            // Fallback: direct primitive test
            nContacts = IntersectDirect(gtest, contacts);
        }

        contactsOut = contacts;
        return nContacts;
    }

    /// <summary>
    /// BVTree-driven intersection between two geometries. Literal port of
    /// IntersectBVs from geometry.cpp:54-137 — recursive BV-vs-BV traversal with
    /// largest-first split priority and leaf-vs-leaf primitive pair checks.
    /// </summary>
    private static int IntersectBVTrees(GeometryUnderTest[] gtest, GeomContact[] contacts)
    {
        var tree0 = gtest[0].BVtree;
        var tree1 = gtest[1].BVtree;
        if (tree0 == null || tree1 == null) return IntersectDirect(gtest, contacts);

        // Seed with the root BVs of each tree (untransformed for tree0, world-transformed for tree1).
        tree0.GetNodeBVRef(out var pBV1, 0, gtest[0].ICaller);
        tree1.GetNodeBVRef(gtest[1].RRel, gtest[1].OffsetRel, gtest[1].ScaleRel, out var pBV2, 0, gtest[1].ICaller);

        return IntersectBVs(gtest, pBV1, pBV2);
    }

    /// Literal port of IntersectBVs (geometry.cpp:54-137). Recurses by splitting the
    /// higher-priority BV until both sides are leaves, then iterates the primitive
    /// buffers and dispatches to <see cref="RegisterIntersection"/>.
    private static int IntersectBVs(GeometryUnderTest[] pGTest, BV pBV1, BV pBV2)
    {
        if (!BVOverlap.Check(pBV1.Type, pBV2.Type, pBV1, pBV2))
            return 0;

        float split1 = pGTest[0].BVtree!.SplitPriority(pBV1);
        float split2 = pGTest[1].BVtree!.SplitPriority(pBV2);

        if (split1 > split2 && split1 > 0)
        {
            // Split first BV. C++ uses tree-local BVs for tree0, world-relative for tree1.
            pGTest[0].BVtree.GetNodeChildrenBVs(pBV1, out var pc1, out var pc2, pGTest[0].ICaller);
            int res = 0;
            if (pc1 != null) res += IntersectBVs(pGTest, pc1, pBV2);
            if (pGTest[0].BStopIntersection || pGTest[1].BStopIntersection) return res;
            if (pc2 != null) res += IntersectBVs(pGTest, pc2, pBV2);
            pGTest[0].BVtree.ReleaseLastBVs(pGTest[0].ICaller);
            return res;
        }
        if (split2 > 0)
        {
            // Split second BV in world-relative space.
            pGTest[1].BVtree.GetNodeChildrenBVs(pGTest[1].RRel, pGTest[1].OffsetRel, pGTest[1].ScaleRel,
                pBV2, out var pc1, out var pc2, pGTest[0].ICaller);
            int res = 0;
            if (pc1 != null) res += IntersectBVs(pGTest, pBV1, pc1);
            if (pGTest[0].BStopIntersection || pGTest[1].BStopIntersection) return res;
            if (pc2 != null) res += IntersectBVs(pGTest, pBV1, pc2);
            pGTest[1].BVtree.ReleaseLastBVs(pGTest[0].ICaller);
            return res;
        }

        // Both BVs are leaves — fetch primitive buffers and pair-test.
        var inters = new PrimInters
        {
            BorderPts = new PhysVector3[16],
            NBorderPt = 0,
            NBestPtVal = 0,
            NBorderSz = 16
        };

        int nPrims1 = pGTest[0].BVtree.GetNodeContents(pBV1.INode, pBV2, 0, 1, pGTest[0], pGTest[1]);
        int nPrims2 = pGTest[1].BVtree.GetNodeContents(pBV2.INode, pBV1, 0, 0, pGTest[1], pGTest[0]);

        // Fall back to single primitive when buffer is empty (single-prim trees).
        var prim1Default = pGTest[0].Geometry?.GetPrimitive();
        var prim2Default = pGTest[1].Geometry?.GetPrimitive();
        int n1 = System.Math.Max(1, nPrims1);
        int n2 = System.Math.Max(1, nPrims2);

        var checker = IntersectionChecker.Instance;
        int resLeaf = 0;
        for (int i = 0; i < n1; i++)
        {
            var prim1 = (pGTest[0].PrimBuf != null && i < pGTest[0].SzPrim)
                ? (Primitive)pGTest[0].PrimBuf[i] : prim1Default;
            if (prim1 == null) continue;
            for (int j = 0; j < n2; j++)
            {
                var prim2 = (pGTest[1].PrimBuf != null && j < pGTest[1].SzPrim)
                    ? (Primitive)pGTest[1].PrimBuf[j] : prim2Default;
                if (prim2 == null) continue;
                if (checker.Check(prim1.TypeId, prim2.TypeId, prim1, prim2, inters) <= 0) continue;

                inters.INode0 = pBV1.INode;
                inters.INode1 = pBV2.INode;
                resLeaf += pGTest[0].Geometry!.RegisterIntersection(prim1, prim2, pGTest[0], pGTest[1], inters);
                if (pGTest[0].BStopIntersection || pGTest[1].BStopIntersection) return resLeaf;
                inters.NBorderPt = inters.NBestPtVal = 0;
            }
        }
        return resLeaf;
    }

    /// <summary>
    /// Direct intersection without BVTrees (for simple geometries).
    /// </summary>
    private static int IntersectDirect(GeometryUnderTest[] gtest, GeomContact[] contacts)
    {
        var g0 = gtest[0].Geometry!;
        var g1 = gtest[1].Geometry!;

        var prim0 = g0.GetPrimitive();
        var prim1 = g1.GetPrimitive();
        if (prim0 == null || prim1 == null) return 0;

        var pinters = new PrimInters();
        int result = IntersectionChecker.Instance.Check(prim0.TypeId, prim1.TypeId, prim0, prim1, pinters);
        if (result > 0)
        {
            contacts[0] = new GeomContact
            {
                Pt = pinters.Pt0,
                N = pinters.Normal,
            };
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// Calculate submerged volume (buoyancy) for a water plane.
    /// Port of CGeometry::CalculateBuoyancy from geometry.h.
    /// Returns the submerged volume. massCenter is the center of buoyancy in world space.
    /// </summary>
    public virtual float CalculateBuoyancy(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale, out PhysVector3 massCenter)
    {
        massCenter = PhysVector3.Zero;
        return 0;
    }

    /// <summary>
    /// Calculate medium (water/air) resistance force and torque.
    /// Port of CGeometry::CalculateMediumResistance from geometry.h.
    /// </summary>
    public virtual void CalculateMediumResistance(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale,
        in PhysVector3 v, in PhysVector3 w, in PhysVector3 com,
        out PhysVector3 dPres, out PhysVector3 dLres)
    {
        dPres = PhysVector3.Zero;
        dLres = PhysVector3.Zero;
    }

    /// <summary>Get the underlying primitive for direct tests. Override in subclasses.</summary>
    public virtual Primitive? GetPrimitive() => null;

    /// <summary>
    /// Unproject a sphere out of this geometry. Returns 1 if a contact was generated.
    /// Port of CGeometry::UnprojectSphere virtual from geometry.h.
    /// Default implementation: no contact.
    /// </summary>
    public virtual int UnprojectSphere(PhysVector3 center, float r, float rsep, ref Primitives.Contact pcontact)
    {
        return 0;
    }

    /// <summary>
    /// Minimum distance between two vertices for them to be considered distinct.
    /// Port of CGeometry::m_minVtxDist (geometry.h). Subclasses set this during build.
    /// </summary>
    public float MinVtxDist { get; set; } = 1e-4f;

    /// <summary>
    /// Append a primitive-pair intersection to the contact buffer. Default writes a
    /// minimal GeomContact. Triangle-mesh subclasses override to merge contacts and
    /// build areas. Port of CGeometry::RegisterIntersection (geometry.h virtual).
    /// </summary>
    public virtual int RegisterIntersection(Primitive prim1, Primitive prim2,
        GeometryUnderTest pGTest, GeometryUnderTest pGTestOp, Primitives.PrimInters inters)
    {
        if (pGTest.Contacts == null || pGTest.NContacts >= pGTest.NMaxContacts) return 0;
        var c = pGTest.Contacts[pGTest.NContacts];
        if (c == null) c = new GeomContact();
        c.Pt = inters.Pt0;
        c.N = inters.Normal;
        c.IPrim[0] = inters.INode0;
        c.IPrim[1] = inters.INode1;
        pGTest.Contacts[pGTest.NContacts++] = c;
        return 1;
    }

    /// <summary>Collision priority for contact normal direction.</summary>
    public int CollisionPriority { get; set; }

    protected virtual void Dispose()
    {
        _lock.Dispose();
    }

    public virtual int GetMemoryUsage() => 0;
}

/// <summary>
/// Registered geometry with inertia/surface info.
/// Port of phys_geometry from CryEngine.
/// </summary>
public class PhysGeometry
{
    private int _refCount = 1;

    public GeometryBase Geometry { get; set; } = null!;
    public PhysVector3 Origin;
    public PhysQuaternion Rotation = PhysQuaternion.Identity;
    public float Scale = 1f;
    public float Volume;
    public PhysMatrix33 InertiaTensor = PhysMatrix33.Identity;
    public int SurfaceIdx;
    public int[] MaterialMapping = Array.Empty<int>();
    public int Id;

    public int AddRef() => Interlocked.Increment(ref _refCount);
    public int Release() => Interlocked.Decrement(ref _refCount);
}
