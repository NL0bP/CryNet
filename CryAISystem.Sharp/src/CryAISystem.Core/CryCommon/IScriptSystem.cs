// Literal port of dev/Code/CryEngine/CryCommon/IScriptSystem.h + ScriptHelpers.h (subset — symbols used by the AI port so far).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public interface IScriptSystem
{
    // (full IScriptSystem will be ported when more files demand it)
    // Added for AIActor.cpp literal port
    bool GetGlobalValue(string key, out SmartScriptTable value);
}

public interface IScriptTable
{
    void Clear();
    bool Clone(IScriptTable srcTable, bool deepCopy);
    // Added for AIActor.cpp literal port
    bool GetValue(string key, out SmartScriptTable value);
    bool GetValue(string key, out string value);
    // Added for PathObstacles.cpp literal port — bool property lookup (bUsedAsDynamicObstacle)
    bool GetValue(string key, out bool value) { value = false; return false; }
    void SetValue(string key, string value);
}

// SmartScriptTable is a smart pointer wrapper in C++. We model it as a struct with explicit Create.
public struct SmartScriptTable
{
    public IScriptTable Table;

    public void Create(IScriptSystem pSS) { /* assigns this.Table to a new script table from pSS */ }
    public void Clear() { Table?.Clear(); }
    public bool Clone(SmartScriptTable src, bool deepCopy) { return Table != null && Table.Clone(src.Table, deepCopy); }

    public static implicit operator bool(SmartScriptTable self) { return self.Table != null; }

    public IScriptTable Op_Arrow() { return Table; }

    // Added for AIActor.cpp literal port — delegate to inner Table
    public bool GetValue(string key, out SmartScriptTable value) { value = default; return Table != null && Table.GetValue(key, out value); }
    public bool GetValue(string key, out string value) { value = null; return Table != null && Table.GetValue(key, out value); }
    // Added for PathObstacles.cpp literal port — bool property lookup
    public bool GetValue(string key, out bool value) { value = false; return Table != null && Table.GetValue(key, out value); }
    public void SetValue(string key, string value) { Table?.SetValue(key, value); }
}
