// Literal port of dev/Code/CryEngine/CryCommon/IAgent.h declaration of IAIObject (subset).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// IAIObject lives in IAgent.h in the C++. Full IAgent.h is 1959 lines and will be ported
// progressively as Phase 2 demands more members.

namespace CryAISystem.CryCommon;

public interface IAIObject
{
    // Forward declaration shell — full member set ported when Phase 2 lands.
    string GetName();
    void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData data);
    uint8 GetFactionID();

    // Subset of CastTo* added session 6 — needed by AIFlyingVehicle.cpp call site.
    // Full literal CastTo set lives on CAIObject (see AIObject.cs).
    CryAISystem.CPuppet CastToCPuppet();
    ushort GetAIType();

    // Added for AIActor.cpp literal port
    bool IsEnabled();
    IEntity GetEntity();
    uint GetAIObjectID();
    uint GetEntityID();
    CryAISystem.CAIActor CastToCAIActor();
    // Added for AIPlayer.cpp literal port
    CryAISystem.CAIPlayer CastToCAIPlayer();
    bool IsHostile(IAIObject pOther, bool bUsingAIIgnorePlayer = true);
    // Added for AIVehicle.cpp literal port
    CryAISystem.CAIVehicle CastToCAIVehicle();
    // Added for PipeUser.cpp literal port
    Vec3 GetPos();
    // Added for Puppet.cpp literal port
    CryAISystem.CPipeUser CastToCPipeUser() { return this as CryAISystem.CPipeUser; }
    // Added for MoveOp.cpp literal port (Phase 4)
    Vec3 GetVelocity() { return new Vec3(0, 0, 0); }
    Vec3 GetPosInNavigationMesh(NavigationAgentTypeID agentTypeID) { return GetPos(); }
}

// IAIRecordable from IAIRecorder.h — enum subset only for now
public interface IAIRecordable
{
    public enum e_AIDbgEvent
    {
        E_RESET,
        E_SIGNALRECIEVED,
        E_SIGNALRECIEVEDAUX,
        E_SIGNALEXECUTING,
        E_SIGNALEXECUTEDWARNING,
        E_GOALPIPEINSERTED,
        E_GOALPIPESELECTED,
        E_GOALPIPERESETED,
        E_BEHAVIORSELECTED,
        E_BEHAVIORDESTRUCTOR,
        E_BEHAVIORCONSTRUCTOR,
        E_ATTENTIONTARGET,
        E_REGISTERSTIMULUS,
        E_HANDLERNEVENT,
        E_ACTIONSTART,
        E_ACTIONSUSPEND,
        E_ACTIONRESUME,
        E_ACTIONEND,
        E_REFPOINTPOS,
        E_EVENT,
        E_LUACOMMENT,
        E_PERSONALLOG,
        // Added for AIPlayer.cpp literal port — IAIRecorder.h enum values
        E_HEALTH,
        E_AGENTPOS,
        E_AGENTDIR,
        E_ATTENTIONTARGETPOS,
    }

    public struct RecorderEventData
    {
        public string pString;
        public Vec3 pos;
        public float val;
        public RecorderEventData(string s) { pString = s; pos = new Vec3(0,0,0); val = 0; }
        public RecorderEventData(Vec3 p) { pString = null; pos = p; val = 0; }
        public RecorderEventData(float v) { pString = null; pos = new Vec3(0,0,0); val = v; }
    }
}

// Forward decls for IEntity / IEntitySystem (CryCommon partial literal ports)
public interface IEntity
{
    string GetName();
    IAIObject GetAI();
    Vec3 GetWorldPos();
    Vec3 GetPos();
    IPhysicalEntity GetPhysics();
    uint GetFlags();
    // Subset of IEntity.h API used by AIObjectManager.OnEntityReturnedToPool / OnBookmarkEntitySerialize.
    bool HasAI();
    bool IsFromPool();
    uint GetId();
    uint GetAIObjectID();
    void SetAIObjectID(uint id);
    // Added for AIActor.cpp literal port
    IScriptTable GetScriptTable();
    bool IsActive();
    void GetLocalBounds(out AABB bbox);
    bool IsHidden();
    // Added for AIPlayer.cpp literal port
    Quat GetRotation();
    void GetWorldBounds(out AABB bbox);
    T GetComponent<T>() where T : class;
    // Added for AIVehicle.cpp literal port
    Matrix34 GetWorldTM();
    // Added for PipeUser.cpp literal port
    void let_AbortAIAction();
    // Added for AIHideObject.cpp literal port
    Quat GetWorldRotation() { return GetRotation(); }
    void GetLocalBounds(AABB bbox) { /* overload */ }
    // Added for Movement system (Phase 4)
    void SetWorldTM(Matrix34 tm) { }
}

public interface IEntitySystem
{
    IEntity GetEntity(uint id);
    // Added for Puppet.cpp literal port
    IEntity FindEntityByName(string name) { return null; }
    // Added for PathObstacles.cpp literal port — GetEntityFromPhysics(IPhysicalEntity*)
    IEntity GetEntityFromPhysics(IPhysicalEntity pPhysEntity) { return null; }
}

public interface IRenderer
{
    int GetFrameID(bool b);
}
