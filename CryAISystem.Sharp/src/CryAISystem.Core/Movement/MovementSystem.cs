// Literal port of dev/Code/CryEngine/CryAISystem/Movement/MovementSystem.{h,cpp}
// + MovementSystemCreator.{h,cpp}
// + MovementActor.{h,cpp}
// + MovementPlan.{h,cpp}
// + MovementHelpers.{h,cpp}
// + MovementPlanner.{h,cpp}
// + MovementBlock_DefaultEmpty.h
// + MovementBlock_FollowPath.{h,cpp}
// + MovementBlock_HarshStop.{h,cpp}
// + MovementBlock_TurnTowardsPosition.{h,cpp}
// + MovementBlock_InstallAgentInCover.h
// + MovementBlock_UninstallAgentFromCover.h
// + MovementBlock_SetupPipeUserCoverInformation.{h,cpp}
// + MovementBlock_UseExactPositioningBase.{h,cpp}
// + MovementBlock_UseExactPositioning.{h,cpp}
// + MovementBlock_UseSmartObject.{h,cpp}
// + MoveOp.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CryAISystem.CryCommon;
using static CryAISystem.CryMath;
using static CryAISystem.AILog;
using static CryAISystem.AIBubbleHelper;
using NavMNM = CryAISystem.Navigation.MNM;

namespace CryAISystem
{

// -----------------------------------------------------------------------
// MovementSystemCreator.h / .cpp
// -----------------------------------------------------------------------
public class MovementSystemCreator
{
    public IMovementSystem CreateMovementSystem()
    {
        return new Movement.MovementSystem();
    }
}

// -----------------------------------------------------------------------
// typedef std::deque<MovementRequestID> MovementRequestQueue;
// -----------------------------------------------------------------------
public class MovementRequestQueue : LinkedList<MovementRequestID> { }

} // namespace CryAISystem

namespace CryAISystem.Movement
{

// -----------------------------------------------------------------------
// MovementActor.h / .cpp
// -----------------------------------------------------------------------
public class MovementActor : IMovementActor
{
    public MovementActor(uint _entityID, IMovementActorAdapter _pAdapter)
    {
        entityID = _entityID;
        requestIdCurrentlyInPlanner = 0;
        lastPointInPathIsSmartObject = false;
        pAdapter = _pAdapter;
        System.Diagnostics.Debug.Assert(pAdapter != null);
    }

    // IMovementActor
    public IMovementActorAdapter GetAdapter()
    {
        System.Diagnostics.Debug.Assert(pAdapter != null);
        return pAdapter;
    }

    public void RequestPathTo(Vec3 destination, float lengthToTrimFromThePathEnd,
        MNMDangersFlags dangersFlags = MNMDangersFlags.eMNMDangers_None,
        bool considerActorsAsPathObstacles = false)
    {
        bool cutPathAtSmartObject = false;

        IEntity pEntity = gEnv.pEntitySystem?.GetEntity(entityID);
        if (pEntity == null)
            return;

        if (callbacks.queuePathRequestFunction != null)
        {
            MNMPathRequest request = new MNMPathRequest(
                pEntity.GetPos(), destination, new Vec3(0, 1, 0), -1, 0.0f,
                lengthToTrimFromThePathEnd, true, null,
                new NavigationAgentTypeID { id = 1 }, dangersFlags);
            callbacks.queuePathRequestFunction(request);
        }
    }

    public Movement_PathfinderState GetPathfinderState()
    {
        if (callbacks.checkOnPathfinderStateFunction != null)
        {
            return callbacks.checkOnPathfinderStateFunction();
        }
        return Movement_PathfinderState.CouldNotFindPath;
    }

    public string GetName()
    {
        IEntity pEntity = gEnv.pEntitySystem?.GetEntity(entityID);
        return pEntity != null ? pEntity.GetName() : "(none)";
    }

    public void Log(string message)
    {
        #if AI_COMPILE_WITH_PERSONAL_LOG
        CAIActor aiActor = GetAIActor();
        if (aiActor != null)
        {
            aiActor.GetPersonalLog().AddMessage(entityID, message);
        }
        #endif
    }

    public bool IsLastPointInPathASmartObject() { return lastPointInPathIsSmartObject; }
    public uint GetEntityId() { return entityID; }
    public MovementActorCallbacks GetCallbacks() { return callbacks; }
    // ~IMovementActor

    public void SetLowCoverStance()
    {
        CAIActor aiActor = GetAIActor();
        if (aiActor != null)
            aiActor.GetState().bodystate = (int)EStance.STANCE_LOW_COVER;
    }

    public CAIActor GetAIActor()
    {
        CAIActor aiActor = null;

        IEntity entity = gEnv.pEntitySystem?.GetEntity(entityID);
        if (entity != null)
        {
            IAIObject ai = entity.GetAI();
            if (ai != null)
                aiActor = ai.CastToCAIActor();
        }

        return aiActor;
    }

    public IPlanner planner;
    public uint entityID;
    public MovementRequestQueue requestQueue = new MovementRequestQueue();
    public MovementRequestID requestIdCurrentlyInPlanner;
    public bool lastPointInPathIsSmartObject;

    public MovementActorCallbacks callbacks = new MovementActorCallbacks();
    private IMovementActorAdapter pAdapter;
}

// -----------------------------------------------------------------------
// MovementPlan.h / .cpp
// -----------------------------------------------------------------------
public class Plan
{
    public const uint NoBlockIndex = uint.MaxValue;

    public Plan()
    {
        m_current = NoBlockIndex;
    }

    public enum Status
    {
        Running,
        Finished,
        CantBeFinished,
    }

    public void AddBlock<TBlock>() where TBlock : Block, new()
    {
        m_blocks.Add(new TBlock());
    }

    public void AddBlock(Block block)
    {
        m_blocks.Add(block);
    }

    public Status Execute(MovementUpdateContext context)
    {
        if (m_current == NoBlockIndex)
            ChangeToIndex(0, context.actor);

        while (true)
        {
            System.Diagnostics.Debug.Assert(m_current != NoBlockIndex);
            System.Diagnostics.Debug.Assert(m_current < (uint)m_blocks.Count);

            Block.Status status = m_blocks[(int)m_current].Update(context);

            if (status == Block.Status.Finished)
            {
                if (m_current + 1 < (uint)m_blocks.Count)
                {
                    ChangeToIndex(m_current + 1, context.actor);
                    continue;
                }
                else
                {
                    ChangeToIndex(NoBlockIndex, context.actor);
                    return Status.Finished;
                }
            }
            else if (status == Block.Status.CantBeFinished)
            {
                return Status.CantBeFinished;
            }
            else
            {
                System.Diagnostics.Debug.Assert(status == Block.Status.Running);
            }

            break;
        }

        return Status.Running;
    }

    public void ChangeToIndex(uint newIndex, IMovementActor actor)
    {
        uint oldIndex = m_current;

        if (oldIndex != NoBlockIndex)
            m_blocks[(int)oldIndex].End(actor);

        if (newIndex != NoBlockIndex)
            m_blocks[(int)newIndex].Begin(actor);

        m_current = newIndex;
    }

    public bool HasBlocks() { return m_blocks.Count > 0; }

    public void Clear(IMovementActor actor)
    {
        ChangeToIndex(NoBlockIndex, actor);
        m_blocks.Clear();
    }

    public void CutOffAfterCurrentBlock()
    {
        if (m_current < (uint)m_blocks.Count)
        {
            int newSize = (int)(m_current + 1);
            if (newSize < m_blocks.Count)
                m_blocks.RemoveRange(newSize, m_blocks.Count - newSize);
        }
    }

    public bool InterruptibleNow()
    {
        if (m_current == NoBlockIndex)
            return true;

        return m_blocks[(int)m_current].InterruptibleNow();
    }

    public uint GetCurrentBlockIndex() { return m_current; }
    public uint GetBlockCount() { return (uint)m_blocks.Count; }

    public Block GetBlock(uint index)
    {
        return m_blocks[(int)index];
    }

    private List<Block> m_blocks = new List<Block>();
    private uint m_current;
}

// -----------------------------------------------------------------------
// MovementHelpers.h / .cpp
// -----------------------------------------------------------------------
public static class Helpers
{
    public class StuckDetector
    {
        public StuckDetector()
        {
            m_accumulatedTimeAgentIsStuck = 0.0f;
            m_agentDistanceToTheEndInPreviousUpdate = FLT_MAX;
        }

        public void Update(MovementUpdateContext context)
        {
            uint actorEntityId = context.actor.GetEntityId();
            IEntity pEntity = gEnv.pEntitySystem?.GetEntity(actorEntityId);
            Vec3 pipeUserPosition = pEntity.GetPos();

            float distToEnd = context.pathFollower.GetDistToEnd(pipeUserPosition);
            float treshold = 0.05f;
            if (distToEnd + treshold < m_agentDistanceToTheEndInPreviousUpdate)
            {
                m_agentDistanceToTheEndInPreviousUpdate = distToEnd;
                m_accumulatedTimeAgentIsStuck = 0.0f;
            }
            else
            {
                m_accumulatedTimeAgentIsStuck += context.updateTime;
            }
        }

        public bool IsAgentStuck()
        {
            float maxAllowedTimeAgentCanBeStuck = 3.0f;
            return m_accumulatedTimeAgentIsStuck > maxAllowedTimeAgentCanBeStuck;
        }

        public void Reset()
        {
            m_accumulatedTimeAgentIsStuck = 0.0f;
            m_agentDistanceToTheEndInPreviousUpdate = FLT_MAX;
        }

        private float m_accumulatedTimeAgentIsStuck;
        private float m_agentDistanceToTheEndInPreviousUpdate;
    }

    public static void BeginPathFollowing(IMovementActor actor, MovementStyle style, CNavPath navPath)
    {
        actor.GetAdapter().SetActorPath(style, navPath);
        actor.GetAdapter().SetActorStyle(style, navPath);
    }

    // Returns false when target is unreachable
    public static bool UpdatePathFollowing(PathFollowResult result, MovementUpdateContext context, MovementStyle style)
    {
        context.actor.GetAdapter().ConfigurePathfollower(style);

        IPathFollower pathFollower = context.pathFollower;
        Vec3 position = context.actor.GetAdapter().GetPhysicsPosition();
        Vec3 velocity = context.actor.GetAdapter().GetVelocity();

        bool targetReachable = pathFollower.Update(result, position, velocity, context.updateTime);

        if (targetReachable)
        {
            context.actor.GetAdapter().SetMovementOutputValue(result);
        }
        else
        {
            context.actor.GetAdapter().ClearMovementState();
        }

        return targetReachable;
    }
}

// -----------------------------------------------------------------------
// MovementPlanner.h / .cpp -- IPlanner interface
// -----------------------------------------------------------------------
public interface IPlanner
{
    public class Status
    {
        public Status()
        {
            m_requestSatisfied = false;
            m_pathfinderFailed = false;
            m_movingAlongPathFailed = false;
            m_reachedMaxNumberOfReplansAllowed = false;
        }

        public void SetRequestSatisfied() { m_requestSatisfied = true; }
        public void SetPathfinderFailed() { m_pathfinderFailed = true; }
        public void SetMovingAlongPathFailed() { m_movingAlongPathFailed = true; }
        public void SetReachedMaxAllowedReplans() { m_reachedMaxNumberOfReplansAllowed = true; }

        public bool HasRequestBeenSatisfied() { return m_requestSatisfied; }
        public bool HasPathfinderFailed() { return m_pathfinderFailed; }
        public bool HasMovingAlongPathFailed() { return m_movingAlongPathFailed; }
        public bool HasReachedTheMaximumNumberOfReplansAllowed() { return m_reachedMaxNumberOfReplansAllowed; }

        private bool m_requestSatisfied;
        private bool m_pathfinderFailed;
        private bool m_movingAlongPathFailed;
        private bool m_reachedMaxNumberOfReplansAllowed;
    }

    bool IsUpdateNeeded();
    void StartWorkingOnRequest(MovementRequest request, MovementUpdateContext context);
    void CancelCurrentRequest(MovementActor actor);
    Status Update(MovementUpdateContext context);
    bool IsReadyForNewRequest();
    void GetStatus(MovementRequestStatus status);
}

// -----------------------------------------------------------------------
// MovementPlanner.h / .cpp -- GenericPlanner
// -----------------------------------------------------------------------
public class GenericPlanner : IPlanner
{
    public GenericPlanner()
    {
        m_pathfinderRequestQueued = false;
        m_amountOfFailedReplanning = 0;
    }

    public bool IsUpdateNeeded()
    {
        return m_plan.HasBlocks() || m_pathfinderRequestQueued;
    }

    public void StartWorkingOnRequest(MovementRequest request, MovementUpdateContext context)
    {
        m_amountOfFailedReplanning = 0;
        StartWorkingOnRequest_Internal(request, context);
    }

    private void StartWorkingOnRequest_Internal(MovementRequest request, MovementUpdateContext context)
    {
        System.Diagnostics.Debug.Assert(IsReadyForNewRequest());

        m_request = request;

        if (request.type == MovementRequest.Type.MoveTo)
        {
            if (!m_request.style.IsMovingAlongDesignedPath())
            {
                context.actor.RequestPathTo(request.destination, request.lengthToTrimFromThePathEnd,
                    request.dangersFlags, request.considerActorsAsPathObstacles);
                m_pathfinderRequestQueued = true;

                m_plan.CutOffAfterCurrentBlock();
            }
            else
            {
                ProducePlan(context);
            }
        }
        else if (request.type == MovementRequest.Type.Stop)
        {
            ProducePlan(context);
        }
        else
        {
            System.Diagnostics.Debug.Assert(false); // Unsupported request type
        }
    }

    public void CancelCurrentRequest(MovementActor actor)
    {
        // The request has been canceled but the plan remains intact.
    }

    public IPlanner.Status Update(MovementUpdateContext context)
    {
        IPlanner.Status status = new IPlanner.Status();

        if (m_pathfinderRequestQueued)
        {
            CheckOnPathfinder(context, status);

            if (status.HasPathfinderFailed())
            {
                return status;
            }
        }

        ExecuteCurrentPlan(context, status);

        return status;
    }

    public bool IsReadyForNewRequest()
    {
        if (m_pathfinderRequestQueued)
            return false;

        return m_plan.InterruptibleNow();
    }

    public void GetStatus(MovementRequestStatus status)
    {
        if (m_pathfinderRequestQueued)
        {
            status.id = MovementRequestStatus.ID.FindingPath;
        }
        else
        {
            status.id = MovementRequestStatus.ID.ExecutingPlan;
            status.currentBlockIndex = m_plan.GetCurrentBlockIndex();

            for (uint i = 0, n = m_plan.GetBlockCount(); i < n; ++i)
            {
                string blockName = m_plan.GetBlock(i).GetName();
                status.blockInfos.Add(new MovementRequestStatus.BlockInfo(blockName));
            }
        }
    }

    private void CheckOnPathfinder(MovementUpdateContext context, IPlanner.Status status)
    {
        Movement_PathfinderState state = context.actor.GetPathfinderState();

        bool pathfinderFinished = (state != Movement_PathfinderState.StillFinding);
        if (pathfinderFinished)
        {
            m_pathfinderRequestQueued = false;

            if (state == Movement_PathfinderState.FoundPath)
                ProducePlan(context);
            else
                status.SetPathfinderFailed();
        }
    }

    private void ExecuteCurrentPlan(MovementUpdateContext context, IPlanner.Status status)
    {
        if (m_plan.HasBlocks())
        {
            Plan.Status s = m_plan.Execute(context);

            if (s == Plan.Status.Finished)
            {
                status.SetRequestSatisfied();
                m_plan.Clear(context.actor);
            }
            else if (s == Plan.Status.CantBeFinished)
            {
                ++m_amountOfFailedReplanning;
                if (m_request.style.IsMovingAlongDesignedPath())
                {
                    status.SetMovingAlongPathFailed();
                }
                else if (IsReadyForNewRequest())
                {
                    if (CanReplan(m_request))
                    {
                        context.actor.Log("Movement plan couldn't be finished, re-planning.");
                        MovementRequest replanRequest = m_request;
                        StartWorkingOnRequest_Internal(replanRequest, context);
                    }
                    else
                    {
                        status.SetReachedMaxAllowedReplans();
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.Assert(s == Plan.Status.Running);
            }
        }
    }

    private void ProducePlan(MovementUpdateContext context)
    {
        System.Diagnostics.Debug.Assert(m_plan.InterruptibleNow());

        m_plan.Clear(context.actor);

        switch (m_request.type)
        {
        case MovementRequest.Type.MoveTo:
            ProduceMoveToPlan(context);
            break;
        case MovementRequest.Type.Stop:
            ProduceStopPlan(context);
            break;
        default:
            System.Diagnostics.Debug.Assert(false);
            break;
        }

        context.actor.GetAdapter().OnMovementPlanProduced();
    }

    private void ProduceMoveToPlan(MovementUpdateContext context)
    {
        if (m_request.style.IsMovingAlongDesignedPath())
        {
            SShape designedPath = new SShape();
            if (!context.actor.GetAdapter().GetDesignedPath(designedPath))
                return;

            if (designedPath.shape == null || designedPath.shape.Count == 0)
                return;

            TPathPoints fullPath = new TPathPoints();
            foreach (Vec3 pt in designedPath.shape)
                fullPath.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_UNSET, pt));
            CNavPath navPath = new CNavPath();
            navPath.SetPathPoints(fullPath);
            m_plan.AddBlock(new MovementBlocks.FollowPath(navPath, 0.0f, m_request.style, false));
        }
        else
        {
            INavPath pNavPath = context.actor.GetCallbacks().getPathFunction();
            TPathPoints fullPath = pNavPath.GetPath();

            if (m_request.style.ShouldTurnTowardsMovementDirectionBeforeMoving())
            {
                if (fullPath.Count >= 2)
                {
                    m_plan.AddBlock<MovementBlocks.HarshStop>();

                    int idx = 1;
                    Vec3 positionToTurnTowards = fullPath[idx].vPos;

                    float distanceThreshold = 0.2f;
                    float distanceThresholdSq = distanceThreshold * distanceThreshold;
                    Vec3 diff = positionToTurnTowards - context.actor.GetAdapter().GetPhysicsPosition();
                    float distanceToTurnPointSq = diff.GetLengthSquared();
                    if (distanceToTurnPointSq >= distanceThresholdSq)
                    {
                        m_plan.AddBlock(new MovementBlocks.TurnTowardsPosition(positionToTurnTowards));
                    }
                }
            }

            if (context.actor.GetAdapter().IsInCover())
            {
                m_plan.AddBlock(new MovementBlocks.UninstallAgentFromCover(m_request.style.GetStance()));
            }

            if (m_request.style.IsMovingToCover())
            {
                m_plan.AddBlock<MovementBlocks.SetupActorCoverInformation>();
            }

            // Go through the full path from start to end and split it up into
            // FollowPath & UseSmartObject blocks.
            int first = 0;
            int curr = 0;
            int end = fullPath.Count;

            MovementBlocks.UseSmartObject lastAddedSmartObjectBlock = null;

            System.Diagnostics.Debug.Assert(curr != end);

            while (curr != end)
            {
                int next = curr + 1;

                PathPointDescriptor point = fullPath[curr];

                bool isSmartObject = point.navType == IAISystem_ENavigationType.NAV_SMARTOBJECT;
                bool isCustomObject = point.navType == IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION;
                bool isLastNode = next == end;

                CNavPath path = new CNavPath();

                if (isCustomObject || isSmartObject || isLastNode)
                {
                    // Extract the path between the first point and
                    // the smart object/last node we just found.
                    TPathPoints points = new TPathPoints();
                    for (int i = first; i < next; i++)
                        points.Add(fullPath[i]);
                    path.SetPathPoints(points);

                    bool blockAfterThisIsUseExactPositioning = isLastNode && (m_request.style.GetExactPositioningRequest() != null);
                    bool blockAfterThisUsesSomeFormOfExactPositioning = isSmartObject || isCustomObject || blockAfterThisIsUseExactPositioning;
                    float endDistance = blockAfterThisUsesSomeFormOfExactPositioning ? 2.5f : 0.0f;
                    bool endsInCover = isLastNode && m_request.style.IsMovingToCover();
                    m_plan.AddBlock(new MovementBlocks.FollowPath(path, endDistance, m_request.style, endsInCover));

                    if (lastAddedSmartObjectBlock != null)
                    {
                        lastAddedSmartObjectBlock.SetUpcomingPath(path);
                        lastAddedSmartObjectBlock.SetUpcomingStyle(m_request.style);
                    }

                    if (blockAfterThisIsUseExactPositioning)
                    {
                        System.Diagnostics.Debug.Assert(!isSmartObject);
                        System.Diagnostics.Debug.Assert(!path.Empty());
                        m_plan.AddBlock(new MovementBlocks.UseExactPositioning(path, m_request.style));
                    }
                }

                if (isSmartObject || isCustomObject)
                {
                    System.Diagnostics.Debug.Assert(!path.Empty());
                    if (isSmartObject)
                    {
                        MovementBlocks.UseSmartObject useSmartObjectBlock = new MovementBlocks.UseSmartObject(path, point.offMeshLinkData, m_request.style);
                        lastAddedSmartObjectBlock = useSmartObjectBlock;
                        m_plan.AddBlock(useSmartObjectBlock);
                    }
                    else
                    {
                        MovementSystem aiMovementSystem = (MovementSystem)context.movementSystem;
                        m_plan.AddBlock(aiMovementSystem.CreateCustomBlock(path, point.offMeshLinkData, m_request.style));
                    }

                    curr = next;
                    first = curr;
                }
                else
                {
                    curr = next;
                }
            }

            if (m_request.style.IsMovingToCover())
            {
                m_plan.AddBlock<MovementBlocks.InstallAgentInCover>();
            }
        }
    }

    private void ProduceStopPlan(MovementUpdateContext context)
    {
        m_plan.AddBlock(new MovementBlocks.HarshStop());
    }

    private bool CanReplan(MovementRequest request)
    {
        return m_amountOfFailedReplanning < s_maxAllowedReplanning;
    }

    private Plan m_plan = new Plan();
    private MovementRequest m_request;
    private byte m_amountOfFailedReplanning;
    private bool m_pathfinderRequestQueued;
    private const byte s_maxAllowedReplanning = 3;
}

// -----------------------------------------------------------------------
// Movement Blocks
// -----------------------------------------------------------------------
namespace MovementBlocks
{

// -----------------------------------------------------------------------
// MovementBlock_DefaultEmpty.h
// -----------------------------------------------------------------------
public class DefaultEmpty : Block
{
    public override Block.Status Update(MovementUpdateContext context)
    {
        AIError("Trying to use the DefaultEmpty block, an undefined implementation of a movement block.");
        return Status.CantBeFinished;
    }
    public override string GetName() { return "DefaultEmpty"; }
}

// -----------------------------------------------------------------------
// MovementBlock_FollowPath.h / .cpp
// -----------------------------------------------------------------------
public class FollowPath : Block
{
    public FollowPath(CNavPath path, float endDistance, MovementStyle style, bool endsInCover)
    {
        m_path = path;
        m_finishBlockEndDistance = endDistance;
        m_accumulatedPathFollowerFailureTime = 0.0f;
        m_style = style;
        m_endsInCover = endsInCover;
    }

    public override bool InterruptibleNow() { return true; }

    public override void Begin(IMovementActor actor)
    {
        m_accumulatedPathFollowerFailureTime = 0.0f;
        m_stuckDetector.Reset();
        Helpers.BeginPathFollowing(actor, m_style, m_path);

        if (m_style.ShouldGlanceInMovementDirection())
        {
            float lookTimeOffset = -1.5f;
            actor.GetAdapter().SetLookTimeOffset(lookTimeOffset);
            m_lookTarget = actor.GetAdapter().CreateLookTarget();
        }
    }

    public override void End(IMovementActor actor)
    {
        m_lookTarget = null;
    }

    public override Block.Status Update(MovementUpdateContext context)
    {
        context.actor.GetAdapter().ResetMovementContext();

        if (m_endsInCover)
            context.actor.GetAdapter().UpdateCoverLocations();

        PathFollowResult result = new PathFollowResult();
        bool targetReachable = Helpers.UpdatePathFollowing(result, context, m_style);

        Vec3 physicsPosition = context.actor.GetAdapter().GetPhysicsPosition();
        float pathDistanceToEnd = context.pathFollower.GetDistToEnd(physicsPosition);

        context.actor.GetAdapter().UpdateLooking(context.updateTime,
            m_lookTarget ?? new Vec3(0, 0, 0), targetReachable,
            pathDistanceToEnd, result.followTargetPos, m_style);

        m_stuckDetector.Update(context);

        if (m_stuckDetector.IsAgentStuck())
        {
            return Block.Status.CantBeFinished;
        }

        if (!targetReachable)
        {
            m_accumulatedPathFollowerFailureTime += context.updateTime;

            if (m_accumulatedPathFollowerFailureTime > 3.0f)
            {
                return Block.Status.CantBeFinished;
            }
        }

        if (m_finishBlockEndDistance > 0.0f)
        {
            if (pathDistanceToEnd < m_finishBlockEndDistance)
            {
                return Block.Status.Finished;
            }
        }

        IMovementActor actor = context.actor;

        if (result.reachedEnd && !actor.IsLastPointInPathASmartObject())
        {
            Vec3 velocity = actor.GetAdapter().GetVelocity();
            Vec3 groundVelocity = new Vec3(velocity.x, velocity.y, 0.0f);
            Vec3 moveDir = actor.GetAdapter().GetMoveDirection();
            float speed = groundVelocity.Dot(moveDir);
            bool stopped = speed < 0.01f;

            if (stopped)
            {
                return Block.Status.Finished;
            }
        }

        return Block.Status.Running;
    }

    public override string GetName() { return "FollowPath"; }

    private CNavPath m_path;
    private MovementStyle m_style;
    private Helpers.StuckDetector m_stuckDetector = new Helpers.StuckDetector();
    private Vec3? m_lookTarget;
    private float m_finishBlockEndDistance;
    private float m_accumulatedPathFollowerFailureTime;
    private bool m_endsInCover;
}

// -----------------------------------------------------------------------
// MovementBlock_HarshStop.h / .cpp
// -----------------------------------------------------------------------
public class HarshStop : Block
{
    public override void Begin(IMovementActor actor)
    {
        actor.GetAdapter().ClearMovementState();
    }

    public override Block.Status Update(MovementUpdateContext context)
    {
        bool stopped = !context.actor.GetAdapter().IsMoving();
        return stopped ? Status.Finished : Status.Running;
    }

    public override string GetName() { return "HarshStop"; }
}

// -----------------------------------------------------------------------
// MovementBlock_TurnTowardsPosition.h / .cpp
// -----------------------------------------------------------------------
public class TurnTowardsPosition : Block
{
    public TurnTowardsPosition(Vec3 position)
    {
        m_positionToTurnTowards = position;
        m_timeSpentAligning = 0.0f;
        m_correctBodyDirTime = 0.0f;
    }

    public override void End(IMovementActor actor)
    {
        actor.GetAdapter().ResetBodyTarget();
    }

    public override Block.Status Update(MovementUpdateContext context)
    {
        // Align body towards the move target
        Vec3 actorPhysicalPosition = context.actor.GetAdapter().GetPhysicsPosition();
        Vec3 dirToMoveTarget = m_positionToTurnTowards - actorPhysicalPosition;
        dirToMoveTarget.z = 0.0f;
        dirToMoveTarget.Normalize();
        context.actor.GetAdapter().SetBodyTargetDirection(dirToMoveTarget);

        Vec3 actualBodyDir = context.actor.GetAdapter().GetAnimationBodyDirection();
        bool lookingTowardsMoveTarget = (actualBodyDir.Dot(dirToMoveTarget) > cosf(DEG2RAD(17.0f)));
        if (lookingTowardsMoveTarget)
            m_correctBodyDirTime += context.updateTime;
        else
            m_correctBodyDirTime = 0.0f;

        float timeSpentAligning = m_timeSpentAligning + context.updateTime;
        m_timeSpentAligning = timeSpentAligning;

        if (m_correctBodyDirTime > 0.2f)
            return Block.Status.Finished;

        float timeout = 8.0f;
        if (timeSpentAligning > timeout)
        {
            gEnv.pLog?.LogWarning(string.Format(
                "Agent '{0}' at {1} {2} {3} failed to turn towards {4} {5} {6} within {7} seconds. Proceeding anyway.",
                context.actor.GetName(),
                actorPhysicalPosition.x, actorPhysicalPosition.y, actorPhysicalPosition.z,
                m_positionToTurnTowards.x, m_positionToTurnTowards.y, m_positionToTurnTowards.z, timeout));
            return Block.Status.Finished;
        }

        return Block.Status.Running;
    }

    public override bool InterruptibleNow() { return true; }
    public override string GetName() { return "TurnTowardsPosition"; }

    private Vec3 m_positionToTurnTowards;
    private float m_timeSpentAligning;
    private float m_correctBodyDirTime;
}

// -----------------------------------------------------------------------
// MovementBlock_InstallAgentInCover.h
// -----------------------------------------------------------------------
public class InstallAgentInCover : Block
{
    public override void Begin(IMovementActor actor)
    {
        actor.GetAdapter().InstallInLowCover(true);
    }
    public override string GetName() { return "InstallInCover"; }
}

// -----------------------------------------------------------------------
// MovementBlock_UninstallAgentFromCover.h
// -----------------------------------------------------------------------
public class UninstallAgentFromCover : Block
{
    public UninstallAgentFromCover(MovementStyle.Stance stance)
    {
        m_stance = stance;
    }

    public override void Begin(IMovementActor actor)
    {
        actor.GetAdapter().SetInCover(false);
        actor.GetAdapter().SetStance(m_stance);
    }

    public override string GetName() { return "UninstallFromCover"; }

    private MovementStyle.Stance m_stance;
}

// -----------------------------------------------------------------------
// MovementBlock_SetupPipeUserCoverInformation.h / .cpp
// -----------------------------------------------------------------------
public class SetupActorCoverInformation : Block
{
    public override void Begin(IMovementActor actor)
    {
        actor.GetAdapter().SetupCoverInformation();
    }
    public override string GetName() { return "SetCoverInfo"; }
}

// -----------------------------------------------------------------------
// MovementBlock_UseExactPositioningBase.h / .cpp
// -----------------------------------------------------------------------
public class UseExactPositioningBase : Block
{
    public UseExactPositioningBase(CNavPath path, MovementStyle style)
    {
        m_path = path;
        m_style = style;
        m_state = State.Prepare;
        m_accumulatedPathFollowerFailureTime = 0.0f;
    }

    public override void Begin(IMovementActor actor)
    {
        m_accumulatedPathFollowerFailureTime = 0.0f;
        actor.GetAdapter().SetActorStyle(m_style, m_path);
        m_stuckDetector.Reset();
    }

    public override void End(IMovementActor actor)
    {
        actor.GetAdapter().ClearMovementState();
    }

    public override Block.Status Update(MovementUpdateContext context)
    {
        if (m_state == State.Prepare)
        {
            return UpdatePrepare(context);
        }
        else
        {
            System.Diagnostics.Debug.Assert(m_state == State.Traverse);
            return UpdateTraverse(context);
        }
    }

    protected enum TryRequestingExactPositioningResult
    {
        RequestSucceeded,
        RequestDelayed_ContinuePathFollowing,
        RequestDelayed_SkipPathFollowing,
        RequestFailed_FinishImmediately,
    }

    protected virtual TryRequestingExactPositioningResult TryRequestingExactPositioning(MovementUpdateContext context)
    {
        return TryRequestingExactPositioningResult.RequestFailed_FinishImmediately;
    }

    protected virtual void HandleExactPositioningError(MovementUpdateContext context) {}

    protected virtual void OnTraverseStarted(MovementUpdateContext context) {}

    private Block.Status UpdatePrepare(MovementUpdateContext context)
    {
        EActorTargetPhase targetPhase = context.actor.GetAdapter().GetActorPhase();

        if (targetPhase == EActorTargetPhase.eATP_Error)
        {
            HandleExactPositioningError(context);
            return Block.Status.Finished;
        }

        if (targetPhase == EActorTargetPhase.eATP_Starting || targetPhase == EActorTargetPhase.eATP_Started)
        {
            context.actor.GetAdapter().ClearMovementState();
            m_state = State.Traverse;
            OnTraverseStarted(context);
            return Block.Status.Running;
        }

        if (targetPhase == EActorTargetPhase.eATP_None)
        {
            TryRequestingExactPositioningResult result = TryRequestingExactPositioning(context);

            switch (result)
            {
            case TryRequestingExactPositioningResult.RequestSucceeded:
                {
                    {
                        INavPath navPath = context.actor.GetCallbacks().getPathFunction();
                        if (navPath != null)
                            navPath.GetParams().inhibitPathRegeneration = true;
                    }
                }
                break;
            case TryRequestingExactPositioningResult.RequestDelayed_ContinuePathFollowing:
                break;
            case TryRequestingExactPositioningResult.RequestDelayed_SkipPathFollowing:
                return Status.Running;
            case TryRequestingExactPositioningResult.RequestFailed_FinishImmediately:
                return Status.Finished;
            }
        }

        System.Diagnostics.Debug.Assert(
            (targetPhase == EActorTargetPhase.eATP_None) || (targetPhase == EActorTargetPhase.eATP_Waiting));

        PathFollowResult pathResult = new PathFollowResult();
        bool targetReachable = Helpers.UpdatePathFollowing(pathResult, context, m_style);

        m_stuckDetector.Update(context);

        if (m_stuckDetector.IsAgentStuck())
        {
            return Block.Status.CantBeFinished;
        }

        if (!targetReachable)
        {
            m_accumulatedPathFollowerFailureTime += context.updateTime;

            if (m_accumulatedPathFollowerFailureTime > 3.0f)
            {
                return Block.Status.CantBeFinished;
            }
        }

        return Block.Status.Running;
    }

    private Block.Status UpdateTraverse(MovementUpdateContext context)
    {
        EActorTargetPhase phase = context.actor.GetAdapter().GetActorPhase();

        if (phase == EActorTargetPhase.eATP_None || phase == EActorTargetPhase.eATP_StartedAndFinished)
        {
            return Status.Finished;
        }

        return Status.Running;
    }

    protected enum State
    {
        Prepare,
        Traverse,
    }

    protected MovementStyle m_style;
    protected State m_state;

    private CNavPath m_path;
    private float m_accumulatedPathFollowerFailureTime;
    private Helpers.StuckDetector m_stuckDetector = new Helpers.StuckDetector();
}

// -----------------------------------------------------------------------
// MovementBlock_UseExactPositioning.h / .cpp
// -----------------------------------------------------------------------
public class UseExactPositioning : UseExactPositioningBase
{
    public UseExactPositioning(CNavPath path, MovementStyle style)
        : base(path, style)
    {
    }

    public override string GetName() { return "UseExactPositioning"; }
    public override bool InterruptibleNow() { return true; }

    protected override TryRequestingExactPositioningResult TryRequestingExactPositioning(MovementUpdateContext context)
    {
        System.Diagnostics.Debug.Assert(m_style.GetExactPositioningRequest() != null);
        if (m_style.GetExactPositioningRequest() == null)
        {
            return TryRequestingExactPositioningResult.RequestFailed_FinishImmediately;
        }

        bool useLowerPrecision = false;
        context.actor.GetAdapter().RequestExactPosition(m_style.GetExactPositioningRequest(), useLowerPrecision);

        return TryRequestingExactPositioningResult.RequestSucceeded;
    }

    protected override void HandleExactPositioningError(MovementUpdateContext context)
    {
        uint actorEntityId = context.actor.GetEntityId();
        IEntity entity = gEnv.pEntitySystem?.GetEntity(actorEntityId);
        if (entity != null)
        {
            string message = "Exact positioning failed to get me to the start of the actor target or was canceled incorrectly.";
            AIQueueBubbleMessage("PrepareForExactPositioningError", actorEntityId, message,
                EBubbleNotificationSystemFlags.eBNS_Balloon | EBubbleNotificationSystemFlags.eBNS_LogWarning);
        }
    }
}

// -----------------------------------------------------------------------
// MovementBlock_UseSmartObject.h / .cpp
// -----------------------------------------------------------------------
public class UseSmartObject : UseExactPositioningBase
{
    public UseSmartObject(CNavPath path, PathPointDescriptor.OffMeshLinkData mnmData, MovementStyle style)
        : base(path, style)
    {
        m_smartObjectMNMData = mnmData;
        m_timeSpentWaitingForSmartObjectToBecomeFree = 0.0f;
    }

    public override string GetName() { return "UseSmartObject"; }
    public override bool InterruptibleNow() { return m_state == State.Prepare; }

    public override void Begin(IMovementActor actor)
    {
        base.Begin(actor);
        m_timeSpentWaitingForSmartObjectToBecomeFree = 0.0f;
    }

    public override Block.Status Update(MovementUpdateContext context)
    {
        Block.Status baseStatus = base.Update(context);

        if (m_state == State.Traverse)
        {
            PathFollowResult result = new PathFollowResult();
            Helpers.UpdatePathFollowing(result, context, m_upcomingStyle);
        }

        return baseStatus;
    }

    public void SetUpcomingPath(CNavPath upcomingPath)
    {
        m_upcomingPath = upcomingPath;
    }

    public void SetUpcomingStyle(MovementStyle upcomingStyle)
    {
        m_upcomingStyle = upcomingStyle;
    }

    protected override void OnTraverseStarted(MovementUpdateContext context)
    {
        Helpers.BeginPathFollowing(context.actor, m_upcomingStyle, m_upcomingPath);
    }

    protected override TryRequestingExactPositioningResult TryRequestingExactPositioning(MovementUpdateContext context)
    {
        NavMNM.OffMeshNavigation offMeshNavigation = gAIEnv.pNavigationSystem?.GetOffMeshNavigationManager()?.GetOffMeshNavigationForMesh(new NavigationMeshID { id = m_smartObjectMNMData.meshID });
        NavMNM.OffMeshLink pOffMeshLink = offMeshNavigation?.GetObjectLinkInfo(m_smartObjectMNMData.offMeshLinkID);
        NavMNM.OffMeshLink_SmartObject pSOLink = pOffMeshLink?.CastTo_SmartObject();

        System.Diagnostics.Debug.Assert(pSOLink != null);
        if (pSOLink != null)
        {
            CSmartObjectManager pSmartObjectManager = gAIEnv.pSmartObjectManager;
            CSmartObject pSmartObject = pSOLink.m_pSmartObject;

            if (pSmartObjectManager != null && pSmartObjectManager.IsSmartObjectBusy(pSmartObject))
            {
                context.actor.GetAdapter().ClearMovementState();

                m_timeSpentWaitingForSmartObjectToBecomeFree += gEnv.pTimer?.GetFrameTime() ?? 0.0f;
                if (m_timeSpentWaitingForSmartObjectToBecomeFree > 10.0f)
                {
                    uint actorEntityId = context.actor.GetEntityId();
                    IEntity entity = gEnv.pEntitySystem?.GetEntity(actorEntityId);
                    if (entity != null)
                    {
                        Vec3 teleportDestination = GetSmartObjectEndPosition();

                        string message = string.Format(
                            "Agent spent too long waiting for a smart object to become free. UseSmartObject spent too long waiting. Teleported to end of smart object ({0}, {1}, {2})",
                            teleportDestination.x, teleportDestination.y, teleportDestination.z);
                        AIQueueBubbleMessage("WaitForSmartObjectToBecomeFree", actorEntityId, message,
                            EBubbleNotificationSystemFlags.eBNS_Balloon | EBubbleNotificationSystemFlags.eBNS_LogWarning);

                        Matrix34 transform = entity.GetWorldTM();
                        transform.SetTranslation(teleportDestination);
                        entity.SetWorldTM(transform);

                        return TryRequestingExactPositioningResult.RequestFailed_FinishImmediately;
                    }
                }

                return TryRequestingExactPositioningResult.RequestDelayed_SkipPathFollowing;
            }
            else if (context.actor.GetAdapter().IsClosestToUseTheSmartObject(pSOLink))
            {
                if (context.actor.GetAdapter().PrepareNavigateSmartObject(pSmartObject, pSOLink))
                {
                    return TryRequestingExactPositioningResult.RequestSucceeded;
                }
                else
                {
                    context.actor.GetAdapter().ResetActorTargetRequest();
                    context.actor.GetAdapter().InvalidateSmartObjectLink(pSmartObject, pSOLink);

                    System.Diagnostics.Debug.Assert(false);

                    return TryRequestingExactPositioningResult.RequestFailed_FinishImmediately;
                }
            }
            else
            {
                return TryRequestingExactPositioningResult.RequestDelayed_ContinuePathFollowing;
            }
        }
        else
        {
            System.Diagnostics.Debug.Assert(false);
            return TryRequestingExactPositioningResult.RequestDelayed_ContinuePathFollowing;
        }
    }

    protected override void HandleExactPositioningError(MovementUpdateContext context)
    {
        uint actorEntityId = context.actor.GetEntityId();
        IEntity entity = gEnv.pEntitySystem?.GetEntity(actorEntityId);
        if (entity != null)
        {
            Vec3 teleportDestination = GetSmartObjectEndPosition();

            string message = string.Format(
                "Exact positioning failed to get me to the start of the smart object. Teleported to end of smart object ({0}, {1}, {2})",
                teleportDestination.x, teleportDestination.y, teleportDestination.z);
            AIQueueBubbleMessage("PrepareForSmartObjectError", actorEntityId, message,
                EBubbleNotificationSystemFlags.eBNS_Balloon | EBubbleNotificationSystemFlags.eBNS_LogWarning);

            Matrix34 transform = entity.GetWorldTM();
            transform.SetTranslation(teleportDestination);
            entity.SetWorldTM(transform);

            context.actor.GetAdapter().ResetActorTargetRequest();
        }
    }

    private Vec3 GetSmartObjectEndPosition()
    {
        NavMNM.OffMeshNavigation offMeshNavigation = gAIEnv.pNavigationSystem?.GetOffMeshNavigationManager()?.GetOffMeshNavigationForMesh(new NavigationMeshID { id = m_smartObjectMNMData.meshID });
        NavMNM.OffMeshLink pOffMeshLink = offMeshNavigation?.GetObjectLinkInfo(m_smartObjectMNMData.offMeshLinkID);
        NavMNM.OffMeshLink_SmartObject pSOLink = pOffMeshLink?.CastTo_SmartObject();

        System.Diagnostics.Debug.Assert(pSOLink != null);
        if (pSOLink != null)
        {
            CSmartObject pSmartObject = pSOLink.m_pSmartObject;
            Vec3 endPosition = pSmartObject.GetHelperPos(pSOLink.m_pToHelper);
            return endPosition;
        }

        return new Vec3(0, 0, 0);
    }

    private CNavPath m_upcomingPath;
    private MovementStyle m_upcomingStyle;
    private PathPointDescriptor.OffMeshLinkData m_smartObjectMNMData;
    private float m_timeSpentWaitingForSmartObjectToBecomeFree;
}

} // namespace MovementBlocks

// -----------------------------------------------------------------------
// MovementSystem.h / .cpp
// -----------------------------------------------------------------------
public class MovementSystem : IMovementSystem
{
    public MovementSystem()
    {
        m_nextUniqueRequestID = 0;
    }

    // IMovementSystem
    public void RegisterEntity(uint entityId, MovementActorCallbacks callbacksConfiguration, IMovementActorAdapter adapter)
    {
        MovementActor actor = GetExistingActorOrCreateNewOne(entityId, adapter);
        actor.callbacks = callbacksConfiguration;
    }

    public void UnregisterEntity(uint entityId)
    {
        m_actors.RemoveAll(a => a.entityID == entityId);
    }

    public MovementRequestID QueueRequest(MovementRequest request)
    {
        System.Diagnostics.Debug.Assert(request.entityID != 0);

        MovementRequestID requestID = GenerateUniqueMovementRequestID();

        MovementActor pActor = GetExistingActor(request.entityID);

        if (pActor == null)
        {
            return MovementRequestID.Invalid();
        }

        pActor.requestQueue.AddLast(requestID);

        m_requests.Add(new KeyValuePair<MovementRequestID, MovementRequest>(requestID, request));

        return requestID;
    }

    public void CancelRequest(MovementRequestID requestID)
    {
        int requestIdx = m_requests.FindIndex(r => r.Key == requestID);

        if (requestIdx >= 0)
        {
            MovementRequest request = m_requests[requestIdx].Value;

            MovementActor actor = m_actors.Find(a => a.entityID == request.entityID);

            if (actor != null)
            {
                if (IsPlannerWorkingOnRequestID(actor, requestID))
                {
                    actor.planner.CancelCurrentRequest(actor);
                    actor.requestIdCurrentlyInPlanner = 0;
                }

                // stl::find_and_erase
                var node = actor.requestQueue.Find(requestID);
                if (node != null) actor.requestQueue.Remove(node);
            }

            m_requests.RemoveAt(requestIdx);
        }
    }

    public void GetRequestStatus(MovementRequestID requestID, MovementRequestStatus status)
    {
        status.id = MovementRequestStatus.ID.NotQueued;

        foreach (MovementActor actor in m_actors)
        {
            // Is the request being processed right now?
            if (IsPlannerWorkingOnRequestID(actor, requestID))
            {
                actor.planner.GetStatus(status);
                return;
            }

            // See if it's at least queued
            foreach (MovementRequestID rid in actor.requestQueue)
            {
                if (rid == requestID)
                {
                    status.id = MovementRequestStatus.ID.Queued;
                    return;
                }
            }
        }
    }

    public void Update(float updateTime)
    {
        UpdateActors(updateTime);
    }

    public void Reset()
    {
        m_nextUniqueRequestID = 0;
        m_actors.Clear();
        m_requests.Clear();
    }

    public void RegisterFunctionToConstructMovementBlockForCustomNavigationType(CustomNavigationBlockCreatorFunction blockFactoryFunction)
    {
        m_createMovementBlockToHandleCustomNavigationType = blockFactoryFunction;
    }
    // ~IMovementSystem

    public Block CreateCustomBlock(CNavPath path, PathPointDescriptor.OffMeshLinkData mnmData, MovementStyle style)
    {
        if (m_createMovementBlockToHandleCustomNavigationType != null)
        {
            return m_createMovementBlockToHandleCustomNavigationType(path, mnmData, style);
        }
        else
        {
            AIError("The movement block to handle a path point of the NAV_CUSTOM_NAVIGATION type is not defined.");
            return new MovementBlocks.DefaultEmpty();
        }
    }

    private enum ActorUpdateStatus
    {
        KeepUpdatingActor,
        ActorCanBeRemoved
    }

    private MovementRequestID GenerateUniqueMovementRequestID()
    {
        // while (!++m_nextUniqueRequestID) — increment, skip zero
        do { m_nextUniqueRequestID++; } while (m_nextUniqueRequestID == 0);
        return m_nextUniqueRequestID;
    }

    private void FinishRequest(
        MovementActor actor,
        MovementRequestResult.Result resultID,
        MovementRequestResult.FailureReason failureReason = MovementRequestResult.FailureReason.NoReason)
    {
        MovementRequestID requestID = new MovementRequestID(0);
        MovementRequest activeRequest = GetActiveRequest(actor, ref requestID);
        MovementRequest.Callback callback = activeRequest.callback;

        if (callback != null)
        {
            MovementRequestResult result = new MovementRequestResult(requestID, resultID, failureReason);
            callback(result); // Caution! The user callback code might invalidate iterators.
        }

        CleanUpAfterFinishedRequest(actor);
    }

    private void CleanUpAfterFinishedRequest(MovementActor actor)
    {
        MovementRequestQueue queue = actor.requestQueue;
        System.Diagnostics.Debug.Assert(queue.Count > 0);

        // Erase the active request (first request in queue)
        MovementRequestID requestID = queue.First.Value;
        int idx = m_requests.FindIndex(r => r.Key == requestID);
        if (idx >= 0)
            m_requests.RemoveAt(idx);

        queue.RemoveFirst();

        actor.requestIdCurrentlyInPlanner = 0;
    }

    private MovementRequest GetActiveRequest(MovementActor actor, ref MovementRequestID outRequestID)
    {
        MovementRequestQueue queue = actor.requestQueue;
        System.Diagnostics.Debug.Assert(queue.Count > 0);

        MovementRequestID requestID;
        if (queue.Count > 0)
            requestID = queue.First.Value;
        else
            requestID = new MovementRequestID(0);

        outRequestID = requestID;

        int idx = m_requests.FindIndex(r => r.Key == requestID);

        if (idx >= 0)
            return m_requests[idx].Value;
        else
        {
            System.Diagnostics.Debug.Assert(false);
            return new MovementRequest(); // dummy
        }
    }

    // Overload without outRequestID
    private MovementRequest GetActiveRequest(MovementActor actor)
    {
        MovementRequestID dummy = new MovementRequestID(0);
        return GetActiveRequest(actor, ref dummy);
    }

    private MovementActor GetExistingActorOrCreateNewOne(uint entityId, IMovementActorAdapter adapter)
    {
        MovementActor existing = m_actors.Find(a => a.entityID == entityId);

        if (existing != null)
        {
            return existing;
        }
        else
        {
            MovementActor actor = new MovementActor(entityId, adapter);
            actor.planner = new GenericPlanner();
            m_actors.Add(actor);
            return actor;
        }
    }

    private MovementActor GetExistingActor(uint entityId)
    {
        return m_actors.Find(a => a.entityID == entityId);
    }

    private bool IsPlannerWorkingOnRequestID(MovementActor actor, MovementRequestID id)
    {
        return (actor.requestIdCurrentlyInPlanner == id) && (id != MovementRequestID.Invalid());
    }

    private void UpdateActors(float updateTime)
    {
        foreach (MovementActor actor in m_actors)
        {
            ActorUpdateStatus status = UpdateActor(actor, updateTime);
        }
    }

    private static bool IsActorValidForMovementUpdateContextCreation(MovementActor actor)
    {
        if (actor.callbacks.getPathFollowerFunction != null && actor.callbacks.getPathFollowerFunction() != null)
        {
            if (actor.planner == null)
                return false;
            return true;
        }
        return false;
    }

    private ActorUpdateStatus UpdateActor(MovementActor actor, float updateTime)
    {
        if (!IsActorValidForMovementUpdateContextCreation(actor))
        {
            return ActorUpdateStatus.ActorCanBeRemoved;
        }

        IPathFollower pathFollower = actor.callbacks.getPathFollowerFunction();
        System.Diagnostics.Debug.Assert(pathFollower != null);

        MovementUpdateContext context = new MovementUpdateContext(
            actor,
            this,
            pathFollower,
            actor.planner,
            updateTime);

        StartWorkingOnNewRequestIfPossible(context);

        if (context.planner.IsUpdateNeeded())
        {
            UpdatePlannerAndDealWithResult(context);
        }

        if (actor.requestQueue.Count > 0 || context.planner.IsUpdateNeeded())
            return ActorUpdateStatus.KeepUpdatingActor;
        else
            return ActorUpdateStatus.ActorCanBeRemoved;
    }

    private void StartWorkingOnNewRequestIfPossible(MovementUpdateContext context)
    {
        MovementActor actor = (MovementActor)context.actor;
        MovementRequestQueue requestQueue = actor.requestQueue;

        if (requestQueue.Count > 0)
        {
            MovementRequestID frontRequestID = requestQueue.First.Value;
            IPlanner planner = context.planner;

            if (!IsPlannerWorkingOnRequestID(actor, frontRequestID) && planner.IsReadyForNewRequest())
            {
                planner.StartWorkingOnRequest(GetActiveRequest(actor), context);
                actor.requestIdCurrentlyInPlanner = frontRequestID;
            }
        }
    }

    private void UpdatePlannerAndDealWithResult(MovementUpdateContext context)
    {
        IPlanner planner = context.planner;
        IPlanner.Status status = planner.Update(context);

        MovementActor actor = (MovementActor)context.actor;
        MovementRequestQueue requestQueue = actor.requestQueue;

        if (requestQueue.Count > 0 && IsPlannerWorkingOnRequestID(actor, requestQueue.First.Value))
        {
            if (status.HasPathfinderFailed())
                FinishRequest(actor, MovementRequestResult.Result.Failure, MovementRequestResult.FailureReason.CouldNotFindPathToRequestedDestination);
            else if (status.HasMovingAlongPathFailed())
                FinishRequest(actor, MovementRequestResult.Result.Failure, MovementRequestResult.FailureReason.CouldNotMoveAlongDesignerDesignedPath);
            else if (status.HasReachedTheMaximumNumberOfReplansAllowed())
                FinishRequest(actor, MovementRequestResult.Result.Failure, MovementRequestResult.FailureReason.FailedToProduceSuccessfulPlanAfterMaximumNumberOfAttempts);
            else if (status.HasRequestBeenSatisfied())
                FinishRequest(actor, MovementRequestResult.Result.Success);
        }
    }

    // Contains pairs of MovementRequestID and MovementRequest
    private List<KeyValuePair<MovementRequestID, MovementRequest>> m_requests = new List<KeyValuePair<MovementRequestID, MovementRequest>>();

    // The actors are added and removed when they get new requests and
    // when they run out of them.
    private List<MovementActor> m_actors = new List<MovementActor>();

    private MovementRequestID m_nextUniqueRequestID;
    private CustomNavigationBlockCreatorFunction m_createMovementBlockToHandleCustomNavigationType;
}

} // namespace CryAISystem.Movement
