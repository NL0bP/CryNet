// Port of CryPhysics voxelgeom.h/cpp - voxel grid geometry
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Voxel grid geometry. Stores a triangle mesh spatially indexed
/// into a 3D grid of cells for fast ray-cast and intersection queries.
/// Port of CVoxelGeom from CryEngine.
/// </summary>
public class VoxelGeometry : GeometryBase
{
    public override int GeomType => GeomTypes.VoxelGrid;

    /// <summary>The voxel grid primitive data.</summary>
    public VoxelGrid Grid { get; private set; } = new();

    // Mesh data (shared with grid)
    public PhysVector3[] Vertices { get; private set; } = Array.Empty<PhysVector3>();
    public int[] Indices { get; private set; } = Array.Empty<int>();
    public PhysVector3[] Normals { get; private set; } = Array.Empty<PhysVector3>();
    public byte[]? Ids { get; private set; }
    public int TriCount { get; private set; }

    private float _minVtxDist;

    public VoxelGeometry()
    {
    }

    /// <summary>
    /// Create voxel grid from mesh data and a 3D grid definition.
    /// Port of CVoxelGeom::CreateVoxelGrid.
    /// </summary>
    public VoxelGeometry CreateVoxelGrid(PhysVector3[] vertices, int[] indices,
        PhysVector3[] normals, byte[]? ids, int nTris,
        PhysVector3 origin, (int X, int Y, int Z) size, PhysVector3 step)
    {
        Vertices = vertices;
        Indices = indices;
        Normals = normals;
        Ids = ids;
        TriCount = nTris;

        Grid.Origin = origin;
        Grid.Step = step;
        Grid.StepR = new PhysVector3(1f / step.X, 1f / step.Y, 1f / step.Z);
        Grid.Size = size;
        Grid.Stride = (1, size.X, size.X * size.Y);
        Grid.Basis = PhysMatrix33.Identity;
        Grid.IsOriented = false;
        Grid.Vertices = vertices;
        Grid.Indices = indices;
        Grid.Normals = normals;
        Grid.Ids = ids;
        Grid.Rotation = PhysMatrix33.Identity;
        Grid.Offset = PhysVector3.Zero;
        Grid.Scale = 1f;
        Grid.RScale = 1f;

        int ncells = size.X * size.Y * size.Z;

        // Two-pass algorithm to build cell-triangle index
        // Pass 1: count triangles per cell
        int[] cellTris = new int[ncells + 1];

        for (int i = nTris - 1; i >= 0; i--)
        {
            ComputeTriBBox(i, out var bboxMin, out var bboxMax);
            for (int iz = bboxMin.Z; iz <= bboxMax.Z; iz++)
                for (int iy = bboxMin.Y; iy <= bboxMax.Y; iy++)
                    for (int ix = bboxMin.X; ix <= bboxMax.X; ix++)
                        cellTris[ix + iy * Grid.Stride.Y + iz * Grid.Stride.Z]++;
        }

        // Prefix sum
        for (int i = 1; i <= ncells; i++)
            cellTris[i] += cellTris[i - 1];

        // Pass 2: fill triangle buffer
        int[] triBuf = new int[cellTris[ncells]];
        for (int i = nTris - 1; i >= 0; i--)
        {
            ComputeTriBBox(i, out var bboxMin, out var bboxMax);
            for (int iz = bboxMin.Z; iz <= bboxMax.Z; iz++)
                for (int iy = bboxMin.Y; iy <= bboxMax.Y; iy++)
                    for (int ix = bboxMin.X; ix <= bboxMax.X; ix++)
                    {
                        int icell = ix + iy * Grid.Stride.Y + iz * Grid.Stride.Z;
                        cellTris[icell]--;
                        triBuf[cellTris[icell]] = i;
                    }
        }

        Grid.CellTris = cellTris;
        Grid.TriBuf = triBuf;

        _minVtxDist = step.X * 0.0001f;

        return this;
    }

    /// <summary>
    /// Compute the bounding box of a triangle in grid coordinates.
    /// </summary>
    private void ComputeTriBBox(int iTri, out (int X, int Y, int Z) bboxMin, out (int X, int Y, int Z) bboxMax)
    {
        var min3 = new PhysVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max3 = new PhysVector3(float.MinValue, float.MinValue, float.MinValue);

        // Compute centroid for nudging
        var centroid = PhysVector3.Zero;
        for (int j = 0; j < 3; j++)
            centroid = centroid + (Vertices[Indices[iTri * 3 + j]] - Grid.Origin);
        centroid = centroid * (1f / 3f);

        for (int j = 0; j < 3; j++)
        {
            var v = Vertices[Indices[iTri * 3 + j]] - Grid.Origin;
            for (int axis = 0; axis < 3; axis++)
            {
                float x = v[axis] * Grid.StepR[axis];
                // Nudge towards centroid to avoid boundary issues
                float nudge = MathF.Max(-0.001f, MathF.Min(0.001f,
                    (centroid[axis] - v[axis]) * Grid.StepR[axis]));
                x += nudge;
                int sizeOnAxis = axis switch { 0 => Grid.Size.X, 1 => Grid.Size.Y, _ => Grid.Size.Z };
                int icell = System.Math.Clamp((int)MathF.Floor(x), 0, sizeOnAxis - 1);
                min3[axis] = MathF.Min(min3[axis], icell);
                max3[axis] = MathF.Max(max3[axis], icell);
            }
        }

        bboxMin = ((int)min3.X, (int)min3.Y, (int)min3.Z);
        bboxMax = ((int)max3.X, (int)max3.Y, (int)max3.Z);
    }

    public override void GetBBox(ref Box bbox)
    {
        var halfSize = new PhysVector3(
            Grid.Size.X * Grid.Step.X * 0.5f,
            Grid.Size.Y * Grid.Step.Y * 0.5f,
            Grid.Size.Z * Grid.Step.Z * 0.5f
        );
        bbox.Center = Grid.Origin + halfSize;
        bbox.Size = halfSize;
        bbox.Basis = PhysMatrix33.Identity;
        bbox.IsOriented = false;
    }

    public override float GetVolume()
    {
        return Grid.Size.X * Grid.Step.X *
               Grid.Size.Y * Grid.Step.Y *
               Grid.Size.Z * Grid.Step.Z;
    }

    public override PhysVector3 GetCenter()
    {
        return Grid.Origin + new PhysVector3(
            Grid.Size.X * Grid.Step.X * 0.5f,
            Grid.Size.Y * Grid.Step.Y * 0.5f,
            Grid.Size.Z * Grid.Step.Z * 0.5f
        );
    }

    public override PhysicalProperties CalcPhysicalProperties()
    {
        var center = GetCenter();
        var size = new PhysVector3(
            Grid.Size.X * Grid.Step.X,
            Grid.Size.Y * Grid.Step.Y,
            Grid.Size.Z * Grid.Step.Z
        );
        return new PhysicalProperties
        {
            Volume = GetVolume(),
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
        closestPt = PhysVector3.Zero;
        normal = PhysVector3.UnitZ;
        float minDist2 = float.MaxValue;

        // Determine which cell the point falls in
        var local = pt - Grid.Origin;
        int cx = System.Math.Clamp((int)MathF.Floor(local.X * Grid.StepR.X), 0, Grid.Size.X - 1);
        int cy = System.Math.Clamp((int)MathF.Floor(local.Y * Grid.StepR.Y), 0, Grid.Size.Y - 1);
        int cz = System.Math.Clamp((int)MathF.Floor(local.Z * Grid.StepR.Z), 0, Grid.Size.Z - 1);

        int icell = cx + cy * Grid.Stride.Y + cz * Grid.Stride.Z;
        if (Grid.CellTris != null && Grid.TriBuf != null && icell + 1 < Grid.CellTris.Length)
        {
            for (int i = Grid.CellTris[icell]; i < Grid.CellTris[icell + 1]; i++)
            {
                int itri = Grid.TriBuf[i];
                var v0 = Vertices[Indices[itri * 3]];
                var v1 = Vertices[Indices[itri * 3 + 1]];
                var v2 = Vertices[Indices[itri * 3 + 2]];
                var cp = ClosestPointOnTriangle(pt, v0, v1, v2);
                float d2 = (pt - cp).LengthSq();
                if (d2 < minDist2)
                {
                    minDist2 = d2;
                    closestPt = cp;
                    normal = Normals[itri];
                }
            }
        }

        return MathF.Sqrt(minDist2);
    }

    public override int PointInsideStatus(in PhysVector3 pt)
    {
        // Voxel grids don't have a meaningful inside/outside distinction
        return -1;
    }

    public override Primitive GetPrimitive() => Grid;

    public override int GetMemoryUsage()
    {
        int size = Vertices.Length * 12 + Indices.Length * 4 + Normals.Length * 12;
        if (Grid.CellTris != null) size += Grid.CellTris.Length * 4;
        if (Grid.TriBuf != null) size += Grid.TriBuf.Length * 4;
        return size;
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
}

/// <summary>
/// Extension to access Grid3D size by index.
/// </summary>
internal static class Grid3DExtensions
{
    public static int GetSizeComponent(this (int X, int Y, int Z) size, int axis)
    {
        return axis switch { 0 => size.X, 1 => size.Y, 2 => size.Z, _ => 0 };
    }
}
