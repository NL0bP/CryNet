// Literal partial port of dev/Code/CryEngine/CryCommon/physinterface.h.
// Subset needed by AICollision.cpp: intersection_params, ray_hit, geom_world_data,
// pe_status_pos additions, pe_status_nparts, IPhysicalWorld interface (PrimitiveWorldIntersection
// + GetEntitiesInBox + RayTraceEntity + CollideEntityWithBeam + CollideEntityWithPrimitive
// + GetGeomManager + GetPhysUtils), CryPhysics.Entities.IPhysicalEntity, IGeomManager, IPhysUtils, IGeometry,
// SPWIParams nested struct, WriteLockCond.
// Full literal port (8000+L) deferred — see deferred.md.
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// physinterface.h ~1986
public class intersection_params
{
    public int iUnprojectionMode;          // 0-angular, 1-rotational
    public Vec3 centerOfRotation;          // for mode 1 only
    public Vec3 axisOfRotation;            // if left 0, will be set based on collision area normal
    public float time_interval;            // used to set unprojection limits
    public float vrel_min;                 // if local relative velocity in contact area is above this, unprojects along its derection; otherwise along area normal
    public float maxSurfaceGapAngle;       // theshold for generating area contacts
    public float minAxisDist;              // disables rotational unprojection if contact point is closer than this to the axis
    public Vec3 unprojectionPlaneNormal;   // restrict linear unprojection to this plane
    public Vec3 axisContactNormal;         // a mild hint about possible contact normal
    public float maxUnproj;                // unprojections longer than this are discarded
    public Vec3[] ptOutsidePivot = new Vec3[2]; // discard contacts that are not facing outward wrt this point
    public bool bSweepTest;                // requests a linear sweep test along v*time_interval (v from geom_world_data)
    public bool bKeepPrevContacts;         // append results to existing contact buffer
    public bool bStopAtFirstTri;           // stop after the first collision is detected
    public bool bNoAreaContacts;           // don't try to detect contact areas
    public bool bNoBorder;                 // don't trace contact border
    public int bExactBorder;               // always tries to return a consequtive border (useful for boolean ops)
    public int bNoIntersection;            // don't find all intersection points (only applies to primitive-primitive collisions)
    public int bBothConvex;                // (output) both operands were convex
    public int bThreadSafe;                // set if it's known that no other thread will contend for the internal intersection data (only used in PrimitiveWorldIntersection now)
    public int bThreadSafeMesh;            // set if it's known that no other thread will try to modify the colliding geometry
    public geom_contact pGlobalContacts;   // pointer to thread's global contact buffer

    public intersection_params()
    {
        iUnprojectionMode = 0;
        vrel_min = 1E-6f;
        time_interval = 100.0f;
        maxSurfaceGapAngle = 1.0f * (float)(System.Math.PI / 180.0);
        pGlobalContacts = null;
        minAxisDist = 0;
        bSweepTest = false;
        centerOfRotation = new Vec3(0, 0, 0);
        axisContactNormal = new Vec3(0, 0, 1);
        unprojectionPlaneNormal = new Vec3(0, 0, 0);
        axisOfRotation = new Vec3(0, 0, 0);
        bKeepPrevContacts = false;
        bStopAtFirstTri = false;
        ptOutsidePivot[0] = new Vec3(1E11f, 1E11f, 1E11f);
        ptOutsidePivot[1] = new Vec3(1E11f, 1E11f, 1E11f);
        maxUnproj = 1E10f;
        bNoAreaContacts = false;
        bNoBorder = false;
        bNoIntersection = 0;
        bExactBorder = 0;
        bThreadSafe = bThreadSafeMesh = 0;
    }
}

// physinterface.h ~2655
public class ray_hit
{
    public float dist;
    public CryPhysics.Entities.IPhysicalEntity pCollider;
    public int ipart;
    public int partid;
    public short surface_idx;
    public short idmatOrg;     // original material index, not mapped with material mapping
    public int foreignIdx;
    public int iNode;          // BV tree node that had the intersection; can be used for "warm start" next time
    public Vec3 pt;
    public Vec3 n;             // surface normal
    public int bTerrain;       // global terrain hit
    public int iPrim;          // hit triangle index
    public ray_hit next;       // reserved for internal use, do not change
}

// physinterface.h — pe_status_nparts
public class pe_status_nparts : CryPhysics.Params.PhysicsStatusBase
{
    public override int TypeId => 11;
}

// physinterface.h — geom_world_data
public class geom_world_data
{
    public Vec3 offset;
    public Matrix33 R;
    public float scale = 1.0f;
    public Vec3 v;            // linear velocity
    public Vec3 w;            // angular velocity
    public Vec3 centerOfMass;
    public int iStartNode;

    public geom_world_data()
    {
        R = new Matrix33();
        scale = 1.0f;
    }
}

// physinterface.h — pe_status_pos addition: BBox accessor on top of CryPhysics.StatusPos.
// The literal C++ pe_status_pos has `Vec3 BBox[2]; Vec3 pos; quaternionf q; float scale;`
// already on the existing CryPhysics.Sharp.StatusPos. We add a `BBox` indexed accessor matching
// the C++ array semantics so the literal AICollision call sites (`status.BBox[0]`) compile.
public static class StatusPosExtensions
{
    public static Vec3[] BBoxArray(this CryPhysics.Params.StatusPos sp)
    {
        // CryPhysics.Sharp StatusPos has separate BBoxMin/BBoxMax — wrap as a 2-element array.
        return new Vec3[] { sp.BBoxMin, sp.BBoxMax };
    }
}

// Mutex placeholder — physinterface.h declares WriteLockCond as a thin RAII wrapper around
// a conditional mutex. C# uses GC + System.Threading; the literal type exists only so the
// C++ call shape (`WriteLockCond lockContacts; ... PrimitiveWorldIntersection(..., &lockContacts)`)
// translates 1:1.
public class WriteLockCond
{
}

// IGeometry — full literal port deferred. Subset needed by AICollision.CheckWalkabilitySimple.
public interface IGeometry
{
    void SetData(object boxPrim);
}

// IGeomManager — subset
public interface IGeomManager
{
    IGeometry CreatePrimitive(int type, object pprim);
}

// IPhysUtils — subset
public interface IPhysUtils
{
    void DeletePointer(System.Array p);
}

// CryPhysics.Entities.IPhysicalEntity is already defined in CryPhysics.Sharp (CryPhysics.Entities.CryPhysics.Entities.IPhysicalEntity).
// Re-export the C++ name via a global using alias so the literal port reads `CryPhysics.Entities.IPhysicalEntity`.
// (See GlobalUsings.cs.)

// IPhysicalWorld — subset of physinterface.h's primary interface; only the methods AICollision uses.
public interface IPhysicalWorld
{
    int GetEntitiesInBox(Vec3 ptmin, Vec3 ptmax, ref CryPhysics.Entities.IPhysicalEntity[] pList, int objtypes, ulong szListAux = 0);
    int RayTraceEntity(CryPhysics.Entities.IPhysicalEntity pent, Vec3 origin, Vec3 dir, ref ray_hit pHit, object pForeignData, int geomFlagsAny);
    bool CollideEntityWithBeam(CryPhysics.Entities.IPhysicalEntity pent, Vec3 src, Vec3 dir, float r, ref ray_hit phit);
    bool CollideEntityWithPrimitive(CryPhysics.Entities.IPhysicalEntity pent, int itype, object pprim, Vec3 sweepDir, ref ray_hit phit, intersection_params pip);

    float PrimitiveWorldIntersection(int itype, object pprim, Vec3 sweepDir, EAICollisionEntities entTypes,
        CryPhysics.Entities.IPhysicalEntity[] ppContact, int geomFlagsAll, int geomFlagsAny, intersection_params parameters);

    float PrimitiveWorldIntersection(int itype, object pprim, Vec3 sweepDir, EAICollisionEntities entTypes,
        ref geom_contact[] ppContact, int geomFlagsAll, int geomFlagsAny, intersection_params parameters,
        int p1, int p2, CryPhysics.Entities.IPhysicalEntity[] pSkipEnts, int nSkipEnts, WriteLockCond lockContacts);

    float PrimitiveWorldIntersection(int itype, object pprim, Vec3 sweepDir, int entTypes,
        ref geom_contact ppContact, int geomFlagsAll, int geomFlagsAny, object p0, object p1, object p2,
        CryPhysics.Entities.IPhysicalEntity[] pSkipEnts, int nSkipEnts);

    float PrimitiveWorldIntersection(SPWIParams parameters);

    IGeomManager GetGeomManager();
    IPhysUtils GetPhysUtils();

    // Nested SPWIParams — used by OverlapTorsoSegment
    public class SPWIParams
    {
        public CryPhysics.Entities.IPhysicalEntity[] pSkipEnts;
        public int nSkipEnts;
        public Vec3 sweepDir;
        public intersection_params pip;
        public int itype;
        public object pprim;
    }
}
