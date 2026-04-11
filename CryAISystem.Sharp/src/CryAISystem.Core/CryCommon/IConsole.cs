// Literal port of dev/Code/CryEngine/CryCommon/IConsole.h (subset).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public interface ICVar
{
    int GetIVal();
    float GetFVal();
    string GetString();
    void Set(string s);
    void Set(float f);
    void Set(int i);
}

public interface IConsole
{
    ICVar GetCVar(string name);
    ICVar RegisterFloat(string sName, float fValue, int nFlags, string help);
    ICVar RegisterInt(string sName, int iValue, int nFlags, string help);
    ICVar RegisterString(string sName, string sValue, int nFlags, string help);
}
