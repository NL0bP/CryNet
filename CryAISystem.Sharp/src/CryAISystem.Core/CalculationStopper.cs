// Literal port of dev/Code/CryEngine/CryAISystem/CalculationStopper.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Temp file holding code extracted from CAISystem.h/cpp

namespace CryAISystem;

//#define CALIBRATE_STOPPER
//#define DEBUG_STOPPER
//#define STOPPER_CAN_USE_COUNTER

/// Used to determine when a calculation should stop. Normally this would be after a certain time (dt). However,
/// if m_useCounter has been set then the times are internally converted to calls to ShouldCalculationStop using
/// callRate, which should have been estimated previously
public class CCalculationStopper
{
    /// name is used during calibration. fCalculationTime is the amount of time (in seconds).
    /// fCallsPerSecond is for when we are running in "counter" mode - the time
    /// gets converted into a number of calls to ShouldCalculationStop
    public CCalculationStopper(string szName, float fCalculationTime, float fCallsPerSecond)
    {
        m_endTime = gEnv.pTimer.GetAsyncTime() + new CTimeValue(fCalculationTime);

#if STOPPER_CAN_USE_COUNTER
        if (m_useCounter)
        {
            m_stopCounter = (uint)(dt * fCallsPerSecond);
            if (m_stopCounter < 1)
                m_stopCounter = 1;
        }
#endif

#if CALIBRATE_STOPPER
        m_name = name;
        m_calls = 0;
        m_dt = dt;
#endif
    }

    public bool ShouldCalculationStop()
    {
#if DEBUG_STOPPER
        if (m_neverStop)
            return false;
#endif

#if STOPPER_CAN_USE_COUNTER
        if (m_useCounter)
        {
            if (m_stopCounter > 0)
            {
                --m_stopCounter;
            }
            return m_stopCounter == 0;
        }
        else
#endif

        {
#if CALIBRATE_STOPPER
            ++m_calls;
            if (gEnv.pTimer.GetAsyncTime() > m_endTime)
            {
                var record = m_mapCallRate[m_name];
                record.first += m_calls;
                record.second += m_dt;
                return true;
            }
            else
            {
                return false;
            }
#else
            return gEnv.pTimer.GetAsyncTime() > m_endTime;
#endif
        }
    }

    public float GetSecondsRemaining()
    {
#if STOPPER_CAN_USE_COUNTER
        if (m_useCounter)
        {
            return m_stopCounter / m_fCallsPerSecond;
        }
        else
#endif
        {
            return (m_endTime - gEnv.pTimer.GetAsyncTime()).GetSeconds();
        }
    }

#if STOPPER_CAN_USE_COUNTER
    public static bool m_useCounter;
#endif

    private CTimeValue m_endTime;

#if STOPPER_CAN_USE_COUNTER
    private /*mutable*/ uint m_stopCounter; // if > 0 use this - stop when it's 0
    private float m_fCallsPerSecond;
#endif

#if DEBUG_STOPPER
    private static bool m_neverStop; // for debugging
#endif

#if CALIBRATE_STOPPER
    public /*mutable*/ uint m_calls;
    public float m_dt;
    public string m_name;
    /// the pair is calls and time (seconds)
    // typedef std::map< string, std::pair<unsigned, float> > TMapCallRate;
    public static System.Collections.Generic.SortedDictionary<string, (uint first, float second)> m_mapCallRate = new();
#endif
}
