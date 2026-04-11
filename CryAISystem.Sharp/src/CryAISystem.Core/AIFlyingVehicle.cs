// Literal port of dev/Code/CryEngine/CryAISystem/AIFlyingVehicle.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryAISystem.CryCommon;

namespace CryAISystem;

public class CAIFlyingVehicle : CPuppet
{
    // typedef CPuppet Base; — calls to base members use C# `base.`

    public CAIFlyingVehicle()
    {
        m_combatModeEnabled = false;
        m_firingAllowed = true;
        _fastcast_CAIFlyingVehicle = true;
    }

    // virtual ~CAIFlyingVehicle();
    ~CAIFlyingVehicle()
    {
        SetObservable(false);
        SetObserver(false);
    }

    public new void SetObserver(bool observer)
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
                observerParams.sightRange = m_Parameters.m_PerceptionParams.sightRange;
                float secondaryFov = 0f;
                GetSightFOVCos(out observerParams.fovCos, out secondaryFov);
                observerParams.callback = OnVisionChanged;

                PhysSkipList skipList = new PhysSkipList();
                GetPhysicalSkipEntities(skipList);

                observerParams.skipListSize = System.Math.Min(skipList.Count, ObserverParams.MaxSkipListSize);
                for (int i = 0; i < (int)observerParams.skipListSize; ++i)
                    observerParams.skipList[i] = skipList[i];

                VisionID visionID = GetVisionID();
                if (visionID.id == 0)
                {
                    visionID = gAIEnv.pVisionMap.CreateVisionID(GetName());

                    SetVisionID(visionID);
                }

                gAIEnv.pVisionMap.RegisterObserver(visionID, observerParams);
            }
            else
            {
                VisionID visionID = GetVisionID();
                if (visionID.id != 0)
                    gAIEnv.pVisionMap.UnregisterObserver(visionID);
            }

            m_observer = observer;
        }
    }

    // Sandy - Dec 2011: Unfortunately this code will cause Lua scripts to be reloaded
    // while they are running, causing all sorts of undefined behavior.
    // Not sure what this 'hot-fix' was supposed to do though, so I am
    // going to leave it here now as a warning!
    //
    ////This is a "hot-fix". Need to dig out the real issue
    //void  CAIFlyingVehicle::Reset(EObjectResetType type)
    //{
    //	if (AIOBJRESET_INIT == type)
    //	{
    //		if (IEntity *entity = GetEntity())
    //		{
    //			if (IComponentScriptPtr scriptComponent = pEntity->GetComponent<IComponentScript>())
    //			{
    //				SEntitySpawnParams params;
    //				scriptComponent->Reload(entity, params);
    //			}
    //		}
    //	}
    //
    //	Base::Reset(type);
    //}


    public static void OnVisionChanged(VisionID observerID, ObserverParams observerParams, VisionID observableID, ObservableParams observableParams, bool visible)
    {
        ObserverParams parameters = gAIEnv.pVisionMap.GetObserverParams(observerID);

        if (parameters != null)
        {
            IEntity ent = gEnv.pEntitySystem.GetEntity(parameters.entityId);
            IAIObject obj = ent.GetAI();

            if (obj != null)
            {
                CPuppet puppet = obj.CastToCPuppet();
                if (puppet != null)
                {
                    CAIFlyingVehicle flyingAI = (CAIFlyingVehicle)puppet;

                    uint targetID = observableParams.entityId;
                    if (targetID != flyingAI.GetEntityID())
                    {
                        if (visible)
                        {
                            if (targetID != 0)
                            {
                                IEntity entity = gEnv.pEntitySystem.GetEntity(observableParams.entityId);
                                if (entity != null)
                                {
                                    IAIObject aiObject = entity.GetAI();
                                    if (aiObject != null)
                                    {
                                        ushort aiObjectType = aiObject.GetAIType();
                                        if ((aiObjectType == (ushort)CryAISystem.EAIObjectType.AIOBJECT_GRENADE) || (aiObjectType == (ushort)CryAISystem.EAIObjectType.AIOBJECT_RPG))
                                        {
                                            Vec3 pos = entity.GetPos();
                                            Vec3 ownPos = flyingAI.GetPos();
                                            Vec3 diff = ownPos - pos;
                                            diff.Normalize();

                                            pe_status_dynamics dynamics = new pe_status_dynamics();
                                            if (entity.GetPhysics() != null)
                                            {
                                                entity.GetPhysics().GetStatus(dynamics);

                                                if (dynamics.v.Dot(diff) > 0.9f)
                                                {
                                                    GoalParams param = new GoalParams();
                                                    param.SetName("params");

                                                    GoalParams paramChild = new GoalParams();
                                                    paramChild.SetName("impulse");
                                                    paramChild.SetValue(pos);

                                                    GoalParams paramChild2 = new GoalParams();
                                                    paramChild2.SetName("dir");
                                                    paramChild2.SetValue(dynamics.v);
                                                    paramChild.AddChild(paramChild2);

                                                    GoalParams paramChild3 = new GoalParams();
                                                    paramChild3.SetName("id");
                                                    paramChild3.SetValue(observableParams.entityId);
                                                    paramChild.AddChild(paramChild3);

                                                    param.AddChild(paramChild);


                                                    CGoalPipe pipe = flyingAI.GetCurrentGoalPipe();
                                                    if (pipe != null)
                                                    {
                                                        pipe.ParseParams(param);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public override void Serialize(TSerialize ser)
    {
        base.Serialize(ser);

        ser.Value("m_combatModeEnabled", ref m_combatModeEnabled);
        ser.Value("m_firingAllowed", ref m_firingAllowed);
    }

    private static void AISendSignal(CAIFlyingVehicle pFlyingVehicle, string signalName)
    {
        System.Diagnostics.Debug.Assert(pFlyingVehicle != null);
        System.Diagnostics.Debug.Assert(signalName != null);

        uint signalNameCrc = CCrc32.Compute(signalName);
        gEnv.pAISystem.SendSignal(SIGNALFILTER.SIGNALFILTER_SENDER, 1, signalName, pFlyingVehicle, null, signalNameCrc);
    }

    public override void PostSerialize()
    {
        base.PostSerialize();

        if (m_combatModeEnabled)
        {
            AISendSignal(this, "CombatTargetEnabled");
        }
        else
        {
            AISendSignal(this, "CombatTargetDisabled");
        }

        if (m_firingAllowed)
        {
            AISendSignal(this, "FiringAllowed");
        }
        else
        {
            AISendSignal(this, "FiringNotAllowed");
        }
    }

    public new void SetSignal(int nSignalID, string szText, IEntity pSender, IAISignalExtraData pData, uint crcCode)
    {
        uint s_combatTargetEnabledCrc  = CCrc32.Compute("CombatTargetEnabled");
        uint s_combatTargetDisabledCrc = CCrc32.Compute("CombatTargetDisabled");
        uint s_firingAllowedCrc        = CCrc32.Compute("FiringAllowed");
        uint s_firingNotAllowedCrc     = CCrc32.Compute("FiringNotAllowed");

        if (crcCode == s_combatTargetEnabledCrc)
        {
            m_combatModeEnabled = true;
        }
        else if (crcCode == s_combatTargetDisabledCrc)
        {
            m_combatModeEnabled = false;
        }
        else if (crcCode == s_firingAllowedCrc)
        {
            m_firingAllowed = true;
        }
        else if (crcCode == s_firingNotAllowedCrc)
        {
            m_firingAllowed = false;
        }

        base.SetSignal(nSignalID, szText, pSender, pData, crcCode);
    }

    private bool m_combatModeEnabled;
    private bool m_firingAllowed;
}

// Forward decl shell for ObserverParams (Vision/Perception — Phase 5).
// (ObservableParams + PhysSkipList live in AIObject.cs.)
public class ObserverParams
{
    public const int MaxSkipListSize = 8;
    public uint entityId;
    public uint32 factionsToObserveMask;
    public uint8 faction;
    public uint32 typesToObserveMask;
    public uint32 typeMask;
    public Vec3 eyePosition;
    public Vec3 eyeDirection;
    public float sightRange;
    public float fovCos;
    public System.Action<VisionID, ObserverParams, VisionID, ObservableParams, bool> callback;
    public IPhysicalEntity[] skipList = new IPhysicalEntity[MaxSkipListSize];
    public int skipListSize;
}

public enum SIGNALFILTER
{
    SIGNALFILTER_SENDER = 1,
}
