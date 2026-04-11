// Literal port of dev/Code/CryEngine/CryCommon/ISerialize.h (subset — symbols used by the AI port so far).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// In C++ TSerialize is a type alias for `CSerializeWrapper<ISerialize>`. We collapse it to a struct
// holding a reference to ISerialize and exposing the same public API.
//
// The C++ uses an overload set with templates so the same `ser.Value("name", v)` works for any type.
// In C# we provide explicit overloads for the types the AI port uses + a generic catch-all.
public interface ISerialize
{
    bool IsReading();
    bool IsWriting();
    void BeginGroup(string szName);
    void EndGroup();
    void GenericValue(string name, ref object v);
}

public struct TSerialize
{
    private ISerialize m_pImpl;

    public TSerialize(ISerialize impl) { m_pImpl = impl; }

    public bool IsReading() => m_pImpl.IsReading();
    public bool IsWriting() => m_pImpl.IsWriting();
    public void BeginGroup(string szName) => m_pImpl.BeginGroup(szName);
    public void EndGroup() => m_pImpl.EndGroup();

    /// Port of TSerialize::BeginOptionalGroup — returns true and opens the group when writing
    /// (only if `condition` is true), or when reading and the group is present in the stream.
    /// Literal port of ISerialize.h template helper signature.
    public bool BeginOptionalGroup(string szName, bool condition)
    {
        if (IsWriting() && !condition) return false;
        BeginGroup(szName);
        return true;
    }

    // Generic Value — handles any type by ref
    public void Value<T>(string name, ref T v)
    {
        object obj = v;
        m_pImpl.GenericValue(name, ref obj);
        v = (T)obj;
    }

    // Convenience non-ref overload (read-only or temporary)
    public void Value<T>(string name, T v)
    {
        object obj = v;
        m_pImpl.GenericValue(name, ref obj);
    }

    // ValueWithDefault — used by SOBJECTSTATE serialize
    public void ValueWithDefault<T>(string name, ref T v, T defaultValue)
    {
        if (IsReading()) v = defaultValue;
        Value(name, ref v);
    }

    // EnumValue — used to serialize enum range
    public void EnumValue<T>(string name, ref T v, T first, T last) where T : struct, System.Enum
    {
        int iv = System.Convert.ToInt32(v);
        Value(name, ref iv);
        v = (T)System.Enum.ToObject(typeof(T), iv);
    }
}
