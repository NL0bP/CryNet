// Port of CryPhysics unprojectionchecks.h - dispatch table for contact unprojection
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Collision;

/// <summary>
/// Unprojection mode parameters. Port of unprojection_mode struct.
/// Defines how overlapping primitives should be separated.
/// </summary>
public class UnprojectionMode
{
    public const int Linear = 0;
    public const int Rotational = 1;

    public int Mode;               // 0=linear, 1=rotational
    public PhysVector3 Dir;        // Direction (linear) or rotation axis
    public PhysVector3 Center;     // Center of rotation (rotational mode)
    public float Vel;              // Linear or angular velocity
    public float TMax;             // Maximum unprojection length
    public float TMin;             // Minimum unprojection length
    public float MinPtDist;        // Tolerance value
    public float MaxCos = 0.1f;    // Maximum cosine threshold
    public bool CheckContact;

    public PhysMatrix33 R0 = PhysMatrix33.Identity;
    public PhysVector3 Offset0;
}

/// <summary>
/// Delegate for unprojection check functions.
/// Returns non-zero if valid contact found.
/// </summary>
public delegate int UnprojectionCheckFunc(
    UnprojectionMode mode, Primitive prim1, int feature1,
    Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area);

/// <summary>
/// Dispatch table for linear and rotational unprojection tests.
/// Port of CUnprojectionChecker from CryEngine.
/// table[mode][type1][type2] maps to the appropriate unprojection function.
/// </summary>
public class UnprojectionChecker
{
    public static readonly UnprojectionChecker Instance = new();

    // [2] for linear/rotational, [NPrims][NPrims] for type pairs
    private readonly UnprojectionCheckFunc?[,,] _table;

    public UnprojectionChecker()
    {
        int n = PrimitiveConstants.NPrims;
        _table = new UnprojectionCheckFunc?[2, n, n];

        // Linear unprojection registrations
        // box row (type 0)
        Register(UnprojectionMode.Linear, Box.Type, Box.Type, LinearUnprojection.BoxBox);
        Register(UnprojectionMode.Linear, Box.Type, Triangle.Type, LinearUnprojection.BoxTri);
        Register(UnprojectionMode.Linear, Box.Type, Ray.Type, LinearUnprojection.BoxRay);
        Register(UnprojectionMode.Linear, Box.Type, Sphere.Type, LinearUnprojection.BoxSphere);
        Register(UnprojectionMode.Linear, Box.Type, Cylinder.Type, LinearUnprojection.BoxCylinder);
        Register(UnprojectionMode.Linear, Box.Type, Capsule.Type, LinearUnprojection.BoxCapsule);

        // triangle row (type 1)
        Register(UnprojectionMode.Linear, Triangle.Type, Box.Type, LinearUnprojection.TriBox);
        Register(UnprojectionMode.Linear, Triangle.Type, Triangle.Type, LinearUnprojection.TriTri);
        Register(UnprojectionMode.Linear, Triangle.Type, Ray.Type, LinearUnprojection.TriRay);
        Register(UnprojectionMode.Linear, Triangle.Type, Sphere.Type, LinearUnprojection.TriSphere);
        Register(UnprojectionMode.Linear, Triangle.Type, Cylinder.Type, LinearUnprojection.TriCylinder);
        Register(UnprojectionMode.Linear, Triangle.Type, Capsule.Type, LinearUnprojection.TriCapsule);

        // ray row (type 3)
        Register(UnprojectionMode.Linear, Ray.Type, Box.Type, LinearUnprojection.RayBox);
        Register(UnprojectionMode.Linear, Ray.Type, Triangle.Type, LinearUnprojection.RayTri);
        Register(UnprojectionMode.Linear, Ray.Type, Sphere.Type, LinearUnprojection.RaySphere);
        Register(UnprojectionMode.Linear, Ray.Type, Cylinder.Type, LinearUnprojection.RayCylinder);
        Register(UnprojectionMode.Linear, Ray.Type, Capsule.Type, LinearUnprojection.RayCapsule);

        // sphere row (type 4)
        Register(UnprojectionMode.Linear, Sphere.Type, Box.Type, LinearUnprojection.SphereBox);
        Register(UnprojectionMode.Linear, Sphere.Type, Triangle.Type, LinearUnprojection.SphereTri);
        Register(UnprojectionMode.Linear, Sphere.Type, Ray.Type, LinearUnprojection.SphereRay);
        Register(UnprojectionMode.Linear, Sphere.Type, Sphere.Type, LinearUnprojection.SphereSphere);
        Register(UnprojectionMode.Linear, Sphere.Type, Cylinder.Type, LinearUnprojection.SphereCylinder);
        Register(UnprojectionMode.Linear, Sphere.Type, Capsule.Type, LinearUnprojection.SphereCapsule);

        // cylinder row (type 5)
        Register(UnprojectionMode.Linear, Cylinder.Type, Box.Type, LinearUnprojection.CylinderBox);
        Register(UnprojectionMode.Linear, Cylinder.Type, Triangle.Type, LinearUnprojection.CylinderTri);
        Register(UnprojectionMode.Linear, Cylinder.Type, Ray.Type, LinearUnprojection.CylinderRay);
        Register(UnprojectionMode.Linear, Cylinder.Type, Sphere.Type, LinearUnprojection.CylinderSphere);
        Register(UnprojectionMode.Linear, Cylinder.Type, Cylinder.Type, LinearUnprojection.CylCyl);
        Register(UnprojectionMode.Linear, Cylinder.Type, Capsule.Type, LinearUnprojection.CylinderCapsule);

        // capsule row (type 6)
        Register(UnprojectionMode.Linear, Capsule.Type, Box.Type, LinearUnprojection.CapsuleBox);
        Register(UnprojectionMode.Linear, Capsule.Type, Triangle.Type, LinearUnprojection.CapsuleTri);
        Register(UnprojectionMode.Linear, Capsule.Type, Ray.Type, LinearUnprojection.CapsuleRay);
        Register(UnprojectionMode.Linear, Capsule.Type, Sphere.Type, LinearUnprojection.CapsuleSphere);
        Register(UnprojectionMode.Linear, Capsule.Type, Cylinder.Type, LinearUnprojection.CapsuleCylinder);
        Register(UnprojectionMode.Linear, Capsule.Type, Capsule.Type, LinearUnprojection.CapsuleCapsule);

        // Rotational unprojection registrations
        Register(UnprojectionMode.Rotational, Triangle.Type, Triangle.Type, RotationalUnprojection.TriTriRotUnprojection);
        Register(UnprojectionMode.Rotational, Ray.Type, Triangle.Type, RotationalUnprojection.RayTriRotUnprojection);
        Register(UnprojectionMode.Rotational, Triangle.Type, Ray.Type, RotationalUnprojection.TriRayRotUnprojection);
        Register(UnprojectionMode.Rotational, Ray.Type, Cylinder.Type, RotationalUnprojection.RayCylRotUnprojection);
        Register(UnprojectionMode.Rotational, Cylinder.Type, Ray.Type, RotationalUnprojection.CylRayRotUnprojection);
        Register(UnprojectionMode.Rotational, Ray.Type, Box.Type, RotationalUnprojection.RayBoxRotUnprojection);
        Register(UnprojectionMode.Rotational, Box.Type, Ray.Type, RotationalUnprojection.BoxRayRotUnprojection);
        Register(UnprojectionMode.Rotational, Ray.Type, Capsule.Type, RotationalUnprojection.RayCapsuleRotUnprojection);
        Register(UnprojectionMode.Rotational, Capsule.Type, Ray.Type, RotationalUnprojection.CapsuleRayRotUnprojection);
        Register(UnprojectionMode.Rotational, Ray.Type, Sphere.Type, RotationalUnprojection.RaySphereRotUnprojection);
        Register(UnprojectionMode.Rotational, Sphere.Type, Ray.Type, RotationalUnprojection.SphereRayRotUnprojection);
    }

    private void Register(int mode, int type1, int type2, UnprojectionCheckFunc func)
    {
        _table[mode, type1, type2] = func;
    }

    public int Check(UnprojectionMode mode, int type1, int type2,
        Primitive prim1, int feature1, Primitive prim2, int feature2,
        ref Contact contact, GeomContactArea? area = null)
    {
        var func = _table[mode.Mode, type1, type2];
        return func?.Invoke(mode, prim1, feature1, prim2, feature2, ref contact, area) ?? 0;
    }

    public bool CheckExists(int mode, int type1, int type2)
    {
        return _table[mode, type1, type2] != null;
    }
}
