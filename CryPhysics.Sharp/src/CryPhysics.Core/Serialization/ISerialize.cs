// Literal port of dev/Code/CryEngine/CryCommon/ISerialize.h (subset used by CryPhysics).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryPhysics.Serialization;

/// <summary>
/// Abstract serialization sink. Port of `class ISerialize`. Backends include text, binary,
/// network. C++ uses templates per-type; C# uses generic Value&lt;T&gt; with object boxing
/// (matching the CryAISystem.Sharp pattern in CryCommon/ISerialize.cs).
/// </summary>
public interface ISerialize
{
    bool IsReading();                                           // ISerialize.h:246
    bool IsWriting() => !IsReading();
    bool ShouldCommitValues() => true;                          // ISerialize.h:247
    void BeginGroup(string szName);                             // ISerialize.h:241
    bool BeginOptionalGroup(string szName, bool condition);     // ISerialize.h:242
    void EndGroup();                                            // ISerialize.h:243
    void FlagPartialRead() { }                                  // ISerialize.h:232
    void GenericValue(string name, ref object v);
    void GenericValueWithDefault(string name, ref object v, object defaultValue);
}

/// <summary>
/// Wrapper struct: in C++ `typedef CSerializeWrapper&lt;ISerialize&gt; TSerialize` (ISerialize.h:1005).
/// Mirrors CryAISystem's TSerialize struct so callers can pass by value as in C++.
/// </summary>
public struct TSerialize
{
    private readonly ISerialize _impl;

    public TSerialize(ISerialize impl) { _impl = impl; }

    public bool IsReading() => _impl.IsReading();
    public bool IsWriting() => _impl.IsWriting();
    public bool ShouldCommitValues() => _impl.ShouldCommitValues();
    public void BeginGroup(string szName) => _impl.BeginGroup(szName);
    public bool BeginOptionalGroup(string szName, bool condition) => _impl.BeginOptionalGroup(szName, condition);
    public void EndGroup() => _impl.EndGroup();
    public void FlagPartialRead() => _impl.FlagPartialRead();

    /// Templated Value&lt;T&gt; — by-ref for round-trip semantics (matches C++ T& parameter).
    public void Value<T>(string name, ref T v)
    {
        object obj = v!;
        _impl.GenericValue(name, ref obj);
        v = (T)obj;
    }

    /// Convenience non-ref overload (write-only).
    public void Value<T>(string name, T v)
    {
        object obj = v!;
        _impl.GenericValue(name, ref obj);
    }

    /// Port of ISerialize.h:260 ValueWithDefault.
    public void ValueWithDefault<T>(string name, ref T v, T defaultValue)
    {
        object obj = v!;
        _impl.GenericValueWithDefault(name, ref obj, defaultValue!);
        v = (T)obj;
    }

    /// Port of EnumValue (template specialization in ISerialize.h).
    public void EnumValue<T>(string name, ref T v) where T : struct, System.Enum
    {
        int iv = System.Convert.ToInt32(v);
        Value(name, ref iv);
        v = (T)System.Enum.ToObject(typeof(T), iv);
    }

    public void EnumValue<T>(string name, ref T v, T first, T last) where T : struct, System.Enum
        => EnumValue(name, ref v);
}

/// <summary>
/// RAII helper: opens an optional group on construction, closes on Dispose.
/// Port of SSerializeScopedBeginGroup (ISerialize.h:1043-1056).
/// Usage: `using var g = new SerializeScopedBeginGroup(ser, "name");`
/// </summary>
public readonly struct SerializeScopedBeginGroup : System.IDisposable
{
    private readonly TSerialize _ser;
    public SerializeScopedBeginGroup(TSerialize ser, string sGroupName) { _ser = ser; _ser.BeginGroup(sGroupName); }
    public void Dispose() => _ser.EndGroup();
}

/// <summary>
/// Obsolete binary stream class — used only by the legacy `int GetStateSnapshot(CStream&amp;,...)`
/// overloads. Port of `class CStream` (referenced in physinterface.h:2432-2434, "obsolete, was
/// used in Far Cry"). Provides minimal byte-stream read/write so the placeholder forwarding
/// methods compile and execute as in C++ (writing zero bytes is a no-op snapshot).
/// </summary>
public class CStream
{
    private readonly System.IO.MemoryStream _stream;
    private readonly System.IO.BinaryReader _reader;
    private readonly System.IO.BinaryWriter _writer;
    public bool IsReading { get; }

    public CStream(bool reading)
    {
        IsReading = reading;
        _stream = new System.IO.MemoryStream();
        _reader = new System.IO.BinaryReader(_stream);
        _writer = new System.IO.BinaryWriter(_stream);
    }

    public CStream(byte[] data)
    {
        IsReading = true;
        _stream = new System.IO.MemoryStream(data, false);
        _reader = new System.IO.BinaryReader(_stream);
        _writer = new System.IO.BinaryWriter(System.IO.Stream.Null);
    }

    public byte[] ToArray() => _stream.ToArray();

    public void Write(int v) => _writer.Write(v);
    public void Write(float v) => _writer.Write(v);
    public void Write(byte v) => _writer.Write(v);
    public int ReadInt() => _reader.ReadInt32();
    public float ReadFloat() => _reader.ReadSingle();
    public byte ReadByte() => _reader.ReadByte();
}
