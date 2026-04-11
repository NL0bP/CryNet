// Port of CryPhysics geoman.h/cpp - geometry creation and management
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Geometry factory and manager. Creates, registers, and pools geometry objects.
/// Port of CGeomManager / IGeomManager from CryEngine.
/// </summary>
public class GeometryManager
{
    private readonly List<PhysGeometry> _registeredGeoms = new();
    private int _nextId;

    /// <summary>Create a triangle mesh geometry from vertices and indices.</summary>
    public TriMeshGeometry CreateMesh(PhysVector3[] vertices, int[] indices, int nTris, int flags = 0)
    {
        var mesh = new TriMeshGeometry();
        mesh.SetData(vertices, indices, nTris);
        return mesh;
    }

    /// <summary>Create a primitive geometry (box, sphere, capsule, cylinder).</summary>
    public GeometryBase CreatePrimitive(int type, Primitive prim)
    {
        return type switch
        {
            Primitives.Box.Type => CreateBoxFromPrim((Box)prim),
            Sphere.Type => CreateSphereFromPrim((Sphere)prim),
            Cylinder.Type => CreateCylinderFromPrim((Cylinder)prim),
            Capsule.Type => CreateCapsuleFromPrim((Capsule)prim),
            _ => throw new ArgumentException($"Unknown primitive type: {type}")
        };
    }

    private BoxGeometry CreateBoxFromPrim(Box box) =>
        new(box.Center, box.Size, box.Basis);

    private SphereGeometry CreateSphereFromPrim(Sphere sphere) =>
        new(sphere.Center, sphere.Radius);

    private CylinderGeometry CreateCylinderFromPrim(Cylinder cyl) =>
        new(cyl.Center, cyl.Axis, cyl.Radius, cyl.HalfHeight);

    private CapsuleGeometry CreateCapsuleFromPrim(Capsule caps) =>
        new(caps.Center, caps.Axis, caps.Radius, caps.HalfHeight);

    /// <summary>Register a geometry and create a PhysGeometry wrapper.</summary>
    public PhysGeometry RegisterGeometry(GeometryBase geom, int surfaceIdx = 0, int[]? matMapping = null)
    {
        var pg = new PhysGeometry
        {
            Geometry = geom,
            Id = _nextId++,
            SurfaceIdx = surfaceIdx,
            MaterialMapping = matMapping ?? Array.Empty<int>(),
            Volume = geom.GetVolume()
        };

        var props = geom.CalcPhysicalProperties();
        pg.InertiaTensor = props.InertiaTensor;

        _registeredGeoms.Add(pg);
        return pg;
    }

    /// <summary>Unregister a geometry.</summary>
    public void UnregisterGeometry(PhysGeometry pg)
    {
        _registeredGeoms.Remove(pg);
        pg.Geometry.Release();
    }

    /// <summary>Get all registered geometries.</summary>
    public IReadOnlyList<PhysGeometry> RegisteredGeometries => _registeredGeoms;
}
