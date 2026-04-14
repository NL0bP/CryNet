// Port of CryPhysics bvtree.h - abstract bounding volume tree
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// Bounding volume types enumeration.
/// </summary>
public enum BVType
{
    Box = 0,
    HeightfieldNode = 1,
    VoxelNode = 2,
    Ray = 3
}

/// <summary>
/// Bounding volume data for a tree node.
/// </summary>
public class BoundingVolume
{
    public BVType Type;
    public Box? BBox;        // For box-type BV nodes
    public int INode;        // Node index in tree
}

/// <summary>
/// Abstract bounding volume tree. Base class for AABB, OBB, etc.
/// Port of CBVTree from CryEngine.
/// </summary>
public abstract class BVTree
{
    /// <summary>The geometry that owns this tree.</summary>
    public IGeometry? Owner { get; set; }

    /// <summary>Get bounding volume for a node.</summary>
    public abstract void GetNodeBV(ref BoundingVolume bv, int iNode);

    /// <summary>Get bounding volume for a node with transform.</summary>
    public abstract void GetNodeBV(ref BoundingVolume bv, int iNode,
        in PhysVector3 offset, float scale, in PhysQuaternion rotation);

    // Legacy 2-arg GetNodeContents lives on the base now (see literal C++ API section below).

    /// <summary>Get total number of nodes.</summary>
    public abstract int NodeCount { get; }

    /// <summary>Get the maximum depth of the tree.</summary>
    public virtual int MaxDepth => 0;

    /// <summary>Mark a node for re-computation.</summary>
    public virtual void MarkAsChanged(int iNode) { }

    /// <summary>Build the tree from geometry data.</summary>
    public abstract void Build(PhysVector3[] vertices, int[] indices, int nTris);

    /// <summary>Get memory usage.</summary>
    public virtual int GetMemoryUsage() => 0;

    /// <summary>Maximum number of primitives held in a single leaf node.
    /// Port of CBVTree::MaxPrimsInNode from bvtree.h. Default is 1.</summary>
    public virtual int MaxPrimsInNode() => 1;

    /// <summary>
    /// Prepare this tree for an intersection test. Port of CBVTree::PrepareForIntersectionTest from bvtree.h.
    /// Default implementation is a no-op; subclasses may override to allocate scratch state.
    /// </summary>
    public virtual void PrepareForIntersectionTest(Geometry.GeometryUnderTest pGTest,
        Geometry.GeometryBase pCollider, Geometry.GeometryUnderTest pGTestColl) { }

    /// <summary>
    /// Get all leaf primitives from this tree. Used for simplified intersection.
    /// Returns a list of (Primitive, index) pairs.
    /// </summary>
    public virtual List<(Primitive prim, int idx)> GetAllPrimitives(IGeometry geom)
    {
        return new List<(Primitive, int)>();
    }

    // ----------------------------------------------------------------------------
    // Literal C++ CBVTree API (bvtree.h:108-150). New methods take/return the BV
    // class hierarchy from BV.cs. Default implementations match the C++ defaults
    // exactly (no-op or return 0/1). Subclasses may override.
    // ----------------------------------------------------------------------------

    /// Port of `virtual int GetType() = 0;` (bvtree.h:111).
    public virtual int GetTypeId() => -1;

    /// Port of `virtual void GetBBox(box *pbox) {}` (bvtree.h:112).
    public virtual void GetBBox(ref Box pbox) { }

    /// Port of `virtual float Build(CGeometry *pGeom) = 0;` (bvtree.h:114).
    /// Default returns 0; subclasses override to drive tree construction.
    public virtual float BuildFromGeom(Geometry.GeometryBase pGeom) { return 0f; }

    /// Port of `virtual void SetGeomConvex() {}`.
    public virtual void SetGeomConvex() { }

    /// Port of `virtual int PrepareForIntersectionTest(geometry_under_test*, CGeometry*, geometry_under_test*)`
    /// (bvtree.h:117-123). Returns 1 (continue) and clears the used-nodes scratch.
    public virtual int PrepareForIntersectionTestBV(Geometry.GeometryUnderTest pGTest,
        Geometry.GeometryBase pCollider, Geometry.GeometryUnderTest pGTestColl)
    {
        // Mirrors the C++ default body that null-clears pUsedNodesMap/pUsedNodesIdx.
        return 1;
    }

    /// Port of `virtual void CleanupAfterIntersectionTest(geometry_under_test*)` (bvtree.h:125).
    public virtual void CleanupAfterIntersectionTest(Geometry.GeometryUnderTest pGTest) { }

    /// Port of `virtual void GetNodeBV(BV*&, int iNode=0, int iCaller=0)` (bvtree.h:126).
    public virtual void GetNodeBVRef(out BV pBV, int iNode = 0, int iCaller = 0) { pBV = new BBox(); }

    /// Port of `virtual void GetNodeBV(BV*&, const Vec3 &sweepdir, float sweepstep, int iNode=0, int iCaller=0)` (bvtree.h:127).
    public virtual void GetNodeBVRef(out BV pBV, in PhysVector3 sweepDir, float sweepStep, int iNode = 0, int iCaller = 0)
    {
        GetNodeBVRef(out pBV, iNode, iCaller);
    }

    /// Port of `virtual void GetNodeBV(const Matrix33 &Rw, const Vec3 &offsw, float scalew, BV*&, int iNode=0, int iCaller=0)` (bvtree.h:128).
    public virtual void GetNodeBVRef(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        out BV pBV, int iNode = 0, int iCaller = 0)
    {
        GetNodeBVRef(out pBV, iNode, iCaller);
    }

    /// Port of `virtual void GetNodeBV(...sweepdir, sweepstep...)` (bvtree.h:129).
    public virtual void GetNodeBVRef(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        out BV pBV, in PhysVector3 sweepDir, float sweepStep, int iNode = 0, int iCaller = 0)
    {
        GetNodeBVRef(Rw, offsw, scalew, out pBV, iNode, iCaller);
    }

    /// Port of `virtual float SplitPriority(const BV *pBV)` (bvtree.h:130).
    public virtual float SplitPriority(BV pBV) => 0f;

    /// Port of `virtual void GetNodeChildrenBVs(...)` overloads (bvtree.h:131-133). Default no-ops.
    public virtual void GetNodeChildrenBVs(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        BV pBVParent, out BV? pBVChild1, out BV? pBVChild2, int iCaller = 0)
    { pBVChild1 = null; pBVChild2 = null; }
    public virtual void GetNodeChildrenBVs(BV pBVParent, out BV? pBVChild1, out BV? pBVChild2, int iCaller = 0)
    { pBVChild1 = null; pBVChild2 = null; }
    public virtual void GetNodeChildrenBVs(BV pBVParent, in PhysVector3 sweepDir, float sweepStep,
        out BV? pBVChild1, out BV? pBVChild2, int iCaller = 0)
    { pBVChild1 = null; pBVChild2 = null; }

    /// Port of `virtual void ReleaseLastBVs(int iCaller=0)` (bvtree.h:134-135).
    public virtual void ReleaseLastBVs(int iCaller = 0) { }
    public virtual void ReleaseLastSweptBVs(int iCaller = 0) { }
    public virtual void ResetCollisionArea() { }
    public virtual float GetMaxSkipDim() => 0f;

    /// Port of `virtual int GetNodeContents(int iNode, BV *pBVCollider, int bColliderUsed,
    /// int bColliderLocal, geometry_under_test *pGTest, geometry_under_test *pGTestOp) = 0;` (bvtree.h:145-146).
    /// Default returns 0 (no primitives); concrete trees override.
    public virtual int GetNodeContents(int iNode, BV pBVCollider, int bColliderUsed, int bColliderLocal,
        Geometry.GeometryUnderTest pGTest, Geometry.GeometryUnderTest pGTestOp)
    { return 0; }

    /// Port of `virtual int GetNodeContentsIdx(int iNode, int &iStartPrim)` (bvtree.h:148).
    public virtual int GetNodeContentsIdx(int iNode, out int iStartPrim) { iStartPrim = 0; return 1; }

    /// Port of `virtual void MarkUsedTriangle(int itri, geometry_under_test *pGTest)` (bvtree.h:149).
    public virtual void MarkUsedTriangle(int itri, Geometry.GeometryUnderTest pGTest) { }

    // Legacy adapter — keeps the simpler 2-arg GetNodeContents signature usable from
    // older code paths. New traversal in CGeometry::Intersect uses the 6-arg overload above.
    public virtual int GetNodeContents(int iNode, ref int[] contents) { contents = System.Array.Empty<int>(); return 0; }
}

/// <summary>
/// Interface for geometry objects that can own a BV tree.
/// </summary>
public interface IGeometry
{
    int GeomType { get; }
    BVTree? Tree { get; }
    void GetBBox(ref Box bbox);
    float GetVolume();
    PhysVector3 GetCenter();
}
