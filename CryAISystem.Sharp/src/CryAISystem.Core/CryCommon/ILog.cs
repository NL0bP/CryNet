// Literal port of dev/Code/CryEngine/CryCommon/ILog.h (subset).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public interface ILog
{
    // <interfuscator:shuffle>
    void Log(string format, params object[] args);
    void LogToConsole(string format, params object[] args);
    void LogToFile(string format, params object[] args);
    void LogError(string format, params object[] args);
    void LogWarning(string format, params object[] args);
    void UpdateLoadingScreen(string format, params object[] args);
    int GetVerbosityLevel();
    // </interfuscator:shuffle>
}
