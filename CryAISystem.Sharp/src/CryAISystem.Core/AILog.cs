// Literal port of dev/Code/CryEngine/CryAISystem/AILog.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem;

// #ifndef _DEBUG
// // comment this out to remove asserts at compile time
// #define ENABLE_AI_ASSERT
// #endif

/// Default message verbosity levels based on type
public enum AI_LOG_VERBOSITY
{
    AI_LOG_OFF = 0,
    AI_LOG_ERROR = 1,
    AI_LOG_WARNING = 1,
    AI_LOG_PROGRESS = 1,
    AI_LOG_EVENT = 2,
    AI_LOG_COMMENT = 3,
}

public static class AILog
{
    // these should all be in sync - so testing one for 0 should be the same for all
    private static ISystem pSystem = null;

    private const string outputPrefix = "AI: ";
    private const uint outputPrefixLen = 4; // sizeof(outputPrefix) - 1

    // #define DECL_OUTPUT_BUF char outputBufferLog[MAX_WARNING_LENGTH + outputPrefixLen]; unsigned outputBufferSize = sizeof(outputBufferLog)

    private const int maxSavedMsgs = 5;
    private const int maxSavedMsgLength = 4096 + (int)outputPrefixLen + 1; // MAX_WARNING_LENGTH placeholder

    private enum ESavedMsgType { SMT_WARNING, SMT_ERROR }

    private struct SSavedMsg
    {
        public ESavedMsgType savedMsgType;
        public string savedMsg;
        public CTimeValue time;
    }

    private static SSavedMsg[] savedMsgs = new SSavedMsg[maxSavedMsgs];
    private static int savedMsgIndex = 0;

    //====================================================================
    // DebugDrawLabel
    //====================================================================
    private static void DebugDrawLabel(ESavedMsgType type, float timeFrac, int col, int row, string szText)
    {
        float ColumnSize = 11;
        float RowSize = 11;
        float baseY = 10;
        ColorB colorWarning = new ColorB(0, 255, 255);
        ColorB colorError = new ColorB(255, 255, 0);
        ColorB color = (type == ESavedMsgType.SMT_ERROR) ? colorError : colorWarning;
        CDebugDrawContext dc = new CDebugDrawContext();

        float alpha = 1.0f;
        const float fadeFrac = 0.5f;
        if (timeFrac < fadeFrac && fadeFrac > 0.0f)
            alpha = timeFrac / fadeFrac;
        color.a = (uint8)(255 * alpha);

        float actualCol = ColumnSize * (float)col;
        float actualRow;
        if (row >= 0)
            actualRow = baseY + RowSize * (float)row;
        else
            actualRow = dc.Op_Arrow().GetHeight() - (baseY + RowSize * (float)(-row));

        dc.Op_Arrow().Draw2dLabel(actualCol, actualRow, 1.2f, color, false, "%s", szText);
    }

    //====================================================================
    // DisplaySavedMsgs
    //====================================================================
    public static void AILogDisplaySavedMsgs()
    {
        float savedMsgDuration = gAIEnv.CVars.OverlayMessageDuration;
        if (savedMsgDuration < 0.01f)
            return;
        const int col = 1;

        int row = -1;
        CTimeValue currentTime = gEnv.pTimer.GetFrameStartTime();
        CTimeValue time = currentTime - new CTimeValue(savedMsgDuration);
        for (int i = 0; i < maxSavedMsgs; ++i)
        {
            int index = (maxSavedMsgs + savedMsgIndex - i) % maxSavedMsgs;
            if (savedMsgs[index].time < time)
                return;
            // get rid of msgs from the future - can happen during load/save
            if (savedMsgs[index].time > currentTime)
                savedMsgs[index].time = time;
            //      savedMsgIndex = (maxSavedMsgs + savedMsgIndex - 1) % maxSavedMsgs;

            float timeFrac = (savedMsgs[index].time - time).GetSeconds() / savedMsgDuration;
            DebugDrawLabel(savedMsgs[index].savedMsgType, timeFrac, col, row, savedMsgs[index].savedMsg);
            --row;
        }
    }

    //====================================================================
    // AIInitLog
    //====================================================================
    public static void AIInitLog(ISystem system)
    {
        if (pSystem != null)
            AIWarning("Re-registering AI Logging");

        AIAssert(system != null);
        if (system == null)
            return;
        IConsole console = gEnv.pConsole;
#if _DEBUG
        int isDebug = 1;
#else
        int isDebug = 0;
#endif

        if (console != null)
            pSystem = system;

        for (int i = 0; i < maxSavedMsgs; ++i)
        {
            savedMsgs[i].savedMsg = "";
            savedMsgs[i].savedMsgType = ESavedMsgType.SMT_WARNING;
            savedMsgs[i].time = new CTimeValue(0.0f);
            savedMsgIndex = 0;
        }
    }

    //====================================================================
    // AIGetLogConsoleVerbosity
    //====================================================================
    public static int AIGetLogConsoleVerbosity()
    {
        return gAIEnv.CVars.LogConsoleVerbosity;
    }

    //====================================================================
    // AIGetLogFileVerbosity
    //====================================================================
    public static int AIGetLogFileVerbosity()
    {
        return gAIEnv.CVars.LogFileVerbosity;
    }

    //====================================================================
    // AICheckLogVerbosity
    //====================================================================
    public static bool AICheckLogVerbosity(AI_LOG_VERBOSITY CheckVerbosity)
    {
        bool bResult = false;

        int iAILogVerbosity = AIGetLogFileVerbosity();
        int iAIConsoleVerbosity = AIGetLogConsoleVerbosity();

        if (iAILogVerbosity >= (int)CheckVerbosity || iAIConsoleVerbosity >= (int)CheckVerbosity)
        {
            // Check against actual log system
            int nVerbosity = gEnv.pLog.GetVerbosityLevel();
            bResult = (nVerbosity >= (int)CheckVerbosity);
        }

        return bResult;
    }

    //===================================================================
    // AIGetWarningErrorsEnabled
    //===================================================================
    public static bool AIGetWarningErrorsEnabled()
    {
        return gAIEnv.CVars.EnableWarningsErrors != 0;
    }

    //====================================================================
    // AIError
    //====================================================================
    public static void AIError(string format, params object[] args)
    {
        if (pSystem == null || !AIGetWarningErrorsEnabled() || !AICheckLogVerbosity(AI_LOG_VERBOSITY.AI_LOG_ERROR))
            return;

        string outputBufferLog = string.Format(format, args);

        if (gEnv.IsEditor())
        {
            pSystem.Warning(EValidatorModule.VALIDATOR_MODULE_AI, EValidatorSeverity.VALIDATOR_ERROR, EValidatorFlags.VALIDATOR_FLAG_AI, null, "!AI: Error: " + outputBufferLog);
        }
        else
        {
            gEnv.pLog.LogError(outputBufferLog);
        }

        savedMsgIndex = (savedMsgIndex + 1) % maxSavedMsgs;
        savedMsgs[savedMsgIndex].savedMsgType = ESavedMsgType.SMT_ERROR;
        savedMsgs[savedMsgIndex].savedMsg = outputBufferLog;
        savedMsgs[savedMsgIndex].time = gEnv.pTimer.GetFrameStartTime();
    }

    //====================================================================
    // AIWarning
    //====================================================================
    public static void AIWarning(string format, params object[] args)
    {
        if (pSystem == null || !AIGetWarningErrorsEnabled() || !AICheckLogVerbosity(AI_LOG_VERBOSITY.AI_LOG_WARNING))
            return;

        string outputBufferLog = string.Format(format, args);
        pSystem.Warning(EValidatorModule.VALIDATOR_MODULE_AI, EValidatorSeverity.VALIDATOR_WARNING, EValidatorFlags.VALIDATOR_FLAG_AI, null, "AI: " + outputBufferLog);

        savedMsgIndex = (savedMsgIndex + 1) % maxSavedMsgs;
        savedMsgs[savedMsgIndex].savedMsgType = ESavedMsgType.SMT_WARNING;
        savedMsgs[savedMsgIndex].savedMsg = outputBufferLog;
        savedMsgs[savedMsgIndex].time = gEnv.pTimer.GetFrameStartTime();
    }

    //====================================================================
    // AILogAlways
    //====================================================================
    public static void AILogAlways(string format, params object[] args)
    {
        if (pSystem == null)
            return;

        string outputBufferLog = outputPrefix + string.Format(format, args);

        gEnv.pLog.Log(outputBufferLog);
    }

    public static void AILogLoading(string format, params object[] args)
    {
        if (pSystem == null)
            return;

        const string outputPrefix2 = "--- AI: ";

        string outputBufferLog = outputPrefix2 + string.Format(format, args);

        gEnv.pLog.UpdateLoadingScreen(outputBufferLog);
    }

    //====================================================================
    // AIHandleLogMessage
    //====================================================================
    public static void AIHandleLogMessage(string outputBufferLog)
    {
        int cV = AIGetLogConsoleVerbosity();
        int fV = AIGetLogFileVerbosity();

        if ((cV >= (int)AI_LOG_VERBOSITY.AI_LOG_PROGRESS) && (fV >= (int)AI_LOG_VERBOSITY.AI_LOG_PROGRESS))
            gEnv.pLog.Log("%s", outputBufferLog);
        else if (cV >= (int)AI_LOG_VERBOSITY.AI_LOG_PROGRESS)
            gEnv.pLog.LogToConsole("%s", outputBufferLog);
        else if (fV >= (int)AI_LOG_VERBOSITY.AI_LOG_PROGRESS)
            gEnv.pLog.LogToFile("%s", outputBufferLog);
    }

    //====================================================================
    // AILogProgress
    //====================================================================
    public static void AILogProgress(string format, params object[] args)
    {
        if (pSystem == null || !AICheckLogVerbosity(AI_LOG_VERBOSITY.AI_LOG_PROGRESS))
            return;

        string outputBufferLog = outputPrefix + string.Format(format, args);

        AIHandleLogMessage(outputBufferLog);
    }

    //====================================================================
    // AILogEvent
    //====================================================================
    public static void AILogEvent(string format, params object[] args)
    {
        if (pSystem == null || !AICheckLogVerbosity(AI_LOG_VERBOSITY.AI_LOG_EVENT))
            return;

        string outputBufferLog = outputPrefix + string.Format(format, args);

        AIHandleLogMessage(outputBufferLog);
    }

    //====================================================================
    // AILogComment
    //====================================================================
    public static void AILogComment(string format, params object[] args)
    {
        if (pSystem == null || !AICheckLogVerbosity(AI_LOG_VERBOSITY.AI_LOG_COMMENT))
            return;

        string outputBufferLog = outputPrefix + string.Format(format, args);

        AIHandleLogMessage(outputBufferLog);
    }

    // AIAssert -> CRY_ASSERT
    public static void AIAssert(bool exp)
    {
        CryAssert.Check(exp);
    }
}
