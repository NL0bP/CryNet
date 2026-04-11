// Literal port of dev/Code/CryEngine/CryAISystem/StatsManager.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Stores global AI statistics and provides display/output methods
// Notes       : Should become a flexible bag of numbers with a strong visualisation
//               Should also support any stats stored locally in the code

namespace CryAISystem;

// Currently available stats
public enum EStatistics
{
    eStat_ActiveActors,                                     // Enabled AI Actors
    eStat_FullUpdates,                                       // Full updates performed in a frame
    eStat_SyncTPSQueries,                                    // TPS queries called synchronously this frame
    eStat_AsyncTPSQueries,                                   // TPS queries called _Asynchronously_ this frame
    eStat_Last                                               // Sentinel value
}

// Ideas: //	(TPS) eStat_GenerationTime, eStat_TotalTime,



public enum EStatsReset
{
    eStatReset_Frame,                                        // Reset just values accumulated over a frame
    eStatReset_All,                                          // Reset all values
}


public class CStatsManager
{
    public CStatsManager()
    {
        m_fValues = new float[(int)EStatistics.eStat_Last];

        InitDescriptions();

        Reset(EStatsReset.eStatReset_All);
    }

    // ~CStatsManager() — managed array, no explicit free needed

    public void Reset(EStatsReset eReset)
    {
        // Reset some or all of the stats values
        for (uint32 i = 0; i < (uint32)EStatistics.eStat_Last; i++)
        {
            switch (eReset)
            {
                case EStatsReset.eStatReset_Frame:
                    if ((m_sMetadata[i].flags & (int)EStatFlags.eSF_FrameReset) == 0)
                        break;
                    // Otherwise, fall-through
                    goto case EStatsReset.eStatReset_All;
                case EStatsReset.eStatReset_All:
                    m_fValues[i] = 0.0f;
                    break;
            }
        }
    }

    public float GetStat(EStatistics eStat)
    {
        System.Diagnostics.Debug.Assert((int)eStat < (int)EStatistics.eStat_Last);
        return m_fValues[(int)eStat];
    }

    public void SetStat(EStatistics eStat, float fValue)
    {
        System.Diagnostics.Debug.Assert((int)eStat < (int)EStatistics.eStat_Last);
        m_fValues[(int)eStat] = fValue;
    }

    // Convenience method - it's very common for these to be integers and few problems converting
    public void SetStat(EStatistics eStat, int iValue)
    { SetStat(eStat, (float)iValue); }

    // Useful formatting strings
    // 30 characters total are allowed for this in the standard view!
    // 20 (max) of those are usually used for the short description
    private const string sFmtDefault = "%s: %3.1f";

    public void Render()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        const float xPos = 78.0f;
        const float yPos = 45.0f;
        const float yStep = 2.0f;

        if (gAIEnv.CVars.StatsDisplayMode == 0)
            return;

        float yOffset = 0.0f;

        const string sTitle = "------- AI Statistics -------";
        dc.Op_Arrow().TextToScreen(xPos, yPos, sTitle);
        yOffset += yStep;

        for (uint32 i = 0; i < (uint32)EStatistics.eStat_Last; i++, yOffset += yStep)
        {
            SStatMetadata metadata = m_sMetadata[i];
            string sFormSpec = metadata.format != null ? metadata.format : sFmtDefault;
            dc.Op_Arrow().TextToScreen(xPos, yPos + yOffset, sFormSpec, metadata.description, m_fValues[i]);
        }
    }

    private void InitDescriptions()
    {
        // The list of statistics metadata
        SStatMetadata[] sMetadata = new SStatMetadata[]
        {
            new SStatMetadata { description = "Enabled AI Actors",  flags = (int)EStatFlags.eSF_FrameReset | (int)EStatFlags.eSF_Integer, format = null },
            new SStatMetadata { description = "AI full updates",    flags = (int)EStatFlags.eSF_FrameReset, format = null },
            new SStatMetadata { description = "TPS queries (sync)", flags = (int)EStatFlags.eSF_FrameReset, format = null },
            new SStatMetadata { description = "TPS queries (async)",flags = (int)EStatFlags.eSF_FrameReset, format = null },
        };

        // Make sure that the number of descriptions never varies from the size of the enum
        System.Diagnostics.Debug.Assert(sMetadata.Length == (int)EStatistics.eStat_Last);

        m_sMetadata = sMetadata;
    }

    private enum EStatFlags
    {
        eSF_FrameReset = 1,                           // Accumulated over a frame, so 0 on frame-reset
        eSF_Integer = 1 << 1,                         // Actually an integer - treat it as such
    }

    private struct SStatMetadata
    {
        public string description;          // Short textual description - bounded at 20 to fit neatly on screen
        public int flags;                   // EStatFlags	specifying how to treat the value
        public string format;               // Optional printf format specifier
    }

    private float[] m_fValues;
    private SStatMetadata[] m_sMetadata;
}
