// Literal port of dev/Code/CryEngine/CryCommon/IClusterDetector.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// typedef uint32 ClusterPointProperties;
// typedef uint32 ClusterId;

public struct ClusterPoint
{
    public ClusterPoint(uint _id, Vec3 _pos)
    {
        pos = _pos;
        clusterId = ~0u;
        pointId = _id;
    }

    public Vec3 pos;
    public uint clusterId;
    // This is needed for the requesters to be able
    // to match the point with the entity they have
    public uint pointId;
}

public interface IClusterRequest
{
    // typedef Functor1<IClusterRequest*> Callback;
    // <interfuscator:shuffle>
    // virtual ~IClusterRequest() {};
    void SetNewPointInRequest(uint pointId, Vec3 location);
    void SetCallback(System.Action<IClusterRequest> callback);
    nuint GetNumberOfPoint();
    void SetMaximumSqDistanceAllowedPerCluster(float maxDistance);
    ClusterPoint GetPointAt(nuint pointIndex);
    nuint GetTotalClustersNumber();
    // </interfuscator:shuffle>
}


public interface IClusterDetector
{
    // typedef unsigned int ClusterRequestID;
    // typedef std::pair<ClusterRequestID, IClusterRequest*> IClusterRequestPair;
    // <interfuscator:shuffle>
    // virtual ~IClusterDetector() {}
    System.Collections.Generic.KeyValuePair<uint, IClusterRequest> CreateNewRequest();
    void QueueRequest(uint requestId);
    // </interfuscator:shuffle>
}
