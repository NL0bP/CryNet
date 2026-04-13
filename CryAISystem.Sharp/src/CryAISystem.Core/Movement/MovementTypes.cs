// Literal port of CryCommon Movement types:
//   MovementRequestID.h, MovementRequest.h, MovementStyle.h, MovementBlock.h,
//   MovementUpdateContext.h, IMovementActor.h, IMovementSystem.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using CryAISystem.CryCommon;
using static CryAISystem.CryMath;
using NavMNM = CryAISystem.Navigation.MNM;

namespace CryAISystem
{

// -----------------------------------------------------------------------
// MovementRequestID.h
// -----------------------------------------------------------------------
public struct MovementRequestID
{
    public uint id;

    public MovementRequestID() { id = 0; }
    public MovementRequestID(uint _id) { id = _id; }

    public static MovementRequestID operator ++(MovementRequestID r) { r.id++; return r; }
    public static bool operator ==(MovementRequestID a, MovementRequestID b) => a.id == b.id;
    public static bool operator !=(MovementRequestID a, MovementRequestID b) => a.id != b.id;
    public static implicit operator uint(MovementRequestID r) => r.id;
    public static implicit operator MovementRequestID(uint v) => new MovementRequestID(v);

    public static MovementRequestID Invalid() { return new MovementRequestID(0); }

    public override bool Equals(object obj) => obj is MovementRequestID other && id == other.id;
    public override int GetHashCode() => id.GetHashCode();
}

// -----------------------------------------------------------------------
// MovementRequest.h -- MovementRequestResult
// -----------------------------------------------------------------------
public class MovementRequestResult
{
    public enum Result
    {
        Success,
        Failure,
        ReachedDestination = Success,
    }

    public enum FailureReason
    {
        NoReason,
        CouldNotFindPathToRequestedDestination,
        CouldNotMoveAlongDesignerDesignedPath,
        FailedToProduceSuccessfulPlanAfterMaximumNumberOfAttempts,
    }

    public MovementRequestResult(MovementRequestID _id, Result _result, FailureReason _failureReason)
    {
        requestID = _id;
        result = _result;
        failureReason = _failureReason;
    }

    public MovementRequestResult(MovementRequestID _id, Result _result)
    {
        requestID = _id;
        result = _result;
        failureReason = FailureReason.NoReason;
    }

    public static bool operator ==(MovementRequestResult a, Result b) => a != null && a.result == b;
    public static bool operator !=(MovementRequestResult a, Result b) => a == null || a.result != b;
    public static implicit operator bool(MovementRequestResult r) => r != null && r.result == Result.Success;

    public override bool Equals(object obj) => obj is MovementRequestResult other && result == other.result && requestID == other.requestID;
    public override int GetHashCode() => HashCode.Combine(requestID, result);

    public readonly MovementRequestID requestID;
    public readonly Result result;
    public readonly FailureReason failureReason;
}

// -----------------------------------------------------------------------
// MovementRequest.h -- MovementRequest
// -----------------------------------------------------------------------
public class MovementRequest
{
    public delegate void Callback(MovementRequestResult result);

    public enum Type
    {
        MoveTo,
        Stop,
        CountTypes,
    }

    public MovementRequest()
    {
        destination = new Vec3(0, 0, 0);
        type = Type.MoveTo;
        callback = null;
        entityID = 0;
        dangersFlags = MNMDangersFlags.eMNMDangers_None;
        considerActorsAsPathObstacles = false;
        lengthToTrimFromThePathEnd = 0.0f;
    }

    public static string GetTypeAsDebugName(Type type)
    {
        if (type == Type.MoveTo)
            return "MoveTo";
        else if (type == Type.Stop)
            return "Stop";
        return "Undefined";
    }

    public MovementStyle style = new MovementStyle();
    public Vec3 destination;
    public Type type;
    public Callback callback;
    public uint entityID;
    public MNMDangersFlags dangersFlags;
    public bool considerActorsAsPathObstacles;
    public float lengthToTrimFromThePathEnd;
}

// -----------------------------------------------------------------------
// MovementRequest.h -- MovementRequestStatus
// -----------------------------------------------------------------------
public class MovementRequestStatus
{
    public MovementRequestStatus() { currentBlockIndex = 0; id = ID.NotQueued; }

    public class BlockInfo
    {
        public string name;
        public BlockInfo() { name = null; }
        public BlockInfo(string _name) { name = _name; }
    }

    public enum ID
    {
        NotQueued,
        Queued,
        FindingPath,
        ExecutingPlan
    }

    public List<BlockInfo> blockInfos = new List<BlockInfo>();
    public uint currentBlockIndex;
    public ID id;
}

public static class MovementRequestStatusHelper
{
    public static void ConstructHumanReadableText(MovementRequestStatus status, ref string statusText)
    {
        switch (status.id)
        {
        case MovementRequestStatus.ID.Queued:
            statusText = "Request In Queue";
            break;
        case MovementRequestStatus.ID.FindingPath:
            statusText = "Finding Path";
            break;
        case MovementRequestStatus.ID.ExecutingPlan:
            statusText = "Executing Plan: ";
            int totalBlockInfos = status.blockInfos.Count;
            for (int index = 0; index < totalBlockInfos; ++index)
            {
                if (index != 0)
                    statusText += " ";
                bool active = (index == (int)status.currentBlockIndex);
                if (active) statusText += "[";
                statusText += status.blockInfos[index].name;
                if (active) statusText += "]";
            }
            break;
        case MovementRequestStatus.ID.NotQueued:
            statusText = "Request Not Queued";
            break;
        default:
            statusText = "Unknown Status";
            break;
        }
    }
}

// -----------------------------------------------------------------------
// MovementStyle.h
// -----------------------------------------------------------------------
public class MovementStyle
{
    public enum Stance { Relaxed, Alerted, Stand, Crouch }
    public enum Speed { Walk, Run, Sprint }

    public MovementStyle()
    {
        m_stance = Stance.Stand;
        m_speed = Speed.Run;
        m_speedLiteral = 0.0f;
        m_hasSpeedLiteral = false;
        m_bodyOrientationMode = EBodyOrientationMode.HalfwayTowardsAimOrLook;
        m_movingToCover = false;
        m_movingAlongDesignedPath = false;
        m_turnTowardsMovementDirectionBeforeMoving = false;
        m_strafe = false;
        m_hasExactPositioningRequest = false;
        m_glanceInMovementDirection = false;
    }

    public void ReadFromXml(XmlNodeRef node) { /* simplified -- full engine reads XML attributes */ }

    public void SetStance(Stance stance) { m_stance = stance; }
    public void SetSpeed(Speed speed) { m_speed = speed; }
    public void SetSpeedLiteral(float speedLiteral) { m_speedLiteral = speedLiteral; m_hasSpeedLiteral = true; }
    public void SetMovingToCover(bool movingToCover) { m_movingToCover = movingToCover; }
    public void SetTurnTowardsMovementDirectionBeforeMoving(bool enabled) { m_turnTowardsMovementDirectionBeforeMoving = enabled; }
    public void SetMovingAlongDesignedPath(bool movingAlongDesignedPath) { m_movingAlongDesignedPath = movingAlongDesignedPath; }
    public void SetExactPositioningRequest(SAIActorTargetRequest pExactPositioningRequest)
    {
        if (pExactPositioningRequest != null)
        {
            m_hasExactPositioningRequest = true;
            m_exactPositioningRequest = pExactPositioningRequest;
        }
        else
        {
            m_hasExactPositioningRequest = false;
        }
    }

    public Stance GetStance() { return m_stance; }
    public Speed GetSpeed() { return m_speed; }
    public bool HasSpeedLiteral() { return m_hasSpeedLiteral; }
    public float GetSpeedLiteral() { return m_speedLiteral; }
    public EBodyOrientationMode GetBodyOrientationMode() { return m_bodyOrientationMode; }
    public bool IsMovingToCover() { return m_movingToCover; }
    public bool IsMovingAlongDesignedPath() { return m_movingAlongDesignedPath; }
    public bool ShouldTurnTowardsMovementDirectionBeforeMoving() { return m_turnTowardsMovementDirectionBeforeMoving; }
    public bool ShouldStrafe() { return m_strafe; }
    public bool ShouldGlanceInMovementDirection() { return m_glanceInMovementDirection; }
    public SAIActorTargetRequest GetExactPositioningRequest() { return m_hasExactPositioningRequest ? m_exactPositioningRequest : null; }

    private Stance m_stance;
    private Speed m_speed;
    private float m_speedLiteral;
    private bool m_hasSpeedLiteral;
    private EBodyOrientationMode m_bodyOrientationMode;
    private SAIActorTargetRequest m_exactPositioningRequest;
    private bool m_movingToCover;
    private bool m_movingAlongDesignedPath;
    private bool m_turnTowardsMovementDirectionBeforeMoving;
    private bool m_strafe;
    private bool m_hasExactPositioningRequest;
    private bool m_glanceInMovementDirection;
}

// -----------------------------------------------------------------------
// MovementUpdateContext.h
// -----------------------------------------------------------------------
public class MovementUpdateContext
{
    public MovementUpdateContext(
        IMovementActor _actor,
        IMovementSystem _movementSystem,
        IPathFollower _pathFollower,
        Movement.IPlanner _planner,
        float _updateTime)
    {
        actor = _actor;
        movementSystem = _movementSystem;
        pathFollower = _pathFollower;
        planner = _planner;
        updateTime = _updateTime;
    }

    public IMovementActor actor;
    public IMovementSystem movementSystem;
    public IPathFollower pathFollower;
    public Movement.IPlanner planner;
    public float updateTime;
}

// -----------------------------------------------------------------------
// IMovementActor.h -- IMovementActorAdapter
// -----------------------------------------------------------------------
public interface IMovementActorAdapter
{
    void OnMovementPlanProduced();
    Vec3 GetPhysicsPosition();
    Vec3 GetVelocity();
    Vec3 GetMoveDirection();
    Vec3 GetAnimationBodyDirection();
    EActorTargetPhase GetActorPhase();
    void SetMovementOutputValue(PathFollowResult result);
    void SetBodyTargetDirection(Vec3 direction);
    void ResetMovementContext();
    void ClearMovementState();
    void ResetBodyTarget();
    void ResetActorTargetRequest();
    bool IsMoving();
    void RequestExactPosition(SAIActorTargetRequest request, bool lowerPrecision);
    bool IsClosestToUseTheSmartObject(NavMNM.OffMeshLink_SmartObject smartObjectLink);
    bool PrepareNavigateSmartObject(CSmartObject pSmartObject, NavMNM.OffMeshLink_SmartObject pSmartObjectLink);
    void InvalidateSmartObjectLink(CSmartObject pSmartObject, NavMNM.OffMeshLink_SmartObject pSmartObjectLink);
    void SetInCover(bool inCover);
    void UpdateCoverLocations();
    void InstallInLowCover(bool inCover);
    void SetupCoverInformation();
    bool IsInCover();
    bool GetDesignedPath(SShape pathShape);
    void CancelRequestedPath();
    void ConfigurePathfollower(MovementStyle style);
    void SetActorPath(MovementStyle style, CNavPath navPath);
    void SetActorStyle(MovementStyle style, CNavPath navPath);
    void SetStance(MovementStyle.Stance stance);
    Vec3 CreateLookTarget();
    void SetLookTimeOffset(float lookTimeOffset);
    void UpdateLooking(float updateTime, Vec3 lookTarget, bool targetReachable, float pathDistanceToEnd, Vec3 followTargetPosition, MovementStyle style);
}

// -----------------------------------------------------------------------
// IMovementActor.h -- IMovementActor
// -----------------------------------------------------------------------
public interface IMovementActor
{
    IMovementActorAdapter GetAdapter();
    void RequestPathTo(Vec3 destination, float lengthToTrimFromThePathEnd,
        MNMDangersFlags dangersFlags = MNMDangersFlags.eMNMDangers_None,
        bool considerActorsAsPathObstacles = false);
    Movement_PathfinderState GetPathfinderState();
    string GetName();
    void Log(string message);
    bool IsLastPointInPathASmartObject();
    uint GetEntityId();
    MovementActorCallbacks GetCallbacks();
}

// -----------------------------------------------------------------------
// Bubble system helpers (AIBubblesSystem/IAIBubblesSystem.h subset)
// -----------------------------------------------------------------------
[Flags]
public enum EBubbleNotificationSystemFlags
{
    eBNS_Balloon = 1,
    eBNS_LogWarning = 2,
    eBNS_BlockingPopup = 4,
}

public static class AIBubbleHelper
{
    public static void AIQueueBubbleMessage(string msgName, uint entityId, string message, EBubbleNotificationSystemFlags flags)
    {
        gEnv.pLog?.LogWarning($"[AIBubble:{msgName}] Entity {entityId}: {message}");
    }
}

// -----------------------------------------------------------------------
// EnterLeaveUpdateGoalOp.h -- base class for goal ops
// -----------------------------------------------------------------------
public class EnterLeaveUpdateGoalOp
{
    public virtual void Enter(CPipeUser pipeUser) {}
    public virtual void Leave(CPipeUser pipeUser) {}
    public virtual void Update(CPipeUser pipeUser) {}

    protected EGoalOpResult m_status = EGoalOpResult.eGOR_IN_PROGRESS;
    public EGoalOpResult GetStatus() { return m_status; }
    protected void GoalOpSucceeded() { m_status = EGoalOpResult.eGOR_SUCCEEDED; }
    protected void GoalOpFailed() { m_status = EGoalOpResult.eGOR_FAILED; }
}

// -----------------------------------------------------------------------
// CXMLAttrReader -- simplified port for dictionary-based XML attribute reading
// -----------------------------------------------------------------------
public class CXMLAttrReader<T>
{
    private Dictionary<string, T> m_entries = new Dictionary<string, T>();
    public void Reserve(int n) { }
    public void Add(string key, T value) { m_entries[key] = value; }
    public bool Get(XmlNodeRef node, string attrName, ref T outValue, bool required = false)
    {
        if (node == null) return false;
        string val;
        if (!node.getAttr(attrName, out val)) return false;
        if (val != null && m_entries.TryGetValue(val, out T found))
        {
            outValue = found;
            return true;
        }
        return false;
    }
}

} // namespace CryAISystem

// -----------------------------------------------------------------------
// MovementBlock.h -- in Movement namespace
// -----------------------------------------------------------------------
namespace CryAISystem.Movement
{
    public class Block
    {
        public enum Status
        {
            Running,
            Finished,
            CantBeFinished,
        }

        public virtual void Begin(CryAISystem.IMovementActor actor) {}
        public virtual void End(CryAISystem.IMovementActor actor) {}
        public virtual Status Update(CryAISystem.MovementUpdateContext context) { return Status.Finished; }
        public virtual bool InterruptibleNow() { return false; }
        public virtual string GetName() { return ""; }
    }

    // typedef boost::shared_ptr<Movement::Block> BlockPtr -- in C# just use Block reference
    // typedef Functor3wRet<...> CustomNavigationBlockCreatorFunction
    public delegate Block CustomNavigationBlockCreatorFunction(INavPath path, PathPointDescriptor.OffMeshLinkData mnmData, CryAISystem.MovementStyle style);

    // Movement::IPlanner -- forward declared in MovementPlanner.cs
}
