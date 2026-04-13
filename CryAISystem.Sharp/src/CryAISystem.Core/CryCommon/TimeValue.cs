// Literal port of dev/Code/CryEngine/CryCommon/TimeValue.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public struct CTimeValue
{
    public const int64 TIMEVALUE_PRECISION = 100000;            // one second

    public void GetMemoryUsage(ICrySizer pSizer) {/*nothing*/}

    // Default constructor.
    public CTimeValue()
    {
        m_lValue = 0;
    }

    // Constructor.
    public CTimeValue(float fSeconds)
    {
        m_lValue = (int64)(fSeconds * TIMEVALUE_PRECISION);
    }

    public CTimeValue(double fSeconds)
    {
        m_lValue = (int64)(fSeconds * TIMEVALUE_PRECISION);
    }

    // Constructor.
    // Arguments:
    //		inllValue - positive negative, absolute or relative in 1 second= TIMEVALUE_PRECISION units.
    public CTimeValue(int64 inllValue)
    {
        m_lValue = inllValue;
    }

    // Use only for relative value, absolute values suffer a lot from precision loss.
    public float GetSeconds()
    {
        return m_lValue * (1f / TIMEVALUE_PRECISION);
    }

    // Get relative time difference in seconds - call on the endTime object:  endTime.GetDifferenceInSeconds( startTime );
    public float GetDifferenceInSeconds(CTimeValue startTime)
    {
        return (m_lValue - startTime.m_lValue) * (1f / TIMEVALUE_PRECISION);
    }

    public void SetSeconds(float infSec)
    {
        m_lValue = (int64)(infSec * TIMEVALUE_PRECISION);
    }

    public void SetSeconds(double infSec)
    {
        m_lValue = (int64)(infSec * TIMEVALUE_PRECISION);
    }

    public void SetSeconds(int64 indwSec)
    {
        m_lValue = indwSec * TIMEVALUE_PRECISION;
    }

    public void SetMilliSeconds(int64 indwMilliSec)
    {
        m_lValue = indwMilliSec * (TIMEVALUE_PRECISION / 1000);
    }

    // Use only for relative value, absolute values suffer a lot from precision loss.
    public float GetMilliSeconds()
    {
        return m_lValue * (1000f / TIMEVALUE_PRECISION);
    }

    public int64 GetMilliSecondsAsInt64()
    {
        return m_lValue * 1000 / TIMEVALUE_PRECISION;
    }

    public int64 GetMicroSecondsAsInt64()
    {
        return m_lValue * (1000 * 1000) / TIMEVALUE_PRECISION;
    }

    public int64 GetValue()
    {
        return m_lValue;
    }

    public void SetValue(int64 val)
    {
        m_lValue = val;
    }

    // Description:
    //		Useful for periodic events (e.g. water wave, blinking).
    //		Changing TimePeriod can results in heavy changes in the returned value.
    // Return Value:
    //   [0..1[
    public float GetPeriodicFraction(CTimeValue TimePeriod)
    {
        // todo: change float implement to int64 for more precision
        float fAbs = GetSeconds() / TimePeriod.GetSeconds();
        return fAbs - (int)(fAbs);
    }

    // math operations -----------------------

    // Minus.
    public static CTimeValue operator -(CTimeValue self, CTimeValue inRhs) { CTimeValue ret = new(); ret.m_lValue = self.m_lValue - inRhs.m_lValue; return ret; }
    // Plus.
    public static CTimeValue operator +(CTimeValue self, CTimeValue inRhs) { CTimeValue ret = new(); ret.m_lValue = self.m_lValue + inRhs.m_lValue; return ret; }
    // Unary minus.
    public static CTimeValue operator -(CTimeValue self) { CTimeValue ret = new(); ret.m_lValue = -self.m_lValue; return ret; }

    // comparison -----------------------

    public static bool operator <(CTimeValue self, CTimeValue inRhs) { return self.m_lValue < inRhs.m_lValue; }
    public static bool operator >(CTimeValue self, CTimeValue inRhs) { return self.m_lValue > inRhs.m_lValue; }
    public static bool operator >=(CTimeValue self, CTimeValue inRhs) { return self.m_lValue >= inRhs.m_lValue; }
    public static bool operator <=(CTimeValue self, CTimeValue inRhs) { return self.m_lValue <= inRhs.m_lValue; }
    public static bool operator ==(CTimeValue self, CTimeValue inRhs) { return self.m_lValue == inRhs.m_lValue; }
    public static bool operator !=(CTimeValue self, CTimeValue inRhs) { return self.m_lValue != inRhs.m_lValue; }

    public override bool Equals(object obj) { return obj is CTimeValue v && v.m_lValue == m_lValue; }
    public override int GetHashCode() { return m_lValue.GetHashCode(); }

    public void GetMemoryStatistics(ICrySizer pSizer) {/*nothing*/}

    // ----------------------------------------------------------

    private int64 m_lValue;                                                // absolute or relative value in 1/TIMEVALUE_PRECISION, might be negative

    // friend class CTimer;
}

// Forward declaration shim — full literal port of CrySizer.h is deferred.
public interface ICrySizer
{
    void AddObject(object obj, nuint size) { }
    void AddContainer<T>(System.Collections.Generic.ICollection<T> container) { }
}
