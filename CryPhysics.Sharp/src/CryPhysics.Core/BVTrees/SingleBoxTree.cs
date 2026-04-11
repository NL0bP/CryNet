// Port of CryPhysics singleboxtree.h/cpp - degenerate BV tree for single primitives
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// Degenerate BV tree that contains a single bounding box.
/// Used for simple primitive geometries (box, sphere, capsule, cylinder).
/// Port of CSingleBoxTree from CryEngine.
/// </summary>
public class SingleBoxTree : BVTree
{
    private Box _box = new();

    public override int NodeCount => 1;

    public void SetBox(in PhysVector3 center, in PhysVector3 size, in PhysMatrix33 basis)
    {
        _box.Center = center;
        _box.Size = size;
        _box.Basis = basis;
        _box.IsOriented = true;
    }

    public void SetBox(Box box)
    {
        _box = box;
    }

    public override void GetNodeBV(ref BoundingVolume bv, int iNode)
    {
        bv.Type = BVType.Box;
        bv.INode = 0;
        bv.BBox = _box;
    }

    public override void GetNodeBV(ref BoundingVolume bv, int iNode,
        in PhysVector3 offset, float scale, in PhysQuaternion rotation)
    {
        bv.Type = BVType.Box;
        bv.INode = 0;
        bv.BBox ??= new Box();
        bv.BBox.Center = rotation.Rotate(_box.Center * scale) + offset;
        bv.BBox.Size = _box.Size * scale;
        if (_box.IsOriented)
        {
            var rotMtx = new PhysMatrix33(rotation);
            bv.BBox.Basis = _box.Basis * rotMtx.Transposed();
        }
        else
        {
            bv.BBox.Basis = new PhysMatrix33(rotation.Conjugate());
        }
        bv.BBox.IsOriented = true;
    }

    public override int GetNodeContents(int iNode, ref int[] contents)
    {
        contents = new[] { 0 };
        return 1;
    }

    public override void Build(PhysVector3[] vertices, int[] indices, int nTris)
    {
        // Compute AABB from vertices
        var min = new PhysVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new PhysVector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var v in vertices)
        {
            if (v.X < min.X) min.X = v.X;
            if (v.Y < min.Y) min.Y = v.Y;
            if (v.Z < min.Z) min.Z = v.Z;
            if (v.X > max.X) max.X = v.X;
            if (v.Y > max.Y) max.Y = v.Y;
            if (v.Z > max.Z) max.Z = v.Z;
        }

        _box.Center = (min + max) * 0.5f;
        _box.Size = (max - min) * 0.5f;
        _box.Basis = PhysMatrix33.Identity;
        _box.IsOriented = false;
    }
}
