// Literal port of dev/Code/CryEngine/CryAISystem/AIDbgRecorder.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Simple text AI debugging event recorder
//
// Notes       : Really, this class is two separate debuggers - consider splitting
//               Move the access point to gAIEnv
//               Only creates the files on files on first logging - add some kind of init

namespace CryAISystem;

// Simple text debug recorder
// Completely independent from CAIRecorder, which is more sophisticated
public class CAIDbgRecorder
{
    private const string AIRECORDER_FILENAME = "AILog.log";
    private const string AIRECORDER_SECONDARYFILENAME = "AISignals.csv";

    private const int BUFFER_SIZE = 256;

    public CAIDbgRecorder() { }
    // ~CAIDbgRecorder() {}

    public bool IsRecording(IAIObject pTarget, IAIRecordable.e_AIDbgEvent eventArg)
    {
        if (!string.IsNullOrEmpty(m_sFile))
            return false;

        if (eventArg == IAIRecordable.e_AIDbgEvent.E_RESET)
            return true;

        if (pTarget == null)
            return false;

        return gAIEnv.CVars.StatsTarget == "all"
            || gAIEnv.CVars.StatsTarget == pTarget.GetName();
    }

    public void Record(IAIObject pTarget, IAIRecordable.e_AIDbgEvent eventArg, string pString)
    {
        if (eventArg == IAIRecordable.e_AIDbgEvent.E_RESET)
        {
            LogString("\n\n--------------------------------------------------------------------------------------------\n");
            LogString("<RESETTING AI SYSTEM>\n");
            LogString("aiTick:startTime:<entity> ai_event    details\n");
            LogString("--------------------------------------------------------------------------------------------\n");
            // (MATT) Since some gAIEnv settings change only on a reset, this would be the time to list them {2008/11/20}
            return;
        }
#if AI_LOG_SIGNALS
        else if (eventArg == IAIRecordable.e_AIDbgEvent.E_SIGNALEXECUTEDWARNING)
        {
            string bufferString = string.Format("{0}\n", pString);
            LogStringSecondary(bufferString);
            return;
        }
#endif

        if (pTarget == null)
            return;

        // Filter to only log the targets we are interested in
        // I.e. if not "all" and if not our current target, return
        string sStatsTarget = gAIEnv.CVars.StatsTarget;
        if ((sStatsTarget != "all") && (sStatsTarget != pTarget.GetName()))
            return;


        string pEventString = "";
        switch (eventArg)
        {
            case IAIRecordable.e_AIDbgEvent.E_SIGNALRECIEVED:
                pEventString = "signal_recieved      ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_SIGNALRECIEVEDAUX:
                pEventString = "auxsignal_recieved   ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_SIGNALEXECUTING:
                pEventString = "signal_executing     ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_GOALPIPEINSERTED:
                pEventString = "goalpipe_inserted    ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_GOALPIPESELECTED:
                pEventString = "goalpipe_selected    ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_GOALPIPERESETED:
                pEventString = "goalpipe_reseted     ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_BEHAVIORSELECTED:
                pEventString = "behaviour_selected   ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_BEHAVIORDESTRUCTOR:
                pEventString = "behaviour_destructor ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_BEHAVIORCONSTRUCTOR:
                pEventString = "behaviour_constructor";
                break;
            case IAIRecordable.e_AIDbgEvent.E_ATTENTIONTARGET:
                pEventString = "atttarget_change     ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_REGISTERSTIMULUS:
                pEventString = "register_stimulus    ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_HANDLERNEVENT:
                pEventString = "handler_event        ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_ACTIONSTART:
                pEventString = "action_start         ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_ACTIONSUSPEND:
                pEventString = "action_suspend       ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_ACTIONRESUME:
                pEventString = "action_resume        ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_ACTIONEND:
                pEventString = "action_end           ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_REFPOINTPOS:
                pEventString = "refpoint_pos         ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_EVENT:
                pEventString = "triggering event     ";
                break;
            case IAIRecordable.e_AIDbgEvent.E_LUACOMMENT:
                pEventString = "lua comment          ";
                break;
            default:
                pEventString = "undefined            ";
                break;
        }

        // Fetch the current AI tick and the time that tick started
        int frame = GetAISystem().GetAITickCount();
        float time = GetAISystem().GetFrameStartTimeSeconds();

        if (pString == null)
            pString = "<null>";
        string bufferString = string.Format("{0,6}:{1,9:F3}: <{2}> {3}\t\t\t{4}\n", frame, time, pTarget.GetName(), pEventString, pString);
        LogString(bufferString);
    }

    protected void InitFile()
    {
        // Set the string
        m_sFile = "%log%/";
        m_sFile += AIRECORDER_FILENAME;

        // Open to wipe and write any preamble
        // (port: AZ::IO + fxopen + FileIO::FPutS — file IO ports pending Phase 11)
    }

    protected void InitFileSecondary()
    {
        m_sFileSecondary = "%root%/";
        m_sFileSecondary += AIRECORDER_SECONDARYFILENAME;
        // (port: file IO pending)
    }

    protected void LogString(string pString)
    {
        bool mergeWithLog = gAIEnv.CVars.RecordLog != 0;
        if (!mergeWithLog)
        {
            if (string.IsNullOrEmpty(m_sFile))
                InitFile();
            // (port: file IO pending)
        }
        else
        {
            GetAISystem().LogEvent("<AIrec>", pString);
        }
    }

    protected void LogStringSecondary(string pString)
    {
        if (string.IsNullOrEmpty(m_sFile))
        {
            InitFileSecondary();
        }
        // (port: file IO pending)
    }

    // Empty indicates currently unused
    // Has to be mutable right now because it changes on first logging
    protected /*mutable*/ string m_sFile = "";
    protected /*mutable*/ string m_sFileSecondary = "";
}
