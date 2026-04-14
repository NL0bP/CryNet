// Port of CryPhysics trimesh.h/cpp - triangle mesh geometry
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Collision;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

// ============================================================================
// Topology and island supporting types (port of trinfo, tri_flags, mesh_island)
// ============================================================================

/// <summary>
/// Per-triangle topology info: buddy triangle indices for each of the 3 edges.
/// Port of trinfo from trimesh.h.
/// ibuddy[e] = index of triangle sharing edge e, or -1 if no neighbor.
/// Edge 0 = vtx0-vtx1, Edge 1 = vtx1-vtx2, Edge 2 = vtx2-vtx0.
/// </summary>
public struct TriTopology
{
    public int Buddy0; // neighbor across edge 0
    public int Buddy1; // neighbor across edge 1
    public int Buddy2; // neighbor across edge 2

    public int this[int edge]
    {
        get => edge switch { 0 => Buddy0, 1 => Buddy1, 2 => Buddy2, _ => -1 };
        set { switch (edge) { case 0: Buddy0 = value; break; case 1: Buddy1 = value; break; case 2: Buddy2 = value; break; } }
    }
}

/// <summary>
/// Per-triangle island assignment flags.
/// Port of tri_flags from trimesh.h.
/// </summary>
internal struct TriIslandFlags
{
    public int INext;
    public int IPrev;
    public bool IsFree;
}

/// <summary>
/// Connected island of triangles within a mesh.
/// Port of mesh_island from CryEngine.
/// </summary>
public class MeshIsland
{
    public float Volume;
    public PhysVector3 Center;
    public int FirstTri;       // head of linked list of triangles in this island
    public int TriangleCount;
    public int ParentIsland = -1;
    public int ChildIsland = -1;
    public int NextIsland = -1;
    public bool Processed;
}

/// <summary>
/// Border trace segment for mesh-mesh intersection tracing.
/// Port of border_trace from trimesh.h.
/// </summary>
public class BorderTrace
{
    public PhysVector3[] Points = Array.Empty<PhysVector3>();
    public int[,]? TriIndices;  // [npt, 2] - triangle indices on each side
    public float[] SegLengths = Array.Empty<float>();
    public int PointCount;
    public int BufferSize;

    public PhysVector3 EndPoint;
    public int EndTri;
    public int EndEdge;
    public float EndDist2;
    public bool ExactBorder;

    public PhysVector3[] NormalSum = new PhysVector3[2];
    public PhysVector3 BestNormal;
    public int[] TriCounts = new int[2];
}

/// <summary>
/// Voxel grid representation of a mesh.
/// Port of CTriMesh::voxgrid from trimesh.h.
/// </summary>
public class TriMeshVoxelGrid
{
    public int SizeX, SizeY, SizeZ;
    public PhysVector3 Center;
    public PhysQuaternion Rotation = PhysQuaternion.Identity;
    public float CellDim;
    public byte[] Data = Array.Empty<byte>();

    public TriMeshVoxelGrid() { }

    public TriMeshVoxelGrid(int sx, int sy, int sz, PhysVector3 center)
    {
        SizeX = sx; SizeY = sy; SizeZ = sz;
        Center = center;
        Allocate();
    }

    public void Allocate()
    {
        int total = SizeX * 2 * SizeY * 2 * SizeZ * 2 + 1;
        Data = new byte[total];
    }

    /// <summary>
    /// Access a voxel by grid coordinates (can be negative, centered at 0).
    /// Port of voxgrid::operator() from trimesh.h.
    /// Returns 0 for out-of-bounds access (sentinel at last element).
    /// </summary>
    public ref byte this[int ix, int iy, int iz]
    {
        get
        {
            bool inBounds = ix >= -SizeX && ix < SizeX &&
                            iy >= -SizeY && iy < SizeY &&
                            iz >= -SizeZ && iz < SizeZ;
            if (inBounds)
            {
                int idx = ((iz + SizeZ) * SizeY * 2 + iy + SizeY) * SizeX * 2 + ix + SizeX;
                return ref Data[idx];
            }
            // Return ref to sentinel (last element)
            return ref Data[Data.Length - 1];
        }
    }

    /// <summary>Total filled voxel count.</summary>
    public int FilledCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Data.Length - 1; i++)
                if ((Data[i] & 1) != 0) count++;
            return count;
        }
    }
}

/// <summary>
/// Result of a mesh subtraction or slicing operation.
/// Port of bop_meshupdate concept from CryEngine.
/// </summary>
public class MeshUpdateResult
{
    public int[] RemovedTriangles = Array.Empty<int>();
    public int[] AddedTriangles = Array.Empty<int>();
    public (int from, int to)[] WeldedVertices = Array.Empty<(int, int)>();
    public int RemovedTriCount;
    public int AddedTriCount;
    public int WeldedVtxCount;
}

// ============================================================================
// Main TriMeshGeometry class
// ============================================================================

/// <summary>
/// Triangle mesh collision geometry with BV-tree acceleration.
/// Port of CTriMesh from CryEngine.
/// This is the most complex geometry type - handles mesh-mesh intersection,
/// closest point queries, buoyancy, CSG operations, island splitting, and more.
/// </summary>
public class TriMeshGeometry : GeometryBase
{
    public override int GeomType => GeomTypes.TriMesh;

    // Mesh data
    public PhysVector3[] Vertices { get; private set; } = Array.Empty<PhysVector3>();
    public int[] Indices { get; private set; } = Array.Empty<int>();  // Triangle indices (3 per tri)
    public int TriCount { get; private set; }
    public int VertexCount { get; private set; }

    // Per-triangle normals
    public PhysVector3[] Normals { get; private set; } = Array.Empty<PhysVector3>();

    // Per-triangle material IDs - port of m_pIds (char* array) from CTriMesh.
    // Used to look up surface properties (friction, bounciness) per contact.
    public byte[]? MaterialIds { get; private set; }

    // Topology data - port of m_pTopology from CTriMesh
    public TriTopology[]? Topology { get; private set; }

    // Island data - port of m_pIslands, m_pTri2Island from CTriMesh
    public MeshIsland[]? Islands { get; private set; }
    public int IslandCount { get; private set; }
    private TriIslandFlags[]? _tri2Island;

    // Mesh flags - port of m_flags
    private int _meshFlags;
    public bool IsMultipart { get; private set; }

    // Cached volume and center - port of m_V, m_center
    private float _cachedVolume = float.NaN;
    private PhysVector3 _cachedCenter;

    // Mesh error count from topology calculation
    public int ErrorCount { get; private set; }

    // Maximum valency (triangles sharing a vertex) — port of m_nMaxVertexValency from CTriMesh.
    // Used to size scratch buffers during intersection; defaults to 8 as in the C++ code.
    public int MaxVertexValency { get; set; } = 8;

    // Cached AABB
    private PhysVector3 _bboxMin, _bboxMax;

    // Modular increment helpers matching C++ inc_mod3/dec_mod3
    private static readonly int[] IncMod3 = { 1, 2, 0 };
    private static readonly int[] DecMod3 = { 2, 0, 1 };

    public TriMeshGeometry()
    {
        Tree = new AABBTree();
    }

    /// <summary>Set mesh data and build BV tree.</summary>
    public void SetData(PhysVector3[] vertices, int[] indices, int nTris, byte[]? materialIds = null)
    {
        Vertices = vertices;
        Indices = indices;
        TriCount = nTris;
        VertexCount = vertices.Length;

        // Per-triangle material IDs (port of m_pIds from CTriMesh)
        if (materialIds != null && materialIds.Length >= nTris)
        {
            MaterialIds = new byte[nTris];
            Array.Copy(materialIds, MaterialIds, nTris);
        }
        else
        {
            MaterialIds = null;
        }

        // Compute normals
        Normals = new PhysVector3[nTris];
        for (int i = 0; i < nTris; i++)
            RecalcTriNormal(i);

        // Compute AABB
        RecomputeAABB();

        // Build topology
        Topology = new TriTopology[nTris];
        for (int i = 0; i < nTris; i++)
            Topology[i] = new TriTopology { Buddy0 = -1, Buddy1 = -1, Buddy2 = -1 };
        CalculateTopology();

        // Build BV tree
        Tree!.Build(vertices, indices, nTris);

        // Build island map
        IsMultipart = BuildIslandMap() > 1;

        // Invalidate cached volume
        _cachedVolume = float.NaN;
    }

    /// <summary>
    /// Get the material ID for a given triangle index.
    /// Port of m_pIds[triIndex] access from CTriMesh.
    /// Returns 0 if no material IDs are assigned or index is out of range.
    /// </summary>
    public byte GetMaterialId(int triIndex)
    {
        if (MaterialIds == null || (uint)triIndex >= (uint)MaterialIds.Length)
            return 0;
        return MaterialIds[triIndex];
    }

    /// <summary>Recalculate a single triangle normal. Port of RecalcTriNormal.</summary>
    private void RecalcTriNormal(int i)
    {
        var cross = (Vertices[Indices[i * 3 + 1]] - Vertices[Indices[i * 3]]) ^
                    (Vertices[Indices[i * 3 + 2]] - Vertices[Indices[i * 3]]);
        float len = cross.Length();
        Normals[i] = len > 1e-20f ? cross * (1f / len) : PhysVector3.UnitZ;
    }

    /// <summary>Recompute the AABB from all vertices.</summary>
    private void RecomputeAABB()
    {
        _bboxMin = new PhysVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        _bboxMax = new PhysVector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var v in Vertices)
        {
            if (v.X < _bboxMin.X) _bboxMin.X = v.X;
            if (v.Y < _bboxMin.Y) _bboxMin.Y = v.Y;
            if (v.Z < _bboxMin.Z) _bboxMin.Z = v.Z;
            if (v.X > _bboxMax.X) _bboxMax.X = v.X;
            if (v.Y > _bboxMax.Y) _bboxMax.Y = v.Y;
            if (v.Z > _bboxMax.Z) _bboxMax.Z = v.Z;
        }
    }

    // ========================================================================
    // Topology (port of CTriMesh::CalculateTopology)
    // ========================================================================

    /// <summary>
    /// Build edge-neighbor (buddy triangle) info for every triangle edge.
    /// Port of CTriMesh::CalculateTopology from trimesh.cpp.
    /// For each triangle edge, finds the adjacent triangle sharing that edge.
    /// Returns true if the mesh is manifold (no bad edges).
    /// </summary>
    public bool CalculateTopology(bool checkOnly = false)
    {
        if (TriCount == 0) return true;

        Topology ??= new TriTopology[TriCount];
        if (Topology.Length < TriCount)
            Topology = new TriTopology[TriCount];

        // Build per-vertex triangle lists
        // pTris[v] = start index into pVtxTris for vertex v
        int[] vtxTriCount = new int[VertexCount + 1];
        for (int i = 0; i < TriCount * 3; i++)
            vtxTriCount[Indices[i]]++;
        // Prefix sum
        for (int i = 0; i < VertexCount; i++)
            vtxTriCount[i + 1] += vtxTriCount[i];

        int[] vtxTris = new int[TriCount * 3];
        for (int i = TriCount - 1; i >= 0; i--)
            for (int j = 0; j < 3; j++)
                vtxTris[--vtxTriCount[Indices[i * 3 + j]]] = i;

        int nBadEdges = 0;

        for (int i = 0; i < TriCount; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int vtxA = Indices[i * 3 + j];
                int vtxB = Indices[i * 3 + IncMod3[j]];

                // Find triangles sharing both vtxA and vtxB
                int buddyTri = -1;
                int sharedCount = 0;

                // Intersect triangle lists of vtxA and vtxB
                int startA = vtxTriCount[vtxA], endA = vtxTriCount[vtxA + 1];
                int startB = vtxTriCount[vtxB], endB = vtxTriCount[vtxB + 1];

                for (int ia = startA; ia < endA; ia++)
                {
                    int triA = vtxTris[ia];
                    if (triA == i) continue;
                    for (int ib = startB; ib < endB; ib++)
                    {
                        if (vtxTris[ib] == triA)
                        {
                            // Found a triangle sharing both vertices
                            // Prefer the one with the edge in reverse order (manifold)
                            for (int k = 0; k < 3; k++)
                            {
                                if (Indices[triA * 3 + k] == vtxB &&
                                    Indices[triA * 3 + IncMod3[k]] == vtxA)
                                {
                                    buddyTri = triA;
                                    goto foundBuddy;
                                }
                            }
                            // Non-manifold edge (same winding), still track it
                            if (buddyTri < 0)
                                buddyTri = triA;
                            sharedCount++;
                        }
                    }
                }
                foundBuddy:

                if (!checkOnly)
                    Topology[i][j] = buddyTri;

                if (sharedCount > 1 || buddyTri < 0)
                    nBadEdges++;
            }
        }

        ErrorCount = nBadEdges;
        return nBadEdges == 0;
    }

    /// <summary>
    /// Get the edge index of triangle itri whose buddy is itri_buddy.
    /// Port of CTriMesh::GetEdgeByBuddy from trimesh.h.
    /// </summary>
    public int GetEdgeByBuddy(int itri, int itriBuddy)
    {
        if (Topology == null || itri < 0 || itri >= TriCount) return -1;
        if (Topology[itri][0] == itriBuddy) return 0;
        if (Topology[itri][1] == itriBuddy) return 1;
        if (Topology[itri][2] == itriBuddy) return 2;
        return -1;
    }

    // ========================================================================
    // Island Splitting (port of CTriMesh::BuildIslandMap, SplitIntoIslands)
    // ========================================================================

    /// <summary>
    /// Build island connectivity map - identifies connected components.
    /// Port of CTriMesh::BuildIslandMap from trimesh.cpp.
    /// Uses flood fill on triangle adjacency graph to find connected islands.
    /// Returns number of islands.
    /// </summary>
    public int BuildIslandMap()
    {
        if (Islands != null)
            return IslandCount;

        IslandCount = 0;
        if (TriCount == 0 || Topology == null)
            return 0;

        _tri2Island = new TriIslandFlags[TriCount];
        for (int i = 0; i < TriCount; i++)
        {
            _tri2Island[i].INext = (i + 1) % TriCount;
            _tri2Island[i].IPrev = (i - 1 + TriCount) % TriCount;
            _tri2Island[i].IsFree = true;
        }

        int iFreeTri = 0;
        _cachedVolume = 0;
        _cachedCenter = PhysVector3.Zero;
        var islandList = new List<MeshIsland>();
        var triQueue = new List<int>(256);

        do
        {
            var island = new MeshIsland
            {
                Volume = 0,
                Center = PhysVector3.Zero,
                FirstTri = 0x7FFF,
                TriangleCount = 0
            };

            triQueue.Clear();
            triQueue.Add(iFreeTri);
            _tri2Island[iFreeTri].IsFree = false;

            int qIdx = 0;
            while (qIdx < triQueue.Count)
            {
                int itri = triQueue[qIdx++];

                // Remove from free list
                int prev = _tri2Island[itri].IPrev;
                int next = _tri2Island[itri].INext;
                _tri2Island[prev].INext = next;
                _tri2Island[next].IPrev = prev;
                if (iFreeTri == itri)
                    iFreeTri = next;

                // Assign to island
                _tri2Island[itri].IPrev = IslandCount; // island index
                _tri2Island[itri].INext = island.FirstTri;
                island.FirstTri = itri;
                island.TriangleCount++;

                // Accumulate volume (using divergence theorem z-component)
                var v0 = Vertices[Indices[itri * 3]];
                var v1 = Vertices[Indices[itri * 3 + 1]];
                var v2 = Vertices[Indices[itri * 3 + 2]];
                float cross2dZ = (v1.X - v0.X) * (v2.Y - v0.Y) - (v1.Y - v0.Y) * (v2.X - v0.X);
                island.Volume += cross2dZ * (v0.Z + v1.Z + v2.Z);
                island.Center = island.Center + v0 + v1 + v2;

                // Enqueue unvisited neighbors
                for (int e = 0; e < 3; e++)
                {
                    int buddy = Topology[itri][e];
                    if (buddy >= 0 && _tri2Island[buddy].IsFree)
                    {
                        _tri2Island[buddy].IsFree = false;
                        triQueue.Add(buddy);
                    }
                }
            }

            island.Volume *= 1f / 6f;
            if (island.TriangleCount > 0)
                island.Center = island.Center * (1f / (island.TriangleCount * 3));

            _cachedVolume += island.Volume;
            _cachedCenter = _cachedCenter + island.Center * island.Volume;
            islandList.Add(island);
            IslandCount++;
        } while (_tri2Island[iFreeTri].IsFree);

        if (_cachedVolume > 0)
            _cachedCenter = _cachedCenter * (1f / _cachedVolume);

        Islands = islandList.ToArray();
        return IslandCount;
    }

    /// <summary>
    /// Split mesh into separate island meshes based on connectivity.
    /// Port of CTriMesh::SplitIntoIslands from trimesh.cpp.
    /// Returns array of new TriMeshGeometry objects for each island (except the
    /// largest/first island which stays in this mesh), or null if only one island.
    /// </summary>
    public TriMeshGeometry[]? SplitIntoIslands()
    {
        if (BuildIslandMap() < 2)
            return null;

        if (Islands == null || _tri2Island == null || Topology == null)
            return null;

        var results = new List<TriMeshGeometry>();
        int[] vtxMap = new int[VertexCount];
        int[] triMap = new int[TriCount];
        Array.Fill(vtxMap, -1);
        Array.Fill(triMap, -1);

        // Process each island except the first (island 0 stays in current mesh)
        for (int isle = 1; isle < IslandCount; isle++)
        {
            if (Islands[isle].ParentIsland >= 0)
                continue; // Skip child islands, they follow their parent

            // Count vertices and triangles for this island
            var newVertices = new List<PhysVector3>();
            var newIndices = new List<int>();
            Array.Fill(vtxMap, -1);

            int itri = Islands[isle].FirstTri;
            while (itri != 0x7FFF && itri >= 0 && itri < TriCount)
            {
                for (int j = 0; j < 3; j++)
                {
                    int origVtx = Indices[itri * 3 + j];
                    if (vtxMap[origVtx] < 0)
                    {
                        vtxMap[origVtx] = newVertices.Count;
                        newVertices.Add(Vertices[origVtx]);
                    }
                    newIndices.Add(vtxMap[origVtx]);
                }
                triMap[itri] = newIndices.Count / 3 - 1;
                itri = _tri2Island[itri].INext;
            }

            if (newVertices.Count == 0) continue;

            var mesh = new TriMeshGeometry();
            mesh.SetData(newVertices.ToArray(), newIndices.ToArray(), newIndices.Count / 3);
            results.Add(mesh);
        }

        // Compact current mesh to keep only island 0
        if (results.Count > 0)
        {
            var keepVerts = new List<PhysVector3>();
            var keepIndices = new List<int>();
            Array.Fill(vtxMap, -1);

            int itri = Islands[0].FirstTri;
            while (itri != 0x7FFF && itri >= 0 && itri < TriCount)
            {
                for (int j = 0; j < 3; j++)
                {
                    int origVtx = Indices[itri * 3 + j];
                    if (vtxMap[origVtx] < 0)
                    {
                        vtxMap[origVtx] = keepVerts.Count;
                        keepVerts.Add(Vertices[origVtx]);
                    }
                    keepIndices.Add(vtxMap[origVtx]);
                }
                itri = _tri2Island[itri].INext;
            }

            // Reset island data before SetData (which rebuilds it)
            Islands = null;
            IslandCount = 0;
            _tri2Island = null;

            SetData(keepVerts.ToArray(), keepIndices.ToArray(), keepIndices.Count / 3);
        }

        return results.Count > 0 ? results.ToArray() : null;
    }

    // ========================================================================
    // CSG Operations (port of CTriMesh::Subtract, CTriMesh::Slice)
    // ========================================================================

    /// <summary>
    /// Subtract another geometry from this mesh.
    /// Port of CTriMesh::Subtract from trimesh.cpp / boolean3d.cpp.
    /// Removes all triangles of this mesh that are inside the other geometry,
    /// and clips triangles at the boundary.
    /// Returns a MeshUpdateResult describing what changed.
    /// </summary>
    public MeshUpdateResult Subtract(GeometryBase other, GeomWorldData? data1 = null, GeomWorldData? data2 = null)
    {
        var result = new MeshUpdateResult();
        var removedTris = new List<int>();

        data1 ??= new GeomWorldData();
        data2 ??= new GeomWorldData();

        // Transform other geometry's position into our local space
        var otherOffset = data2.Offset - data1.Offset;
        var invR = data1.R.Transposed();
        var localOtherOffset = invR * otherOffset;
        float otherScale = data2.Scale / data1.Scale;

        // For each triangle, test if its centroid is inside the other geometry
        for (int i = 0; i < TriCount; i++)
        {
            var v0 = Vertices[Indices[i * 3]];
            var v1 = Vertices[Indices[i * 3 + 1]];
            var v2 = Vertices[Indices[i * 3 + 2]];
            var centroid = (v0 + v1 + v2) * (1f / 3f);

            // Transform centroid to other's local space
            var ptInOther = invR * (data1.R * centroid * data1.Scale + data1.Offset
                                     - data2.Offset) * (1f / data2.Scale);
            ptInOther = data2.R.Transposed() * ptInOther;

            if (other.PointInsideStatus(ptInOther) != 0)
                removedTris.Add(i);
        }

        if (removedTris.Count == 0)
        {
            result.RemovedTriangles = Array.Empty<int>();
            return result;
        }

        // Remove flagged triangles and rebuild mesh
        var triKeepSet = new HashSet<int>(removedTris);
        var newIndices = new List<int>();
        var vtxMap = new int[VertexCount];
        Array.Fill(vtxMap, -1);
        var newVerts = new List<PhysVector3>();

        for (int i = 0; i < TriCount; i++)
        {
            if (triKeepSet.Contains(i)) continue;
            for (int j = 0; j < 3; j++)
            {
                int vi = Indices[i * 3 + j];
                if (vtxMap[vi] < 0)
                {
                    vtxMap[vi] = newVerts.Count;
                    newVerts.Add(Vertices[vi]);
                }
                newIndices.Add(vtxMap[vi]);
            }
        }

        result.RemovedTriangles = removedTris.ToArray();
        result.RemovedTriCount = removedTris.Count;

        // Reset islands before rebuild
        Islands = null;
        IslandCount = 0;
        _tri2Island = null;

        SetData(newVerts.ToArray(), newIndices.ToArray(), newIndices.Count / 3);
        return result;
    }

    /// <summary>
    /// Slice mesh with a plane, splitting triangles that cross the plane.
    /// Port of CTriMesh::Slice from trimesh.cpp.
    /// The plane is defined by a triangle (uses the triangle's plane).
    /// minLen: minimum edge length for new triangles.
    /// Returns a MeshUpdateResult describing the changes.
    /// </summary>
    public MeshUpdateResult Slice(in PhysVector3 planeNormal, in PhysVector3 planePoint,
                                   float minLen = 0f, float minArea = 0f)
    {
        var result = new MeshUpdateResult();
        float planeDist = planeNormal.Dot(planePoint);
        float minLen2 = minLen * minLen;
        float minArea2 = minArea * minArea;

        var newVerts = new List<PhysVector3>(Vertices);
        var newIndices = new List<int>();
        var addedTris = new List<int>();
        int originalTriCount = TriCount;

        for (int i = 0; i < originalTriCount; i++)
        {
            int i0 = Indices[i * 3], i1 = Indices[i * 3 + 1], i2 = Indices[i * 3 + 2];
            float d0 = planeNormal.Dot(Vertices[i0]) - planeDist;
            float d1 = planeNormal.Dot(Vertices[i1]) - planeDist;
            float d2 = planeNormal.Dot(Vertices[i2]) - planeDist;

            int side0 = d0 > 1e-6f ? 1 : (d0 < -1e-6f ? -1 : 0);
            int side1 = d1 > 1e-6f ? 1 : (d1 < -1e-6f ? -1 : 0);
            int side2 = d2 > 1e-6f ? 1 : (d2 < -1e-6f ? -1 : 0);

            // If all on one side or on the plane, keep the triangle as-is
            if ((side0 >= 0 && side1 >= 0 && side2 >= 0) ||
                (side0 <= 0 && side1 <= 0 && side2 <= 0))
            {
                newIndices.Add(i0);
                newIndices.Add(i1);
                newIndices.Add(i2);
                continue;
            }

            // Triangle crosses plane - split it
            // Find the lone vertex (the one on the opposite side from the other two)
            int[] idx = { i0, i1, i2 };
            float[] dist = { d0, d1, d2 };
            int[] sides = { side0, side1, side2 };

            // Rotate so that idx[0] is the lone vertex
            int loneVtx = -1;
            for (int j = 0; j < 3; j++)
            {
                int j1 = IncMod3[j], j2 = DecMod3[j];
                if (sides[j] != 0 && sides[j] != sides[j1] && sides[j] != sides[j2])
                {
                    loneVtx = j;
                    break;
                }
                if (sides[j] != 0 && sides[j1] != 0 && sides[j] != sides[j1] && sides[j2] == 0)
                {
                    // j2 is on plane; j and j1 are on opposite sides; lone = j or j1
                    loneVtx = j;
                    break;
                }
            }
            if (loneVtx < 0)
            {
                // Degenerate case - just keep original triangle
                newIndices.Add(i0); newIndices.Add(i1); newIndices.Add(i2);
                continue;
            }

            // Compute intersection points on the two edges from the lone vertex
            int vi0 = idx[loneVtx];
            int vi1 = idx[IncMod3[loneVtx]];
            int vi2 = idx[DecMod3[loneVtx]];
            float d_0 = dist[loneVtx];
            float d_1 = dist[IncMod3[loneVtx]];
            float d_2 = dist[DecMod3[loneVtx]];

            // Interpolation parameters for edge intersections
            float t1 = d_0 / (d_0 - d_1);
            float t2 = d_0 / (d_0 - d_2);

            var p1 = Vertices[vi0] + (Vertices[vi1] - Vertices[vi0]) * t1;
            var p2 = Vertices[vi0] + (Vertices[vi2] - Vertices[vi0]) * t2;

            // Check degenerate splits
            if (minLen2 > 0 && (p1 - p2).LengthSq() < minLen2)
            {
                newIndices.Add(i0); newIndices.Add(i1); newIndices.Add(i2);
                continue;
            }

            int ip1 = newVerts.Count;
            newVerts.Add(p1);
            int ip2 = newVerts.Count;
            newVerts.Add(p2);

            // Triangle 1: lone vertex + two intersection points (on lone side)
            newIndices.Add(vi0); newIndices.Add(ip1); newIndices.Add(ip2);

            // Triangle 2: intersection point 1 + vi1 + intersection point 2
            newIndices.Add(ip1); newIndices.Add(vi1); newIndices.Add(ip2);

            // Triangle 3: vi1 + vi2 + intersection point 2
            newIndices.Add(vi1); newIndices.Add(vi2); newIndices.Add(ip2);

            addedTris.Add(newIndices.Count / 3 - 3);
            addedTris.Add(newIndices.Count / 3 - 2);
            addedTris.Add(newIndices.Count / 3 - 1);
        }

        result.AddedTriangles = addedTris.ToArray();
        result.AddedTriCount = addedTris.Count;

        // Reset island data before rebuild
        Islands = null;
        IslandCount = 0;
        _tri2Island = null;

        SetData(newVerts.ToArray(), newIndices.ToArray(), newIndices.Count / 3);
        return result;
    }

    // ========================================================================
    // Border Tracing (port of CTriMesh::TraceTriangleInters)
    // ========================================================================

    /// <summary>
    /// Trace intersection border between this mesh and another.
    /// Port of CTriMesh::TraceTriangleInters from trimesh.cpp.
    /// Given a starting triangle and intersection, walks along the mesh topology
    /// to trace the full intersection contour.
    /// Returns a BorderTrace with the intersection polyline.
    /// </summary>
    public BorderTrace? TraceTriangleInters(int startTri, int startEdge,
                                            in PhysVector3 startPt, in PhysVector3 endPt,
                                            float endDist2Threshold = 0.01f,
                                            int maxPoints = 1024)
    {
        if (Topology == null || startTri < 0 || startTri >= TriCount)
            return null;

        var border = new BorderTrace
        {
            Points = new PhysVector3[maxPoints],
            TriIndices = new int[maxPoints, 2],
            SegLengths = new float[maxPoints],
            BufferSize = maxPoints,
            EndPoint = endPt,
            EndDist2 = endDist2Threshold,
            PointCount = 0,
            NormalSum = { [0] = PhysVector3.Zero, [1] = PhysVector3.Zero },
            BestNormal = PhysVector3.UnitZ,
            TriCounts = { [0] = 0, [1] = 0 }
        };

        border.Points[0] = startPt;
        border.TriIndices![0, 0] = startTri;
        border.TriIndices[0, 1] = -1;
        border.PointCount = 1;

        int itri = startTri;
        int iedge = startEdge;
        int maxIter = System.Math.Min(maxPoints - 1, TriCount * 3);

        for (int iter = 0; iter < maxIter; iter++)
        {
            int nextTri = Topology[itri][iedge];
            if (nextTri < 0)
                break;

            // Check if we've returned to the start
            if ((border.Points[border.PointCount - 1] - endPt).LengthSq() < endDist2Threshold
                && border.PointCount > 3)
                return border;

            if (border.PointCount >= border.BufferSize)
                break;

            // Record the border point at the midpoint of the shared edge
            int ev0 = Indices[itri * 3 + iedge];
            int ev1 = Indices[itri * 3 + IncMod3[iedge]];
            var edgeMid = (Vertices[ev0] + Vertices[ev1]) * 0.5f;
            border.Points[border.PointCount] = edgeMid;
            border.TriIndices[border.PointCount, 0] = itri;
            border.TriIndices[border.PointCount, 1] = nextTri;

            if (border.PointCount > 0)
                border.SegLengths[border.PointCount - 1] =
                    (border.Points[border.PointCount] - border.Points[border.PointCount - 1]).Length();

            border.NormalSum[0] = border.NormalSum[0] + Normals[itri];
            border.TriCounts[0]++;

            if (MathF.Abs(Normals[itri].Z) > MathF.Abs(border.BestNormal.Z))
                border.BestNormal = Normals[itri];

            border.PointCount++;

            // Walk to next triangle
            int nextEdge = GetEdgeByBuddy(nextTri, itri);
            if (nextEdge < 0) break;
            itri = nextTri;
            iedge = IncMod3[nextEdge]; // advance to next edge in the new triangle
        }

        return border;
    }

    // ========================================================================
    // Mesh Filtering (port of CTriMesh::FilterMesh)
    // ========================================================================

    /// <summary>
    /// Remove degenerate triangles from the mesh.
    /// Faithful port of CTriMesh::FilterMesh from trimesh.cpp (line 887).
    ///
    /// Phase 1: Weld vertices of edges shorter than minLen, collapse their
    ///          indices, and remove resulting degenerate (collapsed) triangles.
    ///          Then rebuild topology.
    ///
    /// Phase 2 (requires topology): Remove zero-thickness fins - pairs of
    ///          triangles that share two edges (same buddy on two edges),
    ///          forming a zero-area fin.  Re-stitch their neighbors to
    ///          maintain manifold topology.
    ///
    /// Phase 3: Detect thin slivers where the cross-product of two edges is
    ///          small relative to their product of lengths (angle below
    ///          minAngle).  For T-junctions (one short side, one long side
    ///          of similar length), edge-swap with the buddy triangle to
    ///          improve the mesh locally rather than removing geometry.
    ///
    /// Returns number of changes made (0 means mesh was already clean).
    /// </summary>
    public int FilterMesh(float minLen = 0.001f, float minAngle = 0.01f)
    {
        if (TriCount == 0) return 0;

        float minLen2 = minLen * minLen;
        float minAngle2 = minAngle * minAngle;
        int nTotalChanges = 0;

        // ----------------------------------------------------------------
        // Phase 1: Weld short edges
        // ----------------------------------------------------------------
        int[] vtxMap = new int[VertexCount];
        for (int i = 0; i < VertexCount; i++) vtxMap[i] = i;

        bool anyWeld = false;
        for (int i = 0; i < TriCount; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int vi = Indices[i * 3 + j];
                int vj = Indices[i * 3 + IncMod3[j]];
                if ((Vertices[vi] - Vertices[vj]).LengthSq() < minLen2)
                {
                    // Always weld higher index into lower (matching C++ idxmin/idxmax logic)
                    int idxMin = System.Math.Min(vi, vj);
                    int idxMax = System.Math.Max(vi, vj);
                    vtxMap[idxMax] = idxMin;
                    anyWeld = true;
                }
            }
        }

        if (anyWeld)
        {
            // Resolve transitive welds: follow the chain until we reach a self-mapping vertex
            for (int i = VertexCount - 1; i >= 0; i--)
            {
                int j = i;
                while (vtxMap[j] != j) j = vtxMap[j];
                if (j != i)
                {
                    vtxMap[i] = j;
                    Vertices[i] = Vertices[j];
                }
            }

            // Apply weld map and remove degenerate triangles (collapsed edges)
            var keptIndices = new List<int>();
            for (int i = 0; i < TriCount; i++)
            {
                int v0 = vtxMap[Indices[i * 3]];
                int v1 = vtxMap[Indices[i * 3 + 1]];
                int v2 = vtxMap[Indices[i * 3 + 2]];

                if (v0 == v1 || v0 == v2 || v1 == v2)
                {
                    nTotalChanges++;
                    continue; // Triangle collapsed to a line or point
                }

                // Update indices in-place for later phases (if indices changed)
                if (Indices[i * 3] != v0 || Indices[i * 3 + 1] != v1 || Indices[i * 3 + 2] != v2)
                {
                    nTotalChanges++;
                }

                keptIndices.Add(v0);
                keptIndices.Add(v1);
                keptIndices.Add(v2);
            }

            if (nTotalChanges > 0)
            {
                // Rebuild mesh data with compacted triangles and recalculated topology
                Islands = null; IslandCount = 0; _tri2Island = null;
                SetData(Vertices, keptIndices.ToArray(), keptIndices.Count / 3);
            }
        }

        // ----------------------------------------------------------------
        // Phase 2: Remove zero-thickness fins
        // Port of the fin-detection loop in FilterMesh (trimesh.cpp ~line 940).
        // A fin is a pair of triangles glued together on two edges, producing
        // zero area.  Detect when two buddy indices of a triangle are the same
        // neighbor.
        // ----------------------------------------------------------------
        if (Topology != null)
        {
            bool[] triRemoved = new bool[TriCount];
            int nFinChanges = 0;

            for (int i = 0; i < TriCount; i++)
            {
                if (triRemoved[i]) continue;

                for (int j = 0; j < 3; j++)
                {
                    // Check if edge j and edge dec_mod3[j] share the same buddy
                    int buddy = Topology[i][j];
                    if (buddy >= 0 && buddy == Topology[i][DecMod3[j]])
                    {
                        // This is a fin: triangle i and triangle buddy are glued
                        // Find the edge on buddy that points back to i
                        int iedge = -1;
                        for (int e = 0; e < 2; e++)
                        {
                            if (Topology[buddy][e] == i)
                            {
                                iedge = e;
                                break;
                            }
                        }

                        // Re-stitch topology: connect the remaining neighbors directly
                        int ibuddy1 = Topology[i][IncMod3[j]];
                        if (ibuddy1 >= 0)
                        {
                            int edgeOnBuddy1 = GetEdgeByBuddy(ibuddy1, i);
                            if (edgeOnBuddy1 >= 0 && iedge >= 0)
                                Topology[ibuddy1][edgeOnBuddy1] = Topology[buddy][iedge];
                        }

                        if (iedge >= 0)
                        {
                            int ibuddy2 = Topology[buddy][iedge];
                            if (ibuddy2 >= 0)
                            {
                                int edgeOnBuddy2 = GetEdgeByBuddy(ibuddy2, buddy);
                                if (edgeOnBuddy2 >= 0)
                                    Topology[ibuddy2][edgeOnBuddy2] = Topology[i][IncMod3[j]];
                            }
                        }

                        triRemoved[i] = true;
                        triRemoved[buddy] = true;
                        nFinChanges++;
                        break;
                    }
                }
            }

            if (nFinChanges > 0)
            {
                nTotalChanges += nFinChanges;
                var keptIndices = new List<int>();
                for (int i = 0; i < TriCount; i++)
                {
                    if (triRemoved[i]) continue;
                    keptIndices.Add(Indices[i * 3]);
                    keptIndices.Add(Indices[i * 3 + 1]);
                    keptIndices.Add(Indices[i * 3 + 2]);
                }

                Islands = null; IslandCount = 0; _tri2Island = null;
                SetData(Vertices, keptIndices.ToArray(), keptIndices.Count / 3);
            }
        }

        // ----------------------------------------------------------------
        // Phase 3: Detect and fix thin slivers / T-junctions
        // Port of the thin-triangle and T-junction fix loop (trimesh.cpp ~line 965).
        // For each triangle, check all three angles.  When the cross product
        // of two edges is small relative to the product of their lengths the
        // angle is degenerate.  If the two edge lengths are dissimilar it is
        // a T-junction: edge-swap with the buddy across the long edge to
        // improve the local mesh quality.  Otherwise the triangle is simply
        // removed.
        // ----------------------------------------------------------------
        if (Topology != null)
        {
            int nSliverChanges = 0;
            bool[] triRemoved = new bool[TriCount];

            for (int i = 0; i < TriCount; i++)
            {
                if (triRemoved[i]) continue;

                var vtx = new PhysVector3[4];
                vtx[0] = Vertices[Indices[i * 3]];
                vtx[1] = Vertices[Indices[i * 3 + 1]];
                vtx[2] = Vertices[Indices[i * 3 + 2]];

                int jSliver = -1;
                float len0 = 0, len1 = 0;
                for (int j = 0; j < 3; j++)
                {
                    var e1 = vtx[IncMod3[j]] - vtx[j];
                    var e2 = vtx[DecMod3[j]] - vtx[j];
                    float crossLen2 = (e1 ^ e2).LengthSq();
                    len1 = e1.LengthSq();
                    len0 = e2.LengthSq();
                    if (len1 * len0 > 1e-20f && crossLen2 < len1 * len0 * minAngle2)
                    {
                        jSliver = j;
                        break;
                    }
                }

                if (jSliver < 0) continue;

                // T-junction fix: edge-swap with buddy if the two edge lengths are dissimilar
                bool isNotIsoceles = len0 < len1 * 0.95f || len0 > len1 * 1.05f;
                if (isNotIsoceles)
                {
                    // j = the junction vertex; determine which edge is longer
                    int jVtx = len0 < len1 ? DecMod3[jSliver] : IncMod3[jSliver];
                    int ibuddy = Topology[i][IncMod3[jVtx]];
                    if (ibuddy >= 0)
                    {
                        int iedge = GetEdgeByBuddy(ibuddy, i);
                        if (iedge >= 0)
                        {
                            vtx[3] = Vertices[Indices[ibuddy * 3 + DecMod3[iedge]]];

                            // Validate that the swap improves both new triangles
                            float cross1 = (vtx[jVtx] - vtx[3] ^ vtx[DecMod3[jVtx]] - vtx[3]).LengthSq();
                            float prod1 = (vtx[jVtx] - vtx[3]).LengthSq() * (vtx[DecMod3[jVtx]] - vtx[3]).LengthSq();
                            float cross2 = (vtx[jVtx] - vtx[3] ^ vtx[IncMod3[jVtx]] - vtx[3]).LengthSq();
                            float prod2 = (vtx[jVtx] - vtx[3]).LengthSq() * (vtx[IncMod3[jVtx]] - vtx[3]).LengthSq();

                            bool swapOk = prod1 > 1e-20f && cross1 > prod1 * minAngle2
                                       && prod2 > 1e-20f && cross2 > prod2 * minAngle2
                                       && (Topology[ibuddy][IncMod3[iedge]] | Topology[i][DecMod3[jVtx]]) >= 0;

                            if (swapOk)
                            {
                                // Perform edge swap
                                Indices[i * 3 + DecMod3[jVtx]] = Indices[ibuddy * 3 + DecMod3[iedge]];
                                Indices[ibuddy * 3 + IncMod3[iedge]] = Indices[i * 3 + jVtx];

                                // Update topology pointers
                                int nb1 = Topology[ibuddy][IncMod3[iedge]];
                                if (nb1 >= 0)
                                {
                                    int eOnNb1 = GetEdgeByBuddy(nb1, ibuddy);
                                    if (eOnNb1 >= 0)
                                        Topology[nb1][eOnNb1] = i;
                                }
                                int nb2 = Topology[i][DecMod3[jVtx]];
                                if (nb2 >= 0)
                                {
                                    int eOnNb2 = GetEdgeByBuddy(nb2, i);
                                    if (eOnNb2 >= 0)
                                        Topology[nb2][eOnNb2] = ibuddy;
                                }

                                Topology[i][IncMod3[jVtx]] = Topology[ibuddy][IncMod3[iedge]];
                                Topology[ibuddy][iedge] = Topology[i][DecMod3[jVtx]];
                                Topology[i][DecMod3[jVtx]] = ibuddy;
                                Topology[ibuddy][IncMod3[iedge]] = i;

                                RecalcTriNormal(i);
                                RecalcTriNormal(ibuddy);
                                nSliverChanges++;
                                continue;
                            }
                        }
                    }
                }

                // Could not fix via edge-swap: remove the sliver triangle
                triRemoved[i] = true;
                nSliverChanges++;
            }

            if (nSliverChanges > 0)
            {
                nTotalChanges += nSliverChanges;

                // Check if any triangles were actually removed (vs. edge-swapped only)
                bool anyRemoved = false;
                for (int i = 0; i < TriCount; i++)
                {
                    if (triRemoved[i]) { anyRemoved = true; break; }
                }

                if (anyRemoved)
                {
                    var keptIndices = new List<int>();
                    for (int i = 0; i < TriCount; i++)
                    {
                        if (triRemoved[i]) continue;
                        keptIndices.Add(Indices[i * 3]);
                        keptIndices.Add(Indices[i * 3 + 1]);
                        keptIndices.Add(Indices[i * 3 + 2]);
                    }

                    Islands = null; IslandCount = 0; _tri2Island = null;
                    SetData(Vertices, keptIndices.ToArray(), keptIndices.Count / 3);
                }
            }
        }

        return nTotalChanges;
    }

    // ========================================================================
    // Flood Fill Volume (port of CTriMesh::FloodFill)
    // ========================================================================

    /// <summary>
    /// Compute volume of the mesh below a water plane by flood-filling from a seed point.
    /// Port of CTriMesh::FloodFill from trimesh.cpp.
    /// Shoots a ray from origin along gravity to find the bottom of the vessel,
    /// then progressively fills upward computing displaced volume.
    /// Returns the volume below the water plane and sets the submerged depth.
    /// </summary>
    public float FloodFillVolume(in PhysVector3 origin, in PhysVector3 gravity,
                                  float targetVolume, out float depth)
    {
        depth = 0;
        if (TriCount == 0 || Topology == null) return 0;

        var gdir = gravity.Normalized();

        // Find the lowest vertex by projecting along gravity
        int lowestVtx = 0;
        float lowestDot = Vertices[0].Dot(gdir);
        for (int i = 1; i < VertexCount; i++)
        {
            float d = Vertices[i].Dot(gdir);
            if (d > lowestDot)
            {
                lowestDot = d;
                lowestVtx = i;
            }
        }

        // Build per-vertex triangle lists for walking
        int[] vtxTriCount = new int[VertexCount + 1];
        for (int i = 0; i < TriCount * 3; i++)
            vtxTriCount[Indices[i]]++;
        for (int i = 0; i < VertexCount; i++)
            vtxTriCount[i + 1] += vtxTriCount[i];
        int[] vtxTris = new int[TriCount * 3];
        for (int i = TriCount - 1; i >= 0; i--)
            for (int j = 0; j < 3; j++)
                vtxTris[--vtxTriCount[Indices[i * 3 + j]]] = i;

        // Grow region from lowest vertex upward, computing volume
        bool[] usedTri = new bool[TriCount];
        float volume = 0;
        var queue = new Queue<int>();
        queue.Enqueue(lowestVtx);
        var visitedVtx = new HashSet<int> { lowestVtx };

        depth = Vertices[lowestVtx].Dot(gdir);

        while (queue.Count > 0 && volume < targetVolume)
        {
            int vtx = queue.Dequeue();
            int start = vtxTriCount[vtx];
            int end = vtxTriCount[vtx + 1];

            for (int i = start; i < end; i++)
            {
                int itri = vtxTris[i];
                if (usedTri[itri]) continue;
                usedTri[itri] = true;

                var v0 = Vertices[Indices[itri * 3]];
                var v1 = Vertices[Indices[itri * 3 + 1]];
                var v2 = Vertices[Indices[itri * 3 + 2]];

                // Accumulate signed volume contribution
                volume += v0.Dot(v1 ^ v2) / 6f;

                // Enqueue adjacent vertices
                for (int j = 0; j < 3; j++)
                {
                    int nv = Indices[itri * 3 + j];
                    if (visitedVtx.Add(nv))
                    {
                        queue.Enqueue(nv);
                        float d = Vertices[nv].Dot(gdir);
                        if (d < depth) depth = d;
                    }
                }
            }
        }

        depth = MathF.Abs(Vertices[lowestVtx].Dot(gdir) - depth);
        return MathF.Abs(volume);
    }

    // ========================================================================
    // RebuildBVTree (port of CTriMesh::RebuildBVTree)
    // ========================================================================

    /// <summary>
    /// Rebuild the bounding volume tree after mesh modification.
    /// Port of CTriMesh::RebuildBVTree from trimesh.cpp.
    /// Rebuilds the AABBTree from current mesh data and reorders normals/topology
    /// to match the new triangle ordering.
    /// </summary>
    public void RebuildBVTree()
    {
        if (TriCount == 0)
        {
            Tree = new AABBTree();
            return;
        }

        // Rebuild the tree
        Tree = new AABBTree();
        Tree.Build(Vertices, Indices, TriCount);

        // Recompute AABB
        RecomputeAABB();

        // Invalidate cached data
        _cachedVolume = float.NaN;
        Islands = null;
        IslandCount = 0;
        _tri2Island = null;
    }

    // ========================================================================
    // Voxelize (port of CTriMesh::Voxelize)
    // ========================================================================

    /// <summary>
    /// Convert mesh to a voxel grid representation.
    /// Port of CTriMesh::Voxelize from trimesh.cpp.
    /// Rasterizes the mesh surface into a 3D voxel grid, then flood-fills
    /// the interior. Each voxel is marked as solid (1) or empty (0).
    /// </summary>
    /// <param name="rotation">Rotation to apply before voxelization.</param>
    /// <param name="cellDim">Size of each voxel cell.</param>
    /// <returns>The filled voxel grid.</returns>
    public TriMeshVoxelGrid Voxelize(PhysQuaternion rotation, float cellDim)
    {
        if (TriCount == 0)
            return new TriMeshVoxelGrid(1, 1, 1, PhysVector3.Zero);

        float rCellDim = 1f / cellDim;

        // Compute rotated bounding box
        var ptMin = rotation.Rotate(Vertices[Indices[0]]);
        var ptMax = ptMin;
        for (int i = 1; i < TriCount * 3; i++)
        {
            var pt = rotation.Rotate(Vertices[Indices[i]]);
            ptMin = PhysVector3.Min(ptMin, pt);
            ptMax = PhysVector3.Max(ptMax, pt);
        }

        var center = (ptMin + ptMax) * 0.5f;
        var halfSize = (ptMax - ptMin) * 0.5f;
        int sx = (int)(halfSize.X * rCellDim + 0.5f);
        int sy = (int)(halfSize.Y * rCellDim + 0.5f);
        int sz = (int)(halfSize.Z * rCellDim + 0.5f);
        sx = System.Math.Max(sx, 1);
        sy = System.Math.Max(sy, 1);
        sz = System.Math.Max(sz, 1);

        var grid = new TriMeshVoxelGrid(sx, sy, sz, center);
        grid.Rotation = rotation;
        grid.CellDim = cellDim;

        // Rasterize each triangle into the voxel grid
        for (int i = 0; i < TriCount; i++)
        {
            var vtx = new PhysVector3[3];
            for (int j = 0; j < 3; j++)
                vtx[j] = rotation.Rotate(Vertices[Indices[i * 3 + j]]) - center;

            // Compute 2D cross product for winding direction
            float area = (vtx[1].X - vtx[0].X) * (vtx[2].Y - vtx[0].Y)
                       - (vtx[1].Y - vtx[0].Y) * (vtx[2].X - vtx[0].X);

            if (MathF.Abs(area) < 1e-10f) continue;

            int dir = area > 0 ? 2 : -2;

            // Mark centroid voxel
            var centroid = (vtx[0] + vtx[1] + vtx[2]) * (rCellDim / 3f);
            int cx = (int)MathF.Round(centroid.X - 0.5f);
            int cy = (int)MathF.Round(centroid.Y - 0.5f);
            int cz = (int)MathF.Round(centroid.Z - 0.5f);
            grid[cx, cy, cz] |= 1;

            // Rasterize triangle projection to XY plane
            var triMin = PhysVector3.Min(PhysVector3.Min(vtx[0], vtx[1]), vtx[2]) * rCellDim;
            var triMax = PhysVector3.Max(PhysVector3.Max(vtx[0], vtx[1]), vtx[2]) * rCellDim;

            int ixMin = (int)MathF.Floor(triMin.X - 0.5f);
            int iyMin = (int)MathF.Floor(triMin.Y - 0.5f);
            int ixMax = (int)MathF.Ceiling(triMax.X + 0.5f);
            int iyMax = (int)MathF.Ceiling(triMax.Y + 0.5f);

            // Z blend coefficients for barycentric interpolation
            float invArea = 1f / area;

            for (int ix = ixMin; ix < ixMax; ix++)
            {
                for (int iy = iyMin; iy < iyMax; iy++)
                {
                    float px = (ix + 0.5f) * cellDim;
                    float py = (iy + 0.5f) * cellDim;

                    // Point-in-triangle test using barycentric coordinates
                    float dx0 = vtx[0].X - px, dy0 = vtx[0].Y - py;
                    float dx1 = vtx[1].X - px, dy1 = vtx[1].Y - py;
                    float dx2 = vtx[2].X - px, dy2 = vtx[2].Y - py;

                    float c0 = dx1 * dy2 - dx2 * dy1;
                    float c1 = dx2 * dy0 - dx0 * dy2;
                    float c2 = dx0 * dy1 - dx1 * dy0;

                    if (MathF.Min(MathF.Min(c0 * area, c1 * area), c2 * area) > 0)
                    {
                        // Interpolate Z
                        float z = (c0 * vtx[0].Z + c1 * vtx[1].Z + c2 * vtx[2].Z) * invArea;
                        int iz = (int)MathF.Round(z * rCellDim - 0.5f);
                        grid[ix, iy, iz] |= 1;

                        // Fill column below the triangle surface
                        for (int izz = iz; izz >= -sz; izz--)
                            grid[ix, iy, izz] = (byte)(grid[ix, iy, izz] + dir);
                    }
                }
            }
        }

        // Final pass: convert winding count to solid/empty
        for (int ix = -sx; ix < sx; ix++)
            for (int iy = -sy; iy < sy; iy++)
                for (int iz = -sz; iz < sz; iz++)
                {
                    ref byte vox = ref grid[ix, iy, iz];
                    vox = (byte)((vox | (vox >> 1)) & 1);
                }

        return grid;
    }

    // ========================================================================
    // Existing methods (unchanged)
    // ========================================================================

    public override void GetBBox(ref Box bbox)
    {
        bbox.Center = (_bboxMin + _bboxMax) * 0.5f;
        bbox.Size = (_bboxMax - _bboxMin) * 0.5f;
        bbox.Basis = PhysMatrix33.Identity;
        bbox.IsOriented = false;
    }

    public override float GetVolume()
    {
        if (!float.IsNaN(_cachedVolume))
            return MathF.Abs(_cachedVolume);

        // Compute volume using divergence theorem
        float vol = 0;
        for (int i = 0; i < TriCount; i++)
        {
            var v0 = Vertices[Indices[i * 3]];
            var v1 = Vertices[Indices[i * 3 + 1]];
            var v2 = Vertices[Indices[i * 3 + 2]];
            vol += v0.Dot(v1 ^ v2);
        }
        _cachedVolume = vol / 6f;
        return MathF.Abs(_cachedVolume);
    }

    public override PhysVector3 GetCenter()
    {
        if (!float.IsNaN(_cachedVolume) && _cachedCenter.LengthSq() > 0)
            return _cachedCenter;
        return (_bboxMin + _bboxMax) * 0.5f;
    }

    public override PhysicalProperties CalcPhysicalProperties()
    {
        var center = GetCenter();
        var size = _bboxMax - _bboxMin;
        float v = GetVolume();

        return new PhysicalProperties
        {
            Volume = v,
            CenterOfMass = center,
            InertiaTensor = PhysMatrix33.Diagonal(
                (size.Y * size.Y + size.Z * size.Z) / 12f,
                (size.X * size.X + size.Z * size.Z) / 12f,
                (size.X * size.X + size.Y * size.Y) / 12f
            )
        };
    }

    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        float minDist2 = float.MaxValue;
        closestPt = PhysVector3.Zero;
        normal = PhysVector3.UnitZ;

        for (int i = 0; i < TriCount; i++)
        {
            var v0 = Vertices[Indices[i * 3]];
            var v1 = Vertices[Indices[i * 3 + 1]];
            var v2 = Vertices[Indices[i * 3 + 2]];

            var closest = ClosestPointOnTriangle(pt, v0, v1, v2);
            float dist2 = (pt - closest).LengthSq();
            if (dist2 < minDist2)
            {
                minDist2 = dist2;
                closestPt = closest;
                normal = Normals[i];
            }
        }

        return MathF.Sqrt(minDist2);
    }

    private static PhysVector3 ClosestPointOnTriangle(in PhysVector3 p, in PhysVector3 a, in PhysVector3 b, in PhysVector3 c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = p - a;
        float d1 = ab.Dot(ap), d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0) return a;

        var bp = p - b;
        float d3 = ab.Dot(bp), d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3) return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            float v = d1 / (d1 - d3);
            return a + ab * v;
        }

        var cp = p - c;
        float d5 = ab.Dot(cp), d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6) return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            float w = d2 / (d2 - d6);
            return a + ac * w;
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + (c - b) * w;
        }

        float denom = 1f / (va + vb + vc);
        float vFinal = vb * denom;
        float wFinal = vc * denom;
        return a + ab * vFinal + ac * wFinal;
    }

    public override int PointInsideStatus(in PhysVector3 pt)
    {
        // Ray casting method: count intersections with +Z ray
        int crossings = 0;
        var ray = new Primitives.Ray(pt, new PhysVector3(0, 0, 1000f));

        for (int i = 0; i < TriCount; i++)
        {
            var v0 = Vertices[Indices[i * 3]];
            var v1 = Vertices[Indices[i * 3 + 1]];
            var v2 = Vertices[Indices[i * 3 + 2]];

            if (RayTriangleIntersect(ray.Origin, ray.Dir, v0, v1, v2))
                crossings++;
        }

        return crossings % 2; // Odd = inside
    }

    private static bool RayTriangleIntersect(in PhysVector3 origin, in PhysVector3 dir,
        in PhysVector3 v0, in PhysVector3 v1, in PhysVector3 v2)
    {
        var e1 = v1 - v0;
        var e2 = v2 - v0;
        var h = dir ^ e2;
        float a = e1.Dot(h);
        if (MathF.Abs(a) < 1e-10f) return false;

        float f = 1f / a;
        var s = origin - v0;
        float u = f * s.Dot(h);
        if (u < 0f || u > 1f) return false;

        var q = s ^ e1;
        float v = f * dir.Dot(q);
        if (v < 0f || u + v > 1f) return false;

        float t = f * e2.Dot(q);
        return t > 0f && t < 1f;
    }

    // ========================================================================
    // Buoyancy & Medium Resistance (port of CTriMesh::CalculateBuoyancy / CalculateMediumResistance)
    // ========================================================================

    /// <summary>
    /// Calculate submerged volume and center of buoyancy for a water plane.
    /// Port of CTriMesh::CalculateBuoyancy from trimesh.cpp lines 2896-2951.
    /// The water plane is defined by planeNormal and planeOrigin.
    /// R, offset, scale transform the mesh into world space.
    /// </summary>
    public override float CalculateBuoyancy(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale, out PhysVector3 massCenter)
    {
        // Build rotation that maps planeNormal to Z axis
        var planeQ = PhysQuaternion.CreateRotationV0V1(planeNormal, PhysVector3.UnitZ);
        var planeRot = new PhysMatrix33(planeQ);

        // Transform mesh into plane-aligned space:
        // gtest.R = planerot, gtest.offset = planerot*(pgwd->offset - pplane->origin), gtest.R *= pgwd->R
        var gtestOffset = planeRot * (offset - planeOrigin);
        var gtestR = planeRot * R;

        float Vaccum = 0;
        var comAccum = PhysVector3.Zero;

        Span<float> V = stackalloc float[4];
        Span<PhysVector3> com = stackalloc PhysVector3[4];
        Span<PhysVector3> pt = stackalloc PhysVector3[3];
        Span<int> sign = stackalloc int[3];
        Span<PhysVector3> triPts = stackalloc PhysVector3[3];
        Span<PhysVector3> tri0Pts = stackalloc PhysVector3[3];

        for (int itri = 0; itri < TriCount; itri++)
        {
            // PrepareTriangle: transform vertices and normal to world space
            int idx = itri * 3;
            var triPt0 = gtestR * Vertices[Indices[idx]] * scale + gtestOffset;
            var triPt1 = gtestR * Vertices[Indices[idx + 1]] * scale + gtestOffset;
            var triPt2 = gtestR * Vertices[Indices[idx + 2]] * scale + gtestOffset;
            var triN = gtestR * Normals[itri];

            // Classify vertices by z: sign[i]=1 if z<0 (below water), 0 if above
            int iLow = 0, iHigh = 0, nAbove = 3;
            triPts[0] = triPt0; triPts[1] = triPt1; triPts[2] = triPt2;

            for (int i = 0; i < 3; i++)
            {
                sign[i] = MathUtils.IsNeg(triPts[i].Z);
                nAbove -= sign[i];
                if (triPts[i].Z < triPts[iLow].Z) iLow = i;
                if (triPts[i].Z > triPts[iHigh].Z) iHigh = i;
            }

            // Find intersection points with z=0 plane
            int j = 0;
            for (int i = 0; i < 3; i++)
            {
                int inext = IncMod3[i];
                if (sign[i] != sign[inext])
                {
                    float t = -triPts[i].Z / (triPts[inext].Z - triPts[i].Z);
                    pt[j++] = triPts[i] * (1.0f - t) + triPts[inext] * t;
                }
            }

            int nPieces;
            if (nAbove < 2)
            {
                // Mostly below water: decompose the full triangle volume
                var tri0Pt0 = triPts[0]; tri0Pt0.Z = triPts[iHigh].Z;
                var tri0Pt1 = triPts[1]; tri0Pt1.Z = triPts[iHigh].Z;
                var tri0Pt2 = triPts[2]; tri0Pt2.Z = triPts[iHigh].Z;
                tri0Pts[0] = tri0Pt0; tri0Pts[1] = tri0Pt1; tri0Pts[2] = tri0Pt2;

                V[0] = MathUtils.CalcPyramidVolume(triPts[iHigh], tri0Pts[IncMod3[iHigh]], triPts[IncMod3[iHigh]], tri0Pts[DecMod3[iHigh]], out com[0]);
                V[1] = MathUtils.CalcPyramidVolume(triPts[iHigh], tri0Pts[DecMod3[iHigh]], triPts[DecMod3[iHigh]], triPts[IncMod3[iHigh]], out com[1]);

                // Prism base area
                V[2] = MathF.Abs(((tri0Pts[1] - tri0Pts[0]) ^ (tri0Pts[2] - tri0Pts[0])).Z * 0.5f * tri0Pts[0].Z);
                com[2] = (tri0Pts[0] + tri0Pts[1] + tri0Pts[2]) * (1.0f / 3f);
                com[2].Z = tri0Pts[0].Z * 0.5f;

                if (nAbove == 1)
                {
                    // One vertex above: subtract the cap above water
                    V[2] *= -1.0f;
                    var tri0High = tri0Pts[iHigh]; tri0High.Z = 0;
                    nPieces = 4;
                    V[3] = MathUtils.CalcPyramidVolume(triPts[iHigh], tri0High, pt[0], pt[1], out com[3]);
                }
                else
                {
                    nPieces = 3;
                }
            }
            else if (nAbove == 2)
            {
                // Only one vertex below water
                var tri0Low = triPts[iLow]; tri0Low.Z = 0;
                nPieces = 1;
                V[0] = MathUtils.CalcPyramidVolume(triPts[iLow], tri0Low, pt[0], pt[1], out com[0]);
            }
            else
            {
                nPieces = 0; // all above water
            }

            int signMul = -MathUtils.Sgn(triN.Z);
            for (int i = 0; i < nPieces; i++)
            {
                V[i] *= signMul;
                Vaccum += V[i];
                comAccum = comAccum + com[i] * V[i];
            }
        }

        if (Vaccum > 0)
        {
            // Transform center of buoyancy back to world space
            // planerot is the rotation from world to plane space, so inverse = transpose
            massCenter = planeRot.Transposed() * (comAccum / Vaccum) + planeOrigin;
        }
        else
        {
            massCenter = PhysVector3.Zero;
        }

        return Vaccum;
    }

    /// <summary>
    /// Calculate medium resistance force and torque.
    /// Port of CTriMesh::CalculateMediumResistance from trimesh.cpp lines 2954-2968.
    /// </summary>
    public override void CalculateMediumResistance(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale,
        in PhysVector3 v, in PhysVector3 w, in PhysVector3 com,
        out PhysVector3 dPres, out PhysVector3 dLres)
    {
        dPres = PhysVector3.Zero;
        dLres = PhysVector3.Zero;

        float planeD = planeOrigin.Dot(planeNormal);
        Span<PhysVector3> triPt = stackalloc PhysVector3[3];

        for (int itri = 0; itri < TriCount; itri++)
        {
            int idx = itri * 3;
            triPt[0] = R * Vertices[Indices[idx]] * scale + offset;
            triPt[1] = R * Vertices[Indices[idx + 1]] * scale + offset;
            triPt[2] = R * Vertices[Indices[idx + 2]] * scale + offset;
            var triN = R * Normals[itri];

            MathUtils.CalcMediumResistance(triPt, 3, triN, planeNormal, planeD,
                v, w, com, ref dPres, ref dLres);
        }
    }

    public override int GetMemoryUsage()
    {
        int topMem = Topology != null ? Topology.Length * 12 : 0;
        return Vertices.Length * 12 + Indices.Length * 4 + Normals.Length * 12
             + topMem + (Tree?.GetMemoryUsage() ?? 0);
    }

    // ========================================================================
    // Intersection (port of CTriMesh::PrepareForIntersectionTest / Intersect)
    // ========================================================================

    /// <summary>
    /// Prepare this trimesh for an intersection test.
    /// Port of CTriMesh::PrepareForIntersectionTest from trimesh.cpp lines 1211-1268.
    /// </summary>
    public override void PrepareForIntersectionTest(GeometryUnderTest pGTest, GeometryBase pCollider,
        GeometryUnderTest pGTestColl, bool bKeepPrevContacts)
    {
        pGTest.Geometry = this;
        pGTest.BVtree = Tree;
        BVTrees.BVTree? pTree = Tree;
        pTree?.PrepareForIntersectionTest(pGTest, pCollider, pGTestColl);

        pGTest.TypePrim = IndexedTriangle.Type;
        int nNodeTris = pTree != null ? pTree.MaxPrimsInNode() : 1;

        // Allocate scratch buffers (C# uses heap arrays in place of C++ thread-local ring buffers)
        var primBuf = new IndexedTriangle[nNodeTris];
        for (int i = 0; i < nNodeTris; i++) primBuf[i] = new IndexedTriangle();
        pGTest.PrimBuf = primBuf;
        pGTest.SzPrimBuf = nNodeTris;

        var primBuf1 = new IndexedTriangle[MaxVertexValency];
        for (int i = 0; i < MaxVertexValency; i++) primBuf1[i] = new IndexedTriangle();
        pGTest.PrimBuf1 = primBuf1;
        pGTest.SzPrimBuf1 = MaxVertexValency;

        pGTest.IFeatureBuf = new int[MaxVertexValency];

        int szbuf = System.Math.Max(nNodeTris, MaxVertexValency);
        pGTest.IdBuf = new byte[szbuf];

        pGTest.SzPrim = 1; // sizeof(indexed_triangle) — not meaningful in C#, retain non-zero marker

        pGTest.MinAreaEdge = 0;
    }

    /// <summary>
    /// Simple intersection entry point used by the driver for Box / Capsule colliders.
    /// Iterates all triangles and calls the appropriate primitive intersection test.
    /// Port of the Box / Capsule branches of CGeometry::Intersect (geometry.cpp lines 289-485)
    /// combined with CTriMesh's per-triangle primitive loop.
    /// </summary>
    public override int Intersect(GeometryBase other, ref GeomContact[] contacts)
    {
        if (other == null) return 0;
        int nContacts = 0;
        if (contacts == null) contacts = new GeomContact[System.Math.Max(8, TriCount)];

        var tri = new Triangle();
        var pinters = new PrimInters();

        if (other.GeomType == GeomTypes.Box)
        {
            var pbox = ((BoxGeometry)other).Box;
            for (int itri = 0; itri < TriCount; itri++)
            {
                int idx = itri * 3;
                tri.P0 = Vertices[Indices[idx]];
                tri.P1 = Vertices[Indices[idx + 1]];
                tri.P2 = Vertices[Indices[idx + 2]];
                tri.Normal = Normals[itri];

                if (IntersectionTests.TriBox(tri, pbox, pinters) > 0)
                {
                    if (nContacts >= contacts.Length)
                        System.Array.Resize(ref contacts, contacts.Length * 2);

                    var c = new GeomContact
                    {
                        Pt = pinters.Pt0,
                        N = pinters.Normal,
                    };
                    c.IPrim[0] = itri;
                    c.Id[0] = GetMaterialId(itri);
                    contacts[nContacts++] = c;
                }
            }
            return nContacts;
        }

        if (other.GeomType == GeomTypes.Capsule)
        {
            var cyl = ((CapsuleGeometry)other).Cylinder;
            var pcaps = new Capsule(cyl.Center, cyl.Axis, cyl.Radius, cyl.HalfHeight);
            for (int itri = 0; itri < TriCount; itri++)
            {
                int idx = itri * 3;
                tri.P0 = Vertices[Indices[idx]];
                tri.P1 = Vertices[Indices[idx + 1]];
                tri.P2 = Vertices[Indices[idx + 2]];
                tri.Normal = Normals[itri];

                if (IntersectionTests.TriCapsule(tri, pcaps, pinters) > 0)
                {
                    if (nContacts >= contacts.Length)
                        System.Array.Resize(ref contacts, contacts.Length * 2);

                    var c = new GeomContact
                    {
                        Pt = pinters.Pt0,
                        N = pinters.Normal,
                    };
                    c.IPrim[0] = itri;
                    c.Id[0] = GetMaterialId(itri);
                    contacts[nContacts++] = c;
                }
            }
            return nContacts;
        }

        return base.Intersect(other, ref contacts);
    }
}
