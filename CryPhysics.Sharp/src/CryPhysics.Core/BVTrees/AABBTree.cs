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
}
