// Literal port of dev/Code/CryEngine/CryAISystem/CollisionAvoidance/CollisionAvoidanceSystem.h
// .cpp impl (~1228L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.CollisionAvoidance;

public class CollisionAvoidanceSystem : ICollisionAvoidanceSystem
{
    public CollisionAvoidanceSystem() { /* impl in .cpp */ }

    public virtual AgentID CreateAgent(uint objectID) { return new AgentID(); /* impl in .cpp */ }
    public virtual ObstacleID CreateObstable() { return new ObstacleID(); /* impl in .cpp */ }

    public virtual void RemoveAgent(AgentID agentID) { /* impl in .cpp */ }
    public virtual void RemoveObstacle(ObstacleID obstacleID) { /* impl in .cpp */ }

    public virtual void SetAgent(AgentID agentID, Agent parameters) { /* impl in .cpp */ }
    public virtual Agent GetAgent(AgentID agentID) { return new Agent(); /* impl in .cpp */ }

    public virtual void SetObstacle(ObstacleID obstacleID, Obstacle parameters) { /* impl in .cpp */ }
    public virtual Obstacle GetObstacle(ObstacleID obstacleID) { return new Obstacle(); /* impl in .cpp */ }

    public virtual Vec2 GetAvoidanceVelocity(AgentID agentID) { return new Vec2(0, 0); /* impl in .cpp */ }

    public virtual void Reset(bool bUnload = false) { /* impl in .cpp */ }
    public virtual void Update(float updateTime) { /* impl in .cpp */ }

    public virtual void DebugDraw() { /* impl in .cpp */ }

    private float LeftOf(Vec2 line, Vec2 point) { return 0; /* impl in .cpp */ }

    private struct ConstraintLine
    {
        public enum EOrigin
        {
            AgentConstraint = 0,
            ObstacleConstraint = 1,
        }

        public Vec2 direction;
        public Vec2 point;

        public ushort flags;
        public ushort objectID;
    }

    private struct NearbyAgent
    {
        public NearbyAgent(float _distanceSq, ushort _agentID, ushort _flags = 0)
        { distanceSq = _distanceSq; agentID = _agentID; flags = _flags; }

        public enum EFlags
        {
            CanSeeMe = 1 << 0,
            IsMoving = 1 << 1,
        }

        public static bool operator <(NearbyAgent a, NearbyAgent b) { return a.distanceSq < b.distanceSq; }
        public static bool operator >(NearbyAgent a, NearbyAgent b) { return a.distanceSq > b.distanceSq; }

        public float distanceSq;
        public ushort agentID;
        public ushort flags;
    }

    private struct NearbyObstacle
    {
        public NearbyObstacle(float _distanceSq, ushort _agentID, ushort _flags = 0)
        { distanceSq = _distanceSq; obstacleID = _agentID; flags = _flags; }

        public enum EFlags
        {
            CanSeeMe = 1 << 0,
            IsMoving = 1 << 1,
        }

        public static bool operator <(NearbyObstacle a, NearbyObstacle b) { return a.distanceSq < b.distanceSq; }
        public static bool operator >(NearbyObstacle a, NearbyObstacle b) { return a.distanceSq > b.distanceSq; }

        public float distanceSq;
        public ushort obstacleID;
        public ushort flags;
    }

    private const int FeasibleAreaMaxVertexCount = 64;

    private class NearbyAgents : List<NearbyAgent> { }
    private class NearbyObstacles : List<NearbyObstacle> { }
    private class ConstraintLines : List<ConstraintLine> { }

    private nuint ComputeNearbyAgents(Agent agent, nuint agentIndex, float range, NearbyAgents nearbyAgents) { return 0; /* impl in .cpp */ }
    private nuint ComputeNearbyObstacles(Agent agent, nuint agentIndex, float range, NearbyObstacles nearbyObstacles) { return 0; /* impl in .cpp */ }

    private nuint ComputeConstraintLinesForAgent(Agent agent, nuint agentIndex, float timeHorizonScale,
        NearbyAgents nearbyAgents, nuint maxAgentsConsidered, NearbyObstacles nearbyObstacles, ConstraintLines lines) { return 0; /* impl in .cpp */ }
    private void ComputeAgentConstraintLine(Agent agent, Agent obstacleAgent, bool reciprocal, float timeHorizonScale, ref ConstraintLine line) { /* impl in .cpp */ }
    private void ComputeObstacleConstraintLine(Agent agent, Obstacle obstacle, float timeHorizonScale, ref ConstraintLine line) { /* impl in .cpp */ }

    private bool ClipPolygon(Vec2[] polygon, nuint vertexCount, ConstraintLine line, Vec2[] output, out nuint outputVertexCount) { outputVertexCount = 0; return false; /* impl in .cpp */ }
    private nuint ComputeFeasibleArea(ConstraintLine[] lines, nuint lineCount, float radius, Vec2[] feasibleArea) { return 0; /* impl in .cpp */ }
    private bool ClipVelocityByFeasibleArea(Vec2 velocity, Vec2[] feasibleArea, nuint vertexCount, out Vec2 output) { output = velocity; return false; /* impl in .cpp */ }

    private struct CandidateVelocity
    {
        public float distanceSq;
        public Vec2 velocity;

        public static bool operator <(CandidateVelocity a, CandidateVelocity b) { return a.distanceSq < b.distanceSq; }
        public static bool operator >(CandidateVelocity a, CandidateVelocity b) { return a.distanceSq > b.distanceSq; }
    }

    private nuint ComputeOptimalAvoidanceVelocity(Vec2[] feasibleArea, nuint vertexCount, Agent agent,
         float minSpeed, float maxSpeed, CandidateVelocity[] output) { return 0; /* impl in .cpp */ }

    private Vec2 ClampSpeedWithNavigationMesh(NavigationAgentTypeID agentTypeID, Vec3 agentPosition, Vec2 currentVelocity, Vec2 velocityToClamp) { return velocityToClamp; /* impl in .cpp */ }
    private bool FindFirstWalkableVelocity(AgentID agentID, CandidateVelocity[] candidates, nuint candidateCount, out Vec2 output) { output = new Vec2(0, 0); return false; /* impl in .cpp */ }

    private bool FindLineCandidate(ConstraintLine[] lines, nuint lineCount, nuint lineNumber, float radius, Vec2 velocity, out Vec2 candidate) { candidate = velocity; return false; /* impl in .cpp */ }
    private bool FindCandidate(ConstraintLine[] lines, nuint lineCount, float radius, Vec2 velocity, out Vec2 candidate) { candidate = velocity; return false; /* impl in .cpp */ }

    private void DebugDrawConstraintLine(Vec3 agentLocation, ConstraintLine line, ColorB color) { /* impl in .cpp */ }

    private List<Agent> m_agents = new List<Agent>();
    private List<Vec2> m_agentAvoidanceVelocities = new List<Vec2>();
    private List<Obstacle> m_obstacles = new List<Obstacle>();
    private NearbyAgents m_nearbyAgents = new NearbyAgents();
    private NearbyObstacles m_nearbyObstacles = new NearbyObstacles();
    private ConstraintLines m_constraintLines = new ConstraintLines();
    private List<uint> m_agentObjectIDs = new List<uint>();
    private List<string> m_agentNames = new List<string>();
}

// Forward decls / shells from ICollisionAvoidanceSystem.h
public interface ICollisionAvoidanceSystem
{
    AgentID CreateAgent(uint objectID);
    ObstacleID CreateObstable();
    void RemoveAgent(AgentID agentID);
    void RemoveObstacle(ObstacleID obstacleID);
    void SetAgent(AgentID agentID, Agent parameters);
    Agent GetAgent(AgentID agentID);
    void SetObstacle(ObstacleID obstacleID, Obstacle parameters);
    Obstacle GetObstacle(ObstacleID obstacleID);
    Vec2 GetAvoidanceVelocity(AgentID agentID);
    void Reset(bool bUnload = false);
    void Update(float updateTime);
    void DebugDraw();
}

public struct AgentID { public uint id; }
public struct ObstacleID { public uint id; }
public class Agent { }
public class Obstacle { }
