// Port of CryPhysics threading model - thread-local caller context
// Original: Copyright Crytek GMBH, used under license
// Replaces the iCaller / get_iCaller() pattern from StdAfx.h
// Ports g_idata[MAX_PHYS_THREADS] and g_RBdata[MAX_PHYS_THREADS] patterns

using CryPhysics.Collision;
using CryPhysics.Math;

namespace CryPhysics.Threading;

/// <summary>
/// Maximum number of physics threads. Port of MAX_PHYS_THREADS.
/// </summary>
public static class PhysicsThreading
{
    public const int MaxPhysThreads = 4;

    /// <summary>Thread-local caller context.</summary>
    [ThreadStatic]
    private static CallerContext? _tlsContext;

    /// <summary>Get the current thread's caller context (lazily initialized).</summary>
    public static CallerContext GetCallerContext()
    {
        return _tlsContext ??= new CallerContext();
    }

    /// <summary>Mark current thread as physics thread.</summary>
    public static void MarkAsPhysThread(int idx)
    {
        var ctx = GetCallerContext();
        ctx.PhysThreadIdx = idx;
    }

    /// <summary>Check if current thread is the physics thread.</summary>
    public static bool IsPhysThread()
    {
        return _tlsContext?.PhysThreadIdx >= 0;
    }

    /// <summary>
    /// Get the iCaller index for the current thread.
    /// Port of get_iCaller() from StdAfx.h.
    /// Returns 0 for the main thread, 1..N for worker threads, -1 for external.
    /// </summary>
    public static int GetICaller()
    {
        return _tlsContext?.PhysThreadIdx ?? -1;
    }
}

/// <summary>
/// Per-thread context for physics operations.
/// Replaces the g_idata[iCaller] pattern and thread-local scratch buffers.
/// Port of the iCaller indexing system from CryEngine.
/// </summary>
public class CallerContext
{
    /// <summary>Index of this thread in the physics thread pool (-1 for non-physics threads).</summary>
    public int PhysThreadIdx = -1;

    /// <summary>Thread-local intersection data scratch space.</summary>
    public IntersectionData IntersData = new();

    /// <summary>Thread-local temporary contact buffer.</summary>
    public GeomContactBuffer ContactBuffer = new();

    /// <summary>
    /// Thread-local solver scratch data.
    /// Port of g_RBdata[MAX_PHYS_THREADS] - per-thread contact solver state.
    /// </summary>
    public SolverThreadData SolverData = new();
}

/// <summary>
/// Thread-local scratch data for intersection tests.
/// Replaces the intersData / G(vname) macro pattern from geometry.h.
/// Port of the per-thread intersection buffers (g_idata[iCaller]).
/// </summary>
public class IntersectionData
{
    // Scratch buffers for BV tree traversal
    public int[] NodeStack = new int[256];
    public int NodeStackPos;

    // Temporary geometry buffers
    public PhysVector3[] TmpVtx = new PhysVector3[256];
    public int[] TmpIdx = new int[256];
    public float[] TmpFloats = new float[64];

    // Primitive scratch buffers for collision detection
    public PhysVector3[] EdgeNormals = new PhysVector3[64];
    public PhysVector3[] FeatBuf = new PhysVector3[64];
    public int[] FeatureIdx = new int[64];

    // Contact accumulation
    public int NContacts;

    public void Reset()
    {
        NodeStackPos = 0;
        NContacts = 0;
    }
}

/// <summary>
/// Thread-local solver scratch data.
/// Port of g_RBdata[MAX_PHYS_THREADS] from rigidbody.cpp.
/// Each physics thread gets its own solver workspace to avoid contention
/// during the contact preparation phase.
/// </summary>
public class SolverThreadData
{
    /// <summary>Max contacts per thread for the preparation phase.</summary>
    public const int MaxContactsPerThread = 2048;

    /// <summary>Temporary contact list built during contact detection.</summary>
    public Dynamics.EntityContact[] PendingContacts = new Dynamics.EntityContact[MaxContactsPerThread];
    public int NPendingContacts;

    /// <summary>Temporary entity list for spatial queries.</summary>
    public Entities.IPhysicalEntity[] TmpEntList = new Entities.IPhysicalEntity[256];
    public int NTmpEnts;

    /// <summary>Scratch velocity/impulse arrays for constraint resolution.</summary>
    public PhysVector3[] TmpImpulses = new PhysVector3[128];
    public float[] TmpScalars = new float[128];

    public void Reset()
    {
        NPendingContacts = 0;
        NTmpEnts = 0;
    }

    /// <summary>
    /// Register a contact found during the parallel detection phase.
    /// Thread-safe per-thread (each thread writes only to its own SolverThreadData).
    /// </summary>
    public bool AddPendingContact(Dynamics.EntityContact contact)
    {
        if (NPendingContacts >= MaxContactsPerThread) return false;
        PendingContacts[NPendingContacts++] = contact;
        return true;
    }
}

/// <summary>
/// Thread-local buffer for geometry contacts.
/// Replaces global contact arrays from CryEngine.
/// </summary>
public class GeomContactBuffer
{
    public const int MaxContacts = 128;

    public Primitives.GeomContact[] Contacts = new Primitives.GeomContact[MaxContacts];
    public int NContacts;

    public GeomContactBuffer()
    {
        for (int i = 0; i < MaxContacts; i++)
            Contacts[i] = new Primitives.GeomContact();
    }

    public void Reset() => NContacts = 0;

    public Primitives.GeomContact? Allocate()
    {
        if (NContacts >= MaxContacts) return null;
        return Contacts[NContacts++];
    }
}
