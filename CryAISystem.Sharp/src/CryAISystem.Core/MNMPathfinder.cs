// Literal port of dev/Code/CryEngine/CryAISystem/MNMPathfinder.h + MNMPathfinder.cpp (831L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.AILog;
using CryAISystem.CryCommon;
using NavMNM = CryAISystem.Navigation.MNM;
using CryAISystem.Navigation.NavigationSystem;
namespace CryAISystem;

public class CMNMPathfinder : IMNMPathfinder
{
    //===================================================================
    // Constructor
    //===================================================================
    public CMNMPathfinder()
    {
        m_pathfindingFailedEventsToDispatch = new List<PathfindingFailedEvent>();
        m_pathfindingFailedEventsToDispatch.Capacity = gAIEnv.CVars.MNMPathfinderConcurrentRequests;
        m_pathfindingCompletedEventsToDispatch = new List<PathfindingCompletedEvent>();
        m_pathfindingCompletedEventsToDispatch.Capacity = gAIEnv.CVars.MNMPathfinderConcurrentRequests;
        m_requestedPathsQueue = new RequestQueue();
        m_processingContextsPool = new ProcessingContextsPool();
    }
    // ~CMNMPathfinder();

    //===================================================================
    // Reset
    //===================================================================
    public void Reset()
    {
        WaitForJobsToFinish();

        // send failed to all requested paths before we clear this queue
        while (m_requestedPathsQueue.Count > 0)
        {
            var front = m_requestedPathsQueue.PopFront();
            PathRequestFailed(front.id, front.request);
        }

        m_processingContextsPool.Reset();
        m_pathfindingFailedEventsToDispatch.Clear();
        m_pathfindingCompletedEventsToDispatch.Clear();
    }

    //===================================================================
    // RequestPathTo
    //===================================================================
    public virtual QueuedPathID RequestPathTo(IAIPathAgent pRequester, MNMPathRequest request)
    {
        // Validate requester
        if (pRequester == null)
        {
            AIWarning("[CMNMPathfinder::QueuePathRequest] No agent specified.");
            return new QueuedPathID();
        }

        // Validate Agent Type
        string actorName = "unknown"; // pRequester.GetPathAgentName() — IAIPathAgent shell lacks method

        if (request.agentTypeID.id == 0)
        {
            AIWarning("[CMNMPathfinder::QueuePathRequest] Request from agent {0} has no NavigationType defined.", actorName);
            return new QueuedPathID();
        }

        // Validate callback
        if (request.resultCallback == null)
        {
            AIWarning("[CMNMPathfinder::QueuePathRequest] Agent {0} does not provide a result Callback", actorName);
            return new QueuedPathID();
        }

        // Validate start/end locations
        NavigationMeshID meshID = gAIEnv.pNavigationSystem != null ?
            gAIEnv.pNavigationSystem.GetEnclosingMeshID(request.agentTypeID, request.startPos) : default;

        if (meshID.id == 0)
        {
            return new QueuedPathID();
        }

        return m_requestedPathsQueue.PushBack(new QueuedRequest(pRequester, request.agentTypeID, request));
    }

    //===================================================================
    // CancelPathRequest
    //===================================================================
    public virtual void CancelPathRequest(QueuedPathID requestId)
    {
        m_processingContextsPool.CancelPathRequest(requestId);

        CancelResultDispatchingForRequest(requestId);

        m_requestedPathsQueue.Erase(requestId);
    }

    //===================================================================
    // WaitForJobsToFinish
    //===================================================================
    public void WaitForJobsToFinish()
    {
        // In C++, this waits for threaded jobs. In C# port, processing is synchronous.
    }

    //===================================================================
    // CheckIfPointsAreOnStraightWalkableLine
    //===================================================================
    public bool CheckIfPointsAreOnStraightWalkableLine(NavigationMeshID meshID, Vec3 source, Vec3 destination, float heightOffset = 0.2f)
    {
        if (meshID.id == 0)
            return false;

        NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(meshID);
        NavMNM.MeshGrid grid = mesh.grid;

        Vec3 raiseUp = new Vec3(0.0f, 0.0f, heightOffset);
        Vec3 raisedSource = source + raiseUp;

        NavMNM.vector3_t startLoc = new NavMNM.vector3_t(new NavMNM.real_t(raisedSource.x), new NavMNM.real_t(raisedSource.y), new NavMNM.real_t(raisedSource.z));
        NavMNM.vector3_t endLoc = new NavMNM.vector3_t(new NavMNM.real_t(destination.x), new NavMNM.real_t(destination.y), new NavMNM.real_t(destination.z));

        NavMNM.real_t verticalRange = new NavMNM.real_t(2.0f);
        uint triStart = grid.GetTriangleAt(startLoc, verticalRange, verticalRange);
        uint triEnd = grid.GetTriangleAt(endLoc, verticalRange, verticalRange);

        if (triStart == 0 || triEnd == 0)
            return false;

        NavMNM.MeshGrid.RaycastRequestBase raycastRequest = new NavMNM.MeshGrid.RaycastRequestBase(512);

        if (grid.RayCast(startLoc, triStart, endLoc, triEnd, raycastRequest) != NavMNM.MeshGrid.ERayCastResult.eRayCastResult_NoHit)
            return false;

        return true;
    }

    //===================================================================
    // Update
    //===================================================================
    public void Update()
    {
        if (gAIEnv.CVars.MNMPathFinderDebug != 0)
        {
            DebugAllStatistics();
        }

        DispatchResults();

        m_processingContextsPool.CleanupFinishedRequests();

        SetupNewValidPathRequests();

        SpawnJobs();
    }

    //===================================================================
    // SetupNewValidPathRequests
    //===================================================================
    public void SetupNewValidPathRequests()
    {
        int freeSlots = m_processingContextsPool.GetFreeSlotsCount();

        for (int i = 0; i < freeSlots && m_requestedPathsQueue.Count > 0; ++i)
        {
            var front = m_requestedPathsQueue.PopFront();
            QueuedPathID idQueuedRequest = front.id;
            QueuedRequest requestToServe = front.request;

            int ctxId = m_processingContextsPool.GetFirstAvailableContextId();
            if (ctxId >= 0)
            {
                ProcessingContext processingContext = m_processingContextsPool.GetContextAtPosition(ctxId);
                processingContext.status = ProcessingContext.EProcessingStatus.Reserved;

                bool hasSetupSucceeded = SetupForNextPathRequest(idQueuedRequest, requestToServe, processingContext);
                if (!hasSetupSucceeded)
                {
                    m_processingContextsPool.ReleaseContext(ctxId);
                    m_pathfindingFailedEventsToDispatch.Add(new PathfindingFailedEvent(idQueuedRequest, requestToServe));
                }
                else
                {
                    processingContext.status = ProcessingContext.EProcessingStatus.InProgress;
                }
            }
        }
    }

    //===================================================================
    // DispatchResults
    //===================================================================
    public void DispatchResults()
    {
        foreach (var failedEvent in m_pathfindingFailedEventsToDispatch)
        {
            PathRequestFailed(failedEvent.requestId, failedEvent.request);
        }
        m_pathfindingFailedEventsToDispatch.Clear();

        foreach (var succeeded in m_pathfindingCompletedEventsToDispatch)
        {
            succeeded.callback?.Invoke(succeeded.requestId, succeeded.eventData);
        }
        m_pathfindingCompletedEventsToDispatch.Clear();
    }

    public nuint GetRequestQueueSize() { return (nuint)m_requestedPathsQueue.Count; }

    //===================================================================
    // OnNavigationMeshChanged
    //===================================================================
    public void OnNavigationMeshChanged(NavigationMeshID meshId, MNM_TileID tileId)
    {
        // In the full implementation, this re-starts path requests affected by mesh changes.
        // Simplified: no-op for shell MNM types.
    }

    //===================================================================
    // GetCurrentNavTriangle
    //===================================================================
    public virtual System.ValueTuple<uint, Vec3, Vec3, Vec3> GetCurrentNavTriangle(IAIPathAgent pRequester, NavigationAgentTypeID agentTypeID)
    {
        // Full implementation requires MNM::MeshGrid internals.
        return (0, new Vec3(0, 0, 0), new Vec3(0, 0, 0), new Vec3(0, 0, 0));
    }

    //===================================================================
    // SetupForNextPathRequest
    //===================================================================
    private bool SetupForNextPathRequest(QueuedPathID requestID, QueuedRequest request, ProcessingContext processingContext)
    {
        ProcessingRequest processingRequest = processingContext.processingRequest;
        processingRequest.Reset();

        // Validate start/end locations
        NavigationMeshID meshID = gAIEnv.pNavigationSystem != null ?
            gAIEnv.pNavigationSystem.GetEnclosingMeshID(request.agentTypeID, request.requestParams.startPos) : default;

        if (meshID.id == 0)
        {
            AIWarning("[CMNMPathfinder::SetupForNextPathRequest] Agent is not inside a navigation volume.");
            return false;
        }

        NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(meshID);
        NavMNM.MeshGrid grid = mesh.grid;

        NavMNM.MeshGrid.Params gridParams = grid.GetParams();
        NavMNM.vector3_t origin = new NavMNM.vector3_t(new NavMNM.real_t(gridParams.origin.x), new NavMNM.real_t(gridParams.origin.y), new NavMNM.real_t(gridParams.origin.z));

        ushort agentRadiusUnits = gAIEnv.pNavigationSystem.GetAgentRadiusInVoxelUnits(request.agentTypeID);
        ushort agentHeightUnits = gAIEnv.pNavigationSystem.GetAgentHeightInVoxelUnits(request.agentTypeID);

        NavMNM.vector3_t startLocation = new NavMNM.vector3_t(new NavMNM.real_t(request.requestParams.startPos.x), new NavMNM.real_t(request.requestParams.startPos.y),
            new NavMNM.real_t(request.requestParams.startPos.z));
        NavMNM.vector3_t endLocation = new NavMNM.vector3_t(new NavMNM.real_t(request.requestParams.endPos.x), new NavMNM.real_t(request.requestParams.endPos.y),
            new NavMNM.real_t(request.requestParams.endPos.z));
        Vec3 voxelSize = grid.GetParams().voxelSize;
        NavMNM.real_t horizontalRange = NavMNM.MNMUtils.CalculateMinHorizontalRange(agentRadiusUnits, voxelSize.x);
        NavMNM.real_t verticalRange = NavMNM.MNMUtils.CalculateMinVerticalRange(agentHeightUnits, voxelSize.z);

        AgentType agentTypeProperties;
        bool arePropertiesValid = gAIEnv.pNavigationSystem.GetAgentTypeProperties(request.agentTypeID, out agentTypeProperties);
        ushort zOffsetMultiplier = Math.Min((ushort)2, agentTypeProperties.settings.heightVoxelCount);
        NavMNM.real_t verticalUpwardRange = arePropertiesValid ? new NavMNM.real_t(zOffsetMultiplier * agentTypeProperties.settings.voxelSize.z) : new NavMNM.real_t(0.0f);

        Vec3 safeStartLocation = request.requestParams.startPos;
        uint triangleStartID;
        triangleStartID = grid.GetTriangleAt(startLocation - origin, verticalRange, verticalUpwardRange);
        if (triangleStartID == 0)
        {
            NavMNM.real_t distSq = new NavMNM.real_t(0);
            NavMNM.vector3_t closest = default;
            triangleStartID = grid.GetClosestTriangle(startLocation - origin, verticalRange, horizontalRange, ref distSq, ref closest);
            if (triangleStartID == 0)
            {
                AIWarning("Navigation system couldn't figure out where the start point for agent was.");
                return false;
            }
            else
            {
                safeStartLocation = closest.GetVec3();
            }
        }

        Vec3 safeEndLocation = request.requestParams.endPos;
        uint triangleEndID = grid.GetTriangleAt(endLocation - origin, verticalRange, verticalRange);
        if (triangleEndID == 0)
        {
            NavMNM.real_t distSq = new NavMNM.real_t(0);
            NavMNM.vector3_t closest = default;
            triangleEndID = grid.GetClosestTriangle(endLocation - origin, verticalRange, horizontalRange, ref distSq, ref closest);
            if (triangleEndID != 0)
            {
                safeEndLocation = closest.GetVec3();
            }
            else
            {
                AIWarning("Navigation system couldn't figure out where the destination point for agent was.");
                return false;
            }
        }

        // The data for MNM are good until this point so we can set up the path finding
        processingRequest.pRequester = request.pRequester;
        processingRequest.meshID = meshID;
        processingRequest.fromTriangleID = triangleStartID;
        processingRequest.toTriangleID = triangleEndID;
        processingRequest.queuedID = requestID;

        processingRequest.data = request;
        processingRequest.data.requestParams.startPos = safeStartLocation;
        processingRequest.data.requestParams.endPos = safeEndLocation;

        NavMNM.real_t startToEndDist = (endLocation - startLocation).lenNoOverflow();
        processingContext.workingSet.aStarOpenList.SetUpForPathSolving((uint)grid.GetTriangleCount(), triangleStartID, startLocation, startToEndDist);

        return true;
    }

    //===================================================================
    // SpawnJobs
    //===================================================================
    private void SpawnJobs()
    {
        // In C# port, execute synchronously
        foreach (var ctx in m_processingContextsPool.Pool_)
        {
            switch (ctx.status)
            {
            case ProcessingContext.EProcessingStatus.InProgress:
                ProcessPathRequest(ctx);
                break;
            case ProcessingContext.EProcessingStatus.FindWayCompleted:
                ConstructPathIfWayWasFound(ctx);
                break;
            }
        }
    }

    //===================================================================
    // ProcessPathRequest
    //===================================================================
    private void ProcessPathRequest(ProcessingContext processingContext)
    {
        if (processingContext.status != ProcessingContext.EProcessingStatus.InProgress)
            return;

        processingContext.workingSet.aStarOpenList.ResetConsumedTimeDuringCurrentFrame();

        ProcessingRequest processingRequest = processingContext.processingRequest;

        NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(processingRequest.meshID);
        NavMNM.MeshGrid grid = mesh.grid;
        NavMNM.MeshGrid.Params gridParams = grid.GetParams();
        NavMNM.OffMeshNavigation meshOffMeshNav = gAIEnv.pNavigationSystem.GetOffMeshNavigationManager().GetOffMeshNavigationForMesh(processingRequest.meshID);

        NavMNM.MeshGrid.WayQueryRequest inputParams = new NavMNM.MeshGrid.WayQueryRequest(
            processingRequest.pRequester,
            processingRequest.fromTriangleID,
            new NavMNM.vector3_t(processingRequest.data.requestParams.startPos - gridParams.origin),
            processingRequest.toTriangleID,
            new NavMNM.vector3_t(processingRequest.data.requestParams.endPos - gridParams.origin),
            meshOffMeshNav ?? new NavMNM.OffMeshNavigation(),
            processingRequest.data.GetDangersInfos());

        if (grid.FindWay(inputParams, processingContext.workingSet, processingContext.queryResult) == NavMNM.MeshGrid.EWayQueryResult.eWQR_Continuing)
            return;

        processingContext.status = ProcessingContext.EProcessingStatus.FindWayCompleted;
        return;
    }

    //===================================================================
    // ConstructPathIfWayWasFound
    //===================================================================
    private void ConstructPathIfWayWasFound(ProcessingContext processingContext)
    {
        if (processingContext.status != ProcessingContext.EProcessingStatus.FindWayCompleted)
            return;

        ProcessingRequest processingRequest = processingContext.processingRequest;

        NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(processingRequest.meshID);
        NavMNM.MeshGrid grid = mesh.grid;
        NavMNM.MeshGrid.Params gridParams = grid.GetParams();
        NavMNM.OffMeshNavigation meshOffMeshNav = gAIEnv.pNavigationSystem.GetOffMeshNavigationManager().GetOffMeshNavigationForMesh(processingRequest.meshID);

        // CPathHolder<PathPointDescriptor> outputPath
        List<PathPointDescriptor> outputPath = new List<PathPointDescriptor>();

        PathPointDescriptor navPathStart = new PathPointDescriptor(IAISystem_ENavigationType.NAV_UNSET, processingRequest.data.requestParams.startPos);
        PathPointDescriptor navPathEnd = new PathPointDescriptor(IAISystem_ENavigationType.NAV_UNSET, processingRequest.data.requestParams.endPos);

        NavMNM.vector3_t origin = new NavMNM.vector3_t(new NavMNM.real_t(gridParams.origin.x), new NavMNM.real_t(gridParams.origin.y), new NavMNM.real_t(gridParams.origin.z));

        // NOTE: waypoints are in reverse order
        int waySize = processingContext.queryResult.GetWaySize();
        NavMNM.WayTriangleData[] outputWay = processingContext.queryResult.GetWayData();
        for (int i = 0; i < waySize; ++i)
        {
            // Using the edge-midpoints of adjacent triangles to build the path.
            if (i > 0)
            {
                Vec3 edgeMidPoint = new Vec3(0, 0, 0);
                if (grid.CalculateMidEdge(outputWay[i - 1].triangleID, outputWay[i].triangleID, ref edgeMidPoint))
                {
                    PathPointDescriptor pathPoint = new PathPointDescriptor(IAISystem_ENavigationType.NAV_UNSET, edgeMidPoint + origin.GetVec3());
                    pathPoint.iTriId = outputWay[i].triangleID;
                    outputPath.Insert(0, pathPoint);
                }
            }

            if (outputWay[i].offMeshLinkID != 0)
            {
                // Grab off-mesh link object
                NavMNM.OffMeshLink pOffMeshLink = meshOffMeshNav != null ? meshOffMeshNav.GetObjectLinkInfo(outputWay[i].offMeshLinkID) : null;
                if (pOffMeshLink == null)
                {
                    // Link can no longer be found; this path is now invalid
                    processingContext.queryResult.Clear();
                    break;
                }

                bool isOffMeshLinkSmartObject = pOffMeshLink.GetLinkType() == NavMNM.OffMeshLink.LinkType.eLinkType_SmartObject;
                IAISystem_ENavigationType type = isOffMeshLinkSmartObject ? IAISystem_ENavigationType.NAV_SMARTOBJECT : IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION;

                // Add Entry/Exit points
                PathPointDescriptor pathPoint = new PathPointDescriptor();
                pathPoint.navType = IAISystem_ENavigationType.NAV_UNSET;
                pathPoint.offMeshLinkData.meshID = processingRequest.meshID.id;

                // Add Exit point
                pathPoint.vPos = pOffMeshLink.GetEndPosition();
                outputPath.Insert(0, pathPoint);

                // Add Entry point
                PathPointDescriptor entryPoint = new PathPointDescriptor();
                entryPoint.navType = type;
                entryPoint.vPos = pOffMeshLink.GetStartPosition();
                entryPoint.offMeshLinkData.meshID = processingRequest.meshID.id;
                entryPoint.offMeshLinkData.offMeshLinkID = outputWay[i].offMeshLinkID;
                entryPoint.iTriId = outputWay[i].triangleID;
                outputPath.Insert(0, entryPoint);
            }
        }
        bool pathFound = (processingContext.queryResult.GetWaySize() != 0);

        PathfindingCompletedEvent successEvent = new PathfindingCompletedEvent();
        MNMPathRequestResult resultData = successEvent.eventData;
        if (pathFound)
        {
            CNavPath navPath = resultData.pPath;
            resultData.result = EMNMPathResult.eMNMPR_Success;
            navPath.Clear("CMNMPathfinder::ProcessPathRequest");
            // Insert start/end locations in the path
            outputPath.Add(navPathEnd);
            outputPath.Insert(0, navPathStart);

            // C++ checks requestParams.beautify (defaults to true); field not available on MNMPathRequest (PipeUser.cs).
            // Faithfully default to true, gated only by CVar.
            if (gAIEnv.CVars.BeautifyPath != 0)
            {
                PullPathOnNavigationMesh(outputPath, processingRequest.meshID, (ushort)gAIEnv.CVars.PathStringPullingIterations, outputWay, processingContext.queryResult.GetWaySize());
            }

            // FillNavPath — push all points into the CNavPath
            foreach (var pp in outputPath)
            {
                navPath.PushBack(pp);
            }
            if (navPath.Empty())
            {
                navPath.PushBack(outputPath[outputPath.Count - 1], true);
            }

            MNMPathRequest requestParams = processingRequest.data.requestParams;
            SNavPathParams pparams = new SNavPathParams();
            pparams.start = requestParams.startPos;
            pparams.end = requestParams.endPos;
            pparams.startDir = new Vec3(0.0f, 0.0f, 0.0f);
            pparams.endDir = requestParams.endDir;
            pparams.nForceBuildingID = requestParams.forceTargetBuildingId;
            pparams.allowDangerousDestination = requestParams.allowDangerousDestination;
            pparams.endDistance = requestParams.endDistance;
            pparams.meshID = processingRequest.meshID;

            navPath.SetParams(pparams);
            // CNavPath doesn't expose SetEndDir in the partial class; store in params endDir
        }
        else
        {
            resultData.result = EMNMPathResult.eMNMPR_NoPathFound;
        }

        successEvent.callback = processingRequest.data.requestParams.resultCallback;
        successEvent.requestId = processingRequest.queuedID;

        processingRequest.Reset();
        processingContext.workingSet.aStarOpenList.PathSolvingDone();

        m_pathfindingCompletedEventsToDispatch.Add(successEvent);

        processingContext.status = ProcessingContext.EProcessingStatus.Completed;
        return;
    }

    // Literal port of CPathHolder<PathPointDescriptor>::PullPathOnNavigationMesh
    private static void PullPathOnNavigationMesh(List<PathPointDescriptor> path, NavigationMeshID meshID, ushort iteration, NavMNM.WayTriangleData[] way, int maxLength)
    {
        if (path.Count < 3)
            return;

        NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(meshID);
        NavMNM.MeshGrid grid = mesh.grid;
        Vec3 gridOrigin = grid.GetParams().origin;

        for (ushort iter = 0; iter < iteration; ++iter)
        {
            // Iterate in reverse order (matching C++ reverse_iterator)
            int currentPosition = 0;
            for (int idx = path.Count - 1; idx - 2 >= 0 && currentPosition < maxLength; --idx)
            {
                PathPointDescriptor startPoint = path[idx];
                PathPointDescriptor middlePoint = path[idx - 1];
                PathPointDescriptor endPoint = path[idx - 2];

                // Let's pull only the triplets that are fully not interacting with off-mesh links
                if (middlePoint.offMeshLinkData.offMeshLinkID == NavMNM.Constants.eOffMeshLinks_InvalidOffMeshLinkID &&
                    endPoint.offMeshLinkData.offMeshLinkID == NavMNM.Constants.eOffMeshLinks_InvalidOffMeshLinkID)
                {
                    Vec3 from = startPoint.vPos;
                    Vec3 to = endPoint.vPos;
                    Vec3 middle = middlePoint.vPos;

                    NavMNM.vector3_t startLoc = new NavMNM.vector3_t(from - gridOrigin);
                    NavMNM.vector3_t middleLoc = new NavMNM.vector3_t(middle - gridOrigin);
                    NavMNM.vector3_t endLoc = new NavMNM.vector3_t(to - gridOrigin);

                    grid.PullString(startLoc, way[currentPosition].triangleID, endLoc, way[currentPosition + 1].triangleID, ref middleLoc);
                    middlePoint.vPos = middleLoc.GetVec3() + gridOrigin;
                }

                // On the exit the smart object the middle point of the triangle
                // is added in the path, so when the middle point is a smart object
                // it means we are still in the previous triangle (the way is from the end to the start)
                if (middlePoint.offMeshLinkData.offMeshLinkID == NavMNM.Constants.eOffMeshLinks_InvalidOffMeshLinkID)
                    ++currentPosition;
            }
        }
    }

    //===================================================================
    // PathRequestFailed
    //===================================================================
    private void PathRequestFailed(QueuedPathID requestID, QueuedRequest request)
    {
        MNMPathRequestResult result = new MNMPathRequestResult();
        request.requestParams.resultCallback?.Invoke(requestID, result);
    }

    //===================================================================
    // CancelResultDispatchingForRequest
    //===================================================================
    private void CancelResultDispatchingForRequest(QueuedPathID requestId)
    {
        m_pathfindingFailedEventsToDispatch.RemoveAll(e => e.requestId.id == requestId.id);
        m_pathfindingCompletedEventsToDispatch.RemoveAll(e => e.requestId.id == requestId.id);
    }

    //===================================================================
    // DebugAllStatistics
    //===================================================================
    private void DebugAllStatistics()
    {
        // Debug draw — simplified for shell types.
    }

    // friend functions
    public static void ProcessPathRequestJob(object pProcessingContext) { /* no-op in C# */ }
    public static void ConstructPathIfWayWasFoundJob(object pProcessingContext) { /* no-op in C# */ }

    // ---- Inner types ----

    private class QueuedRequest
    {
        public QueuedRequest() { dangerousAreas = new NavMNM.DangerousAreasList(); }
        public QueuedRequest(IAIPathAgent requester, NavigationAgentTypeID agentType, MNMPathRequest request)
        {
            pRequester = requester;
            agentTypeID = agentType;
            requestParams = request;
            dangerousAreas = new NavMNM.DangerousAreasList();
        }
        public IAIPathAgent pRequester;
        public NavigationAgentTypeID agentTypeID;
        public MNMPathRequest requestParams;
        private NavMNM.DangerousAreasList dangerousAreas;
        public NavMNM.DangerousAreasList GetDangersInfos() { return dangerousAreas; }
    }

    private class ProcessingRequest
    {
        public IAIPathAgent pRequester;
        public NavigationMeshID meshID;
        public uint fromTriangleID;
        public uint toTriangleID;
        public QueuedPathID queuedID;
        public QueuedRequest data = new QueuedRequest();
        public bool IsValid() { return queuedID.id != 0; }
        public void Reset() { pRequester = null; meshID = default; fromTriangleID = 0; toTriangleID = 0; queuedID = default; data = new QueuedRequest(); }
    }

    private class ProcessingContext
    {
        public enum EProcessingStatus { Invalid, Reserved, InProgress, FindWayCompleted, Completed }
        public ProcessingRequest processingRequest = new ProcessingRequest();
        public NavMNM.MeshGrid.WayQueryResult queryResult = new NavMNM.MeshGrid.WayQueryResult(512);
        public NavMNM.MeshGrid.WayQueryWorkingSet workingSet = new NavMNM.MeshGrid.WayQueryWorkingSet();
        public volatile EProcessingStatus status = EProcessingStatus.Invalid;

        public void Reset()
        {
            processingRequest.Reset();
            queryResult.Reset();
            workingSet.Reset();
            status = EProcessingStatus.Invalid;
        }

        public string GetStatusAsString()
        {
            return status switch
            {
                EProcessingStatus.Invalid => "Invalid",
                EProcessingStatus.Reserved => "Reserved",
                EProcessingStatus.InProgress => "InProgress",
                EProcessingStatus.FindWayCompleted => "FindWayCompleted",
                EProcessingStatus.Completed => "Completed",
                _ => "Error. Unknown status",
            };
        }
    }

    private class ProcessingContextsPool
    {
        public List<ProcessingContext> Pool_ = new List<ProcessingContext>();

        public ProcessingContextsPool()
        {
            int count = gAIEnv.CVars.MNMPathfinderConcurrentRequests;
            for (int i = 0; i < count; ++i)
                Pool_.Add(new ProcessingContext());
        }

        public int GetFreeSlotsCount()
        {
            int count = 0;
            foreach (var c in Pool_) if (c.status == ProcessingContext.EProcessingStatus.Invalid) count++;
            return count;
        }

        public int GetFirstAvailableContextId()
        {
            for (int i = 0; i < Pool_.Count; ++i)
                if (Pool_[i].status == ProcessingContext.EProcessingStatus.Invalid)
                    return i;
            return -1;
        }

        public ProcessingContext GetContextAtPosition(int id) { return Pool_[id]; }

        public void ReleaseContext(int id) { Pool_[id].status = ProcessingContext.EProcessingStatus.Invalid; }

        public void CancelPathRequest(QueuedPathID requestId)
        {
            foreach (var c in Pool_)
                if (c.processingRequest.queuedID.id == requestId.id)
                    c.status = ProcessingContext.EProcessingStatus.Invalid;
        }

        public void CleanupFinishedRequests()
        {
            foreach (var c in Pool_)
                if (c.status == ProcessingContext.EProcessingStatus.Completed)
                {
                    c.Reset();
                }
        }

        public void Reset()
        {
            Pool_.Clear();
            int count = gAIEnv.CVars.MNMPathfinderConcurrentRequests;
            for (int i = 0; i < count; ++i)
                Pool_.Add(new ProcessingContext());
        }
    }

    private struct PathfindingFailedEvent
    {
        public QueuedPathID requestId;
        public QueuedRequest request;
        public PathfindingFailedEvent(QueuedPathID id, QueuedRequest req) { requestId = id; request = req; }
    }

    private class PathfindingCompletedEvent
    {
        public MNMPathRequestResult eventData;
        public Action<QueuedPathID, MNMPathRequestResult> callback;
        public QueuedPathID requestId;

        public PathfindingCompletedEvent()
        {
            eventData = new MNMPathRequestResult();
            eventData.pPath = new CNavPath();
        }
    }

    private class RequestQueue
    {
        private List<(QueuedPathID id, QueuedRequest request)> m_queue = new();
        private uint m_nextId = 1;

        public int Count => m_queue.Count;

        public QueuedPathID PushBack(QueuedRequest request)
        {
            QueuedPathID id = new QueuedPathID { id = m_nextId++ };
            m_queue.Add((id, request));
            return id;
        }

        public (QueuedPathID id, QueuedRequest request) PopFront()
        {
            var front = m_queue[0];
            m_queue.RemoveAt(0);
            return front;
        }

        public void Erase(QueuedPathID id)
        {
            m_queue.RemoveAll(x => x.id.id == id.id);
        }
    }

    // ---- Fields ----
    private RequestQueue m_requestedPathsQueue;
    private ProcessingContextsPool m_processingContextsPool;
    private List<PathfindingFailedEvent> m_pathfindingFailedEventsToDispatch;
    private List<PathfindingCompletedEvent> m_pathfindingCompletedEventsToDispatch;
}

// Forward decls / shells
public interface IMNMPathfinder
{
    QueuedPathID RequestPathTo(IAIPathAgent pRequester, MNMPathRequest request);
    void CancelPathRequest(QueuedPathID requestId);
}
public struct MNM_TileID { public uint id; }
// QueuedPathID and MNMPathRequest are defined in PipeUser.cs
// MNMPathRequestResult is defined in AIActor.cs
public enum EMNMPathResult { eMNMPR_NoPathFound, eMNMPR_Success }
public class INavPathPtr { }
