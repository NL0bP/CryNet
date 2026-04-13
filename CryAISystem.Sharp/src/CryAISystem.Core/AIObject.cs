// Literal port of dev/Code/CryEngine/CryAISystem/AIObject.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : interface for the CAIObject class.

using CryAISystem.Navigation.NavigationSystem;

namespace CryAISystem;

/*! Basic ai object class. Defines a framework that all puppets and points of interest later follow. */
public class CAIObject : IAIObject
{
    // only allow these classes to create AI objects.
    // friend class CAIObjectManager;
    // friend struct SAIObjectCreationHelper;
    public CAIObject()
    {
        m_vPosition = new Vec3(0, 0, 0);
        m_entityID = 0;
        m_bEnabled = true;
        m_lastNavNodeIndex = 0;
        m_fRadius = 0.0f;
        m_pFormation = null;
        m_nObjectType = 0;
        m_objectSubType = ESubType.STP_NONE;
        m_bUpdatedOnce = false;
        m_vFirePosition = new Vec3(0, 0, 0);
        m_vFireDir = new Vec3(0, 0, 0);
        m_expectedPhysicsPosFrameId = -1;
        m_vExpectedPhysicsPos = new Vec3(0, 0, 0);
        m_vBodyDir = new Vec3(0, 0, 0);
        m_vEntityDir = new Vec3(0, 0, 0);
        m_vMoveDir = new Vec3(0, 0, 0);
        m_vView = new Vec3(0, 0, 0);
        m_vLastPosition = new Vec3(0, 0, 0);
        m_groupId = -1;
        m_factionID = IFactionMap.InvalidFactionID;
        m_isThreateningForHostileFactions = true;
        m_bTouched = false;
        m_observable = false;
        m_createdFromPool = false;
        m_serialize = true;

        AILog.AILogComment("CAIObject ({0})", this);
    }
    // ~CAIObject() — translated as Dispose-equivalent for the literal release of formation/observable
    ~CAIObject()
    {
        AILog.AILogComment("~CAIObject  {0} ({1})", GetName(), this);

        SetObservable(false);

        ReleaseFormation();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //IAIObject interfaces//////////////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////////////////////////////////////////////////////
    //Startup/shutdown//////////////////////////////////////////////////////////////////////
    public virtual void Reset(EObjectResetType type)
    {
        ReleaseFormation();

        m_bEnabled = true;
        m_bUpdatedOnce = false;
        m_bTouched = false;

        m_lastNavNodeIndex = 0;

        // grenades and rockets are always observable
        if ((m_nObjectType == (ushort)EAIObjectType.AIOBJECT_RPG) || (m_nObjectType == (ushort)EAIObjectType.AIOBJECT_GRENADE))
            SetObservable(true);
    }

    public virtual void Release()
    {
        // AI objects relating to pooled entities are
        // handled by CAIObjectManager and shouldn't be deleted directly
        if (m_createdFromPool)
        {
            gAIEnv.pAIObjectManager.ReleasePooledObject(this);
        }
        else
        {
            // delete this; — managed in C#
        }
    }

    // "true" if method Update(EObjectUpdate type) has been invoked AT LEAST once
    public virtual bool IsUpdatedOnce() { return m_bUpdatedOnce; }

    public virtual bool IsEnabled() { return m_bEnabled; }
    public virtual void Event(ushort eType, SAIEVENT pEvent)
    {
        switch ((EAIEvent)eType)
        {
            case EAIEvent.AIEVENT_DISABLE:
                {
                    SetObservable(false);
                    m_bEnabled = false;
                }
                break;
            case EAIEvent.AIEVENT_ENABLE:
                m_bEnabled = true;
                break;
            case EAIEvent.AIEVENT_SLEEP:
                m_bEnabled = false;
                break;
            case EAIEvent.AIEVENT_WAKEUP:
                m_bEnabled = true;
                break;
            default:
                break;
        }
    }

    public virtual void EntityEvent(SEntityEvent eventArg)
    {
        switch (eventArg.eventType)
        {
            case EEntityEvent.ENTITY_EVENT_ATTACH_THIS:
            case EEntityEvent.ENTITY_EVENT_DETACH_THIS:
            case EEntityEvent.ENTITY_EVENT_ENABLE_PHYSICS:
                UpdateObservableSkipList();
                break;
            default:
                break;
        }
    }
    //Startup/shutdown//////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////////////////////////////////////////////////////
    //Basic properties//////////////////////////////////////////////////////////////////////
    public virtual uint GetAIObjectID()
    {
        uint nID = m_refThis.GetObjectID();
        System.Diagnostics.Debug.Assert(nID != 0);
        return nID;
    }

    public virtual VisionID GetVisionID() { return m_visionID; }
    public virtual void SetObservable(bool observable)
    {
        if (m_observable != observable)
        {
            if (observable)
            {
                ObservableParams observableParams = new ObservableParams();
                observableParams.entityId = GetEntityID();
                observableParams.faction = GetFactionID();
                observableParams.typeMask = GetObservableTypeMask();

                GetObservablePositions(observableParams);

                PhysSkipList skipList = new PhysSkipList();
                GetPhysicalSkipEntities(skipList);

                observableParams.skipListSize = (uint)System.Math.Min((int)skipList.Count, ObservableParams.MaxSkipListSize);
                for (nuint i = 0; i < (nuint)observableParams.skipListSize; ++i)
                    observableParams.skipList[(int)i] = skipList[(int)i];

                // Marcio: Should check for associated objects and add them here too?
                if (m_visionID.id == 0)
                    m_visionID = gAIEnv.pVisionMap.CreateVisionID(GetName());

                gAIEnv.pVisionMap.RegisterObservable(m_visionID, observableParams);
            }
            else
            {
                if (m_visionID.id != 0)
                    gAIEnv.pVisionMap.UnregisterObservable(m_visionID);
            }

            m_observable = observable;
        }
    }

    public virtual bool IsObservable() { return m_observable; }

    public virtual void GetObservablePositions(ObservableParams observableParams)
    {
        observableParams.observablePositionsCount = 1;
        observableParams.observablePositions[0] = GetPos();
    }

    public virtual uint GetObservableTypeMask()
    {
        return (uint)EObservableType.General;
    }

    public virtual void GetPhysicalSkipEntities(PhysSkipList skipList)
    {
        IPhysicalEntity physics = GetPhysics();
        if (physics != null)
            stl.push_back_unique(skipList, physics);

        IPhysicalEntity charPhysics = GetPhysics(true);
        if (charPhysics != null)
            stl.push_back_unique(skipList, charPhysics);
    }

    public void UpdateObservableSkipList()
    {
        if (m_observable)
        {
            PhysSkipList skipList = new PhysSkipList();
            GetPhysicalSkipEntities(skipList);

            ObservableParams observableParams = new ObservableParams();
            observableParams.skipListSize = (uint)System.Math.Min((int)skipList.Count, ObservableParams.MaxSkipListSize);
            for (nuint i = 0; i < (nuint)observableParams.skipListSize; ++i)
                observableParams.skipList[(int)i] = skipList[(int)i];

            gAIEnv.pVisionMap.ObservableChanged(GetVisionID(), observableParams, (uint)EChangeHint.eChangedSkipList);
        }
    }

    public virtual void SetName(string pName) { m_name = pName; }
    public virtual string GetName() { return m_name; }

    public virtual ushort GetAIType() { return m_nObjectType; }
    public virtual ESubType GetSubType() { return m_objectSubType; }
    public virtual void SetType(ushort type) { m_nObjectType = type; }

    public virtual void SetPos(Vec3 pos, Vec3 dirForw = default)
    {
        // CCCPOINT(CAIObject_SetPos);

#if CRYAISYSTEM_DEBUG
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
        {
            AILog.AIWarning("NotANumber tried to be set for position of AI entity {0}", GetName());
            return;
        }
#endif

        System.Diagnostics.Debug.Assert(pos.IsValid());
        System.Diagnostics.Debug.Assert(dirForw.IsValid());
        System.Diagnostics.Debug.Assert(dirForw.IsUnit() || dirForw.IsZero());

        if (!Vec3Helpers.IsEquivalent(m_vLastPosition, pos, Vec3Helpers.VEC_EPSILON))
        {
            m_vLastPosition = m_vPosition;

            IEntity pEntity = GetEntity();
            if (pEntity != null)
                GetAISystem().NotifyAIObjectMoved(pEntity, new SEntityEvent(EEntityEvent.ENTITY_EVENT_XFORM));

            m_lastNavNodeIndex = 0;
        }

        if (GetProxy() == null)
        {
            m_vMoveDir = dirForw;
            m_vBodyDir = dirForw;
            m_vEntityDir = dirForw;
            m_vView = dirForw;
        }

        m_vPosition = pos;

        if (m_observable)
        {
            ObservableParams observableParams = new ObservableParams();
            GetObservablePositions(observableParams);
            gAIEnv.pVisionMap.ObservableChanged(m_visionID, observableParams, (uint)EChangeHint.eChangedPosition);
        }
    }

    public virtual Vec3 GetPos() { m_bTouched = true; return m_vPosition; }

    public virtual Vec3 GetPosInNavigationMesh(uint agentTypeID)
    {
        Vec3 outputLocation = GetPos();
        NavigationMeshID meshID = gAIEnv.pNavigationSystem.GetEnclosingMeshID(new NavigationAgentTypeID { id = agentTypeID }, outputLocation);
        if (meshID.id != 0)
        {
            NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(meshID);
            float strictVerticalRange = 1.0f;
            float fPushUp = 0.2f;
            float pushUp = fPushUp;
            uint triangleID = 0;
            Vec3 location_t = new Vec3(outputLocation.x, outputLocation.y, outputLocation.z + pushUp);
            triangleID = mesh.grid.GetTriangleAt(location_t, strictVerticalRange, strictVerticalRange);
            if (triangleID == 0)
            {
                float largeVerticalRange = 6.0f;
                float largeHorizontalRange = 3.0f;
                Vec3 closestLocation;
                float distSq;
                triangleID = mesh.grid.GetClosestTriangle(location_t, largeVerticalRange, largeHorizontalRange, out distSq, out closestLocation);
                if (triangleID != 0)
                {
                    outputLocation = closestLocation;
                    outputLocation = new Vec3(outputLocation.x, outputLocation.y, outputLocation.z + fPushUp);
                }
            }
        }
        return outputLocation;
    }

    public virtual void SetRadius(float fRadius) { m_fRadius = fRadius; }
    public virtual float GetRadius() { return m_fRadius; }

    public virtual Vec3 GetBodyDir() { return m_vBodyDir; }
    public virtual void SetBodyDir(Vec3 dir) { m_vBodyDir = dir; }

    public virtual Vec3 GetViewDir() { return m_vView; }
    public virtual void SetViewDir(Vec3 dir) { m_vView = dir; }
    public virtual EFieldOfViewResult IsPointInFOV(Vec3 pos, float distanceScale = 1.0f)
    {
        return EFieldOfViewResult.eFOV_Outside;
    }

    public virtual Vec3 GetEntityDir() { return m_vEntityDir; }
    public virtual void SetEntityDir(Vec3 dir) { m_vEntityDir = dir; }

    public virtual Vec3 GetMoveDir() { return m_vMoveDir; }
    public virtual void SetMoveDir(Vec3 dir) { m_vMoveDir = dir; }
    public virtual Vec3 GetVelocity()
    {
        IAIActorProxy pProxy = GetProxy();
        if (pProxy == null)
            return new Vec3(0, 0, 0);

        IPhysicalEntity pPhysicalEntity = pProxy.GetPhysics();
        if (pPhysicalEntity == null)
            return new Vec3(0, 0, 0);

        // CCCPOINT(CAIObject_GetVelocity);

        // if living entity return that vel since that is actually the rate of change of position
        pe_status_living status = new pe_status_living();
        if (pPhysicalEntity.GetStatus(status) != 0)
            return status.vel;

        pe_status_dynamics dSt = new pe_status_dynamics();
        pPhysicalEntity.GetStatus(dSt);

        return dSt.v;
    }

    public virtual nuint GetNavNodeIndex() { return m_lastNavNodeIndex; }
    //Basic properties//////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////////////////////////////////////////////////////
    //Serialize/////////////////////////////////////////////////////////////////////////////
    public virtual void SetEntityID(uint ID)
    {
        AILog.AIAssert((ID == 0) || (gEnv.pEntitySystem.GetEntity(ID) != null));

        m_entityID = ID;
    }
    public virtual uint GetEntityID() { return m_entityID; }
    public virtual IEntity GetEntity()
    {
        IEntitySystem pEntitySystem = gEnv.pEntitySystem;
        return pEntitySystem != null ? pEntitySystem.GetEntity(m_entityID) : null;
    }

    public virtual void Serialize(TSerialize ser)
    {
        ser.Value("m_refThis", ref m_refThis);

        ser.Value("m_bEnabled", ref m_bEnabled);
        ser.Value("m_bTouched", ref m_bTouched);

        // Do not cache the result of GetNavNodeIndex across serialisation
        if (ser.IsReading())
            m_lastNavNodeIndex = 0;
        ser.Value("m_vLastPosition", ref m_vLastPosition);

        int formationIndex = CFormation.INVALID_FORMATION_ID;
        if (m_pFormation != null && ser.IsWriting())
        {
            formationIndex = m_pFormation.GetId();
        }
        ser.Value("formationIndex", ref formationIndex);
        if (formationIndex != CFormation.INVALID_FORMATION_ID && ser.IsReading())
        {
            m_pFormation = GetAISystem().GetFormation(formationIndex);
        }

        // m_movementAbility.Serialize(ser); // not needed as ability is constant
        ser.Value("m_bUpdatedOnce", ref m_bUpdatedOnce);
        // todo m_listWaitGoalOps
        ser.Value("m_nObjectType", ref m_nObjectType);
        ser.EnumValue("m_objectSubType", ref m_objectSubType, ESubType.STP_NONE, ESubType.STP_MAXVALUE);
        ser.Value("m_vPosition", ref m_vPosition);
        ser.Value("m_vFirePosition", ref m_vFirePosition);
        ser.Value("m_vFireDir", ref m_vFireDir);
        ser.Value("m_vBodyDir", ref m_vBodyDir);
        ser.Value("m_vEntityDir", ref m_vEntityDir);
        ser.Value("m_vMoveDir", ref m_vMoveDir);
        ser.Value("m_vView", ref m_vView);
        // todo m_pAssociation
        ser.Value("m_fRadius", ref m_fRadius);
        ser.Value("m_groupId", ref m_groupId);
        m_refAssociation.Serialize(ser, "m_refAssociation");
        // m_listWaitGoalOps is not serialized but recreated after serializing goal pipe, when reading, in CPipeUser::Serialize()
        ser.Value("m_name", ref m_name);

        ser.Value("m_entityID", ref m_entityID);

        ser.Value("m_factionID", ref m_factionID);
        ser.Value("m_isThreateningForHostileFactions", ref m_isThreateningForHostileFactions);

        bool observable = m_observable;
        ser.Value("m_observable", ref observable);
        if (ser.IsReading())
        {
            SetObservable(observable);

#if CRYAISYSTEM_DEBUG
            ResetRecorderUnit();
#endif
        }
    }

    public virtual void PostSerialize() { }

    public bool ShouldSerialize()
    {
        if (!m_serialize)
            return false;

        if (m_createdFromPool)
            return false;

        if (gAIEnv.CVars.ForceSerializeAllObjects == 0)
        {
            IEntity pEntity = GetEntity();
            return pEntity != null ? ((pEntity.GetFlags() & (uint)EEntityFlag.ENTITY_FLAG_NO_SAVE) == 0) : true;
        }

        return true;
    }
    public void SetShouldSerialize(bool ser) { m_serialize = ser; }
    public bool IsFromPool() { return m_createdFromPool; }
    //Serialize/////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////////////////////////////////////////////////////
    //Starting to assume WAY to many conflicting things about possible derived classes//////
    public virtual void SetFirePos(Vec3 pos) { m_vFirePosition = pos; }
    public virtual Vec3 GetFirePos() { return m_vFirePosition; }
    public virtual IBlackBoard GetBlackBoard() { return null; }

    public virtual uint8 GetFactionID() { return m_factionID; }

    public virtual void SetFactionID(uint8 factionID)
    {
        if (m_factionID != factionID)
        {
            m_factionID = factionID;
            if (IsObservable())
            {
                ObservableParams parameters = new ObservableParams();
                parameters.faction = GetFactionID();

                gAIEnv.pVisionMap.ObservableChanged(GetVisionID(), parameters, (uint)EChangeHint.eChangedFaction);
            }
        }
    }

    public virtual void SetGroupId(int id) { m_groupId = id; }
    public virtual int GetGroupId() { return m_groupId; }
    public virtual bool IsHostile(IAIObject pOther, bool bUsingAIIgnorePlayer = true)
    {
        if (pOther == null)
            return false;

        if (!IsThreateningForHostileFactions())
            return false;

        if (gAIEnv.pFactionMap.GetReaction(this.GetFactionID(), pOther.GetFactionID()) == IFactionMap.ReactionType.Hostile)
            return true;

        ushort nType = ((CAIObject)pOther).GetType();
        return (nType == (ushort)EAIObjectType.AIOBJECT_GRENADE || nType == (ushort)EAIObjectType.AIOBJECT_RPG);
    }
    public virtual void SetThreateningForHostileFactions(bool threatening) { m_isThreateningForHostileFactions = threatening; }
    public virtual bool IsThreateningForHostileFactions() { return m_isThreateningForHostileFactions; }

    public virtual bool IsTargetable()
    {
        return IsEnabled();
    }

    public virtual uint GetPerceivedEntityID() { return m_entityID; }

    public virtual void SetProxy(IAIActorProxy proxy)
    {
        System.Diagnostics.Debug.Assert((m_nObjectType != (ushort)EAIObjectType.AIOBJECT_WAYPOINT) &&
               (m_nObjectType != (ushort)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT));
    }

    public virtual IAIActorProxy GetProxy() { return null; }
    ////////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////////////////////////////////////////////////////
    //Formations////////////////////////////////////////////////////////////////////////////
    public virtual bool CreateFormation(uint nCrc32ForFormationName, Vec3 vTargetPos = default)
    {
        string sFormationName = GetAISystem().GetFormationNameFromCRC32(nCrc32ForFormationName);
        return CreateFormation(sFormationName, vTargetPos);
    }

    public virtual bool CreateFormation(string szName, Vec3 vTargetPos = default)
    {
        if (m_pFormation != null)
        {
            GetAISystem().ReleaseFormation(WeakRefHelpers.GetWeakRef(this), true);
        }

        m_pFormation = null;

        if (szName == null)
            return false;

        // CCCPOINT(CAIObject_CreateFormation);

        m_pFormation = GetAISystem().CreateFormation(WeakRefHelpers.GetWeakRef(this), szName, vTargetPos);
        return (m_pFormation != null);
    }

    public virtual bool HasFormation() { return m_pFormation != null; }

    public virtual bool ReleaseFormation()
    {
        if (m_pFormation != null)
        {
            // CCCPOINT(CAIObject_ReleaseFormation);
            GetAISystem().ReleaseFormation(WeakRefHelpers.GetWeakRef(this), true);
            m_pFormation = null;
            return true;
        }
        return false;
    }

    public virtual void CreateGroupFormation(IAIObject pLeader) { }
    public virtual void SetFormationPos(Vec2 v2RelPos) { }
    public virtual void SetFormationLookingPoint(Vec3 v3RelPos) { }
    public virtual void SetFormationAngleThreshold(float fThresholdDegrees) { }
    public virtual Vec3 GetFormationPos() { return new Vec3(0, 0, 0); }
    public virtual Vec3 GetFormationVelocity()
    {
        CAISystem pAISystem = GetAISystem();

        CLeader pLeader = pAISystem.GetLeader(GetGroupId());
        if (pLeader != null)
        {
            CAIObject pAIObject = pLeader.GetFormationOwner().GetAIObject();
            if (pAIObject != null)
            {
                return pAIObject.GetVelocity();
            }
        }

        IAIObject pBoss = pAISystem.GetNearestObjectOfTypeInRange(this, (uint)EAIObjectType.AIOBJECT_ACTOR, 0, 20.0f, (uint)(EAIFAFlags.AIFAF_HAS_FORMATION | EAIFAFlags.AIFAF_SAME_GROUP_ID));
        if (pBoss != null)
        {
            Vec3 bossVelocity = ((CAIObject)pBoss).GetVelocity();
            return bossVelocity;
        }

        return new Vec3(0, 0, 0);
    }
    public virtual Vec3 GetFormationLookingPoint() { return new Vec3(0, 0, 0); }
    public virtual void SetFormationUpdateSight(float range, float minTime, float maxTime)
    {
        if (m_pFormation != null)
            m_pFormation.SetUpdateSight(range, minTime, maxTime);
    }
    ////////////////////////////////////////////////////////////////////////////////////////

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //IAIRecordable interfaces////////////////////////////////////////////////////////////////////////////////////////
    public virtual void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData pEventData) { }
    public virtual void RecordSnapshot() { }
    public virtual IAIDebugRecord GetAIDebugRecord()
    {
#if CRYAISYSTEM_DEBUG
        return GetOrCreateRecorderUnit(this);
#else
        return null;
#endif
    }

#if CRYAISYSTEM_DEBUG
    public CRecorderUnit CreateAIDebugRecord()
    {
        return GetOrCreateRecorderUnit(this, true);
    }
#endif
    ////////////////////////////////////////////////////////////////////////////////////////

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //CAIObject interfaces////////////////////////////////////////////////////////////////////////////////////////////
    public virtual IPhysicalEntity GetPhysics(bool wantCharacterPhysics = false)
    {
        IEntity pEntity = GetEntity();
        return pEntity != null ? pEntity.GetPhysics() : null;
    }

    public virtual void SetFireDir(Vec3 dir) { m_vFireDir = dir; }
    public virtual Vec3 GetFireDir() { return m_vFireDir; }
    public virtual Vec3 GetShootAtPos() { return m_vPosition; }

    public virtual CWeakRef<CAIObject> GetAssociation() { return m_refAssociation; }
    public virtual void SetAssociation(CWeakRef<CAIObject> refAssociation) { m_refAssociation = refAssociation; }

    public virtual void OnObjectRemoved(CAIObject pObject) { }

    public Vec3 GetLastPosition() { return m_vLastPosition; }

    public Vec3 GetPhysicsPos()
    {
        IEntity pEntity = GetEntity();

        if (pEntity != null)
        {
            if ((m_expectedPhysicsPosFrameId == gEnv.pRenderer.GetFrameID(false)))
            {
                return m_vExpectedPhysicsPos;
            }
            else
            {
                return pEntity.GetWorldPos();
            }
        }
        else
        {
            return GetPos();
        }
    }

    public void SetExpectedPhysicsPos(Vec3 pos)
    {
        // CCCPOINT(CAIObject_SetExpectedPhysicsPos);

#if CRYAISYSTEM_DEBUG
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
        {
            AILog.AIWarning("NotANumber tried to be set for expected position of AI entity {0}", GetName());
            return;
        }
#endif

        System.Diagnostics.Debug.Assert(pos.IsValid());

        m_expectedPhysicsPosFrameId = gEnv.pRenderer.GetFrameID(false);
        m_vExpectedPhysicsPos = pos;
    }

    public string GetEventName(ushort eType)
    {
        switch (eType)
        {
            case (ushort)EAIEvent.AIEVENT_ONVISUALSTIMULUS:
                return "OnVisualStimulus";
            case (ushort)EAIEvent.AIEVENT_ONSOUNDEVENT:
                return "OnSoundEvent";
            case (ushort)EAIEvent.AIEVENT_AGENTDIED:
                return "AgentDied";
            case (ushort)EAIEvent.AIEVENT_SLEEP:
                return "Sleep";
            case (ushort)EAIEvent.AIEVENT_WAKEUP:
                return "Wakeup";
            case (ushort)EAIEvent.AIEVENT_ENABLE:
                return "Enable";
            case (ushort)EAIEvent.AIEVENT_DISABLE:
                return "Disable";
            case (ushort)EAIEvent.AIEVENT_PATHFINDON:
                return "PathfindOn";
            case (ushort)EAIEvent.AIEVENT_PATHFINDOFF:
                return "PathfindOff";
            case (ushort)EAIEvent.AIEVENT_CLEAR:
                return "Clear";
            case (ushort)EAIEvent.AIEVENT_DROPBEACON:
                return "DropBeacon";
            case (ushort)EAIEvent.AIEVENT_USE:
                return "Use";
            default:
                return "undefined";
        }
    }

    public void SetSubType(ESubType type) { m_objectSubType = type; }
    public ushort GetType() { return m_nObjectType; }

    public void SetSelfReference(CWeakRef<CAIObject> refArg)
    {
        System.Diagnostics.Debug.Assert(m_refThis.IsNil());
        m_refThis = refArg;
    }

    public CWeakRef<CAIObject> GetSelfReference()
    {
        System.Diagnostics.Debug.Assert(!m_refThis.IsNil());
        return m_refThis;
    }

    public bool HasSelfReference() { return m_refThis.IsSet(); }
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // Cast helpers (kept for downstream forward shells)
    public CAIActor CastToCAIActor() { return this as CAIActor; }
    public virtual bool IsAgent() { return CastToCAIActor() != null; }
    public virtual CPipeUser CastToCPipeUser() { return null; }
    public virtual CPuppet CastToCPuppet() { return null; }
    // Patches added session 4 — port of IAIObject.h CastTo* methods (the C++ uses _fastcast bools;
    // C# uses safe `as` casts, which produce identical observable results).
    public virtual CAIVehicle CastToCAIVehicle() { return null; }
    public virtual CAIFlyingVehicle CastToCAIFlyingVehicle() { return null; }
    public virtual CAIPlayer CastToCAIPlayer() { return null; }
    public virtual CLeader CastToCLeader() { return null; }

    // Literal port of IAIObject.h _fastcast bitfields (lines 73-84). C++ packs them into a single
    // int via bitfield syntax; the C# port uses individual bool fields with the same names so
    // call sites like `_fastcast_CAIFlyingVehicle = true` translate verbatim.
    protected bool _fastcast_CAIActor;
    protected bool _fastcast_CAIAttribute;
    protected bool _fastcast_CAIPlayer;
    protected bool _fastcast_CLeader;
    protected bool _fastcast_CPipeUser;
    protected bool _fastcast_CPuppet;
    protected bool _fastcast_CAIVehicle;
    protected bool _fastcast_CAIFlyingVehicle;

    protected void SetVisionID(VisionID visionID) { m_visionID = visionID; }

    protected bool m_bEnabled;
    protected ushort m_nObjectType;
    protected ESubType m_objectSubType;
    protected int m_groupId;
    protected float m_fRadius;

    protected uint m_entityID;

    private CWeakRef<CAIObject> m_refThis = new CWeakRef<CAIObject>();

    private Vec3 m_vPosition;
    private Vec3 m_vEntityDir;
    private Vec3 m_vBodyDir;
    private Vec3 m_vMoveDir;
    private Vec3 m_vView;

    protected /*mutable*/ nuint m_lastNavNodeIndex;

    public bool m_bUpdatedOnce;
    public /*mutable*/ bool m_bTouched;

    public Vec3 m_vLastPosition;
    public CFormation m_pFormation;

    private Vec3 m_vFirePosition;
    private Vec3 m_vFireDir;

    private Vec3 m_vExpectedPhysicsPos;
    private int m_expectedPhysicsPosFrameId;

    private VisionID m_visionID;
    private uint8 m_factionID;
    private bool m_isThreateningForHostileFactions;
    private bool m_observable;

    public bool m_createdFromPool; // C++ uses friend access via SAIObjectCreationHelper / CAIObjectManager — promoted to public so the literal port can write `pObject.m_createdFromPool = true`
    private bool m_serialize;

    private string m_name = "";

    protected CWeakRef<CAIObject> m_refAssociation = new CWeakRef<CAIObject>();
}

// CAIActor literal port lives in AIActor.cs.
// CPipeUser literal port lives in PipeUser.cs.
// CPuppet literal port lives in Puppet.cs.

// Forward decls for IAgent.h types — partial literal ports of CryCommon needed by AIObject.cpp
public enum EObjectResetType { AIOBJRESET_INIT, AIOBJRESET_SHUTDOWN }
public enum EObjectUpdate { AIUPDATE_FULL, AIUPDATE_DRY }
// Literal port of IAIObject.h ESubType — completed in session 4 to match the C++ ordering exactly.
public enum ESubType
{
    STP_NONE,
    STP_FORMATION,
    STP_REFPOINT,
    STP_LOOKAT,
    STP_CAR,
    STP_BOAT,
    STP_HELI,
    STP_2D_FLY,
    STP_SOUND,
    STP_GUNFIRE,
    STP_MEMORY,
    STP_BEACON,
    STP_SPECIAL,
    STP_ANIM_TARGET,
    STP_HELICRYSIS2,
    STP_MAXVALUE
}
public enum EFieldOfViewResult { eFOV_Outside, eFOV_Primary, eFOV_Secondary }

// EAIEvent — literal port of IAgent.h #define values (literal C++ defines preserved)
public enum EAIEvent : ushort
{
    AIEVENT_ONVISUALSTIMULUS = 2,
    AIEVENT_ONSOUNDEVENT = 4,
    AIEVENT_AGENTDIED = 5,
    AIEVENT_SLEEP = 6,
    AIEVENT_WAKEUP = 7,
    AIEVENT_ENABLE = 8,
    AIEVENT_DISABLE = 9,
    AIEVENT_PATHFINDON = 11,
    AIEVENT_PATHFINDOFF = 12,
    AIEVENT_CLEAR = 15,
    AIEVENT_DROPBEACON = 17,
    AIEVENT_USE = 19,
    AIEVENT_CLEARACTIVEGOALS = 22,
    AIEVENT_DRIVER_IN = 23,
    AIEVENT_DRIVER_OUT = 24,
    AIEVENT_FORCEDNAVIGATION = 25,
    AIEVENT_ADJUSTPATH = 26,
    AIEVENT_LOWHEALTH = 27,
    AIEVENT_ONBULLETRAIN = 28,
    // Player stunt events — IAgent.h lines 130-139
    AIEVENT_PLAYER_STUNT_SPRINT = 101,
    AIEVENT_PLAYER_STUNT_JUMP = 102,
    AIEVENT_PLAYER_STUNT_PUNCH = 103,
    AIEVENT_PLAYER_STUNT_THROW = 104,
    AIEVENT_PLAYER_STUNT_THROW_NPC = 105,
    AIEVENT_PLAYER_THROW = 106,
    AIEVENT_PLAYER_STUNT_CLOAK = 107,
    AIEVENT_PLAYER_STUNT_UNCLOAK = 108,
    AIEVENT_PLAYER_STUNT_ARMORED = 109,
    AIEVENT_PLAYER_STAMP_MELEE = 110,
}

// EAIObjectType — literal port of IAgent.h #define values
public enum EAIObjectType : ushort
{
    AIOBJECT_DUMMY = 0,
    AIOBJECT_ACTOR = 5,
    AIOBJECT_VEHICLE = 6,
    AIOBJECT_TARGET = 9,
    AIOBJECT_AWARE = 10,
    AIOBJECT_ATTRIBUTE = 11,
    AIOBJECT_WAYPOINT = 12,
    AIOBJECT_HIDEPOINT = 13,
    AIOBJECT_SNDSUPRESSOR = 14,
    AIOBJECT_NAV_SEED = 15,
    AIOBJECT_HELICOPTER = 40,
    AIOBJECT_HELICOPTERCRYSIS2 = 41,
    AIOBJECT_GUNFIRE = 42,
    AIOBJECT_INFECTED = 45,
    AIOBJECT_ALIENTICK = 46,
    AIOBJECT_CAR = 50,
    AIOBJECT_BOAT = 60,
    AIOBJECT_AIRPLANE = 70,
    AIOBJECT_2D_FLY = 80,
    AIOBJECT_MOUNTEDWEAPON = 90,
    AIOBJECT_GLOBALALERTNESS = 94,
    AIOBJECT_LEADER = 95,
    AIOBJECT_ORDER = 96,
    AIOBJECT_PLAYER = 100,
    AIOBJECT_GRENADE = 150,
    AIOBJECT_RPG = 151,
    AIOBJECT_NONE = 200,
    AIANCHOR_COMBAT_HIDESPOT = 320,
    AIANCHOR_COMBAT_HIDESPOT_SECONDARY = 330,
}

// EAgentAvoidanceAbilities — literal port of IAgent.h lines 591-606
[System.Flags]
public enum EAgentAvoidanceAbilities : int
{
    eAvoidance_NONE = 0,

    eAvoidance_Vehicles = 0x01,             // Agent can avoid vehicles
    eAvoidance_Actors = 0x02,               // Agent can avoid puppets - DEPRECATED
    eAvoidance_Players = 0x04,              // Agent can avoid players - DEPRECATED

    eAvoidance_StaticObstacle = 0x10,       // Agent can avoid static physical objects (non-pushable)
    eAvoidance_PushableObstacle = 0x20,     // Agent can avoid pushable objects

    eAvoidance_DamageRegion = 0x100,        // Agent can avoid damage regions

    eAvoidance_ALL = 0xFFFF,
    eAvoidance_DEFAULT = eAvoidance_ALL,    // Avoid all by default
}

[System.Flags]
public enum EAIFAFlags : uint
{
    AIFAF_NONE = 0,
    AIFAF_VISIBLE_FROM_REQUESTER = 1 << 0,
    AIFAF_HAS_FORMATION = 1 << 1,
    AIFAF_USE_REFPOINT_POS = 1 << 2,
    AIFAF_INCLUDE_DEVALUED = 1 << 3,
    AIFAF_INCLUDE_DISABLED = 1 << 4,
    AIFAF_LEFT_FROM_REFPOINT = 1 << 5,
    AIFAF_RIGHT_FROM_REFPOINT = 1 << 6,
    AIFAF_INFRONT_OF_REQUESTER = 1 << 7,
    AIFAF_SAME_GROUP_ID = 1 << 8,
    AIFAF_DONT_DEVALUE = 1 << 9,
    AIFAF_PHYSICAL_VISIBILITY_ONLY = 1 << 10,
}

// Vision change hints — literal port of IVisionMap.h EChangeHint
public enum EChangeHint : uint
{
    eChangedPosition = 1u << 0,
    eChangedFactionsToObserveMask = 1u << 1,
    eChangedTypesToObserveMask = 1u << 2,
    eChangedSightRange = 1u << 3,
    eChangedFaction = 1u << 4,
    eChangedTypeMask = 1u << 5,
    eChangedCallback = 1u << 6,
    eChangedUserData = 1u << 7,
    eChangedSkipList = 1u << 8,
    eChangedOrientation = 1u << 9,
    eChangedFOV = 1u << 10,
    eChangedRaycastFlags = 1u << 11,
    eChangedEntityId = 1u << 12,
    eChangedAll = 0xffffffff,
}

// Observable types
public enum EObservableType : uint
{
    None = 0,
    General = 1u << 0,
    Wedge = 1u << 1,
    DeadBody = 1u << 2,
    Interesting = 1u << 3,
    All = 0xFFFFFFFF,
}

// SAIEVENT — literal port of IAgent.h struct (subset used)
public class SAIEVENT
{
    public Vec3 vPosition;
    public Vec3 vStimPos;
    public float fThreat;
    public int nType;
    public int nFlags;
    public int nDeltaHealth;
    public IAIObject sourceId;
    public IEntity targetId;
    public uint sourceEntityID;
    public uint targetEntityID;
    public string psz; // bullet rain reactor name
    public bool bFuzzySight;
    public float fThreatRange;
    // Added for AIVehicle.cpp literal port
    public bool bSetObserver;
    // Added for Puppet.cpp literal port
    public Vec3 vForcedNavigation;
}

// EEntityEvent — literal port of IEntity.h enum (subset)
public enum EEntityEvent
{
    ENTITY_EVENT_XFORM = 0,
    ENTITY_EVENT_TIMER,
    ENTITY_EVENT_INIT,
    ENTITY_EVENT_DONE,
    ENTITY_EVENT_RETURNING_TO_POOL,
    ENTITY_EVENT_VISIBLITY,
    ENTITY_EVENT_RESET,
    ENTITY_EVENT_ATTACH,
    ENTITY_EVENT_ATTACH_THIS,
    ENTITY_EVENT_DETACH,
    ENTITY_EVENT_DETACH_THIS,
    ENTITY_EVENT_LINK,
    ENTITY_EVENT_DELINK,
    ENTITY_EVENT_HIDE,
    ENTITY_EVENT_UNHIDE,
    ENTITY_EVENT_ENABLE_PHYSICS,
    ENTITY_EVENT_PHYSICS_CHANGE_STATE,
    ENTITY_EVENT_SCRIPT_EVENT,
    ENTITY_EVENT_ENTERAREA,
    ENTITY_EVENT_LEAVEAREA,
    ENTITY_EVENT_ENTERNEARAREA,
    ENTITY_EVENT_LEAVENEARAREA,
    ENTITY_EVENT_MOVEINSIDEAREA,
    ENTITY_EVENT_MOVENEARAREA,
    ENTITY_EVENT_PHYS_BREAK,
    ENTITY_EVENT_AI_DONE,
    ENTITY_EVENT_SOUND_DONE,
    ENTITY_EVENT_NOT_SEEN_TIMEOUT,
    ENTITY_EVENT_COLLISION,
    ENTITY_EVENT_RENDER,
    ENTITY_EVENT_PRE_SERIALIZE,
    ENTITY_EVENT_POST_SERIALIZE,
    ENTITY_EVENT_INVISIBLE,
    ENTITY_EVENT_VISIBLE,
    ENTITY_EVENT_MATERIAL,
    ENTITY_EVENT_MATERIAL_LAYER,
    ENTITY_EVENT_CROSS_AREA,
    ENTITY_EVENT_ACTIVATED,
    ENTITY_EVENT_DEACTIVATED,
    ENTITY_EVENT_LAST,
}

// SEntityEvent — literal port of IEntity.h struct
public class SEntityEvent
{
    public EEntityEvent eventType;
    public long[] nParam = new long[4];
    public float fParam;
    public Vec3 vec;

    public SEntityEvent(EEntityEvent type) { eventType = type; }
    public SEntityEvent() { }
}

[System.Flags]
public enum EEntityFlag : uint
{
    ENTITY_FLAG_CASTSHADOW = 1 << 1,
    ENTITY_FLAG_GOOD_OCCLUDER = 1 << 2,
    ENTITY_FLAG_NO_DECALNODE_DECALS = 1 << 3,
    ENTITY_FLAG_WRITE_ONLY = 1 << 4,
    ENTITY_FLAG_NOT_REGISTER_IN_SECTORS = 1 << 5,
    ENTITY_FLAG_CALC_PHYSICS = 1 << 6,
    ENTITY_FLAG_CLIENT_ONLY = 1 << 7,
    ENTITY_FLAG_SERVER_ONLY = 1 << 8,
    ENTITY_FLAG_CUSTOM_VIEWDIST_RATIO = 1 << 9,
    ENTITY_FLAG_CALCBBOX_USEALL = 1 << 10,
    ENTITY_FLAG_VOLUME_SOUND = 1 << 11,
    ENTITY_FLAG_HAS_AI = 1 << 12,
    ENTITY_FLAG_TRIGGER_AREAS = 1 << 13,
    ENTITY_FLAG_NO_SAVE = 1 << 14,
    ENTITY_FLAG_NEEDS_MOVEINSIDE = 1 << 15,
    ENTITY_FLAG_CAMERA_SOURCE = 1 << 16,
    ENTITY_FLAG_CLIENTSIDE_STATE = 1 << 17,
    ENTITY_FLAG_SEND_RENDER_EVENT = 1 << 18,
    ENTITY_FLAG_NO_PROXIMITY = 1 << 19,
    ENTITY_FLAG_PROCEDURAL = 1 << 20,
}

// ObservableParams — literal port of VisionMapTypes.h struct
public class ObservableParams
{
    public const int MaxPositionCount = 6;
    public const int MaxPositionsCount = MaxPositionCount; // alias
    public const int MaxSkipListSize = 32;

    public uint typeMask;
    public uint factionsToObserveMask;
    public uint typesToObserveMask;
    public uint entityId;
    public uint8 faction;
    public int observablePositionsCount;
    public Vec3[] observablePositions = new Vec3[MaxPositionCount];
    public uint skipListSize;
    public IPhysicalEntity[] skipList = new IPhysicalEntity[MaxSkipListSize];
    // Added for Puppet.cpp literal port
    public uint userData;
    // Added for VisionMap.cpp literal port
    public System.Action<VisionID, ObserverParams, VisionID, ObservableParams, bool> callback;
}

public class PhysSkipList : System.Collections.Generic.List<IPhysicalEntity> { }
public class IAIDebugRecord { }
public class CRecorderUnit : IAIDebugRecord
{
    public void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData pEventData) { /* impl pending Phase 11 */ }
}
public class CRecordable { }

// CFormation — Phase 9 forward decl + INVALID_FORMATION_ID const
public class CFormation
{
    public const int INVALID_FORMATION_ID = -1;
    public int GetId() { return INVALID_FORMATION_ID; /* impl pending Phase 9 */ }
    public void SetUpdateSight(float range, float minTime, float maxTime) { /* impl pending */ }
    public void Update() { /* impl pending Phase 9 */ }
    // Added for MoveOp.cpp literal port (Phase 4)
    public CPathMarker GetPathMarker() { return null; /* impl pending Phase 9 */ }
    public CAIObject GetOwner() { return null; /* impl pending Phase 9 */ }
    public int GetPointIndex(CWeakRef<CAIObject> weakRef) { return -1; /* impl pending Phase 9 */ }
    public void GetPointOffset(int pointIndex, ref Vec3 offset) { /* impl pending Phase 9 */ }
}

public struct VisionID
{
    public uint id;
    public string m_debugName;
    public VisionID(uint id, string name = null) { this.id = id; m_debugName = name; }
    public bool IsNil() { return id == 0; }
    public static implicit operator bool(VisionID v) => v.id != 0;
    public static bool operator !(VisionID v) => v.id == 0;
    public static implicit operator uint(VisionID v) => v.id;
    public static implicit operator VisionID(uint v) => new VisionID { id = v };
}
// Vec2 alias = CryPhysics.Math.PhysVector2 (CryPhysicsAliases.cs).

// Vec3 helper extensions / constants needed by literal port
public static class Vec3Helpers
{
    public const float VEC_EPSILON = 0.05f;
    public static bool IsEquivalent(Vec3 a, Vec3 b, float epsilon)
    {
        return System.Math.Abs(a.x - b.x) <= epsilon
            && System.Math.Abs(a.y - b.y) <= epsilon
            && System.Math.Abs(a.z - b.z) <= epsilon;
    }
}

public static class Vec3Extensions2
{
    public static bool IsValid(this Vec3 v) { return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z); }
    public static bool IsUnit(this Vec3 v) { float lensq = v.x * v.x + v.y * v.y + v.z * v.z; return System.Math.Abs(lensq - 1.0f) < 0.01f; }
    public static bool IsZero(this Vec3 v) { return v.x == 0 && v.y == 0 && v.z == 0; }
}

// SAIObjectCreationHelper - helper for serializing AI objects
// (used for both normal and bookmark serialization)
// Literal port of dev/Code/CryEngine/CryAISystem/AIObjectManager.cpp lines 34-93
public struct SAIObjectCreationHelper
{
    public string name;
    public EAIClass aiClass;
    public uint32 objectId;

    public SAIObjectCreationHelper(CAIObject pObject)
    {
        if (pObject != null)
        {
            name = pObject.GetName();
            objectId = pObject.GetAIObjectID();

            // Working from child to parent through the hierarchy is essential here - each object can be many types
            // This could all be much neater with a different type system to fastcast.
            if      (pObject.CastToCAIVehicle() != null)       aiClass = EAIClass.eAIC_AIVehicle;
            else if (pObject.CastToCAIFlyingVehicle() != null) aiClass = EAIClass.eAIC_AIFlyingVehicle;
            else if (pObject.CastToCPuppet() != null)          aiClass = EAIClass.eAIC_Puppet;
            else if (pObject.CastToCPipeUser() != null)        aiClass = EAIClass.eAIC_PipeUser;
            else if (pObject.CastToCAIPlayer() != null)        aiClass = EAIClass.eAIC_AIPlayer;
            else if (pObject.CastToCLeader() != null)          aiClass = EAIClass.eAIC_Leader;
            else if (pObject.CastToCAIActor() != null)         aiClass = EAIClass.eAIC_AIActor;
            else /* CAIObject */                                aiClass = EAIClass.eAIC_AIObject;
        }
        else
        {
            objectId = 0 /*INVALID_AIOBJECTID*/;
            name = "Unset";
            aiClass = EAIClass.eAIC_Invalid;
        }
    }

    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("ObjectHeader");
        ser.Value("name", ref name);    // debug mainly, could be removed
        ser.EnumValue("class", ref aiClass, EAIClass.eAIC_FIRST, EAIClass.eAIC_LAST);
        ser.Value("objectId", ref objectId);
        ser.EndGroup();
    }

    public CAIObject RecreateObject(object pAlloc = null /*=NULL*/)
    {
        // skip unset ones (eg from a nil CStrongRef)
        if (objectId == 0 /*INVALID_AIOBJECTID*/ && aiClass == EAIClass.eAIC_Invalid)
            return null;

        // first verify it doesn't already exist
        System.Diagnostics.Debug.Assert(gAIEnv.pAIObjectManager.GetAIObject(objectId) == null);

        CAIObject pObject = null;
        switch (aiClass)
        {
            case EAIClass.eAIC_AIVehicle:        pObject = new CAIVehicle(); break;
            case EAIClass.eAIC_Puppet:           pObject = new CPuppet(); break;
            case EAIClass.eAIC_PipeUser:         pObject = new CPipeUser(); break;
            case EAIClass.eAIC_AIPlayer:         pObject = new CAIPlayer(); break;
            case EAIClass.eAIC_Leader:           pObject = new CLeader(/*0*/); break; // Groupid is reqd
            case EAIClass.eAIC_AIActor:          pObject = new CAIActor(); break;
            case EAIClass.eAIC_AIObject:         pObject = new CAIObject(); break;
            case EAIClass.eAIC_AIFlyingVehicle:  pObject = new CAIFlyingVehicle(); break;
            default: System.Diagnostics.Debug.Assert(false); break;
        }

        return pObject;
    }
}
