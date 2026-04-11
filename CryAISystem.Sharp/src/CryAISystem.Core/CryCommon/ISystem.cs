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

    public bool IsEditor() { return false; }
    public bool IsDedicated() { return false; }
}

public interface ISystem
{
    IScriptSystem GetIScriptSystem();
    XmlNodeRef LoadXmlFromFile(string sFilename);
    void Warning(EValidatorModule module, EValidatorSeverity severity, EValidatorFlags flags, string file, string format, params object[] args);
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

    public static bool IsEditor() => Instance.IsEditor();
    public static bool IsDedicated() => Instance.IsDedicated();
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
}
