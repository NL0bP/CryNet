// Literal port of dev/Code/CryEngine/CryAISystem/ClusterDetector.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : This is the actual class that implements the detection of
//               the different clusters in the world space
//               This system only knows about the presence of points into
//               the space and he group them into clusters

using System.Collections.Generic;

namespace CryAISystem;

public struct Cluster
{
    public struct DistancePointPair
    {
        public float first;     // DistanceSqFromCenter
        public Vec3 second;
        public DistancePointPair(float a, Vec3 b) { first = a; second = b; }
    }

    public Vec3 centerPos;
    public uint8 numberOfElements;
    public float pointsWeight;
    public DistancePointPair pointAtMaxDistanceSqFromCenter;

    public Cluster(Vec3 _pos)
    {
        centerPos = _pos;
        numberOfElements = 0;
        pointsWeight = 1.0f;
        pointAtMaxDistanceSqFromCenter = new DistancePointPair(float.MinValue, new Vec3(0, 0, 0));
    }

    public static Cluster Default()
    {
        Cluster c = new Cluster(new Vec3(0, 0, 0));
        return c;
    }
}

public class ClusterRequest : IClusterRequest
{
    public enum EClusterRequestState
    {
        eRequestState_Created,
        eRequestState_ReadyToBeProcessed,
    }

    public ClusterRequest()
    {
        m_points = new List<ClusterPoint>();
        m_state = EClusterRequestState.eRequestState_Created;
        m_maxDistanceSq = 0;
        m_totalClustersNumber = 0;
    }

    public virtual void SetNewPointInRequest(uint pointId, Vec3 location)
    {
        m_points.Add(new ClusterPoint(pointId, location));
    }

    public virtual nuint GetNumberOfPoint() { return (nuint)m_points.Count; }
    public virtual void SetMaximumSqDistanceAllowedPerCluster(float maxDistanceSq) { m_maxDistanceSq = maxDistanceSq; }
    public virtual void SetCallback(System.Action<IClusterRequest> callback) { m_callback = callback; }
    public virtual ClusterPoint GetPointAt(nuint pointIndex) { return m_points[(int)pointIndex]; }
    public virtual nuint GetTotalClustersNumber() { return m_totalClustersNumber; }

    public List<ClusterPoint> m_points;
    public System.Action<IClusterRequest> m_callback;
    public EClusterRequestState m_state;
    public float m_maxDistanceSq;
    public nuint m_totalClustersNumber;
}


public class ClusterDetector : IClusterDetector
{
    private const float MIN_ACCEPTABLE_DISTANCE_DIFFERENCE_SQ = 0.1f * 0.1f;

    public class ClusterRequestPair
    {
        public uint first;
        public ClusterRequest second;
        public ClusterRequestPair(uint id, ClusterRequest req) { first = id; second = req; }
    }

    public class CurrentRequestState
    {
        public CurrentRequestState()
        {
            pCurrentRequest = null;
            currentRequestId = ~0u;
            aabbCenter = new Vec3(0, 0, 0);
            spawningPositionForNextCluster = new Vec3(0, 0, 0);
            clusters = new List<Cluster>();
        }

        public void Reset()
        {
            pCurrentRequest = null;
            clusters.Clear();
            clusters.Capacity = 0;
            aabbCenter = new Vec3(0, 0, 0);
            spawningPositionForNextCluster = new Vec3(0, 0, 0);
        }

        public ClusterRequest pCurrentRequest;
        public uint currentRequestId;
        public List<Cluster> clusters;
        public Vec3 aabbCenter;
        public Vec3 spawningPositionForNextCluster;
    }

    public enum EStatus
    {
        eStatus_Waiting = 0,
        eStatus_ClusteringInProgress,
        eStatus_ClusteringCompleted,
    }

    public ClusterDetector()
    {
        m_internalState = new CurrentRequestState();
        m_nextUniqueRequestID = 0;
        m_status = EStatus.eStatus_Waiting;
        m_requests = new LinkedList<ClusterRequestPair>();
    }

    public virtual KeyValuePair<uint, IClusterRequest> CreateNewRequest()
    {
        uint uniqueId = GenerateUniqueClusterRequestId();
        ClusterRequestPair pair = new ClusterRequestPair(uniqueId, new ClusterRequest());
        m_requests.AddLast(pair);
        return new KeyValuePair<uint, IClusterRequest>(uniqueId, pair.second);
    }

    public virtual void QueueRequest(uint requestId)
    {
        var node = m_requests.First;
        while (node != null)
        {
            if (node.Value.first == requestId)
            {
                if (node.Value.second.GetNumberOfPoint() != 0)
                {
                    node.Value.second.m_state = ClusterRequest.EClusterRequestState.eRequestState_ReadyToBeProcessed;
                }
                else
                {
                    // If an empty request is queued, let's remove it
                    m_requests.Remove(node);
                }

                return;
            }
            node = node.Next;
        }
    }

    public void Reset()
    {
        m_internalState.Reset();
        m_requests.Clear();
        m_nextUniqueRequestID = 0;
        m_status = EStatus.eStatus_Waiting;
    }

    private uint GenerateUniqueClusterRequestId()
    {
        return ++m_nextUniqueRequestID;
    }

    public void Update(float frameDeltaTime)
    {
        if (m_requests.Count == 0)
            return;

        if (m_status == EStatus.eStatus_Waiting)
        {
            bool hasRequestReadyToProcess = InitializeNextRequest();
            if (!hasRequestReadyToProcess)
                return;

            m_status = EStatus.eStatus_ClusteringInProgress;
        }
        // At this point a current request should always be set
        System.Diagnostics.Debug.Assert(m_internalState.pCurrentRequest != null);

        if (m_status == EStatus.eStatus_ClusteringInProgress)
        {
            /*
                The clustering algorithm used is K-Mean.
            */

            while (true)
            {
                bool isFurtherRefinementNeeded = false;
                while (true)
                {
                    ResetClustersElements();
                    isFurtherRefinementNeeded = ExecuteClusteringStep();
                    UpdateClustersCenter();
                    if (!isFurtherRefinementNeeded)
                        break;
                }

                if (IsNewClusterNeeded())
                    AddNewCluster();
                else
                    break;
            }

            m_status = EStatus.eStatus_ClusteringCompleted;
        }

        if (m_status == EStatus.eStatus_ClusteringCompleted)
        {
            m_internalState.pCurrentRequest.m_totalClustersNumber = (nuint)m_internalState.clusters.Count;
            m_internalState.pCurrentRequest.m_callback?.Invoke(m_internalState.pCurrentRequest);

            m_internalState.Reset();
            m_requests.RemoveFirst();

            m_status = EStatus.eStatus_Waiting;
        }
    }

    private bool IsNewClusterNeeded()
    {
        bool isNewClusterNeeded = false;
        Cluster.DistancePointPair furtherPointFromAClusterCenter = new Cluster.DistancePointPair(float.MinValue, new Vec3(0, 0, 0));
        for (int i = 0; i < m_internalState.clusters.Count; ++i)
        {
            Cluster it = m_internalState.clusters[i];
            if (it.pointAtMaxDistanceSqFromCenter.first > m_internalState.pCurrentRequest.m_maxDistanceSq &&
                it.pointAtMaxDistanceSqFromCenter.first > furtherPointFromAClusterCenter.first)
            {
                furtherPointFromAClusterCenter = it.pointAtMaxDistanceSqFromCenter;
                isNewClusterNeeded = true;
            }
        }
        m_internalState.spawningPositionForNextCluster = furtherPointFromAClusterCenter.second;
        return isNewClusterNeeded;
    }

    private bool ExecuteClusteringStep()
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        bool isFurtherRefinementNeeded = false;
        List<ClusterPoint> points = m_internalState.pCurrentRequest.m_points;
        for (int pi = 0; pi < points.Count; ++pi)
        {
            ClusterPoint pointIt = points[pi];
            float minDistanceSq = float.MaxValue;
            nuint bestClusterId = ~(nuint)0;
            for (int ci = 0; ci < m_internalState.clusters.Count; ++ci)
            {
                Cluster clusterIt = m_internalState.clusters[ci];
                Vec3 clusterCenter = clusterIt.centerPos;
                float dx = clusterCenter.x - pointIt.pos.x;
                float dy = clusterCenter.y - pointIt.pos.y;
                float dz = clusterCenter.z - pointIt.pos.z;
                float curDistanceSq = dx * dx + dy * dy + dz * dz;

                if ((minDistanceSq - curDistanceSq) > MIN_ACCEPTABLE_DISTANCE_DIFFERENCE_SQ)
                {
                    minDistanceSq = curDistanceSq;
                    bestClusterId = (nuint)ci;
                }
            }
            if (pointIt.clusterId != (uint)bestClusterId)
            {
                isFurtherRefinementNeeded = true;
                pointIt.clusterId = (uint)bestClusterId;
                points[pi] = pointIt;
            }
            System.Diagnostics.Debug.Assert((nuint)pointIt.clusterId < (nuint)m_internalState.clusters.Count);
            Cluster clusterForCurrentPoint = m_internalState.clusters[(int)pointIt.clusterId];
            if ((minDistanceSq - clusterForCurrentPoint.pointAtMaxDistanceSqFromCenter.first) >= 0.0f)
            {
                clusterForCurrentPoint.pointAtMaxDistanceSqFromCenter = new Cluster.DistancePointPair(minDistanceSq, pointIt.pos);
            }

            ++(clusterForCurrentPoint.numberOfElements);
            m_internalState.clusters[(int)pointIt.clusterId] = clusterForCurrentPoint;
        }

        return isFurtherRefinementNeeded;
    }

    private ClusterRequestPair ChooseRequestToServe()
    {
        var node = m_requests.First;
        while (node != null)
        {
            if (node.Value.second.m_state == ClusterRequest.EClusterRequestState.eRequestState_ReadyToBeProcessed)
            {
                return node.Value;
            }
            node = node.Next;
        }
        return null;
    }

    private nuint CalculateRuleOfThumbClusterSize(nuint numberOfPoints)
    {
        nuint halfPoints = numberOfPoints >> 1;
        float approxSqrtHalfPoints = (float)System.Math.Sqrt((float)halfPoints);
        return (nuint)(approxSqrtHalfPoints + 0.5f);
    }

    private bool InitializeNextRequest()
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        m_internalState.Reset();

        ClusterRequestPair pCurrentRequestPair = ChooseRequestToServe();
        if (pCurrentRequestPair == null)
            return false;

        m_internalState.currentRequestId = pCurrentRequestPair.first;
        m_internalState.pCurrentRequest = pCurrentRequestPair.second;

        // Calculating the center of the aabb containing all the points of the request
        List<ClusterPoint> points = m_internalState.pCurrentRequest.m_points;
        float positionsWeight = 1.0f / points.Count;
        for (int pi = 0; pi < points.Count; ++pi)
        {
            m_internalState.aabbCenter = m_internalState.aabbCenter + points[pi].pos;
        }
        m_internalState.aabbCenter = m_internalState.aabbCenter * positionsWeight;

        nuint startupClusterNumber = (nuint)System.Math.Max((long)CalculateRuleOfThumbClusterSize((nuint)m_internalState.pCurrentRequest.m_points.Count), 1L);
        m_internalState.clusters.Capacity = (int)(2 * startupClusterNumber);
        m_internalState.clusters = new List<Cluster>((int)startupClusterNumber);
        for (nuint i = 0; i < startupClusterNumber; ++i) m_internalState.clusters.Add(Cluster.Default());

        InitializeClusters();
        return true;
    }

    private void InitializeClusters()
    {
        float spreadAngleUnit = (2.0f * (float)System.Math.PI) / m_internalState.clusters.Count;
        for (int i = 0; i < m_internalState.clusters.Count; ++i)
        {
            int clusterIndex = i;
            Vec3 pos = CalculateInitialClusterPosition((nuint)clusterIndex, spreadAngleUnit);
            Cluster c = m_internalState.clusters[i];
            c.centerPos = pos;
            m_internalState.clusters[i] = c;
        }
    }

    private Vec3 CalculateInitialClusterPosition(nuint clusterIndex, float spreadAngleUnit)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        Vec3 offset = new Vec3(2.0f, 0.0f, 0.0f);
        // offset = offset.GetRotated(Vec3Constants<float>::fVec3_OneZ, spreadAngleUnit * clusterIndex);
        // (port: Vec3.GetRotated pending — leaving as offset for now)
        return m_internalState.aabbCenter + offset;
    }

    private void UpdateClustersCenter()
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        for (int i = 0; i < m_internalState.clusters.Count; ++i)
        {
            Cluster c = m_internalState.clusters[i];
            if (c.numberOfElements == 0)
            {
                continue;
            }
            c.centerPos = new Vec3(0, 0, 0);
            c.pointsWeight = 1.0f / c.numberOfElements;
            m_internalState.clusters[i] = c;
        }

        List<ClusterPoint> points = m_internalState.pCurrentRequest.m_points;
        for (int pi = 0; pi < points.Count; ++pi)
        {
            ClusterPoint pointIt = points[pi];
            Cluster c = m_internalState.clusters[(int)pointIt.clusterId];
            c.centerPos = c.centerPos + pointIt.pos * c.pointsWeight;
            m_internalState.clusters[(int)pointIt.clusterId] = c;
        }
    }

    private void ResetClustersElements()
    {
        for (int i = 0; i < m_internalState.clusters.Count; ++i)
        {
            Cluster c = m_internalState.clusters[i];
            c.numberOfElements = 0;
            c.pointsWeight = 1.0f;
            c.pointAtMaxDistanceSqFromCenter = new Cluster.DistancePointPair(float.MinValue, new Vec3(0, 0, 0));
            m_internalState.clusters[i] = c;
        }
    }

    private bool AddNewCluster()
    {
        m_internalState.clusters.Add(Cluster.Default());
        if (m_internalState.spawningPositionForNextCluster.x != 0 || m_internalState.spawningPositionForNextCluster.y != 0 || m_internalState.spawningPositionForNextCluster.z != 0)
        {
            int last = m_internalState.clusters.Count - 1;
            Cluster c = m_internalState.clusters[last];
            c.centerPos = m_internalState.spawningPositionForNextCluster;
            m_internalState.clusters[last] = c;
        }
        else
        {
            InitializeClusters();
        }
        return true;
    }

    private CurrentRequestState m_internalState;
    private uint m_nextUniqueRequestID;
    private LinkedList<ClusterRequestPair> m_requests;
    private EStatus m_status;
}
