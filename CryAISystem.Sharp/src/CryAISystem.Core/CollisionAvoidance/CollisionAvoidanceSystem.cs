// Literal port of dev/Code/CryEngine/CryAISystem/CollisionAvoidance/CollisionAvoidanceSystem.h + .cpp
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using CryAISystem.CryCommon;
using CryAISystem.Navigation.NavigationSystem;
using NavMNM = CryAISystem.Navigation.MNM;

namespace CryAISystem.CollisionAvoidance;

public class CollisionAvoidanceSystem : ICollisionAvoidanceSystem
{
    public CollisionAvoidanceSystem() { }

    public virtual AgentID CreateAgent(uint objectID)
    {
        uint id = (uint)m_agents.Count;
        m_agents.Add(new Agent());

        m_agentAvoidanceVelocities.Add(new Vec2(0, 0));

        m_agentObjectIDs.Add(objectID);

        m_agentNames.Add("");

        IAIObject aiObject = gAIEnv.pAIObjectManager.GetAIObject(objectID);
        m_agentNames[(int)id] = aiObject.GetName();

        return new AgentID { id = id };
    }

    public virtual ObstacleID CreateObstable()
    {
        uint id = (uint)m_obstacles.Count;
        m_obstacles.Add(new Obstacle());

        return new ObstacleID { id = id };
    }

    public virtual void RemoveAgent(AgentID agentID) { }

    public virtual void RemoveObstacle(ObstacleID obstacleID) { }

    public virtual void SetAgent(AgentID agentID, Agent parameters)
    {
        m_agents[(int)agentID.id] = parameters;
    }

    public virtual Agent GetAgent(AgentID agentID)
    {
        return m_agents[(int)agentID.id];
    }

    public virtual void SetObstacle(ObstacleID obstacleID, Obstacle parameters)
    {
        m_obstacles[(int)obstacleID.id] = parameters;
    }

    public virtual Obstacle GetObstacle(ObstacleID obstacleID)
    {
        return m_obstacles[(int)obstacleID.id];
    }

    public virtual Vec2 GetAvoidanceVelocity(AgentID agentID)
    {
        return m_agentAvoidanceVelocities[(int)agentID.id];
    }

    public virtual void Reset(bool bUnload = false)
    {
        if (bUnload)
        {
            m_agents.Clear(); m_agents.TrimExcess();
            m_agentAvoidanceVelocities.Clear(); m_agentAvoidanceVelocities.TrimExcess();
            m_obstacles.Clear(); m_obstacles.TrimExcess();

            m_agentObjectIDs.Clear(); m_agentObjectIDs.TrimExcess();
            m_agentNames.Clear(); m_agentNames.TrimExcess();

            m_constraintLines.Clear(); m_constraintLines.TrimExcess();
            m_nearbyAgents.Clear(); m_nearbyAgents.TrimExcess();
            m_nearbyObstacles.Clear(); m_nearbyObstacles.TrimExcess();
        }
        else
        {
            m_agents.Clear();
            m_agentAvoidanceVelocities.Clear();
            m_obstacles.Clear();

            m_agentObjectIDs.Clear();
            m_agentNames.Clear();
        }
    }

    public virtual void Update(float updateTime)
    {
        int index = 0;

        bool debugDraw = gAIEnv.CVars.DebugDraw > 0;
        float Epsilon = 0.00001f;
        int MaxAgentsConsidered = 8;

        for (int it = 0; it < m_agents.Count; ++it, ++index)
        {
            Agent agent = m_agents[it];

            Vec2 newVelocity = agent.desiredVelocity;
            m_agentAvoidanceVelocities[index] = newVelocity;

            float desiredSpeedSq = agent.desiredVelocity.GetLength2();
            if (desiredSpeedSq < Epsilon)
                continue;

            m_constraintLines.Clear();
            m_nearbyAgents.Clear();
            m_nearbyObstacles.Clear();

            float range = gAIEnv.CVars.CollisionAvoidanceRange;

            ComputeNearbyObstacles(agent, (nuint)index, range, m_nearbyObstacles);
            ComputeNearbyAgents(agent, (nuint)index, range, m_nearbyAgents);

            nuint obstacleConstraintCount = ComputeConstraintLinesForAgent(agent, (nuint)index, 1.0f, m_nearbyAgents, (nuint)MaxAgentsConsidered,
                m_nearbyObstacles, m_constraintLines);

            nuint agentConstraintCount = (nuint)m_constraintLines.Count - obstacleConstraintCount;
            nuint constraintCount = (nuint)m_constraintLines.Count;
            nuint considerCount = agentConstraintCount;

            if (constraintCount == 0)
            {
                m_agentAvoidanceVelocities[index] = newVelocity;
                continue;
            }

            Vec2 candidate = agent.desiredVelocity;

            Vec2[] feasibleArea = new Vec2[FeasibleAreaMaxVertexCount];
            nuint vertexCount = ComputeFeasibleArea(m_constraintLines.ToArray(), constraintCount, agent.maxSpeed,
                feasibleArea);

            float minSpeed = gAIEnv.CVars.CollisionAvoidanceMinSpeed;

            CandidateVelocity[] candidates = new CandidateVelocity[FeasibleAreaMaxVertexCount + 1];
            nuint candidateCount = ComputeOptimalAvoidanceVelocity(feasibleArea, vertexCount, agent, minSpeed, agent.maxSpeed, candidates);

            if (candidateCount == 0 || !FindFirstWalkableVelocity(new AgentID { id = (uint)index }, candidates, candidateCount, out newVelocity))
            {
                m_constraintLines.Clear();

                obstacleConstraintCount = ComputeConstraintLinesForAgent(agent, (nuint)index, 0.25f, m_nearbyAgents, considerCount,
                    m_nearbyObstacles, m_constraintLines);

                agentConstraintCount = (nuint)m_constraintLines.Count - obstacleConstraintCount;
                constraintCount = (nuint)m_constraintLines.Count;

                while (considerCount > 0)
                {
                    vertexCount = ComputeFeasibleArea(m_constraintLines.ToArray(), (nuint)m_constraintLines.Count, agent.maxSpeed,
                        feasibleArea);

                    candidateCount = ComputeOptimalAvoidanceVelocity(feasibleArea, vertexCount, agent, minSpeed, agent.maxSpeed, candidates);

                    if (candidateCount != 0 && !FindFirstWalkableVelocity(new AgentID { id = (uint)index }, candidates, candidateCount, out newVelocity))
                        break;

                    if (m_nearbyAgents.Count == 0)
                        break;

                    NearbyAgent furthestNearbyAgent = m_nearbyAgents[(int)(considerCount - 1)];
                    Agent furthestAgent = m_agents[furthestNearbyAgent.agentID];

                    if (furthestNearbyAgent.distanceSq <= sqr(agent.radius + agent.radius + furthestAgent.radius))
                        break;

                    --considerCount;
                    --constraintCount;
                }
            }

            m_agentAvoidanceVelocities[index] = newVelocity;

            if (debugDraw)
            {
                IAIObject aiObj = gAIEnv.pAIObjectManager.GetAIObject(m_agentObjectIDs[index]);
                if (aiObj != null)
                {
                    CAIActor actor = aiObj.CastToCAIActor();
                    if (actor != null)
                    {
                        if (gAIEnv.CVars.DebugDrawCollisionAvoidanceAgentName.Length > 0 &&
                            string.Equals(actor.GetName(), gAIEnv.CVars.DebugDrawCollisionAvoidanceAgentName, StringComparison.OrdinalIgnoreCase))
                        {
                            Vec3 agentLocation = actor.GetPhysicsPos();

                            CDebugDrawContext dc = new CDebugDrawContext();

                            dc.DrawCircleOutline(agentLocation, agent.maxSpeed, new ColorB(0, 0, 255, 255));

                            Vec3[] polygon3D = new Vec3[128];

                            for (nuint i = 0; i < vertexCount; ++i)
                                polygon3D[i] = new Vec3(agentLocation.x + feasibleArea[i].x, agentLocation.y + feasibleArea[i].y,
                                    agentLocation.z + 0.005f);

                            ColorB polyColor = new ColorB(255, 255, 255, 96);

                            for (nuint i = 2; i < vertexCount; ++i)
                                dc.DrawTriangle(polygon3D[0], polyColor, polygon3D[i - 1], polyColor,
                                    polygon3D[i], polyColor);

                            int fit = 0;
                            int fend = (int)constraintCount;

                            ColorB[] lineColor = new ColorB[12] {
                                new ColorB(255, 165, 0, 128),
                                new ColorB(210, 180, 140, 128),
                                new ColorB(0, 0, 128, 128),
                                new ColorB(0, 128, 0, 128),
                                new ColorB(138, 43, 226, 128),
                                new ColorB(205, 92, 92, 128),
                                new ColorB(34, 139, 34, 128),
                                new ColorB(47, 79, 79, 128),
                                new ColorB(64, 224, 208, 128),
                                new ColorB(255, 215, 0, 128),
                                new ColorB(240, 230, 140, 128),
                                new ColorB(95, 158, 160, 128),
                            };

                            for (; fit < fend; ++fit)
                            {
                                ConstraintLine line = m_constraintLines[fit];

                                ColorB color = lineColor[line.objectID % 12];

                                if ((line.flags & (ushort)ConstraintLine.EOrigin.ObstacleConstraint) != 0)
                                    color = new ColorB(128, 128, 128, 255);

                                DebugDrawConstraintLine(agentLocation, line, color);
                            }
                        }
                    }
                }
            }
        }
    }

    public virtual void DebugDraw()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        uint index = 0;

        ColorB desiredColor = new ColorB(0, 0, 0, 255);
        ColorB newColor = new ColorB(0, 100, 0, 128);

        for (int it = 0; it < m_agents.Count; ++it, ++index)
        {
            Agent agent = m_agents[it];

            IAIObject aiObj = gAIEnv.pAIObjectManager.GetAIObject(m_agentObjectIDs[(int)index]);
            if (aiObj != null)
            {
                CAIActor actor = aiObj.CastToCAIActor();
                if (actor != null)
                {
                    Vec3 agentLocation = actor.GetPhysicsPos();
                    Vec2 agentAvoidanceVelocity = m_agentAvoidanceVelocities[(int)index];

                    dc.DrawArrow(agentLocation, new Vec3(agent.desiredVelocity.x, agent.desiredVelocity.y, 0), 0.135f, desiredColor);
                    dc.DrawArrow(agentLocation, new Vec3(agentAvoidanceVelocity.x, agentAvoidanceVelocity.y, 0), 0.2f, newColor);

                    dc.DrawRangeCircle(agentLocation + new Vec3(0, 0, 0.3f), agent.radius, 0.1f,
                        new ColorB(50, 204, 153, 128), new ColorB(50, 50, 204, 255));
                }
            }
        }
    }

    private float LeftOf(Vec2 line, Vec2 point)
    {
        return line.Cross(point);
    }

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

    private nuint ComputeNearbyAgents(Agent agent, nuint agentIndex, float range, NearbyAgents nearbyAgents)
    {
        float Epsilon = 0.00001f;

        nuint nearbyAgentIndex = 0;

        Vec3 agentLocation = agent.currentLocation;
        Vec2 agentLookDirection = agent.currentLookDirection;

        for (int ait = 0; ait < m_agents.Count; ++ait, ++nearbyAgentIndex)
        {
            if (agentIndex != nearbyAgentIndex)
            {
                Agent otherAgent = m_agents[ait];

                Vec2 relativePosition = new Vec2(otherAgent.currentLocation) - new Vec2(agentLocation);
                float distanceSq = relativePosition.GetLength2();

                bool nearby = distanceSq < sqr(range + otherAgent.radius);
                bool sameFloor = fabs_tpl(otherAgent.currentLocation.z - agentLocation.z) < 2.0f;

                //Note: For some reason different agents end with the same location,
                //yet the source of the problem has to be found
                bool ignore = (distanceSq < 0.0001f);

                if (nearby && sameFloor && !ignore)
                {
                    Vec2 direction = relativePosition.GetNormalized();
                    bool isVisible = agentLookDirection.Dot(new Vec2(otherAgent.currentLocation) - (new Vec2(agentLocation) - (direction * otherAgent.radius))) > 0.0f;

                    //if (isVisible)
                    {
                        bool isMoving = otherAgent.desiredVelocity.GetLength2() >= Epsilon;
                        bool canSeeMe = true; //otherAgent.currentLookDirection.Dot(agentLocation - (otherAgent.currentLocation + (direction * agent.radius))) > 0.0f;

                        nearbyAgents.Add(new NearbyAgent(distanceSq, (ushort)nearbyAgentIndex,
                            (ushort)((canSeeMe ? (ushort)NearbyAgent.EFlags.CanSeeMe : (ushort)0)
                            | (isMoving ? (ushort)NearbyAgent.EFlags.IsMoving : (ushort)0))));
                    }
                }
            }
        }

        nearbyAgents.Sort((a, b) => a.distanceSq.CompareTo(b.distanceSq));

        return (nuint)nearbyAgents.Count;
    }

    private nuint ComputeNearbyObstacles(Agent agent, nuint agentIndex, float range, NearbyObstacles nearbyObstacles)
    {
        nuint obstacleIndex = 0;

        Vec3 agentLocation = agent.currentLocation;
        Vec2 agentLookDirection = agent.currentLookDirection;

        for (int oit = 0; oit < m_obstacles.Count; ++oit, ++obstacleIndex)
        {
            Obstacle obstacle = m_obstacles[oit];

            Vec2 relativePosition = new Vec2(obstacle.currentLocation) - new Vec2(agentLocation);
            float distanceSq = relativePosition.GetLength2();

            bool nearby = distanceSq < sqr(range + obstacle.radius);
            bool sameFloor = fabs_tpl(obstacle.currentLocation.z - agentLocation.z) < 2.0f;

            if (nearby && sameFloor)
            {
                Vec2 direction = relativePosition.GetNormalized();
                nearbyObstacles.Add(new NearbyObstacle(distanceSq, (ushort)obstacleIndex));
            }
        }

        return (nuint)nearbyObstacles.Count;
    }

    private nuint ComputeConstraintLinesForAgent(Agent agent, nuint agentIndex, float timeHorizonScale,
        NearbyAgents nearbyAgents, nuint maxAgentsConsidered, NearbyObstacles nearbyObstacles, ConstraintLines lines)
    {
        nuint obstacleCount = 0;

        for (int oit = 0; oit < nearbyObstacles.Count; ++oit)
        {
            NearbyObstacle nearbyObstacle = nearbyObstacles[oit];
            Obstacle obstacle = m_obstacles[nearbyObstacle.obstacleID];

            ConstraintLine line = new ConstraintLine();
            line.flags = (ushort)ConstraintLine.EOrigin.ObstacleConstraint;
            line.objectID = nearbyObstacle.obstacleID;

            ComputeObstacleConstraintLine(agent, obstacle, timeHorizonScale, ref line);

            lines.Add(line);
            ++obstacleCount;
        }

        int aend = (int)Math.Min((nuint)nearbyAgents.Count, maxAgentsConsidered);

        for (int ait = 0; ait < aend; ++ait)
        {
            NearbyAgent nearbyAgent = nearbyAgents[ait];
            Agent otherAgent = m_agents[nearbyAgent.agentID];

            ConstraintLine line = new ConstraintLine();
            line.objectID = nearbyAgent.agentID;
            line.flags = (ushort)ConstraintLine.EOrigin.AgentConstraint;

            if ((nearbyAgent.flags & (ushort)NearbyAgent.EFlags.IsMoving) != 0)
                ComputeAgentConstraintLine(agent, otherAgent, true, timeHorizonScale, ref line);
            else
            {
                Obstacle obstacle = new Obstacle();
                obstacle.currentLocation = otherAgent.currentLocation;
                obstacle.radius = otherAgent.radius;

                ComputeObstacleConstraintLine(agent, obstacle, timeHorizonScale, ref line);
            }

            lines.Add(line);
        }

        return obstacleCount;
    }

    private void ComputeAgentConstraintLine(Agent agent, Agent obstacleAgent, bool reciprocal, float timeHorizonScale, ref ConstraintLine line)
    {
        Vec2 relativePosition = new Vec2(obstacleAgent.currentLocation) - new Vec2(agent.currentLocation);
        Vec2 relativeVelocity = agent.currentVelocity - obstacleAgent.currentVelocity;

        float distanceSq = relativePosition.GetLength2();
        float radii = agent.radius + obstacleAgent.radius;
        float radiiSq = sqr(radii);

        float TimeHorizon = timeHorizonScale *
            (reciprocal ? gAIEnv.CVars.CollisionAvoidanceAgentTimeHorizon : gAIEnv.CVars.CollisionAvoidanceObstacleTimeHorizon);
        float TimeStep = gAIEnv.CVars.CollisionAvoidanceTimeStep;

        float invTimeHorizon = 1.0f / TimeHorizon;
        float invTimeStep = 1.0f / TimeStep;

        Vec2 u;

        if (distanceSq > radiiSq)
        {
            Vec2 cutoffCenter = relativePosition * invTimeHorizon;

            Vec2 w = relativeVelocity - cutoffCenter;
            float wLenSq = w.GetLength2();

            float dot = w.Dot(relativePosition);

            // compute closest point from relativeVelocity to the velocity object boundary
            if ((dot < 0.0f) && (sqr(dot) > radiiSq * wLenSq))
            {
                // w is pointing backwards from cone apex direction
                // closest point lies on the cutoff arc
                float wLen = sqrt_tpl(wLenSq);
                w = w / wLen;

                line.direction = new Vec2(w.y, -w.x);
                u = (radii * invTimeHorizon - wLen) * w;
            }
            else
            {
                // w is pointing into the cone
                // closest point is on an edge
                float edge = sqrt_tpl(distanceSq - radiiSq);

                if (LeftOf(relativePosition, w) > 0.0f)
                {
                    // left edge
                    line.direction = new Vec2(relativePosition.x * edge - relativePosition.y * radii,
                        relativePosition.x * radii + relativePosition.y * edge) / distanceSq;
                }
                else
                {
                    // right edge
                    line.direction = -new Vec2(relativePosition.x * edge + relativePosition.y * radii,
                        -relativePosition.x * radii + relativePosition.y * edge) / distanceSq;
                }

                float proj = relativeVelocity.Dot(line.direction);

                u = proj * line.direction - relativeVelocity;
            }
        }
        else
        {
            float distance = sqrt_tpl(distanceSq);
            Vec2 w = relativePosition / distance;

            line.direction = new Vec2(-w.y, w.x);

            Vec2 point = ((distance - radii) * invTimeStep) * w;
            float dot = (relativeVelocity - point).Dot(line.direction);

            u = point + dot * line.direction - relativeVelocity;
        }

        float effort = reciprocal ? 0.5f : 1.0f;
        line.point = agent.currentVelocity + effort * u;
    }

    private void ComputeObstacleConstraintLine(Agent agent, Obstacle obstacle, float timeHorizonScale, ref ConstraintLine line)
    {
        Vec2 relativePosition = new Vec2(obstacle.currentLocation) - new Vec2(agent.currentLocation);

        float distanceSq = relativePosition.GetLength2();
        float radii = agent.radius + obstacle.radius;
        float radiiSq = sqr(radii);

        float heuristicWeightForDistance = 0.01f;
        float minimumTimeHorizonScale = 0.25f;
        float adjustedTimeHorizonScale = max(min(timeHorizonScale, (heuristicWeightForDistance * distanceSq)), minimumTimeHorizonScale);
        float TimeHorizon = gAIEnv.CVars.CollisionAvoidanceObstacleTimeHorizon * adjustedTimeHorizonScale;
        float TimeStep = gAIEnv.CVars.CollisionAvoidanceTimeStep;

        float invTimeHorizon = 1.0f / TimeHorizon;
        float invTimeStep = 1.0f / TimeStep;

        Vec2 cutoffCenter = relativePosition * invTimeHorizon;

        if (distanceSq > radiiSq)
        {
            Vec2 w = agent.desiredVelocity - cutoffCenter;
            float wLenSq = w.GetLength2();

            float dot = w.Dot(relativePosition);

            // compute closest point from relativeVelocity to the velocity object boundary
            if ((dot < 0.0f) && (sqr(dot) > radiiSq * wLenSq))
            {
                // w is pointing backwards from cone apex direction
                // closest point lies on the cutoff arc
                float wLen = sqrt_tpl(wLenSq);
                w = w / wLen;

                line.direction = new Vec2(w.y, -w.x);
                line.point = cutoffCenter + (radii * invTimeHorizon) * w;
            }
            else
            {
                // w is pointing into the cone
                // closest point is on an edge
                float edge = sqrt_tpl(distanceSq - radiiSq);

                if (LeftOf(relativePosition, w) > 0.0f)
                {
                    // left edge
                    line.direction = new Vec2(relativePosition.x * edge - relativePosition.y * radii,
                        relativePosition.x * radii + relativePosition.y * edge) / distanceSq;
                }
                else
                {
                    // right edge
                    line.direction = -new Vec2(relativePosition.x * edge + relativePosition.y * radii,
                        -relativePosition.x * radii + relativePosition.y * edge) / distanceSq;
                }

                line.point = cutoffCenter + radii * invTimeHorizon * new Vec2(-line.direction.y, line.direction.x);
            }
        }
        else if (distanceSq > 0.00001f)
        {
            float distance = sqrt_tpl(distanceSq);
            Vec2 w = relativePosition / distance;

            line.direction = new Vec2(-w.y, w.x);

            Vec2 point = ((radii - distance) * invTimeStep) * w;
            float dot = (agent.currentVelocity - point).Dot(line.direction);
            line.point = cutoffCenter + obstacle.radius * invTimeHorizon * new Vec2(-line.direction.y, line.direction.x);
        }
        else
        {
            Vec2 w = agent.currentVelocity.GetNormalizedSafe(new Vec2(0.0f, 1.0f));
            line.direction = new Vec2(-w.y, w.x);

            float dot = agent.currentVelocity.Dot(line.direction);
            line.point = dot * line.direction - agent.currentVelocity;
        }
    }

    private bool ClipPolygon(Vec2[] polygon, nuint vertexCount, ConstraintLine line, Vec2[] output, out nuint outputVertexCount)
    {
        bool shapeChanged = false;
        int outputIndex = 0;

        Vec2 v0 = polygon[0];
        bool v0Side = line.direction.Cross(v0 - line.point) >= 0.0f;

        for (nuint i = 1; i <= vertexCount; ++i)
        {
            Vec2 v1 = polygon[i % vertexCount];
            bool v1Side = line.direction.Cross(v1 - line.point) >= 0.0f;

            if (v0Side && (v0Side == v1Side))
            {
                output[outputIndex++] = v1;
            }
            else if (v0Side != v1Side)
            {
                float det = line.direction.Cross(v1 - v0);

                if (fabs_tpl(det) >= 0.000001f)
                {
                    shapeChanged = true;

                    float detA = (v0 - line.point).Cross(v1 - v0);
                    float t = detA / det;

                    output[outputIndex++] = line.point + line.direction * t;
                }

                if (v1Side)
                    output[outputIndex++] = v1;
            }
            else
                shapeChanged = true;

            v0 = v1;
            v0Side = v1Side;
        }

        outputVertexCount = (nuint)outputIndex;

        return shapeChanged;
    }

    private nuint ComputeFeasibleArea(ConstraintLine[] lines, nuint lineCount, float radius, Vec2[] feasibleArea)
    {
        Vec2[] buf0 = new Vec2[FeasibleAreaMaxVertexCount];
        Vec2[] original = buf0;

        Vec2[] buf1 = new Vec2[FeasibleAreaMaxVertexCount];
        Vec2[] clipped = buf1;
        Vec2[] output = clipped;

        System.Diagnostics.Debug.Assert(3 + (int)lineCount <= FeasibleAreaMaxVertexCount);

        float HalfSize = 1.0f + radius;

        original[0] = new Vec2(-HalfSize, HalfSize);
        original[1] = new Vec2(HalfSize, HalfSize);
        original[2] = new Vec2(HalfSize, -HalfSize);
        original[3] = new Vec2(-HalfSize, -HalfSize);

        nuint feasibleVertexCount = 4;
        nuint outputCount = feasibleVertexCount;

        for (nuint i = 0; i < lineCount; ++i)
        {
            ConstraintLine constraint = lines[i];

            if (ClipPolygon(original, outputCount, constraint, clipped, out outputCount))
            {
                if (outputCount != 0)
                {
                    output = clipped;
                    // swap original and clipped
                    Vec2[] temp = original;
                    original = clipped;
                    clipped = temp;
                }
                else
                    return 0;
            }
        }

        Array.Copy(output, feasibleArea, (int)outputCount);

        return outputCount;
    }

    private bool ClipVelocityByFeasibleArea(Vec2 velocity, Vec2[] feasibleArea, nuint vertexCount, out Vec2 output)
    {
        if (Overlap.Point_Polygon2D(velocity, feasibleArea, (int)vertexCount))
        {
            output = velocity;
            return false;
        }

        Vec3 zeroVec = new Vec3(0, 0, 0);
        Vec3 vel3 = new Vec3(velocity.x, velocity.y, 0);

        for (nuint i = 0; i < vertexCount; ++i)
        {
            Vec2 v0 = feasibleArea[i];
            Vec2 v1 = feasibleArea[(i + 1) % vertexCount];

            float a, b;
            if (Intersect.Lineseg_Lineseg2D(new Lineseg(new Vec3(v0.x, v0.y, 0), new Vec3(v1.x, v1.y, 0)),
                new Lineseg(zeroVec, vel3), out a, out b))
            {
                output = velocity * b;
                return true;
            }
        }

        output = new Vec2(0, 0);
        return true;
    }

    private static nuint IntersectLineSegCircle(Vec2 center, float radius, Vec2 a, Vec2 b, Vec2[] output)
    {
        Vec2 v1 = b - a;
        float lineSegmentLengthSq = v1.GetLength2();
        if (lineSegmentLengthSq < 0.00001f)
            return 0;

        Vec2 v2 = center - a;
        float radiusSq = sqr(radius);

        float dot = v1.Dot(v2);
        float dotProdOverLength = (dot / lineSegmentLengthSq);
        Vec2 proj1 = new Vec2((dotProdOverLength * v1.x), (dotProdOverLength * v1.y));
        Vec2 midpt = new Vec2(a.x + proj1.x, a.y + proj1.y);

        float distToCenterSq = sqr(midpt.x - center.x) + sqr(midpt.y - center.y);
        if (distToCenterSq > radiusSq)
            return 0;

        if (fabs_tpl(distToCenterSq - radiusSq) < 0.00001f)
        {
            output[0] = midpt;
            return 1;
        }

        float distToIntersection;
        if (fabs_tpl(distToCenterSq) < 0.00001f)
            distToIntersection = radius;
        else
        {
            distToIntersection = sqrt_tpl(radiusSq - distToCenterSq);
        }

        Vec2 vIntersect = v1.GetNormalized() * distToIntersection;

        nuint resultCount = 0;
        Vec2 solution1 = midpt + vIntersect;
        if ((solution1.x - a.x) * vIntersect.x + (solution1.y - a.y) * vIntersect.y > 0.0f &&
            (solution1.x - b.x) * vIntersect.x + (solution1.y - b.y) * vIntersect.y < 0.0f)
        {
            output[resultCount++] = solution1;
        }

        Vec2 solution2 = midpt - vIntersect;
        if ((solution2.x - a.x) * vIntersect.x + (solution2.y - a.y) * vIntersect.y > 0.0f &&
            (solution2.x - b.x) * vIntersect.x + (solution2.y - b.y) * vIntersect.y < 0.0f)
        {
            output[resultCount++] = solution2;
        }

        return resultCount;
    }

    private nuint ComputeOptimalAvoidanceVelocity(Vec2[] feasibleArea, nuint vertexCount, Agent agent,
         float minSpeed, float maxSpeed, CandidateVelocity[] output)
    {
        Vec2 desiredVelocity = agent.desiredVelocity;
        Vec2 currentVelocity = agent.currentVelocity;
        if (vertexCount > 2)
        {
            Vec2 velocity;

            if (!ClipVelocityByFeasibleArea(desiredVelocity, feasibleArea, vertexCount, out velocity))
            {
                output[0].velocity = desiredVelocity;
                output[0].distanceSq = 0.0f;

                return 1;
            }
            else
            {
                float MinSpeedSq = sqr(minSpeed);
                float MaxSpeedSq = sqr(maxSpeed);

                nuint candidateCount = 0;

                float clippedSpeedSq = velocity.GetLength2();

                if (clippedSpeedSq > MinSpeedSq)
                {
                    output[candidateCount].velocity = velocity;
                    output[candidateCount].distanceSq = (velocity - desiredVelocity).GetLength2();
                    candidateCount++;
                }

                Vec2[] intersections = new Vec2[2];
                nuint intersectionCount = 0;
                Vec2 center = new Vec2(0, 0);

                for (nuint i = 0; i < vertexCount; ++i)
                {
                    Vec2 va = feasibleArea[i];
                    Vec2 vb = feasibleArea[(i + 1) % vertexCount];

                    float aLenSq = va.GetLength2();
                    if ((aLenSq <= MaxSpeedSq) && (aLenSq > .0f))
                    {
                        output[candidateCount].velocity = va;
                        output[candidateCount].distanceSq = (va - desiredVelocity).GetLength2();
                        candidateCount++;
                    }

                    intersectionCount = IntersectLineSegCircle(center, maxSpeed, va, vb, intersections);

                    for (nuint ii = 0; ii < intersectionCount; ++ii)
                    {
                        Vec2 candidateVelocity = intersections[ii];

                        if (candidateVelocity.GetLength2() > MinSpeedSq)
                        {
                            output[candidateCount].velocity = candidateVelocity;
                            output[candidateCount].distanceSq = (candidateVelocity - desiredVelocity).GetLength2();
                            candidateCount++;
                        }
                    }
                }

                if (candidateCount > 0)
                {
                    Array.Sort(output, 0, (int)candidateCount, Comparer<CandidateVelocity>.Create(
                        (a, b) => a.distanceSq.CompareTo(b.distanceSq)));

                    return candidateCount;
                }
            }
        }

        return 0;
    }

    private Vec2 ClampSpeedWithNavigationMesh(NavigationAgentTypeID agentTypeID, Vec3 agentPosition, Vec2 currentVelocity, Vec2 velocityToClamp)
    {
        Vec2 outputVelocity = velocityToClamp;
        if (gAIEnv.CVars.CollisionAvoidanceClampVelocitiesWithNavigationMesh == 1)
        {
            float TimeHorizon = 0.25f * gAIEnv.CVars.CollisionAvoidanceAgentTimeHorizon;
            float invTimeHorizon = 1.0f / gAIEnv.CVars.CollisionAvoidanceAgentTimeHorizon;
            float TimeStep = gAIEnv.CVars.CollisionAvoidanceTimeStep;

            Vec3 from = agentPosition;
            Vec3 to = agentPosition + new Vec3(velocityToClamp.x, velocityToClamp.y, 0.0f);

            NavigationMeshID meshID = gAIEnv.pNavigationSystem.GetEnclosingMeshID(agentTypeID, from);
            if (meshID.id != 0)
            {
                NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(meshID);
                NavMNM.MeshGrid grid = mesh.grid;

                NavMNM.vector3_t startLoc = new NavMNM.vector3_t(new NavMNM.real_t(from.x), new NavMNM.real_t(from.y), new NavMNM.real_t(from.z));
                NavMNM.vector3_t endLoc = new NavMNM.vector3_t(new NavMNM.real_t(to.x), new NavMNM.real_t(to.y), new NavMNM.real_t(to.z));

                NavMNM.real_t horizontalRange = new NavMNM.real_t(5.0f);
                NavMNM.real_t verticalRange = new NavMNM.real_t(1.0f);

                uint triStart = grid.GetTriangleAt(startLoc, verticalRange, verticalRange);

                uint triEnd = grid.GetTriangleAt(endLoc, verticalRange, verticalRange);
                if (triEnd == 0)
                {
                    NavMNM.real_t closestEndDistSq = new NavMNM.real_t(0.0f);
                    NavMNM.vector3_t closestEndLocation = new NavMNM.vector3_t();
                    triEnd = grid.GetClosestTriangle(endLoc, verticalRange, horizontalRange, ref closestEndDistSq, ref closestEndLocation);
                    grid.PushPointInsideTriangle(triEnd, ref closestEndLocation, new NavMNM.real_t(0.05f));
                    endLoc = closestEndLocation;
                }

                if (triStart != 0 && triEnd != 0)
                {
                    NavMNM.MeshGrid.RaycastRequestBase raycastRequest = new NavMNM.MeshGrid.RaycastRequestBase(512);
                    NavMNM.MeshGrid.ERayCastResult result = grid.RayCast(startLoc, triStart, endLoc, triEnd, raycastRequest);
                    if (result == NavMNM.MeshGrid.ERayCastResult.eRayCastResult_Hit)
                    {
                        float velocityMagnitude = min(TimeStep, raycastRequest.hit.distance.as_float());
                        Vec3 newEndLoc = agentPosition + ((endLoc.GetVec3() - agentPosition) * velocityMagnitude);
                        Vec3 newVelocity = newEndLoc - agentPosition;
                        outputVelocity = new Vec2(newVelocity.x, newVelocity.y) * invTimeHorizon;
                    }
                    else if (result == NavMNM.MeshGrid.ERayCastResult.eRayCastResult_NoHit)
                    {
                        Vec3 newVelocity = endLoc.GetVec3() - agentPosition;
                        outputVelocity = new Vec2(newVelocity.x, newVelocity.y);
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(false);
                    }
                }
            }
        }
        return outputVelocity;
    }

    private bool FindFirstWalkableVelocity(AgentID agentID, CandidateVelocity[] candidates, nuint candidateCount, out Vec2 output)
    {
        output = new Vec2(0, 0);
        Agent agent = m_agents[(int)agentID.id];
        uint aiObjectID = m_agentObjectIDs[(int)agentID.id];

        CAIObject aiObject = gAIEnv.pObjectContainer.GetAIObjectById(aiObjectID);
        CAIActor actor = aiObject?.CastToCAIActor();
        if (actor == null)
            return false;

        for (nuint i = 0; i < candidateCount; ++i)
        {
            CandidateVelocity candidate = candidates[i];

            Vec3 from = agent.currentLocation;
            Vec3 to = agent.currentLocation + new Vec3(candidate.velocity.x * 0.125f, candidate.velocity.y * 0.125f, 0.0f);

            output = ClampSpeedWithNavigationMesh(actor.GetNavigationTypeID(), agent.currentLocation, agent.currentVelocity, candidate.velocity);
            if (output.GetLength2() < 0.1f)
                continue;
            return true;
        }

        return false;
    }

    private bool FindLineCandidate(ConstraintLine[] lines, nuint lineCount, nuint lineNumber, float radius, Vec2 velocity, out Vec2 candidate)
    {
        ConstraintLine line = lines[lineNumber];

        float discriminant = sqr(radius) - sqr(line.direction.Cross(line.point));

        if (discriminant < 0.0f)
        {
            candidate = velocity;
            return false;
        }

        float discriminantSqrt = sqrt_tpl(discriminant);
        float dot = line.direction.Dot(line.point);

        float tLeft = -dot - discriminantSqrt;
        float tRight = -dot + discriminantSqrt;

        for (nuint i = 0; i < lineNumber; ++i)
        {
            ConstraintLine constraint = lines[i];

            float determinant = line.direction.Cross(constraint.direction);
            float distanceSigned = constraint.direction.Cross(line.point - constraint.point);

            // parallel constraints
            if (fabs_tpl(determinant) < 0.00001f)
            {
                if (distanceSigned < 0.0f)
                {
                    candidate = velocity;
                    return false;
                }
                else
                    continue; // no constraint
            }

            float t = distanceSigned / determinant;

            if (determinant > 0.0f)
                tRight = min(t, tRight);
            else
                tLeft = max(t, tLeft);

            if (tLeft > tRight)
            {
                candidate = velocity;
                return false;
            }
        }

        float tFinal = line.direction.Dot(velocity - line.point);

        if (tFinal < tLeft)
            candidate = line.point + tLeft * line.direction;
        else if (tFinal > tRight)
            candidate = line.point + tRight * line.direction;
        else
            candidate = line.point + tFinal * line.direction;

        return true;
    }

    private bool FindCandidate(ConstraintLine[] lines, nuint lineCount, float radius, Vec2 velocity, out Vec2 candidate)
    {
        if (velocity.GetLength2() > sqr(radius))
            candidate = velocity.GetNormalized() * radius;
        else
            candidate = velocity;

        for (nuint i = 0; i < lineCount; ++i)
        {
            ConstraintLine constraint = lines[i];

            if (LeftOf(constraint.direction, candidate - constraint.point) < 0.0f)
            {
                if (!FindLineCandidate(lines, lineCount, i, radius, velocity, out candidate))
                    return false;
            }
        }

        return true;
    }

    private void DebugDrawConstraintLine(Vec3 agentLocation, ConstraintLine line, ColorB color)
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        Vec3 v1 = agentLocation + new Vec3(line.point.x, line.point.y, 0);
        Vec3 v0 = v1 - new Vec3(line.direction.x, line.direction.y, 0) * 10.0f;
        Vec3 v2 = v1 + new Vec3(line.direction.x, line.direction.y, 0) * 10.0f;

        dc.DrawLine(v0, color, v2, color, 5.0f);
        dc.DrawArrow(v1, new Vec3(-line.direction.y, line.direction.x, 0) * 0.35f, 0.095f, color);
    }

    private struct CandidateVelocity
    {
        public float distanceSq;
        public Vec2 velocity;

        public static bool operator <(CandidateVelocity a, CandidateVelocity b) { return a.distanceSq < b.distanceSq; }
        public static bool operator >(CandidateVelocity a, CandidateVelocity b) { return a.distanceSq > b.distanceSq; }
    }

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

// Literal port of ICollisionAvoidanceSystem::Agent from ICollisionAvoidanceSystem.h
public class Agent
{
    public float radius = 0.4f;
    public float maxSpeed = 4.5f;
    public float maxAcceleration = 0.1f;

    public Vec3 currentLocation;
    public Vec2 currentVelocity;
    public Vec2 currentLookDirection;
    public Vec2 desiredVelocity;

    public Agent() { }

    public Agent(float _radius, float _maxSpeed, float _maxAcceleration, Vec2 _currentLocation, Vec2 _currentVelocity,
        Vec2 _currentLookDirection, Vec2 _desiredVelocity)
    {
        radius = _radius;
        maxSpeed = _maxSpeed;
        maxAcceleration = _maxAcceleration;
        currentLocation = new Vec3(_currentLocation.x, _currentLocation.y, 0);
        currentVelocity = _currentVelocity;
        currentLookDirection = _currentLookDirection;
        desiredVelocity = _desiredVelocity;
    }
}

// Literal port of ICollisionAvoidanceSystem::Obstacle from ICollisionAvoidanceSystem.h
public class Obstacle
{
    public float radius = 1.0f;

    public Vec3 currentLocation;
    public Vec2 currentVelocity;

    public Obstacle() { }
}
