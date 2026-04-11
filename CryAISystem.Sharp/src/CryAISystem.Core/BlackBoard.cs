// Literal port of dev/Code/CryEngine/CryAISystem/BlackBoard.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

//   Description : Script Binding for Item

using CryAISystem.CryCommon;

namespace CryAISystem;

public class CBlackBoard : IBlackBoard
{
    // virtual ~CBlackBoard(){}

    public CBlackBoard()
    {
        m_BB.Create(gEnv.pSystem.GetIScriptSystem());
    }

    public virtual SmartScriptTable GetForScript() { return m_BB; }
    public virtual void SetFromScript(SmartScriptTable sourceBB)
    {
        m_BB.Clone(sourceBB, true);
    }
    public virtual void Clear() { m_BB.Clear(); }

    private SmartScriptTable m_BB;
}
