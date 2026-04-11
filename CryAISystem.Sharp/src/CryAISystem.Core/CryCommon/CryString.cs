// Literal port shell of dev/Code/CryEngine/CryCommon/CryString.h (subset).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// Full CryString.h is 2372 lines (CryStringT<T>, stack_string, etc.). Ported on demand.

using System.Text;

namespace CryAISystem.CryCommon;

// stack_string in C++ is a fixed-buffer string with stack storage. We model it as a thin wrapper over
// StringBuilder/string. Calls in literal AI ports are: `stack_string s; s.Format("...", args); s.c_str();`
public class stack_string
{
    private string m_value = string.Empty;

    public stack_string() { }
    public stack_string(string s) { m_value = s; }

    public void Format(string format, params object[] args)
    {
        m_value = string.Format(format, args);
    }

    public string c_str() { return m_value; }

    public static implicit operator string(stack_string s) { return s.m_value; }
}

// CryLog free function declarations (port of CryLog.h log helpers).
public static class CryLog
{
    public static void CryLogAlways(string format, params object[] args) { }
    public static void CryWarning(string format, params object[] args) { }
}
