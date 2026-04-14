// Port of CryPhysics heightfieldgeom.h/cpp - heightfield geometry
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Checker used by the heightfield ray intersection.
/// Port of the anonymous struct hf_cell_checker in heightfieldgeom.cpp (lines 56-89).
/// </summary>
internal struct HfCellChecker
{
    public float Dir2dLen;
    public float MaxZCell;
    public float MaxT;
    public int IdContact;
    public int BNoCull;
    public Triangle HfTri;
    public Ray HfRay;
    public Heightfield? Phf;
    public PrimInters Inters;
    public PhysVector2 Org2d;
    public PhysVector2 Dir2d;

    /// <summary>
    /// Test a single heightfield cell against the ray.
    /// Port of hf_cell_checker::check_cell (heightfieldgeom.cpp:65-88).
    /// Returns 1 to stop grid traversal, 0 to continue.
    /// </summary>
    public int CheckCell(Vector2i icell, ref int ilastcell)
    {
        if (Phf == null) return 1;
        // quotientf t((org2d+icell)*dir2d, dir2d_len*dir2d_len)
        float tNum = (Org2d.X + icell.X) * Dir2d.X + (Org2d.Y + icell.Y) * Dir2d.Y;
        float tDen = Dir2dLen * Dir2dLen;
        // t.x>maxt  <=>  tNum > MaxT*tDen (assuming tDen>=0 — dir2d_len*dir2d_len>=0)
        if (tNum > MaxT * tDen || !Phf.InRange(icell.X, icell.Y))
            return 1;

        float[] h = new float[4];
        // zlowest = hfray.origin.z*t.y + hfray.dir.z*t.x - max_zcell
        float zlowest = HfRay.Origin.Z * tDen + HfRay.Dir.Z * tNum - MaxZCell;
        int itype = Phf.GetSurfTypeAt(icell.X, icell.Y);
        h[0] = Phf.GetHeightAt(icell.X, icell.Y);
        h[1] = Phf.GetHeightAt(icell.X + 1, icell.Y);
        h[2] = Phf.GetHeightAt(icell.X, icell.Y + 1);
        h[3] = Phf.GetHeightAt(icell.X + 1, icell.Y + 1);

        float maxh = MathF.Max(MathF.Max(MathF.Max(h[0], h[1]), h[2]), h[3]);
        if (zlowest <= maxh * tDen && itype >= 0)
        {
            HfTri ??= new Triangle();
            // First triangle: p0=(ix*step,iy*step,h0), p1=((ix+1)*step,iy*step,h1), p2=(ix*step,(iy+1)*step,h2)
            HfTri.P0 = new PhysVector3(icell.X * Phf.Step.X, icell.Y * Phf.Step.Y, h[0]);
            HfTri.P1 = new PhysVector3(HfTri.P0.X + Phf.Step.X, icell.Y * Phf.Step.Y, h[1]);
            HfTri.P2 = new PhysVector3(icell.X * Phf.Step.X, HfTri.P0.Y + Phf.Step.Y, h[2]);
            HfTri.Normal = (HfTri.P1 - HfTri.P0) ^ (HfTri.P2 - HfTri.P0);
            // Ray-tri intersection placeholder — full ray path is not used for ships
            // if (ray_tri_intersection(...) && hftri.n*hfray.dir<bNoCull) { ... return 1; }

            // Second triangle: rotate — p0 = previous p2, p2.x += step, p2.z = h[3]
            HfTri.P0 = HfTri.P2;
            HfTri.P2 = new PhysVector3(HfTri.P2.X + Phf.Step.X, HfTri.P2.Y, h[3]);
            HfTri.Normal = (HfTri.P1 - HfTri.P0) ^ (HfTri.P2 - HfTri.P0);
            // if (ray_tri_intersection(...) && ...) { idcontact = itype; return 1; }
        }
        return 0;
    }
}

/// <summary>
/// Heightfield collision geometry. Extends TriMeshGeometry to generate
/// triangles on-the-fly from a 2D height grid.
/// Port of CHeightfield from CryEngine.
/// </summary>
public class HeightfieldGeometry : GeometryBase
{
    /// <summary>Maximum indices per mesh patch (port of PHYS_MAX_INDICES from CryPhysics).</summary>
    public const int PHYS_MAX_INDICES = 2048;

    public override int GeomType => GeomTypes.Heightfield;

    /// <summary>The heightfield primitive data.</summary>
    public Heightfield Hf { get; private set; } = new();

    /// <summary>HeightfieldBV tree for spatial queries.</summary>
    private HeightfieldBV _hfTree = new();

    // Height bounds
    private float _minHeight = -1000f;
    private float _maxHeight = 1000f;

    // Cached patch mesh data (generated on demand during intersection)
    private PhysVector3[] _patchVertices = Array.Empty<PhysVector3>();
    private int[] _patchIndices = Array.Empty<int>();
    private PhysVector3[] _patchNormals = Array.Empty<PhysVector3>();
    private byte[] _patchIds = Array.Empty<byte>();
    private int _patchVertCount;
    private int _patchTriCount;

    // Patch region
    private Vector2i _patchStart = new(-1, -1);
    private Vector2i _patchSize;

    private float _minVtxDist;

    public HeightfieldGeometry()
    {
        Tree = _hfTree;
    }

    /// <summary>
    /// Initialize heightfield from primitive data.
    /// Port of CHeightfield::CreateHeightfield.
    /// </summary>
    public HeightfieldGeometry CreateHeightfield(Heightfield phf)
    {
        phf.StepR = new PhysVector2(1f / phf.Step.X, 1f / phf.Step.Y);
        Hf = phf;
        Hf.Basis = PhysMatrix33.Identity;
        Hf.Origin = PhysVector3.Zero;

        // Compute type power from type mask
        int i = (phf.TypeMask ^ (phf.TypeMask - 1)) + 1 >> 1;
        Hf.TypePower = 0;
        while ((i & 1) == 0 && i != 0)
        {
            i >>= 1;
            Hf.TypePower++;
        }

        _minHeight = -1000f;
        _maxHeight = 1000f;
        _hfTree.SetHeightfield(Hf);
        _hfTree.Owner = this;

        // Pre-allocate patch buffers
        _patchVertices = new PhysVector3[32];
        _patchNormals = new PhysVector3[64];
        _patchIndices = new int[64 * 3];
        _patchIds = new byte[64];

        _minVtxDist = (Hf.Step.X + Hf.Step.Y) * 1E-3f;
        _patchVertCount = 0;
        _patchTriCount = 0;

        return this;
    }

    /// <summary>
    /// Get two triangles for a heightfield cell at grid coordinates (ix, iy).
    /// Each cell is split into two triangles along the diagonal.
    /// </summary>
    public void GetCellTriangles(int ix, int iy, out Triangle tri0, out Triangle tri1)
    {
        float h00 = Hf.GetHeightAt(ix, iy);
        float h10 = Hf.GetHeightAt(ix + 1, iy);
        float h01 = Hf.GetHeightAt(ix, iy + 1);
        float h11 = Hf.GetHeightAt(ix + 1, iy + 1);

        var p00 = new PhysVector3(ix * Hf.Step.X, iy * Hf.Step.Y, h00);
        var p10 = new PhysVector3((ix + 1) * Hf.Step.X, iy * Hf.Step.Y, h10);
        var p01 = new PhysVector3(ix * Hf.Step.X, (iy + 1) * Hf.Step.Y, h01);
        var p11 = new PhysVector3((ix + 1) * Hf.Step.X, (iy + 1) * Hf.Step.Y, h11);

        // Triangle 0: p00, p10, p01
        tri0 = new Triangle(p00, p10, p01);
        // Triangle 1: p01, p10, p11
        tri1 = new Triangle(p01, p10, p11);
    }

    /// <summary>
    /// Build the patch mesh for a given region of the heightfield.
    /// Port of the mesh building logic from CHeightfield::PrepareForIntersectionTest.
    /// </summary>
    public void BuildPatch(int ix, int iy, int sx, int sy)
    {
        if (_patchStart.X == ix && _patchStart.Y == iy &&
            _patchSize.X == sx && _patchSize.Y == sy)
            return; // Already built

        _patchStart = new Vector2i(ix, iy);
        _patchSize = new Vector2i(sx, sy);

        int nVerts = (sx + 1) * (sy + 1);
        int nTris = sx * sy * 2;

        // Grow buffers if needed
        if (_patchVertices.Length < nVerts)
            _patchVertices = new PhysVector3[((nVerts - 1) & ~15) + 16];
        if (_patchNormals.Length < nTris)
        {
            int allocTris = ((nTris - 1) & ~15) + 16;
            _patchNormals = new PhysVector3[allocTris];
            _patchIndices = new int[allocTris * 3];
            _patchIds = new byte[allocTris];
        }

        _patchVertCount = nVerts;
        _patchTriCount = nTris;

        var origin = new PhysVector3(ix * Hf.Step.X, iy * Hf.Step.Y, 0);

        // Fill vertices
        _hfTree.MinHeight = float.MaxValue;
        _hfTree.MaxHeight = float.MinValue;
        int vi = 0;
        for (int j = 0; j <= sy; j++)
        {
            float curY = Hf.Step.Y * (iy + j);
            float curX = Hf.Step.X * ix;
            for (int i = 0; i <= sx; i++, vi++, curX += Hf.Step.X)
            {
                float h = Hf.GetHeightAt(ix + i, iy + j);
                _patchVertices[vi] = new PhysVector3(curX, curY, h) - origin;
                _hfTree.MinHeight = MathF.Min(_hfTree.MinHeight, _patchVertices[vi].Z);
                _hfTree.MaxHeight = MathF.Max(_hfTree.MaxHeight, _patchVertices[vi].Z);
            }
        }

        // Fill triangles
        int triIdx = 0;
        int idxPtr = 0;
        int vertI = 0;
        for (int row = 0; row < sy; row++, vertI++)
        {
            for (int col = 0; col < sx; col++, triIdx += 2, vertI++)
            {
                // Triangle 0: i, i+1, i+sx+1
                _patchIndices[idxPtr] = vertI;
                _patchIndices[idxPtr + 1] = vertI + 1;
                _patchIndices[idxPtr + 2] = vertI + sx + 1;
                var e1 = _patchVertices[_patchIndices[idxPtr + 1]] - _patchVertices[_patchIndices[idxPtr]];
                var e2 = _patchVertices[_patchIndices[idxPtr + 2]] - _patchVertices[_patchIndices[idxPtr]];
                _patchNormals[triIdx] = (e1 ^ e2).Normalized();
                idxPtr += 3;

                // Triangle 1: i+sx+1, i+1, i+sx+2
                _patchIndices[idxPtr] = vertI + sx + 1;
                _patchIndices[idxPtr + 1] = vertI + 1;
                _patchIndices[idxPtr + 2] = vertI + sx + 2;
                e1 = _patchVertices[_patchIndices[idxPtr + 1]] - _patchVertices[_patchIndices[idxPtr]];
                e2 = _patchVertices[_patchIndices[idxPtr + 2]] - _patchVertices[_patchIndices[idxPtr]];
                _patchNormals[triIdx + 1] = (e1 ^ e2).Normalized();
                idxPtr += 3;

                byte surfType = Hf.GetSurfTypeAt(ix + col, iy + row);
                _patchIds[triIdx] = surfType;
                _patchIds[triIdx + 1] = surfType;
            }
        }
    }

    public override void GetBBox(ref Box bbox)
    {
        float halfX = Hf.Size.X * Hf.Step.X * 0.5f;
        float halfY = Hf.Size.Y * Hf.Step.Y * 0.5f;
        float halfZ = (_maxHeight - _minHeight) * 0.5f;

        bbox.Center = new PhysVector3(
            Hf.Origin.X + halfX,
            Hf.Origin.Y + halfY,
            _minHeight + halfZ
        );
        bbox.Size = new PhysVector3(halfX, halfY, halfZ);
        bbox.Basis = PhysMatrix33.Identity;
        bbox.IsOriented = false;
    }

    public override float GetVolume()
    {
        // Heightfield has no meaningful closed volume
        return 0f;
    }

    public override PhysVector3 GetCenter()
    {
        return Hf.Origin + new PhysVector3(
            Hf.Size.X * Hf.Step.X * 0.5f,
            Hf.Size.Y * Hf.Step.Y * 0.5f,
            0
        );
    }

    public override PhysicalProperties CalcPhysicalProperties()
    {
        return new PhysicalProperties
        {
            Volume = 0f,
            CenterOfMass = GetCenter(),
            InertiaTensor = PhysMatrix33.Identity
        };
    }

    /// <summary>
    /// Find closest point on heightfield to a given point.
    /// Port of CHeightfield::FindClosestPoint.
    /// </summary>
    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        closestPt = new PhysVector3(1E10f, 1E10f, 1E10f);
        normal = PhysVector3.UnitZ;

        int ix = (int)MathF.Floor(pt.X * Hf.StepR.X);
        int iy = (int)MathF.Floor(pt.Y * Hf.StepR.Y);

        if ((uint)ix > (uint)(Hf.Size.X - 2) || (uint)iy > (uint)(Hf.Size.Y - 2))
            return float.MaxValue;

        float h00 = Hf.GetHeightAt(ix, iy);
        float h10 = Hf.GetHeightAt(ix + 1, iy);
        float h01 = Hf.GetHeightAt(ix, iy + 1);
        float h11 = Hf.GetHeightAt(ix + 1, iy + 1);

        // Determine which corner is nearest
        float xf = pt.X * Hf.StepR.X - ix;
        float yf = pt.Y * Hf.StepR.Y - iy;
        int sx = xf > 0.5f ? 1 : 0;
        int sy = yf > 0.5f ? 1 : 0;

        float[] h = { h00, h10, h01, h11 };
        closestPt = new PhysVector3((ix + sx) * Hf.Step.X, (iy + sy) * Hf.Step.Y, h[sx + sy * 2]);

        // Check both cell triangles for closest point on face/edge
        GetCellTriangles(ix, iy, out var tri0, out var tri1);

        // Check triangle face distances
        CheckPtTriDist(tri0, pt, ref closestPt);
        CheckPtTriDist(tri1, pt, ref closestPt);

        normal = (pt - closestPt);
        float dist = normal.Length();
        if (dist > 1e-10f)
            normal = normal / dist;
        else
            normal = PhysVector3.UnitZ;

        return dist;
    }

    private static void CheckPtTriDist(Triangle tri, in PhysVector3 pt, ref PhysVector3 ptres)
    {
        float dist = (pt - tri.P0).Dot(tri.Normal);
        if (dist > 0 && dist * dist < (pt - ptres).LengthSq() * tri.Normal.LengthSq())
        {
            // Check if pt projects inside triangle
            if (((tri.P1 - tri.P0) ^ (pt - tri.P0)).Dot(tri.Normal) > 0 &&
                ((tri.P2 - tri.P1) ^ (pt - tri.P1)).Dot(tri.Normal) > 0 &&
                ((tri.P0 - tri.P2) ^ (pt - tri.P2)).Dot(tri.Normal) > 0)
            {
                var n = tri.Normal.Normalized();
                ptres = pt - n * n.Dot(pt - tri.P0);
            }
        }
    }

    /// <summary>
    /// Test if a point is below the heightfield surface.
    /// Port of CHeightfield::PointInsideStatus.
    /// </summary>
    public override int PointInsideStatus(in PhysVector3 pt)
    {
        float xf = pt.X * Hf.StepR.X;
        float yf = pt.Y * Hf.StepR.Y;
        int ix = (int)MathF.Floor(xf);
        int iy = (int)MathF.Floor(yf);

        if ((uint)ix > (uint)(Hf.Size.X - 2) || (uint)iy > (uint)(Hf.Size.Y - 2))
            return 0;

        xf -= ix;
        yf -= iy;

        float h0, h1, h2;
        if (xf + yf < 1f)
        {
            h0 = Hf.GetHeightAt(ix, iy);
            h1 = Hf.GetHeightAt(ix + 1, iy);
            h2 = Hf.GetHeightAt(ix, iy + 1);
        }
        else
        {
            h0 = Hf.GetHeightAt(ix, iy + 1);
            h1 = Hf.GetHeightAt(ix + 1, iy + 1);
            h2 = Hf.GetHeightAt(ix + 1, iy);
            yf = 1f - yf;
        }

        float surfaceH = (h0 * (1f - xf) + h1 * xf) * (1f - yf) + h2 * yf;
        return pt.Z < surfaceH ? 1 : 0;
    }

    public override Primitive GetPrimitive() => Hf;

    public override int GetMemoryUsage()
    {
        return _patchVertices.Length * 12 + _patchIndices.Length * 4 +
               _patchNormals.Length * 12 + _patchIds.Length;
    }

    /// <summary>Last origin offset applied during PrepareForIntersectionTest (port of m_lastOriginOffs).</summary>
    private PhysVector3 _lastOriginOffs;

    /// <summary>
    /// Extrude a box along a sweep direction. Produces a new oriented box that contains
    /// the swept volume of the source box. Port of ::ExtrudeBox in utils.cpp:364.
    /// </summary>
    private static void ExtrudeBox(Box pbox, in PhysVector3 dir, float step, Box pextbox)
    {
        float proj, maxproj;
        int i;

        // maxproj = (row0 - dir*(dir*row0)).len2 * size[0]
        PhysVector3 r0 = pbox.Basis.GetRow(0);
        PhysVector3 r1 = pbox.Basis.GetRow(1);
        PhysVector3 r2 = pbox.Basis.GetRow(2);

        maxproj = (r0 - dir * dir.Dot(r0)).LengthSq() * pbox.Size.X;
        proj = (r1 - dir * dir.Dot(r1)).LengthSq() * pbox.Size.Y;
        i = (maxproj - proj) < 0 ? 1 : 0; maxproj = MathF.Max(proj, maxproj);
        proj = (r2 - dir * dir.Dot(r2)).LengthSq() * pbox.Size.Z;
        i |= ((maxproj - proj) < 0 ? 1 : 0) << 1;
        // i &= 2|(i>>1^1)  — selects the axis with the most perpendicular projection to dir
        i &= 2 | ((i >> 1) ^ 1);

        // pextbox->Basis.SetRow(2,dir)
        pextbox.Basis.SetRow(2, dir);
        // row0 = (row_i - dir*(dir*row_i)).normalized()
        PhysVector3 ri = pbox.Basis.GetRow(i);
        pextbox.Basis.SetRow(0, (ri - dir * dir.Dot(ri)).Normalized());
        // row1 = row2 ^ row0
        pextbox.Basis.SetRow(1, pextbox.Basis.GetRow(2) ^ pextbox.Basis.GetRow(0));
        pextbox.IsOriented = true;

        // mtx = pextbox->Basis * pbox->Basis.T()
        PhysMatrix33 mtx = pextbox.Basis * pbox.Basis.Transposed();
        // size = mtx.Fabs() * pbox->size;  size.z += fabs(step)*0.5
        PhysMatrix33 mtxAbs = new PhysMatrix33(
            MathF.Abs(mtx.M00), MathF.Abs(mtx.M01), MathF.Abs(mtx.M02),
            MathF.Abs(mtx.M10), MathF.Abs(mtx.M11), MathF.Abs(mtx.M12),
            MathF.Abs(mtx.M20), MathF.Abs(mtx.M21), MathF.Abs(mtx.M22));
        PhysVector3 newSize = mtxAbs * pbox.Size;
        newSize.Z += MathF.Abs(step) * 0.5f;
        pextbox.Size = newSize;
        pextbox.Center = pbox.Center + dir * (step * 0.5f);
    }

    /// <summary>
    /// Prepare the heightfield for an intersection test against a collider.
    /// Port of CHeightfield::PrepareForIntersectionTest (heightfieldgeom.cpp:198-285).
    /// </summary>
    public override void PrepareForIntersectionTest(GeometryUnderTest pGTest, GeometryBase collider,
        GeometryUnderTest pGTestColl, bool bKeepPrevContacts)
    {
        Box abox = new Box();
        Box aboxext = new Box();
        Box pbox;

        // pCollider->GetBVTree()->GetBBox(&abox)
        if (collider.Tree is HeightfieldBV hfColl)
            hfColl.GetBBox(ref abox);
        else
            collider.GetBBox(ref abox);

        if (pGTestColl != null && pGTestColl.SweepStep > 0)
        {
            ExtrudeBox(abox, pGTestColl.SweepDirLoc, pGTestColl.SweepStepLoc, aboxext);
            pbox = aboxext;
        }
        else
        {
            pbox = abox;
        }

        int idxMax = PHYS_MAX_INDICES;

        // project_box_on_grid(pbox,&m_hf, pGTest, ix,iy,sx,sy,minz)
        HeightfieldBV.ProjectBoxOnGrid(pbox, Hf, out int ix, out int iy, out int sx, out int sy, out float minz);

        // if ((sx-1 | sy-1 | idx_max-(sx+1)*(sy+1)) < 0) return 0
        if (((sx - 1) | (sy - 1) | (idxMax - (sx + 1) * (sy + 1))) < 0)
            return;

        var origin = new PhysVector3(ix * Hf.Step.X, iy * Hf.Step.Y, 0);
        if (pGTest != null)
        {
            _lastOriginOffs = pGTest.R * origin * pGTest.Scale;
            pGTest.Offset += _lastOriginOffs;
        }

        // Rebuild patch only if region changed
        if (((_hfTree.PatchStart.X - ix) | (_hfTree.PatchStart.Y - iy) |
             (_hfTree.PatchSize.X - sx) | (_hfTree.PatchSize.Y - sy)) != 0)
        {
            // Compute heights for the patch and check minz vs maxh early-out
            int heightsLen = (sx + 1) * (sy + 1);
            float[] heights = new float[heightsLen];
            float maxh = heights[0] = Hf.GetHeightAt(ix, iy);
            for (int ii = ix; ii <= ix + sx; ii++)
                for (int jj = iy; jj <= iy + sy; jj++)
                {
                    float h = Hf.GetHeightAt(ii, jj);
                    heights[(ii - ix) + (jj - iy) * (sx + 1)] = h;
                    if (h > maxh) maxh = h;
                }
            if (minz > maxh)
                return;

            // Build the patch mesh (handled by existing BuildPatch)
            BuildPatch(ix, iy, sx, sy);

            // Match C++: holes (id==-1) remove connectivity — applied inside BuildPatch equivalent.
            // (TriMesh topology is optional here; kept as a no-op when topology isn't tracked.)
        }
        else if (minz > _hfTree.MaxHeight)
        {
            return;
        }

        // Clear used-tri bitmap (m_Tree.m_pUsedTriMap)
        if (_hfTree.UsedTriMap != null)
        {
            for (int k = (_patchTriCount - 1) >> 5; k >= 0 && k < _hfTree.UsedTriMap.Length; k--)
                _hfTree.UsedTriMap[k] = 0;
        }

        if (pGTest == null || pGTest.BStopIntersection)
            return;

        // res = CTriMesh::PrepareForIntersectionTest(...)  — delegate to base class
        base.PrepareForIntersectionTest(pGTest, collider, pGTestColl, bKeepPrevContacts);
    }

    /// <summary>
    /// Intersect against another geometry.
    /// Port of CHeightfield::Intersect (heightfieldgeom.cpp:91-196).
    /// Ray branch returns 0 (not hot for ships); non-ray path delegates to base class.
    /// </summary>
    public override int Intersect(GeometryBase other, ref GeomContact[] contacts)
    {
        if (other.GeomType == GeomTypes.Ray)
        {
            // Full ray-heightfield traversal (DrawRayOnGrid + hf_cell_checker) is not yet ported;
            // ray path is not exercised in ArcheAge ship-vs-terrain usage.
            return 0;
        }

        return base.Intersect(other, ref contacts);
    }
}
