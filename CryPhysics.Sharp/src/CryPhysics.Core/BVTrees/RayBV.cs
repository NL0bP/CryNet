// Port of CryPhysics raybv.h/cpp - ray bounding volume tree
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// Degenerate BV tree that wraps a single ray primitive.
/// Used by CRayGeom for ray-cast queries.
/// Port of CRayBV from CryEngine.
/// </summary>
public class RayBV : BVTree
{
    /// <summary>The ray primitive wrapped by this BV.</summary>
    public Ray? Ray { get; private set; }

    public override int NodeCount => 1;

    /// <summary>Set the ray data.</summary>
    public void SetRay(Ray ray)
    {
        Ray = ray;
    }

    /// <summary>
    /// Build is a no-op for ray BVs.
    /// Port of CRayBV::Build.
    /// </summary>
    public override void Build(PhysVector3[] vertices, int[] indices, int nTris)
    {
        // Ray BV doesn't build from vertices/indices
    }

    /// <summary>
    /// Get the bounding volume for the ray (local space).
    /// Port of CRayBV::GetNodeBV (local).
    /// </summary>
    public override void GetNodeBV(ref BoundingVolume bv, int iNode)
    {
        bv.Type = BVType.Ray;
        bv.INode = 0;

        if (Ray == null) return;

        // Compute AABB from ray endpoints for box-based BV
        bv.BBox ??= new Box();
        var p0 = Ray.Origin;
        var p1 = Ray.Origin + Ray.Dir;
        bv.BBox.Center = (p0 + p1) * 0.5f;
        bv.BBox.Size = new PhysVector3(
            MathF.Abs(Ray.Dir.X) * 0.5f,
            MathF.Abs(Ray.Dir.Y) * 0.5f,
            MathF.Abs(Ray.Dir.Z) * 0.5f
        );
        bv.BBox.Basis = PhysMatrix33.Identity;
        bv.BBox.IsOriented = false;
    }

    /// <summary>
    /// Get the bounding volume for the ray with world transform.
    /// Port of CRayBV::GetNodeBV (world space).
    /// </summary>
    public override void GetNodeBV(ref BoundingVolume bv, int iNode,
        in PhysVector3 offset, float scale, in PhysQuaternion rotation)
    {
        bv.Type = BVType.Ray;
        bv.INode = 0;

        if (Ray == null) return;

        // Transform ray to world space
        var worldOrigin = rotation.Rotate(Ray.Origin * scale) + offset;
        var worldDir = rotation.Rotate(Ray.Dir * scale);

        bv.BBox ??= new Box();
        var p1 = worldOrigin + worldDir;
        bv.BBox.Center = (worldOrigin + p1) * 0.5f;
        bv.BBox.Size = new PhysVector3(
            MathF.Abs(worldDir.X) * 0.5f,
            MathF.Abs(worldDir.Y) * 0.5f,
            MathF.Abs(worldDir.Z) * 0.5f
        );
        bv.BBox.Basis = PhysMatrix33.Identity;
        bv.BBox.IsOriented = false;
    }

    /// <summary>
    /// Get node contents - returns the single ray primitive.
    /// Port of CRayBV::GetNodeContents.
    /// </summary>
    public override int GetNodeContents(int iNode, ref int[] contents)
    {
        contents = new[] { 0 };
        return 1;
    }

    /// <summary>
    /// Get all primitives - returns the ray.
    /// </summary>
    public override List<(Primitive prim, int idx)> GetAllPrimitives(IGeometry geom)
    {
        if (Ray != null)
            return new List<(Primitive, int)> { (Ray, 0) };
        return new List<(Primitive, int)>();
    }

    public override int GetMemoryUsage() => 32;

    // Literal C++ CRayBV API overrides (raybv.cpp).

    public override int GetTypeId() => BVTreeTypes.Ray;

    public override void GetNodeBVRef(out BV pBV, int iNode = 0, int iCaller = 0)
    {
        pBV = new BVRay { Type = BVTreeTypes.Ray, INode = 0, ARay = Ray };
    }

    public override void GetNodeBVRef(in PhysMatrix33 Rw, in PhysVector3 offsw, float scalew,
        out BV pBV, int iNode = 0, int iCaller = 0)
    {
        var transformed = Ray == null ? null : new Ray(
            Rw * (Ray.Origin * scalew) + offsw,
            Rw * (Ray.Dir * scalew));
        pBV = new BVRay { Type = BVTreeTypes.Ray, INode = 0, ARay = transformed };
    }

    public override int GetNodeContents(int iNode, BV pBVCollider, int bColliderUsed, int bColliderLocal,
        Geometry.GeometryUnderTest pGTest, Geometry.GeometryUnderTest pGTestOp)
    {
        // Single primitive (the ray itself).
        pGTest.SzPrim = 0;
        return 1;
    }
}
