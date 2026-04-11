// Port of intersection_params and geom_world_data from CryEngine
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Geometry;

/// <summary>
/// World-space transform data for a geometry during intersection.
/// Port of geom_world_data from physinterface.h.
/// </summary>
public class GeomWorldData
{
    public PhysVector3 Offset;
    public PhysMatrix33 R = PhysMatrix33.Identity;
    public float Scale = 1f;
    public PhysVector3 V;                // Linear velocity (for sweep tests)
    public PhysVector3 W;                // Angular velocity
    public PhysVector3 CenterOfMass;
    public int IStartNode;               // BVTree start node for partial tests
}

/// <summary>
/// Parameters controlling intersection tests.
/// Port of intersection_params from physinterface.h.
/// </summary>
public class IntersectionParams
{
    public bool BSweepTest;
    public bool BKeepPrevContacts;
    public bool BNoAreaContacts;
    public bool BBothConvex;
    public bool BThreadSafeMesh;
    public PhysVector3 AxisContactNormal;
    public PhysVector3 CenterOfRotation;
    public float VrelMin;
    public PhysVector3[] PtOutsidePivot = new PhysVector3[2];
}

/// <summary>
/// Per-geometry test state during BVTree traversal.
/// Port of geometry_under_test from CryEngine geometry.h.
/// </summary>
public class GeometryUnderTest
{
    public GeometryBase? Geometry;
    public BVTrees.BVTree? BVtree;

    // World-space transform
    public PhysVector3 Offset;
    public PhysMatrix33 R = PhysMatrix33.Identity;
    public float Scale = 1f;
    public float Rscale = 1f;

    // Relative transforms (this geometry relative to the other)
    public PhysVector3 OffsetRel;
    public PhysMatrix33 RRel = PhysMatrix33.Identity;
    public float ScaleRel = 1f;
    public float RscaleRel = 1f;

    // Motion
    public PhysVector3 V;
    public PhysVector3 W;
    public PhysVector3 CenterOfMass;
    public PhysVector3 CenterOfRotation;
    public PhysVector3 AxisContactNormal;

    // Results
    public GeomContact[]? Contacts;
    public int NContacts;
    public int NMaxContacts = 64;
    public bool BStopIntersection;
    public float SweepStep;

    public PhysVector3 PtOutsidePivot;
    public IntersectionParams? Params;
    public float MinAreaEdge;
}

/// <summary>
/// Result of a geometry-geometry intersection test.
/// Port of geom_contact from physinterface.h.
/// </summary>
public class GeomContact
{
    public PhysVector3 Pt;               // Contact point
    public PhysVector3 N;                // Contact normal
    public float T;                      // Unprojection distance
    public PhysVector3 Dir;              // Unprojection direction
    public int IUnprojMode;              // 0 = linear, 1 = rotational
    public int[] IPrim = new int[2];     // Primitive indices for each geometry
    public int[] IFeature = new int[2];  // Feature indices
    public byte[] Id = new byte[2];      // Surface IDs
    public float Vel;                    // Relative velocity at contact
    public int NBorderPt;                // Number of border points
    public GeomContactArea? PArea;       // Area contact data (if any)
}

/// <summary>
/// Area contact data for large contact patches.
/// Port of geom_contact_area.
/// </summary>
public class GeomContactArea
{
    public PhysVector3[] Pt = Array.Empty<PhysVector3>();
    public PhysVector3 N1;               // Second normal
    public int Npt;                      // Number of area points
    public int NMaxpt;
    public float MinEdge;
    public int[][] PiPrim = new int[2][];
    public int[][] PiFeature = new int[2][];
}
