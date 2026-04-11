// Literal port of dev/Code/CryEngine/CryCommon/IFactionMap.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

public interface IFactionMap
{
    public enum ReactionType
    {
        Hostile = 0, // intentionally from most-hostile to most-friendly
        Neutral,
        Friendly,
    }

    public const uint8 InvalidFactionID = 0xff;

    // <interfuscator:shuffle>
    // virtual ~IFactionMap(){}
    uint32 GetFactionCount();
    string GetFactionName(uint8 fraction);
    uint8 GetFactionID(string name);

    void SetReaction(uint8 factionOne, uint8 factionTwo, IFactionMap.ReactionType reaction);
    IFactionMap.ReactionType GetReaction(uint8 factionOne, uint8 factionTwo);
    // </interfuscator:shuffle>
}
