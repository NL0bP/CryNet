// Literal port of dev/Code/CryEngine/CryAISystem/XMLUtils.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem;

public static class XMLUtils
{
    public enum BoolType
    {
        Invalid = -1,
        False = 0,
        True = 1,
    }

    public static BoolType ToBoolType(string str)
    {
        if (string.Compare(str, "1", true) == 0 || string.Compare(str, "true", true) == 0 || string.Compare(str, "yes", true) == 0)
            return BoolType.True;

        if (string.Compare(str, "0", true) == 0 || string.Compare(str, "false", true) == 0 || string.Compare(str, "no", true) == 0)
            return BoolType.False;

        return BoolType.Invalid;
    }

    public static BoolType GetBoolType(XmlNodeRef node, string attribute, BoolType deflt)
    {
        if (node.haveAttr(attribute))
        {
            string value;
            node.getAttr(attribute, out value);

            return ToBoolType(value);
        }

        return deflt;
    }
}
