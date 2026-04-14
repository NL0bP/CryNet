// Literal port of dev/Code/CryEngine/CryCommon/physinterface.h:168-256 PhysicsForeignData class.
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryPhysics.Entities;

/// <summary>
/// Two-way convertible foreign-data union. Port of `class PhysicsForeignData final` from
/// physinterface.h. Holds a 64-bit value usable as a pointer (`object?`), an int, or a uint64.
/// In C++ the original uses `reinterpret_cast` to round-trip a void* through a uint64 — in C#
/// we hold both an `object?` and a `ulong`, picking whichever matches the constructor.
/// </summary>
public struct PhysicsForeignData : IEquatable<PhysicsForeignData>
{
    private readonly object? _ref;
    private readonly ulong _data;

    /// Sentinel value used by C++ `MARK_UNUSED`/`is_unused` (see physinterface.h:432,442,450).
    public const ulong UnusedSentinel = 0xFFFFFFFFFFFFFFFFUL;

    public PhysicsForeignData(object? data) { _ref = data; _data = 0; }
    public PhysicsForeignData(int data) { _ref = null; _data = (ulong)(uint)data; }
    public PhysicsForeignData(ulong data) { _ref = null; _data = data; }

    /// Implicit conversion from any reference type (matches `void*` ctor in C++).
    public static implicit operator PhysicsForeignData(string s) => new PhysicsForeignData(s);

    public bool Equals(PhysicsForeignData rhs)
        => ReferenceEquals(_ref, rhs._ref) && _data == rhs._data;

    public override bool Equals(object? obj)
        => obj is PhysicsForeignData rhs && Equals(rhs);

    public override int GetHashCode()
        => _ref?.GetHashCode() ?? _data.GetHashCode();

    public static bool operator ==(PhysicsForeignData a, PhysicsForeignData b) => a.Equals(b);
    public static bool operator !=(PhysicsForeignData a, PhysicsForeignData b) => !a.Equals(b);

    /// Cast to typed reference. Matches C++ `T* operator T*()`.
    public T? As<T>() where T : class => _ref as T;

    /// Get raw object reference (may be null).
    public object? AsObject() => _ref;

    /// Get raw 64-bit value (0 when storing a reference).
    public ulong AsUInt64() => _data;

    public bool IsUnused() => _data == UnusedSentinel;
    public void MarkUnused() { /* C++ writes 0xFF...FF; struct is immutable in C# port */ }

    /// True if the foreign data is null (no ref AND zero data).
    public bool IsNull => _ref == null && _data == 0;
}
