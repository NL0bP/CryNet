// Port of CryEngine primitives.h - geometric primitive definitions
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;
using CryPhysics.Math;

namespace CryPhysics.Primitives;

/// <summary>Number of primitive types supported by the collision system.</summary>
public static class PrimitiveConstants
{
    public const int NPrims = 8;
    public const int IFeatLog2 = 23;
    public const int IdxMask = ~(0xFF << IFeatLog2);
    public const int TriEnd = 0x80 << IFeatLog2;
}

/// <summary>Base class for all geometric primitives.</summary>
public abstract class Primitive
{
    public abstract int TypeId { get; }
}

/// <summary>
/// Oriented bounding box. Port of primitives::box.
/// Basis transforms from world to box local space: v_box = Basis * v_world.
/// </summary>
public class Box : Primitive
{
    public const int Type = 0;
    public override int TypeId => Type;

    public PhysMatrix33 Basis = PhysMatrix33.Identity;
    public bool IsOriented;
    public PhysVector3 Center;
    public PhysVector3 Size; // Half-extents

    public Box() { }

    public Box(in PhysVector3 center, in PhysVector3 size, in PhysMatrix33 basis, bool oriented = true)
    {
        Center = center;
        Size = size;
        Basis = basis;
        IsOriented = oriented;
    }
}

/// <summary>Triangle primitive. Port of primitives::triangle.</summary>
public class Triangle : Primitive
{
    public const int Type = 1;
    public override int TypeId => Type;

    public PhysVector3 P0, P1, P2;
    public PhysVector3 Normal;

    public Triangle() { }

    public Triangle(in PhysVector3 p0, in PhysVector3 p1, in PhysVector3 p2)
    {
        P0 = p0; P1 = p1; P2 = p2;
        Normal = ((p1 - p0) ^ (p2 - p0)).Normalized();
    }

    /// <summary>Get vertex by index.</summary>
    public PhysVector3 this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => i switch { 0 => P0, 1 => P1, 2 => P2, _ => throw new IndexOutOfRangeException() };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set { switch (i) { case 0: P0 = value; break; case 1: P1 = value; break; case 2: P2 = value; break; } }
    }
}

/// <summary>Indexed triangle with additional index field.</summary>
public class IndexedTriangle : Triangle
{
    public int Index;

    public IndexedTriangle() { }
    public IndexedTriangle(in PhysVector3 p0, in PhysVector3 p1, in PhysVector3 p2, int idx)
        : base(p0, p1, p2) { Index = idx; }
}

/// <summary>Ray primitive. Port of primitives::ray.</summary>
public class Ray : Primitive
{
    public const int Type = 3;
    public override int TypeId => Type;

    public PhysVector3 Origin;
    public PhysVector3 Dir;

    public Ray() { }
    public Ray(in PhysVector3 origin, in PhysVector3 dir) { Origin = origin; Dir = dir; }
}

/// <summary>Sphere primitive. Port of primitives::sphere.</summary>
public class Sphere : Primitive
{
    public const int Type = 4;
    public override int TypeId => Type;

    public PhysVector3 Center;
    public float Radius;

    public Sphere() { }
    public Sphere(in PhysVector3 center, float radius) { Center = center; Radius = radius; }
}

/// <summary>Cylinder primitive. Port of primitives::cylinder.</summary>
public class Cylinder : Primitive
{
    public const int Type = 5;
    public override int TypeId => Type;

    public PhysVector3 Center;
    public PhysVector3 Axis;
    public float Radius;
    public float HalfHeight;

    public Cylinder() { }
    public Cylinder(in PhysVector3 center, in PhysVector3 axis, float radius, float halfHeight)
    {
        Center = center; Axis = axis; Radius = radius; HalfHeight = halfHeight;
    }
}

/// <summary>Capsule primitive (cylinder with hemispherical caps). Port of primitives::capsule.</summary>
public class Capsule : Cylinder
{
    public new const int Type = 6;
    public override int TypeId => Type;

    public Capsule() { }
    public Capsule(in PhysVector3 center, in PhysVector3 axis, float radius, float halfHeight)
        : base(center, axis, radius, halfHeight) { }
}

/// <summary>Plane primitive. Port of primitives::plane.</summary>
public class Plane : Primitive
{
    public const int Type = 8;
    public override int TypeId => Type;

    public PhysVector3 Normal;
    public PhysVector3 Origin;

    public Plane() { }
    public Plane(in PhysVector3 normal, in PhysVector3 origin) { Normal = normal; Origin = origin; }
}

/// <summary>Coordinate plane with local basis axes. Port of primitives::coord_plane.</summary>
public class CoordPlane : Plane
{
    public PhysVector3 Axis0;
    public PhysVector3 Axis1;

    public CoordPlane() { }
}

/// <summary>2D grid primitive. Port of primitives::grid.</summary>
public class Grid : Primitive
{
    public const int GridType = -1; // No collision type
    public override int TypeId => GridType;

    public PhysMatrix33 Basis = PhysMatrix33.Identity;
    public bool IsOriented;
    public PhysVector3 Origin;
    public PhysVector2 Step;      // Cell size
    public PhysVector2 StepR;     // 1/step (reciprocal)
    public Vector2i Size;         // Grid dimensions
    public Vector2i Stride;       // Memory stride
    public bool IsCyclic;

    /// <summary>Check if coordinates are in range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InRange(int ix, int iy)
    {
        return (uint)ix < (uint)Size.X && (uint)iy < (uint)Size.Y;
    }

    /// <summary>Clamp coordinate to valid range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Crop(int i, int coord, int allowBorder)
    {
        int max = (coord == 0 ? Size.X : Size.Y) - 1 + allowBorder;
        if (i < 0) return 0;
        if (i > max) return max;
        return i;
    }

    /// <summary>Clamp 2D coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2i CropXY(Vector2i ic, int allowBorder)
    {
        return new Vector2i(
            Crop(ic.X, 0, allowBorder),
            Crop(ic.Y, 1, allowBorder)
        );
    }
}

/// <summary>
/// Heightfield primitive (extends grid with height data).
/// Port of primitives::heightfield.
/// </summary>
public class Heightfield : Grid
{
    public new const int GridType = 2;
    public override int TypeId => GridType;

    public float HeightScale = 1f;
    public ushort TypeMask;
    public ushort HeightMask;
    public int TypeHole;
    public int TypePower;

    /// <summary>Callback to get height at grid coordinates.</summary>
    public Func<int, int, float>? GetHeight;

    /// <summary>Callback to get surface type at grid coordinates.</summary>
    public Func<int, int, byte>? GetSurfType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetHeightAt(int ix, int iy)
    {
        return GetHeight?.Invoke(ix, iy) ?? 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetSurfTypeAt(int ix, int iy)
    {
        return GetSurfType?.Invoke(ix, iy) ?? 0;
    }
}

/// <summary>3D grid primitive. Port of primitives::grid3d.</summary>
public class Grid3D : Primitive
{
    public override int TypeId => -1;

    public PhysMatrix33 Basis = PhysMatrix33.Identity;
    public bool IsOriented;
    public PhysVector3 Origin;
    public PhysVector3 Step;
    public PhysVector3 StepR;
    public (int X, int Y, int Z) Size;
    public (int X, int Y, int Z) Stride;
}

/// <summary>Voxel grid primitive. Port of primitives::voxelgrid.</summary>
public class VoxelGrid : Grid3D
{
    public const int Type = 7;
    public override int TypeId => Type;

    public PhysMatrix33 Rotation = PhysMatrix33.Identity;
    public PhysVector3 Offset;
    public float Scale = 1f;
    public float RScale = 1f;

    // Mesh data references
    public PhysVector3[]? Vertices;
    public int[]? Indices;
    public PhysVector3[]? Normals;
    public byte[]? Ids;
    public int[]? CellTris;
    public int[]? TriBuf;
}

/// <summary>
/// Strided data access. Port of strided_pointer.
/// Provides access to data with a configurable stride between elements.
/// </summary>
public readonly struct StridedSpan<T> where T : struct
{
    private readonly T[] _data;
    private readonly int _offset;
    private readonly int _stride; // In elements, not bytes

    public StridedSpan(T[] data, int offset = 0, int stride = 1)
    {
        _data = data;
        _offset = offset;
        _stride = stride;
    }

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _data[_offset + index * _stride];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _data[_offset + index * _stride] = value;
    }

    public StridedSpan<T> Slice(int offset) => new(_data, _offset + offset * _stride, _stride);

    public bool IsValid => _data != null;
}

/// <summary>
/// Contact data from primitive intersection.
/// Port of prim_inters from primitives.h.
/// </summary>
public class PrimInters
{
    public PhysVector3 Pt0, Pt1;
    public PhysVector3 Normal;
    public byte[,] Feature = new byte[2, 2]; // iFeature[2][2]
    public float MinPtDist2;
    public short Id0, Id1;
    public int INode0, INode1;
    public PhysVector3[]? BorderPts;
    public int NBorderPt;
    public PhysVector3 BestPt;
    public int NBestPtVal;
}

/// <summary>
/// Basic contact info. Port of contact struct.
/// </summary>
public struct Contact
{
    public double T;
    public double TAux;
    public PhysVector3 Pt;
    public PhysVector3 Normal;
    public uint IFeature0;
    public uint IFeature1;
}

/// <summary>
/// Contact area geometry. Port of geom_contact_area.
/// </summary>
public class GeomContactArea
{
    public enum AreaType { Polygon, Polyline }

    public AreaType Type;
    public int NPt;
    public float MinEdge;
    public int[]? PrimIdx0;
    public int[]? PrimIdx1;
    public int[]? FeatureIdx0;
    public int[]? FeatureIdx1;
    public PhysVector3[]? Points;
    public PhysVector3 Normal1;
}

/// <summary>
/// Detailed geometry contact information.
/// Port of geom_contact from primitives.h.
/// </summary>
public class GeomContact
{
    public double T;
    public PhysVector3 Pt;
    public PhysVector3 Normal;
    public PhysVector3 Dir;
    public int UnprojMode;
    public float Vel;
    public int Id0, Id1;
    public int IPrim0, IPrim1;
    public int IFeature0, IFeature1;
    public int INode0, INode1;
    public PhysVector3[]? BorderPts;
    public (int, int)[]? BorderIdx;
    public int NBorderPt;
    public bool IsClosed;
    public PhysVector3 Center;
    public bool BorderConsecutive;
    public GeomContactArea? Area;
}
