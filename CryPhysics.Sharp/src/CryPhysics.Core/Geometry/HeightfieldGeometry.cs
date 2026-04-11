// Port of CryPhysics heightfieldgeom.h/cpp - heightfield geometry
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Heightfield collision geometry. Extends TriMeshGeometry to generate
/// triangles on-the-fly from a 2D height grid.
/// Port of CHeightfield from CryEngine.
/// </summary>
public class HeightfieldGeometry : GeometryBase
{
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
}
