// Port of CryPhysics heightfieldbv.h/cpp - heightfield bounding volume tree
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// Bounding volume tree for heightfield geometry.
/// Acts as a single-node BV that represents the entire heightfield patch.
/// Port of CHeightfieldBV from CryEngine.
/// </summary>
public class HeightfieldBV : BVTree
{
    /// <summary>Reference to the heightfield primitive data.</summary>
    public Heightfield? Hf { get; private set; }

    /// <summary>Start coordinates of the current patch.</summary>
    public Vector2i PatchStart = new(-1, -1);

    /// <summary>Size of the current patch in cells.</summary>
    public Vector2i PatchSize;

    /// <summary>Height bounds for the current patch.</summary>
    public float MinHeight;
    public float MaxHeight;

    /// <summary>Bitmap of used triangles during intersection.</summary>
    public uint[]? UsedTriMap;

    public override int NodeCount => 1;

    /// <summary>
    /// Set the heightfield data reference.
    /// Port of CHeightfieldBV::SetHeightfield.
    /// </summary>
    public void SetHeightfield(Heightfield phf)
    {
        Hf = phf;
        PatchStart = new Vector2i(-1, -1);
        PatchSize = phf.Size;
        MinHeight = -1f;
        MaxHeight = 1f;
    }

    /// <summary>
    /// Build the BV tree (trivial for heightfields - just returns volume).
    /// Port of CHeightfieldBV::Build.
    /// </summary>
    public override void Build(PhysVector3[] vertices, int[] indices, int nTris)
    {
        // Heightfield BV doesn't actually build from vertices/indices;
        // it uses the heightfield data directly.
    }

    /// <summary>
    /// Get axis-aligned bounding box for the heightfield patch.
    /// Port of CHeightfieldBV::GetBBox.
    /// </summary>
    public void GetBBox(ref Box pbox)
    {
        if (Hf == null) return;

        pbox.Size = new PhysVector3(
            PatchSize.X * Hf.Step.X * 0.5f,
            PatchSize.Y * Hf.Step.Y * 0.5f,
            (MaxHeight - MinHeight) * 0.5f
        );
        pbox.Center = new PhysVector3(
            PatchStart.X * Hf.Step.X + pbox.Size.X,
            PatchStart.Y * Hf.Step.Y + pbox.Size.Y,
            MinHeight + pbox.Size.Z
        );
        pbox.Basis = PhysMatrix33.Identity;
        pbox.IsOriented = false;
    }

    /// <summary>
    /// Get the bounding volume for a node (always node 0 for heightfields).
    /// Port of CHeightfieldBV::GetNodeBV (local space).
    /// </summary>
    public override void GetNodeBV(ref BoundingVolume bv, int iNode)
    {
        bv.Type = BVType.HeightfieldNode;
        bv.INode = 0;
        bv.BBox ??= new Box();
        GetBBox(ref bv.BBox);
    }

    /// <summary>
    /// Get the bounding volume for a node with world transform.
    /// Port of CHeightfieldBV::GetNodeBV (world space).
    /// </summary>
    public override void GetNodeBV(ref BoundingVolume bv, int iNode,
        in PhysVector3 offset, float scale, in PhysQuaternion rotation)
    {
        if (Hf == null) return;

        bv.Type = BVType.HeightfieldNode;
        bv.INode = 0;
        bv.BBox ??= new Box();

        // Transform the bounding box to world space
        var localBox = new Box();
        GetBBox(ref localBox);

        var rotMtx = new PhysMatrix33(rotation);
        bv.BBox.Center = rotation.Rotate(localBox.Center * scale) + offset;
        bv.BBox.Size = localBox.Size * scale;
        bv.BBox.Basis = rotMtx.Transposed();
        bv.BBox.IsOriented = true;
    }

    /// <summary>
    /// Get node contents (all triangles in the heightfield patch).
    /// Port of CHeightfieldBV::GetNodeContents.
    /// </summary>
    public override int GetNodeContents(int iNode, ref int[] contents)
    {
        int nTris = PatchSize.X * PatchSize.Y * 2;
        if (contents.Length < nTris)
            contents = new int[nTris];
        for (int i = 0; i < nTris; i++)
            contents[i] = i;
        return nTris;
    }

    /// <summary>
    /// Mark a triangle as used during intersection testing.
    /// Port of CHeightfieldBV::MarkUsedTriangle.
    /// </summary>
    public void MarkUsedTriangle(int itri)
    {
        if (UsedTriMap != null)
            UsedTriMap[itri >> 5] |= 1u << (itri & 31);
    }

    /// <summary>
    /// Project a box onto the heightfield grid to find the affected cell range.
    /// Port of project_box_on_grid.
    /// </summary>
    public static void ProjectBoxOnGrid(Box pbox, Grid pgrid,
        out int ix, out int iy, out int sx, out int sy, out float minz)
    {
        PhysVector3 center;
        PhysVector3 dim;

        if (!pbox.IsOriented)
        {
            dim = pbox.Size;
            center = pbox.Center;
        }
        else
        {
            // Transform box size through basis to get AABB extents
            dim = new PhysVector3(
                MathF.Abs(pbox.Basis.M00) * pbox.Size.X + MathF.Abs(pbox.Basis.M01) * pbox.Size.Y + MathF.Abs(pbox.Basis.M02) * pbox.Size.Z,
                MathF.Abs(pbox.Basis.M10) * pbox.Size.X + MathF.Abs(pbox.Basis.M11) * pbox.Size.Y + MathF.Abs(pbox.Basis.M12) * pbox.Size.Z,
                MathF.Abs(pbox.Basis.M20) * pbox.Size.X + MathF.Abs(pbox.Basis.M21) * pbox.Size.Y + MathF.Abs(pbox.Basis.M22) * pbox.Size.Z
            );
            center = pbox.Center;
        }

        ix = System.Math.Max(0, (int)MathF.Floor((center.X - dim.X) * pgrid.StepR.X));
        iy = System.Math.Max(0, (int)MathF.Floor((center.Y - dim.Y) * pgrid.StepR.Y));
        sx = System.Math.Min((int)MathF.Ceiling((center.X + dim.X) * pgrid.StepR.X), pgrid.Size.X) - ix;
        sy = System.Math.Min((int)MathF.Ceiling((center.Y + dim.Y) * pgrid.StepR.Y), pgrid.Size.Y) - iy;
        minz = center.Z - dim.Z;
    }

    public override int GetMemoryUsage()
    {
        int size = 64; // Base size
        if (UsedTriMap != null) size += UsedTriMap.Length * 4;
        return size;
    }

    // Literal C++ CHeightfieldBV API overrides (heightfieldbv.cpp).

    public override int GetTypeId() => BVTreeTypes.Heightfield;

    public override void GetNodeBVRef(out BV pBV, int iNode = 0, int iCaller = 0)
    {
        // Heightfield BV exposes itself directly so the consumer can step the grid.
        pBV = new BVHeightfield { Type = BVTreeTypes.Heightfield, INode = 0, Hf = Hf };
    }

    public override void GetNodeBVRef(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        out BV pBV, int iNode = 0, int iCaller = 0)
    {
        // Same as local — heightfield handles its own world-transform internally via the geometry.
        GetNodeBVRef(out pBV, iNode, iCaller);
    }

    public override int GetNodeContents(int iNode, BV pBVCollider, int bColliderUsed, int bColliderLocal,
        Geometry.GeometryUnderTest pGTest, Geometry.GeometryUnderTest pGTestOp)
    {
        if (Hf == null) return 0;
        int nTris = PatchSize.X * PatchSize.Y * 2;
        if (pGTest.PrimBuf == null || pGTest.PrimBuf.Length < nTris)
            pGTest.PrimBuf = new IndexedTriangle[System.Math.Max(nTris, 16)];
        for (int i = 0; i < nTris; i++)
            pGTest.PrimBuf[i] = new IndexedTriangle { Index = i };
        pGTest.SzPrim = nTris;
        return nTris;
    }

    public override void MarkUsedTriangle(int itri, Geometry.GeometryUnderTest pGTest)
        => MarkUsedTriangle(itri);
}
