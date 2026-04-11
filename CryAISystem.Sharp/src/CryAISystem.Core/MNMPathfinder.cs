// Literal port of dev/Code/CryEngine/CryAISystem/MNMPathfinder.h (public CMNMPathfinder class only).
// MNM::PathfinderUtils inner namespace types deferred along with .cpp impl (831L) — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public class CMNMPathfinder : IMNMPathfinder
{
    public CMNMPathfinder() { /* impl in .cpp */ }
    // ~CMNMPathfinder();

    public void Reset() { /* impl in .cpp */ }

    public virtual QueuedPathID RequestPathTo(IAIPathAgent pRequester, MNMPathRequest request) { return new QueuedPathID(); /* impl in .cpp */ }
    public virtual void CancelPathRequest(QueuedPathID requestId) { /* impl in .cpp */ }

    public void WaitForJobsToFinish() { /* impl in .cpp */ }

    public bool CheckIfPointsAreOnStraightWalkableLine(NavigationMeshID meshID, Vec3 source, Vec3 destination, float heightOffset = 0.2f) { return false; /* impl in .cpp */ }

    public void Update() { /* impl in .cpp */ }

    public void SetupNewValidPathRequests() { /* impl in .cpp */ }
    public void DispatchResults() { /* impl in .cpp */ }

    public nuint GetRequestQueueSize() { return 0; /* impl in .cpp */ }

    public void OnNavigationMeshChanged(NavigationMeshID meshId, MNM_TileID tileId) { /* impl in .cpp */ }

    public virtual System.ValueTuple<uint, Vec3, Vec3, Vec3> GetCurrentNavTriangle(IAIPathAgent pRequester, NavigationAgentTypeID agentTypeID) { return (0, new Vec3(0, 0, 0), new Vec3(0, 0, 0), new Vec3(0, 0, 0)); /* impl in .cpp */ }

    private QueuedPathID QueuePathRequest(IAIPathAgent pRequester, MNMPathRequest request) { return new QueuedPathID(); /* impl in .cpp */ }

    private void SpawnJobs() { /* impl in .cpp */ }
    // (port: ProcessingContext-related private members deferred along with MNM::PathfinderUtils namespace)

    private void DebugAllStatistics() { /* impl in .cpp */ }

    // friend functions
    public static void ProcessPathRequestJob(object pProcessingContext) { /* impl in .cpp */ }
    public static void ConstructPathIfWayWasFoundJob(object pProcessingContext) { /* impl in .cpp */ }
}

// Forward decls / shells
public interface IMNMPathfinder
{
    QueuedPathID RequestPathTo(IAIPathAgent pRequester, MNMPathRequest request);
    void CancelPathRequest(QueuedPathID requestId);
}
public struct MNM_TileID { public uint id; }
