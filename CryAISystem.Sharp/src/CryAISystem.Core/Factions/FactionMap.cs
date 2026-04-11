// Literal port of dev/Code/CryEngine/CryAISystem/Factions/FactionMap.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem.Factions;

public class CFactionMap : IFactionMap
{
    public const int MaxFactionCount = 32;

    public CFactionMap()
    {
        Clear();
        LoadConfig("Scripts/AI/Factions.xml");
    }

    public uint32 GetFactionCount()
    {
        return (uint32)m_names.Count;
    }

    public string GetFactionName(uint8 faction)
    {
        FactionNames.Enumerator it = m_names.GetEnumerator();
        string found;
        if (m_names.TryGetValue(faction, out found))
            return found;

        return null;
    }

    public uint8 GetFactionID(string name)
    {
        uint8 id;
        if (m_ids.TryGetValue(name, out id))
            return id;

        return IFactionMap.InvalidFactionID;
    }

    public void Clear()
    {
        m_names.Clear();
        m_ids.Clear();

        for (uint32 i = 0; i < MaxFactionCount; ++i)
            for (uint32 j = 0; j < MaxFactionCount; ++j)
                m_reactions[i, j] = (uint8)((i != j) ? IFactionMap.ReactionType.Hostile : IFactionMap.ReactionType.Friendly);
    }

    public void Reload()
    {
        Clear();

        FileNames.Enumerator it = m_fileNames.GetEnumerator();

        for (int i = 0; i < m_fileNames.Count; ++i)
            LoadConfig(m_fileNames[i]);
    }

    private struct ReactionInfo
    {
        public string factionName;
        public uint32 line;
        public IFactionMap.ReactionType reactionType;
    }

    public bool LoadConfig(string fileName)
    {
        if (!gEnv.pCryPak.IsFileExist(fileName))
            return false;

        stl.push_back_unique(m_fileNames, fileName);

        XmlNodeRef rootNode = GetISystem().LoadXmlFromFile(fileName);

        if (rootNode == null)
        {
            AILog.AIWarning("Failed to open XML file '{0}'...", fileName);

            return false;
        }

        IFactionMap.ReactionType[] defaultReactions = new IFactionMap.ReactionType[MaxFactionCount];
        for (uint32 i = 0; i < MaxFactionCount; ++i)
            defaultReactions[i] = IFactionMap.ReactionType.Hostile;

        Dictionary<uint8, List<ReactionInfo>> reactions = new Dictionary<uint8, List<ReactionInfo>>();

        string tagName = rootNode.getTag();

        if (string.Compare(tagName, "Factions", true) == 0)
        {
            int factionNodeCount = rootNode.getChildCount();

            for (int i = 0; i < factionNodeCount; ++i)
            {
                XmlNodeRef factionNode = rootNode.getChild(i);

                if (string.Compare(factionNode.getTag(), "Faction", true) == 0)
                {
                    string name;
                    if (!factionNode.getAttr("name", out name))
                    {
                        AILog.AIWarning("Missing 'name' attribute for 'Faction' tag in file '{0}' at line {1}...", fileName,
                            factionNode.getLine());

                        return false;
                    }

                    uint8 factionID = (uint8)m_names.Count;
                    if (factionID >= MaxFactionCount)
                    {
                        AILog.AIWarning("Maximum number of allowed factions reached in file '{0}' at line '{1}'!", fileName,
                            factionNode.getLine());

                        return false;
                    }

                    m_names.Add(factionID, name);
                    bool inserted = !m_ids.ContainsKey(name);
                    if (inserted) m_ids.Add(name, factionID);

                    if (!inserted)
                    {
                        AILog.AIWarning("Duplicate faction '{0}' in file '{1}' at line {2}...", name, fileName, factionNode.getLine());

                        return false;
                    }

                    IFactionMap.ReactionType defaultReactionType = IFactionMap.ReactionType.Hostile;

                    string defaultReaction;
                    if (factionNode.getAttr("default", out defaultReaction) && !string.IsNullOrEmpty(defaultReaction))
                    {
                        if (!GetReactionType(defaultReaction, out defaultReactionType))
                        {
                            AILog.AIWarning("Invalid default reaction '{0}' in file '{1}' at line '{2}'...",
                                defaultReaction, fileName, factionNode.getLine());

                            return false;
                        }
                    }

                    defaultReactions[factionID] = defaultReactionType;

                    uint32 reactionNodeCount = (uint32)factionNode.getChildCount();
                    for (uint32 j = 0; j < reactionNodeCount; ++j)
                    {
                        XmlNodeRef reactionNode = factionNode.getChild((int)j);

                        if (string.Compare(reactionNode.getTag(), "Reaction", true) == 0)
                        {
                            string faction;
                            if (!reactionNode.getAttr("faction", out faction))
                            {
                                AILog.AIWarning("Missing 'faction' attribute for 'Reaction' tag in file '{0}' at line {1}...", fileName,
                                    reactionNode.getLine());

                                return false;
                            }

                            string reaction;
                            if (!reactionNode.getAttr("reaction", out reaction))
                            {
                                AILog.AIWarning("Missing 'reaction' attribute for 'Reaction' tag in file '{0}' at line {1}...", fileName,
                                    reactionNode.getLine());

                                return false;
                            }

                            IFactionMap.ReactionType reactionType = IFactionMap.ReactionType.Neutral;

                            if (!GetReactionType(reaction, out reactionType))
                            {
                                AILog.AIWarning("Invalid reaction '{0}' in file '{1}' at line '{2}'...", reaction, fileName, reactionNode.getLine());

                                //return false;
                            }

                            ReactionInfo info;
                            info.factionName = faction;
                            info.line = (uint32)reactionNode.getLine();
                            info.reactionType = reactionType;

                            if (!reactions.ContainsKey(factionID))
                                reactions.Add(factionID, new List<ReactionInfo>());

                            List<ReactionInfo> infos = reactions[factionID];
                            infos.Add(info);
                        }
                        else
                        {
                            AILog.AIWarning("Unexpected tag '{0}' in file '{1}' at line {2}...", reactionNode.getTag(), fileName,
                                reactionNode.getLine());

                            return false;
                        }
                    }
                }
                else
                {
                    AILog.AIWarning("Unexpected tag '{0}' in file '{1}' at line {2}...", factionNode.getTag(), fileName,
                        factionNode.getLine());

                    return false;
                }
            }
        }
        else
        {
            AILog.AIWarning("Unexpected tag '{0}' in file '{1}' at line {2}...", tagName, fileName, rootNode.getLine());

            return false;
        }

        for (uint32 i = 0; i < MaxFactionCount; ++i)
            for (uint32 j = 0; j < MaxFactionCount; ++j)
                m_reactions[i, j] = (uint8)((i != j) ? defaultReactions[i] : IFactionMap.ReactionType.Friendly);

        foreach (KeyValuePair<uint8, List<ReactionInfo>> it in reactions)
        {
            List<ReactionInfo> infos = it.Value;

            for (int rifit = 0; rifit < infos.Count; ++rifit)
            {
                ReactionInfo info = infos[rifit];

                uint8 idValue;
                if (m_ids.TryGetValue(info.factionName, out idValue))
                    SetReaction(it.Key, idValue, info.reactionType);
                else
                {
                    AILog.AIWarning("Unknown faction '{0}' in file '{1}' at line '{2}'...", info.factionName, fileName, info.line);

                    return false;
                }
            }
        }

        return true;
    }

    public void SetReaction(uint8 factionOne, uint8 factionTwo, IFactionMap.ReactionType reaction)
    {
        if ((factionOne < MaxFactionCount) && (factionTwo < MaxFactionCount))
        {
            m_reactions[factionOne, factionTwo] = (uint8)reaction;

            // Marcio: HAX
            var it = GetAISystem().m_mapFaction.GetEnumerator();

            while (it.MoveNext())
            {
                if (it.Current.Key == factionOne)
                {
                    CAIObject obj = it.Current.Value.GetAIObject();
                    if (obj != null)
                    {
                        CAIActor actor = obj.CastToCAIActor();
                        if (actor != null)
                            actor.ReactionChanged(factionTwo, reaction);
                    }
                }
                else
                    break;
            }
        }
    }

    public IFactionMap.ReactionType GetReaction(uint8 factionOne, uint8 factionTwo)
    {
        if ((factionOne < MaxFactionCount) && (factionTwo < MaxFactionCount))
            return (IFactionMap.ReactionType)m_reactions[factionOne, factionTwo];

        if (factionOne == IFactionMap.InvalidFactionID || factionTwo == IFactionMap.InvalidFactionID)
            return IFactionMap.ReactionType.Neutral;

        return IFactionMap.ReactionType.Hostile;
    }

    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("FactionMap");

        // find highest faction id
        uint32 highestId = 0;

        if (ser.IsWriting())
        {
            foreach (KeyValuePair<string, uint8> it in m_ids)
            {
                if (it.Value > highestId)
                    highestId = it.Value;
            }
        }

        ser.Value("SerializedFactionCount", ref highestId);

        stack_string nameFormatter = new stack_string();
        for (uint i = 0; i < highestId; ++i)
        {
            for (uint j = 0; j < highestId; ++j)
            {
                nameFormatter.Format("Reaction_{0}_to_{1}", i, j);
                ser.Value(nameFormatter.c_str(), ref m_reactions[i, j]);
            }
        }

        ser.EndGroup();
    }

    private bool GetReactionType(string reactionName, out IFactionMap.ReactionType reactionType)
    {
        if (string.Compare(reactionName, "Friendly", true) == 0)
            reactionType = IFactionMap.ReactionType.Friendly;
        else if (string.Compare(reactionName, "Hostile", true) == 0)
            reactionType = IFactionMap.ReactionType.Hostile;
        else if (string.Compare(reactionName, "Neutral", true) == 0)
            reactionType = IFactionMap.ReactionType.Neutral;
        else
        {
            reactionType = IFactionMap.ReactionType.Neutral;
            return false;
        }

        return true;
    }

    private uint8[,] m_reactions = new uint8[MaxFactionCount, MaxFactionCount];

    private class FactionNames : Dictionary<uint8, string> { }
    private FactionNames m_names = new FactionNames();

    private class FactionIds : Dictionary<string, uint8> { }
    private FactionIds m_ids = new FactionIds();

    private class FileNames : List<string> { }
    private FileNames m_fileNames = new FileNames();
}
