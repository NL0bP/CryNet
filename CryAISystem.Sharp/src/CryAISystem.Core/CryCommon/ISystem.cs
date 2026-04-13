// Literal port of dev/Code/CryEngine/CryCommon/ISystem.h (subset — gEnv + ISystem core).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// SSystemGlobalEnvironment is the C++ global accessed via the gEnv pointer.
// We expose the same fields. The actual instance is the static `gEnv` global below.
public class SSystemGlobalEnvironment
{
    public ISystem pSystem;
    public ITimer pTimer;
    public ILog pLog;
    public IConsole pConsole;
    public ICryPak pCryPak;
    public IEntitySystem pEntitySystem;
    public IRenderer pRenderer;
    public IPhysicalWorld pPhysicalWorld;
    public IAISystemForwardDecl pAISystem;
    public I3DEngine p3DEngine;

    public bool bMultiplayer;
    public bool IsEditor() { return false; }
    public bool IsEditing() { return false; }
    public bool IsDedicated() { return false; }
    public IScriptSystem pScriptSystem;
}

public interface ISystem
{
    IScriptSystem GetIScriptSystem();
    XmlNodeRef LoadXmlFromFile(string sFilename);
    void Warning(EValidatorModule module, EValidatorSeverity severity, EValidatorFlags flags, string file, string format, params object[] args);
    // Added for Puppet.cpp literal port
    IEntitySystem GetIEntitySystem() { return gEnv.pEntitySystem; }
    CryAISystem.CPNoise3 GetNoiseGen() { return new CryAISystem.CPNoise3(); }
}

// gEnv is a free-standing global in the C++ engine. We host it on a top-level
// static class to keep usage `gEnv.pTimer.GetAsyncTime()` literal.
public static class gEnv
{
    public static SSystemGlobalEnvironment Instance = new SSystemGlobalEnvironment();

    public static ISystem pSystem => Instance.pSystem;
    public static ITimer pTimer => Instance.pTimer;
    public static ILog pLog => Instance.pLog;
    public static IConsole pConsole => Instance.pConsole;
    public static ICryPak pCryPak => Instance.pCryPak;
    public static IEntitySystem pEntitySystem => Instance.pEntitySystem;
    public static IRenderer pRenderer => Instance.pRenderer;
    public static IPhysicalWorld pPhysicalWorld => Instance.pPhysicalWorld;
    public static IAISystemForwardDecl pAISystem => Instance.pAISystem;
    public static I3DEngine p3DEngine => Instance.p3DEngine;

    public static bool bMultiplayer => Instance.bMultiplayer;
    public static bool IsEditor() => Instance.IsEditor();
    public static bool IsEditing() => Instance.IsEditing();
    public static bool IsDedicated() => Instance.IsDedicated();
    public static IScriptSystem pScriptSystem => Instance.pScriptSystem;
}

public static class SystemGlobals
{
    public static ISystem GetISystem() { return gEnv.pSystem; }
}

// IAISystem forward decl shell — full literal port pending Phase 11 (CAISystem.h port).
// Subset of methods needed by AIFlyingVehicle.cpp / Phase 2 .cpp impls so far.
public interface IAISystemForwardDecl
{
    void SendSignal(SIGNALFILTER filter, int nFollowUp, string szText, IAIObject pSenderObject, IAISignalExtraData pData, uint crcCode);
    // Added for AIActor.cpp literal port (Phase 2)
    float GetGlobalVisualScale(CryAISystem.CAIActor actor);
    float GetGlobalAudioScale(CryAISystem.CAIActor actor);
    CryAISystem.IVisionMap GetVisionMap();
    CryAISystem.IAISignalExtraData CreateSignalExtraData();
    void FreeSignalExtraData(CryAISystem.AISignalExtraData data);
    // Added for Puppet.cpp literal port
    CryAISystem.CAIActionManager GetAIActionManager() { return CryAISystem.gAIEnv.pAIActionManager; }
    // Added for MoveOp.cpp literal port (Phase 4)
    CryAISystem.IMovementSystem GetMovementSystem() { return CryAISystem.gAIEnv.pMovementSystem; }
    uint GetAgentDebugTarget() { return 0; }
}

// I3DEngine — subset of I3DEngine.h used by AIVehicle.cpp + Shape.cpp
public interface I3DEngine
{
    float GetTerrainElevation(float x, float y);
    float GetWaterLevel(Vec3 pt);
    /// Overload returning ocean level (no args). C++ signature: GetWaterLevel()
    float GetWaterLevel();
}
