// Literal port of dev/Code/CryEngine/CryCommon/IScriptSystem.h + ScriptHelpers.h (subset — symbols used by the AI port so far).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public interface IScriptSystem
{
    // (full IScriptSystem will be ported when more files demand it)
}

public interface IScriptTable
{
    void Clear();
    bool Clone(IScriptTable srcTable, bool deepCopy);
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
}
