// Literal port of dev/Code/CryEngine/CryCommon/primitives.h (primitives namespace + geom_contact + contact + prim_inters + geom_contact_area).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// In C++ this is `namespace primitives { ... }`. C# rendered as a static class so call sites
// `primitives.sphere`, `primitives.box.type`, `primitives.cylinder` etc. preserve C++ syntax.

namespace CryAISystem.CryCommon;

public static class primitives
{
    ////////////////////////// primitives //////////////////////

    public class primitive
    {
    }

    // The C++ originals declare `enum entype { type=0 };` inside each primitive struct, which
    // makes `instance.type` valid in C++. C# const cannot be accessed through an instance, so
    // we expose `type` as a virtual instance property that returns the same value.
    public class box : primitive
    {
        public const int box_type = 0;
        public virtual int type { get { return box_type; } }
        public Matrix33 Basis;            // v_box = Basis*v_world; Basis = Rotation.T()
        public int bOriented;
        public Vec3 center;
        public Vec3 size;

        public box() { Basis = new Matrix33(); }
    }

    public class triangle : primitive
    {
        public const int triangle_type = 1;
        public virtual int type { get { return triangle_type; } }
        public Vec3[] pt = new Vec3[3];
        public Vec3 n;
    }

    public class indexed_triangle : triangle
    {
        public int idx;
    }

    public class ray : primitive
    {
        public const int ray_type = 3;
        public virtual int type { get { return ray_type; } }
        public Vec3 origin;
        public Vec3 dir;
    }

    public class sphere : primitive
    {
        public const int sphere_type = 4;
        public virtual int type { get { return sphere_type; } }
        public Vec3 center;
        public float r;
    }

    public class cylinder : primitive
    {
        public const int cylinder_type = 5;
        public virtual int type { get { return cylinder_type; } }
        public Vec3 center;
        public Vec3 axis;
        public float r, hh;
    }

    public class capsule : cylinder
    {
        public const int capsule_type = 6;
        public override int type { get { return capsule_type; } }
    }

    public class plane : primitive
    {
        public const int plane_type = 8;
        public virtual int type { get { return plane_type; } }
        public Vec3 n;
        public Vec3 origin;
    }

    public class coord_plane : plane
    {
        public Vec3[] axes = new Vec3[2];
    }
}

public struct prim_inters
{
    public Vec3[] pt;          // [2]
    public Vec3 n;
    public byte iFeature00, iFeature01, iFeature10, iFeature11; // [2][2]
    public float minPtDist2;
    public short id0, id1;     // [2]
    public int iNode0, iNode1; // [2]
    public Vec3[] ptborder;
    public int nborderpt, nbordersz;
    public Vec3 ptbest;
    public int nBestPtVal;

    public void Init()
    {
        pt = new Vec3[2];
        minPtDist2 = 0.0f;
        ptbest = new Vec3(0, 0, 0);
    }
}

public struct contact
{
    public float t, taux;       // 'real' in C++ = float
    public Vec3 pt;
    public Vec3 n;
    public uint iFeature0, iFeature1; // [2]
}

public static class PrimitiveConstants
{
    public const int NPRIMS = 8;       // since plane is currently not supported in collision checks
    public const int IFEAT_LOG2 = 23;
    public const int IDXMASK = ~(0xFF << IFEAT_LOG2);
    public const int TRIEND = 0x80 << IFEAT_LOG2;
}

public class geom_contact_area
{
    public enum entype { polygon, polyline }
    public int type;
    public int npt;
    public int nmaxpt;
    public float minedge;
    public int[] piPrim0, piPrim1;       // [2]
    public int[] piFeature0, piFeature1; // [2]
    public Vec3[] pt;
    public Vec3 n1; // normal of other object surface (or edge)
}

public class geom_contact
{
    public float t;
    public Vec3 pt;
    public Vec3 n;
    public Vec3 dir;       // unprojection direction
    public int iUnprojMode;
    public float vel;      // original velocity along this direction, <0 if least squares normal was used
    public int[] id = new int[2];       // external ids for colliding geometry parts
    public int[] iPrim = new int[2];
    public int[] iFeature = new int[2];
    public int[] iNode = new int[2];    // BV-tree nodes of contacting primitives
    public Vec3[] ptborder;             // intersection border
    public int[][] idxborder;           // primitive index | primitive's feature's id << IFEAT_LOG2
    public int nborderpt;
    public int bClosed;
    public Vec3 center;
    public bool bBorderConsecutive;
    public geom_contact_area parea;
}
