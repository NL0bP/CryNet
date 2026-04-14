// Port of CryPhysics aabbtree.h/cpp - Axis-Aligned Bounding Box tree
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// AABB tree node. Port of AABBnode from CryEngine.
/// </summary>
public struct AABBNode
{
    public PhysVector3 Min;
    public PhysVector3 Max;
    public int ChildLeft;   // -1 = leaf
    public int ChildRight;  // -1 = leaf
    public int StartTri;    // First triangle index (for leaves)
    public int NumTris;     // Number of triangles (for leaves)

    public bool IsLeaf => ChildLeft < 0;
}

/// <summary>
/// Axis-Aligned Bounding Box tree for spatial acceleration of triangle meshes.
/// Port of CAABBTree from CryEngine.
/// </summary>
public class AABBTree : BVTree
{
    private AABBNode[] _nodes = Array.Empty<AABBNode>();
    private int _nodeCount;
    private int _maxDepth;

    // Triangle indices sorted by the tree
    private int[] _triIndices = Array.Empty<int>();

    public override int NodeCount => _nodeCount;
    public override int MaxDepth => _maxDepth;

    public override void GetNodeBV(ref BoundingVolume bv, int iNode)
    {
        if (iNode < 0 || iNode >= _nodeCount) return;
        ref var node = ref _nodes[iNode];
        bv.Type = BVType.Box;
        bv.INode = iNode;
        bv.BBox ??= new Box();
        bv.BBox.Center = (node.Min + node.Max) * 0.5f;
        bv.BBox.Size = (node.Max - node.Min) * 0.5f;
        bv.BBox.Basis = PhysMatrix33.Identity;
        bv.BBox.IsOriented = false;
    }

    public override void GetNodeBV(ref BoundingVolume bv, int iNode,
        in PhysVector3 offset, float scale, in PhysQuaternion rotation)
    {
        if (iNode < 0 || iNode >= _nodeCount) return;
        ref var node = ref _nodes[iNode];
        var center = (node.Min + node.Max) * 0.5f;
        var size = (node.Max - node.Min) * 0.5f;

        bv.Type = BVType.Box;
        bv.INode = iNode;
        bv.BBox ??= new Box();
        bv.BBox.Center = rotation.Rotate(center * scale) + offset;
        bv.BBox.Size = size * scale;
        bv.BBox.Basis = new PhysMatrix33(rotation.Conjugate());
        bv.BBox.IsOriented = true;
    }

    public override int GetNodeContents(int iNode, ref int[] contents)
    {
        if (iNode < 0 || iNode >= _nodeCount || !_nodes[iNode].IsLeaf)
            return 0;
        ref var node = ref _nodes[iNode];
        if (contents.Length < node.NumTris)
            contents = new int[node.NumTris];
        Array.Copy(_triIndices, node.StartTri, contents, 0, node.NumTris);
        return node.NumTris;
    }

    /// <summary>
    /// Build the AABB tree from triangle mesh data.
    /// Uses top-down recursive splitting along the longest axis.
    /// </summary>
    public override void Build(PhysVector3[] vertices, int[] indices, int nTris)
    {
        if (nTris == 0)
        {
            _nodes = Array.Empty<AABBNode>();
            _triIndices = Array.Empty<int>();
            _nodeCount = 0;
            return;
        }

        // Initialize triangle indices
        _triIndices = new int[nTris];
        for (int i = 0; i < nTris; i++) _triIndices[i] = i;

        // Compute triangle centroids
        var centroids = new PhysVector3[nTris];
        for (int i = 0; i < nTris; i++)
        {
            int i0 = indices[i * 3], i1 = indices[i * 3 + 1], i2 = indices[i * 3 + 2];
            centroids[i] = (vertices[i0] + vertices[i1] + vertices[i2]) * (1f / 3f);
        }

        // Pre-allocate nodes (max 2*nTris-1)
        _nodes = new AABBNode[2 * nTris];
        _nodeCount = 0;
        _maxDepth = 0;

        BuildRecursive(vertices, indices, centroids, 0, nTris, 0);

        // Trim node array
        Array.Resize(ref _nodes, _nodeCount);
    }

    private int BuildRecursive(PhysVector3[] verts, int[] indices, PhysVector3[] centroids, int start, int count, int depth)
    {
        int nodeIdx = _nodeCount++;
        _maxDepth = System.Math.Max(_maxDepth, depth);

        // Compute AABB for this set of triangles
        var min = new PhysVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new PhysVector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = start; i < start + count; i++)
        {
            int ti = _triIndices[i];
            for (int v = 0; v < 3; v++)
            {
                var vert = verts[indices[ti * 3 + v]];
                if (vert.X < min.X) min.X = vert.X;
                if (vert.Y < min.Y) min.Y = vert.Y;
                if (vert.Z < min.Z) min.Z = vert.Z;
                if (vert.X > max.X) max.X = vert.X;
                if (vert.Y > max.Y) max.Y = vert.Y;
                if (vert.Z > max.Z) max.Z = vert.Z;
            }
        }

        _nodes[nodeIdx].Min = min;
        _nodes[nodeIdx].Max = max;

        if (count <= 2) // Leaf
        {
            _nodes[nodeIdx].ChildLeft = -1;
            _nodes[nodeIdx].ChildRight = -1;
            _nodes[nodeIdx].StartTri = start;
            _nodes[nodeIdx].NumTris = count;
            return nodeIdx;
        }

        // Split along longest axis
        var extent = max - min;
        int axis = 0;
        if (extent.Y > extent.X) axis = 1;
        if (extent.Z > extent[axis]) axis = 2;

        float splitVal = (min[axis] + max[axis]) * 0.5f;

        // Partition triangles
        int mid = start;
        for (int i = start; i < start + count; i++)
        {
            if (centroids[_triIndices[i]][axis] < splitVal)
            {
                (_triIndices[i], _triIndices[mid]) = (_triIndices[mid], _triIndices[i]);
                mid++;
            }
        }

        // Prevent degenerate splits
        if (mid == start || mid == start + count)
            mid = start + count / 2;

        _nodes[nodeIdx].ChildLeft = BuildRecursive(verts, indices, centroids, start, mid - start, depth + 1);
        _nodes[nodeIdx].ChildRight = BuildRecursive(verts, indices, centroids, mid, start + count - mid, depth + 1);
        _nodes[nodeIdx].StartTri = -1;
        _nodes[nodeIdx].NumTris = 0;

        return nodeIdx;
    }

    public override int GetMemoryUsage()
    {
        return _nodes.Length * 40 + _triIndices.Length * 4; // Approximate
    }

    // ----------------------------------------------------------------------------
    // Literal C++ CBVTree API overrides (aabbtree.cpp).
    // ----------------------------------------------------------------------------

    public override int GetTypeId() => BVTreeTypes.AABB;

    /// Port of CAABBTree::GetNodeBV(BV*&, int iNode, int iCaller) — fills a BBox
    /// in tree-local space.
    public override void GetNodeBVRef(out BV pBV, int iNode = 0, int iCaller = 0)
    {
        var bb = new BBox { Type = BVTreeTypes.AABB, INode = iNode };
        if (iNode >= 0 && iNode < _nodeCount)
        {
            ref var node = ref _nodes[iNode];
            bb.ABox.Center = (node.Min + node.Max) * 0.5f;
            bb.ABox.Size = (node.Max - node.Min) * 0.5f;
            bb.ABox.Basis = PhysMatrix33.Identity;
            bb.ABox.IsOriented = false;
        }
        pBV = bb;
    }

    /// Port of CAABBTree::GetNodeBV(const Matrix33 &Rw, const Vec3 &offsw, float scalew, BV*&, ...).
    /// Transforms the tree-local AABB into world space producing an OBB.
    public override void GetNodeBVRef(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        out BV pBV, int iNode = 0, int iCaller = 0)
    {
        var bb = new BBox { Type = BVTreeTypes.AABB, INode = iNode };
        if (iNode >= 0 && iNode < _nodeCount)
        {
            ref var node = ref _nodes[iNode];
            var center = (node.Min + node.Max) * 0.5f;
            var size = (node.Max - node.Min) * 0.5f;
            bb.ABox.Center = Rw * (center * scalew) + offsw;
            bb.ABox.Size = size * scalew;
            bb.ABox.Basis = Rw.Transposed();
            bb.ABox.IsOriented = true;
        }
        pBV = bb;
    }

    /// Port of CAABBTree::GetNodeChildrenBVs(...). Returns the AABBs of the two
    /// children (or null for leaves).
    public override void GetNodeChildrenBVs(BV pBVParent, out BV? pBVChild1, out BV? pBVChild2, int iCaller = 0)
    {
        pBVChild1 = pBVChild2 = null;
        if (pBVParent.INode < 0 || pBVParent.INode >= _nodeCount) return;
        ref var node = ref _nodes[pBVParent.INode];
        if (node.IsLeaf) return;
        GetNodeBVRef(out var c1, node.ChildLeft, iCaller); pBVChild1 = c1;
        GetNodeBVRef(out var c2, node.ChildRight, iCaller); pBVChild2 = c2;
    }

    public override void GetNodeChildrenBVs(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        BV pBVParent, out BV? pBVChild1, out BV? pBVChild2, int iCaller = 0)
    {
        pBVChild1 = pBVChild2 = null;
        if (pBVParent.INode < 0 || pBVParent.INode >= _nodeCount) return;
        ref var node = ref _nodes[pBVParent.INode];
        if (node.IsLeaf) return;
        GetNodeBVRef(Rw, offsw, scalew, out var c1, node.ChildLeft, iCaller); pBVChild1 = c1;
        GetNodeBVRef(Rw, offsw, scalew, out var c2, node.ChildRight, iCaller); pBVChild2 = c2;
    }

    /// Port of CAABBTree::GetNodeContents(int iNode, BV *pBVCollider, int bColliderUsed,
    /// int bColliderLocal, geometry_under_test *pGTest, geometry_under_test *pGTestOp).
    /// For an AABB tree the collider's BV is irrelevant — we just dump leaf triangles
    /// into <see cref="Geometry.GeometryUnderTest.PrimBuf"/>. Returns the count.
    public override int GetNodeContents(int iNode, BV pBVCollider, int bColliderUsed, int bColliderLocal,
        Geometry.GeometryUnderTest pGTest, Geometry.GeometryUnderTest pGTestOp)
    {
        if (iNode < 0 || iNode >= _nodeCount) return 0;
        ref var node = ref _nodes[iNode];
        if (!node.IsLeaf) return 0;

        // Allocate IndexedTriangle scratch on demand. The caller iterates the buffer
        // via its `SzPrim` field. We only emit indices here — the geometry resolves them.
        if (pGTest.PrimBuf == null || pGTest.PrimBuf.Length < node.NumTris)
            pGTest.PrimBuf = new IndexedTriangle[System.Math.Max(node.NumTris, 16)];
        for (int i = 0; i < node.NumTris; i++)
        {
            var tri = new IndexedTriangle { Index = _triIndices[node.StartTri + i] };
            pGTest.PrimBuf[i] = tri;
        }
        pGTest.SzPrim = node.NumTris;
        return node.NumTris;
    }

    public override int GetNodeContentsIdx(int iNode, out int iStartPrim)
    {
        if (iNode < 0 || iNode >= _nodeCount) { iStartPrim = 0; return 0; }
        iStartPrim = _nodes[iNode].StartTri;
        return _nodes[iNode].NumTris;
    }
}
