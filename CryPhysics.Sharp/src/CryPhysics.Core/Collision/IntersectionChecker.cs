// Port of CryPhysics intersectionchecks.h - dispatch table for primitive intersection tests
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Primitives;

namespace CryPhysics.Collision;

/// <summary>
/// Delegate for primitive intersection test functions.
/// Returns non-zero if intersection found, fills pinters with contact data.
/// </summary>
public delegate int IntersectionCheckFunc(Primitive prim1, Primitive prim2, PrimInters pinters);

/// <summary>
/// Dispatch table for all primitive-primitive intersection tests.
/// Port of CIntersectionChecker from CryEngine.
/// Access via static instance: IntersectionChecker.Instance.Check(type1, type2, prim1, prim2, inters)
/// </summary>
public class IntersectionChecker
{
    public static readonly IntersectionChecker Instance = new();

    private readonly IntersectionCheckFunc?[,] _table;

    public IntersectionChecker()
    {
        int n = PrimitiveConstants.NPrims;
        _table = new IntersectionCheckFunc?[n, n];

        // Register all intersection tests
        // Box = 0, Triangle = 1, Heightfield = 2, Ray = 3, Sphere = 4, Cylinder = 5, Capsule = 6, VoxelGrid = 7
        Register(Box.Type, Box.Type, IntersectionTests.BoxBox);
        Register(Box.Type, Triangle.Type, IntersectionTests.BoxTri);
        Register(Triangle.Type, Box.Type, IntersectionTests.TriBox);
        Register(Box.Type, Ray.Type, IntersectionTests.BoxRay);
        Register(Ray.Type, Box.Type, IntersectionTests.RayBox);
        Register(Box.Type, Sphere.Type, IntersectionTests.BoxSphere);
        Register(Sphere.Type, Box.Type, IntersectionTests.SphereBox);
        Register(Box.Type, Cylinder.Type, IntersectionTests.BoxCylinder);
        Register(Cylinder.Type, Box.Type, IntersectionTests.CylinderBox);
        Register(Box.Type, Capsule.Type, IntersectionTests.BoxCapsule);
        Register(Capsule.Type, Box.Type, IntersectionTests.CapsuleBox);

        Register(Triangle.Type, Triangle.Type, IntersectionTests.TriTri);
        Register(Triangle.Type, Ray.Type, IntersectionTests.TriRay);
        Register(Ray.Type, Triangle.Type, IntersectionTests.RayTri);
        Register(Triangle.Type, Sphere.Type, IntersectionTests.TriSphere);
        Register(Sphere.Type, Triangle.Type, IntersectionTests.SphereTri);
        Register(Triangle.Type, Cylinder.Type, IntersectionTests.TriCylinder);
        Register(Cylinder.Type, Triangle.Type, IntersectionTests.CylinderTri);
        Register(Triangle.Type, Capsule.Type, IntersectionTests.TriCapsule);
        Register(Capsule.Type, Triangle.Type, IntersectionTests.CapsuleTri);

        Register(Ray.Type, Sphere.Type, IntersectionTests.RaySphere);
        Register(Sphere.Type, Ray.Type, IntersectionTests.SphereRay);
        Register(Ray.Type, Cylinder.Type, IntersectionTests.RayCylinder);
        Register(Cylinder.Type, Ray.Type, IntersectionTests.CylinderRay);
        Register(Ray.Type, Capsule.Type, IntersectionTests.RayCapsule);
        Register(Capsule.Type, Ray.Type, IntersectionTests.CapsuleRay);

        Register(Sphere.Type, Sphere.Type, IntersectionTests.SphereSphere);
        Register(Sphere.Type, Cylinder.Type, IntersectionTests.SphereCylinder);
        Register(Cylinder.Type, Sphere.Type, IntersectionTests.CylinderSphere);
        Register(Sphere.Type, Capsule.Type, IntersectionTests.SphereCapsule);
        Register(Capsule.Type, Sphere.Type, IntersectionTests.CapsuleSphere);

        Register(Cylinder.Type, Cylinder.Type, IntersectionTests.CylinderCylinder);
        Register(Cylinder.Type, Capsule.Type, IntersectionTests.CylinderCapsule);
        Register(Capsule.Type, Cylinder.Type, IntersectionTests.CapsuleCylinder);

        Register(Capsule.Type, Capsule.Type, IntersectionTests.CapsuleCapsule);
    }

    private void Register(int type1, int type2, IntersectionCheckFunc func)
    {
        _table[type1, type2] = func;
    }

    /// <summary>Check intersection between two primitives by type dispatch.</summary>
    public int Check(int type1, int type2, Primitive prim1, Primitive prim2, PrimInters pinters)
    {
        var func = _table[type1, type2];
        return func?.Invoke(prim1, prim2, pinters) ?? 0;
    }

    /// <summary>Check if an intersection test exists for the given type pair.</summary>
    public bool CheckExists(int type1, int type2)
    {
        return _table[type1, type2] != null;
    }
}
