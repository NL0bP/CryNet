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

    /// <summary>Get the number of primitive contents in a node.</summary>
    public abstract int GetNodeContents(int iNode, ref int[] contents);

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
