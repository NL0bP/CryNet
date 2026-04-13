// Literal port of dev/Code/CryEngine/CryAISystem/AIActor.h + AIActor.cpp (2628L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.
// Description : CAIActor class — base class for all AI agents.

using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.AISignalConstants;
using static CryAISystem.AIPhysConstants;
using static CryAISystem.WalkabilityConstants;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.NilRefHelper;
using static CryAISystem.EAIEvent;       // AIEVENT_* constants
using static CryAISystem.EAIObjectType;  // AIOBJECT_* constants
using CryAISystem.CryCommon;
using SmartScriptTable = CryAISystem.CryCommon.SmartScriptTable;

namespace CryAISystem;

// Structure reflecting the physical entity parts.
public struct SAIDamagePart
{
    public SAIDamagePart(int dummy = 0)
    {
        pos = new Vec3(0, 0, 0);
        damageMult = 0f;
        volume = 0f;
        surfaceIdx = 0;
    }
    public void GetMemoryUsage(ICrySizer pSizer) { }
    public Vec3 pos;
    public float damageMult;
    public float volume;
    public int surfaceIdx;
}

// typedef std::vector<SAIDamagePart> DamagePartVector;
public class DamagePartVector : List<SAIDamagePart> { }
// typedef std::vector<IPerceptionHandlerModifier*> TPerceptionHandlerModifiersVector;
public class TPerceptionHandlerModifiersVector : List<IPerceptionHandlerModifier> { }

/*! Basic ai object class. Defines a framework that all puppets and points of interest later follow. */
public class CAIActor : CAIObject, IAIActor
{
    // friend class CAISystem;

    private const float UNINITIALIZED_COS_CACHE = 2.0f;
    private const string GET_READY_TO_CHANGE_BEHAVIOR_SIGNAL = "OnBehaviorChangeRequest";

    // ===================================================================
    // Construction/Destruction (AIActor.cpp lines 52-107)
    // ===================================================================
    public CAIActor()
    {
        m_bCheckedBody = true;
#if CRYAISYSTEM_DEBUG
        m_healthHistory = null;
#endif
        m_lightLevel = EAILightLevel.AILL_LIGHT;
        m_usingCombatLight = false;
        m_perceptionDisabled = 0;
        m_cachedWaterOcclusionValue = 0.0f;
        m_vLastFullUpdatePos = new Vec3(0, 0, 0);
        m_lastFullUpdateStance = EStance.STANCE_NULL;
        m_observer = false;
        m_bCloseContact = false;
        m_FOVPrimaryCos = UNINITIALIZED_COS_CACHE;
        m_FOVSecondaryCos = UNINITIALIZED_COS_CACHE;
        m_territoryShape = null;
        m_lastBodyDir = new Vec3(0, 0, 0);
        m_bodyTurningSpeed = 0;
        m_stimulusStartTime = -100.0f;
        m_activeCoordinationCount = 0;
        m_navigationTypeID = new NavigationAgentTypeID();
        m_behaviorTreeEvaluationMode = BehaviorTreeEvaluationMode.EvaluateWhenVariablesChange;
        m_currentCollisionAvoidanceRadiusIncrement = 0.0f;
        m_runningBehaviorTree = false;

        _fastcast_CAIActor = true;

        AILog.AILogComment("CAIActor ({0})", this);
    }

    // virtual ~CAIActor() (AIActor.cpp lines 83-107)
    public void Destructor()
    {
        StopBehaviorTree();

        AILog.AILogComment("~CAIActor  {0} ({1})", GetName(), this);

        gAIEnv.pGroupManager.RemoveGroupMember(GetGroupId(), GetAIObjectID());

        CAISystem pAISystem = GlobalFunctions.GetAISystem();

        CAIGroup pGroup = pAISystem.GetAIGroup(GetGroupId());
        if (pGroup != null)
            pGroup.RemoveMember(this);

        SetObserver(false);

#if CRYAISYSTEM_DEBUG
        m_healthHistory = null; // delete m_healthHistory
#endif

        m_State.ClearSignals();

        pAISystem.NotifyEnableState(this, false);
        pAISystem.UnregisterAIActor(StaticCast<CAIActor>(GetSelfReference()));
    }

    public virtual IAIActor CastToIAIActor() { return this; }

    // ===================================================================
    // SetBehaviorVariable (AIActor.cpp lines 109-138)
    // ===================================================================
    public void SetBehaviorVariable(string variableName, bool value)
    {
        if (m_behaviorSelectionTree != null)
        {
            SelectionVariableID variableID =
                m_behaviorSelectionTree.GetTemplate().GetVariableDeclarations().GetVariableID(variableName);
            System.Diagnostics.Debug.Assert(m_behaviorSelectionTree.GetTemplate().GetVariableDeclarations().IsDeclared(variableID));
#if !RELEASE
            if (!m_behaviorSelectionTree.GetTemplate().GetVariableDeclarations().IsDeclared(variableID))
            {
                AILog.AIWarning("Variable \"{0}\" missing from {1}'s Behaviour Selection Tree.", variableName, GetName());
            }
#endif
            m_behaviorSelectionVariables.SetVariable(variableID, value);
        }

        {
            BehaviorTree.Variables.Collection variableCollection = GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.GetBehaviorVariableCollection_Deprecated(GetEntityID());
            BehaviorTree.Variables.Declarations variableDeclarations = GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.GetBehaviorVariableDeclarations_Deprecated(GetEntityID());
            if (variableCollection == null || variableDeclarations == null)
                return;

            BehaviorTree.Variables.VariableID variableID = BehaviorTree.Variables.VariableHelpers.GetVariableID(variableName);

            if (variableDeclarations.IsDeclared(variableID))
                variableCollection.SetVariable(variableID, value);
            else
                AILog.AIWarning("Variable '{0}' missing from {1}'s Behavior Tree.", variableName, GetName());
        }
    }

    // ===================================================================
    // GetBehaviorVariable (AIActor.cpp lines 140-168)
    // ===================================================================
    public bool GetBehaviorVariable(string variableName)
    {
        bool value = false;

        if (m_behaviorSelectionTree != null)
        {
            SelectionVariableID variableID =
                m_behaviorSelectionTree.GetTemplate().GetVariableDeclarations().GetVariableID(variableName);

            m_behaviorSelectionVariables.GetVariable(variableID, ref value);
            return value;
        }

        {
            BehaviorTree.Variables.Collection variableCollection = GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.GetBehaviorVariableCollection_Deprecated(GetEntityID());
            BehaviorTree.Variables.Declarations variableDeclarations = GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.GetBehaviorVariableDeclarations_Deprecated(GetEntityID());
            if (variableCollection == null || variableDeclarations == null)
                return false;

            BehaviorTree.Variables.VariableID variableID = BehaviorTree.Variables.VariableHelpers.GetVariableID(variableName);

            if (variableDeclarations.IsDeclared(variableID))
                variableCollection.GetVariable(variableID, ref value);
            else
                AILog.AIWarning("Variable '{0}' missing from {1}'s Behavior Tree.", variableName, GetName());

            return value;
        }
    }

    public SelectionTree GetBehaviorSelectionTree() { return m_behaviorSelectionTree; }
    public SelectionVariables GetBehaviorSelectionVariables() { return m_behaviorSelectionVariables; }

    // ===================================================================
    // ResetModularBehaviorTree (AIActor.cpp lines 181-206)
    // ===================================================================
    public void ResetModularBehaviorTree(EObjectResetType type)
    {
        if (type == EObjectResetType.AIOBJRESET_SHUTDOWN)
        {
            StopBehaviorTree();
        }
        else
        {
            // Try to load a Modular Behavior Tree
            IScriptTable table = GetEntity()?.GetScriptTable();
            if (table != null)
            {
                SmartScriptTable properties = default;
                if (table.GetValue("Properties", out properties) && properties)
                {
                    string behaviorTreeName = null;

                    if (properties.GetValue("esModularBehaviorTree", out behaviorTreeName) && behaviorTreeName != null && behaviorTreeName.Length > 0)
                    {
                        StartBehaviorTree(behaviorTreeName);
                    }
                }
            }
        }
    }

    // ===================================================================
    // ResetBehaviorSelectionTree (AIActor.cpp lines 208-251)
    // ===================================================================
    public void ResetBehaviorSelectionTree(EObjectResetType type)
    {
        m_behaviorTreeEvaluationMode = BehaviorTreeEvaluationMode.EvaluateWhenVariablesChange;

        bool bRemoveBehaviorSelectionTree = (type == EObjectResetType.AIOBJRESET_SHUTDOWN);
        IAIActorProxy proxy = GetProxy();

        if (!bRemoveBehaviorSelectionTree && proxy != null)
        {
            string behaviorSelectionTreeName = proxy.GetBehaviorSelectionTreeName();

            bool treeChanged = ((behaviorSelectionTreeName != null && m_behaviorSelectionTree == null) ||
                (behaviorSelectionTreeName != null && string.Compare(m_behaviorSelectionTree.GetTemplate().GetName(), behaviorSelectionTreeName, true) != 0));

            if (treeChanged)
            {
                SelectionTreeTemplateID templateID = gAIEnv.pSelectionTreeManager.GetTreeTemplateID(behaviorSelectionTreeName);

                if (gAIEnv.pSelectionTreeManager.HasTreeTemplate(templateID))
                {
                    SelectionTreeTemplate treeTemplate = gAIEnv.pSelectionTreeManager.GetTreeTemplate(templateID);
                    if (treeTemplate.Valid())
                    {
                        m_behaviorSelectionTree = new SelectionTree(treeTemplate.GetSelectionTree());
                        m_behaviorSelectionVariables = new SelectionVariables(treeTemplate.GetVariableDeclarations().GetDefaults());
                        m_behaviorSelectionVariables.ResetChanged(true);
                    }
                }
                else
                {
                    bRemoveBehaviorSelectionTree = true;
                }
            }
        }

        if (bRemoveBehaviorSelectionTree)
        {
            m_behaviorSelectionTree = null;
            m_behaviorSelectionVariables = null;
        }
    }

    // ===================================================================
    // ProcessBehaviorSelectionTreeSignal (AIActor.cpp lines 253-268)
    // ===================================================================
    public bool ProcessBehaviorSelectionTreeSignal(string signalName, uint signalCRC)
    {
        if (m_behaviorSelectionVariables != null)
        {
#if CRYAISYSTEM_DEBUG
            m_behaviorSelectionVariables.DebugTrackSignalHistory(signalName);
#endif

            SelectionTreeTemplate treeTemplate = m_behaviorSelectionTree.GetTemplate();
            return treeTemplate.GetSignalVariables().ProcessSignal(signalName, signalCRC, m_behaviorSelectionVariables);
        }

        return false;
    }

    // ===================================================================
    // UpdateBehaviorSelectionTree (AIActor.cpp lines 270-314)
    // ===================================================================
    public bool UpdateBehaviorSelectionTree()
    {
        if (m_behaviorSelectionTree != null)
        {
            bool evaluateTree =
                m_behaviorSelectionVariables != null &&
                m_behaviorSelectionVariables.Changed() &&
                m_behaviorTreeEvaluationMode == BehaviorTreeEvaluationMode.EvaluateWhenVariablesChange;

            if (evaluateTree)
            {
                string behaviorName = "";

                SelectionNodeID currentNodeID = m_behaviorSelectionTree.GetCurrentNodeID();
                SelectionNodeID selectedNodeID = m_behaviorSelectionTree.Evaluate(m_behaviorSelectionVariables);
                if (selectedNodeID)
                {
                    m_behaviorSelectionVariables.ResetChanged();

                    if (currentNodeID.id == selectedNodeID.id)
                        return false;

                    SelectionTreeNode node = m_behaviorSelectionTree.GetNode(selectedNodeID);
                    behaviorName = node.GetName();

                    SelectionTreeTemplate treeTemplate = m_behaviorSelectionTree.GetTemplate();
                    string translatedName = treeTemplate.GetTranslator().GetTranslation(selectedNodeID);
                    if (translatedName != null)
                        behaviorName = translatedName;
                }

                IAIActorProxy pProxy = GetProxy();
                System.Diagnostics.Debug.Assert(pProxy != null);
                if (pProxy != null)
                    pProxy.SetBehaviour(behaviorName);

                return true;
            }
        }

        return false;
    }

#if CRYAISYSTEM_DEBUG
    // ===================================================================
    // DebugDrawBehaviorSelectionTree (AIActor.cpp lines 320-330)
    // ===================================================================
    public void DebugDrawBehaviorSelectionTree()
    {
        if (m_behaviorSelectionVariables != null)
        {
            SelectionTreeTemplate treeTemplate = m_behaviorSelectionTree.GetTemplate();
            m_behaviorSelectionVariables.DebugDraw(true, treeTemplate.GetVariableDeclarations());
        }

        if (m_behaviorSelectionTree != null)
            m_behaviorSelectionTree.DebugDraw();
    }
#endif

    // ===================================================================
    // QueryBodyInfo / GetBodyInfo (AIActor.cpp lines 335-344)
    // ===================================================================
    public SAIBodyInfo QueryBodyInfo()
    {
        m_proxy?.QueryBodyInfo(m_bodyInfo);
        return m_bodyInfo;
    }
    public SAIBodyInfo GetBodyInfo() { return m_bodyInfo; }

    ////////////////////////////////////////////////////////////////////////////////////////
    //IAIPathAgent//////////////////////////////////////////////////////////////////////////

    // AIActor.cpp lines 1847-1949
    public virtual IEntity GetPathAgentEntity() { return GetEntity(); }
    public virtual string GetPathAgentName() { return GetName(); }
    public virtual ushort GetPathAgentType() { return GetType(); }
    public virtual float GetPathAgentPassRadius() { return GetParameters().m_fPassRadius; }
    public virtual Vec3 GetPathAgentPos() { return GetPhysicsPos(); }
    public virtual Vec3 GetPathAgentVelocity() { return GetVelocity(); }
    public virtual void GetPathAgentNavigationBlockers(NavigationBlockers navigationBlockers, PathfindRequest pRequest) { }

    // AIActor.cpp lines 1886-1894
    public override nuint GetNavNodeIndex()
    {
        if (m_lastNavNodeIndex != 0)
            return (m_lastNavNodeIndex < nuint.MaxValue) ? m_lastNavNodeIndex : 0;

        m_lastNavNodeIndex = nuint.MaxValue;
        return 0;
    }

    public virtual AgentMovementAbility GetPathAgentMovementAbility() { return m_movementAbility; }
    public virtual IPathFollower GetPathFollower() { return null; }

    public virtual uint GetPathAgentLastNavNode() { return (uint)GetNavNodeIndex(); }
    public virtual void SetPathAgentLastNavNode(uint lastNavNode) { m_lastNavNodeIndex = lastNavNode; }

    public virtual void SetPathToFollow(string pathName) { }
    public virtual void SetPathAttributeToFollow(bool bSpline) { }
    public virtual void SetPFBlockerRadius(int blockerType, float radius) { }

    public virtual ETriState CanTargetPointBeReached(CTargetPointRequest request) { request.SetResult(ETriState.eTS_false); return ETriState.eTS_false; }
    public virtual bool UseTargetPointRequest(CTargetPointRequest request) { return false; }

    public virtual bool GetValidPositionNearby(Vec3 proposedPosition, out Vec3 adjustedPosition) { adjustedPosition = proposedPosition; return false; }
    public virtual bool GetTeleportPosition(out Vec3 teleportPos) { teleportPos = new Vec3(0, 0, 0); return false; }

    public virtual bool IsPointValidForAgent(Vec3 pos, uint flags) { return true; }

    //IAIPathAgent//////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////

    //===================================================================
    // SetPos (AIActor.cpp lines 346-395)
    //===================================================================
    public override void SetPos(Vec3 pos, Vec3 dirFwrd = default)
    {
        gAIEnv.pActorLookUp?.Prepare(0);

        Vec3 position = pos;
        Vec3 vEyeDir = dirFwrd;

        IAIActorProxy pProxy = GetProxy();
        if (pProxy != null)
        {
            SAIBodyInfo bodyInfo = new SAIBodyInfo();
            pProxy.QueryBodyInfo(bodyInfo);

            position = bodyInfo.vEyePos;
            vEyeDir = bodyInfo.GetEyeDir();

            SetViewDir(vEyeDir);
            SetBodyDir(bodyInfo.GetBodyDir());

            SetFirePos(bodyInfo.vFirePos);
            SetFireDir(bodyInfo.vFireDir);
            SetMoveDir(bodyInfo.vMoveDir);
            SetEntityDir(bodyInfo.GetBodyDir());
        }

        base.SetPos(position, vEyeDir);

        if (m_pFormation != null)
            m_pFormation.Update();

        if (m_observer)
        {
            ObserverParams observerParams = new ObserverParams();
            observerParams.eyePosition = position;
            observerParams.eyeDirection = vEyeDir;

            gAIEnv.pVisionMap?.ObserverChanged(GetVisionID(), observerParams, eChangedPosition | eChangedOrientation);
        }

        gAIEnv.pActorLookUp?.UpdatePosition(this, position);
    }

    //===================================================================
    // Reset (AIActor.cpp lines 399-505)
    //===================================================================
    public override void Reset(EObjectResetType type)
    {
        base.Reset(type);

        if (type == EObjectResetType.AIOBJRESET_INIT)
        {
            m_initialPosition.pos = GetEntity().GetPos();
            m_initialPosition.isValid = true;
        }

        m_bCheckedBody = true;

        CAISystem pAISystem = GlobalFunctions.GetAISystem();

        m_State.FullReset();

        m_State.eLookStyle = ELookStyle.LOOKSTYLE_DEFAULT;
        m_State.bAllowLowerBodyToTurn = true;

        ReleaseFormation();

        if (m_proxy == null)
        {
            AILog.AILogComment("CAIActor({0}) Creating AIActorProxy", this);
            m_proxy = pAISystem.GetActorProxyFactory()?.CreateActorProxy(GetEntityID());
            gAIEnv.pActorLookUp?.UpdateProxy(this);
        }

        m_proxy?.Reset(type);

        m_bEnabled = true;
        m_bUpdatedOnce = false;

#if AI_COMPILE_WITH_PERSONAL_LOG
        m_personalLog.Clear();
#endif

#if CRYAISYSTEM_DEBUG
        if (m_healthHistory != null)
            m_healthHistory.Reset();
#endif

        // synch self with owner entity if there is one
        IEntity pEntity = GetEntity();
        if (pEntity != null)
        {
            m_bEnabled = pEntity.IsActive();
            SetPos(pEntity.GetPos());
        }

        m_lightLevel = EAILightLevel.AILL_LIGHT;
        m_usingCombatLight = false;
        System.Diagnostics.Debug.Assert(m_perceptionDisabled == 0);
        m_perceptionDisabled = 0;

        m_cachedWaterOcclusionValue = 0.0f;

        m_vLastFullUpdatePos = new Vec3(0, 0, 0);
        m_lastFullUpdateStance = EStance.STANCE_NULL;

        m_probableTargets.Clear();

        m_blackBoard.Clear();
        { var sst = m_blackBoard.GetForScript(); if (sst) sst.SetValue("Owner", GetName()); }

        m_perceptionHandlerModifiers.Clear();

        ResetPersonallyHostiles();
        ResetBehaviorSelectionTree(type);

        ResetModularBehaviorTree(type);

        string navigationTypeName = m_proxy?.GetNavigationTypeName();
        if (navigationTypeName != null && navigationTypeName.Length > 0)
        {
            NavigationAgentTypeID id = gAIEnv.pNavigationSystem?.GetAgentTypeID(navigationTypeName) ?? new NavigationAgentTypeID();
            if (id.id != 0)
            {
                m_navigationTypeID = id;
            }
        }

        pAISystem.NotifyEnableState(this, m_bEnabled && (type == EObjectResetType.AIOBJRESET_INIT));

        // Clear the W/T names we use for lookup
        m_territoryShape = null;
        m_territoryShapeName = "";

        m_lastBodyDir = new Vec3(0, 0, 0);
        m_bodyTurningSpeed = 0;

        if (GetType() != (ushort)AIOBJECT_PLAYER)
        {
            SetObserver(type == EObjectResetType.AIOBJRESET_INIT);
            SetObservable(type == EObjectResetType.AIOBJRESET_INIT);
        }

        m_bCloseContact = false;
        m_stimulusStartTime = -100.0f;

        m_bodyInfo = new SAIBodyInfo();

        m_activeCoordinationCount = 0;

        m_currentCollisionAvoidanceRadiusIncrement = 0.0f;
    }

    // ===================================================================
    // EnablePerception / IsPerceptionEnabled (AIActor.cpp lines 507-521)
    // ===================================================================
    public virtual void EnablePerception(bool enable)
    {
        if (enable)
            --m_perceptionDisabled;
        else
            ++m_perceptionDisabled;

        System.Diagnostics.Debug.Assert(m_perceptionDisabled >= 0);
        System.Diagnostics.Debug.Assert(m_perceptionDisabled < 16);
    }

    public virtual bool IsPerceptionEnabled() { return m_perceptionDisabled <= 0; }

    // ===================================================================
    // ResetPerception (AIActor.cpp lines 523-526)
    // ===================================================================
    public virtual void ResetPerception() { m_probableTargets.Clear(); }

    // ===================================================================
    // ParseParameters (AIActor.cpp lines 530-538)
    // ===================================================================
    public virtual void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true)
    {
        SetParameters(parameters.m_sParamStruct);

        if (bParseMovementParams)
            m_movementAbility = parameters.m_moveAbility;

        GlobalFunctions.GetAISystem().NotifyEnableState(this, m_bEnabled);
    }

    // ===================================================================
    // OnObjectRemoved (AIActor.cpp lines 543-583)
    // ===================================================================
    public override void OnObjectRemoved(CAIObject pObject)
    {
        base.OnObjectRemoved(pObject);

        // make sure no pending signal left from removed AIObjects
        if (m_State.vSignals.Count > 0)
        {
            uint removedEntityID = pObject.GetEntityID();
            if (removedEntityID != 0)
            {
                for (int i = m_State.vSignals.Count - 1; i >= 0; --i)
                {
                    AISIGNAL curSignal = m_State.vSignals[i];
                    if (curSignal.senderID == removedEntityID)
                    {
                        // delete static_cast<AISignalExtraData*>(curSignal.pEData);
                        m_State.vSignals.RemoveAt(i);
                    }
                }
            }
        }

        for (int i = 0; i < m_probableTargets.Count; )
        {
            if (m_probableTargets[i] == pObject)
            {
                m_probableTargets[i] = m_probableTargets[m_probableTargets.Count - 1];
                m_probableTargets.RemoveAt(m_probableTargets.Count - 1);
            }
            else
                ++i;
        }

        RemovePersonallyHostile(pObject.GetAIObjectID());
    }

    // ===================================================================
    // Update (AIActor.cpp lines 588-753)
    // ===================================================================
    public virtual void Update(EObjectUpdate type)
    {
        IAIActorProxy pAIActorProxy = GetProxy();

        if (CastToCPipeUser() == null)
        {
            if (!IsEnabled())
            {
                AILog.AIWarning("CAIActor::Update: Trying to update disabled AI Actor: {0}", GetName());
                return;
            }

            if (pAIActorProxy == null)
            {
                AILog.AIWarning("CAIActor::Update: AI Actor does not have proxy: {0}", GetName());
                return;
            }
            if (GetPhysics() == null)
            {
                AILog.AIWarning("CAIActor::Update: AI Actor does not have physics: {0}", GetName());
                return;
            }
            if (pAIActorProxy.IsDead())
            {
                AILog.AIWarning("CAIActor::Update: Trying to update dead AI Actor: {0}", GetName());
                return;
            }
        }

        QueryBodyInfo();

        UpdateBehaviorSelectionTree();
        UpdateCloakScale();

        CAISystem pAISystem = GlobalFunctions.GetAISystem();

        // Determine if position has changed
        Vec3 vPos = GetPos();
        if (type == EObjectUpdate.AIUPDATE_FULL)
        {
            if (!IsEquivalent_b(m_vLastFullUpdatePos, vPos, 1.0f))
            {
                m_cachedWaterOcclusionValue = pAISystem.GetWaterOcclusionValue(vPos);
                m_vLastFullUpdatePos = vPos;
                m_lastFullUpdateStance = m_bodyInfo.stance;
            }

            // update close contact info
            if (m_bCloseContact && ((pAISystem.GetFrameStartTime() - m_CloseContactTime).GetMilliSecondsAsInt64() > 1500))
            {
                m_bCloseContact = false;
            }
        }

        float dt = pAISystem.GetFrameDeltaTime();
        if (dt > 0.0f)
        {
            float turnAngle = Ang3.CreateRadZ(m_lastBodyDir, GetEntityDir());
            m_bodyTurningSpeed = turnAngle / dt;
        }
        else
        {
            m_bodyTurningSpeed = 0;
        }

        m_lastBodyDir = GetEntityDir();

        if (CastToCPipeUser() == null)
        {
            if (type == EObjectUpdate.AIUPDATE_FULL)
            {
                bool usingCombatLightRef = m_usingCombatLight;
                m_lightLevel = pAISystem.GetLightManager()?.GetLightLevelAt(GetPos(), this, ref usingCombatLightRef) ?? EAILightLevel.AILL_LIGHT;
                m_usingCombatLight = usingCombatLightRef;
            }

            // make sure to update direction when entity is not moved
            SAIBodyInfo bodyInfo = GetBodyInfo();
            SetPos(bodyInfo.vEyePos);
            SetEntityDir(bodyInfo.vEntityDir);
            SetBodyDir(bodyInfo.GetBodyDir());

            // AI Actor goto stuff
            if (!m_State.vMoveTarget.IsZero())
            {
                Vec3 vToMoveTarget = m_State.vMoveTarget - GetPos();
                if (!m_movementAbility.b3DMove)
                {
                    vToMoveTarget.z = 0.0f;
                }
                if (vToMoveTarget.GetLengthSquared() < sqr(m_movementAbility.pathRadius))
                {
                    ResetLookAt();
                    SetBodyTargetDir(bodyInfo.vEntityDir);
                }
                else
                {
                    SetBodyTargetDir(vToMoveTarget.Normalized());
                }
            }

            SetMoveDir(bodyInfo.vMoveDir);
            m_State.vMoveDir = bodyInfo.vMoveDir;

            SetViewDir(bodyInfo.GetEyeDir());

            CAIObject pAttTarget = m_refAttentionTarget.GetAIObject();
            if (pAttTarget != null && pAttTarget.IsEnabled())
            {
                if (CanSee(pAttTarget.GetVisionID()))
                {
                    m_State.eTargetType = EAITargetType.AITARGET_VISUAL;
                    m_State.nTargetType = pAttTarget.GetType();
                    m_State.bTargetEnabled = true;
                }
                else
                {
                    switch (m_State.eTargetType)
                    {
                    case EAITargetType.AITARGET_VISUAL:
                        m_State.eTargetThreat = EAITargetThreat.AITHREAT_AGGRESSIVE;
                        m_State.eTargetType = EAITargetType.AITARGET_MEMORY;
                        m_State.nTargetType = pAttTarget.GetType();
                        m_State.bTargetEnabled = true;
                        m_stimulusStartTime = GlobalFunctions.GetAISystem().GetFrameStartTimeSeconds();
                        break;

                    case EAITargetType.AITARGET_MEMORY:
                    case EAITargetType.AITARGET_SOUND:
                        if (GlobalFunctions.GetAISystem().GetFrameStartTimeSeconds() - m_stimulusStartTime >= 5.0f)
                        {
                            m_State.nTargetType = -1;
                            m_State.bTargetEnabled = false;
                            m_State.eTargetThreat = EAITargetThreat.AITHREAT_NONE;
                            m_State.eTargetType = EAITargetType.AITARGET_NONE;

                            SetAttentionTarget(NILREF);
                        }
                        break;
                    }
                }
            }
            else
            {
                m_State.nTargetType = -1;
                m_State.bTargetEnabled = false;
                m_State.eTargetThreat = EAITargetThreat.AITHREAT_NONE;
                m_State.eTargetType = EAITargetType.AITARGET_NONE;

                SetAttentionTarget(NILREF);
            }
        }

        m_bUpdatedOnce = true;
    }

    // ===================================================================
    // UpdateProxy (AIActor.cpp lines 755-768)
    // ===================================================================
    public virtual void UpdateProxy(EObjectUpdate type)
    {
        IAIActorProxy pAIActorProxy = GetProxy();

        SetMoveDir(m_State.vMoveDir);

        System.Diagnostics.Debug.Assert(pAIActorProxy != null);
        if (pAIActorProxy != null)
            pAIActorProxy.Update(m_State, (type == EObjectUpdate.AIUPDATE_FULL));
    }

    // ===================================================================
    // UpdateCloakScale (AIActor.cpp lines 773-776)
    // ===================================================================
    protected void UpdateCloakScale()
    {
        m_Parameters.m_fCloakScale = m_Parameters.m_fCloakScaleTarget;
    }

    // ===================================================================
    // UpdateDisabled (AIActor.cpp lines 780-788)
    // ===================================================================
    public virtual void UpdateDisabled(EObjectUpdate type)
    {
        IAIActorProxy pProxy = GetProxy();
        if (pProxy != null)
            pProxy.CheckUpdateStatus();
    }

    // ===================================================================
    // UpdateDamageParts (AIActor.cpp lines 793-869)
    // ===================================================================
    protected void UpdateDamageParts(DamagePartVector parts)
    {
        IAIActorProxy pProxy = GetProxy();
        if (pProxy == null)
            return;

        IPhysicalEntity phys = pProxy.GetPhysics(true);
        if (phys == null)
            return;

        bool queryDamageValues = true;

        pe_status_nparts statusNParts = new pe_status_nparts();
        int nParts = phys.GetStatus(statusNParts);

        if (parts.Count != nParts)
        {
            while (parts.Count < nParts) parts.Add(new SAIDamagePart());
            while (parts.Count > nParts) parts.RemoveAt(parts.Count - 1);
            queryDamageValues = true;
        }

        // The global damage table — full script integration deferred
        SmartScriptTable pDamageTable = default;
        if (queryDamageValues)
        {
            SmartScriptTable pSinglePlayerTable = default;
            gEnv.pScriptSystem?.GetGlobalValue("SinglePlayer", out pSinglePlayerTable);
            if (pSinglePlayerTable)
            {
                if (GetType() == (ushort)AIOBJECT_PLAYER)
                    pSinglePlayerTable.GetValue("DamageAIToPlayer", out pDamageTable);
                else
                    pSinglePlayerTable.GetValue("DamageAIToAI", out pDamageTable);
            }
            if (!pDamageTable)
                queryDamageValues = false;
        }

        pe_status_pos statusPos = new pe_status_pos();
        pe_params_part paramsPart = new pe_params_part();
        for (statusPos.ipart = 0, paramsPart.ipart = 0; statusPos.ipart < nParts; ++statusPos.ipart, ++paramsPart.ipart)
        {
            if (phys.GetParams(paramsPart) == 0 || phys.GetStatus(statusPos) == 0)
                continue;

            primitives.box box = new primitives.box();
            (statusPos.pGeomProxy as IGeometry)?.GetBBox(ref box);

            box.center = box.center * statusPos.scale;
            box.size = box.size * statusPos.scale;

            var part = parts[statusPos.ipart];
            part.pos = statusPos.pos + statusPos.q.Rotate(box.center);
            part.volume = (box.size.x * 2) * (box.size.y * 2) * (box.size.z * 2);

            if (queryDamageValues)
            {
                float damage = 0.0f;
                // full surface/material lookup deferred — requires ISurfaceTypeManager
                part.damageMult = damage;
            }

            parts[statusPos.ipart] = part;
        }
    }

    // ===================================================================
    // OnAIHandlerSentSignal (AIActor.cpp lines 871-898)
    // ===================================================================
    public virtual void OnAIHandlerSentSignal(string signalText, uint crc)
    {
        if (crc == 0)
        {
            crc = CCrc32.Compute(signalText);
        }
        else
        {
            System.Diagnostics.Debug.Assert(crc == CCrc32.Compute(signalText));
        }

        if (gAIEnv.CVars.LogSignals != 0)
            gEnv.pLog?.Log("OnAIHandlerSentSignal: '{0}' [{1}].", signalText, GetName());

        ProcessBehaviorSelectionTreeSignal(signalText, crc);

        if (IsRunningBehaviorTree())
        {
            BehaviorTree.Event eventArg = new BehaviorTree.Event();
            eventArg.id = crc;
#if USING_BEHAVIOR_TREE_EVENT_DEBUGGING
            eventArg.name = signalText;
#endif
            GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.HandleEvent(GetEntityID(), eventArg);
        }
    }

    // ===================================================================
    // SetSignal (AIActor.cpp lines 904-1014)
    // ===================================================================
    public virtual void SetSignal(int nSignalID, string szText, IEntity pSender = null, IAISignalExtraData pData = null, uint crcCode = 0)
    {
        CCCPOINT("SetSignal");

        // Ensure we delete the pData object if we early out
        // C++ uses RAII DeleteBeforeReturning guard; C# uses try/finally
        try
        {
#if DEBUG
            if (szText.Length + 1 > AISIGNAL.SIGNAL_NAME_LENGTH)
            {
                AILog.AIWarning("####>CAIObject::SetSignal SIGNAL STRING IS TOO LONG for <{0}> :: {1}  sz-> {2}", GetName(), szText, szText.Length);
            }
#endif // DEBUG

            // always process signals sent only to notify wait goal operation
            if (crcCode == 0)
                crcCode = CCrc32.Compute(szText);

            // (MATT) This is the only place that the CRCs are used and their implementation is very clumsy {2008/08/09}
            if (nSignalID != AISIGNAL_NOTIFY_ONLY)
            {
                if (nSignalID != AISIGNAL_ALLOW_DUPLICATES)
                {
                    foreach (var ai in m_State.vSignals)
                    {
#if DEBUG
                        if (string.Compare(ai.strText, szText, true) == 0 && !ai.Compare(crcCode))
                        {
                            AILog.AIWarning("Hash values are different, but strings are identical! {0} - {1} ", ai.strText, szText);
                        }

                        if (string.Compare(ai.strText, szText, true) != 0 && ai.Compare(crcCode))
                        {
                            AILog.AIWarning("Please report to aws.amazon.com/support. Hash values are identical, but strings are different! {0} - {1} ", ai.strText, szText);
                        }
#endif // DEBUG
                    }
                }

                if (!m_bEnabled && nSignalID != AISIGNAL_INCLUDE_DISABLED)
                {
                    // (Kevin) This seems like an odd assumption to be making. INCLUDE_DISABLED needs to be a bit or a separate passed-in value.
                    //	WarFace compatibility cannot have duplicate signals sent to disabled AI. (08/14/2009)
                    if (gAIEnv.configuration.eCompatibilityMode == EConfigCompatibilityMode.ECCM_WARFACE || nSignalID != AISIGNAL_ALLOW_DUPLICATES)
                    {
                        AILog.AILogComment("AIActor {0} {1} dropped signal '{2}' due to being disabled", this, GetName(), szText);
                        return;
                    }
                }
            }

            AISIGNAL signal = new AISIGNAL();
            signal.nSignal = nSignalID;
            signal.strText = szText;
            signal.m_nCrcText = crcCode;
            signal.senderID = pSender != null ? pSender.GetId() : 0;
            signal.pEData = pData;

#if CRYAISYSTEM_DEBUG
            RecorderEventData recorderEventData = new RecorderEventData(szText);
#endif

            if (nSignalID != AISIGNAL_RECEIVED_PREV_UPDATE)
            {
#if CRYAISYSTEM_DEBUG
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_SIGNALRECIEVED, ref recorderEventData);
                GlobalFunctions.GetAISystem().Record(this, IAIRecordable.e_AIDbgEvent.E_SIGNALRECIEVED, szText);
#endif
            }

            // don't let notify signals enter the queue
            if (nSignalID == AISIGNAL_NOTIFY_ONLY)
            {
                OnAIHandlerSentSignal(szText, crcCode);	// still a polymorphic call that ends up in the most derived class (which is intended)
                return;
            }

            // If all our early-outs passed and it wasn't just a "notify" then actually enter the signal into the stack!
            pData = null; // set to null to prevent autodeletion of pData on return

            // need to make sure constructor signal is always at the back - to be processed first
            if (m_State.vSignals.Count > 0)
            {
                AISIGNAL backSignal = m_State.vSignals[m_State.vSignals.Count - 1];

                if (string.Compare("Constructor", backSignal.strText, true) == 0)
                {
                    m_State.vSignals.RemoveAt(m_State.vSignals.Count - 1);
                    m_State.vSignals.Add(signal);
                    m_State.vSignals.Add(backSignal);
                }
                else
                    m_State.vSignals.Add(signal);
            }
            else
                m_State.vSignals.Add(signal);
        }
        finally
        {
            // C++ RAII guard: DeleteBeforeReturning ensures pData is freed on any exit
            if (pData != null)
                GlobalFunctions.GetAISystem().FreeSignalExtraData(pData as AISignalExtraData);
        }
    }

    // ===================================================================
    // IsHostile (AIActor.cpp lines 1019-1061)
    // ===================================================================
    public override bool IsHostile(IAIObject pOtherAI, bool bUsingAIIgnorePlayer = true)
    {
        bool hostile = false;

        if (pOtherAI != null)
        {
            CAIObject other = (CAIObject)pOtherAI;

            if (other.GetType() == (ushort)AIOBJECT_ATTRIBUTE)
            {
                CAIObject association = other.GetAssociation().GetAIObject();
                if (association != null)
                    other = association;
            }

            if (bUsingAIIgnorePlayer && (other.GetType() == (ushort)AIOBJECT_PLAYER) && (gAIEnv.CVars.IgnorePlayer != 0))
                return false;

            uint8 myFaction = GetFactionID();
            uint8 otherFaction = other.GetFactionID();

            if (!other.IsThreateningForHostileFactions())
                return false;

            hostile = (gAIEnv.pFactionMap?.GetReaction(myFaction, otherFaction) == IFactionMap.ReactionType.Hostile);

            if (!hostile && m_forcefullyHostiles.Count > 0)
            {
                if (m_forcefullyHostiles.Contains(pOtherAI.GetAIObjectID()))
                    hostile = true;
            }

            if (hostile)
            {
                CAIActor actor = other.CastToCAIActor();
                if (actor != null)
                    if (bUsingAIIgnorePlayer && (m_Parameters.m_bAiIgnoreFgNode || actor.GetParameters().m_bAiIgnoreFgNode))
                        hostile = false;
            }
        }

        return hostile;
    }

    // ===================================================================
    // Event (AIActor.cpp lines 1066-1166)
    // ===================================================================
    public override void Event(ushort eType, SAIEVENT pEvent)
    {
        CAISystem pAISystem = GlobalFunctions.GetAISystem();
        IAIActorProxy pAIActorProxy = GetProxy();

        bool bWasEnabled = m_bEnabled;

        base.Event(eType, pEvent);

        switch (eType)
        {
        case (ushort)AIEVENT_DISABLE:
            {
                uint aiObjectId = GetAIObjectID();
                gAIEnv.pTargetTrackManager?.ResetAgent(aiObjectId);
                gAIEnv.pTargetTrackManager?.SetAgentEnabled(aiObjectId, false);

                pAISystem.UpdateGroupStatus(GetGroupId());
                pAISystem.NotifyEnableState(this, false);

                SetObserver(false);
            }
            break;
        case (ushort)AIEVENT_ENABLE:
            if (pAIActorProxy != null && pAIActorProxy.IsDead())
                return;
            m_bEnabled = true;
            gAIEnv.pTargetTrackManager?.SetAgentEnabled(GetAIObjectID(), true);
            pAISystem.UpdateGroupStatus(GetGroupId());
            pAISystem.NotifyEnableState(this, true);

            SetObserver(GetType() != (ushort)AIOBJECT_PLAYER);
            SetObservable(true);
            break;
        case (ushort)AIEVENT_SLEEP:
            m_bCheckedBody = false;
            if (pAIActorProxy != null && pAIActorProxy.GetLinkedVehicleEntityId() == 0)
            {
                m_bEnabled = false;
                pAISystem.NotifyEnableState(this, m_bEnabled);
            }
            break;
        case (ushort)AIEVENT_WAKEUP:
            m_bEnabled = true;
            pAISystem.NotifyEnableState(this, m_bEnabled);
            m_bCheckedBody = true;
            pAISystem.UpdateGroupStatus(GetGroupId());
            break;
        case (ushort)AIEVENT_ONVISUALSTIMULUS:
            HandleVisualStimulus(pEvent);
            break;
        case (ushort)AIEVENT_ONSOUNDEVENT:
            HandleSoundEvent(pEvent);
            break;
        case (ushort)AIEVENT_ONBULLETRAIN:
            HandleBulletRain(pEvent);
            break;
        case (ushort)AIEVENT_AGENTDIED:
            {
                ResetModularBehaviorTree(EObjectResetType.AIOBJRESET_SHUTDOWN);

                pAISystem.NotifyTargetDead(this);

                m_bCheckedBody = false;
                m_bEnabled = false;
                pAISystem.NotifyEnableState(this, m_bEnabled);

                pAISystem.RemoveFromGroup(GetGroupId(), this);

                pAISystem.ReleaseFormationPoint(this);
                CancelRequestedPath(false);
                ReleaseFormation();

                m_State.ClearSignals();

                uint killerID = pEvent.targetEntityID;
                pAISystem.OnAgentDeath(GetEntityID(), killerID);

                if (pAIActorProxy != null)
                {
                    pAIActorProxy.Reset(EObjectResetType.AIOBJRESET_SHUTDOWN);
                }

                SetObservable(false);
                SetObserver(false);
            }
            break;
        }

        // Update the group status
        if (bWasEnabled != m_bEnabled)
        {
            GlobalFunctions.GetAISystem().UpdateGroupStatus(GetGroupId());
        }
    }

    // ===================================================================
    // EntityEvent (AIActor.cpp lines 1168-1191)
    // ===================================================================
    public override void EntityEvent(SEntityEvent eventArg)
    {
        switch (eventArg.eventType)
        {
        case EEntityEvent.ENTITY_EVENT_ATTACH_THIS:
        case EEntityEvent.ENTITY_EVENT_DETACH_THIS:
            QueryBodyInfo();
            break;

        case EEntityEvent.ENTITY_EVENT_ENABLE_PHYSICS:
            UpdateObserverSkipList();
            break;

        case EEntityEvent.ENTITY_EVENT_DONE:
        case EEntityEvent.ENTITY_EVENT_RETURNING_TO_POOL:
            StopBehaviorTree();
            break;

        default:
            break;
        }

        base.EntityEvent(eventArg);
    }

    // ===================================================================
    // CanAcquireTarget (AIActor.cpp lines 1196-1217)
    // ===================================================================
    public virtual bool CanAcquireTarget(IAIObject pOther)
    {
        if (pOther == null || !pOther.IsEnabled() || pOther.GetEntity().IsHidden())
            return false;

        CCCPOINT("CAIActor_CanAcquireTarget");

        CAIObject pOtherAI = (CAIObject)pOther;
        if (pOtherAI.GetType() == (ushort)AIOBJECT_ATTRIBUTE && pOtherAI.GetAssociation().IsValid())
            pOtherAI = (CAIObject)pOtherAI.GetAssociation().GetAIObject();

        if (pOtherAI == null || !pOtherAI.IsTargetable())
            return false;

        CAIActor pOtherActor = pOtherAI.CastToCAIActor();
        if (pOtherActor == null)
            return (pOtherAI.GetType() == (ushort)AIOBJECT_TARGET);

        if (GlobalFunctions.GetAISystem().GetCombatClassScale(m_Parameters.m_CombatClass, pOtherActor.GetParameters().m_CombatClass) > 0)
            return true;
        return false;
    }

    // ===================================================================
    // SetGroupId (AIActor.cpp lines 1221-1238)
    // ===================================================================
    public override void SetGroupId(int id)
    {
        if (id != GetGroupId())
        {
            gAIEnv.pGroupManager?.RemoveGroupMember(GetGroupId(), GetAIObjectID());
            if (id > 0)
                gAIEnv.pGroupManager?.AddGroupMember(id, GetAIObjectID());

            base.SetGroupId(id);
            GlobalFunctions.GetAISystem().AddToGroup(this);

            CAIObject pBeacon = (CAIObject)GlobalFunctions.GetAISystem().GetBeacon(id);
            if (pBeacon != null)
                GlobalFunctions.GetAISystem().UpdateBeacon(id, pBeacon.GetPos(), this);

            m_Parameters.m_nGroup = id;
        }
    }

    // ===================================================================
    // SetFactionID (AIActor.cpp lines 1240-1253)
    // ===================================================================
    public override void SetFactionID(uint8 factionID)
    {
        base.SetFactionID(factionID);

        if (IsObserver())
        {
            ObserverParams observerParams = new ObserverParams();
            uint8 faction = GetFactionID();
            observerParams.factionsToObserveMask = GetFactionVisionMask(faction);
            observerParams.faction = faction;

            gAIEnv.pVisionMap?.ObserverChanged(GetVisionID(), observerParams, eChangedFaction | eChangedFactionsToObserveMask);
        }
    }

    // ===================================================================
    // Behavior listeners (AIActor.cpp lines 1255-1297)
    // ===================================================================
    public virtual void RegisterBehaviorListener(IActorBehaviorListener listener) { m_behaviorListeners.Add(listener); }
    public virtual void UnregisterBehaviorListener(IActorBehaviorListener listener) { m_behaviorListeners.Remove(listener); }

    public virtual void BehaviorEvent(EBehaviorEvent eventArg)
    {
        foreach (IActorBehaviorListener listener in new List<IActorBehaviorListener>(m_behaviorListeners))
        {
            listener.BehaviorEvent(this, eventArg);
        }
    }

    public virtual void BehaviorChanged(string current, string previous)
    {
        foreach (IActorBehaviorListener listener in new List<IActorBehaviorListener>(m_behaviorListeners))
        {
            listener.BehaviorChanged(this, current, previous);
        }
    }

    // ===================================================================
    // SetParameters (AIActor.cpp lines 1302-1317)
    // ===================================================================
    public virtual void SetParameters(AgentParameters sParams)
    {
        SetGroupId(sParams.m_nGroup);
        SetFactionID(sParams.factionID);

        m_Parameters = sParams;
        m_Parameters.m_fAccuracy = clamp_tpl(m_Parameters.m_fAccuracy, 0.0f, 1.0f);

        GlobalFunctions.GetAISystem().AddToFaction(this, sParams.factionID);

        CacheFOVCos(sParams.m_PerceptionParams.FOVPrimary, sParams.m_PerceptionParams.FOVSecondary);
        float range = System.Math.Max(sParams.m_PerceptionParams.sightRange,
            sParams.m_PerceptionParams.sightRangeVehicle);

        VisionChanged(range, m_FOVPrimaryCos, m_FOVSecondaryCos);
    }

#if CRYAISYSTEM_DEBUG
    // ===================================================================
    // UpdateHealthHistory (AIActor.cpp lines 1319-1331)
    // ===================================================================
    public void UpdateHealthHistory()
    {
        if (GetProxy() == null) return;
        if (m_healthHistory == null)
            m_healthHistory = new CValueHistory<float>(100, 0.1f);
        float health = (GetProxy().GetActorHealth() + GetProxy().GetActorArmor());
        float maxHealth = (float)GetProxy().GetActorMaxHealth();

        m_healthHistory.Sample(health / maxHealth, GlobalFunctions.GetAISystem().GetFrameDeltaTime());
    }
#endif

    // ===================================================================
    // Serialize (AIActor.cpp lines 1335-1471)
    // ===================================================================
    public override void Serialize(TSerialize ser)
    {
        ser.Value("m_bCheckedBody", ref m_bCheckedBody);

        m_State.Serialize(ser);
        m_Parameters.Serialize(ser);
        SerializeMovementAbility(ser);

        base.Serialize(ser);

        if (ser.IsReading())
        {
            SetParameters(m_Parameters);
        }

        if (ser.IsReading())
        {
            uint entityId = GetEntityID();

            if (m_proxy == null)
            {
                AILog.AILogComment("CAIActor({0}) Creating AIActorProxy for serialization.", this);
                m_proxy = GlobalFunctions.GetAISystem().GetActorProxyFactory()?.CreateActorProxy(entityId);
            }

            gAIEnv.pActorLookUp?.UpdateProxy(this);
        }

        if (m_proxy != null)
            m_proxy.Serialize(ser);
        else
            AILog.AIWarning("CAIActor::Serialize Missing proxy for '{0}' after loading", GetName());

        ser.EnumValue("m_behaviorTreeEvaluationMode", ref m_behaviorTreeEvaluationMode,
            BehaviorTreeEvaluationMode.FirstBehaviorTreeEvaluationMode, BehaviorTreeEvaluationMode.BehaviorTreeEvaluationModeCount);

        bool hasBST = m_behaviorSelectionTree != null;
        if (ser.BeginOptionalGroup("BehaviorSelectionTree", hasBST))
        {
            if (ser.IsReading())
                ResetBehaviorSelectionTree(EObjectResetType.AIOBJRESET_INIT);

            if (m_behaviorSelectionTree != null)
                m_behaviorSelectionTree.Serialize(ser);
            else
                AILog.AIWarning("CAIActor::Serialize Missing Behavior Selection Tree for '{0}' after loading", GetName());

            if (m_behaviorSelectionVariables != null)
                m_behaviorSelectionVariables.Serialize(ser);
            else
                AILog.AIWarning("CAIActor::Serialize Missing Behavior Selection Variables for '{0}' after loading", GetName());

            if (ser.IsReading())
                UpdateBehaviorSelectionTree();

            ser.EndGroup();
        }
        else if (ser.IsReading())
        {
            m_behaviorSelectionTree = null;
            m_behaviorSelectionVariables = null;
        }

        if (ser.IsReading())
        {
            ResetBehaviorSelectionTree(EObjectResetType.AIOBJRESET_INIT);
        }

        if (ser.IsReading())
        {
            ResetModularBehaviorTree(EObjectResetType.AIOBJRESET_INIT);
        }

        bool observer = m_observer;
        ser.Value("m_observer", ref observer);

        if (ser.IsReading())
        {
            SetObserver(observer);

            bool addToGroup = GetProxy() != null ? !GetProxy().IsDead() : CastToCLeader() != null;
            if (addToGroup)
                GlobalFunctions.GetAISystem().AddToGroup(this);

            ReactionChanged(0, IFactionMap.ReactionType.Hostile);

            m_probableTargets.Clear();
            m_usingCombatLight = false;
            m_lightLevel = EAILightLevel.AILL_LIGHT;
        }

        ser.Value("m_cachedWaterOcclusionValue", ref m_cachedWaterOcclusionValue);

        ser.Value("m_vLastFullUpdatePos", ref m_vLastFullUpdatePos);
        uint lastFullUpdateStance = (uint)m_lastFullUpdateStance;
        ser.Value("m_lastFullUpdateStance", ref lastFullUpdateStance);
        if (ser.IsReading())
        {
            m_lastFullUpdateStance = (EStance)lastFullUpdateStance;
        }

        if (ser.IsReading())
        {
            SetAttentionTarget(NILREF);
        }

        ser.Value("m_bCloseContact", ref m_bCloseContact);

        // Territory
        ser.Value("m_territoryShapeName", ref m_territoryShapeName);
        if (ser.IsReading())
            m_territoryShape = GlobalFunctions.GetAISystem().GetGenericShapeOfName(m_territoryShapeName);

        // m_forcefullyHostiles serialization — SortedSet<uint>
        int hostileCount = m_forcefullyHostiles.Count;
        ser.Value("m_forcefullyHostiles_count", ref hostileCount);
        if (ser.IsReading())
        {
            m_forcefullyHostiles.Clear();
            for (int i = 0; i < hostileCount; i++)
            {
                uint val = 0;
                ser.Value("hostile", ref val);
                m_forcefullyHostiles.Add(val);
            }
        }
        else
        {
            foreach (uint val in m_forcefullyHostiles)
            {
                uint v = val;
                ser.Value("hostile", ref v);
            }
        }

        ser.Value("m_activeCoordinationCount", ref m_activeCoordinationCount);

        uint navigationTypeId = m_navigationTypeID.id;
        ser.Value("m_navigationTypeID", ref navigationTypeId);
        if (ser.IsReading())
        {
            m_navigationTypeID = new NavigationAgentTypeID { id = navigationTypeId };
        }

        ser.Value("m_currentCollisionAvoidanceRadiusIncrement", ref m_currentCollisionAvoidanceRadiusIncrement);

        ser.Value("m_initialPosition.isValid", ref m_initialPosition.isValid);
        ser.Value("m_initialPosition.pos", ref m_initialPosition.pos);
    }

    // ===================================================================
    // SetAttentionTarget (AIActor.cpp lines 1473-1484)
    // ===================================================================
    public virtual void SetAttentionTarget(CWeakRef<CAIObject> refTarget)
    {
        CCCPOINT("CAIActor_SetAttentionTarget");
        m_refAttentionTarget = refTarget;

#if CRYAISYSTEM_DEBUG
        CAIObject pAttTarget = refTarget.GetAIObject();
        RecorderEventData recorderEventData = new RecorderEventData(pAttTarget != null ? pAttTarget.GetName() : "<none>");
        RecordEvent(IAIRecordable.e_AIDbgEvent.E_ATTENTIONTARGET, recorderEventData);
#endif
    }

    // ===================================================================
    // GetFloorPosition (AIActor.cpp lines 1487-1492)
    // ===================================================================
    public virtual Vec3 GetFloorPosition(Vec3 pos)
    {
        Vec3 floorPos = pos;
        return (GetFloorPos(ref floorPos, pos, WalkabilityFloorUpDist, WalkabilityFloorDownDist, WalkabilityDownRadius, AICE_STATIC))
            ? floorPos : pos;
    }

    // ===================================================================
    // CheckCloseContact (AIActor.cpp lines 1495-1503)
    // ===================================================================
    public virtual void CheckCloseContact(IAIObject pTarget, float distSq)
    {
        if (!m_bCloseContact && distSq < sqr(GetParameters().m_fMeleeRange))
        {
            SetSignal(1, "OnCloseContact", pTarget.GetEntity(), null, gAIEnv.SignalCRCs.m_nOnCloseContact);
            SetCloseContact(true);
        }
    }

    // ===================================================================
    // SetCloseContact (AIActor.cpp lines 1506-1511)
    // ===================================================================
    public void SetCloseContact(bool bCloseContact)
    {
        if (bCloseContact && !m_bCloseContact)
            m_CloseContactTime = GlobalFunctions.GetAISystem().GetFrameStartTime();
        m_bCloseContact = bCloseContact;
    }

    // ===================================================================
    // IsObjectInFOV (AIActor.cpp lines 1513-1521)
    // ===================================================================
    public EFieldOfViewResult IsObjectInFOV(CAIObject pTarget, float fDistanceScale = 1.0f)
    {
        CCCPOINT("CAIActor_IsObjectInFOVCone");

        Vec3 vTargetPos = pTarget.GetPos();
        float fSightRange = GetMaxTargetVisibleRange(pTarget) * fDistanceScale;
        return (fSightRange > 0.0f ? CheckPointInFOV(vTargetPos, fSightRange) : EFieldOfViewResult.eFOV_Outside);
    }

    // ===================================================================
    // GetLiveTarget (static, AIActor.cpp lines 1523-1559)
    // ===================================================================
    public static CWeakRef<CAIActor> GetLiveTarget(CWeakRef<CAIObject> refTarget)
    {
        CCCPOINT("CPuppet_GetLiveTarget");

        CWeakRef<CAIActor> refResult = new CWeakRef<CAIActor>();
        CAIObject pTarget = refTarget.GetAIObject();
        if (pTarget != null)
        {
            CAIActor pAIActor = pTarget.CastToCAIActor();
            if (pAIActor != null && pAIActor.IsActive() && pAIActor.IsAgent())
            {
                refResult = StaticCast<CAIActor>(refTarget);
            }
            else
            {
                CAIActor pAssociatedAIActor = CastToCAIActorSafe(pTarget.GetAssociation().GetAIObject());
                if (pAssociatedAIActor != null && pAssociatedAIActor.IsEnabled() && pAssociatedAIActor.IsAgent())
                {
                    refResult = StaticCast<CAIActor>(GetWeakRef(pAssociatedAIActor));
                }
            }
        }
        return refResult;
    }

    public static CAIObject GetLiveTarget(CAIObject pTarget)
    {
        if (pTarget == null)
            return null;
        CAIActor pTargetAIActor = pTarget.CastToCAIActor();
        if (pTargetAIActor != null)
            if (!pTargetAIActor.IsActive())
                return null;
        if (pTarget.IsAgent())
            return pTarget;
        CAIObject pAssociation = pTarget.GetAssociation().GetAIObject();
        return (pAssociation != null && pAssociation.IsEnabled() && pAssociation.IsAgent()) ? pAssociation : null;
    }

    // ===================================================================
    // GetFactionVisionMask (AIActor.cpp lines 1561-1586)
    // ===================================================================
    protected uint GetFactionVisionMask(uint8 factionID)
    {
        uint mask = 0;
        uint factionCount = gAIEnv.pFactionMap?.GetFactionCount() ?? 0;

        for (uint i = 0; i < factionCount; ++i)
        {
            if (i != factionID)
            {
                if (gAIEnv.pFactionMap.GetReaction((uint8)factionID, (uint8)i) < IFactionMap.ReactionType.Neutral)
                    mask |= 1u << (int)i;
            }
        }

        foreach (uint hostileId in m_forcefullyHostiles)
        {
            CAIObject aiObject = gAIEnv.pAIObjectManager?.GetAIObject(hostileId) as CAIObject;
            if (aiObject != null && aiObject.GetFactionID() != IFactionMap.InvalidFactionID)
                mask |= 1u << aiObject.GetFactionID();
        }

        return mask;
    }

    // ===================================================================
    // SerializeMovementAbility (AIActor.cpp lines 1588-1662)
    // ===================================================================
    protected void SerializeMovementAbility(TSerialize ser)
    {
        ser.BeginGroup("AgentMovementAbility");
        AgentMovementAbility moveAbil = m_movementAbility;

        ser.Value("b3DMove", ref moveAbil.b3DMove);
        ser.Value("bUsePathfinder", ref moveAbil.bUsePathfinder);
        ser.Value("usePredictiveFollowing", ref moveAbil.usePredictiveFollowing);
        ser.Value("allowEntityClampingByAnimation", ref moveAbil.allowEntityClampingByAnimation);
        ser.Value("maxAccel", ref moveAbil.maxAccel);
        ser.Value("maxDecel", ref moveAbil.maxDecel);
        ser.Value("minTurnRadius", ref moveAbil.minTurnRadius);
        ser.Value("maxTurnRadius", ref moveAbil.maxTurnRadius);
        ser.Value("avoidanceRadius", ref moveAbil.avoidanceRadius);
        ser.Value("pathLookAhead", ref moveAbil.pathLookAhead);
        ser.Value("pathRadius", ref moveAbil.pathRadius);
        ser.Value("pathSpeedLookAheadPerSpeed", ref moveAbil.pathSpeedLookAheadPerSpeed);
        ser.Value("cornerSlowDown", ref moveAbil.cornerSlowDown);
        ser.Value("slopeSlowDown", ref moveAbil.slopeSlowDown);
        ser.Value("optimalFlightHeight", ref moveAbil.optimalFlightHeight);
        ser.Value("minFlightHeight", ref moveAbil.minFlightHeight);
        ser.Value("maxFlightHeight", ref moveAbil.maxFlightHeight);
        ser.Value("maneuverTrh", ref moveAbil.maneuverTrh);
        ser.Value("velDecay", ref moveAbil.velDecay);
        ser.Value("pathFindPrediction", ref moveAbil.pathFindPrediction);
        ser.Value("pathRegenIntervalDuringTrace", ref moveAbil.pathRegenIntervalDuringTrace);
        ser.Value("teleportEnabled", ref moveAbil.teleportEnabled);
        ser.Value("lightAffectsSpeed", ref moveAbil.lightAffectsSpeed);
        ser.Value("resolveStickingInTrace", ref moveAbil.resolveStickingInTrace);
        ser.Value("directionalScaleRefSpeedMin", ref moveAbil.directionalScaleRefSpeedMin);
        ser.Value("directionalScaleRefSpeedMax", ref moveAbil.directionalScaleRefSpeedMax);
        ser.Value("avoidanceAbilities", ref moveAbil.avoidanceAbilities);
        ser.Value("pushableObstacleWeakAvoidance", ref moveAbil.pushableObstacleWeakAvoidance);
        ser.Value("pushableObstacleAvoidanceRadius", ref moveAbil.pushableObstacleAvoidanceRadius);
        ser.Value("pushableObstacleMassMin", ref moveAbil.pushableObstacleMassMin);
        ser.Value("pushableObstacleMassMax", ref moveAbil.pushableObstacleMassMax);

        ser.BeginGroup("AgentMovementSpeeds");
        AgentMovementSpeeds moveSpeeds = moveAbil.movementSpeeds;
        for (int i = 0; i < (int)AgentMovementSpeeds.EAgentMovementUrgency.AMU_NUM_VALUES; i++)
            for (int j = 0; j < (int)AgentMovementSpeeds.EAgentMovementStance.AMS_NUM_VALUES; j++)
            {
                ser.BeginGroup("range");
                AgentMovementSpeeds.SSpeedRange range = moveSpeeds.GetRange(j, i);
                ser.Value("def", ref range.def);
                ser.Value("min", ref range.min);
                ser.Value("max", ref range.max);
                moveSpeeds.SetRange(j, i, range);
                ser.EndGroup();
            }
        ser.EndGroup();

        ser.BeginGroup("AgentPathfindingProperties");
        AgentPathfindingProperties pfProp = moveAbil.pathfindingProperties;
        pfProp.navCapMask.Serialize(ser);
        ser.Value("triangularResistanceFactor", ref pfProp.triangularResistanceFactor);
        ser.Value("waypointResistanceFactor", ref pfProp.waypointResistanceFactor);
        ser.Value("flightResistanceFactor", ref pfProp.flightResistanceFactor);
        ser.Value("volumeResistanceFactor", ref pfProp.volumeResistanceFactor);
        ser.Value("roadResistanceFactor", ref pfProp.roadResistanceFactor);
        ser.Value("waterResistanceFactor", ref pfProp.waterResistanceFactor);
        ser.Value("maxWaterDepth", ref pfProp.maxWaterDepth);
        ser.Value("minWaterDepth", ref pfProp.minWaterDepth);
        ser.Value("exposureFactor", ref pfProp.exposureFactor);
        ser.Value("dangerCost", ref pfProp.dangerCost);
        ser.Value("zScale", ref pfProp.zScale);
        ser.EndGroup();

        ser.EndGroup();

        m_movementAbility = moveAbil;
    }

    // ===================================================================
    // AdjustTargetVisibleRange (AIActor.cpp line 1667-1670)
    // ===================================================================
    public virtual float AdjustTargetVisibleRange(CAIActor observer, float fVisibleRange) { return fVisibleRange; }

    // ===================================================================
    // GetMaxTargetVisibleRange (AIActor.cpp lines 1674-1711)
    // ===================================================================
    public virtual float GetMaxTargetVisibleRange(IAIObject pTarget, bool bCheckCloak = true)
    {
        float fRange = 0.0f;

        AgentParameters parameters = GetParameters();
        AgentPerceptionParameters perception = parameters.m_PerceptionParams;

        CloakObservability cloakObs = new CloakObservability();
        cloakObs.cloakMaxDistCrouchedAndMoving = perception.cloakMaxDistCrouchedAndMoving;
        cloakObs.cloakMaxDistCrouchedAndStill = perception.cloakMaxDistCrouchedAndStill;
        cloakObs.cloakMaxDistMoving = perception.cloakMaxDistMoving;
        cloakObs.cloakMaxDistStill = perception.cloakMaxDistStill;

        CAIActor pTargetActor = CastToCAIActorSafe(pTarget);
        if (pTargetActor == null || !pTargetActor.IsInvisibleFrom(GetPos(), bCheckCloak, true, cloakObs))
        {
            fRange = perception.sightRange;

            if (pTarget != null)
            {
                if (pTarget.GetAIType() == (ushort)AIOBJECT_VEHICLE && parameters.m_PerceptionParams.sightRangeVehicle > float.Epsilon)
                {
                    fRange = parameters.m_PerceptionParams.sightRangeVehicle;
                }

                if (pTargetActor != null && fRange > float.Epsilon)
                {
                    fRange = pTargetActor.AdjustTargetVisibleRange(this, fRange);
                }
            }
        }

        return fRange;
    }

    // ===================================================================
    // IsCloakEffective (AIActor.cpp lines 1716-1721)
    // ===================================================================
    public bool IsCloakEffective(Vec3 pos)
    {
        return (m_Parameters.m_fCloakScaleTarget > 0.0f &&
            !IsUsingCombatLight() &&
            (GetGrabbedEntity() == null || !IsGrabbedEntityInView(pos)));
    }

    // ===================================================================
    // IsInvisibleFrom (AIActor.cpp lines 1726-1753)
    // ===================================================================
    public virtual bool IsInvisibleFrom(Vec3 pos, bool bCheckCloak = true, bool bCheckCloakDistance = true, CloakObservability cloakObservability = default)
    {
        bool bInvisible = m_Parameters.m_bInvisible;

        if (!bInvisible && bCheckCloak)
        {
            bInvisible = (m_Parameters.m_bCloaked && IsCloakEffective(pos));

            if (bInvisible && bCheckCloakDistance)
            {
                float cloakMaxDist = 0.0f;

                if (m_bodyInfo.stance == EStance.STANCE_CROUCH)
                {
                    cloakMaxDist = m_bodyInfo.isMoving ? cloakObservability.cloakMaxDistCrouchedAndMoving : cloakObservability.cloakMaxDistCrouchedAndStill;
                }
                else
                {
                    cloakMaxDist = m_bodyInfo.isMoving ? cloakObservability.cloakMaxDistMoving : cloakObservability.cloakMaxDistStill;
                }

                bInvisible = (GetPos() - pos).GetLengthSquared() > sqr(cloakMaxDist);
            }
        }

        return bInvisible;
    }

    // NotifyDeath (AIActor.cpp lines 1757-1759) — empty in C++
    public void NotifyDeath() { }

    // ===================================================================
    // GetPhysicalSkipEntities (AIActor.cpp lines 1778-1805)
    // ===================================================================
    public override void GetPhysicalSkipEntities(PhysSkipList skipList)
    {
        base.GetPhysicalSkipEntities(skipList);

        SAIBodyInfo bi = GetBodyInfo();
        IEntity pLinkedVehicleEntity = bi.GetLinkedVehicleEntity();
        if (pLinkedVehicleEntity != null)
        {
            CheckAndAddPhysEntity(skipList, pLinkedVehicleEntity.GetPhysics());
        }

        IEntity pGrabbedEntity = GetGrabbedEntity();
        if (pGrabbedEntity != null)
            CheckAndAddPhysEntity(skipList, pGrabbedEntity.GetPhysics());
    }

    private static void CheckAndAddPhysEntity(PhysSkipList skipList, IPhysicalEntity physics)
    {
        if (physics != null)
        {
            pe_status_pos stat = new pe_status_pos();
            if ((physics.GetStatus(stat) != 0) && (((1 << stat.iSimClass) & COVER_OBJECT_TYPES) != 0))
            {
                if (!skipList.Contains(physics))
                    skipList.Add(physics);
            }
        }
    }

    // ===================================================================
    // UpdateObserverSkipList (AIActor.cpp lines 1807-1823)
    // ===================================================================
    public virtual void UpdateObserverSkipList()
    {
        if (m_observer)
        {
            PhysSkipList skipList = new PhysSkipList();
            GetPhysicalSkipEntities(skipList);

            ObserverParams observerParams = new ObserverParams();
            observerParams.skipListSize = System.Math.Min(skipList.Count, ObserverParams.MaxSkipListSize);
            observerParams.skipList = new IPhysicalEntity[ObserverParams.MaxSkipListSize];
            for (int i = 0; i < observerParams.skipListSize; ++i)
                observerParams.skipList[i] = skipList[i];

            gAIEnv.pVisionMap?.ObserverChanged(GetVisionID(), observerParams, eChangedSkipList);
        }
    }

    // ===================================================================
    // GetLocalBounds (AIActor.cpp lines 1825-1845)
    // ===================================================================
    public void GetLocalBounds(out AABB bbox)
    {
        bbox = new AABB();
        bbox.min = new Vec3(0, 0, 0);
        bbox.max = new Vec3(0, 0, 0);

        IEntity pEntity = GetEntity();
        IPhysicalEntity pPhysicalEntity = pEntity?.GetPhysics();
        if (pPhysicalEntity != null)
        {
            pe_status_pos pstate = new pe_status_pos();
            if (pPhysicalEntity.GetStatus(pstate) != 0)
            {
                bbox.min = pstate.BBox[0] / pstate.scale;
                bbox.max = pstate.BBox[1] / pstate.scale;
            }
        }
        else
        {
            pEntity?.GetLocalBounds(out bbox);
        }
    }

    public override IPhysicalEntity GetPhysics(bool bWantCharacterPhysics = false)
    {
        IAIActorProxy pAIActorProxy = GetProxy();
        return pAIActorProxy != null ? pAIActorProxy.GetPhysics(bWantCharacterPhysics)
                                     : base.GetPhysics(bWantCharacterPhysics);
    }

    public virtual bool CanDamageTarget(IAIObject target = null) { return true; }
    public virtual bool CanDamageTargetWithMelee() { return true; }

    public override void SetProxy(IAIActorProxy proxy) { m_proxy = proxy; }
    public override IAIActorProxy GetProxy() { return m_proxy; }

    public ref SOBJECTSTATE GetState() { return ref m_State; }

    // ===================================================================
    // FOV / Vision (AIActor.cpp lines 1953-2098)
    // ===================================================================
    public void GetSightFOVCos(out float primaryFOVCos, out float secondaryFOVCos)
    {
        primaryFOVCos = m_FOVPrimaryCos;
        secondaryFOVCos = m_FOVSecondaryCos;
    }

    public void CacheFOVCos(float FOVPrimary, float FOVSecondary)
    {
        if (FOVPrimary < 0.0f || FOVPrimary > 360.0f)
        {
            m_FOVPrimaryCos = -1.0f;
            m_FOVSecondaryCos = -1.0f;
        }
        else
        {
            if (FOVSecondary >= 0.0f && FOVPrimary > FOVSecondary)
                FOVSecondary = FOVPrimary;

            m_FOVPrimaryCos = cosf(DEG2RAD(FOVPrimary * 0.5f));

            if (FOVSecondary < 0.0f || FOVSecondary > 360.0f)
                m_FOVSecondaryCos = -1.0f;
            else
                m_FOVSecondaryCos = cosf(DEG2RAD(FOVSecondary * 0.5f));
        }
    }

    public virtual void ReactionChanged(uint8 factionID, IFactionMap.ReactionType reaction)
    {
        if (m_observer)
        {
            ObserverParams observerParams = new ObserverParams();
            uint8 faction = GetFactionID();
            observerParams.factionsToObserveMask = GetFactionVisionMask(faction);
            observerParams.faction = faction;

            gAIEnv.pVisionMap?.ObserverChanged(GetVisionID(), observerParams, eChangedFaction | eChangedFactionsToObserveMask);
        }
    }

    public virtual void VisionChanged(float sightRange, float primaryFOVCos, float secondaryFOVCos)
    {
        if (m_observer)
        {
            ObserverParams observerParams = new ObserverParams();
            observerParams.sightRange = sightRange;
            observerParams.fovCos = primaryFOVCos;

            gAIEnv.pVisionMap?.ObserverChanged(GetVisionID(), observerParams, eChangedSightRange | eChangedFOV);
        }
    }

    // ===================================================================
    // SetObserver (AIActor.cpp lines 2009-2052)
    // ===================================================================
    public virtual void SetObserver(bool observer)
    {
        if (m_observer != observer)
        {
            if (observer)
            {
                uint8 faction = GetFactionID();
                ObserverParams observerParams = new ObserverParams();
                observerParams.entityId = GetEntityID();
                observerParams.factionsToObserveMask = GetFactionVisionMask(faction);
                observerParams.faction = faction;
                observerParams.typesToObserveMask = GetObserverTypeMask();
                observerParams.typeMask = GetObservableTypeMask();
                observerParams.eyePosition = GetPos();
                observerParams.eyeDirection = GetViewDir();
                observerParams.fovCos = m_FOVPrimaryCos;
                observerParams.sightRange = m_Parameters.m_PerceptionParams.sightRange;

                PhysSkipList skipList = new PhysSkipList();
                GetPhysicalSkipEntities(skipList);

                observerParams.skipList = new IPhysicalEntity[ObserverParams.MaxSkipListSize];
                observerParams.skipListSize = System.Math.Min(skipList.Count, ObserverParams.MaxSkipListSize);
                for (int i = 0; i < observerParams.skipListSize; ++i)
                    observerParams.skipList[i] = skipList[i];

                VisionID visionID = GetVisionID();
                if (visionID.IsNil())
                {
                    visionID = gAIEnv.pVisionMap?.CreateVisionID(GetName()) ?? new VisionID();
                    SetVisionID(visionID);
                }

                gAIEnv.pVisionMap?.RegisterObserver(visionID, observerParams);
            }
            else
            {
                VisionID visionID = GetVisionID();
                if (!visionID.IsNil())
                    gAIEnv.pVisionMap?.UnregisterObserver(visionID);
            }

            m_observer = observer;
        }
    }

    public virtual uint GetObserverTypeMask() { return General | AliveAgent | DeadAgent | Player; }
    public virtual uint GetObservableTypeMask() { return General | AliveAgent; }

    public virtual bool IsObserver() { return m_observer; }

    public virtual bool CanSee(VisionID otherID)
    {
        return gAIEnv.pVisionMap?.IsVisible(GetVisionID(), otherID) ?? false;
    }

    // ===================================================================
    // Personally hostile (AIActor.cpp lines 2074-2098)
    // ===================================================================
    public void AddPersonallyHostile(uint hostileID) { m_forcefullyHostiles.Add(hostileID); ReactionChanged(0, IFactionMap.ReactionType.Hostile); }
    public void RemovePersonallyHostile(uint hostileID) { m_forcefullyHostiles.Remove(hostileID); ReactionChanged(0, IFactionMap.ReactionType.Hostile); }
    public void ResetPersonallyHostiles() { m_forcefullyHostiles.Clear(); ReactionChanged(0, IFactionMap.ReactionType.Hostile); }
    public bool IsPersonallyHostile(uint hostileID) { return m_forcefullyHostiles.Contains(hostileID); }

    public void ClearProbableTargets() { m_probableTargets.Clear(); }
    public void AddProbableTarget(CAIObject pTarget) { m_probableTargets.Add(pTarget); }

    // ===================================================================
    // CheckPointInFOV (AIActor.cpp lines 2120-2153)
    // ===================================================================
    protected EFieldOfViewResult CheckPointInFOV(Vec3 point, float sightRange)
    {
        Vec3 eyePosition = GetPos();
        Vec3 eyeToPointDisplacement = point - eyePosition;
        float squaredEyeToPointDistance = eyeToPointDisplacement.GetLengthSquared();

        bool pointIsAtEyePosition = (squaredEyeToPointDistance <= float.Epsilon);
        if (pointIsAtEyePosition)
            return EFieldOfViewResult.eFOV_Outside;

        if (squaredEyeToPointDistance <= sqr(sightRange))
        {
            float primaryFovCos = 1.0f;
            float secondaryFovCos = 1.0f;
            GetSightFOVCos(out primaryFovCos, out secondaryFovCos);

            Vec3 eyeToPointDirection = eyeToPointDisplacement.Normalized();
            Vec3 eyeDirection = GetViewDir();

            float dotProduct = eyeDirection.Dot(eyeToPointDirection);
            if (dotProduct >= secondaryFovCos)
            {
                if (dotProduct >= primaryFovCos)
                    return EFieldOfViewResult.eFOV_Primary;
                else
                    return EFieldOfViewResult.eFOV_Secondary;
            }
        }

        return EFieldOfViewResult.eFOV_Outside;
    }

    // HandlePathDecision (AIActor.cpp lines 2155-2157) — empty in C++
    protected virtual void HandlePathDecision(MNMPathRequestResult result) { }

    // ===================================================================
    // HandleVisualStimulus (AIActor.cpp lines 2159-2199)
    // ===================================================================
    protected virtual void HandleVisualStimulus(SAIEVENT pAIEvent)
    {
        float fGlobalVisualPerceptionScale = gEnv.pAISystem?.GetGlobalVisualScale(this) ?? 1.0f;
        float fVisualPerceptionScale = m_Parameters.m_PerceptionParams.perceptionScale.visual * fGlobalVisualPerceptionScale;
        if (gAIEnv.CVars.IgnoreVisualStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode || fVisualPerceptionScale <= 0.0f)
            return;

        if (gAIEnv.pTargetTrackManager?.IsEnabled() ?? false)
        {
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
                    m_stimulusStartTime = GlobalFunctions.GetAISystem().GetFrameStartTimeSeconds();

                    m_State.eTargetThreat = EAITargetThreat.AITHREAT_AGGRESSIVE;
                    m_State.eTargetType = EAITargetType.AITARGET_VISUAL;

                    CWeakRef<CAIObject> refAttentionTarget = GetWeakRef((CAIObject)pEventOwnerAI);
                    if (!refAttentionTarget.Equals(m_refAttentionTarget))
                    {
                        SetAttentionTarget(refAttentionTarget);
                    }
                }
            }
        }
    }

    // ===================================================================
    // HandleSoundEvent (AIActor.cpp lines 2201-2243)
    // ===================================================================
    protected virtual void HandleSoundEvent(SAIEVENT pAIEvent)
    {
        float fGlobalAudioPerceptionScale = gEnv.pAISystem?.GetGlobalAudioScale(this) ?? 1.0f;
        float fAudioPerceptionScale = m_Parameters.m_PerceptionParams.perceptionScale.audio * fGlobalAudioPerceptionScale;
        if (gAIEnv.CVars.IgnoreSoundStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode || fAudioPerceptionScale <= 0.0f)
            return;

        if (gAIEnv.pTargetTrackManager?.IsEnabled() ?? false)
        {
            Vec3 vMyPos = GetPos();
            float fSoundDistance = (vMyPos - pAIEvent.vPosition).Length() * (1.0f / fAudioPerceptionScale);
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
                    if ((m_State.eTargetType != EAITargetType.AITARGET_MEMORY) && (m_State.eTargetType != EAITargetType.AITARGET_VISUAL))
                    {
                        m_State.nTargetType = ((CAIObject)pEventOwnerAI).GetType();
                        m_stimulusStartTime = GlobalFunctions.GetAISystem().GetFrameStartTimeSeconds();

                        m_State.eTargetThreat = EAITargetThreat.AITHREAT_AGGRESSIVE;
                        m_State.eTargetType = EAITargetType.AITARGET_SOUND;

                        SetAttentionTarget(GetWeakRef((CAIObject)pEventOwnerAI));
                    }
                }
            }
        }
    }

    // ===================================================================
    // HandleBulletRain (AIActor.cpp lines 2245-2262)
    // ===================================================================
    protected virtual void HandleBulletRain(SAIEVENT pAIEvent)
    {
        if (gAIEnv.CVars.IgnoreBulletRainStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode)
            return;

        IAISignalExtraData pData = GlobalFunctions.GetAISystem().CreateSignalExtraData();
        AISignalExtraData pDataImpl = (AISignalExtraData)pData;
        pDataImpl.point = pAIEvent.vPosition;
        pDataImpl.point2 = pAIEvent.vStimPos;
        pDataImpl.nID = pAIEvent.sourceEntityID;
        pDataImpl.fValue = pAIEvent.fThreat;

        SetSignal(0, "OnBulletRain", GetEntity(), pData, gAIEnv.SignalCRCs.m_nOnBulletRain);

        if (gAIEnv.pTargetTrackManager?.IsEnabled() ?? false)
            gAIEnv.pTargetTrackManager.HandleStimulusFromAIEvent(GetAIObjectID(), pAIEvent, TargetTrackHelpers.EEventType.eEST_BulletRain);
    }

    public virtual void CancelRequestedPath(bool actorRemoved) { }

    // ===================================================================
    // IsPointInFOV (AIActor.cpp lines 2269-2275)
    // ===================================================================
    public override EFieldOfViewResult IsPointInFOV(Vec3 vPos, float fDistanceScale = 1.0f)
    {
        float fSightRange = m_Parameters.m_PerceptionParams.sightRange * fDistanceScale;
        return CheckPointInFOV(vPos, fSightRange);
    }

    // ===================================================================
    // GetMovementSpeedRange (AIActor.cpp lines 2277-2358)
    // ===================================================================
    public void GetMovementSpeedRange(float fUrgency, bool bSlowForStrafe, out float normalSpeed, out float minSpeed, out float maxSpeed)
    {
        AgentMovementSpeeds.EAgentMovementUrgency urgency;
        AgentMovementSpeeds.EAgentMovementStance stance;

        bool vehicle = GetType() == (ushort)AIOBJECT_VEHICLE;

        if (fUrgency < 0.5f * (AISPEED_SLOW + AISPEED_WALK))
            urgency = AgentMovementSpeeds.EAgentMovementUrgency.AMU_SLOW;
        else if (fUrgency < 0.5f * (AISPEED_WALK + AISPEED_RUN))
            urgency = AgentMovementSpeeds.EAgentMovementUrgency.AMU_WALK;
        else if (fUrgency < 0.5f * (AISPEED_RUN + AISPEED_SPRINT))
            urgency = AgentMovementSpeeds.EAgentMovementUrgency.AMU_RUN;
        else
            urgency = AgentMovementSpeeds.EAgentMovementUrgency.AMU_SPRINT;

        if (IsAffectedByLight() && m_movementAbility.lightAffectsSpeed)
        {
            if (urgency == AgentMovementSpeeds.EAgentMovementUrgency.AMU_SPRINT)
            {
                EAILightLevel eAILightLevel = GetLightLevel();
                if ((eAILightLevel == EAILightLevel.AILL_DARK) || (eAILightLevel == EAILightLevel.AILL_SUPERDARK))
                    urgency = AgentMovementSpeeds.EAgentMovementUrgency.AMU_RUN;
            }
        }

        SAIBodyInfo bodyInfo = GetBodyInfo();

        switch (bodyInfo.stance)
        {
        case EStance.STANCE_STEALTH: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_STEALTH; break;
        case EStance.STANCE_CROUCH: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_CROUCH; break;
        case EStance.STANCE_PRONE: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_PRONE; break;
        case EStance.STANCE_SWIM: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_SWIM; break;
        case EStance.STANCE_RELAXED: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_RELAXED; break;
        case EStance.STANCE_ALERTED: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_ALERTED; break;
        case EStance.STANCE_LOW_COVER: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_LOW_COVER; break;
        case EStance.STANCE_HIGH_COVER: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_HIGH_COVER; break;
        default: stance = AgentMovementSpeeds.EAgentMovementStance.AMS_COMBAT; break;
        }

        float artificialMinSpeedMult = 1.0f;

        AgentMovementSpeeds.SSpeedRange fwdRange = m_movementAbility.movementSpeeds.GetRange((int)stance, (int)urgency);
        fwdRange.min *= artificialMinSpeedMult;
        normalSpeed = fwdRange.def;
        minSpeed = fwdRange.min;
        maxSpeed = fwdRange.max;

        if (m_movementAbility.directionalScaleRefSpeedMin > 0.0f)
        {
            float desiredSpeed = normalSpeed;
            float desiredTurnSpeed = m_bodyTurningSpeed;
            float travelAngle = Ang3.CreateRadZ(GetEntityDir(), GetMoveDir());

            float refSpeedMin = m_movementAbility.directionalScaleRefSpeedMin;
            float refSpeedMax = m_movementAbility.directionalScaleRefSpeedMax;

            float t = sqr(clamp_tpl((desiredSpeed - refSpeedMin) / (refSpeedMax - refSpeedMin), 0.0f, 1.0f));
            float scaleLimit = clamp_tpl(0.8f * (1 - t) + 0.1f * t, 0.3f, 1.0f);

            float turnSlowDownFactor = (gAIEnv.configuration.eCompatibilityMode == EConfigCompatibilityMode.ECCM_CRYSIS2) ? 0.2f : 0.4f;
            float speedScale = 1.0f - fabsf(desiredTurnSpeed * turnSlowDownFactor) / gf_PI;
            speedScale = clamp_tpl(speedScale, scaleLimit, 1.0f);

            float strafeSlowDown = (gf_PI - fabsf(travelAngle * 0.60f)) / gf_PI;
            strafeSlowDown = clamp_tpl(strafeSlowDown, scaleLimit, 1.0f);

            float slopeSlowDown = (gf_PI - fabsf(DEG2RAD(bodyInfo.slopeAngle) / 12.0f)) / gf_PI;
            slopeSlowDown = clamp_tpl(slopeSlowDown, scaleLimit, 1.0f);

            float scale = min(speedScale, min(strafeSlowDown, slopeSlowDown));

            normalSpeed *= scale;
            Limit(ref normalSpeed, minSpeed, maxSpeed);
        }
    }

    // ===================================================================
    // Look / Body / Move / Go / Speed (AIActor.cpp lines 2360-2425)
    // ===================================================================
    public virtual void ResetLookAt() { m_State.vLookTargetPos = new Vec3(0, 0, 0); }

    public virtual bool SetLookAtPointPos(Vec3 vPoint, bool bPriority = false)
    {
        m_State.vLookTargetPos = vPoint;
        Vec3 vDesired = vPoint - GetPos();
        if (!m_movementAbility.b3DMove) { vDesired.z = 0; }
        vDesired.NormalizeSafe();

        SAIBodyInfo bodyInfo = GetBodyInfo();
        Vec3 vCurrent = bodyInfo.vEyeDirAnim;
        if (!m_movementAbility.b3DMove) { vCurrent.z = 0; }
        vCurrent.NormalizeSafe();

        return 0.98f <= vCurrent.Dot(vDesired);
    }

    public virtual bool SetLookAtDir(Vec3 vDir, bool bPriority = false)
    {
        Vec3 vDirCopy = vDir;
        return vDirCopy.NormalizeSafe() != 0 ? SetLookAtPointPos(GetPos() + vDirCopy * 100.0f) : true;
    }

    public virtual void ResetBodyTargetDir() { m_State.vBodyTargetDir = new Vec3(0, 0, 0); }
    public virtual void SetBodyTargetDir(Vec3 vDir) { m_State.vBodyTargetDir = vDir; }
    public virtual Vec3 GetBodyTargetDir() { return m_State.vBodyTargetDir; }

    public virtual void SetMoveTarget(Vec3 vMoveTarget) { m_State.vMoveTarget = vMoveTarget; }
    public virtual void GoTo(Vec3 vTargetPos) { m_State.vLookTargetPos = vTargetPos; m_State.vMoveTarget = vTargetPos; }
    public virtual void SetSpeed(float fSpeed) { m_State.fMovementUrgency = fSpeed; }

    // ===================================================================
    // Territory (AIActor.cpp lines 2444-2509)
    // ===================================================================
    public virtual void SetTerritoryShapeName(string szName)
    {
        System.Diagnostics.Debug.Assert(szName != null);

        if (m_territoryShapeName != szName)
        {
            m_territoryShapeName = szName;

            if (szName != "<None>")
            {
                m_territoryShape = GlobalFunctions.GetAISystem().GetGenericShapeOfName(szName);

                if (m_territoryShape != null)
                {
                    int size = m_territoryShape.shape.Count;
                    if (size > 8)
                    {
                        AILog.AIWarning("Territory shape {0} for {1} has {2} points. Territories should not have more than 8 points",
                            szName, GetName(), size);
                    }
                }
                else
                {
                    m_territoryShapeName += " (not found)";
                }
            }
            else
            {
                m_territoryShape = null;
            }
        }
    }

    public virtual string GetTerritoryShapeName()
    {
        return (gEnv.IsEditor() && !gEnv.IsEditing())
            ? m_Parameters.m_sTerritoryName
            : m_territoryShapeName;
    }

    public virtual string GetWaveName() { return m_Parameters.m_sWaveName; }

    public virtual bool IsPointInsideTerritoryShape(Vec3 vPos, bool bCheckHeight)
    {
        bool bResult = true;
        SShape pTerritory = GetTerritoryShape();
        if (pTerritory != null)
        {
            bResult = pTerritory.IsPointInsideShape(vPos, bCheckHeight);
        }
        return bResult;
    }

    public virtual bool ConstrainInsideTerritoryShape(ref Vec3 vPos, bool bCheckHeight)
    {
        bool bResult = true;
        SShape pTerritory = GetTerritoryShape();
        if (pTerritory != null)
        {
            bResult = pTerritory.ConstrainPointInsideShape(ref vPos, bCheckHeight);
        }
        return bResult;
    }

    public SShape GetTerritoryShape() { return m_territoryShape; }

    // ===================================================================
    // GetObjectType (AIActor.cpp lines 2514-2527)
    // ===================================================================
    public enum EAIObjectType { AIOT_UNKNOWN, AIOT_PLAYER, AIOT_AGENTSMALL, AIOT_AGENTMED, AIOT_AGENTBIG, AIOT_MAXTYPES }
    public static EAIObjectType GetObjectType(CAIObject ai, ushort type)
    {
        if (type == (ushort)AIOBJECT_PLAYER)
            return EAIObjectType.AIOT_PLAYER;
        else if (type == (ushort)AIOBJECT_ACTOR)
            return EAIObjectType.AIOT_AGENTSMALL;
        else if (type == (ushort)AIOBJECT_VEHICLE)
            return EAIObjectType.AIOT_AGENTMED;
        else
            return EAIObjectType.AIOT_UNKNOWN;
    }

    // ===================================================================
    // GetNavInteraction (AIActor.cpp lines 2532-2572)
    // ===================================================================
    public enum ENavInteraction { NI_IGNORE, NI_STEER, NI_SLOW }
    public static ENavInteraction GetNavInteraction(CAIObject navigator, CAIObject obstacle)
    {
        CAIActor actor = navigator.CastToCAIActor();
        if (actor != null)
        {
            SAIBodyInfo info = actor.GetBodyInfo();
            if (info.GetLinkedVehicleEntity() != null)
                return ENavInteraction.NI_IGNORE;
        }

        ushort navigatorType = navigator.GetType();
        ushort obstacleType = obstacle.GetType();

        bool enemy = navigator.IsHostile(obstacle);

        EAIObjectType navigatorOT = GetObjectType(navigator, navigatorType);
        EAIObjectType obstacleOT = GetObjectType(obstacle, obstacleType);

        switch (navigatorOT)
        {
        case EAIObjectType.AIOT_UNKNOWN:
        case EAIObjectType.AIOT_PLAYER:
            return ENavInteraction.NI_IGNORE;
        case EAIObjectType.AIOT_AGENTSMALL:
            return ENavInteraction.NI_STEER;
        case EAIObjectType.AIOT_AGENTMED:
        case EAIObjectType.AIOT_AGENTBIG:
            if (enemy)
                return obstacleOT >= navigatorOT ? ENavInteraction.NI_STEER : ENavInteraction.NI_IGNORE;
            else
                return ENavInteraction.NI_STEER;
        default:
            AILog.AIWarning("GetNavInteraction: Unhandled switch case {0}", (int)navigatorOT);
            return ENavInteraction.NI_IGNORE;
        }
    }

    // ===================================================================
    // Coordination (AIActor.cpp lines 2574-2595)
    // ===================================================================
    public void CoordinationEntered(string signalName)
    {
        ++m_activeCoordinationCount;
        System.Diagnostics.Debug.Assert(m_activeCoordinationCount < 10);

        SetSignal(AISIGNAL_ALLOW_DUPLICATES, signalName);
    }

    public void CoordinationExited(string signalName)
    {
        if (m_activeCoordinationCount > 0)
        {
            --m_activeCoordinationCount;
        }

        if (m_activeCoordinationCount == 0)
            SetSignal(AISIGNAL_ALLOW_DUPLICATES, signalName);
    }

    // ===================================================================
    // GetInitialPosition (AIActor.cpp lines 2597-2608)
    // ===================================================================
    public bool GetInitialPosition(out Vec3 initialPosition)
    {
        initialPosition = m_initialPosition.pos;
        return m_initialPosition.isValid;
    }

    // ===================================================================
    // StartBehaviorTree / StopBehaviorTree / IsRunningBehaviorTree (AIActor.cpp lines 2610-2628)
    // ===================================================================
    private void StartBehaviorTree(string behaviorName)
    {
        if (GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.StartModularBehaviorTree(GetEntityID(), behaviorName) ?? false)
            m_runningBehaviorTree = true;
    }

    private void StopBehaviorTree()
    {
        if (m_runningBehaviorTree)
        {
            GlobalFunctions.GetAISystem().GetIBehaviorTreeManager()?.StopModularBehaviorTree(GetEntityID());
            m_runningBehaviorTree = false;
        }
    }

    private bool IsRunningBehaviorTree() { return m_runningBehaviorTree; }

    //===================================================================
    // non-virtual simple inline accessors
    //===================================================================
    public virtual DamagePartVector GetDamageParts() { return null; }
    public AgentParameters GetParameters() { return m_Parameters; }
    public virtual AgentMovementAbility GetMovementAbility() { return m_movementAbility; }
    public virtual void SetMovementAbility(AgentMovementAbility parameters) { m_movementAbility = parameters; }
    public virtual bool IsLowHealthPauseActive() { return false; }
    public virtual IEntity GetGrabbedEntity() { return null; }
    public virtual bool IsGrabbedEntityInView(Vec3 pos) { return false; }
    public virtual bool IsDevalued(IAIObject pAIObject) { return false; }
    public virtual bool IsActive() { return m_bEnabled; }
    public virtual bool IsAgent() { return true; }
    public bool IsUsingCombatLight() { return m_usingCombatLight; }
    public float GetCachedWaterOcclusionValue() { return m_cachedWaterOcclusionValue; }
    public override IBlackBoard GetBlackBoard() { return m_blackBoard; }
    public virtual IBlackBoard GetBehaviorBlackBoard() { return m_behaviorBlackBoard; }
    public virtual IAIObject GetAttentionTarget() { return m_refAttentionTarget.GetAIObject(); }
    public virtual EAITargetThreat GetAttentionTargetThreat() { return m_State.eTargetThreat; }
    public virtual EAITargetType GetAttentionTargetType() { return m_State.eTargetType; }
    public virtual EAITargetThreat GetPeakThreatLevel() { return m_State.ePeakTargetThreat; }
    public virtual EAITargetType GetPeakThreatType() { return m_State.ePeakTargetType; }
    public virtual uint GetPeakTargetID() { return m_State.ePeakTargetID; }
    public virtual EAITargetThreat GetPreviousPeakThreatLevel() { return m_State.ePreviousPeakTargetThreat; }
    public virtual EAITargetType GetPreviousPeakThreatType() { return m_State.ePreviousPeakTargetType; }
    public virtual uint GetPreviousPeakTargetID() { return m_State.ePreviousPeakTargetID; }
    public bool CloseContactEnabled() { return !m_bCloseContact; }
    public EAILightLevel GetLightLevel() { return m_lightLevel; }
    public virtual bool IsAffectedByLight() { return m_Parameters.m_PerceptionParams.isAffectedByLight; }
    public virtual NavigationAgentTypeID GetNavigationTypeID() { return m_navigationTypeID; }
    public void SetBehaviorTreeEvaluationMode(BehaviorTreeEvaluationMode mode) { m_behaviorTreeEvaluationMode = mode; }
#if AI_COMPILE_WITH_PERSONAL_LOG
    public PersonalLog GetPersonalLog() { return m_personalLog; }
#endif

    public static CAIActor CastToCAIActorSafe(IAIObject pAI) { return pAI?.CastToCAIActor(); }

    //===================================================================
    // Member fields
    //===================================================================
    public bool m_bCheckedBody;
    public SOBJECTSTATE m_State;
    public AgentParameters m_Parameters = new AgentParameters();
    public AgentMovementAbility m_movementAbility = new AgentMovementAbility();

#if CRYAISYSTEM_DEBUG
    public CValueHistory<float> m_healthHistory;
#endif

    public List<CAIObject> m_probableTargets = new List<CAIObject>();

    protected IAIActorProxy m_proxy;

    protected EAILightLevel m_lightLevel;
    protected bool m_usingCombatLight;
    protected sbyte m_perceptionDisabled;

    protected float m_cachedWaterOcclusionValue;

    protected Vec3 m_vLastFullUpdatePos;
    protected EStance m_lastFullUpdateStance;

    protected CBlackBoard m_blackBoard = new CBlackBoard();
    protected CBlackBoard m_behaviorBlackBoard = new CBlackBoard();

    protected TPerceptionHandlerModifiersVector m_perceptionHandlerModifiers = new TPerceptionHandlerModifiersVector();

    protected HashSet<IActorBehaviorListener> m_behaviorListeners = new HashSet<IActorBehaviorListener>();

    protected BehaviorTreeEvaluationMode m_behaviorTreeEvaluationMode;
    protected SelectionTree m_behaviorSelectionTree;
    protected SelectionVariables m_behaviorSelectionVariables;
    protected BehaviorTreeInstance m_behaviorTreeInstance;

    protected CWeakRef<CAIObject> m_refAttentionTarget = new CWeakRef<CAIObject>();

    protected CTimeValue m_CloseContactTime;
    protected bool m_bCloseContact;

    protected string m_territoryShapeName = "";
    protected SShape m_territoryShape;

    protected float m_bodyTurningSpeed;
    protected Vec3 m_lastBodyDir;

    protected float m_stimulusStartTime;

    protected uint m_activeCoordinationCount;

    protected bool m_observer;

    private float m_FOVPrimaryCos;
    private float m_FOVSecondaryCos;

    private float m_currentCollisionAvoidanceRadiusIncrement;

    private SortedSet<uint> m_forcefullyHostiles = new SortedSet<uint>();

    private SAIBodyInfo m_bodyInfo = new SAIBodyInfo();

    private NavigationAgentTypeID m_navigationTypeID;

#if AI_COMPILE_WITH_PERSONAL_LOG
    private PersonalLog m_personalLog = new PersonalLog();
#endif

    private struct SInitialPosition
    {
        public Vec3 pos;
        public bool isValid;
    }

    private SInitialPosition m_initialPosition;

    private bool m_runningBehaviorTree;

    public enum BehaviorTreeEvaluationMode
    {
        EvaluateWhenVariablesChange,
        EvaluationBlockedUntilBehaviorUnlocks,

        BehaviorTreeEvaluationModeCount,
        FirstBehaviorTreeEvaluationMode = 0
    }

    // C++ helper functions for CWeakRef/CStrongRef — shell implementations
    public static CWeakRef<T> StaticCast<T>(CWeakRef<CAIObject> r) where T : class { return new CWeakRef<T>(); }
    public static CWeakRef<CAIObject> GetWeakRef(CAIObject obj) { return new CWeakRef<CAIObject>(); }

    // GetFloorPos — static helper shell; full implementation pending
    public static bool GetFloorPos(ref Vec3 floorPos, Vec3 pos, float upDist, float downDist, float radius, int collisionEntities) { return false; }

    // Vision change hint flags
    public const uint eChangedPosition = 1u << 0;
    public const uint eChangedOrientation = 1u << 1;
    public const uint eChangedFaction = 1u << 2;
    public const uint eChangedFactionsToObserveMask = 1u << 3;
    public const uint eChangedSightRange = 1u << 4;
    public const uint eChangedFOV = 1u << 5;
    public const uint eChangedSkipList = 1u << 6;

    // Observable type flags from VisionMapTypes.h
    public const uint General = 1u << 0;
    public const uint AliveAgent = 1u << 1;
    public const uint DeadAgent = 1u << 2;
    public const uint Player = 1u << 3;

    // Collision entity filter
    public const int AICE_STATIC = 1;
}

// Forward decls / shells for IAgent.h types — full literal port pending
public interface IAIActor { }
public interface IAIPathAgent
{
    IEntity GetPathAgentEntity() { return null; }
}
public interface IPathFollower
{
    void Advance(float dist) { }
    bool Update(PathFollowResult result, Vec3 curPos, Vec3 curVel, float dt) { return true; }
    float GetDistToEnd(Vec3? pCurPos) { return 0.0f; }
}
public interface IAISignalExtraData
{
    // Fields accessible through default interface members
    float fValue { get; set; }
    int iValue { get; set; }
    int iValue2 { get; set; }
    uint nID { get; set; }
    Vec3 point { get; set; }
    Vec3 point2 { get; set; }
    string sObjectName { get; set; }
}
public interface IActorBehaviorListener
{
    void BehaviorEvent(CAIActor actor, EBehaviorEvent eventArg);
    void BehaviorChanged(CAIActor actor, string current, string previous);
}
public interface IPerceptionHandlerModifier
{
    void DebugDraw(IAIDebugRenderer renderer) { /* default no-op */ }
}

// SOBJECTSTATE — literal port of IAgent.h lines 1524-1747
public struct SOBJECTSTATE
{
    // Target state
    public EAITargetThreat eTargetThreat;
    public EAITargetType eTargetType;
    public int nTargetType;
    public bool bTargetEnabled;

    // Peak tracking
    public EAITargetThreat ePeakTargetThreat;
    public EAITargetType ePeakTargetType;
    public uint ePeakTargetID;
    public EAITargetThreat ePreviousPeakTargetThreat;
    public EAITargetType ePreviousPeakTargetType;
    public uint ePreviousPeakTargetID;

    // Looking / aiming
    public Vec3 vLookTargetPos;
    public Vec3 vAimTargetPos;
    public Vec3 vShootTargetPos;
    public bool aimTargetIsValid;
    public ELookStyle eLookStyle;
    public bool bAllowLowerBodyToTurn;

    // Fire state
    public EAIFireState fire;

    // Movement
    public Vec3 vMoveDir;
    public Vec3 vBodyTargetDir;
    public Vec3 vMoveTarget;
    public float fMovementUrgency;
    public float fDesiredSpeed;
    public Vec3 vForcedNavigation;
    public float fForcedNavigationSpeed;
    // Strafing / path end distance — used by CPuppet::UpdateStrafing
    public bool allowStrafing;
    public float fDistanceToPathEnd;
    // Actor target request — used by PipeUser nav-SO logic
    public EActorTargetPhase curActorTargetPhase;
    public SAIActorTargetRequest actorTargetReq;
    // Continuous motion flag
    public bool continuousMotion;
    // Secondary/melee fire state
    public EAIFireState fireSecondary;
    public EAIFireState fireMelee;

    // Additional fields from IAgent.h SOBJECTSTATE — added for GoalOpTrace/GoalOpStick/Puppet.cpp literal port
    public Vec3 vInflectionPoint;
    public Vec3 vDirOffPath;
    public Vec3 curActorTargetFinishPos;
    public bool aimObstructed;
    public bool bTargetIsGroupTarget;
    public int bodystate;
    public uint eTargetID;
    public EAITargetStuntReaction eTargetStuntReaction;
    public float fDistanceFromTarget;
    public float fTargetSpeed;
    public float lean;
    public uint movementContext;
    public float peekOver;
    public SPredictedCharacterStates predictedCharacterStates;
    public SProjectileInfo projectileInfo;
    public ERequestedGrenadeType requestedGrenadeType;
    public Vec3 vDesiredBodyDirectionAtTarget;
    public Vec3 vTargetPos;
    public uint weaponAccessories;

    // Signals
    public List<AISIGNAL> vSignals;

    // Reset() — per-frame reset of output state (C++ IAgent.h SOBJECTSTATE::Reset)
    public void Reset(bool clearMoveDir = true)
    {
        fire = EAIFireState.eAIFS_Off;
        aimTargetIsValid = false;
        if (clearMoveDir)
            vMoveDir = new Vec3(0, 0, 0);
        fDesiredSpeed = 0;
        fMovementUrgency = 0;
        vForcedNavigation = new Vec3(0, 0, 0);
        fForcedNavigationSpeed = 0;
        vBodyTargetDir = new Vec3(0, 0, 0);
    }

    public void FullReset()
    {
        eTargetThreat = EAITargetThreat.AITHREAT_NONE;
        eTargetType = EAITargetType.AITARGET_NONE;
        nTargetType = -1;
        bTargetEnabled = false;
        ePeakTargetThreat = EAITargetThreat.AITHREAT_NONE;
        ePeakTargetType = EAITargetType.AITARGET_NONE;
        ePeakTargetID = 0;
        ePreviousPeakTargetThreat = EAITargetThreat.AITHREAT_NONE;
        ePreviousPeakTargetType = EAITargetType.AITARGET_NONE;
        ePreviousPeakTargetID = 0;
        vLookTargetPos = new Vec3(0, 0, 0);
        vAimTargetPos = new Vec3(0, 0, 0);
        vShootTargetPos = new Vec3(0, 0, 0);
        aimTargetIsValid = false;
        fire = EAIFireState.eAIFS_Off;
        eLookStyle = ELookStyle.LOOKSTYLE_DEFAULT;
        bAllowLowerBodyToTurn = true;
        vMoveDir = new Vec3(0, 0, 0);
        vBodyTargetDir = new Vec3(0, 0, 0);
        vMoveTarget = new Vec3(0, 0, 0);
        fMovementUrgency = 0;
        fDesiredSpeed = 0;
        vForcedNavigation = new Vec3(0, 0, 0);
        fForcedNavigationSpeed = 0;
        curActorTargetPhase = EActorTargetPhase.eATP_None;
        actorTargetReq = actorTargetReq ?? new SAIActorTargetRequest();
        actorTargetReq.Reset();
        continuousMotion = false;
        if (vSignals == null)
            vSignals = new List<AISIGNAL>();
        else
            vSignals.Clear();
    }

    public void ClearSignals()
    {
        vSignals?.Clear();
    }

    public void Serialize(TSerialize ser)
    {
        // Minimal serialization — full SOBJECTSTATE::Serialize is very large (Phase 11)
        ser.BeginGroup("SOBJECTSTATE");
        uint targetType = (uint)eTargetType;
        uint targetThreat = (uint)eTargetThreat;
        ser.Value("eTargetType", ref targetType);
        ser.Value("eTargetThreat", ref targetThreat);
        if (ser.IsReading()) { eTargetType = (EAITargetType)targetType; eTargetThreat = (EAITargetThreat)targetThreat; }
        ser.Value("nTargetType", ref nTargetType);
        ser.Value("bTargetEnabled", ref bTargetEnabled);
        ser.EndGroup();
    }
}

// SAIBodyInfoQuery — literal port of IAgent.h
public struct SAIBodyInfoQuery
{
    public EStance stance;
    public float lean;
    public float peekOver;
    public bool allowLower;

    public SAIBodyInfoQuery(EStance stance, float lean, float peekOver, bool allowLower)
    {
        this.stance = stance;
        this.lean = lean;
        this.peekOver = peekOver;
        this.allowLower = allowLower;
    }
}

// SAIBodyInfo — literal port of IAgent.h lines 1749-1801
public class SAIBodyInfo
{
    public Vec3 vEyePos;
    public Vec3 vEyeDir;
    public Vec3 vEyeDirAnim;
    public Vec3 vEntityDir;
    public Vec3 vAnimBodyDir;
    public Vec3 vMoveDir;
    public Vec3 vUpDir;
    public Vec3 vFireDir;
    public Vec3 vFirePos;
    public float maxSpeed;
    public float normalSpeed;
    public float minSpeed;
    public EStance stance;
    public AABB stanceSize;
    public AABB colliderSize;
    public bool isAiming;
    public bool isFiring;
    public bool isMoving;
    public float lean;
    public float peekOver;
    public float slopeAngle;
    public uint linkedVehicleEntityId;

    public Vec3 GetEyeDir() { return vEyeDirAnim.IsValid() && !vEyeDirAnim.IsZero() ? vEyeDirAnim : vEyeDir; }
    public Vec3 GetBodyDir() { return vAnimBodyDir.IsValid() && !vAnimBodyDir.IsZero() ? vAnimBodyDir : vEntityDir; }

    public IEntity GetLinkedVehicleEntity()
    {
        if (linkedVehicleEntityId != 0)
            return gEnv.pEntitySystem?.GetEntity(linkedVehicleEntityId);
        return null;
    }
}

// AgentParameters — literal port of AgentParams.h
public partial class AgentParameters
{
    public AgentPerceptionParameters m_PerceptionParams = new AgentPerceptionParameters();
    public string m_sWaveName = "";
    public string m_sTerritoryName = "";
    public float m_fAccuracy = 1.0f;
    public float m_fAttackRange = 50.0f;
    public float m_fPassRadius = 0.3f;
    public float m_fMeleeRange = 1.5f;
    public float m_fMeleeRangeShort = 1.0f;
    public int m_CombatClass = 0;
    public int m_nGroup = 0;
    public uint8 factionID = 0;
    public bool m_bAiIgnoreFgNode = false;
    public bool m_bPerceivePlayer = true;
    public bool m_bInvisible = false;
    public bool m_bCloaked = false;
    public float m_fCloakScale = 0.0f;
    public float m_fCloakScaleTarget = 0.0f;
    // Added for AIPlayer.cpp literal port
    public float m_fLastCloakEventTime = 0.0f;
    // Added for PipeUser.cpp literal port
    public float distanceToCover = 0.5f;
    public float inCoverRadius = 0.3f;
    public float effectiveHighCoverHeight = 1.2f;
    // Added for Puppet.cpp literal port — Phase 2/3
    public bool factionHostility = false;
    public float m_aimTurnSpeed = 1.0f;
    public float m_fAwarenessOfPlayer = 0.0f;
    public float m_fMeleeAngleCosineThreshold = 0.5f;
    public float m_fMeleeHitRange = 2.0f;
    public float m_fireTurnSpeed = 1.0f;
    public float m_lookCombatTurnSpeed = 1.0f;
    public float m_lookIdleTurnSpeed = 1.0f;
    public uint m_weaponAccessories = 0;

    public void Serialize(TSerialize ser)
    {
        // Minimal serialization — full AgentParameters::Serialize is large (Phase 11)
        ser.BeginGroup("AgentParameters");
        ser.Value("m_fAccuracy", ref m_fAccuracy);
        ser.Value("m_fPassRadius", ref m_fPassRadius);
        ser.Value("m_nGroup", ref m_nGroup);
        ser.Value("factionID", ref factionID);
        ser.Value("m_bAiIgnoreFgNode", ref m_bAiIgnoreFgNode);
        ser.Value("m_bInvisible", ref m_bInvisible);
        ser.Value("m_bCloaked", ref m_bCloaked);
        ser.Value("m_fCloakScale", ref m_fCloakScale);
        ser.Value("m_fCloakScaleTarget", ref m_fCloakScaleTarget);
        ser.Value("m_sTerritoryName", ref m_sTerritoryName);
        ser.Value("m_sWaveName", ref m_sWaveName);
        ser.EndGroup();
    }
}

// AgentPerceptionParameters — literal port from AgentParams.h
public class AgentPerceptionParameters
{
    public float sightRange = 50.0f;
    public float sightRangeVehicle = 0.0f;
    public float FOVPrimary = -1.0f;
    public float FOVSecondary = -1.0f;
    public bool isAffectedByLight = false;
    public float minAlarmLevel = 0.0f;
    public float cloakMaxDistStill = 0.0f;
    public float cloakMaxDistMoving = 0.0f;
    public float cloakMaxDistCrouchedAndStill = 0.0f;
    public float cloakMaxDistCrouchedAndMoving = 0.0f;
    // Added for AIPlayer.cpp literal port
    public float collisionReactionScale = 1.0f;

    public struct PerceptionScale
    {
        public float visual;
        public float audio;
    }
    public PerceptionScale perceptionScale = new PerceptionScale { visual = 1.0f, audio = 1.0f };

    // Rate of Death / perception timing fields from AgentParams.h
    public float reactionTime = 1.0f;
    public float forgetfulnessTarget = 10.0f;
    public float forgetfulnessMemory = 20.0f;
    public SPerceptionScale sPerceptionScale; // struct alias
    public struct SPerceptionScale { public float visual; public float audio; }
    // Added for Puppet.cpp literal port
    public float stuntReactionTimeOut = 3.0f;
    public float targetPersistence = 0.0f;
}

// AgentMovementAbility — literal port from IAgent.h lines 610-689
public class AgentMovementAbility
{
    public bool b3DMove = false;
    public bool bUsePathfinder = true;
    public bool usePredictiveFollowing = false;
    public bool allowEntityClampingByAnimation = false;
    public float maxAccel = 0.0f;
    public float maxDecel = 0.0f;
    public float minTurnRadius = 0.0f;
    public float maxTurnRadius = 0.0f;
    public float avoidanceRadius = 0.0f;
    public float pathLookAhead = 0.0f;
    public float pathRadius = 0.0f;
    public float pathSpeedLookAheadPerSpeed = 0.0f;
    public float cornerSlowDown = 0.0f;
    public float slopeSlowDown = 0.0f;
    public float optimalFlightHeight = 0.0f;
    public float minFlightHeight = 0.0f;
    public float maxFlightHeight = 0.0f;
    public float maneuverTrh = 0.0f;
    public float velDecay = 0.0f;
    public float pathFindPrediction = 0.0f;
    public float pathRegenIntervalDuringTrace = 0.0f;
    public bool teleportEnabled = false;
    public bool lightAffectsSpeed = false;
    public bool resolveStickingInTrace = false;
    public float directionalScaleRefSpeedMin = 0.0f;
    public float directionalScaleRefSpeedMax = 0.0f;
    public int avoidanceAbilities = 0;
    public float pushableObstacleWeakAvoidance = 0.0f;
    public float pushableObstacleAvoidanceRadius = 0.0f;
    public float pushableObstacleMassMin = 0.0f;
    public float pushableObstacleMassMax = 0.0f;

    public AgentMovementSpeeds movementSpeeds = new AgentMovementSpeeds();
    public AgentPathfindingProperties pathfindingProperties = new AgentPathfindingProperties();
}

// AgentMovementSpeeds — literal port from IAgent.h lines 514-589
public class AgentMovementSpeeds
{
    public enum EAgentMovementUrgency { AMU_SLOW = 0, AMU_WALK, AMU_RUN, AMU_SPRINT, AMU_NUM_VALUES }
    public enum EAgentMovementStance { AMS_RELAXED = 0, AMS_COMBAT, AMS_STEALTH, AMS_ALERTED, AMS_LOW_COVER, AMS_HIGH_COVER, AMS_CROUCH, AMS_PRONE, AMS_SWIM, AMS_NUM_VALUES }

    public struct SSpeedRange
    {
        public float def, min, max;
    }

    private SSpeedRange[,] speedRanges = new SSpeedRange[(int)EAgentMovementStance.AMS_NUM_VALUES, (int)EAgentMovementUrgency.AMU_NUM_VALUES];

    public SSpeedRange GetRange(int stance, int urgency) { return speedRanges[stance, urgency]; }
    public void SetRange(int stance, int urgency, SSpeedRange range) { speedRanges[stance, urgency] = range; }
}

// AgentPathfindingProperties — from IAgent.h
public class AgentPathfindingProperties
{
    public NavCapMask navCapMask = new NavCapMask();
    public float triangularResistanceFactor = 0.0f;
    public float waypointResistanceFactor = 0.0f;
    public float flightResistanceFactor = 0.0f;
    public float volumeResistanceFactor = 0.0f;
    public float roadResistanceFactor = 0.0f;
    public float waterResistanceFactor = 0.0f;
    public float maxWaterDepth = 0.0f;
    public float minWaterDepth = 0.0f;
    public float exposureFactor = 0.0f;
    public float dangerCost = 0.0f;
    public float zScale = 1.0f;
    public bool avoidObstacles = false;
    public float radius = 0.3f;
}

public class NavCapMask
{
    public uint mask = 0;
    public void Serialize(TSerialize ser) { ser.Value("navCapMask", ref mask); }
    public static implicit operator uint(NavCapMask n) => n?.mask ?? 0;
    public static implicit operator NavCapMask(uint v) => new NavCapMask { mask = v };
}

// SPredictedCharacterStates — shell from IAgent.h
public class SPredictedCharacterStates
{
    public const int maxStates = 32;
    public int nStates;
    public struct SPredictedCharacterState
    {
        public Vec3 position; public Vec3 velocity; public float dt;
        public void Set(Vec3 pos, Vec3 vel, float deltaT) { position = pos; velocity = vel; dt = deltaT; }
    }
    public SPredictedCharacterState[] states = new SPredictedCharacterState[maxStates];
}

// SProjectileInfo — shell from IAgent.h
public struct SProjectileInfo
{
    public Vec3 vPosition;
    public float fSpeed;
    public ERequestedGrenadeType grenadeType;
    public bool trackingId;
    public void Reset() { vPosition = new Vec3(0, 0, 0); fSpeed = 0; grenadeType = ERequestedGrenadeType.eRGT_INVALID; trackingId = false; }

    // Implicit conversion to/from SFireCommandProjectileInfo
    public static implicit operator SFireCommandProjectileInfo(SProjectileInfo p) => new SFireCommandProjectileInfo { vShootPos = p.vPosition, fSpeed = p.fSpeed, grenadeType = p.grenadeType };
    public static implicit operator SProjectileInfo(SFireCommandProjectileInfo p) => new SProjectileInfo { vPosition = p.vShootPos, fSpeed = p.fSpeed, grenadeType = p.grenadeType };
}

public class NavigationBlockers
{
    public void Add(NavigationBlocker blocker) { /* shell */ }
}
public partial class NavigationBlocker { }
public class PathfindRequest
{
    public Vec3 startPos;
    public Vec3 endPos;
}
public class CTargetPointRequest
{
    public void SetResult(ETriState r) { result = r; }
    public int itIndex;
    public int itBeforeIndex;
    public Vec3 splitPoint;
    public Vec3 targetPoint;
    public ETriState result;
    public int pathID = -1;
}
public class MNMPathRequestResult
{
    public bool HasPathBeenFound() { return result == EMNMPathResult.eMNMPR_Success; }
    public CNavPath pPath;
    public EMNMPathResult result;
}
public struct CloakObservability
{
    public float cloakMaxDistStill;
    public float cloakMaxDistMoving;
    public float cloakMaxDistCrouchedAndStill;
    public float cloakMaxDistCrouchedAndMoving;
}
public struct NavigationAgentTypeID { public uint id; }
public enum ETriState { eTS_invalid, eTS_false, eTS_maybe, eTS_true }
public enum EAILightLevel { AILL_NONE, AILL_LIGHT, AILL_MEDIUM, AILL_DARK, AILL_SUPERDARK, AILL_LAST }
public enum EBehaviorEvent { BE_None, BE_BehaviorChanged, BE_BehaviorEvent }
// EObjectUpdate already declared in AIObject.cs

// Forward decl shell for BehaviorTree::BehaviorTreeInstance — full literal port pending Phase 8
public class BehaviorTreeInstance { }

// SmartScriptTable already defined in CryCommon/IScriptSystem.cs
// ECompatibilityMode already defined in Configuration.cs
