// Literal port of dev/Code/CryEngine/CryAISystem/Movement/MoveOp.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using CryAISystem.CryCommon;
using static CryAISystem.CryMath;
using static CryAISystem.AIBubbleHelper;

namespace CryAISystem;

// MoveOpDictionaryCollection
public class MoveOpDictionaryCollection
{
    public MoveOpDictionaryCollection()
    {
        destinationTypes.Reserve(5);
        destinationTypes.Add("Target", MoveOp.DestinationType.Target);
        destinationTypes.Add("Cover", MoveOp.DestinationType.Cover);
        destinationTypes.Add("RefPoint", MoveOp.DestinationType.ReferencePoint);
        destinationTypes.Add("FollowPath", MoveOp.DestinationType.FollowPath);
        destinationTypes.Add("Formation", MoveOp.DestinationType.Formation);
    }

    public CXMLAttrReader<MoveOp.DestinationType> destinationTypes = new CXMLAttrReader<MoveOp.DestinationType>();
}

public class MoveOp : EnterLeaveUpdateGoalOp
{
    private static MoveOpDictionaryCollection g_moveOpDictionaryCollection = new MoveOpDictionaryCollection();

    public enum DestinationType
    {
        Target,
        Cover,
        ReferencePoint,
        FollowPath,
        Formation,
    }

    public MoveOp()
    {
        m_movementRequestID = 0;
        m_stopWithinDistanceSq = 0.0f;
        m_destinationAtTimeOfMovementRequest = new Vec3(0, 0, 0);
        m_destination = DestinationType.ReferencePoint;
        m_dangersFlags = MNMDangersFlags.eMNMDangers_None;
        m_requestStopWhenLeaving = false;
        m_movementStyle.SetMovingToCover(false);
    }

    public MoveOp(XmlNodeRef node)
    {
        m_movementRequestID = 0;
        m_stopWithinDistanceSq = 0.0f;
        m_destinationAtTimeOfMovementRequest = new Vec3(0, 0, 0);
        m_destination = DestinationType.ReferencePoint;
        m_dangersFlags = MNMDangersFlags.eMNMDangers_None;
        m_requestStopWhenLeaving = false;

        // Speed? Stance?
        m_movementStyle.ReadFromXml(node);

        // Destination? Target/Cover/ReferencePoint
        DestinationType dest = m_destination;
        g_moveOpDictionaryCollection.destinationTypes.Get(node, "to", ref dest, true);
        m_destination = dest;
        m_movementStyle.SetMovingToCover(m_destination == DestinationType.Cover);
        m_movementStyle.SetMovingAlongDesignedPath(m_destination == DestinationType.FollowPath);

        if (m_destination == DestinationType.FollowPath)
        {
            string pathNameAttr;
            if (node.getAttr("pathName", out pathNameAttr))
                m_pathName = pathNameAttr;
        }

        float stopWithinDistance = 0.0f;
        node.getAttr("stopWithinDistance", out stopWithinDistance);
        SetStopWithinDistance(stopWithinDistance);

        bool shouldAvoidDangers = true;
        node.getAttr("shouldAvoidDangers", out shouldAvoidDangers);

        SetupDangersFlagsForDestination(shouldAvoidDangers);
    }

    private void SetupDangersFlagsForDestination(bool shouldAvoidDangers)
    {
        if (!shouldAvoidDangers)
        {
            m_dangersFlags = MNMDangersFlags.eMNMDangers_None;
            return;
        }

        switch (m_destination)
        {
        case DestinationType.Target:
        case DestinationType.Formation:
            m_dangersFlags = MNMDangersFlags.eMNMDangers_Explosive;
            break;
        case DestinationType.Cover:
        case DestinationType.ReferencePoint:
            m_dangersFlags = MNMDangersFlags.eMNMDangers_AttentionTarget | MNMDangersFlags.eMNMDangers_Explosive;
            break;
        case DestinationType.FollowPath:
            m_dangersFlags = MNMDangersFlags.eMNMDangers_None;
            break;
        default:
            System.Diagnostics.Debug.Assert(false);
            m_dangersFlags = MNMDangersFlags.eMNMDangers_None;
            break;
        }
    }

    public override void Enter(CPipeUser pipeUser)
    {
        if (m_destination != DestinationType.FollowPath)
        {
            RequestMovementTo(DestinationPositionFor(pipeUser), pipeUser);
        }
        else
        {
            if (string.IsNullOrEmpty(m_pathName))
            {
                GetClosestDesignedPath(pipeUser, ref m_pathName);
            }
            RequestFollowExistingPath(m_pathName, pipeUser);
        }
    }

    public override void Leave(CPipeUser pipeUser)
    {
        bool requestWasStillRunning = m_movementRequestID != 0;

        ReleaseCurrentMovementRequest();

        bool shouldRequestStop = (m_requestStopWhenLeaving && requestWasStillRunning) || GetStatus() == EGoalOpResult.eGOR_FAILED;
        if (shouldRequestStop)
        {
            RequestStop(pipeUser);
        }
    }

    public override void Update(CPipeUser pipeUser)
    {
        if (m_destination == DestinationType.Target || m_destination == DestinationType.Formation)
        {
            ChaseTarget(pipeUser);
        }

        bool stopMovementWhenWithinCertainDistance = m_stopWithinDistanceSq > 0.0f;

        if (stopMovementWhenWithinCertainDistance)
        {
            if (GetSquaredDistanceToDestination(pipeUser) < m_stopWithinDistanceSq)
            {
                ReleaseCurrentMovementRequest();
                RequestStop(pipeUser);
                GoalOpSucceeded();
            }
        }
    }

    public void SetMovementStyle(MovementStyle movementStyle)
    {
        m_movementStyle = movementStyle;
    }

    public void SetStopWithinDistance(float distance)
    {
        m_stopWithinDistanceSq = square(distance);
    }

    public void SetRequestStopWhenLeaving(bool requestStop)
    {
        m_requestStopWhenLeaving = requestStop;
    }

    private Vec3 DestinationPositionFor(CPipeUser pipeUser)
    {
        switch (m_destination)
        {
        case DestinationType.Target:
            {
                IAIObject target = pipeUser.GetAttentionTarget();
                if (target != null)
                {
                    CPipeUser pPipeUser = target.CastToCPipeUser();
                    Vec3 targetPosition = pPipeUser != null ? pPipeUser.GetPhysicsPos() : target.GetPosInNavigationMesh(pipeUser.GetNavigationTypeID());
                    Vec3 targetVelocity = target.GetVelocity();
                    targetVelocity.z = 0.0f;
                    return targetPosition + targetVelocity * 0.5f;
                }
                else
                {
                    AIQueueBubbleMessage("MoveOp Destination Position",
                        pipeUser.GetEntityID(),
                        "I don't have a target and MoveOp is set to chase the target.",
                        EBubbleNotificationSystemFlags.eBNS_LogWarning | EBubbleNotificationSystemFlags.eBNS_Balloon);
                    return new Vec3(0, 0, 0);
                }
            }

        case DestinationType.Cover:
            {
                return GetCoverRegisterLocation(pipeUser);
            }

        case DestinationType.ReferencePoint:
            {
                IAIObject refPt = pipeUser.GetRefPoint();
                return refPt != null ? refPt.GetPos() : new Vec3(0, 0, 0);
            }

        case DestinationType.Formation:
            {
                IAIObject pIAIObject = pipeUser.GetSpecialAIObject("formation", 100000.0f);
                if (pIAIObject != null)
                {
                    m_formationInfo.positionInFormation = pIAIObject.GetPos();

                    CFormation formation = getFormation(pipeUser, 100000.0f);
                    if (formation != null)
                    {
                        CPathMarker pm = formation.GetPathMarker();
                        if (pm != null)
                        {
                            int pointIndex = formation.GetPointIndex(WeakRefHelpers.GetWeakRef((CAIObject)pipeUser));
                            if (pointIndex != -1)
                            {
                                Vec3 offset = new Vec3(0, 0, 0);
                                formation.GetPointOffset(pointIndex, ref offset);
                                Vec3 dir = pm.GetDirectionAtDistanceFromNewestPoint(offset.y);
                                float lookAheadDistance = 6.0f;
                                Vec3 positionToMoveTo = m_formationInfo.positionInFormation + dir * lookAheadDistance;

                                if (gAIEnv.pNavigationSystem != null &&
                                    gAIEnv.pNavigationSystem.IsPointReachableFromPosition(
                                        pipeUser.GetNavigationTypeID(), pipeUser.GetPathAgentEntity(),
                                        pipeUser.GetEntity()?.GetPos() ?? new Vec3(0,0,0), positionToMoveTo))
                                {
                                    m_formationInfo.positionInFormationIsReachable = true;
                                    return positionToMoveTo;
                                }
                                else
                                {
                                    m_formationInfo.positionInFormationIsReachable = false;

                                    if (pm.GetPointCount() > 0)
                                    {
                                        float distanceBehindLeader = 1.0f;
                                        return pm.GetPointAtDistanceFromNewestPoint(distanceBehindLeader);
                                    }
                                }
                            }
                        }
                    }
                    // fallback
                    Vec3 formationVelocity = pipeUser.GetFormationVelocity();
                    float lookAheadDistanceRelativeToTargetVelocity = 0.5f;
                    return m_formationInfo.positionInFormation + formationVelocity * lookAheadDistanceRelativeToTargetVelocity;
                }
                else
                {
                    AIQueueBubbleMessage("MoveOp seeking formation failed",
                        pipeUser.GetEntityID(),
                        "I don't have a formation and MoveOp is set to chase it.",
                        EBubbleNotificationSystemFlags.eBNS_LogWarning | EBubbleNotificationSystemFlags.eBNS_Balloon);
                    return new Vec3(0, 0, 0);
                }
            }

        default:
            {
                System.Diagnostics.Debug.Assert(false);
                return new Vec3(0, 0, 0);
            }
        }
    }

    private Vec3 GetCoverRegisterLocation(CPipeUser pipeUser)
    {
        CoverID coverID = pipeUser.GetCoverRegister();
        if (coverID.IsValid())
        {
            float distanceToCover = pipeUser.GetParameters().distanceToCover;
            return gAIEnv.pCoverSystem?.GetCoverLocation(coverID, distanceToCover) ?? new Vec3(0, 0, 0);
        }
        else
        {
            System.Diagnostics.Debug.Assert(false);
            AIQueueBubbleMessage("MoveOp:CoverLocation", pipeUser.GetEntityID(),
                "MoveOp failed to get the cover location due to an invalid Cover ID in the cover register.",
                EBubbleNotificationSystemFlags.eBNS_LogWarning | EBubbleNotificationSystemFlags.eBNS_Balloon | EBubbleNotificationSystemFlags.eBNS_BlockingPopup);
            return new Vec3(0, 0, 0);
        }
    }

    private void RequestMovementTo(Vec3 position, CPipeUser pipeUser)
    {
        System.Diagnostics.Debug.Assert(m_movementRequestID == 0);

        MovementRequest movementRequest = new MovementRequest();
        movementRequest.entityID = pipeUser.GetEntityID();
        movementRequest.destination = position;
        movementRequest.callback = MovementRequestCallback;
        movementRequest.style = m_movementStyle;
        movementRequest.dangersFlags = m_dangersFlags;

        IMovementSystem movementSystem = gEnv.pAISystem?.GetMovementSystem();
        if (movementSystem != null)
            m_movementRequestID = movementSystem.QueueRequest(movementRequest);

        m_destinationAtTimeOfMovementRequest = position;
    }

    private void RequestFollowExistingPath(string pathName, CPipeUser pipeUser)
    {
        System.Diagnostics.Debug.Assert(pathName != null);

        if (pathName == null)
            return;

        CAIActor pCAIActor = pipeUser.CastToCAIActor();
        System.Diagnostics.Debug.Assert(pCAIActor != null);
        if (pCAIActor == null)
            return;

        pCAIActor.SetPathToFollow(pathName);

        MovementRequest movementRequest = new MovementRequest();
        movementRequest.entityID = pipeUser.GetEntityID();
        movementRequest.callback = MovementRequestCallback;
        movementRequest.style = m_movementStyle;

        IMovementSystem movementSystem = gEnv.pAISystem?.GetMovementSystem();
        if (movementSystem != null)
            m_movementRequestID = movementSystem.QueueRequest(movementRequest);
    }

    private void ReleaseCurrentMovementRequest()
    {
        if (m_movementRequestID != 0)
        {
            gEnv.pAISystem?.GetMovementSystem()?.CancelRequest(m_movementRequestID);
            m_movementRequestID = 0;
        }
    }

    private void MovementRequestCallback(MovementRequestResult result)
    {
        System.Diagnostics.Debug.Assert(m_movementRequestID == result.requestID);

        m_movementRequestID = 0;

        if (result == MovementRequestResult.Result.ReachedDestination)
        {
            GoalOpSucceeded();
        }
        else
        {
            GoalOpFailed();

            if (m_destination == DestinationType.Cover)
            {
                // Todo: Blacklist cover
            }
        }
    }

    private static CFormation getFormation(CPipeUser pipeUserInFormation, float range)
    {
        CAISystem pAISystem = GlobalFunctions.GetAISystem();
        CFormation pFormation = null;

        if (pAISystem != null)
        {
            CLeader pLeader = pAISystem.GetLeader(pipeUserInFormation.GetGroupId());
            if (pLeader != null)
            {
                CWeakRef<CAIObject> formationOwnerRef = pLeader.GetFormationOwner();
                CAIObject pFormationOwner = formationOwnerRef?.GetAIObject();
                if (pFormationOwner != null)
                {
                    pFormation = pFormationOwner.m_pFormation;
                }
            }
        }

        if (pFormation == null && pAISystem != null)
        {
            IAIObject pBoss = pAISystem.GetNearestObjectOfTypeInRange(pipeUserInFormation, AIOBJECT_ACTOR, 0,
                range, AIFAF_HAS_FORMATION | AIFAF_INCLUDE_DEVALUED | AIFAF_SAME_GROUP_ID);
            if (pBoss == null)
                pBoss = pAISystem.GetNearestObjectOfTypeInRange(pipeUserInFormation, AIOBJECT_VEHICLE, 0,
                    range, AIFAF_HAS_FORMATION | AIFAF_INCLUDE_DEVALUED | AIFAF_SAME_GROUP_ID);

            CAIObject pTheBoss = pBoss as CAIObject;
            if (pTheBoss != null)
            {
                pFormation = pTheBoss.m_pFormation;
            }
        }

        return pFormation;
    }

    private void ChaseTarget(CPipeUser pipeUser)
    {
        Vec3 targetPosition = DestinationPositionFor(pipeUser);

        Vec3 diff = targetPosition - m_destinationAtTimeOfMovementRequest;
        float targetDeviation = diff.GetLengthSquared();
        float deviationThreshold = square(0.5f);

        if (targetDeviation > deviationThreshold)
        {
            ReleaseCurrentMovementRequest();
            RequestMovementTo(targetPosition, pipeUser);
        }

        if (m_destination == DestinationType.Formation)
        {
            CFormation formation = getFormation(pipeUser, 100000.0f);
            if (formation != null)
            {
                CAIObject formationOwner = formation.GetOwner();
                if (formationOwner != null)
                {
                    CAIActor formationOwnerAsAIActor = formationOwner.CastToCAIActor();
                    if (formationOwnerAsAIActor != null)
                    {
                        CPathMarker pm = formation.GetPathMarker();
                        if (pm != null)
                        {
                            float newSpeed = formationOwnerAsAIActor.GetState().fMovementUrgency;

                            if (m_formationInfo.positionInFormationIsReachable)
                            {
                                float innerRadiusSqr = square(2.0f);
                                float outerRadiusSqr = square(4.0f);

                                Vec3 toSlot = pipeUser.GetPos() - m_formationInfo.positionInFormation;
                                float distanceToFormationSlotSqr = toSlot.GetLengthSquared();

                                Vec3 futureFormationSlot = pm.GetPointAtDistance(m_formationInfo.positionInFormation, 2.0f);
                                Vec3 dirSelfToFormationSlot = (m_formationInfo.positionInFormation - pipeUser.GetPos()).GetNormalized();
                                Vec3 dirFormationSlotToFutureFormationSlot = (futureFormationSlot - m_formationInfo.positionInFormation).GetNormalized();
                                float dot = dirSelfToFormationSlot.Dot(dirFormationSlotToFutureFormationSlot);

                                // check for state transition
                                switch (m_formationInfo.state)
                                {
                                case FormationInfo.State.State_MatchingLeaderSpeed:
                                    if (distanceToFormationSlotSqr > outerRadiusSqr)
                                    {
                                        if (dot > 0.0f)
                                            m_formationInfo.state = FormationInfo.State.State_CatchingUp;
                                        else
                                            m_formationInfo.state = FormationInfo.State.State_SlowingDown;
                                    }
                                    else if (distanceToFormationSlotSqr > innerRadiusSqr)
                                    {
                                        if (dot < 0.0f)
                                            m_formationInfo.state = FormationInfo.State.State_SlowingDown;
                                    }
                                    break;

                                case FormationInfo.State.State_CatchingUp:
                                    if (distanceToFormationSlotSqr < innerRadiusSqr)
                                    {
                                        m_formationInfo.state = FormationInfo.State.State_MatchingLeaderSpeed;
                                    }
                                    else if (dot < 0.0f)
                                    {
                                        m_formationInfo.state = FormationInfo.State.State_MatchingLeaderSpeed;
                                    }
                                    break;

                                case FormationInfo.State.State_SlowingDown:
                                    if (distanceToFormationSlotSqr < innerRadiusSqr)
                                    {
                                        if (dot > 0.0f)
                                            m_formationInfo.state = FormationInfo.State.State_MatchingLeaderSpeed;
                                    }
                                    else if (dot > 0.0f)
                                    {
                                        m_formationInfo.state = FormationInfo.State.State_MatchingLeaderSpeed;
                                    }
                                    break;
                                }

                                switch (m_formationInfo.state)
                                {
                                case FormationInfo.State.State_MatchingLeaderSpeed:
                                    // nothing, just adhere to the leader's speed
                                    break;
                                case FormationInfo.State.State_CatchingUp:
                                    newSpeed = Math.Max(1.0f, newSpeed * 1.5f);
                                    break;
                                case FormationInfo.State.State_SlowingDown:
                                    newSpeed = Math.Min(0.3f, newSpeed * 0.5f);
                                    break;
                                }
                            }
                            else
                            {
                                float fractionOfLeaderSpeed = 0.8f;
                                newSpeed *= fractionOfLeaderSpeed;
                            }

                            pipeUser.SetSpeed(newSpeed);
                            m_movementStyle.SetSpeedLiteral(newSpeed);
                        }
                    }
                }
            }
        }
    }

    private float GetSquaredDistanceToDestination(CPipeUser pipeUser)
    {
        Vec3 destinationPosition = DestinationPositionFor(pipeUser);
        Vec3 diff = destinationPosition - pipeUser.GetPos();
        return diff.GetLengthSquared();
    }

    private void RequestStop(CPipeUser pipeUser)
    {
        MovementRequest movementRequest = new MovementRequest();
        movementRequest.entityID = pipeUser.GetEntityID();
        movementRequest.type = MovementRequest.Type.Stop;
        gEnv.pAISystem?.GetMovementSystem()?.QueueRequest(movementRequest);
    }

    private void GetClosestDesignedPath(CPipeUser pipeUser, ref string closestPathName)
    {
        Vec3 userPosition = pipeUser.GetPos();
        ShapeMap shapesMap = gAIEnv.pNavigation?.GetDesignerPaths();
        if (shapesMap == null) return;

        float minDistance = FLT_MAX;
        foreach (var entry in shapesMap)
        {
            float distanceToNearestPointOnPath;
            Vec3 closestPoint;
            entry.Value.NearestPointOnPath(userPosition, false, out distanceToNearestPointOnPath, out closestPoint);
            if (distanceToNearestPointOnPath < minDistance)
            {
                minDistance = distanceToNearestPointOnPath;
                closestPathName = entry.Key;
            }
        }
    }

    // Formation info struct
    private class FormationInfo
    {
        public enum State
        {
            State_MatchingLeaderSpeed,
            State_CatchingUp,
            State_SlowingDown,
        }

        public State state = State.State_MatchingLeaderSpeed;
        public Vec3 positionInFormation = new Vec3(0, 0, 0);
        public bool positionInFormationIsReachable = true;
    }

    private Vec3 m_destinationAtTimeOfMovementRequest;
    private float m_stopWithinDistanceSq;
    private MovementStyle m_movementStyle = new MovementStyle();
    private MovementRequestID m_movementRequestID;
    private DestinationType m_destination;
    private string m_pathName = "";
    private MNMDangersFlags m_dangersFlags;
    private bool m_requestStopWhenLeaving;
    private FormationInfo m_formationInfo = new FormationInfo();

    // AI object type constants
    private const uint AIOBJECT_ACTOR = 0;
    private const uint AIOBJECT_VEHICLE = 3;
    private const uint AIFAF_HAS_FORMATION = 0x0010;
    private const uint AIFAF_INCLUDE_DEVALUED = 0x0040;
    private const uint AIFAF_SAME_GROUP_ID = 0x1000;
}
