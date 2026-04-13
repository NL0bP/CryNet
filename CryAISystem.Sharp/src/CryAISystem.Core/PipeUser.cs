// Literal port of dev/Code/CryEngine/CryAISystem/PipeUser.h + PipeUser.cpp (5019L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CryAISystem.CryMath;
using static CryAISystem.AISignalConstants;
using static CryAISystem.AIPhysConstants;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.NilRefHelper;
using static CryAISystem.EAIEvent;
using static CryAISystem.EAIObjectType;
using CryAISystem.CryCommon;

namespace CryAISystem;

// created by Petar

public struct CoverUsageInfo
{
    public CoverUsageInfo(int dummy = 0)
    {
        lowLeft = false; lowCenter = false; lowRight = false;
        highLeft = false; highCenter = false; highRight = false;
        lowCompromised = false; highCompromised = false;
    }
    public CoverUsageInfo(bool state)
    {
        lowLeft = state; lowCenter = state; lowRight = state;
        highLeft = state; highCenter = state; highRight = state;
        lowCompromised = state; highCompromised = state;
    }
    public bool lowLeft, lowCenter, lowRight, lowCompromised;
    public bool highLeft, highCenter, highRight, highCompromised;
}

public enum EAimState { AI_AIM_NONE, AI_AIM_WAITING, AI_AIM_OBSTRUCTED, AI_AIM_READY, AI_AIM_FORCED }

public class CPipeUser : CAIActor, IAIPathAgent
{
    private static bool bNotifyListenersLock = false;

    // ctor — PipeUser.cpp:41-106
    public CPipeUser()
    {
        m_fTimePassed = 0; m_adjustingAim = false; m_inCover = false; m_movingToCover = false;
        m_movingInCover = false; m_pPathFollower = null; m_bPathfinderConsidersPathTargetDirection = true;
        m_vLastMoveDir = new Vec3(0,0,0); m_bKeepMoving = false;
        m_nPathDecision = (int)EPathfinderResult.PATHFINDER_NOPATH; m_queuedPathId = 0;
        m_bFirstUpdate = true; m_bBlocked = false; m_pCurrentGoalPipe = null;
        m_fireMode = EFireMode.FIREMODE_OFF; m_fireModeUpdated = false; m_bLooseAttention = false;
        m_IsSteering = false; m_AttTargetPersistenceTimeout = 0.0f;
        m_AttTargetThreat = EAITargetThreat.AITHREAT_NONE; m_AttTargetExposureThreat = EAITargetThreat.AITHREAT_NONE;
        m_AttTargetType = EAITargetType.AITARGET_NONE; m_bPathToFollowIsSpline = false;
        m_lastLiveTargetPos = new Vec3(0,0,0); m_timeSinceLastLiveTarget = -1.0f; m_refShape = null;
        m_looseAttentionId = 0; m_aimState = EAimState.AI_AIM_NONE; m_pActorTargetRequest = null;
        m_PathDestinationPos = new Vec3(0,0,0); m_eNavSOMethod = ENavSOMethod.nSOmNone;
        m_idLastUsedSmartObject = 0; m_actorTargetReqId = 1; m_spreadFireTime = 0.0f;
        m_lastExecutedGoalop = EGoalOperations.eGO_LAST; m_paused = 0; m_bEnableUpdateLookTarget = true;
        m_regCoverID = new CoverID(); m_adjustpath = 0; m_pipeExecuting = false;
        m_cutPathAtSmartObject = true; m_considerActorsAsPathObstacles = false;
        m_movementActorAdapter = new PipeUserMovementActorAdapter(this);
        CCCPOINT("CPipeUser_CPipeUser");
        _fastcast_CPipeUser = true;
        m_CurrentHideObject.m_HideSmartObject.pChainedUserEvent = null;
        m_CurrentHideObject.m_HideSmartObject.pChainedObjectEvent = null;
        m_callbacksForPipeuser.queuePathRequestFunction = (MNMPathRequest req) => RequestPathTo(req);
        m_callbacksForPipeuser.checkOnPathfinderStateFunction = () => GetPathfinderState();
        m_callbacksForPipeuser.getPathFollowerFunction = () => GetPathFollower();
        m_callbacksForPipeuser.getPathFunction = () => GetINavPath();
    }

    // dtor — PipeUser.cpp:110-134
    ~CPipeUser()
    {
        CCCPOINT("CPipeUser_Destructor");
        if (m_pCurrentGoalPipe != null) ResetCurrentPipe(true);
        m_coverUsageInfoState.Reset();
        while (m_mapGoalPipeListeners.Count > 0)
        {
            bool removed = false;
            foreach (var kv in m_mapGoalPipeListeners)
            {
                if (kv.Value.Count > 0) { UnRegisterGoalPipeListener(kv.Value[0].Item1, kv.Key); removed = true; break; }
                else { m_mapGoalPipeListeners.Remove(kv.Key); removed = true; break; }
            }
            if (!removed) break;
        }
        for (int i = 0; i < (int)ESpecialAIObjects.COUNT_AISPECIAL; ++i) m_refSpecialObjects[i].Release();
        m_pPathFollower = null; m_pActorTargetRequest = null;
        gAIEnv.pMovementSystem?.UnregisterEntity(GetEntityID());
    }

    public virtual IPipeUser CastToIPipeUser() { return null; }

    // Event — PipeUser.cpp:136-172
    public override void Event(ushort eType, SAIEVENT pAIEvent)
    {
        base.Event(eType, pAIEvent);
        switch (eType)
        {
        case (ushort)AIEVENT_CLEAR:
            CCCPOINT("CPuppet_Event_Clear"); ClearActiveGoals(); m_bLooseAttention = false;
            GetAISystem().FreeFormationPoint(WeakRefHelpers.GetWeakRef((CAIObject)this)); SetAttentionTarget(NILREF); m_bBlocked = false; break;
        case (ushort)AIEVENT_CLEARACTIVEGOALS: ClearActiveGoals(); m_bBlocked = false; break;
        case (ushort)AIEVENT_AGENTDIED:
            SetNavSOFailureStates();
            if (m_inCover || m_movingToCover) { SetCoverRegister(new CoverID()); m_coverUser.SetCoverID(new CoverID()); m_inCover = m_movingToCover = false; }
            m_Path.Clear("Agent Died"); break;
        case (ushort)AIEVENT_ADJUSTPATH: m_adjustpath = pAIEvent != null ? pAIEvent.nType : 0; break;
        }
    }

    // ParseParameters — PipeUser.cpp:174-187
    public override void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true)
    {
        base.ParseParameters(parameters, bParseMovementParams);
        if (m_pPathFollower != null)
        {
            PathFollowerParams pfp = new PathFollowerParams(); GetPathFollowerParams(pfp);
            m_pPathFollower.SetParams(pfp); /* IPathFollower.Reset via extension */
        }
    }

    // RecordEvent/RecordSnapshot — PipeUser.cpp:4227-4243
    public override void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData pEventData) { }
    public override void RecordSnapshot() { }

    // Reset — PipeUser.cpp:219-318
    public override void Reset(EObjectResetType type)
    {
        CCCPOINT("CPipeUser_Reset");
        m_notAllowedSubpipes.Clear(); SelectPipe(0, "_first_", NILREF, 0, true); m_Path.Clear("CPipeUser::Reset m_Path");
        base.Reset(type); ClearPath("Reset"); m_adjustingAim = false; m_bLastNearForbiddenEdge = false;
        m_coverUser.Reset(); m_movingToCover = false; m_movingInCover = false; m_inCover = false;
        SetAttentionTarget(NILREF); m_PathDestinationPos = new Vec3(0,0,0); m_refPathFindTarget.Reset(); m_IsSteering = false;
        m_fireMode = EFireMode.FIREMODE_OFF; SetFireTarget(NILREF); m_fireModeUpdated = false;
        m_outOfAmmoSent = false; m_lowAmmoSent = false; m_wasReloading = false; m_actorTargetReqId = 1;
        ResetLookAt(); ResetBodyTargetDir(); ResetDesiredBodyDirectionAtTarget(); ResetMovementContext();
        ResetCoverBlacklist(); m_regCoverID = new CoverID(); m_coverUsageInfoState.Reset();
        m_aimState = EAimState.AI_AIM_NONE; m_posLookAtSmartObject = new Vec3(0,0,0);
        m_eNavSOMethod = ENavSOMethod.nSOmNone; m_pendingNavSOStates.Clear(); m_currentNavSOStates.Clear();
        m_CurrentNodeNavType = IAISystem_ENavigationType.NAV_UNSET; m_idLastUsedSmartObject = 0; ClearInvalidatedSOLinks();
        m_CurrentHideObject.m_HideSmartObject.Clear(); m_bFirstUpdate = true;
        m_lastLiveTargetPos = new Vec3(0,0,0); m_timeSinceLastLiveTarget = -1.0f; m_spreadFireTime = 0.0f;
        m_recentUnreachableHideObjects.Clear(); m_bPathToFollowIsSpline = false;
        m_refShapeName = ""; m_refShape = null; m_paused = 0; m_bEnableUpdateLookTarget = true;
        m_pActorTargetRequest = null;
        for (int i = 0; i < (int)ESpecialAIObjects.COUNT_AISPECIAL; ++i) m_refSpecialObjects[i].Release();
        m_adjustpath = 0; m_pipeExecuting = false; m_pathAdjustmentObstacles.Reset();
        switch (type)
        {
        case EObjectResetType.AIOBJRESET_INIT: gAIEnv.pMovementSystem?.RegisterEntity(GetEntityID(), m_callbacksForPipeuser, m_movementActorAdapter); break;
        case EObjectResetType.AIOBJRESET_SHUTDOWN: gAIEnv.pMovementSystem?.UnregisterEntity(GetEntityID()); break;
        default: Debug.Assert(false); break;
        }
    }

    // SetName — PipeUser.cpp:320-340
    public override void SetName(string pName)
    {
        CCCPOINT("CPipeUser_SetName"); base.SetName(pName);
        CAIObject pRefPoint = m_refRefPoint.GetAIObject();
        if (pRefPoint != null) pRefPoint.SetName(pName + "_RefPoint");
        CAIObject pLookAtTarget = m_refLookAtTarget.GetAIObject();
        if (pLookAtTarget != null) pLookAtTarget.SetName(pName + "_LookAtTarget");
    }

    public void GetStateFromActiveGoals(ref SOBJECTSTATE state) { GetStateFromActiveGoalsImpl(state); }

    // GetGoalPipe — PipeUser.cpp:2012-2020
    public CGoalPipe GetGoalPipe(string name) { return gAIEnv.pPipeManager?.OpenGoalPipe(name); }

    // RemoveActiveGoal — PipeUser.cpp:2056-2075
    public void RemoveActiveGoal(int nOrder)
    {
        if (m_vActiveGoals.Count == 0) return;
        int size = m_vActiveGoals.Count;
        if (size == 1) { m_vActiveGoals[0].pGoalOp?.Reset(this); m_vActiveGoals.Clear(); return; }
        m_vActiveGoals[nOrder].pGoalOp?.Reset(this);
        if (nOrder != size - 1) m_vActiveGoals[nOrder] = m_vActiveGoals[size - 1];
        m_vActiveGoals.RemoveAt(m_vActiveGoals.Count - 1);
    }

    // SetAttentionTarget — PipeUser.cpp:1903-1928
    public override void SetAttentionTarget(CWeakRef<CAIObject> refTarget)
    {
        CCCPOINT("CPipeUser_SetAttentionTarget");
        CAIObject pTarget = refTarget.GetAIObject();
        if (pTarget == null) { m_AttTargetThreat = EAITargetThreat.AITHREAT_NONE; m_AttTargetExposureThreat = EAITargetThreat.AITHREAT_NONE; m_AttTargetType = EAITargetType.AITARGET_NONE; }
        else if (m_refAttentionTarget != refTarget && m_pCurrentGoalPipe != null)
        {
            CGoalPipe pLastPipe = m_pCurrentGoalPipe.GetLastSubpipe();
            if (pLastPipe != null && pTarget != null) pLastPipe.SetAttTargetPosAtStart(pTarget.GetPos());
        }
        m_refAttentionTarget = refTarget;
    }

    public virtual void ClearPotentialTargets() { }
    public void SetLastOpResult(CWeakRef<CAIObject> refObject) { m_refLastOpResult = refObject; }
    public virtual bool NavigateAroundObjects(Vec3 targetPos, bool fullUpdate) { return false; }

    // CancelRequestedPath — PipeUser.cpp:4258-4266
    public override void CancelRequestedPath(bool actorRemoved)
    {
        if (m_queuedPathId != 0) { gAIEnv.pMNMPathfinder?.CancelPathRequest(new QueuedPathID { id = m_queuedPathId }); m_queuedPathId = 0; }
    }

    // HandlePathDecision — PipeUser.cpp:4270-4386
    protected override void HandlePathDecision(MNMPathRequestResult result)
    {
        CNavPath pNavPath = new CNavPath();
        if (result.HasPathBeenFoundEx())
        {
            result.GetNavPath()?.CopyTo(pNavPath);
            m_nPathDecision = (int)EPathfinderResult.PATHFINDER_PATHFOUND;
            if (m_eNavSOMethod != ENavSOMethod.nSOmNone && m_State.curActorTargetPhase == EActorTargetPhase.eATP_Waiting) SetNavSOFailureStates();
            if (GetActiveActorTargetRequest() != null && m_State.curActorTargetPhase == EActorTargetPhase.eATP_Waiting) m_State.actorTargetReq.Reset();
            int oldVersion = m_Path.GetVersion(); m_Path = pNavPath; m_Path.SetVersion(oldVersion + 1);
            if (!m_Path.Empty()) m_PathDestinationPos = m_Path.GetLastPathPos();
            float trimLength = m_Path.GetParams()?.endDistance ?? 0;
            if (fabsf(trimLength) > 0.0001f) m_Path.TrimPath(trimLength, IsUsing3DNavigation());
            m_OrigPath = m_Path;
            if (m_cutPathAtSmartObject) m_Path.PrepareNavigationalSmartObjectsForMNM(this);
            m_State.fDistanceToPathEnd = m_Path.GetPathLength(!IsUsing3DNavigation());
        }
        else { ClearPath("CPipeUser::HandlePathDecision m_Path"); m_nPathDecision = (int)EPathfinderResult.PATHFINDER_NOPATH; }
        if (CastToCPuppet() == null) AdjustPath();
    }

    // AdjustPath — PipeUser.cpp:4391-4413
    public void AdjustPath()
    {
        AgentPathfindingProperties pathProps = m_movementAbility.pathfindingProperties;
        if (pathProps.avoidObstacles) { if (!AdjustPathAroundObstacles()) { ClearPath("CPuppet::AdjustPath m_Path"); m_nPathDecision = (int)EPathfinderResult.PATHFINDER_NOPATH; return; } }
        if (m_adjustpath > 0)
        {
            int nBuildingID = 0;
            IAISystem_ENavigationType currentNavType = gAIEnv.pNavigation != null ? gAIEnv.pNavigation.CheckNavigationType(GetPos(), ref nBuildingID, pathProps.navCapMask) : IAISystem_ENavigationType.NAV_UNSET;
            ConvertPathToSpline(currentNavType);
        }
    }

    // OnMNMPathResult — PipeUser.cpp:4247-4254
    public void OnMNMPathResult(QueuedPathID requestId, MNMPathRequestResult result) { Debug.Assert(requestId.id == m_queuedPathId); m_queuedPathId = 0; HandlePathDecision(result); }

    // AdjustPathAroundObstacles — PipeUser.cpp:4418-4429
    public bool AdjustPathAroundObstacles()
    {
        if (gAIEnv.CVars.AdjustPathsAroundDynamicObstacles != 0) { CalculatePathObstacles(); return m_Path.AdjustPathAroundObstacles(m_pathAdjustmentObstacles, m_movementAbility.pathfindingProperties.navCapMask); }
        return true;
    }

    // GetPathFollower (non-const) — PipeUser.cpp:4434-4451
    public new IPathFollower GetPathFollower()
    {
        if (m_pPathFollower == null && m_movementAbility.usePredictiveFollowing)
        {
            PathFollowerParams pfp = new PathFollowerParams(); GetPathFollowerParams(pfp);
            m_pPathFollower = CreatePathFollower(pfp);
            m_pPathFollower?.AttachToPath(m_Path);
        }
        return m_pPathFollower;
    }

    // GetPathFollowerParams — PipeUser.cpp:4464-4477
    public virtual void GetPathFollowerParams(PathFollowerParams outParams)
    {
        outParams.navCapMask = m_movementAbility.pathfindingProperties.navCapMask;
        outParams.passRadius = m_movementAbility.pathfindingProperties.radius;
        outParams.pathRadius = m_movementAbility.pathRadius; outParams.stopAtEnd = true;
        outParams.use2D = !m_movementAbility.b3DMove; outParams.endAccuracy = 0.2f;
        outParams.pathLookAheadDist = m_movementAbility.pathLookAhead; outParams.maxSpeed = 0.0f;
    }

    public uint GetPendingSmartObjectID() { return m_pendingSmartObjectId; }

    // RequestPathTo — PipeUser.cpp:1933-1973
    public virtual void RequestPathTo(Vec3 pos, Vec3 dir, bool allowDangerousDestination, int forceTargetBuildingId = -1, float endTol = float.MaxValue, float endDistance = 0.0f, CAIObject pTargetObject = null, bool cutPathAtSmartObject = true, MNMDangersFlags dangersFlags = MNMDangersFlags.eMNMDangers_None, bool considerActorsAsPathObstacles = false)
    {
        CCCPOINT("CPipeUser_RequestPathTo");
        m_cutPathAtSmartObject = cutPathAtSmartObject; m_considerActorsAsPathObstacles = considerActorsAsPathObstacles;
        m_OrigPath.Clear("CPipeUser::RequestPathTo m_OrigPath"); m_nPathDecision = (int)EPathfinderResult.PATHFINDER_STILLFINDING;
        m_refPathFindTarget = WeakRefHelpers.GetWeakRef(pTargetObject);
        Vec3 myPos = GetPhysicsPos() + new Vec3(0, 0, 0.05f);
        Vec3 endDir = m_bPathfinderConsidersPathTargetDirection ? dir : new Vec3(0, 0, 0);
        CancelRequestedPath(false); Debug.Assert(m_queuedPathId == 0);
        MNMPathRequest request = new MNMPathRequest(myPos, pos, endDir, forceTargetBuildingId, endTol, endDistance, allowDangerousDestination, (QueuedPathID rid, MNMPathRequestResult res) => OnMNMPathResult(rid, res), GetNavigationTypeID(), dangersFlags);
        m_queuedPathId = gAIEnv.pMNMPathfinder?.RequestPathTo(this, request) ?? 0;
        if (m_queuedPathId == 0) { MNMPathRequestResult pr = new MNMPathRequestResult(); HandlePathDecision(pr); }
    }

    // RequestPathTo (MNMPathRequest) — PipeUser.cpp:1975-1999
    public void RequestPathTo(MNMPathRequest request)
    {
        CCCPOINT("CPipeUser_RequestPathTo");
        m_cutPathAtSmartObject = false; m_considerActorsAsPathObstacles = false;
        m_OrigPath.Clear("CPipeUser::RequestPathTo m_OrigPath"); m_nPathDecision = (int)EPathfinderResult.PATHFINDER_STILLFINDING;
        m_refPathFindTarget.Reset(); CancelRequestedPath(false); Debug.Assert(m_queuedPathId == 0);
        request.resultCallback = (QueuedPathID rid, MNMPathRequestResult res) => OnMNMPathResult(rid, res);
        m_queuedPathId = gAIEnv.pMNMPathfinder?.RequestPathTo(this, request) ?? 0;
        if (m_queuedPathId == 0) { MNMPathRequestResult pr = new MNMPathRequestResult(); HandlePathDecision(pr); }
    }

    // RequestPathInDirection — PipeUser.cpp:2004-2007
    public virtual void RequestPathInDirection(Vec3 pos, float distance, CWeakRef<CAIObject> refTargetObject, float endDistance = 0.0f)
    { AILog.AIWarning("CPipeUser::RequestPathInDirection is currently not supported for the MNM Navigation System."); }

    // SetPathToFollow — PipeUser.cpp:3590-3596
    public override void SetPathToFollow(string pathName)
    { if (gAIEnv.CVars.DebugPathFinding != 0) AILog.AILogAlways("CPipeUser::SetPathToFollow {0} path = {1}", GetName(), pathName); m_pathToFollowName = pathName; m_bPathToFollowIsSpline = false; }
    public override void SetPathAttributeToFollow(bool bSpline) { m_bPathToFollowIsSpline = bSpline; }
    public string GetPathToFollow() { return m_pathToFollowName; }

    // GetPathEntryPoint — PipeUser.cpp:3606-3638
    public virtual bool GetPathEntryPoint(out Vec3 entryPos, bool reverse, bool startNearest)
    {
        entryPos = new Vec3(0,0,0);
        if (m_pathToFollowName.Length == 0) return false;
        SShape path1 = new SShape();
        if (gAIEnv.pNavigation == null || !gAIEnv.pNavigation.GetDesignerPath(m_pathToFollowName, path1)) return false;
        if (path1.shape.Count == 0) return false;
        if (startNearest) { float d; path1.NearestPointOnPath(GetPhysicsPos(), false, out d, out entryPos); }
        else if (reverse) entryPos = path1.shape[path1.shape.Count - 1];
        else entryPos = path1.shape[0];
        return true;
    }

    // UsePathToFollow — PipeUser.cpp:3643-3744 (simplified)
    public virtual bool UsePathToFollow(bool reverse, bool startNearest, bool loop)
    {
        if (m_pathToFollowName.Length == 0) return false;
        ClearPath("CPipeUser::UsePathToFollow m_Path");
        SShape path1 = new SShape();
        if (gAIEnv.pNavigation == null || !gAIEnv.pNavigation.GetDesignerPath(m_pathToFollowName, path1)) return false;
        if (path1.shape.Count == 0) return false;
        IAISystem_ENavigationType navType = path1.navType;
        // simplified — push all points in order
        if (reverse) { for (int i = path1.shape.Count - 1; i >= 0; --i) m_Path.PushBack(new PathPointDescriptor(navType, path1.shape[i])); }
        else { for (int i = 0; i < path1.shape.Count; ++i) m_Path.PushBack(new PathPointDescriptor(navType, path1.shape[i])); }
        if (m_bPathToFollowIsSpline) { ConvertPathToSpline(navType); return true; }
        SNavPathParams np = new SNavPathParams(); np.precalculatedPath = true; m_Path.SetParams(np);
        m_nPathDecision = (int)EPathfinderResult.PATHFINDER_PATHFOUND; m_Path.PushFront(new PathPointDescriptor(navType, GetPhysicsPos())); m_OrigPath = m_Path;
        return true;
    }

    // SetPointListToFollow — PipeUser.cpp:3874-3966
    public virtual void SetPointListToFollow(List<Vec3> pointList, IAISystem_ENavigationType navType, bool bSpline)
    {
        ClearPath("CPipeUser::SetPointListToFollow m_Path");
        int howmanyPoints = pointList.Count;
        if (howmanyPoints < 2) return;
        if (howmanyPoints == 2 || !bSpline) { foreach (var pos in pointList) m_Path.PushBack(new PathPointDescriptor(navType, pos)); return; }
        int itX = 0, itY = 1, itZ = 2;
        Vec3 nextStart = (pointList[itX] - pointList[itY]) * 2.0f + pointList[itY];
        List<Vec3> pll = new List<Vec3>();
        for (int i = 0; i < howmanyPoints - 3; ++itX, ++itY, ++itZ, i++)
        { pll.Clear(); nextStart = SetPointListToFollowSub(nextStart, pointList[itY], pointList[itZ], pll, 1.0f); foreach (var pos in pll) m_Path.PushBack(new PathPointDescriptor(navType, pos)); }
        Vec3 mid = pointList[itY]; Vec3 end = (pointList[itZ] - mid) * 2.0f + mid;
        pll.Clear(); nextStart = SetPointListToFollowSub(nextStart, pointList[itY], end, pll, 1.0f);
        if (pll.Count == 0) m_Path.PushBack(new PathPointDescriptor(navType, nextStart));
        else { foreach (var pos in pll) m_Path.PushBack(new PathPointDescriptor(navType, pos)); }
        { Vec3 pos = pointList[itZ]; if ((pos - nextStart).GetLength() > 1.0f) m_Path.PushBack(new PathPointDescriptor(navType, pos)); }
    }

    // UsePointListToFollow — PipeUser.cpp:3971-3984
    public virtual bool UsePointListToFollow() { m_OrigPath.Clear("CPipeUser::UsePointListToFollow m_OrigPath"); SNavPathParams np = new SNavPathParams(); np.precalculatedPath = true; m_Path.SetParams(np); m_nPathDecision = (int)EPathfinderResult.PATHFINDER_PATHFOUND; m_OrigPath = m_Path; return true; }

    public virtual void ClearDevalued() { } public virtual void Forget(CAIObject pDummyObject) { } public virtual void Navigate3d(CAIObject pTarget) { } public virtual void MakeIgnorant(bool bIgnorant) { }
    public CGoalPipe GetCurrentGoalPipe() { return m_pCurrentGoalPipe; }
    public CGoalPipe GetActiveGoalPipe() { return m_pCurrentGoalPipe?.GetLastSubpipe(); }
    public string GetActiveGoalPipeName() { CGoalPipe pipe = GetActiveGoalPipe(); return pipe != null ? pipe.GetName() : "No Active GoalPipe"; }

    // ResetCurrentPipe — PipeUser.cpp:2527-2572
    public void ResetCurrentPipe(bool resetAlways)
    {
        Debug.Assert(!m_pipeExecuting);
        CGoalPipe pPipe = m_pCurrentGoalPipe;
        while (pPipe != null) { NotifyListeners(pPipe, EGoalPipeEvent.ePN_Deselected); if (resetAlways || pPipe.GetSubpipe() == null || !pPipe.GetSubpipe().IsHighPriority()) pPipe = pPipe.GetSubpipe(); else break; }
        if (pPipe == null) ClearActiveGoals(); else pPipe.SetSubpipe(null);
        if (m_pCurrentGoalPipe != null) { m_pCurrentGoalPipe.ResetGoalops(this); m_pCurrentGoalPipe = null; }
        if (pPipe == null) { CPuppet pp = CastToCPuppet(); if (pp != null) pp.m_bCanReceiveSignals = true; m_bKeepMoving = false; ResetLookAt(); ResetBodyTargetDir(); ResetDesiredBodyDirectionAtTarget(); ResetMovementContext(); }
    }

    // GetGoalPipeId — PipeUser.cpp:2574-2582
    public int GetGoalPipeId() { CGoalPipe pPipe = m_pCurrentGoalPipe; if (pPipe == null) return -1; while (pPipe.GetSubpipe() != null) pPipe = pPipe.GetSubpipe(); return pPipe.GetEventId(); }

    // ResetLookAt — PipeUser.cpp:2584-2591
    public override void ResetLookAt() { m_bPriorityLookAtRequested = false; if (m_bLooseAttention) SetLooseAttentionTarget(NILREF); }

    // SetLookAtPointPos — PipeUser.cpp:2593-2618
    public override bool SetLookAtPointPos(Vec3 point, bool priority = false)
    {
        if (!priority && m_bPriorityLookAtRequested) return false;
        m_bPriorityLookAtRequested = priority; CCCPOINT("CPipeUser_SetLookAtPoint");
        CreateLookAtTarget(); m_refLooseAttentionTarget = m_refLookAtTarget;
        m_refLookAtTarget.GetAIObject()?.SetPos(point); m_bLooseAttention = true;
        Vec3 desired = point - GetPos(); desired.z = 0; desired.NormalizeSafe();
        Vec3 current = GetBodyInfo().vEyeDirAnim; current.z = 0; current.NormalizeSafe();
        return 0.98f <= current.Dot(desired);
    }

    // SetLookAtDir — PipeUser.cpp:2620-2657
    public override bool SetLookAtDir(Vec3 dir, bool priority = false)
    {
        if (!priority && m_bPriorityLookAtRequested) return false;
        m_bPriorityLookAtRequested = priority; CCCPOINT("CPipeUser_SetLookAtDir");
        Vec3 vDir = dir;
        if (vDir.NormalizeSafe() > 0f)
        {
            CreateLookAtTarget(); m_refLooseAttentionTarget = m_refLookAtTarget;
            m_refLookAtTarget.GetAIObject()?.SetPos(GetPos() + vDir * 100.0f); m_bLooseAttention = true;
            Vec3 desired = vDir; desired.z = 0; desired.NormalizeSafe();
            Vec3 current = GetBodyInfo().vEyeDirAnim; current.z = 0; current.NormalizeSafe();
            if (0.98f <= current.Dot(desired)) { SetLooseAttentionTarget(NILREF); return true; } else return false;
        }
        else return true;
    }

    public void CreateLookAtTarget() { if (m_refLookAtTarget.IsSet() && m_refLookAtTarget.GetAIObject() != null) return; gAIEnv.pAIObjectManager?.CreateDummyObject(m_refLookAtTarget, GetName() + "_LookAtTarget", ESubType.STP_LOOKAT); m_refLookAtTarget.GetAIObject()?.SetPos(GetPos() + GetMoveDir()); }
    public void CreateRefPoint() { if (m_refRefPoint.IsSet() && m_refRefPoint.GetAIObject() != null) return; gAIEnv.pAIObjectManager?.CreateDummyObject(m_refRefPoint, GetName() + "_RefPoint", ESubType.STP_REFPOINT); m_refRefPoint.GetAIObject()?.SetPos(GetPos(), GetMoveDir()); }

    public override void ResetBodyTargetDir() { m_vBodyTargetDir = new Vec3(0,0,0); }
    public override void SetBodyTargetDir(Vec3 dir) { m_vBodyTargetDir = dir; }
    public override Vec3 GetBodyTargetDir() { return m_vBodyTargetDir; }
    public void ResetDesiredBodyDirectionAtTarget() { m_vDesiredBodyDirectionAtTarget = new Vec3(0,0,0); }
    public void SetDesiredBodyDirectionAtTarget(Vec3 dir) { m_vDesiredBodyDirectionAtTarget = dir; }
    public Vec3 GetDesiredBodyDirectionAtTarget() { return m_vDesiredBodyDirectionAtTarget; }
    public void ResetMovementContext() { m_movementContext = 0; }
    public void ClearMovementContext(uint mc) { m_movementContext &= ~mc; }
    public void SetMovementContext(uint mc) { m_movementContext |= mc; }
    public uint GetMovementContext() { return m_movementContext; }
    public void SetAllowedStrafeDistances(float start, float end, bool whileMoving) { }
    public void SetExtraPriority(float priority) { m_AttTargetPersistenceTimeout = priority; }
    public float GetExtraPriority() { return m_AttTargetPersistenceTimeout; }

    // SetLooseAttentionTarget — PipeUser.cpp:4108-4126
    public int SetLooseAttentionTarget(CWeakRef<CAIObject> refObject, int id = -1)
    {
        CAIObject pObject = refObject.GetAIObject();
        if (pObject != null || id == m_looseAttentionId || id == -1) { m_bLooseAttention = pObject != null; if (m_bLooseAttention) { m_refLooseAttentionTarget = refObject; ++m_looseAttentionId; } else m_refLooseAttentionTarget.Reset(); }
        return m_looseAttentionId;
    }
    public void SetLookStyle(ELookStyle e) { m_State.eLookStyle = e; }
    public virtual void AllowLowerBodyToTurn(bool b) { m_State.bAllowLowerBodyToTurn = b; }
    public virtual bool IsAllowingBodyTurn() { return m_State.bAllowLowerBodyToTurn; }
    public ELookStyle GetLookStyle() { return m_State.eLookStyle; }
    public int SetLooseAttentionTarget(Vec3 pos) { SetLookAtPointPos(pos); return ++m_looseAttentionId; }
    public Vec3 GetLooseAttentionPos() { CCCPOINT("CPipeUser_GetLooseAttentionPos"); if (m_bLooseAttention) { CAIObject la = m_refLooseAttentionTarget.GetAIObject(); if (la != null) return la.GetPos(); } return new Vec3(0,0,0); }
    public int GetLooseAttentionId() { return m_looseAttentionId; }
    public virtual LookTargetPtr CreateLookTarget() { LookTargetPtr lt = new LookTargetPtr(); m_lookTargets.Add(lt); return lt; }
    public void RegisterAttack(string name) { } public void RegisterRetreat(string name) { } public void RegisterWander(string name) { } public void RegisterIdle(string name) { }

    // SelectPipe — PipeUser.cpp:2104-2237
    public bool SelectPipe(int id, string name, CWeakRef<CAIObject> refArgument, int goalPipeId = 0, bool resetAlways = false, GoalParams node = null)
    {
        if (m_pipeExecuting) { m_delayedPipeSelection = new DelayedPipeSelection(id, name, refArgument, goalPipeId, resetAlways); return true; }
        GetEntity()?.let_AbortAIAction(); refArgument.ValidateOrReset(); m_lastExecutedGoalop = EGoalOperations.eGO_LAST;
        if (m_pCurrentGoalPipe != null && !resetAlways && m_pCurrentGoalPipe.GetNameAsString() == name)
        { CCCPOINT("CPipeUser_SelectPipe_A"); NotifyListeners(m_pCurrentGoalPipe.GetEventId(), EGoalPipeEvent.ePN_Deselected); m_pCurrentGoalPipe.SetEventId(goalPipeId); if (refArgument.IsSet()) m_pCurrentGoalPipe.SetRefArgument(refArgument); m_pCurrentGoalPipe.SetLoop((id & (int)EGoalPipeFlags.AIGOALPIPE_RUN_ONCE) == 0); return true; }
        CGoalPipe pHPOwner = null, pHP = m_pCurrentGoalPipe;
        while (pHP != null && !pHP.IsHighPriority()) { pHPOwner = pHP; pHP = pHP.GetSubpipe(); }
        if (pHP == null && refArgument.IsSet()) SetLastOpResult(refArgument);
        if (pHP == null || resetAlways) { CCCPOINT("CPipeUser_SelectPipe_B"); SetNavSOFailureStates(); }
        CGoalPipe pPipe = gAIEnv.pPipeManager?.IsGoalPipe(name);
        if (pPipe != null) { IAIObject att = GetAttentionTarget(); pPipe.SetAttTargetPosAtStart(att != null ? att.GetPos() : new Vec3(0,0,0)); pPipe.SetRefArgument(refArgument); }
        else { pPipe = gAIEnv.pPipeManager?.IsGoalPipe("_first_"); pPipe?.GetRefArgument()?.Reset(); }
        if (pHP == null || resetAlways) ResetCurrentPipe(resetAlways);
        CGoalPipe prevGP = m_pCurrentGoalPipe; m_pCurrentGoalPipe = pPipe; m_pCurrentGoalPipe?.Reset(); m_pCurrentGoalPipe?.SetEventId(goalPipeId);
        if (node != null) m_pCurrentGoalPipe?.ParseParams(node);
        if (pHP != null && !resetAlways) { if (pHPOwner != null) pHPOwner.SetSubpipe(null); m_pCurrentGoalPipe?.SetSubpipe(pHP); }
        m_pCurrentGoalPipe?.SetLoop((id & (int)EGoalPipeFlags.AIGOALPIPE_RUN_ONCE) == 0);
        ClearInvalidatedSOLinks(); CCCPOINT("CPipeUser_SelectPipe_End"); return true;
    }

    // InsertSubPipe — PipeUser.cpp:2371-2512
    public IGoalPipe InsertSubPipe(int mode, string name, CWeakRef<CAIObject> refArgument, int goalPipeId = 0, GoalParams node = null)
    {
        if (m_pCurrentGoalPipe == null) return null;
        if (goalPipeId != 0 && m_notAllowedSubpipes.Contains(goalPipeId)) { m_notAllowedSubpipes.Remove(goalPipeId); return null; }
        bool bExclusive = (mode & (int)EGoalPipeFlags.AIGOALPIPE_NOTDUPLICATE) != 0;
        bool bHighP = (mode & (int)EGoalPipeFlags.AIGOALPIPE_HIGHPRIORITY) != 0;
        bool bSameP = (mode & (int)EGoalPipeFlags.AIGOALPIPE_SAMEPRIORITY) != 0;
        bool bKeepOnTop = (mode & (int)EGoalPipeFlags.AIGOALPIPE_KEEP_ON_TOP) != 0;
        CGoalPipe pPipe = gAIEnv.pPipeManager?.IsGoalPipe(name); if (pPipe == null) return null;
        if (bHighP) pPipe.HighPriority(); if (bKeepOnTop) pPipe.m_bKeepOnTop = true;
        CGoalPipe pExec = m_pCurrentGoalPipe;
        while (pExec.IsInSubpipe()) { if (bExclusive && pExec.GetNameAsString() == name) return null; if (pExec.GetSubpipe().m_bKeepOnTop) break; if (!bSameP && !bHighP && pExec.GetSubpipe().IsHighPriority()) break; pExec = pExec.GetSubpipe(); }
        if (bExclusive && pExec.GetNameAsString() == name) return null;
        if (pExec.GetSubpipe() == null) { if (m_vActiveGoals.Count > 0) { ClearActiveGoals(); pExec.ReExecuteGroup(); } NotifyListeners(pExec, EGoalPipeEvent.ePN_Suspended); if (refArgument.IsSet()) SetLastOpResult(refArgument); m_bBlocked = false; }
        pExec.SetSubpipe(pPipe);
        if (GetAttentionTarget() != null) pPipe.SetAttTargetPosAtStart(GetAttentionTarget().GetPos()); else pPipe.SetAttTargetPosAtStart(new Vec3(0,0,0));
        pPipe.SetRefArgument(refArgument); pPipe.SetEventId(goalPipeId);
        if (node != null) pPipe.ParseParams(node);
        NotifyListeners(pPipe, EGoalPipeEvent.ePN_Inserted); return pPipe;
    }

    // CancelSubPipe — PipeUser.cpp:2327-2369
    public bool CancelSubPipe(int goalPipeId)
    {
        if (m_pCurrentGoalPipe == null) { if (goalPipeId != 0) m_notAllowedSubpipes.Add(goalPipeId); return false; }
        CGoalPipe pCurrent = m_pCurrentGoalPipe; while (pCurrent.GetSubpipe() != null) pCurrent = pCurrent.GetSubpipe();
        bool bClearNeeded = goalPipeId == 0 || pCurrent.GetEventId() == goalPipeId;
        CPuppet pp = CastToCPuppet(); if (pp != null) pp.m_bCanReceiveSignals = true;
        if (m_pCurrentGoalPipe.RemoveSubpipe(this, goalPipeId, true, true))
        { NotifyListeners(goalPipeId, EGoalPipeEvent.ePN_Deselected); if (bClearNeeded) { ClearActiveGoals(); pCurrent = m_pCurrentGoalPipe; while (pCurrent.GetSubpipe() != null) pCurrent = pCurrent.GetSubpipe(); SetLastOpResult(pCurrent.GetRefArgument()); NotifyListeners(pCurrent, EGoalPipeEvent.ePN_Resumed); } return true; }
        else { if (goalPipeId != 0) m_notAllowedSubpipes.Add(goalPipeId); return false; }
    }

    // RemoveSubPipe — PipeUser.cpp:2263-2323
    public bool RemoveSubPipe(int goalPipeId, bool keepInserted = false)
    {
        if (goalPipeId == 0) return false;
        if (m_pCurrentGoalPipe == null) { m_notAllowedSubpipes.Add(goalPipeId); return false; }
        CGoalPipe pPrevPipe = m_pCurrentGoalPipe; while (pPrevPipe.GetSubpipe() != null) pPrevPipe = pPrevPipe.GetSubpipe();
        if (m_pCurrentGoalPipe.RemoveSubpipe(this, goalPipeId, keepInserted, true))
        { NotifyListeners(goalPipeId, EGoalPipeEvent.ePN_Removed); CGoalPipe pCurrent = m_pCurrentGoalPipe; while (pCurrent.GetSubpipe() != null) pCurrent = pCurrent.GetSubpipe(); if (pCurrent != pPrevPipe) { ClearActiveGoals(); SetLastOpResult(pCurrent.GetRefArgument()); NotifyListeners(pCurrent, EGoalPipeEvent.ePN_Resumed); } return true; }
        else { m_notAllowedSubpipes.Add(goalPipeId); return false; }
    }

    public bool IsUsingPipe(string name) { CGoalPipe p = m_pCurrentGoalPipe; if (p == null) return false; while (p.IsInSubpipe()) { if (p.GetNameAsString() == name) return true; p = p.GetSubpipe(); } return p.GetNameAsString() == name; }
    public bool IsUsingPipe(int goalPipeId) { CGoalPipe p = m_pCurrentGoalPipe; while (p != null) { if (p.GetEventId() == goalPipeId) return true; p = p.GetSubpipe(); } return false; }
    public bool AbortActionPipe(int goalPipeId) { CGoalPipe p = m_pCurrentGoalPipe; while (p != null && p.GetEventId() != goalPipeId) p = p.GetSubpipe(); if (p == null || p.IsHighPriority()) return false; p.HighPriority(); while (p != null && p.GetSubpipe() != null) { if (p.GetSubpipe().GetNameAsString() == "_action_") break; else if (p.GetSubpipe().GetEventId() != 0) CancelSubPipe(p.GetSubpipe().GetEventId()); p = p.GetSubpipe(); } return true; }

    public bool IsUsing3DNavigation() { return m_movementAbility.b3DMove; /* simplified */ }
    public virtual void Pause(bool pause) { if (pause) ++m_paused; else if (m_paused > 0) --m_paused; }
    public virtual bool IsPaused() { return m_paused > 0; }
    public bool AllowedToFire() { return m_fireMode != EFireMode.FIREMODE_OFF && m_fireMode != EFireMode.FIREMODE_AIM && m_fireMode != EFireMode.FIREMODE_AIM_SWEEP; }
    public virtual void SetFireMode(EFireMode mode) { m_outOfAmmoSent = false; m_lowAmmoSent = false; m_wasReloading = false; if (m_fireMode == mode) return; m_fireMode = mode; m_fireModeUpdated = true; m_spreadFireTime = CryRandom.cry_random(0.0f, 10.0f); }
    public virtual void SetFireTarget(CWeakRef<CAIObject> refTargetObject) { m_refFireTarget = refTargetObject; }
    public virtual EFireMode GetFireMode() { return m_fireMode; }
    public virtual CAIObject GetRefPoint() { CreateRefPoint(); return m_refRefPoint.GetAIObject(); }

    // SetRefPointPos — PipeUser.cpp:4006-4037
    public virtual void SetRefPointPos(Vec3 pos) { CCCPOINT("CPipeUser_SetRefPointPos"); CreateRefPoint(); CAIObject pRefPoint = m_refRefPoint.GetAIObject(); bool bNotify = m_pCurrentGoalPipe != null && pRefPoint != null && !pRefPoint.GetPos().IsEquivalent(pos, 0.001f); pRefPoint?.SetPos(pos, GetMoveDir()); pRefPoint?.SetEntityID(0); if (bNotify) NotifyListeners(m_pCurrentGoalPipe?.GetLastSubpipe(), EGoalPipeEvent.ePN_RefPointMoved); }
    public virtual void SetRefPointPos(Vec3 pos, Vec3 dir) { CCCPOINT("CPipeUser_SetRefPointPos_Dir"); CreateRefPoint(); CAIObject pRefPoint = m_refRefPoint.GetAIObject(); bool bNotify = m_pCurrentGoalPipe != null && pRefPoint != null && !pRefPoint.GetPos().IsEquivalent(pos, 0.001f); pRefPoint?.SetPos(pos, dir); if (bNotify) NotifyListeners(m_pCurrentGoalPipe?.GetLastSubpipe(), EGoalPipeEvent.ePN_RefPointMoved); }
    public virtual void SetRefShapeName(string shapeName) { m_refShapeName = shapeName; m_refShape = GetAISystem()?.GetGenericShapeOfName(m_refShapeName); if (m_refShape == null) m_refShapeName = ""; }
    public virtual string GetRefShapeName() { return m_refShapeName; }
    public virtual Vec3 GetProbableTargetPosition() { if (m_timeSinceLastLiveTarget >= 0.0f && m_timeSinceLastLiveTarget < 2.0f) return m_lastLiveTargetPos; CAIObject att = m_refAttentionTarget.GetAIObject(); if (att != null) return att.GetPos(); IAIObject beacon = GetAISystem()?.GetBeacon(GetGroupId()); return beacon != null ? beacon.GetPos() : GetPos(); }
    public SShape GetRefShape() { return m_refShape; }
    public void SetCoverRegister(CoverID coverID) { if (m_regCoverID.IsValid()) gAIEnv.pCoverSystem?.SetCoverOccupied(m_regCoverID, false, GetAIObjectID()); m_regCoverID = coverID; if (m_regCoverID.IsValid()) gAIEnv.pCoverSystem?.SetCoverOccupied(m_regCoverID, true, GetAIObjectID()); }
    public CoverID GetCoverRegister() { return m_regCoverID; }
    public virtual void SetActorTargetRequest(SAIActorTargetRequest req) { CAIObject pTarget = GetOrCreateSpecialAIObject(ESpecialAIObjects.AISPECIAL_ANIM_TARGET); if (m_pActorTargetRequest == null) m_pActorTargetRequest = new SAIActorTargetRequest(); m_pActorTargetRequest = req; pTarget?.SetPos(req.approachLocation, req.approachDirection); }
    public virtual void ClearActorTargetRequest() { m_pActorTargetRequest = null; }
    public SAIActorTargetRequest GetActiveActorTargetRequest() { CAIObject pfTarget = m_refPathFindTarget.GetAIObject(); if (pfTarget != null && pfTarget.GetSubType() == ESubType.STP_ANIM_TARGET && m_pActorTargetRequest != null) return m_pActorTargetRequest; return null; }
    public virtual void IgnoreCurrentHideObject(float timeOut) { Vec3 pos = m_CurrentHideObject.GetObjectPos(); foreach (var p in m_recentUnreachableHideObjects) if (Vec3.Distance_Point_PointSq(p.Item2, pos) < sqr(0.1f)) return; m_recentUnreachableHideObjects.AddLast((timeOut, pos)); while (m_recentUnreachableHideObjects.Count > 5) m_recentUnreachableHideObjects.RemoveFirst(); }
    public virtual uint GetLastUsedSmartObjectId() { return m_idLastUsedSmartObject; }
    public virtual bool IsUsingNavSO() { return m_eNavSOMethod != ENavSOMethod.nSOmNone; }
    public virtual void ClearPath(string dbgString) { SetNavSOFailureStates(); m_Path.Clear(dbgString); m_OrigPath.Clear(dbgString); m_refPathFindTarget.Reset(); }
    public override ETriState CanTargetPointBeReached(CTargetPointRequest request) { return m_Path.CanTargetPointBeReached(request, this, true); }
    public override bool UseTargetPointRequest(CTargetPointRequest request) { return m_Path.UseTargetPointRequest(request, this, true); }
    // UpdateLookTarget — PipeUser.cpp:4682-4882 — full literal translation
    public virtual void UpdateLookTarget(CAIObject pTarget)
    {
        CCCPOINT("CPipeUser_UpdateLookTarget");

        // Don't look at targets that aren't at least suspicious
        if (pTarget != null && (pTarget == m_refAttentionTarget.GetAIObject()) && (GetAttentionTargetThreat() <= EAITargetThreat.AITHREAT_SUSPECT))
        {
            pTarget = null;
        }

        // If not moving, allow to look at target.
        bool bLookAtTarget = (m_State.fDesiredSpeed < 0.01f) || m_Path.Empty();

        if (m_bLooseAttention)
        {
            CAIObject pLooseAttentionTarget = m_refLooseAttentionTarget.GetAIObject();
            if (pLooseAttentionTarget != null)
            {
                pTarget = pLooseAttentionTarget;
            }
        }

        Vec3 vLookTargetPos = new Vec3(0,0,0);

        if (m_fireMode == EFireMode.FIREMODE_MELEE || m_fireMode == EFireMode.FIREMODE_MELEE_FORCED)
        {
            if (pTarget != null)
            {
                vLookTargetPos = pTarget.GetPos();
                vLookTargetPos.z = GetPos().z;
                bLookAtTarget = true;
            }
        }

        bool use3DNav = IsUsing3DNavigation();
        bool isMoving = (m_State.fDesiredSpeed > 0.0f) && (m_State.curActorTargetPhase == EActorTargetPhase.eATP_None) && !m_State.vMoveDir.IsZero();

        float distToTarget = float.MaxValue;
        if (pTarget != null)
        {
            Vec3 dirToTarget = pTarget.GetPos() - GetPos();
            distToTarget = dirToTarget.GetLength();
            if (distToTarget > 0.0001f)
            {
                dirToTarget = dirToTarget / distToTarget;
            }

            // Allow to look at the target when it is almost at the movement direction or very close.
            if (isMoving)
            {
                Vec3 vMoveDir = m_State.vMoveDir;
                if (!use3DNav)
                {
                    vMoveDir.z = 0.0f;
                }
                vMoveDir.NormalizeSafe();
                if (distToTarget < 2.5f || (vMoveDir.Dot(dirToTarget) > cosf(DEG2RAD(60))))
                {
                    bLookAtTarget = true;
                }
            }
        }

        if (bLookAtTarget && pTarget != null)
        {
            Vec3 vTargetPos = pTarget.GetPos();

            float maxDeviation = distToTarget * sinf(DEG2RAD(15));

            if (distToTarget > GetParameters().m_fPassRadius)
            {
                vLookTargetPos = vTargetPos;
                vLookTargetPos.z = clamp_tpl(vLookTargetPos.z, GetPos().z - maxDeviation, GetPos().z + maxDeviation);
            }

            // Clamp the lookat height when the target is close.
            ushort nTargetType = pTarget.GetType();
            if ((distToTarget < 1.0f) ||
                ((nTargetType == (ushort)AIOBJECT_DUMMY || nTargetType == (ushort)AIOBJECT_HIDEPOINT || nTargetType == (ushort)AIOBJECT_WAYPOINT ||
                nTargetType > (ushort)AIOBJECT_PLAYER) && (distToTarget < 5.0f))) // anchors & dummy objects
            {
                if (!use3DNav)
                {
                    vLookTargetPos = vTargetPos;
                    vLookTargetPos.z = clamp_tpl(vLookTargetPos.z, GetPos().z - maxDeviation, GetPos().z + maxDeviation);
                }
            }
        }
        else if (isMoving && (gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_CRYSIS2))
        {
            // Look forward or to the movement direction
            Vec3 vLookAheadPoint;
            float lookAheadDist = 2.5f;

            if (m_pPathFollower != null)
            {
                float junk;
                vLookAheadPoint = m_pPathFollower.GetPathPointAhead(lookAheadDist, out junk);
            }
            else
            {
                if (!m_Path.GetPosAlongPath(out vLookAheadPoint, lookAheadDist, !m_movementAbility.b3DMove, true))
                {
                    vLookAheadPoint = GetPhysicsPos();
                }
            }

            // Since the path height is not guaranteed to follow terrain, do not even try to look up or down.
            vLookTargetPos = vLookAheadPoint;

            // Make sure the lookahead position is far enough
            Vec3 delta = vLookTargetPos - GetPhysicsPos();
            delta.z = 0.0f;
            float dist = delta.GetLengthSquared();
            if (dist < 1.0f)
            {
                float u = 1.0f - MathF.Sqrt(dist);
                Vec3 safeDir = GetEntityDir();
                safeDir.z = 0;
                delta = delta + (safeDir - delta) * u;
            }
            delta.Normalize();

            vLookTargetPos = GetPhysicsPos() + delta * 40.0f;
            vLookTargetPos.z = GetPos().z;
        }
        else
        {
            // Disable look target.
            vLookTargetPos = new Vec3(0,0,0);
        }

        if (!m_posLookAtSmartObject.IsZero())
        {
            // The SO lookat should override the lookat target in case not requesting to fire and not using lookat goalop.
            if (!m_bLooseAttention && (m_fireMode == EFireMode.FIREMODE_OFF))
            {
                vLookTargetPos = m_posLookAtSmartObject;
            }
        }

        if (!vLookTargetPos.IsZero())
        {
            float distSq = Distance.Point_Point2DSq(vLookTargetPos, GetPos());
            if (distSq < sqr(2.0f))
            {
                Vec3 fakePos = GetPos() + GetEntityDir() * 2.0f;
                if (distSq < sqr(0.7f))
                {
                    vLookTargetPos = fakePos;
                }
                else
                {
                    float speed = m_State.vMoveDir.GetLength();
                    speed = clamp_tpl(speed, 0.0f, 10.0f);
                    float d = MathF.Sqrt(distSq);
                    float u = 1.0f - (d - 0.7f) / (2.0f - 0.7f);
                    vLookTargetPos = vLookTargetPos + speed / 10.0f * u * (fakePos - vLookTargetPos);
                }
            }
        }

        // for the in-vehicle gunners
        if (GetProxy() != null)
        {
            SAIBodyInfo bodyInfo = GetBodyInfo();

            IEntity pLinkedVehicleEntity = bodyInfo.GetLinkedVehicleEntity();
            if (pLinkedVehicleEntity != null)
            {
                if (GetProxy().GetActorIsFallen())
                {
                    vLookTargetPos = new Vec3(0,0,0);
                }
                else
                {
                    CAIObject pUnit = pLinkedVehicleEntity.GetAI() as CAIObject;
                    if (pUnit != null)
                    {
                        if (pUnit.CastToCAIVehicle() != null)
                        {
                            vLookTargetPos = new Vec3(0,0,0);
                            CAIObject pLooseAttentionTarget = m_refLooseAttentionTarget.GetAIObject();
                            if (m_bLooseAttention && pLooseAttentionTarget != null)
                            {
                                pTarget = pLooseAttentionTarget;
                            }
                            if (pTarget != null)
                            {
                                vLookTargetPos = pTarget.GetPos();
                                m_State.allowStrafing = false;
                            }
                        }
                    }
                }
            }
        }

        m_State.vLookTargetPos = vLookTargetPos;
    }

    // EnableUpdateLookTarget — PipeUser.cpp:4884-4887
    public void EnableUpdateLookTarget(bool bEnable = true) { m_bEnableUpdateLookTarget = bEnable; }
    public void PathIsInvalid() { ClearPath("CPipeUser::PathIsInvalid m_Path"); m_State.vMoveDir = new Vec3(0,0,0); m_State.fDesiredSpeed = 0.0f; }
    public bool WasHideObjectRecentlyUnreachable(Vec3 pos) { foreach (var p in m_recentUnreachableHideObjects) if (Vec3.Distance_Point_PointSq(p.Item2, pos) < sqr(0.1f)) return true; return false; }
    public Vec3 GetLastLiveTargetPosition() { return m_lastLiveTargetPos; }
    public float GetTimeSinceLastLiveTarget() { return m_timeSinceLastLiveTarget; }
    public virtual IAIObject GetAttentionTargetAssociation() { CAIObject att = m_refAttentionTarget.GetAIObject(); return att?.GetAssociation()?.GetAIObject(); }
    public virtual CAIObject GetLastOpResult() { return m_refLastOpResult.GetAIObject(); }
    public virtual CAIObject GetSpecialAIObject(string objName, float range = 0.0f) { if (objName == null) return null; CCCPOINT("CPipeUser_GetSpecialAIObject"); if (objName == "self") return this; if (objName == "atttarget") return m_refAttentionTarget.GetAIObject(); if (objName == "refpoint") return GetRefPoint(); if (objName == "probtarget") { var o = GetOrCreateSpecialAIObject(ESpecialAIObjects.AISPECIAL_PROBTARGET); o?.SetPos(GetProbableTargetPosition()); return o; } return gAIEnv.pAIObjectManager?.GetAIObjectByName(objName); }
    public override EAITargetThreat GetAttentionTargetThreat() { return m_AttTargetThreat; }
    public override EAITargetType GetAttentionTargetType() { return m_AttTargetType; }
    public virtual void SetLastActionStatus(bool bSucceed) { m_bLastActionSucceed = bSucceed; }
    public void ClearInvalidatedSOLinks() { m_invalidatedSOLinks.Clear(); }
    public void InvalidateSOLink(CSmartObject pObject, SmartObjectHelper pFromHelper, SmartObjectHelper pToHelper) { m_invalidatedSOLinks.Add((pObject, pFromHelper, pToHelper)); }
    public bool IsSOLinkInvalidated(CSmartObject pObject, SmartObjectHelper pFromHelper, SmartObjectHelper pToHelper) { return m_invalidatedSOLinks.Contains((pObject, pFromHelper, pToHelper)); }
    public bool ConvertPathToSpline(IAISystem_ENavigationType navType) { var path = m_Path.GetPath(); if (path.Count == 0) return false; List<Vec3> pts = new List<Vec3>(path.Count); foreach (var ppd in path) pts.Add(ppd.vPos); SNavPathParams np = m_Path.GetParams(); m_Path.Clear("CPipeUser::ConvertPathToSpline"); SetPointListToFollow(pts, navType, true); m_OrigPath.Clear("CPipeUser::ConvertPathBySpline"); if (np != null) np.precalculatedPath = true; m_Path.SetParams(np); m_nPathDecision = (int)EPathfinderResult.PATHFINDER_PATHFOUND; m_OrigPath = m_Path; return true; }

    // Update — PipeUser.cpp:762-1122 (core structure — non-Puppet path)
    public new void Update(EObjectUpdate type) { base.Update(type); UpdateCoverBlacklist(gEnv.pTimer?.GetFrameTime() ?? 0.0f); }

    // Cover methods — PipeUser.cpp:1124-1408
    public void SetCoverID(CoverID coverID) { if (coverID.IsValid()) { CoverUser.Params cp = new CoverUser.Params(); cp.distanceToCover = m_Parameters.distanceToCover; cp.inCoverRadius = m_Parameters.inCoverRadius; cp.userID = GetAIObjectID(); m_coverUser.SetParams(cp); } m_coverUser.SetCoverID(coverID); }
    public CoverID GetCoverID() { return m_coverUser.GetCoverID(); }
    public Vec3 GetCoverLocation() { return m_coverUser.GetCoverLocation(); }
    public uint GetCoverEyes(CAIObject targetEnemy, Vec3 enemyTargetLocation, Vec3[] eyes, uint maxCount) { uint ec = 0; if (!enemyTargetLocation.IsZero()) eyes[ec++] = enemyTargetLocation; if (targetEnemy == null) return ec; float rSq = sqr(0.5f); if (gAIEnv.CVars.CoverPredictTarget > 0.001f && ec < maxCount && targetEnemy.IsAgent()) { Vec3 fl = eyes[ec-1] + targetEnemy.GetVelocity() * gAIEnv.CVars.CoverPredictTarget; if (!HasPointInRangeSq(eyes, ec, fl, rSq)) eyes[ec++] = fl; } return ec; }
    private static bool HasPointInRangeSq(Vec3[] points, uint count, Vec3 pos, float rSq) { for (uint i = 0; i < count; ++i) if (Vec3.Distance_Point_PointSq(points[i], pos) < rSq) return true; return false; }
    public bool IsAdjustingAim() { return m_adjustingAim; }
    public void SetAdjustingAim(bool adjustingAim) { m_adjustingAim = adjustingAim; }
    public virtual bool IsInCover() { return m_inCover; }
    public virtual void SetInCover(bool inCover) { Debug.Assert(!inCover || m_coverUser.GetCoverID().IsValid()); if (m_inCover != inCover) { if (inCover) SetSignal(1, "OnEnterCover", null, null, gAIEnv.SignalCRCs.m_nOnEnterCover); else { SetCoverID(new CoverID()); ResetBodyTargetDir(); SetSignal(1, "OnLeaveCover", null, null, gAIEnv.SignalCRCs.m_nOnLeaveCover); } m_inCover = inCover; } }
    public virtual void SetCoverCompromised() { if (m_inCover || m_movingToCover) { SetSignal(1, "OnCoverCompromised", null, null, gAIEnv.SignalCRCs.m_nOnCoverCompromised); CoverID cid = m_coverUser.GetCoverID(); if (cid.IsValid()) SetCoverBlacklisted(cid, true, 10.0f); SetInCover(false); m_coverUser.SetCoverID(new CoverID()); } }
    public virtual bool IsCoverCompromised() { if (gAIEnv.CVars.CoverSystem != 0) return m_coverUser.IsCompromised(); CAIObject att = m_refAttentionTarget.GetAIObject(); if (att == null) return false; if (m_CurrentHideObject.IsValid()) return m_CurrentHideObject.IsCompromised(this, att.GetPos()); return true; }
    public void SetMovingToCover(bool b) { if (m_movingToCover != b) { if (b) SetSignal(1, "OnMovingToCover", null, null, gAIEnv.SignalCRCs.m_nOnMovingToCover); m_movingToCover = b; } }
    public bool IsMovingToCover() { return m_movingToCover; }
    public void SetMovingInCover(bool b) { if (m_movingInCover != b) { if (b) SetSignal(1, "OnMovingInCover", null, null, gAIEnv.SignalCRCs.m_nOnMovingInCover); m_movingInCover = b; } }
    public bool IsMovingInCover() { return m_movingInCover; }
    public virtual bool IsTakingCover(float distanceThreshold) { if (IsInCover()) return true; if (distanceThreshold > 0.0f && IsMovingToCover()) return m_State.fDistanceToPathEnd < distanceThreshold; return false; }
    public void UpdateCoverBlacklist(float updateTime) { List<CoverID> toRemove = new List<CoverID>(); List<CoverID> keys = new List<CoverID>(m_coverBlacklist.Keys); foreach (var k in keys) { m_coverBlacklist[k] -= updateTime; if (m_coverBlacklist[k] <= 0) toRemove.Add(k); } foreach (var k in toRemove) m_coverBlacklist.Remove(k); }
    public void ResetCoverBlacklist() { m_coverBlacklist.Clear(); }
    public void SetCoverBlacklisted(CoverID coverID, bool blacklist, float time = 0.0f) { if (blacklist) m_coverBlacklist[coverID] = time; else m_coverBlacklist.Remove(coverID); }
    public bool IsCoverBlacklisted(CoverID coverID) { return m_coverBlacklist.ContainsKey(coverID); }
    public float GetCoverLocationEffectiveHeight() { return m_coverUser.GetLocationEffectiveHeight(); }
    public CoverHeight CalculateEffectiveCoverHeight() { Vec3[] eyes = new Vec3[8]; uint MaxEyeCount = (uint)Math.Min(gAIEnv.CVars.CoverMaxEyeCount, 8); CAIObject att = m_refAttentionTarget.GetAIObject(); uint ec = 0; if (att != null) ec = GetCoverEyes(att, att.GetPos(), eyes, MaxEyeCount); float eh = m_coverUser.CalculateEffectiveHeightAt(GetPhysicsPos(), eyes, ec); return (eh >= m_Parameters.effectiveHighCoverHeight) ? CoverHeight.HighCover : CoverHeight.LowCover; }
    public float GetLastUpdateInterval() { return m_fTimePassed; }
    // GetCoverUsageInfo — PipeUser.cpp:1422-1550 — full literal translation
    public AsyncState GetCoverUsageInfo(out CoverUsageInfo usageInfo)
    {
        usageInfo = new CoverUsageInfo(false);

        if (m_coverUsageInfoState.state == AsyncState.AsyncComplete)
        {
            usageInfo = m_coverUsageInfoState.result;
            m_coverUsageInfoState.state = AsyncState.AsyncReady;
            return AsyncState.AsyncComplete;
        }

        IAIObject attentionTarget = GetAttentionTarget();
        if (attentionTarget == null)
        {
            usageInfo = new CoverUsageInfo(false);
            return AsyncState.AsyncComplete;
        }

        if (m_coverUsageInfoState.state == AsyncState.AsyncReady)
        {
            m_coverUsageInfoState.state = AsyncState.AsyncInProgress;

            CAIHideObject hideObject = m_CurrentHideObject;

            Vec3 target = attentionTarget.GetPos();
            float offset = GetParameters().m_fPassRadius;

            bool lowCover = hideObject.HasLowCover();
            bool highCover = hideObject.HasHighCover();

            Vec3[] checkOrigin = new Vec3[6];
            bool[] checkResult = new bool[6];

            m_coverUsageInfoState.result.lowCompromised = !lowCover;
            m_coverUsageInfoState.result.highCompromised = !highCover;

            if (lowCover)
            {
                bool compromised = false;
                float leftEdge, rightEdge, leftUmbra, rightUmbra;
                hideObject.GetCoverDistances(true, target, out compromised, out leftEdge, out rightEdge, out leftUmbra, out rightUmbra);
                float left = MathF.Max(leftEdge, leftUmbra);
                float right = MathF.Min(rightEdge, rightUmbra);

                left += offset;
                right -= offset;

                Vec3 originOffset = new Vec3(0.0f, 0.0f, 0.7f);

                checkOrigin[CoverUsageCheckLowLeft] = hideObject.GetPointAlongCoverPath(left) + originOffset;
                checkOrigin[CoverUsageCheckLowRight] = hideObject.GetPointAlongCoverPath(right) + originOffset;
                checkOrigin[CoverUsageCheckLowCenter] = 0.5f * (checkOrigin[CoverUsageCheckLowLeft] + checkOrigin[CoverUsageCheckLowRight]);

                checkResult[CoverUsageCheckLowLeft] = hideObject.IsLeftEdgeValid(true);
                checkResult[CoverUsageCheckLowCenter] = true;
                checkResult[CoverUsageCheckLowRight] = hideObject.IsRightEdgeValid(true);

                m_coverUsageInfoState.result.lowCompromised = compromised;
            }
            else
            {
                checkResult[CoverUsageCheckLowLeft] = false;
                checkResult[CoverUsageCheckLowRight] = false;
                checkResult[CoverUsageCheckLowCenter] = false;

                m_coverUsageInfoState.result.lowLeft = false;
                m_coverUsageInfoState.result.lowCenter = false;
                m_coverUsageInfoState.result.lowRight = false;
            }

            if (highCover)
            {
                bool compromised = false;
                float leftEdge, rightEdge, leftUmbra, rightUmbra;
                hideObject.GetCoverDistances(false, target, out compromised, out leftEdge, out rightEdge, out leftUmbra, out rightUmbra);
                float left = MathF.Max(leftEdge, leftUmbra);
                float right = MathF.Min(rightEdge, rightUmbra);

                left += offset;
                right -= offset;

                Vec3 originOffset = new Vec3(0.0f, 0.0f, 1.5f);

                checkOrigin[CoverUsageCheckHighLeft] = hideObject.GetPointAlongCoverPath(left) + originOffset;
                checkOrigin[CoverUsageCheckHighRight] = hideObject.GetPointAlongCoverPath(right) + originOffset;
                checkOrigin[CoverUsageCheckHighCenter] = 0.5f * (checkOrigin[CoverUsageCheckHighLeft] + checkOrigin[CoverUsageCheckHighRight]);

                checkResult[CoverUsageCheckHighLeft] = hideObject.IsLeftEdgeValid(false);
                checkResult[CoverUsageCheckHighCenter] = true;
                checkResult[CoverUsageCheckHighRight] = hideObject.IsRightEdgeValid(false);

                m_coverUsageInfoState.result.highCompromised = compromised;
            }
            else
            {
                checkResult[CoverUsageCheckHighLeft] = false;
                checkResult[CoverUsageCheckHighCenter] = false;
                checkResult[CoverUsageCheckHighRight] = false;

                m_coverUsageInfoState.result.highLeft = false;
                m_coverUsageInfoState.result.highCenter = false;
                m_coverUsageInfoState.result.highRight = false;
            }

            for (int i = 0; i < 6; ++i)
            {
                if (!checkResult[i])
                    continue;

                Vec3 dir = target - checkOrigin[i];
                if (dir.GetLengthSquared2D() > 3.0f * 3.0f)
                    dir.SetLength(3.0f);

                m_coverUsageInfoState.rayID[i] = gAIEnv.pRayCaster.Queue(
                    new RayCastRequest(checkOrigin[i], dir, (CryAISystem.CryCommon.EAICollisionEntities)COVER_OBJECT_TYPES, HIT_COVER),
                    (QueuedRayID rayID, RayCastResult result) => CoverUsageInfoRayComplete(rayID, result));
                ++m_coverUsageInfoState.rayCount;
            }
        }

        return m_coverUsageInfoState.state;
    }

    // CoverUsageInfoRayComplete — PipeUser.cpp:1552-1593 — full literal translation
    private void CoverUsageInfoRayComplete(QueuedRayID rayID, RayCastResult result)
    {
        --m_coverUsageInfoState.rayCount;

        for (int i = 0; i < 6; ++i)
        {
            if (m_coverUsageInfoState.rayID[i].id != rayID.id)
                continue;

            m_coverUsageInfoState.rayID[i] = new QueuedRayID();

            switch (i)
            {
            case CoverUsageCheckLowLeft:
                m_coverUsageInfoState.result.lowLeft = result.hitCount != 0;
                break;
            case CoverUsageCheckLowCenter:
                m_coverUsageInfoState.result.lowCenter = result.hitCount != 0;
                break;
            case CoverUsageCheckLowRight:
                m_coverUsageInfoState.result.lowRight = result.hitCount != 0;
                break;
            case CoverUsageCheckHighLeft:
                m_coverUsageInfoState.result.highLeft = result.hitCount != 0;
                break;
            case CoverUsageCheckHighCenter:
                m_coverUsageInfoState.result.highCenter = result.hitCount != 0;
                break;
            case CoverUsageCheckHighRight:
                m_coverUsageInfoState.result.highRight = result.hitCount != 0;
                break;
            default:
                Debug.Assert(false);
                break;
            }

            break;
        }

        if (m_coverUsageInfoState.rayCount == 0)
            m_coverUsageInfoState.state = AsyncState.AsyncComplete;
    }

    // ECoverUsageCheckLocation — PipeUser.cpp:1412-1420
    private const int CoverUsageCheckLowLeft = 0;
    private const int CoverUsageCheckLowCenter = 1;
    private const int CoverUsageCheckLowRight = 2;
    private const int CoverUsageCheckHighLeft = 3;
    private const int CoverUsageCheckHighCenter = 4;
    private const int CoverUsageCheckHighRight = 5;
    // OnAIHandlerSentSignal and GetPathfinderState moved to expanded methods below
    public INavPath GetINavPath() { return m_Path; }

    // GoalPipe listener methods — PipeUser.cpp:3427-3525
    public void NotifyListeners(CGoalPipe pPipe, EGoalPipeEvent ev, bool includeSubPipes = false) { if (pPipe != null && pPipe.GetEventId() != 0) NotifyListeners(pPipe.GetEventId(), ev); if (includeSubPipes && pPipe != null) NotifyListeners(pPipe.GetSubpipe(), ev, true); }
    public void NotifyListeners(int goalPipeId, EGoalPipeEvent ev) { if (goalPipeId == 0 || bNotifyListenersLock) return; bNotifyListenersLock = true; List<(int, IGoalPipeListener)> pending = new List<(int, IGoalPipeListener)>(); if (m_mapGoalPipeListeners.TryGetValue(goalPipeId, out var listeners)) { foreach (var (pl, dn) in listeners) { bool unreg = false; pl.OnGoalPipeEvent(this, ev, goalPipeId, ref unreg); if (unreg) pending.Add((goalPipeId, pl)); } } bNotifyListenersLock = false; foreach (var (gpId, pl) in pending) UnRegisterGoalPipeListener(pl, gpId); }
    public virtual void RegisterGoalPipeListener(IGoalPipeListener pListener, int goalPipeId, string debugClassName) { if (!m_mapGoalPipeListeners.TryGetValue(goalPipeId, out var l)) { l = new List<(IGoalPipeListener, string)>(); m_mapGoalPipeListeners[goalPipeId] = l; } l.Add((pListener, debugClassName)); }
    public virtual void UnRegisterGoalPipeListener(IGoalPipeListener pListener, int goalPipeId) { if (m_mapGoalPipeListeners.TryGetValue(goalPipeId, out var l)) { for (int i = 0; i < l.Count; i++) { if (l[i].Item1 == pListener) { l.RemoveAt(i); if (l.Count == 0) m_mapGoalPipeListeners.Remove(goalPipeId); return; } } } }
    public int CountGroupedActiveGoals() { int c = 0; for (int i = 0; i < m_vActiveGoals.Count; i++) if (m_vActiveGoals[i].eGrouping == IGoalPipe_EGroupType.eGT_GROUPED && m_vActiveGoals[i].op != EGoalOperations.eGO_WAIT) ++c; return c; }
    public void ClearGroupedActiveGoals() { for (int i = 0; i < m_vActiveGoals.Count; i++) { if (m_vActiveGoals[i].eGrouping == IGoalPipe_EGroupType.eGT_GROUPED && m_vActiveGoals[i].op != EGoalOperations.eGO_WAIT) { RemoveActiveGoal(i); if (m_vActiveGoals.Count > 0) --i; } } }
    public EAimState GetAimState() { return m_aimState; }
    public void SetNavSOFailureStates() { if (m_eNavSOMethod != ENavSOMethod.nSOmNone) { if (!m_currentNavSOStates.IsEmpty()) { gAIEnv.pSmartObjectManager?.ModifySmartObjectStates(GetEntity(), m_currentNavSOStates.sAnimationFailUserStates); m_currentNavSOStates.Clear(); } if (!m_pendingNavSOStates.IsEmpty()) { gAIEnv.pSmartObjectManager?.ModifySmartObjectStates(GetEntity(), m_pendingNavSOStates.sAnimationFailUserStates); m_pendingNavSOStates.Clear(); } } m_State.actorTargetReq.Reset(); m_eNavSOMethod = ENavSOMethod.nSOmNone; }

    public enum ESpecialAIObjects { AISPECIAL_LAST_HIDEOBJECT, AISPECIAL_PROBTARGET, AISPECIAL_PROBTARGET_IN_TERRITORY, AISPECIAL_PROBTARGET_IN_REFSHAPE, AISPECIAL_PROBTARGET_IN_TERRITORY_AND_REFSHAPE, AISPECIAL_ATTTARGET_IN_TERRITORY, AISPECIAL_ATTTARGET_IN_REFSHAPE, AISPECIAL_ATTTARGET_IN_TERRITORY_AND_REFSHAPE, AISPECIAL_ANIM_TARGET, AISPECIAL_GROUP_TAC_POS, AISPECIAL_GROUP_TAC_LOOK, AISPECIAL_VEHICLE_AVOID_POS, COUNT_AISPECIAL }
    public CAIObject GetOrCreateSpecialAIObject(ESpecialAIObjects type) { CStrongRef<CAIObject> refObj = m_refSpecialObjects[(int)type]; if (!refObj.IsNil()) return refObj.GetAIObject(); CCCPOINT("CPipeUser_GetOrCreateSpecialAIObject"); ESubType subType = ESubType.STP_SPECIAL; string objName = GetName(); switch (type) { case ESpecialAIObjects.AISPECIAL_LAST_HIDEOBJECT: objName += "_*LastHideObj"; break; case ESpecialAIObjects.AISPECIAL_PROBTARGET: objName += "_*ProbTgt"; break; case ESpecialAIObjects.AISPECIAL_ANIM_TARGET: objName += "_*AnimTgt"; subType = ESubType.STP_ANIM_TARGET; break; case ESpecialAIObjects.AISPECIAL_GROUP_TAC_POS: objName += "_GroupTacPos"; subType = ESubType.STP_FORMATION; break; default: objName += "_*Special" + ((int)type).ToString("D2"); break; } gAIEnv.pAIObjectManager?.CreateDummyObject(refObj, objName, subType); return refObj.GetAIObject(); }
    public bool ShouldConsiderActorsAsPathObstacles() { return m_considerActorsAsPathObstacles; }

    // OnAIHandlerSentSignal — PipeUser.cpp:4889-4904 — full literal translation
    public override void OnAIHandlerSentSignal(string szText, uint crcCode)
    {
        Debug.Assert(crcCode != 0);

        int i = 0;
        while (i < m_listWaitGoalOps.Count)
        {
            COPWaitSignal pGoalOp = m_listWaitGoalOps[i];
            if (pGoalOp.NotifySignalReceived(this, szText, null))
                m_listWaitGoalOps.RemoveAt(i);
            else
                ++i;
        }

        base.OnAIHandlerSentSignal(szText, crcCode);
    }

    // GetPathfinderState — PipeUser.cpp:4906-4923 — full literal translation
    public Movement_PathfinderState GetPathfinderState()
    {
        switch (m_nPathDecision)
        {
        case (int)EPathfinderResult.PATHFINDER_STILLFINDING:
            return Movement_PathfinderState.StillFinding;
        case (int)EPathfinderResult.PATHFINDER_PATHFOUND:
            return Movement_PathfinderState.FoundPath;
        case (int)EPathfinderResult.PATHFINDER_NOPATH:
            return Movement_PathfinderState.CouldNotFindPath;
        default:
            Debug.Assert(false);
            return Movement_PathfinderState.CouldNotFindPath;
        }
    }

    // HandleVisualStimulus — PipeUser.cpp:4570-4608 — full literal translation
    protected override void HandleVisualStimulus(SAIEVENT pAIEvent)
    {
        float fGlobalVisualPerceptionScale = gEnv.pAISystem?.GetGlobalVisualScale(this) ?? 1.0f;
        float fVisualPerceptionScale = m_Parameters.m_PerceptionParams.perceptionScale.visual * fGlobalVisualPerceptionScale;
        if (gAIEnv.CVars.IgnoreVisualStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode || fVisualPerceptionScale <= 0.0f)
            return;

        if (gAIEnv.pTargetTrackManager?.IsEnabled() ?? false)
        {
            // Check if in range (using perception scale)
            if (EFieldOfViewResult.eFOV_Outside != IsPointInFOV(pAIEvent.vPosition, fVisualPerceptionScale))
            {
                gAIEnv.pTargetTrackManager.HandleStimulusFromAIEvent(GetAIObjectID(), pAIEvent, TargetTrackHelpers.EEventType.eEST_Visual);

                IEntity pEventOwnerEntity = gEnv.pEntitySystem?.GetEntity(pAIEvent.sourceEntityID);
                if (pEventOwnerEntity == null)
                    return;

                IAIObject pEventOwnerAI = pEventOwnerEntity.GetAI();
                if (pEventOwnerAI == null)
                    return;

                if (IsHostile(pEventOwnerAI))
                {
                    m_State.nTargetType = ((CAIObject)pEventOwnerAI).GetType();
                    m_stimulusStartTime = GetAISystem().GetFrameStartTimeSeconds();

                    m_AttTargetThreat = m_State.eTargetThreat = EAITargetThreat.AITHREAT_AGGRESSIVE;
                    m_AttTargetType = m_State.eTargetType = EAITargetType.AITARGET_VISUAL;

                    CWeakRef<CAIObject> refAttentionTarget = WeakRefHelpers.GetWeakRef((CAIObject)pEventOwnerAI);
                    if (refAttentionTarget != m_refAttentionTarget)
                    {
                        SetAttentionTarget(refAttentionTarget);
                    }
                }
            }
        }
    }

    // HandleSoundEvent — PipeUser.cpp:4611-4652 — full literal translation
    protected override void HandleSoundEvent(SAIEVENT pAIEvent)
    {
        float fGlobalAudioPerceptionScale = gEnv.pAISystem?.GetGlobalAudioScale(this) ?? 1.0f;
        float fAudioPerceptionScale = m_Parameters.m_PerceptionParams.perceptionScale.audio * fGlobalAudioPerceptionScale;
        if (gAIEnv.CVars.IgnoreSoundStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode || fAudioPerceptionScale <= 0.0f)
            return;

        if (gAIEnv.pTargetTrackManager?.IsEnabled() ?? false)
        {
            // Check if in range (using perception scale)
            Vec3 vMyPos = GetPos();
            float fSoundDistance = vMyPos.GetDistance(pAIEvent.vPosition) * (1.0f / fAudioPerceptionScale);
            if (fSoundDistance <= pAIEvent.fThreat)
            {
                gAIEnv.pTargetTrackManager.HandleStimulusFromAIEvent(GetAIObjectID(), pAIEvent, TargetTrackHelpers.EEventType.eEST_Sound);

                IEntity pEventOwnerEntity = gEnv.pEntitySystem?.GetEntity(pAIEvent.sourceEntityID);
                if (pEventOwnerEntity == null)
                    return;

                IAIObject pEventOwnerAI = pEventOwnerEntity.GetAI();
                if (pEventOwnerAI == null)
                    return;

                if (IsHostile(pEventOwnerAI))
                {
                    if ((m_AttTargetType != EAITargetType.AITARGET_MEMORY) && (m_AttTargetType != EAITargetType.AITARGET_VISUAL))
                    {
                        m_State.nTargetType = ((CAIObject)pEventOwnerAI).GetType();
                        m_stimulusStartTime = GetAISystem().GetFrameStartTimeSeconds();

                        m_AttTargetThreat = m_State.eTargetThreat = EAITargetThreat.AITHREAT_AGGRESSIVE;
                        m_AttTargetType = m_State.eTargetType = EAITargetType.AITARGET_SOUND;

                        SetAttentionTarget(WeakRefHelpers.GetWeakRef((CAIObject)pEventOwnerAI));
                    }
                }
            }
        }
    }

    // Serialize — PipeUser.cpp:3195-3411 — full literal translation
    public override void Serialize(TSerialize ser)
    {
        CCCPOINT("CPipeUser_Serialize");

        // m_mapGoalPipeListeners must not change here! there's no need to serialize it!

        if (ser.IsReading())
            ResetCurrentPipe(true);

        base.Serialize(ser);

        ser.Value("m_bLastNearForbiddenEdge", ref m_bLastNearForbiddenEdge);
        ser.Value("m_bLastActionSucceed", ref m_bLastActionSucceed);

        // serialize members
        m_refRefPoint.Serialize(ser, "m_refRefPoint");
        m_refLookAtTarget.Serialize(ser, "m_refLookAtTarget");

        ser.Value("m_bEnableUpdateLookTarget", ref m_bEnableUpdateLookTarget);

        ser.Value("m_AttTargetPersistenceTimeout", ref m_AttTargetPersistenceTimeout);
        ser.EnumValue("m_AttTargetThreat", ref m_AttTargetThreat);
        ser.EnumValue("m_AttTargetExposureThreat", ref m_AttTargetExposureThreat);
        ser.EnumValue("m_AttTargetType", ref m_AttTargetType);

        m_refLastOpResult.Serialize(ser, "m_refLastOpResult");

        ser.Value("m_bPathfinderConsidersPathTargetDirection", ref m_bPathfinderConsidersPathTargetDirection);
        ser.Value("m_fTimePassed", ref m_fTimePassed);

        // Cover information — not serialized by default (matches #else branch)
        if (ser.IsReading())
        {
            m_inCover = false;
            m_movingToCover = false;
            m_movingInCover = false;
            m_vBodyTargetDir = new Vec3(0,0,0);
            m_coverUser.SetCoverID(new CoverID());
        }

        ser.Value("LooseAttention", ref m_bLooseAttention);

        m_refLooseAttentionTarget.Serialize(ser, "m_refLooseAttentionTarget");

        ser.Value("m_looseAttentionId", ref m_looseAttentionId);

        m_CurrentHideObject.Serialize(ser);

        ser.Value("m_nPathDecision", ref m_nPathDecision);
        ser.Value("m_adjustpath", ref m_adjustpath);
        ser.Value("m_IsSteering", ref m_IsSteering);

        if (ser.IsReading())
        {
            m_fireMode = EFireMode.FIREMODE_OFF;
        }

        m_refFireTarget.Serialize(ser, "m_refFireTarget");

        ser.Value("m_fireModeUpdated", ref m_fireModeUpdated);
        ser.Value("m_outOfAmmoSent", ref m_outOfAmmoSent);
        ser.Value("m_lowAmmoSent", ref m_lowAmmoSent);
        ser.Value("m_wasReloading", ref m_wasReloading);
        ser.Value("m_bFirstUpdate", ref m_bFirstUpdate);
        ser.Value("m_pathToFollowName", ref m_pathToFollowName);
        ser.Value("m_bPathToFollowIsSpline", ref m_bPathToFollowIsSpline);
        { uint navTemp = (uint)m_CurrentNodeNavType; ser.Value("m_CurrentNodeNavType", ref navTemp); m_CurrentNodeNavType = (IAISystem_ENavigationType)navTemp; }

        // Serialize special objects
        for (int i = 0; i < (int)ESpecialAIObjects.COUNT_AISPECIAL; ++i)
        {
            string specialName = "m_pSpecialObjects" + i.ToString();
            m_refSpecialObjects[i].Serialize(ser, specialName);
        }

        // Goal pipe — non-serialized path (matches #else branch)
        if (ser.IsReading())
        {
            ResetCurrentPipe(true);
            Debug.Assert(m_pCurrentGoalPipe == null);
            m_notAllowedSubpipes.Clear();
            SelectPipe(0, "_first_", NILREF, 0, true);
        }

        // this stuff can get reset when selecting pipe - so, serialize it after
        ser.Value("Blocked", ref m_bBlocked);
        ser.Value("KeepMoving", ref m_bKeepMoving);
        ser.Value("LastMoveDir", ref m_vLastMoveDir);

        ser.Value("m_PathDestinationPos", ref m_PathDestinationPos);

        m_Path.Serialize(ser);
        m_OrigPath.Serialize(ser);
        if (ser.IsWriting())
        {
            if (m_pPathFollower != null)
            {
                ser.BeginOptionalGroup("m_pPathFollower", true);
                m_pPathFollower.Serialize(ser);
                m_pPathFollower.AttachToPath(m_Path);
                ser.EndGroup();
            }
        }
        else
        {
            m_pPathFollower = null;
            if (ser.BeginOptionalGroup("m_pPathFollower", true))
            {
                m_pPathFollower = CreatePathFollower(new PathFollowerParams());
                m_pPathFollower?.Serialize(ser);
                m_pPathFollower?.AttachToPath(m_Path);
                ser.EndGroup();
            }
        }
        ser.Value("m_posLookAtSmartObject", ref m_posLookAtSmartObject);

        ser.BeginGroup("UnreachableHideObjectList");
        {
            int count = m_recentUnreachableHideObjects.Count;
            ser.Value("UnreachableHideObjectList_size", ref count);
            if (ser.IsReading())
                m_recentUnreachableHideObjects.Clear();
            var it = m_recentUnreachableHideObjects.First;
            for (int i = 0; i < count; i++)
            {
                float time = 0;
                Vec3 point = new Vec3(0,0,0);
                if (ser.IsWriting() && it != null)
                {
                    time = it.Value.Item1;
                    point = it.Value.Item2;
                    it = it.Next;
                }
                string timeName = "time_" + i.ToString();
                string pointName = "point_" + i.ToString();
                ser.Value(timeName, ref time);
                ser.Value(pointName, ref point);
                if (ser.IsReading())
                    m_recentUnreachableHideObjects.AddLast((time, point));
            }
            ser.EndGroup();
        }

        { uint navTemp = (uint)m_eNavSOMethod; ser.Value("m_eNavSOMethod", ref navTemp); m_eNavSOMethod = (ENavSOMethod)navTemp; }
        ser.Value("m_idLastUsedSmartObject", ref m_idLastUsedSmartObject);

        m_currentNavSOStates.Serialize(ser);
        m_pendingNavSOStates.Serialize(ser);

        ser.Value("m_actorTargetReqId", ref m_actorTargetReqId);

        ser.Value("m_refShapeName", ref m_refShapeName);
        if (ser.IsReading())
            m_refShape = GetAISystem().GetGenericShapeOfName(m_refShapeName);

        if (ser.IsWriting())
        {
            if (m_pActorTargetRequest != null)
            {
                ser.BeginOptionalGroup("m_pActorTargetRequest", true);
                m_pActorTargetRequest.Serialize(ser);
                ser.EndGroup();
            }
        }
        else
        {
            if (ser.BeginOptionalGroup("m_pActorTargetRequest", true))
            {
                if (m_pActorTargetRequest == null)
                    m_pActorTargetRequest = new SAIActorTargetRequest();
                m_pActorTargetRequest.Serialize(ser);
                ser.EndGroup();
            }
        }
    }

    // PostSerialize — PipeUser.cpp:3413-3418
    public override void PostSerialize()
    {
        base.PostSerialize();
        gAIEnv.pMovementSystem?.RegisterEntity(GetEntityID(), m_callbacksForPipeuser, m_movementActorAdapter);
    }
    protected void ClearActiveGoals() { for (int i = 0; i < m_vActiveGoals.Count; i++) m_vActiveGoals[i].pGoalOp?.Reset(this); m_vActiveGoals.Clear(); m_bBlocked = false; }
    protected bool ProcessBranchGoal(QGoal Goal, ref bool blocking) { if (Goal.op != EGoalOperations.eGO_BRANCH) return false; blocking = Goal.bBlocking; bool bNot = (Goal.parms.nValue & (int)EBranchConditionFlags.NOT) != 0; if (GetBranchCondition(Goal) ^ bNot) { if (string.IsNullOrEmpty(Goal.parms.str)) blocking |= m_pCurrentGoalPipe.Jump(Goal.parms.nValueAux); else blocking |= m_pCurrentGoalPipe.Jump(Goal.parms.str); } return true; }
    protected bool ProcessRandomGoal(QGoal Goal, ref bool blocking) { if (Goal.op != EGoalOperations.eGO_RANDOM) return false; blocking = true; if (Goal.parms.fValue > CryRandom.cry_random(0, 99)) m_pCurrentGoalPipe.Jump(Goal.parms.nValue); return true; }
    protected bool ProcessClearGoal(QGoal Goal, ref bool blocking) { if (Goal.op != EGoalOperations.eGO_CLEAR) return false; ClearActiveGoals(); if (Goal.parms.fValue != 0) { SetAttentionTarget(NILREF); ClearPotentialTargets(); } blocking = false; return true; }
    // GetBranchCondition — PipeUser.cpp:368-760 — full literal translation
    protected bool GetBranchCondition(QGoal Goal)
    {
        int branchType = Goal.parms.nValue & (~(int)EBranchConditionFlags.NOT);

        // Fetch attention target, because it's used in many cases
        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();

        switch (branchType)
        {
        case (int)EBranchConditions.IF_RANDOM:
            {
                if (CryRandom.cry_random(0.0f, 1.0f) <= Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_NO_PATH:
            {
                if (m_nPathDecision == (int)EPathfinderResult.PATHFINDER_NOPATH)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_PATH_STILL_FINDING:
            {
                if (m_nPathDecision == (int)EPathfinderResult.PATHFINDER_STILLFINDING)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_IS_HIDDEN: // branch if already at hide spot
            {
                if (!m_CurrentHideObject.IsValid())
                    return false;
                Vec3 diff = m_CurrentHideObject.GetLastHidePos() - GetPos();
                diff.z = 0.0f;
                if (diff.GetLengthSquared() < Goal.parms.fValue * Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_CAN_HIDE: // branch if hide spot was found
            {
                if (m_CurrentHideObject.IsValid())
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_CANNOT_HIDE:
            {
                if (!m_CurrentHideObject.IsValid())
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_STANCE_IS: // branch if stance is equal to params.fValue
            {
                if (m_State.bodystate == Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_HAS_FIRED: // jumps if the PipeUser just fired
            {
                if (m_State.fire != CryAISystem.CryCommon.EAIFireState.eAIFS_Off)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_FIRE_IS: // branch if last "firecmd" argument was equal to params.fValue
            {
                bool state = Goal.parms.fValue > 0.5f;
                if (AllowedToFire() == state)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_NO_LASTOP:
            {
                if (m_refLastOpResult.IsNil() || m_refLastOpResult.GetAIObject() == null)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_SEES_LASTOP:
            {
                CAIObject pLastOpResult = m_refLastOpResult.GetAIObject();
                if (pLastOpResult != null && GetAISystem().CheckObjectsVisibility(this, pLastOpResult, Goal.parms.fValue))
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_SEES_TARGET:
            {
                if (Goal.parms.fValueAux >= 0.0f)
                {
                    if (pAttentionTarget != null)
                    {
                        CCCPOINT("CPipeUser_GetBranchCondition_IF_SEES_TARGET");
                        SAIBodyInfo bi = new SAIBodyInfo();

                        if (GetProxy() != null && GetProxy().QueryBodyInfo(new SAIBodyInfoQuery((EStance)(int)Goal.parms.fValueAux, 0.0f, 0.0f, false), bi))
                        {
                            Vec3 pos = bi.vEyePos;
                            Vec3 dir = pAttentionTarget.GetPos() - pos;
                            if (dir.GetLengthSquared() > sqr(Goal.parms.fValue))
                                dir.SetLength(Goal.parms.fValue);

                            if (CanSee(pAttentionTarget.GetVisionID()))
                                return true;
                        }
                    }
                }
                {
                    if (pAttentionTarget != null && GetAISystem().CheckObjectsVisibility(this, pAttentionTarget, Goal.parms.fValue))
                        return true;
                }
            }
            break;
        case (int)EBranchConditions.IF_TARGET_LOST_TIME_MORE:
            {
                CPuppet pPuppet = CastToCPuppet();
                if (pPuppet != null && pPuppet.m_targetLostTime > Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_TARGET_LOST_TIME_LESS:
            {
                CPuppet pPuppet = CastToCPuppet();
                if (pPuppet != null && pPuppet.m_targetLostTime <= Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_EXPOSED_TO_TARGET:
            {
                if (pAttentionTarget != null)
                {
                    CCCPOINT("CPipeUser_GetBranchCondition_IF_EXPOSED_TO_TARGET");
                    Vec3 pos = GetPos();
                    SAIBodyInfo bi = new SAIBodyInfo();
                    if (GetProxy() != null && GetProxy().QueryBodyInfo(new SAIBodyInfoQuery(EStance.STANCE_CROUCH, 0.0f, 0.0f, false), bi))
                        pos = bi.vEyePos;

                    Vec3 dir = pAttentionTarget.GetPos() - pos;
                    dir.SetLength(Goal.parms.fValue);
                    float dist = 0;
                    Vec3 hitPos = new Vec3(0, 0, 0);
                    bool exposed = !AICollision.IntersectSweptSphere(ref hitPos, ref dist, new Lineseg(pos, pos + dir), Goal.parms.fValueAux, CryAISystem.CryCommon.EAICollisionEntities.AICE_ALL);
                    if (exposed)
                        return true;
                }
            }
            break;
        case (int)EBranchConditions.IF_CAN_SHOOT_TARGET_PRONED:
            {
                CCCPOINT("CPipeUser_GetbranchCondition_IF_CAN_SHOOT_TARGET_PRONED");
                CPuppet pPuppet = CastToCPuppet();
                if (pPuppet != null && pPuppet.CanFireInStance(EStance.STANCE_PRONE))
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_CAN_SHOOT_TARGET_CROUCHED:
            {
                CCCPOINT("CPipeUser_GetbranchCondition_IF_CAN_SHOOT_TARGET_CROUCHED");
                CPuppet pPuppet = CastToCPuppet();
                if (pPuppet != null && pPuppet.CanFireInStance(EStance.STANCE_CROUCH))
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_CAN_SHOOT_TARGET_STANDING:
            {
                CCCPOINT("CPipeUser_GetbranchCondition_IF_CAN_SHOOT_TARGET_STANDING");
                CPuppet pPuppet = CastToCPuppet();
                if (pPuppet != null && pPuppet.CanFireInStance(EStance.STANCE_STAND))
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_CAN_SHOOT_TARGET:
            {
                CPuppet pPuppet = CastToCPuppet();
                bool aimOK = GetAimState() != EAimState.AI_AIM_OBSTRUCTED;
                if (pAttentionTarget != null && pPuppet != null && pPuppet.GetFirecommandHandler() != null &&
                    aimOK &&
                    pPuppet.CanAimWithoutObstruction(pAttentionTarget.GetPos()) &&
                    pPuppet.GetFirecommandHandler().ValidateFireDirection(pAttentionTarget.GetPos() - GetFirePos(), false))
                {
                    CCCPOINT("CPipeUser_GetBranchCondition_IF_CAN_SHOOT_TARGET");
                    return true;
                }
            }
            break;
        case (int)EBranchConditions.IF_CAN_MELEE:
            {
                SAIWeaponInfo weaponInfo = new SAIWeaponInfo();
                GetProxy()?.QueryWeaponInfo(weaponInfo);
                if (weaponInfo.canMelee)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_TARGET_DIST_LESS:
        case (int)EBranchConditions.IF_TARGET_DIST_GREATER:
        case (int)EBranchConditions.IF_TARGET_IN_RANGE:
        case (int)EBranchConditions.IF_TARGET_OUT_OF_RANGE:
            {
                CCCPOINT("CPipeUser_GetBranchCondition_IF_TARGET_A");
                if (pAttentionTarget != null)
                {
                    Vec3 vPos = GetPos();
                    Vec3 vTargetPos = pAttentionTarget.GetPos();
                    float fDist = Distance.Point_Point(vPos, vTargetPos);
                    if (branchType == (int)EBranchConditions.IF_TARGET_DIST_LESS)
                        return (fDist <= Goal.parms.fValue);
                    if (branchType == (int)EBranchConditions.IF_TARGET_DIST_GREATER)
                        return (fDist > Goal.parms.fValue);
                    if (branchType == (int)EBranchConditions.IF_TARGET_IN_RANGE)
                        return (fDist <= m_Parameters.m_fAttackRange);
                    if (branchType == (int)EBranchConditions.IF_TARGET_OUT_OF_RANGE)
                        return (fDist > m_Parameters.m_fAttackRange);
                }
            }
            break;
        case (int)EBranchConditions.IF_TARGET_MOVED_SINCE_START:
        case (int)EBranchConditions.IF_TARGET_MOVED:
            {
                CGoalPipe pLastPipe = m_pCurrentGoalPipe?.GetLastSubpipe();
                if (pLastPipe == null) // this should NEVER happen
                {
                    AILog.AIError("CPipeUser::ProcessBranchGoal can get pipe. User: {0}", GetName());
                    return true;
                }

                if (pAttentionTarget != null)
                {
                    CCCPOINT("CPipeUser_GetBranchCondition_IF_TARGET_MOVED");
                    bool ret = Distance.Point_Point(pLastPipe.GetAttTargetPosAtStart(), pAttentionTarget.GetPos()) > Goal.parms.fValue;
                    if (branchType == (int)EBranchConditions.IF_TARGET_MOVED)
                        pLastPipe.SetAttTargetPosAtStart(pAttentionTarget.GetPos());
                    return ret;
                }
            }
            break;
        case (int)EBranchConditions.IF_NO_ENEMY_TARGET:
            {
                CCCPOINT("CPipeUser_GetBranchCondition_IF_NO_ENEMY_TARGET");
                if (pAttentionTarget == null || !IsHostile(pAttentionTarget))
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_PATH_LONGER: // branch if current path is longer than params.fValue
            {
                float dbgPathLength = m_Path.GetPathLength(false);
                if (dbgPathLength > Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_PATH_SHORTER:
            {
                float dbgPathLength = m_Path.GetPathLength(false);
                if (dbgPathLength <= Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_PATH_LONGER_RELATIVE: // branch if current path is longer than (params.fValue) times the distance to destination
            {
                Vec3 pathDest = m_PathDestinationPos;
                float pathLength = m_Path.GetPathLength(false);
                float dist = Distance.Point_Point(GetPos(), pathDest);
                if (pathLength >= dist * Goal.parms.fValue)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_NAV_WAYPOINT_HUMAN: // branch if current navigation graph is waypoint
            {
                int nBuilding = 0;
                IAISystem_ENavigationType navType = gAIEnv.pNavigation != null ? gAIEnv.pNavigation.CheckNavigationType(GetPos(), ref nBuilding, m_movementAbility.pathfindingProperties.navCapMask) : IAISystem_ENavigationType.NAV_UNSET;
                if (navType == IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_NAV_TRIANGULAR: // branch if current navigation graph is triangular
            {
                int nBuilding = 0;
                IAISystem_ENavigationType navType = gAIEnv.pNavigation != null ? gAIEnv.pNavigation.CheckNavigationType(GetPos(), ref nBuilding, m_movementAbility.pathfindingProperties.navCapMask) : IAISystem_ENavigationType.NAV_UNSET;
                if (navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
                    return true;
            }
            break; // Note: C++ missing break here (fall-through), but logically separate
        case (int)EBranchConditions.IF_COVER_COMPROMISED:
        case (int)EBranchConditions.IF_COVER_NOT_COMPROMISED: // jumps if the current cover cannot be used for hiding
            {
                CCCPOINT("CPipeUser_GetBranchCondition_IF_COVER_COMPROMISED");
                bool bCompromised = true;
                if (pAttentionTarget != null)
                    bCompromised = m_CurrentHideObject.IsCompromised(this, pAttentionTarget.GetPos());

                bool bResult = bCompromised;
                if (branchType == (int)EBranchConditions.IF_COVER_NOT_COMPROMISED)
                    bResult = !bResult;

                if (bResult)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_COVER_FIRE_ENABLED: // branch if cover firemode is enabled
            {
                CPuppet pPuppet = CastToCPuppet();
                if (pPuppet != null)
                    return !pPuppet.IsCoverFireEnabled();
            }
            break;
        case (int)EBranchConditions.IF_COVER_SOFT:
            {
                bool isEmptyCover = m_CurrentHideObject.IsCoverPathComplete() && m_CurrentHideObject.GetCoverWidth(true) < 0.1f;
                bool soft = !m_CurrentHideObject.IsObjectCollidable() || isEmptyCover;
                if (soft)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_COVER_NOT_SOFT:
            {
                bool isEmptyCover = m_CurrentHideObject.IsCoverPathComplete() && m_CurrentHideObject.GetCoverWidth(true) < 0.1f;
                bool soft = !m_CurrentHideObject.IsObjectCollidable() || isEmptyCover;
                if (soft)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_LASTOP_FAILED:
            {
                if (m_pCurrentGoalPipe != null && m_pCurrentGoalPipe.GetLastResult() == EGoalOpResult.eGOR_FAILED)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_LASTOP_SUCCEED:
            {
                if (m_pCurrentGoalPipe != null && m_pCurrentGoalPipe.GetLastResult() == EGoalOpResult.eGOR_SUCCEEDED)
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_LASTOP_DIST_LESS:
            {
                CAIObject pLastOpResult = m_refLastOpResult.GetAIObject();
                if (pLastOpResult != null && (Distance.Point_Point(GetPos(), pLastOpResult.GetPos()) < Goal.parms.fValue))
                    return true;
            }
            break;
        case (int)EBranchConditions.IF_LASTOP_DIST_LESS_ALONG_PATH:
            {
                CAIObject pLastOpResult = m_refLastOpResult.GetAIObject();
                if (pLastOpResult != null && (m_nPathDecision == (int)EPathfinderResult.PATHFINDER_PATHFOUND))
                {
                    CPuppet pPuppet = CastToCPuppet();
                    if (pPuppet != null)
                    {
                        Vec3 vMyPos = GetPos();
                        float dist = m_movementAbility.b3DMove ? Distance.Point_Point(vMyPos, pLastOpResult.GetPos())
                                                                : Distance.Point_Point2D(vMyPos, pLastOpResult.GetPos());
                        float distPath = pPuppet.GetDistanceAlongPath(pLastOpResult.GetPos(), true);
                        if (dist < distPath)
                            dist = distPath;
                        if (dist < Goal.parms.fValue)
                            return true;
                    }
                }
            }
            break;
        case (int)EBranchConditions.BRANCH_ALWAYS: // branches always
            return true;
        case (int)EBranchConditions.IF_ACTIVE_GOALS_HIDE:
            if (m_vActiveGoals.Count > 0 || !m_CurrentHideObject.IsValid())
                return true;
            break;
        default: //IF_ACTIVE_GOALS
            if (m_vActiveGoals.Count > 0)
                return true;
            break;
        }

        return false;
    }
    protected virtual IPathFollower CreatePathFollower(PathFollowerParams parameters) { if (gAIEnv.CVars.UseSmartPathFollower == 1) return new CSmartPathFollower(parameters, m_pathAdjustmentObstacles); return new CPathFollower(parameters); }
    protected void HandleNavSOFailure() { if ((m_eNavSOMethod == ENavSOMethod.nSOmSignalAnimation || m_eNavSOMethod == ENavSOMethod.nSOmActionAnimation) && m_State.curActorTargetPhase == EActorTargetPhase.eATP_Error) { if (!m_currentNavSOStates.IsEmpty()) { gAIEnv.pSmartObjectManager?.ModifySmartObjectStates(GetEntity(), m_currentNavSOStates.sAnimationFailUserStates); m_currentNavSOStates.Clear(); } if (!m_pendingNavSOStates.IsEmpty()) { gAIEnv.pSmartObjectManager?.ModifySmartObjectStates(GetEntity(), m_pendingNavSOStates.sAnimationFailUserStates); m_pendingNavSOStates.Clear(); } m_eNavSOMethod = ENavSOMethod.nSOmNone; } }
    protected void SyncActorTargetPhaseWithAIProxy() { bool animStarted = false; switch (m_State.curActorTargetPhase) { case EActorTargetPhase.eATP_Started: animStarted = true; break; case EActorTargetPhase.eATP_StartedAndFinished: animStarted = true; m_State.actorTargetReq.Reset(); break; case EActorTargetPhase.eATP_Finished: m_State.curActorTargetPhase = EActorTargetPhase.eATP_None; m_State.actorTargetReq.Reset(); break; } if (animStarted && m_eNavSOMethod == ENavSOMethod.nSOmNone) { CGoalPipe pCurrent = m_pCurrentGoalPipe; if (pCurrent != null) { while (pCurrent.GetSubpipe() != null) pCurrent = pCurrent.GetSubpipe(); NotifyListeners(pCurrent, EGoalPipeEvent.ePN_AnimStarted); } } }

    // GetStateFromActiveGoals impl — PipeUser.cpp:1677-1895
    private void GetStateFromActiveGoalsImpl(SOBJECTSTATE state)
    {
#if DEBUG
        // Reset the movement reason at each update, it will be setup by the correct goal operation.
        m_DEBUGmovementReason = (int)EMovementReason.AIMORE_UNKNOWN;
#endif

        // FUNCTION_PROFILER(gEnv->pSystem, PROFILE_AI);

        if (m_bFirstUpdate)
        {
            m_bFirstUpdate = false;
            SetLooseAttentionTarget(NILREF);
        }

        if (IsPaused())
            return;

        m_DeferredActiveGoals.Clear();

        m_pipeExecuting = true;

        if (m_pCurrentGoalPipe != null)
        {
            if (m_pCurrentGoalPipe.CountSubpipes() >= 10)
            {
                AILog.AIWarning("{0} has too many ({1}) subpipes. Pipe <{2}>",
                    GetName(), m_pCurrentGoalPipe.CountSubpipes(), m_pCurrentGoalPipe.GetName());
            }

            if (!m_bBlocked) // if goal queue not blocked
            {
                // (MATT) Track whether we will add this to the active goals, executed in subsequent frames {2009/10/14}
                bool doNotExecuteAgain = false;

                QGoal Goal = new QGoal();

                EPopGoalResult pgResult = m_pCurrentGoalPipe.PeekPopGoalResult();
                bool isLoop = m_pCurrentGoalPipe.IsLoop();

                if (pgResult == EPopGoalResult.ePGR_AtEnd)
                {
                    if (isLoop)
                    {
                        m_pCurrentGoalPipe.Reset();

                        ClearActiveGoals();
                        if (GetAttentionTarget() != null)
                            m_pCurrentGoalPipe.SetAttTargetPosAtStart(GetAttentionTarget().GetPos());
                    }
                    else
                    {
                        NotifyListeners(m_pCurrentGoalPipe, EGoalPipeEvent.ePN_Exiting);
                    }
                }

                while ((pgResult = m_pCurrentGoalPipe.PopGoal(ref Goal, this)) == EPopGoalResult.ePGR_Succeed)
                {
                    // Each Process instruction first checks to see if this goal is of the right type, immediately returning false if not
                    bool blocking = false;

                    if (ProcessBranchGoal(Goal, ref blocking))
                    {
                        // Now, having either taken the branch or not:
                        if (blocking)
                        {
                            doNotExecuteAgain = true;
                            break; // blocking - don't execute the next instruction until next frame
                        }
                        continue; // non-blocking - continue with execution immediately
                    }

                    if (ProcessRandomGoal(Goal, ref blocking)) // RandomJump would be a better name
                    {
                        doNotExecuteAgain = blocking;
                        break;
                    }

                    if (ProcessClearGoal(Goal, ref blocking))
                    {
                        doNotExecuteAgain = blocking;
                        break;
                    }

                    EGoalOpResult result = Goal.pGoalOp?.Execute(this) ?? EGoalOpResult.eGOR_FAILED;
                    doNotExecuteAgain = false;

                    if (result == EGoalOpResult.eGOR_IN_PROGRESS)
                    {
                        if (Goal.bBlocking)
                        {
                            break;
                        }
                        else
                        {
                            m_DeferredActiveGoals.Add(Goal);
                        }
                    }
                    else
                    {
                        if (result == EGoalOpResult.eGOR_DONE)
                        {
                            Goal.pGoalOp?.Reset(this);
                        }
                        else
                        {
                            m_pCurrentGoalPipe.SetLastResult(result);
                        }

                        doNotExecuteAgain = (result == EGoalOpResult.eGOR_FAILED) || (result == EGoalOpResult.eGOR_DONE);
                    }
                }

                if (pgResult != EPopGoalResult.ePGR_BreakLoop)
                {
                    if (Goal.pGoalOp == null)
                    {
                        if (isLoop)
                        {
                            m_pCurrentGoalPipe.Reset();
                            ClearActiveGoals();
                            if (GetAttentionTarget() != null)
                                m_pCurrentGoalPipe.SetAttTargetPosAtStart(GetAttentionTarget().GetPos());
                        }
                    }
                    else
                    {
                        if (!doNotExecuteAgain)
                        {
                            //FIXME
                            //todo: remove this, currently happens coz "lookat" never finishes - actor can't rotate sometimes
                            if (m_DeferredActiveGoals.Count < 20)
                            {
                                m_DeferredActiveGoals.Add(Goal);
                            }
                            else
                            {
                                // (MATT) Test if the lookat problem still occurs {2009/10/20}
                                Debug.Assert(false);
                            }
                            m_bBlocked = Goal.bBlocking;
                        }
                    }
                }
            }
        }

        if (m_vActiveGoals.Count >= 10)
        {
            QGoal lastGoal = m_vActiveGoals[m_vActiveGoals.Count - 1];
            AILog.AIWarning("{0} has too many ({1}) active goals. Pipe <{2}>; last goal <{3}>",
                GetName(), m_vActiveGoals.Count,
                m_pCurrentGoalPipe != null ? m_pCurrentGoalPipe.GetName() : "_no_pipe_",
                lastGoal.op == EGoalOperations.eGO_LAST ? lastGoal.sPipeName : CGoalPipe.GetGoalOpName(lastGoal.op));
            Debug.Assert(m_vActiveGoals.Count < 100);
        }

        if (m_vActiveGoals.Count > 0)
        {
            for (int i = 0; i < m_vActiveGoals.Count; i++)
            {
                QGoal Goal = m_vActiveGoals[i];

                m_lastExecutedGoalop = Goal.op;

                /*
                ITimer pTimer = gEnv.pTimer;
                int val = gAIEnv.CVars.ProfileGoals;
                CTimeValue timeLast;
                if (val != 0)
                    timeLast = pTimer.GetAsyncTime();
                */

                EGoalOpResult result = Goal.pGoalOp?.Execute(this) ?? EGoalOpResult.eGOR_FAILED;

                /*if (val != 0)
                {
                    CTimeValue timeCurr = pTimer.GetAsyncTime();
                    float f = (timeCurr - timeLast).GetSeconds();
                    timeLast = timeCurr;
                    string goalName = Goal.op == EGoalOperations.eGO_LAST ? Goal.sPipeName : m_pCurrentGoalPipe.GetGoalOpName(Goal.op);
                    GetAISystem().m_mapDEBUGTimingGOALS[goalName] = f;
                }*/

                if (result != EGoalOpResult.eGOR_IN_PROGRESS)
                {
                    if (Goal.bBlocking)
                    {
                        if (result != EGoalOpResult.eGOR_DONE)
                            m_pCurrentGoalPipe?.SetLastResult(result);
                        m_bBlocked = false;
                    }

                    RemoveActiveGoal(i);
                    if (m_vActiveGoals.Count > 0)
                        --i;
                }
            }
        }

        m_vActiveGoals.AddRange(m_DeferredActiveGoals);

        if (m_bKeepMoving && m_State.vMoveDir.IsZero(0.01f) && !m_vLastMoveDir.IsZero(0.01f))
            m_State.vMoveDir = m_vLastMoveDir;
        else if (!m_State.vMoveDir.IsZero(0.01f))
            m_vLastMoveDir = m_State.vMoveDir;

        Debug.Assert(m_pipeExecuting);
        m_pipeExecuting = false;

        if (!string.IsNullOrEmpty(m_delayedPipeSelection.name))
        {
            DelayedPipeSelection delayed = m_delayedPipeSelection;
            SelectPipe(delayed.mode, delayed.name, delayed.refArgument, delayed.goalPipeId,
                delayed.resetAlways);

            m_delayedPipeSelection = new DelayedPipeSelection();
        }
    }

    // Fields — verbatim from PipeUser.h
    public List<COPWaitSignal> m_listWaitGoalOps = new List<COPWaitSignal>();
    public bool m_bLastNearForbiddenEdge; public bool m_bLastActionSucceed;
    public HashSet<(CSmartObject, SmartObjectHelper, SmartObjectHelper)> m_invalidatedSOLinks = new HashSet<(CSmartObject, SmartObjectHelper, SmartObjectHelper)>();
    public EGoalOperations m_lastExecutedGoalop; public CWeakRef<CAIObject> m_refLastOpResult = new CWeakRef<CAIObject>();
    public Vec3 m_posLookAtSmartObject; public CNavPath m_Path = new CNavPath(); public CNavPath m_OrigPath = new CNavPath();
    public Vec3 m_PathDestinationPos; public bool m_bPathfinderConsidersPathTargetDirection; public float m_fTimePassed;
    public CWeakRef<CAIObject> m_refPathFindTarget = new CWeakRef<CAIObject>(); public bool m_bLooseAttention;
    public CWeakRef<CAIObject> m_refLooseAttentionTarget = new CWeakRef<CAIObject>(); public bool m_bPriorityLookAtRequested;
    public CAIHideObject m_CurrentHideObject = new CAIHideObject(); public Vec3 m_vLastMoveDir; public bool m_bKeepMoving;
    public int m_nPathDecision; public uint m_queuedPathId; public bool m_IsSteering; public float m_flightSteeringZOffset;
    public ENavSOMethod m_eNavSOMethod; public bool m_navSOEarlyPathRegen; public uint m_idLastUsedSmartObject;
    public SNavSOStates m_currentNavSOStates; public SNavSOStates m_pendingNavSOStates; public int m_actorTargetReqId;
    public enum EMovementReason { AIMORE_UNKNOWN, AIMORE_TRACE, AIMORE_MOVE, AIMORE_MANEUVER, AIMORE_SMARTOBJECT }
    public int m_DEBUGmovementReason = (int)EMovementReason.AIMORE_UNKNOWN;
    public virtual void DebugDrawGoals() { for (int i = 0; i < m_vActiveGoals.Count; i++) m_vActiveGoals[i].pGoalOp?.DebugDraw(this); }
    public void DebugDrawCoverUser() { m_coverUser.DebugDraw(); }
    public SortedDictionary<int, List<(IGoalPipeListener, string)>> m_mapGoalPipeListeners = new SortedDictionary<int, List<(IGoalPipeListener, string)>>();

    protected float m_AttTargetPersistenceTimeout; protected EAITargetThreat m_AttTargetThreat; protected EAITargetThreat m_AttTargetExposureThreat; protected EAITargetType m_AttTargetType;
    protected EFireMode m_fireMode; protected CWeakRef<CAIObject> m_refFireTarget = new CWeakRef<CAIObject>(); protected bool m_fireModeUpdated; protected bool m_outOfAmmoSent; protected bool m_lowAmmoSent; protected bool m_wasReloading;
    protected VectorOGoals m_vActiveGoals = new VectorOGoals(); protected VectorOGoals m_DeferredActiveGoals = new VectorOGoals(); protected bool m_bBlocked; protected bool m_bStartTiming; protected float m_fEngageTime;
    protected CGoalPipe m_pCurrentGoalPipe; protected SortedSet<int> m_notAllowedSubpipes = new SortedSet<int>(); protected bool m_bFirstUpdate; protected int m_looseAttentionId;
    protected IAISystem_ENavigationType m_CurrentNodeNavType; protected EAimState m_aimState; protected float m_spreadFireTime; protected Vec3 m_vBodyTargetDir; protected Vec3 m_vDesiredBodyDirectionAtTarget; protected uint m_movementContext;
    protected string m_pathToFollowName = ""; protected bool m_bPathToFollowIsSpline; protected string m_refShapeName = ""; protected SShape m_refShape; protected Vec3 m_vLastSOExitHelper;
    protected CStrongRef<CAIObject>[] m_refSpecialObjects = InitSpecialObjects();
    private static CStrongRef<CAIObject>[] InitSpecialObjects() { var arr = new CStrongRef<CAIObject>[(int)ESpecialAIObjects.COUNT_AISPECIAL]; for (int i = 0; i < arr.Length; i++) arr[i] = new CStrongRef<CAIObject>(); return arr; }
    protected LinkedList<(float, Vec3)> m_recentUnreachableHideObjects = new LinkedList<(float, Vec3)>(); protected Vec3 m_lastLiveTargetPos; protected float m_timeSinceLastLiveTarget;
    protected IPathFollower m_pPathFollower; protected void CalculatePathObstacles() { m_pathAdjustmentObstacles.CalculateObstaclesAroundActor(this); }
    protected CPathObstacles m_pathAdjustmentObstacles = new CPathObstacles(); protected int m_adjustpath; protected SAIActorTargetRequest m_pActorTargetRequest;
    protected bool m_inCover; protected bool m_movingToCover; protected bool m_movingInCover; protected CoverUser m_coverUser = new CoverUser(); protected CoverID m_regCoverID;
    protected byte m_paused; protected bool m_bEnableUpdateLookTarget; protected List<LookTargetPtr> m_lookTargets = new List<LookTargetPtr>();

    private Vec3 SetPointListToFollowSub(Vec3 a, Vec3 b, Vec3 c, List<Vec3> newPointList, float step) { float ideallength = m_bPathToFollowIsSpline ? 1.0f : 4.0f; Vec3 oa = (a + b) / 2.0f; Vec3 ob = b; Vec3 oc = (b + c) / 2.0f; Vec3 s = ob - oa; Vec3 d = oc - oa; if (s.GetLength() < 0.0001f) return b; Vec3 nl = s * ideallength / s.GetLength(); for (int i = 0; i < 3; i++) { float y, z, len; if (i == 0) { y = s.x; z = d.x; len = nl.x; } else if (i == 1) { y = s.y; z = d.y; len = nl.y; } else { y = s.z; z = d.z; len = nl.z; } float ca2 = z - 2.0f * y; float cb2 = 2.0f * y; float cc2 = -len; float sq2 = cb2 * cb2 - 4.0f * ca2 * cc2; if (sq2 >= 0 && ca2 != 0) { float srt = MathF.Sqrt(sq2); float t1 = (-cb2 + srt) / (2.0f * ca2); float t2 = (-cb2 - srt) / (2.0f * ca2); float t = (t1 > 0 && t1 < step) ? t1 : t2; if (t > 0 && t < step) { for (float u = 0; u < step; u += t) { Vec3 np = 2.0f * u * (1.0f - u) * s + u * u * d; np += oa; newPointList.Add(np); } if (newPointList.Count > 0) { Vec3 ret = newPointList[newPointList.Count - 1]; ret = (ret - c) * 2.0f + c; newPointList.RemoveAt(newPointList.Count - 1); return ret; } } } } newPointList.Add(oa); return b; }

    private uint m_pendingSmartObjectId; private CStrongRef<CAIObject> m_refRefPoint = new CStrongRef<CAIObject>(); private CStrongRef<CAIObject> m_refLookAtTarget = new CStrongRef<CAIObject>();
    private bool m_adjustingAim; private Dictionary<CoverID, float> m_coverBlacklist = new Dictionary<CoverID, float>();
    private struct CoverUsageInfoState { public CoverUsageInfoState(int dummy = 0) { state = AsyncState.AsyncReady; rayCount = 0; result = new CoverUsageInfo(false); rayID = new QueuedRayID[6]; } public void Reset() { state = AsyncState.AsyncReady; rayCount = 0; result = new CoverUsageInfo(false); } public QueuedRayID[] rayID; public AsyncState state; public byte rayCount; public CoverUsageInfo result; }
    private CoverUsageInfoState m_coverUsageInfoState = new CoverUsageInfoState();
    private struct DelayedPipeSelection { public DelayedPipeSelection(int dummy = 0) { mode = 0; name = ""; refArgument = new CWeakRef<CAIObject>(type_nil_ref.NILREF); goalPipeId = -1; resetAlways = false; } public DelayedPipeSelection(int _mode, string _name, CWeakRef<CAIObject> _refArgument, int _goalPipeId, bool _resetAlways) { mode = _mode; name = _name; refArgument = _refArgument; goalPipeId = _goalPipeId; resetAlways = _resetAlways; } public int mode; public string name; public CWeakRef<CAIObject> refArgument; public int goalPipeId; public bool resetAlways; }
    private DelayedPipeSelection m_delayedPipeSelection = new DelayedPipeSelection(); private bool m_pipeExecuting; private bool m_cutPathAtSmartObject; private bool m_considerActorsAsPathObstacles;
    private PipeUserMovementActorAdapter m_movementActorAdapter; private MovementActorCallbacks m_callbacksForPipeuser = new MovementActorCallbacks();
}

// Forward decls / shells
public class CGoalPipe : IGoalPipe { public CGoalPipe GetLastSubpipe() { CGoalPipe p = this; while (p.GetSubpipe() != null) p = p.GetSubpipe(); return p; } public string GetName() { return m_sName; } public string GetNameAsString() { return m_sName; } public string GetDebugName() { return ""; } public void ParseParams(GoalParams param) { } public int GetEventId() { return m_nEventId; } public void SetEventId(int id) { m_nEventId = id; } public CWeakRef<CAIObject> GetRefArgument() { return m_refArgument; } public void SetRefArgument(CWeakRef<CAIObject> r) { m_refArgument = r; } public void SetLoop(bool l) { m_bLoop = l; } public bool IsLoop() { return m_bLoop; } public bool IsHighPriority() { return m_bHighPriority; } public void HighPriority() { m_bHighPriority = true; } public CGoalPipe GetSubpipe() { return m_pSubPipe; } public void SetSubpipe(CGoalPipe p) { m_pSubPipe = p; } public bool IsInSubpipe() { return m_pSubPipe != null; } public int CountSubpipes() { int c = 0; CGoalPipe p = m_pSubPipe; while (p != null) { c++; p = p.GetSubpipe(); } return c; } public void Reset() { m_nCurrentIndex = 0; } public void ResetGoalops(CPipeUser u) { } public void SetAttTargetPosAtStart(Vec3 pos) { m_vAttTargetPosAtStart = pos; } public Vec3 GetAttTargetPosAtStart() { return m_vAttTargetPosAtStart; } public EPopGoalResult PeekPopGoalResult() { return m_nCurrentIndex >= m_qGoalPipe.Count ? EPopGoalResult.ePGR_AtEnd : EPopGoalResult.ePGR_Succeed; } public EPopGoalResult PopGoal(ref QGoal goal, CPipeUser pUser) { if (m_nCurrentIndex >= m_qGoalPipe.Count) return EPopGoalResult.ePGR_AtEnd; goal = m_qGoalPipe[m_nCurrentIndex++]; return EPopGoalResult.ePGR_Succeed; } public bool Jump(int offset) { m_nCurrentIndex += offset; return true; } public bool Jump(string label) { return true; } public EGoalOpResult GetLastResult() { return m_lastResult; } public void SetLastResult(EGoalOpResult r) { m_lastResult = r; } public void ReExecuteGroup() { if (m_nCurrentIndex > 0) m_nCurrentIndex--; } public bool RemoveSubpipe(CPipeUser u, int gpId, bool keep, bool notify) { return false; } public static string GetGoalOpName(EGoalOperations op) { return op.ToString(); } public bool m_bKeepOnTop; private string m_sName = ""; private int m_nEventId; private CWeakRef<CAIObject> m_refArgument = new CWeakRef<CAIObject>(); private bool m_bLoop; private bool m_bHighPriority; private CGoalPipe m_pSubPipe; private int m_nCurrentIndex; private List<QGoal> m_qGoalPipe = new List<QGoal>(); private Vec3 m_vAttTargetPosAtStart; private EGoalOpResult m_lastResult; }
public class CSmartObject { public Vec3 GetPos() { return new Vec3(0, 0, 0); } public Vec3 GetHelperPos(SmartObjectHelper helper) { return new Vec3(0, 0, 0); } public uint GetEntityId() { return 0; } } public class SmartObjectHelper { }
public enum EGoalOperations { eGO_NONE, eGO_BRANCH, eGO_RANDOM, eGO_CLEAR, eGO_WAIT, eGO_LAST }
public partial class CNavPath : INavPath { public void Clear(string dbg) { m_pts.Clear(); } public bool Empty() { return m_pts.Count == 0; } public int GetVersion() { return m_ver; } public void SetVersion(int v) { m_ver = v; } public void PushBack(PathPointDescriptor p, bool f = false) { m_pts.Add(p); } public void PushFront(PathPointDescriptor p, bool f = false) { m_pts.Insert(0, p); } public float GetPathLength(bool b2D) { return 0; } public SNavPathParams GetParams() { return m_params; } public void SetParams(SNavPathParams p) { m_params = p; } public PathPointDescriptor GetPrevPathPoint() { return m_pts.Count > 0 ? m_pts[0] : null; } public PathPointDescriptor GetNextPathPoint() { return m_pts.Count > 1 ? m_pts[1] : null; } public Vec3 GetLastPathPos(Vec3 def = default) { return m_pts.Count > 0 ? m_pts[m_pts.Count - 1].vPos : new Vec3(0,0,0); } public Vec3 GetNextPathPos(Vec3 def = default) { var p = GetNextPathPoint(); return p != null ? p.vPos : new Vec3(0,0,0); } public TPathPoints GetPath() { return m_pts; } public PathPointDescriptor.SmartObjectNavDataPtr GetLastPathPointAnimNavSOData() { return null; } public void TrimPath(float l, bool t) { } public void PrepareNavigationalSmartObjectsForMNM(IAIPathAgent a) { } public void Serialize(TSerialize s) { } public bool AdjustPathAroundObstacles(CPathObstacles o, uint m) { return true; } public bool AdjustPathAroundObstacles(CPathObstacles o, NavCapMask m) { return AdjustPathAroundObstacles(o, (uint)m); } public float GetDistToPath(out Vec3 pathPosOut, out float distAlongPath, Vec3 pos, float dist, bool twoD) { pathPosOut = pos; distAlongPath = 0; return -1; } public bool GetPosAlongPath(out Vec3 posOut, float d = 0, bool b = false, bool e = false) { posOut = new Vec3(0,0,0); return false; } public ETriState CanTargetPointBeReached(CTargetPointRequest r, CAIActor a, bool t) { return ETriState.eTS_maybe; } public bool UseTargetPointRequest(CTargetPointRequest r, CAIActor a, bool t) { return false; } public void CopyTo(INavPath r) { } public bool CalculateTargetPos(out Vec3 pos, Vec3 curPos, float lookAhead, bool twoD, IAIPathAgent pAgent) { pos = GetLastPathPos(); return true; } public void UpdatePathPosition(Vec3 pos, float dist, bool twoD) { } private TPathPoints m_pts = new TPathPoints(); private SNavPathParams m_params; private int m_ver = -1; }
public interface INavPath
{
    void CopyTo(INavPath other) { /* default no-op */ }
    uint GetMeshID() { return 0; }
    uint GetVersion() { return 0; }
    // Added for Movement system (Phase 4) -- CNavPath implements these
    TPathPoints GetPath() { return new TPathPoints(); }
    SNavPathParams GetParams() { return new SNavPathParams(); }
}
public enum EFireMode { FIREMODE_OFF, FIREMODE_BURST, FIREMODE_CONTINUOUS, FIREMODE_FORCED, FIREMODE_AIM, FIREMODE_SECONDARY, FIREMODE_SECONDARY_SMOKE, FIREMODE_MELEE, FIREMODE_KILL, FIREMODE_BURST_WHILE_MOVING, FIREMODE_PANIC_SPREAD, FIREMODE_BURST_DRAWFIRE, FIREMODE_MELEE_FORCED, FIREMODE_BURST_SNIPE, FIREMODE_AIM_SWEEP, FIREMODE_BURST_ONCE }
public enum ELookStyle { LOOKSTYLE_DEFAULT, LOOKSTYLE_HARD, LOOKSTYLE_SOFT, LOOKSTYLE_HARD_NOLOWER, LOOKSTYLE_SOFT_NOLOWER }
public class LookTargetPtr { }
public class SAIActorTargetRequest { public Vec3 approachLocation; public Vec3 approachDirection; public string animation = ""; public string vehicleName = ""; public int vehicleSeat; public int id; public bool lowerPrecision; public void Serialize(TSerialize ser) { } public void Reset() { id = 0; lowerPrecision = false; approachLocation = new Vec3(0,0,0); approachDirection = new Vec3(0,0,0); animation = ""; vehicleName = ""; vehicleSeat = 0; } }
public enum ENavSOMethod { nSOmNone, nSOmSignalAnimation, nSOmActionAnimation, nSOmStraight, nSOmRoundOnly, nSOmLast }
public enum CoverHeight { LowCover, HighCover }
public partial class CAIHideObject { public HideSmartObjectData m_HideSmartObject = new HideSmartObjectData(); public bool IsValid() { return false; } public bool IsCompromised(CAIObject o, Vec3 p) { return false; } public Vec3 GetObjectPos() { return new Vec3(0,0,0); } public Vec3 GetLastHidePos() { return new Vec3(0,0,0); } public void Update(CPipeUser u) { } public bool HasLowCover() { return false; } public bool HasHighCover() { return false; } public void Serialize(TSerialize s) { } public void Set(SHideSpot hs) { }
    // Methods needed by GetBranchCondition / GetCoverUsageInfo — delegate to CAIHideObjectReal pattern
    public bool IsCoverPathComplete() { return false; }
    public float GetCoverWidth(bool useLowCover) { return 0.0f; }
    public bool IsObjectCollidable() { return false; }
    public void GetCoverDistances(bool useLowCover, Vec3 target, out bool coverCompromised, out float leftEdge, out float rightEdge, out float leftUmbra, out float rightUmbra) { coverCompromised = true; leftEdge = 0; rightEdge = 0; leftUmbra = 0; rightUmbra = 0; }
    public Vec3 GetPointAlongCoverPath(float distance) { return new Vec3(0,0,0); }
    public bool IsLeftEdgeValid(bool useLowCover) { return false; }
    public bool IsRightEdgeValid(bool useLowCover) { return false; }
    public class HideSmartObjectData { public object pChainedUserEvent; public object pChainedObjectEvent; public void Clear() { pChainedUserEvent = null; pChainedObjectEvent = null; } } }
public class CPathObstacles : IPathObstacles { public void Reset() { } public void CalculateObstaclesAroundActor(CAIActor a) { } public bool IsPointInsideObstacles(Vec3 pt) { return false; } public bool IsLineSegmentIntersectingObstaclesOrCloseToThem(Lineseg linesegToTest, float maxDistanceToConsiderClose) { return false; } public bool IsPathIntersectingObstacles(NavigationMeshID meshID, Vec3 start, Vec3 end, float radius) { return false; } public void CalculateObstaclesAroundLocation(Vec3 pos, float range) { } public void CalculateObstaclesAroundLocation(Vec3 pos, AgentMovementAbility ability, CNavPath path) { } public void CalculateObstaclesAroundLocation(Vec3 pos, AgentMovementAbility ability, INavPath path) { } public List<CPathObstacleReal> GetCombinedObstacles() { return new List<CPathObstacleReal>(); } public Vec3 GetPointOutsideObstacles(Vec3 pos, float safeDistance = 0.0f) { return pos; } }
public class COPWaitSignal { public bool NotifySignalReceived(CPipeUser u, string s, object d) { return false; } }
public interface IGoalPipeListener { void OnGoalPipeEvent(CPipeUser u, EGoalPipeEvent e, int gpId, ref bool unreg); }
public enum EGoalPipeEvent { ePN_None, ePN_Deselected, ePN_Removed, ePN_Resumed, ePN_Suspended, ePN_Inserted, ePN_Exiting, ePN_RefPointMoved, ePN_AnimStarted }
public class CoverUser { public struct Params { public float distanceToCover; public float inCoverRadius; public uint userID; } public void SetParams(Params p) { } public void SetCoverID(CoverID id) { m_coverID = id; } public CoverID GetCoverID() { return m_coverID; } public float GetLocationEffectiveHeight() { return 0; } public Vec3 GetCoverLocation() { return new Vec3(0,0,0); } public Vec3 GetCoverNormal() { return new Vec3(0,0,1); } public bool IsCompromised() { return false; } public bool IsFarFromCoverLocation() { return false; } public void UpdateWhileMoving(float dt, Vec3 p, Vec3[] e, uint ec) { } public void Update(float dt, Vec3 p, Vec3[] e, uint ec, float h) { } public void UpdateNormal(Vec3 p) { } public float CalculateEffectiveHeightAt(Vec3 p, Vec3[] e, uint ec) { return 0; } public void Reset() { m_coverID = new CoverID(); } public void DebugDraw() { } private CoverID m_coverID; }
public struct SNavSOStates { public uint objectEntId; public string sAnimationDoneUserStates; public string sAnimationDoneObjectStates; public string sAnimationFailUserStates; public string sAnimationFailObjectStates; public bool IsEmpty() { return string.IsNullOrEmpty(sAnimationDoneUserStates) && string.IsNullOrEmpty(sAnimationFailUserStates); } public void Clear() { objectEntId = 0; sAnimationDoneUserStates = null; sAnimationDoneObjectStates = null; sAnimationFailUserStates = null; sAnimationFailObjectStates = null; } public void Serialize(TSerialize s) { } }
public class QGoal { public EGoalOperations op; public bool bBlocking; public IGoalPipe_EGroupType eGrouping; public string sPipeName = ""; public GoalOpParams parms = new GoalOpParams(); public IGoalOp pGoalOp; }
public class GoalOpParams { public int nValue; public int nValueAux; public float fValue; public float fValueAux; public string str = ""; }
public interface IGoalOp { EGoalOpResult Execute(CPipeUser u); void ExecuteDry(CPipeUser u); void Reset(CPipeUser u); void DebugDraw(CPipeUser u); }
public enum EGoalOpResult { eGOR_NONE, eGOR_IN_PROGRESS, eGOR_SUCCEEDED, eGOR_FAILED, eGOR_DONE }
public enum EPopGoalResult { ePGR_Succeed, ePGR_AtEnd, ePGR_BreakLoop }
public class VectorOGoals : List<QGoal> { }
public class PipeUserMovementActorAdapter : IMovementActorAdapter
{
    private CPipeUser m_pipeUser;
    public PipeUserMovementActorAdapter() { }
    public PipeUserMovementActorAdapter(CPipeUser u) { m_pipeUser = u; }
    // IMovementActorAdapter — default stub implementations
    public void OnMovementPlanProduced() { }
    public Vec3 GetPhysicsPosition() { return m_pipeUser?.GetPhysicsPos() ?? new Vec3(0,0,0); }
    public Vec3 GetVelocity() { return m_pipeUser?.GetVelocity() ?? new Vec3(0,0,0); }
    public Vec3 GetMoveDirection() { return new Vec3(0,1,0); }
    public Vec3 GetAnimationBodyDirection() { return new Vec3(0,1,0); }
    public EActorTargetPhase GetActorPhase() { return m_pipeUser?.m_State.curActorTargetPhase ?? EActorTargetPhase.eATP_None; }
    public void SetMovementOutputValue(PathFollowResult result) { }
    public void SetBodyTargetDirection(Vec3 direction) { }
    public void ResetMovementContext() { }
    public void ClearMovementState() { }
    public void ResetBodyTarget() { }
    public void ResetActorTargetRequest() { }
    public bool IsMoving() { return false; }
    public void RequestExactPosition(SAIActorTargetRequest request, bool lowerPrecision) { }
    public bool IsClosestToUseTheSmartObject(CryAISystem.Navigation.MNM.OffMeshLink_SmartObject smartObjectLink) { return true; }
    public bool PrepareNavigateSmartObject(CSmartObject pSmartObject, CryAISystem.Navigation.MNM.OffMeshLink_SmartObject pSmartObjectLink) { return false; }
    public void InvalidateSmartObjectLink(CSmartObject pSmartObject, CryAISystem.Navigation.MNM.OffMeshLink_SmartObject pSmartObjectLink) { }
    public void SetInCover(bool inCover) { m_pipeUser?.SetInCover(inCover); }
    public void UpdateCoverLocations() { }
    public void InstallInLowCover(bool inCover) { }
    public void SetupCoverInformation() { }
    public bool IsInCover() { return m_pipeUser?.IsInCover() ?? false; }
    public bool GetDesignedPath(SShape pathShape) { return false; }
    public void CancelRequestedPath() { }
    public void ConfigurePathfollower(MovementStyle style) { }
    public void SetActorPath(MovementStyle style, CNavPath navPath) { }
    public void SetActorStyle(MovementStyle style, CNavPath navPath) { }
    public void SetStance(MovementStyle.Stance stance) { }
    public Vec3 CreateLookTarget() { return new Vec3(0,0,0); }
    public void SetLookTimeOffset(float lookTimeOffset) { }
    public void UpdateLooking(float updateTime, Vec3 lookTarget, bool targetReachable, float pathDistanceToEnd, Vec3 followTargetPosition, MovementStyle style) { }
}
public class MovementActorCallbacks { public Action<MNMPathRequest> queuePathRequestFunction; public Func<Movement_PathfinderState> checkOnPathfinderStateFunction; public Func<IPathFollower> getPathFollowerFunction; public Func<INavPath> getPathFunction; }
public enum IAISystem_ENavigationType : uint { NAV_UNSET = 0, NAV_TRIANGULAR = 1, NAV_WAYPOINT_HUMAN = 2, NAV_WAYPOINT_3DSURFACE = 4, NAV_FLIGHT = 8, NAV_VOLUME = 16, NAV_ROAD = 32, NAV_SMARTOBJECT = 64, NAV_FREE_2D = 128, NAV_CUSTOM_NAVIGATION = 256, NAV_MAX_VALUE = NAV_CUSTOM_NAVIGATION }
public enum Movement_PathfinderState { StillFinding, FoundPath, CouldNotFindPath, Canceled }
public struct QueuedPathID { public uint id; public static implicit operator uint(QueuedPathID q) => q.id; public static implicit operator QueuedPathID(uint v) => new QueuedPathID { id = v }; }
public class MNMPathRequest { public Vec3 startPos; public Vec3 endPos; public Vec3 endDir; public int forceTargetBuildingId; public float endTol; public float endDistance; public bool allowDangerousDestination; public Action<QueuedPathID, MNMPathRequestResult> resultCallback; public NavigationAgentTypeID agentTypeID; public MNMDangersFlags dangersFlags; public MNMPathRequest() { } public MNMPathRequest(Vec3 s, Vec3 e, Vec3 d, int b, float t, float dist, bool dng, Action<QueuedPathID, MNMPathRequestResult> cb, NavigationAgentTypeID tid, MNMDangersFlags f) { startPos = s; endPos = e; endDir = d; forceTargetBuildingId = b; endTol = t; endDistance = dist; allowDangerousDestination = dng; resultCallback = cb; agentTypeID = tid; dangersFlags = f; } }
// Types already defined in other files are not duplicated here.
// MNMPathRequestResult -> AIActor.cs | MNMDangersFlags, IGoalPipe_EGroupType, EGoalPipeFlags, EBranchConditions, EBranchConditionFlags -> new here
public enum MNMDangersFlags { eMNMDangers_None = 0, eMNMDangers_Explosive = 1, eMNMDangers_AttentionTarget = 2 }
public enum IGoalPipe_EGroupType { eGT_NOGROUP, eGT_GROUPED, eGT_GROUPWITHPREV }
public enum EGoalPipeFlags { AIGOALPIPE_RUN_ONCE = 1, AIGOALPIPE_NOTDUPLICATE = 2, AIGOALPIPE_HIGHPRIORITY = 4, AIGOALPIPE_SAMEPRIORITY = 8, AIGOALPIPE_DONT_RESET_AG = 16, AIGOALPIPE_KEEP_ON_TOP = 32 }
public enum EBranchConditions { IF_RANDOM = 1, IF_NO_PATH, IF_PATH_STILL_FINDING, IF_IS_HIDDEN, IF_CAN_HIDE, IF_CANNOT_HIDE, IF_STANCE_IS, IF_HAS_FIRED, IF_FIRE_IS, IF_NO_LASTOP, IF_SEES_LASTOP, IF_SEES_TARGET, IF_TARGET_LOST_TIME_MORE, IF_TARGET_LOST_TIME_LESS, IF_EXPOSED_TO_TARGET, IF_CAN_SHOOT_TARGET_PRONED, IF_CAN_SHOOT_TARGET_CROUCHED, IF_CAN_SHOOT_TARGET_STANDING, IF_CAN_SHOOT_TARGET, IF_CAN_MELEE, IF_TARGET_DIST_LESS, IF_TARGET_DIST_GREATER, IF_TARGET_IN_RANGE, IF_TARGET_OUT_OF_RANGE, IF_TARGET_MOVED_SINCE_START, IF_TARGET_MOVED, IF_NO_ENEMY_TARGET, IF_PATH_LONGER, IF_PATH_SHORTER, IF_PATH_LONGER_RELATIVE, IF_NAV_WAYPOINT_HUMAN, IF_NAV_TRIANGULAR, IF_COVER_COMPROMISED, IF_COVER_NOT_COMPROMISED, IF_COVER_FIRE_ENABLED, IF_COVER_SOFT, IF_COVER_NOT_SOFT, IF_LASTOP_FAILED, IF_LASTOP_SUCCEED, IF_LASTOP_DIST_LESS, IF_LASTOP_DIST_LESS_ALONG_PATH, BRANCH_ALWAYS, IF_ACTIVE_GOALS_HIDE }
public enum EBranchConditionFlags { NOT = 0x10000 }
// PathFollowerParams — was previously in AIVehicle.cs, centralized here
public class PathFollowerParams
{
    public uint navCapMask;
    public float passRadius;
    public float pathRadius;
    public bool stopAtEnd;
    public bool use2D;
    public float endAccuracy;
    public float pathLookAheadDist;
    public float maxSpeed;
    public bool isVehicle;
    // Added for SmartPathFollower.cpp / PathFollower.cpp literal port
    public float endDistance;
    public bool isAllowedToShortcut;
    public float maxAccel;
    public float maxDecel;
    public float minSpeed;
    public float normalSpeed;
}

// Extension methods for types defined in other files that we can't modify
public static class MNMPathRequestResultExtensions
{
    public static bool HasPathBeenFoundEx(this MNMPathRequestResult r) { return r?.GetNavPath() != null; }
    public static INavPath GetNavPath(this MNMPathRequestResult r) { return null; /* extended by Phase 3 MNM path finder */ }
}

public static class IPathFollowerExtensions
{
    public static void SetParams(this IPathFollower f, PathFollowerParams p) { /* base interface is empty — CPathFollower overrides */ }
    public static void AttachToPath(this IPathFollower f, object path) { }
    public static Vec3 GetPathPointAhead(this IPathFollower f, float dist, out float junk) { junk = 0; return new Vec3(0,0,0); }
    public static void Serialize(this IPathFollower f, TSerialize ser) { /* serialization deferred — base interface is empty */ }
}
