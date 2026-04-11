// Literal port of dev/Code/CryEngine/CryAISystem/PersonalLog.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// #if !defined(_RELEASE) && defined(WIN32)
// #  define AI_COMPILE_WITH_PERSONAL_LOG
// #endif

// An actor keeps a personal log where messages are stored.
// This text can be rendered to the screen and will be recorded
// in the AI Recorded for later inspection.
public class PersonalLog
{
    public class Messages : LinkedList<string> { }

    public void AddMessage(uint entityId, string message)
    {
        if (m_messages.Count + 1 > 20)
            m_messages.RemoveFirst();

        m_messages.AddLast(message);

        if (gAIEnv.CVars.OutputPersonalLogToConsole != 0)
        {
            string name = "(null)";

            IEntity entity = gEnv.pEntitySystem != null ? gEnv.pEntitySystem.GetEntity(entityId) : null;
            if (entity != null)
                name = entity.GetName();

            gEnv.pLog.Log("Personal Log [{0}] {1}", name, message);
        }

#if CRYAISYSTEM_DEBUG
        if (gEnv.pEntitySystem != null)
        {
            IEntity entity = gEnv.pEntitySystem.GetEntity(entityId);
            if (entity != null)
            {
                IAIObject ai = entity.GetAI();
                if (ai != null)
                {
                    IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(message);
                    ai.RecordEvent(IAIRecordable.e_AIDbgEvent.E_PERSONALLOG, ref recorderEventData);
                }
            }
        }
#endif
    }

    public Messages GetMessages() { return m_messages; }
    public void Clear() { m_messages.Clear(); }

    private Messages m_messages = new Messages();
}
