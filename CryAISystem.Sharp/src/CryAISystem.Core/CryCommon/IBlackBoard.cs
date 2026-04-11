// Literal port of dev/Code/CryEngine/CryCommon/IBlackBoard.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public interface IBlackBoard
{
    // <interfuscator:shuffle>
    SmartScriptTable GetForScript();
    void SetFromScript(SmartScriptTable bb);
    void Clear();
    // virtual ~IBlackBoard(){}
    // </interfuscator:shuffle>
}
