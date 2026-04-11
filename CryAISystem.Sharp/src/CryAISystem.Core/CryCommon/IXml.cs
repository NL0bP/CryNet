// Literal port of dev/Code/CryEngine/CryCommon/IXml.h (subset).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// In C++ XmlNodeRef is a smart pointer to IXmlNode (typedef _smart_ptr<IXmlNode>).
// In C# we collapse to a reference type IXmlNode and alias the name.
public interface IXmlNode
{
    string getTag();
    int getLine();
    int getChildCount();
    IXmlNode getChild(int i);

    bool haveAttr(string key);
    bool getAttr(string key, out string value);
    bool getAttr(string key, out int value);
    bool getAttr(string key, out float value);
    bool getAttr(string key, out bool value);
}

// XmlNodeRef alias — keep the C++ name in literal ports
public class XmlNodeRef
{
    public IXmlNode Node;
    public XmlNodeRef() { Node = null; }
    public XmlNodeRef(IXmlNode n) { Node = n; }
    public static implicit operator XmlNodeRef(string nullLiteral) { return null; }
    public static implicit operator bool(XmlNodeRef self) { return self != null && self.Node != null; }

    public string getTag() => Node.getTag();
    public int getLine() => Node.getLine();
    public int getChildCount() => Node.getChildCount();
    public XmlNodeRef getChild(int i) => new XmlNodeRef(Node.getChild(i));
    public bool haveAttr(string key) => Node.haveAttr(key);
    public bool getAttr(string key, out string v) => Node.getAttr(key, out v);
    public bool getAttr(string key, out int v) => Node.getAttr(key, out v);
    public bool getAttr(string key, out float v) => Node.getAttr(key, out v);
    public bool getAttr(string key, out bool v) => Node.getAttr(key, out v);
}
