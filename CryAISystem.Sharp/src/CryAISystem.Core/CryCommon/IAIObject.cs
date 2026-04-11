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
    }

    public struct RecorderEventData
    {
        public string pString;
        public RecorderEventData(string s) { pString = s; }
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
}

public interface IEntitySystem
{
    IEntity GetEntity(uint id);
}

public interface IRenderer
{
    int GetFrameID(bool b);
}
