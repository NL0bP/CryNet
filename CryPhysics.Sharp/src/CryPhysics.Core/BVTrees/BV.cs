// Literal port of bvtree.h:22-44 BV class hierarchy.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.BVTrees;

/// <summary>
/// Bounding volume base. Port of `struct BV { int type; int iNode; }` from bvtree.h:22-26.
/// In C++ a BV pointer is implicitly castable to `primitive*`; the C# port surfaces that via
/// the typed subclass <see cref="BVPrimitive"/> + <see cref="BBox"/>/<see cref="BVHeightfield"/>/etc.
/// </summary>
public abstract class BV
{
    public int Type;
    public int INode;

    /// Get the underlying primitive (for direct intersection tests). Mirrors the C++
    /// `inline operator primitive*()` conversion.
    public abstract Primitive? AsPrimitive();
}

/// Generic BV holding any primitive. Port of `BV_Primitive : BV { primitive p; }`.
public class BVPrimitive : BV
{
    public Primitive? P;
    public override Primitive? AsPrimitive() => P;
}

/// Box BV. Port of `BBox : BV { box abox; }` (bvtree.h:30-32).
public class BBox : BV
{
    public Box ABox = new();
    public override Primitive? AsPrimitive() => ABox;
}

/// Heightfield BV. Port of `BVheightfield : BV { heightfield hf; }` (bvtree.h:34-36).
public class BVHeightfield : BV
{
    public Heightfield? Hf;
    public override Primitive? AsPrimitive() => Hf;
}

/// Voxel-grid BV. Port of `BVvoxelgrid : BV { voxelgrid voxgrid; }` (bvtree.h:38-40).
public class BVVoxelgrid : BV
{
    public VoxelGrid? VoxGrid;
    public override Primitive? AsPrimitive() => VoxGrid;
}

/// Ray BV. Port of `BVray : BV { ray aray; }` (bvtree.h:42-44).
public class BVRay : BV
{
    public Ray? ARay;
    public override Primitive? AsPrimitive() => ARay;
}

/// BV tree type enum. Port of `BVtreetypes` (bvtree.h:106).
public static class BVTreeTypes
{
    public const int OBB = 0;
    public const int AABB = 1;
    public const int SingleBox = 2;
    public const int Ray = 3;
    public const int Heightfield = 4;
    public const int Voxel = 5;
}
