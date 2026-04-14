// Port of CryPhysics obbtree.h/cpp - Oriented Bounding Box tree
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// OBB tree node. Port of OBBnode from CryEngine.
/// Each node stores 3 oriented axes, a center, half-extents, and child/tri info.
/// </summary>
public struct OBBNode
{
    public PhysVector3 Axis0, Axis1, Axis2;
    public PhysVector3 Center;
    public PhysVector3 Size; // Half-extents along each axis
    public int Parent;
    public int Child;   // Index of first child (children are at Child, Child+1), or first tri index if leaf
    public int NumTris; // >0 for leaf nodes

    public bool IsLeaf => NumTris > 0;

    /// <summary>Get basis matrix from the 3 axes (row-major).</summary>
    public PhysMatrix33 GetBasis()
    {
        return new PhysMatrix33(Axis0, Axis1, Axis2);
    }

    /// <summary>Set axes from a basis matrix (rows).</summary>
    public void SetBasis(in PhysMatrix33 basis)
    {
        Axis0 = basis.GetRow(0);
        Axis1 = basis.GetRow(1);
        Axis2 = basis.GetRow(2);
    }
}

/// <summary>
/// Oriented Bounding Box tree for spatial acceleration of triangle meshes.
/// Provides tighter bounds than AABB for rotated/elongated meshes.
/// Port of COBBTree from CryEngine.
/// </summary>
public class OBBTree : BVTree
{
    private OBBNode[] _nodes = Array.Empty<OBBNode>();
    private int _nodeCount;
    private int _nodesAlloc;

    // Triangle-to-node mapping for marking
    private int[]? _tri2Node;

    // Build parameters
    private int _minTrisPerNode = 2;
    private int _maxTrisPerNode = 4;
    private float _maxSkipDim;
    private int _maxTrisInNode;

    // Temporary build data
    private int[]? _mapVtxUsed;
    private PhysVector3[]? _vtxUsed;

    // Mesh reference (set during build)
    private PhysVector3[] _vertices = Array.Empty<PhysVector3>();
    private int[] _indices = Array.Empty<int>();
    private int _nTris;

    public override int NodeCount => _nodeCount;
    public override int MaxDepth => ComputeMaxDepth(0, 0);

    /// <summary>Set build parameters.</summary>
    public void SetParams(int minTrisPerNode, int maxTrisPerNode, float skipDim)
    {
        _minTrisPerNode = minTrisPerNode;
        _maxTrisPerNode = maxTrisPerNode;
        _maxSkipDim = skipDim;
    }

    /// <summary>
    /// Build OBB tree from triangle mesh data.
    /// Port of COBBTree::Build.
    /// </summary>
    public override void Build(PhysVector3[] vertices, int[] indices, int nTris)
    {
        if (nTris == 0)
        {
            _nodes = Array.Empty<OBBNode>();
            _nodeCount = 0;
            return;
        }

        _vertices = vertices;
        _indices = (int[])indices.Clone(); // Clone because Build reorders indices
        _nTris = nTris;

        _nodesAlloc = 256;
        _nodes = new OBBNode[_nodesAlloc];
        _tri2Node = new int[nTris];
        _maxTrisInNode = 0;

        // Allocate temporary vertex tracking
        int nVerts = vertices.Length;
        _mapVtxUsed = new int[((nVerts - 1) >> 5) + 1];
        _vtxUsed = new PhysVector3[nVerts];

        _nodeCount = 2;
        _nodes[0].Parent = -1;
        _nodes[1].Parent = -1;

        BuildNode(0, 0, nTris, 0);

        // Trim node array
        if (_nodesAlloc > _nodeCount)
        {
            var trimmed = new OBBNode[_nodeCount];
            Array.Copy(_nodes, trimmed, _nodeCount);
            _nodes = trimmed;
            _nodesAlloc = _nodeCount;
        }

        // Cleanup temp data
        _mapVtxUsed = null;
        _vtxUsed = null;

        // Scale maxSkipDim relative to root
        if (_nodeCount > 0)
        {
            float rootMax = MathF.Max(MathF.Max(_nodes[0].Size.X, _nodes[0].Size.Y), _nodes[0].Size.Z);
            _maxSkipDim *= rootMax;
        }
    }

    /// <summary>
    /// Recursively build a node.
    /// Port of COBBTree::BuildNode.
    /// </summary>
    private float BuildNode(int iNode, int iTriStart, int nTris, int nDepth)
    {
        int idx0 = iTriStart * 3;
        int nIdx = nTris * 3;

        // Collect unique vertices used by this set of triangles
        int iStart = int.MaxValue, iEnd = -1;
        for (int i = 0; i < nIdx; i++)
        {
            int vi = _indices[idx0 + i];
            _mapVtxUsed![vi >> 5] |= 1 << (vi & 31);
            iStart = System.Math.Min(iStart, vi);
            iEnd = System.Math.Max(iEnd, vi);
        }

        int nPts = 0;
        for (int i = iStart; i <= iEnd; i++)
        {
            if ((_mapVtxUsed![i >> 5] & (1 << (i & 31))) != 0)
            {
                _vtxUsed![nPts++] = _vertices[i];
                _mapVtxUsed[i >> 5] &= ~(1 << (i & 31));
            }
        }

        // Compute OBB via covariance-based eigen analysis
        var basis = ComputeEigenBasis(_vtxUsed!, nPts);

        // Compute bounding box in the oriented frame
        var ptMin = new PhysVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var ptMax = new PhysVector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < nPts; i++)
        {
            var pt = basis * _vtxUsed![i];
            for (int j = 0; j < 3; j++)
            {
                if (pt[j] < ptMin[j]) ptMin[j] = pt[j];
                if (pt[j] > ptMax[j]) ptMax[j] = pt[j];
            }
        }

        // For root node, check if AABB is tighter
        if (iNode == 0)
        {
            var aaMin = _vtxUsed![0];
            var aaMax = _vtxUsed[0];
            for (int i = 1; i < nPts; i++)
            {
                aaMin = PhysVector3.Min(aaMin, _vtxUsed[i]);
                aaMax = PhysVector3.Max(aaMax, _vtxUsed[i]);
            }
            var aaSize = aaMax - aaMin;
            var obbSize = ptMax - ptMin;
            if (aaSize.X * aaSize.Y * aaSize.Z < obbSize.X * obbSize.Y * obbSize.Z)
            {
                basis = PhysMatrix33.Identity;
                ptMin = aaMin;
                ptMax = aaMax;
            }
        }

        _nodes[iNode].SetBasis(basis);
        _nodes[iNode].Size = (ptMax - ptMin) * 0.5f;
        // Center in world space: transform back from OBB space
        var centerLocal = (ptMax + ptMin) * 0.5f;
        _nodes[iNode].Center = basis.Transposed() * centerLocal;

        // Ensure minimum dimension
        float minDim = MathF.Max(MathF.Max(_nodes[iNode].Size.X, _nodes[iNode].Size.Y), _nodes[iNode].Size.Z) * 0.001f;
        for (int i = 0; i < 3; i++)
            _nodes[iNode].Size[i] = MathF.Max(minDim, _nodes[iNode].Size[i]);

        // Leaf node check
        if (nTris <= _maxTrisPerNode)
        {
            _nodes[iNode].Child = iTriStart;
            _nodes[iNode].NumTris = nTris;
            _maxTrisInNode = System.Math.Max(_maxTrisInNode, nTris);
            for (int i = iTriStart; i < iTriStart + nTris; i++)
                _tri2Node![i] = iNode;
            return _nodes[iNode].Size.X * _nodes[iNode].Size.Y * _nodes[iNode].Size.Z;
        }

        // Find best split axis
        int bestAxis = 0;
        float bestScore = float.MinValue;
        int[] bestMode = new int[3];

        PhysVector3[] nodeAxes = { _nodes[iNode].Axis0, _nodes[iNode].Axis1, _nodes[iNode].Axis2 };

        for (int iAxis = 0; iAxis < 3; iAxis++)
        {
            float sz = _nodes[iNode].Size[iAxis];
            if (sz < minDim * 10)
            {
                continue;
            }

            var axis = nodeAxes[iAxis];
            float cx = axis.Dot(_nodes[iNode].Center);

            int[] numTris = new int[3];
            float[,] bounds = new float[3, 2];
            for (int m = 0; m < 3; m++) { bounds[m, 0] = -sz; bounds[m, 1] = sz; }

            for (int i = iTriStart; i < iTriStart + nTris; i++)
            {
                float x0 = _vertices[_indices[i * 3 + 0]].Dot(axis) - cx;
                float x1 = _vertices[_indices[i * 3 + 1]].Dot(axis) - cx;
                float x2 = _vertices[_indices[i * 3 + 2]].Dot(axis) - cx;

                float xmin = MathF.Min(MathF.Min(x0, x1), x2);
                float xmax = MathF.Max(MathF.Max(x0, x1), x2);

                // Mode 0: group triangles entirely below center
                int iPart0 = xmax >= 0 ? 1 : 0;
                if (iPart0 == 1) bounds[0, 1] = MathF.Min(bounds[0, 1], xmin);
                else bounds[0, 0] = MathF.Max(bounds[0, 0], xmax);
                numTris[0] += iPart0;

                // Mode 1: group triangles entirely above center
                int iPart1 = xmin >= 0 ? 1 : 0;
                if (iPart1 == 1) bounds[1, 1] = MathF.Min(bounds[1, 1], xmin);
                else bounds[1, 0] = MathF.Max(bounds[1, 0], xmax);
                numTris[1] += iPart1;

                // Mode 2: centroid-based
                int iPart2 = (x0 + x1 + x2) >= 0 ? 1 : 0;
                if (iPart2 == 1) bounds[2, 1] = MathF.Min(bounds[2, 1], xmin);
                else bounds[2, 0] = MathF.Max(bounds[2, 0], xmax);
                numTris[2] += iPart2;
            }

            // Find best mode for this axis
            float[] diff = new float[3];
            for (int m = 0; m < 3; m++)
            {
                diff[m] = bounds[m, 1] - bounds[m, 0] - sz;
                // Penalize degenerate splits
                if (numTris[m] < _minTrisPerNode || nTris - numTris[m] < _minTrisPerNode)
                    diff[m] = float.MinValue;
            }

            int modeForAxis = 0;
            if (diff[1] > diff[modeForAxis]) modeForAxis = 1;
            if (diff[2] > diff[modeForAxis]) modeForAxis = 2;
            bestMode[iAxis] = modeForAxis;

            // Score = separation * cross-section area
            float otherDim1 = _nodes[iNode].Size[(iAxis + 1) % 3];
            float otherDim2 = _nodes[iNode].Size[(iAxis + 2) % 3];
            float score = diff[modeForAxis] * otherDim1 * otherDim2;
            if (score > bestScore)
            {
                bestScore = score;
                bestAxis = iAxis;
            }
        }

        // Partition triangles along best axis
        var splitAxis = nodeAxes[bestAxis];
        float splitCx = splitAxis.Dot(_nodes[iNode].Center);
        int splitMode = bestMode[bestAxis];

        int jSplit = iTriStart;
        for (int i = iTriStart; i < iTriStart + nTris; i++)
        {
            float x0 = _vertices[_indices[i * 3 + 0]].Dot(splitAxis) - splitCx;
            float x1 = _vertices[_indices[i * 3 + 1]].Dot(splitAxis) - splitCx;
            float x2 = _vertices[_indices[i * 3 + 2]].Dot(splitAxis) - splitCx;

            int iPart = splitMode switch
            {
                0 => MathF.Max(MathF.Max(x0, x1), x2) < 0 ? 0 : 1,
                1 => MathF.Min(MathF.Min(x0, x1), x2) >= 0 ? 1 : 0,
                2 => (x0 + x1 + x2) >= 0 ? 1 : 0,
                _ => 0
            };

            if (iPart == 0)
            {
                // Swap triangle indices
                for (int k = 0; k < 3; k++)
                    (_indices[i * 3 + k], _indices[jSplit * 3 + k]) = (_indices[jSplit * 3 + k], _indices[i * 3 + k]);
                jSplit++;
            }
        }
        jSplit -= iTriStart;

        // Degenerate split guard
        if (jSplit < _minTrisPerNode || jSplit > nTris - _minTrisPerNode)
        {
            _nodes[iNode].Child = iTriStart;
            _nodes[iNode].NumTris = nTris;
            _maxTrisInNode = System.Math.Max(_maxTrisInNode, nTris);
            for (int i = iTriStart; i < iTriStart + nTris; i++)
                _tri2Node![i] = iNode;
            return _nodes[iNode].Size.X * _nodes[iNode].Size.Y * _nodes[iNode].Size.Z;
        }

        // Allocate children
        if (_nodeCount + 2 > _nodesAlloc)
        {
            _nodesAlloc += 256;
            var newNodes = new OBBNode[_nodesAlloc];
            Array.Copy(_nodes, newNodes, _nodeCount);
            _nodes = newNodes;
        }

        _nodes[iNode].Child = _nodeCount;
        _nodes[_nodeCount].Parent = iNode;
        _nodes[_nodeCount + 1].Parent = iNode;
        _nodes[_nodeCount].NumTris = 0;
        _nodes[_nodeCount + 1].NumTris = 0;
        int childIdx = _nodeCount;
        _nodeCount += 2;

        float result = BuildNode(childIdx + 1, iTriStart + jSplit, nTris - jSplit, nDepth + 1);
        result += BuildNode(childIdx, iTriStart, jSplit, nDepth + 1);
        return result;
    }

    /// <summary>
    /// Compute an eigen-basis from a point cloud using covariance analysis.
    /// Simplified port of ComputeMeshEigenBasis.
    /// </summary>
    private static PhysMatrix33 ComputeEigenBasis(PhysVector3[] pts, int nPts)
    {
        if (nPts < 3)
            return PhysMatrix33.Identity;

        // Compute mean
        var mean = PhysVector3.Zero;
        for (int i = 0; i < nPts; i++)
            mean = mean + pts[i];
        mean = mean * (1f / nPts);

        // Compute covariance matrix
        float cxx = 0, cxy = 0, cxz = 0, cyy = 0, cyz = 0, czz = 0;
        for (int i = 0; i < nPts; i++)
        {
            var d = pts[i] - mean;
            cxx += d.X * d.X;
            cxy += d.X * d.Y;
            cxz += d.X * d.Z;
            cyy += d.Y * d.Y;
            cyz += d.Y * d.Z;
            czz += d.Z * d.Z;
        }

        // Compute eigenvectors via Jacobi iteration (simplified)
        var cov = new PhysMatrix33(
            cxx, cxy, cxz,
            cxy, cyy, cyz,
            cxz, cyz, czz
        );

        ComputeEigenvectors3x3(cov, out var e0, out var e1, out var e2);

        // Build orthonormal basis
        e0 = e0.Normalized();
        e1 = (e1 - e0 * e0.Dot(e1)).Normalized();
        e2 = (e0 ^ e1).Normalized();

        return new PhysMatrix33(e0, e1, e2);
    }

    /// <summary>
    /// Simple eigenvector computation for a symmetric 3x3 matrix using power iteration.
    /// </summary>
    private static void ComputeEigenvectors3x3(in PhysMatrix33 m,
        out PhysVector3 e0, out PhysVector3 e1, out PhysVector3 e2)
    {
        // Power iteration for dominant eigenvector
        e0 = new PhysVector3(1, 0, 0);
        for (int iter = 0; iter < 20; iter++)
        {
            e0 = (m * e0).Normalized();
        }

        // Deflate and find second eigenvector
        float lambda0 = (m * e0).Dot(e0);
        var m2 = new PhysMatrix33(
            m.M00 - lambda0 * e0.X * e0.X, m.M01 - lambda0 * e0.X * e0.Y, m.M02 - lambda0 * e0.X * e0.Z,
            m.M10 - lambda0 * e0.Y * e0.X, m.M11 - lambda0 * e0.Y * e0.Y, m.M12 - lambda0 * e0.Y * e0.Z,
            m.M20 - lambda0 * e0.Z * e0.X, m.M21 - lambda0 * e0.Z * e0.Y, m.M22 - lambda0 * e0.Z * e0.Z
        );

        e1 = new PhysVector3(0, 1, 0);
        if (MathF.Abs(e0.Dot(e1)) > 0.9f) e1 = new PhysVector3(0, 0, 1);
        for (int iter = 0; iter < 20; iter++)
        {
            e1 = (m2 * e1).Normalized();
        }

        e2 = (e0 ^ e1).Normalized();
    }

    public override void GetNodeBV(ref BoundingVolume bv, int iNode)
    {
        if (iNode < 0 || iNode >= _nodeCount) return;
        ref var node = ref _nodes[iNode];
        bv.Type = BVType.Box;
        bv.INode = iNode;
        bv.BBox ??= new Box();
        bv.BBox.Basis = node.GetBasis();
        bv.BBox.IsOriented = true;
        bv.BBox.Center = node.Center;
        bv.BBox.Size = node.Size;
    }

    public override void GetNodeBV(ref BoundingVolume bv, int iNode,
        in PhysVector3 offset, float scale, in PhysQuaternion rotation)
    {
        if (iNode < 0 || iNode >= _nodeCount) return;
        ref var node = ref _nodes[iNode];
        var rotMtx = new PhysMatrix33(rotation);

        bv.Type = BVType.Box;
        bv.INode = iNode;
        bv.BBox ??= new Box();
        bv.BBox.Basis = node.GetBasis() * rotMtx.Transposed();
        bv.BBox.IsOriented = true;
        bv.BBox.Center = rotation.Rotate(node.Center * scale) + offset;
        bv.BBox.Size = node.Size * scale;
    }

    public override int GetNodeContents(int iNode, ref int[] contents)
    {
        if (iNode < 0 || iNode >= _nodeCount || !_nodes[iNode].IsLeaf)
            return 0;
        ref var node = ref _nodes[iNode];
        if (contents.Length < node.NumTris)
            contents = new int[node.NumTris];
        for (int i = 0; i < node.NumTris; i++)
            contents[i] = node.Child + i;
        return node.NumTris;
    }

    /// <summary>
    /// Get children node indices for a non-leaf node.
    /// </summary>
    public (int child0, int child1) GetNodeChildren(int iNode)
    {
        if (iNode < 0 || iNode >= _nodeCount || _nodes[iNode].IsLeaf)
            return (-1, -1);
        int c = _nodes[iNode].Child;
        return (c, c + 1);
    }

    /// <summary>
    /// Split priority for BV tree traversal.
    /// Port of COBBTree::SplitPriority.
    /// </summary>
    public float SplitPriority(int iNode)
    {
        if (iNode < 0 || iNode >= _nodeCount) return 0f;
        ref var node = ref _nodes[iNode];
        // Non-leaf nodes return volume, leaf nodes return 0
        return node.IsLeaf ? 0f : node.Size.X * node.Size.Y * node.Size.Z;
    }

    /// <summary>Get the bounding box of the root node.</summary>
    public void GetBBox(ref Box pbox)
    {
        if (_nodeCount == 0) return;
        ref var root = ref _nodes[0];
        pbox.Basis = root.GetBasis();
        pbox.IsOriented = true;
        pbox.Center = root.Center;
        pbox.Size = root.Size;
    }

    public override List<(Primitive prim, int idx)> GetAllPrimitives(IGeometry geom)
    {
        var result = new List<(Primitive, int)>();
        CollectPrimitives(0, result);
        return result;
    }

    private void CollectPrimitives(int iNode, List<(Primitive, int)> result)
    {
        if (iNode < 0 || iNode >= _nodeCount) return;
        ref var node = ref _nodes[iNode];

        if (node.IsLeaf)
        {
            for (int i = 0; i < node.NumTris; i++)
            {
                int triIdx = node.Child + i;
                if (triIdx * 3 + 2 < _indices.Length)
                {
                    var tri = new Triangle(
                        _vertices[_indices[triIdx * 3]],
                        _vertices[_indices[triIdx * 3 + 1]],
                        _vertices[_indices[triIdx * 3 + 2]]
                    );
                    result.Add((tri, triIdx));
                }
            }
        }
        else
        {
            CollectPrimitives(node.Child, result);
            CollectPrimitives(node.Child + 1, result);
        }
    }

    private int ComputeMaxDepth(int iNode, int depth)
    {
        if (iNode < 0 || iNode >= _nodeCount) return depth;
        ref var node = ref _nodes[iNode];
        if (node.IsLeaf) return depth;
        int d1 = ComputeMaxDepth(node.Child, depth + 1);
        int d2 = ComputeMaxDepth(node.Child + 1, depth + 1);
        return System.Math.Max(d1, d2);
    }

    public override int GetMemoryUsage()
    {
        int size = _nodes.Length * 88; // Approximate per-node size
        if (_tri2Node != null) size += _tri2Node.Length * 4;
        return size;
    }

    // ----------------------------------------------------------------------------
    // Literal C++ COBBTree API overrides (obbtree.cpp).
    // ----------------------------------------------------------------------------

    public override int GetTypeId() => BVTreeTypes.OBB;

    public override void GetNodeBVRef(out BV pBV, int iNode = 0, int iCaller = 0)
    {
        var bb = new BBox { Type = BVTreeTypes.OBB, INode = iNode };
        if (iNode >= 0 && iNode < _nodeCount)
        {
            ref var node = ref _nodes[iNode];
            bb.ABox.Center = node.Center;
            bb.ABox.Size = node.Size;
            bb.ABox.Basis = node.GetBasis();
            bb.ABox.IsOriented = true;
        }
        pBV = bb;
    }

    public override void GetNodeBVRef(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        out BV pBV, int iNode = 0, int iCaller = 0)
    {
        var bb = new BBox { Type = BVTreeTypes.OBB, INode = iNode };
        if (iNode >= 0 && iNode < _nodeCount)
        {
            ref var node = ref _nodes[iNode];
            bb.ABox.Center = Rw * (node.Center * scalew) + offsw;
            bb.ABox.Size = node.Size * scalew;
            bb.ABox.Basis = node.GetBasis() * Rw.Transposed();
            bb.ABox.IsOriented = true;
        }
        pBV = bb;
    }

    public override void GetNodeChildrenBVs(BV pBVParent, out BV? pBVChild1, out BV? pBVChild2, int iCaller = 0)
    {
        pBVChild1 = pBVChild2 = null;
        if (pBVParent.INode < 0 || pBVParent.INode >= _nodeCount) return;
        ref var node = ref _nodes[pBVParent.INode];
        if (node.IsLeaf) return;
        GetNodeBVRef(out var c1, node.Child, iCaller); pBVChild1 = c1;
        GetNodeBVRef(out var c2, node.Child + 1, iCaller); pBVChild2 = c2;
    }

    public override int GetNodeContents(int iNode, BV pBVCollider, int bColliderUsed, int bColliderLocal,
        Geometry.GeometryUnderTest pGTest, Geometry.GeometryUnderTest pGTestOp)
    {
        if (iNode < 0 || iNode >= _nodeCount) return 0;
        ref var node = ref _nodes[iNode];
        if (!node.IsLeaf) return 0;
        if (pGTest.PrimBuf == null || pGTest.PrimBuf.Length < node.NumTris)
            pGTest.PrimBuf = new IndexedTriangle[System.Math.Max(node.NumTris, 16)];
        for (int i = 0; i < node.NumTris; i++)
            pGTest.PrimBuf[i] = new IndexedTriangle { Index = node.Child + i };
        pGTest.SzPrim = node.NumTris;
        return node.NumTris;
    }

    public override int GetNodeContentsIdx(int iNode, out int iStartPrim)
    {
        if (iNode < 0 || iNode >= _nodeCount) { iStartPrim = 0; return 0; }
        iStartPrim = _nodes[iNode].Child;
        return _nodes[iNode].NumTris;
    }

    public override float SplitPriority(BV pBV) => SplitPriority(pBV.INode);
}
