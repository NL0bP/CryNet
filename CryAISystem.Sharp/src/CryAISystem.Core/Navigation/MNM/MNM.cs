// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/MNM.h (792L),
// FixedVec2.h (199L), FixedVec3.h (268L), FixedAABB.h (131L), IMNM.h,
// HashComputer.h (104L), OpenList.h (73L), Profiler.h (148L).
//
// All types are concrete instantiations of the C++ templates for the MNM namespace:
//   real_t    = fixed_t<int, 16>   (in FixedPoint.cs)
//   vector2_t = FixedVec2<int, 16>
//   vector3_t = FixedVec3<int, 16>
//   aabb_t    = FixedAABB<int, 16>
//
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static CryAISystem.Navigation.MNM.FixedPointMath;

namespace CryAISystem.Navigation.MNM;

// ============================================================================
// IMNM.h types
// ============================================================================

// MNM::Constants
public static class Constants
{
    public const uint InvalidEdgeIndex = ~0u;
    public const uint InvalidTileID = 0;
    public const uint InvalidTriangleID = 0;
    public const uint eStaticIsland_InvalidIslandID = 0;
    public const uint eStaticIsland_FirstValidIslandID = 1;
    public const ulong eGlobalIsland_InvalidIslandID = 0;
    public const uint eOffMeshLinks_InvalidOffMeshLinkID = 0;
}

// MNM::StaticIslandID
// typedef uint32 StaticIslandID;
// Using raw uint in C# (aliased where needed).

// MNM::GlobalIslandID
public struct GlobalIslandID : IComparable<GlobalIslandID>, IEquatable<GlobalIslandID>
{
    public ulong id;

    public GlobalIslandID(ulong defaultValue = Constants.eGlobalIsland_InvalidIslandID) { id = defaultValue; }

    public GlobalIslandID(uint navigationMeshID, uint islandID)
    {
        id = ((ulong)navigationMeshID << 32) | islandID;
    }

    public uint GetStaticIslandID() => (uint)(id & 0xFFFFFFFF);
    public uint GetNavigationMeshIDAsUint32() => (uint)(id >> 32);

    public static bool operator ==(GlobalIslandID a, GlobalIslandID b) => a.id == b.id;
    public static bool operator !=(GlobalIslandID a, GlobalIslandID b) => a.id != b.id;
    public static bool operator <(GlobalIslandID a, GlobalIslandID b) => a.id < b.id;
    public static bool operator >(GlobalIslandID a, GlobalIslandID b) => a.id > b.id;

    public bool Equals(GlobalIslandID other) => id == other.id;
    public override bool Equals(object obj) => obj is GlobalIslandID g && g.id == id;
    public override int GetHashCode() => id.GetHashCode();
    public int CompareTo(GlobalIslandID other) => id.CompareTo(other.id);
}

// MNM::OffMeshLink base class
public abstract class OffMeshLink
{
    public enum LinkType { eLinkType_Invalid = -1, eLinkType_SmartObject, eLinkType_Custom }

    protected LinkType m_linkType;
    protected uint m_entityId; // EntityId
    protected uint m_linkID;   // OffMeshLinkID

    protected OffMeshLink(LinkType linkType, uint entityId) { m_linkType = linkType; m_entityId = entityId; }

    public LinkType GetLinkType() => m_linkType;
    public void SetLinkID(uint linkID) { m_linkID = linkID; }
    public uint GetLinkId() => m_linkID;
    public uint GetEntityIdForOffMeshLink() => m_entityId;

    public abstract bool CanUse(IEntity pRequester, float[] costMultiplier);
    public abstract OffMeshLink Clone();
    public abstract Vec3 GetStartPosition();
    public abstract Vec3 GetEndPosition();
    public abstract void SetStartPosition(Vec3 pos);
    public abstract void SetEndPosition(Vec3 pos);
    public virtual bool IsEnabled() => true;
    public virtual OffMeshLink_SmartObject CastTo_SmartObject() { return this as OffMeshLink_SmartObject; }
}

// OffMeshLink_SmartObject — smart object link type
public class OffMeshLink_SmartObject : OffMeshLink
{
    public OffMeshLink_SmartObject() : base(LinkType.eLinkType_SmartObject, 0) { }
    public OffMeshLink_SmartObject(uint entityId) : base(LinkType.eLinkType_SmartObject, entityId) { }
    public uint GetSmartObjectId() { return m_entityId; }
    public CryAISystem.CSmartObject m_pSmartObject;
    public string m_pSmartObjectClass = "";
    public CryAISystem.SmartObjectHelper m_pFromHelper;
    public CryAISystem.SmartObjectHelper m_pToHelper;

    public override bool CanUse(CryAISystem.CryCommon.IEntity pRequester, float[] costMultiplier) => true;
    public override OffMeshLink Clone() => new OffMeshLink_SmartObject(m_entityId);
    public override Vec3 GetStartPosition() => new Vec3(0, 0, 0);
    public override Vec3 GetEndPosition() => new Vec3(0, 0, 0);
    public override void SetStartPosition(Vec3 pos) { }
    public override void SetEndPosition(Vec3 pos) { }
}

// OffMeshLinkPtr — shared_ptr<OffMeshLink> in C++
// In C# we just use the reference directly.
// DECLARE_BOOST_POINTERS(OffMeshLink) -> typedef shared_ptr
// We use a simple alias.

// ============================================================================
// FixedVec3<int, 16> — vector3_t
// ============================================================================
public struct vector3_t : IEquatable<vector3_t>
{
    public real_t x, y, z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector3_t(real_t _x) { x = _x; y = _x; z = _x; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector3_t(real_t _x, real_t _y, real_t _z) { x = _x; y = _y; z = _z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector3_t(Vec3 vec) { x = new real_t(vec.x); y = new real_t(vec.y); z = new real_t(vec.z); }

    // Construct from Tile::Vertex (FixedVec3<uint16, 5>)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector3_t(Tile.Vertex v)
    {
        x = v.x.ToReal();
        y = v.y.ToReal();
        z = v.z.ToReal();
    }

    // Indexer
    public real_t this[int i]
    {
        get { Debug.Assert(i < 3); return i switch { 0 => x, 1 => y, _ => z }; }
        set { Debug.Assert(i < 3); switch (i) { case 0: x = value; break; case 1: y = value; break; default: z = value; break; } }
    }

    // Arithmetic operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t operator +(vector3_t a, vector3_t b) =>
        new vector3_t(a.x + b.x, a.y + b.y, a.z + b.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t operator -(vector3_t a, vector3_t b) =>
        new vector3_t(a.x - b.x, a.y - b.y, a.z - b.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t operator *(vector3_t a, vector3_t b) =>
        new vector3_t(a.x * b.x, a.y * b.y, a.z * b.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t operator /(vector3_t a, vector3_t b) =>
        new vector3_t(a.x / b.x, a.y / b.y, a.z / b.z);

    // Scalar multiply/divide
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t operator *(vector3_t a, real_t s) =>
        new vector3_t(a.x * s, a.y * s, a.z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t operator /(vector3_t a, real_t s) =>
        new vector3_t(a.x / s, a.y / s, a.z / s);

    // Equality
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(vector3_t a, vector3_t b) =>
        a.x == b.x && a.y == b.y && a.z == b.z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(vector3_t a, vector3_t b) =>
        a.x != b.x || a.y != b.y || a.z != b.z;

    public bool Equals(vector3_t other) => this == other;
    public override bool Equals(object obj) => obj is vector3_t v && this == v;
    public override int GetHashCode() => HashCode.Combine(x.v, y.v, z.v);

    // Set
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(real_t _x, real_t _y, real_t _z) { x = _x; y = _y; z = _z; }

    // dot
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t dot(vector3_t other) => x * other.x + y * other.y + z * other.z;

    // approximatedLen
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t approximatedLen()
    {
        real_t heuristicFactor1 = new real_t(0.5f * (1.0f + (1.0f / (4.0f * MathF.Sqrt(3.0f)))));
        real_t heuristicFactor2 = new real_t(1.0f / MathF.Sqrt(3.0f));

        real_t targetDistAbsX = fabsf(x), targetDistAbsY = fabsf(y), targetDistAbsZ = fabsf(z);
        return heuristicFactor1 * FixedPointMath.min(heuristicFactor2 * (targetDistAbsX + targetDistAbsY + targetDistAbsZ),
            FixedPointMath.max(FixedPointMath.max(targetDistAbsX, targetDistAbsY), targetDistAbsZ));
    }

    // len — may overflow if len^2 > int.MaxValue
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t len() => sqrtf(lenSq());

    // lenNoOverflow
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t lenNoOverflow()
    {
        Debug.Assert(real_t.sqrtf(lenSqNoOverflow()) >= new real_t(0));
        return real_t.sqrtf(lenSqNoOverflow());
    }

    // lenSq — may overflow
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t lenSq()
    {
        var result = x * x + y * y + z * z;
        Debug.Assert(result >= new real_t(0));
        return result;
    }

    // lenSqNoOverflow — uses unsigned_overflow_type (ulong)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong lenSqNoOverflow() => x.sqr() + y.sqr() + z.sqr();

    // abs
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector3_t abs() => new vector3_t(fabsf(x), fabsf(y), fabsf(z));

    // normalized
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector3_t normalized()
    {
        real_t invLen = new real_t(1) / len();
        return new vector3_t(x * invLen, y * invLen, z * invLen);
    }

    // Static min/max
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t minimize(vector3_t a, vector3_t b) =>
        new vector3_t(FixedPointMath.min(a.x, b.x), FixedPointMath.min(a.y, b.y), FixedPointMath.min(a.z, b.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t minimize(vector3_t a, vector3_t b, vector3_t c) =>
        new vector3_t(
            FixedPointMath.min(FixedPointMath.min(a.x, b.x), c.x),
            FixedPointMath.min(FixedPointMath.min(a.y, b.y), c.y),
            FixedPointMath.min(FixedPointMath.min(a.z, b.z), c.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t maximize(vector3_t a, vector3_t b) =>
        new vector3_t(FixedPointMath.max(a.x, b.x), FixedPointMath.max(a.y, b.y), FixedPointMath.max(a.z, b.z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector3_t maximize(vector3_t a, vector3_t b, vector3_t c) =>
        new vector3_t(
            FixedPointMath.max(FixedPointMath.max(a.x, b.x), c.x),
            FixedPointMath.max(FixedPointMath.max(a.y, b.y), c.y),
            FixedPointMath.max(FixedPointMath.max(a.z, b.z), c.z));

    // GetVec3
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 GetVec3() => new Vec3(x.as_float(), y.as_float(), z.as_float());
}

// ============================================================================
// FixedVec2<int, 16> — vector2_t
// ============================================================================
public struct vector2_t : IEquatable<vector2_t>
{
    public real_t x, y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector2_t(real_t _x) { x = _x; y = _x; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector2_t(real_t _x, real_t _y) { x = _x; y = _y; }

    // Construct from vector3_t (explicit in C++)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector2_t(vector3_t v) { x = v.x; y = v.y; }

    // Indexer
    public real_t this[int i]
    {
        get { Debug.Assert(i < 2); return i == 0 ? x : y; }
        set { Debug.Assert(i < 2); if (i == 0) x = value; else y = value; }
    }

    // Arithmetic
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t operator +(vector2_t a, vector2_t b) =>
        new vector2_t(a.x + b.x, a.y + b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t operator -(vector2_t a, vector2_t b) =>
        new vector2_t(a.x - b.x, a.y - b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t operator *(vector2_t a, vector2_t b) =>
        new vector2_t(a.x * b.x, a.y * b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t operator /(vector2_t a, vector2_t b) =>
        new vector2_t(a.x / b.x, a.y / b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t operator *(vector2_t a, real_t s) =>
        new vector2_t(a.x * s, a.y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t operator /(vector2_t a, real_t s) =>
        new vector2_t(a.x / s, a.y / s);

    // Equality
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(vector2_t a, vector2_t b) => a.x == b.x && a.y == b.y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(vector2_t a, vector2_t b) => a.x != b.x || a.y != b.y;

    public bool Equals(vector2_t other) => this == other;
    public override bool Equals(object obj) => obj is vector2_t v && this == v;
    public override int GetHashCode() => HashCode.Combine(x.v, y.v);

    // dot
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t dot(vector2_t other) => x * other.x + y * other.y;

    // cross (2D cross product = scalar)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t cross(vector2_t other) => x * other.y - y * other.x;

    // len
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t len() => sqrtf(lenSq());

    // lenSq
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t lenSq() => x * x + y * y;

    // abs
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vector2_t abs() => new vector2_t(fabsf(x), fabsf(y));

    // Static min/max
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t minimize(vector2_t a, vector2_t b) =>
        new vector2_t(FixedPointMath.min(a.x, b.x), FixedPointMath.min(a.y, b.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t minimize(vector2_t a, vector2_t b, vector2_t c) =>
        new vector2_t(FixedPointMath.min(FixedPointMath.min(a.x, b.x), c.x),
                      FixedPointMath.min(FixedPointMath.min(a.y, b.y), c.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t maximize(vector2_t a, vector2_t b) =>
        new vector2_t(FixedPointMath.max(a.x, b.x), FixedPointMath.max(a.y, b.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vector2_t maximize(vector2_t a, vector2_t b, vector2_t c) =>
        new vector2_t(FixedPointMath.max(FixedPointMath.max(a.x, b.x), c.x),
                      FixedPointMath.max(FixedPointMath.max(a.y, b.y), c.y));

    // GetVec2 — C++ returns Vec3 with z=0 (note: original returns Vec3)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 GetVec2() => new Vec3(x.as_float(), y.as_float(), 0.0f);
}

// ============================================================================
// FixedAABB<int, 16> — aabb_t
// ============================================================================
public struct aabb_t
{
    public vector3_t min, max;

    public struct init_empty { }

    public aabb_t(init_empty _)
    {
        min = new vector3_t(real_t.max());
        max = new vector3_t(real_t.min());
    }

    public aabb_t(vector3_t _min, vector3_t _max) { min = _min; max = _max; }

    public bool empty() => min.x > max.x;

    public vector3_t size() => (max - min).abs();

    public vector3_t center() => (max + min) * real_t.fraction(1, 2);

    public bool overlaps(aabb_t other)
    {
        if (min.x > other.max.x || min.y > other.max.y
            || min.z > other.max.z || max.x < other.min.x
            || max.y < other.min.y || max.z < other.min.z)
            return false;
        return true;
    }

    // operator+= (vec)
    public void AddPoint(vector3_t other)
    {
        min = vector3_t.minimize(min, other);
        max = vector3_t.maximize(max, other);
    }

    // operator+= (aabb)
    public void AddAABB(aabb_t other)
    {
        min = vector3_t.minimize(min, other.min);
        max = vector3_t.maximize(max, other.max);
    }
}

// ============================================================================
// MNM.h types and functions
// ============================================================================

// Identifier types — thin wrappers around uint32 for type safety
// In the C++ these are just typedefs. We keep them as uint to match.
// TriangleID, TileID, OffMeshLinkID are all uint32.
// (The shell structs are replaced by direct uint usage for fidelity.)

// WayTriangleData
public struct WayTriangleData : IComparable<WayTriangleData>, IEquatable<WayTriangleData>
{
    public uint triangleID;       // TriangleID
    public uint offMeshLinkID;    // OffMeshLinkID
    public float costMultiplier;
    public uint incidentEdge;

    public WayTriangleData(uint _triangleID, uint _offMeshLinkID)
    {
        triangleID = _triangleID;
        offMeshLinkID = _offMeshLinkID;
        costMultiplier = 1.0f;
        incidentEdge = Constants.InvalidEdgeIndex;
    }

    // operator bool
    public bool IsValid => triangleID != 0;

    public static bool operator ==(WayTriangleData a, WayTriangleData b) =>
        a.triangleID == b.triangleID && a.offMeshLinkID == b.offMeshLinkID;

    public static bool operator !=(WayTriangleData a, WayTriangleData b) => !(a == b);

    public bool Equals(WayTriangleData other) => this == other;
    public override bool Equals(object obj) => obj is WayTriangleData w && this == w;
    public override int GetHashCode() => HashCode.Combine(triangleID, offMeshLinkID);

    public int CompareTo(WayTriangleData other)
    {
        if (triangleID != other.triangleID) return triangleID.CompareTo(other.triangleID);
        return offMeshLinkID.CompareTo(other.offMeshLinkID);
    }
}

// Helper functions
public static class MNMUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ComputeTriangleID(uint tileID, uint triangleIdx)
    {
        return (tileID << 10) | (triangleIdx & ((1u << 10) - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ComputeTileID(uint triangleID) => triangleID >> 10;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ComputeTriangleIndex(uint triangleID) =>
        (ushort)(triangleID & ((1u << 10) - 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTriangleAlreadyInWay(uint triangleID, uint[] way, int wayTriCount)
    {
        Debug.Assert(way != null);
        for (int i = 0; i < wayTriCount; i++)
            if (way[i] == triangleID) return true;
        return false;
    }

    // maximize / minimize
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool maximize<T>(ref T val, T x) where T : IComparable<T>
    {
        if (val.CompareTo(x) < 0) { val = x; return true; }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool minimize<T>(ref T val, T x) where T : IComparable<T>
    {
        if (val.CompareTo(x) > 0) { val = x; return true; }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void sort2<T>(ref T x, ref T y) where T : IComparable<T>
    {
        if (x.CompareTo(y) > 0) { T tmp = x; x = y; y = tmp; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void rsort2<T>(ref T x, ref T y) where T : IComparable<T>
    {
        if (x.CompareTo(y) < 0) { T tmp = x; x = y; y = tmp; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t clamp(real_t x, real_t min, real_t max)
    {
        if (x > max) return max;
        if (x < min) return min;
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t clampunit(real_t x)
    {
        if (x > new real_t(1)) return new real_t(1);
        if (x < new real_t(0)) return new real_t(0);
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int next_mod3(int x) => (x + 1) % 3;

    // dec_mod3 — C++ uses a lookup table: static const unsigned int dec_mod3[] = { 2, 0, 1 };
    public static readonly int[] dec_mod3 = { 2, 0, 1 };

    // ProjectionPointLineSeg — works for vector3_t and vector2_t
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t ProjectionPointLineSeg3(vector3_t p, vector3_t a0, vector3_t a1)
    {
        vector3_t a = a1 - a0;
        real_t lenSq = a.dot(a);
        if (lenSq > new real_t(0))
        {
            vector3_t ap = p - a0;
            real_t dotVal = a.dot(ap);
            return dotVal / lenSq;
        }
        return new real_t(0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t ProjectionPointLineSeg2(vector2_t p, vector2_t a0, vector2_t a1)
    {
        vector2_t a = a1 - a0;
        real_t lenSq = a.dot(a);
        if (lenSq > new real_t(0))
        {
            vector2_t ap = p - a0;
            real_t dotVal = a.dot(ap);
            return dotVal / lenSq;
        }
        return new real_t(0);
    }

    // DistPointLineSegSq
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t DistPointLineSegSq3(vector3_t p, vector3_t a0, vector3_t a1)
    {
        vector3_t a = a1 - a0;
        vector3_t ap = p - a0;
        vector3_t bp = p - a1;

        real_t e = ap.dot(a);
        if (e <= new real_t(0)) return ap.dot(ap);

        real_t f = a.dot(a);
        if (e >= f) return bp.dot(bp);

        return ap.dot(ap) - ((e * e) / f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t DistPointLineSegSq2(vector2_t p, vector2_t a0, vector2_t a1)
    {
        vector2_t a = a1 - a0;
        vector2_t ap = p - a0;
        vector2_t bp = p - a1;

        real_t e = ap.dot(a);
        if (e <= new real_t(0)) return ap.dot(ap);

        real_t f = a.dot(a);
        if (e >= f) return bp.dot(bp);

        return ap.dot(ap) - ((e * e) / f);
    }

    // ClosestPtPointTriangle
    public static vector3_t ClosestPtPointTriangle(vector3_t p, vector3_t a, vector3_t b, vector3_t c)
    {
        vector3_t ab = b - a;
        vector3_t ac = c - a;
        vector3_t ap = p - a;

        real_t d1 = ab.dot(ap);
        real_t d2 = ac.dot(ap);

        if (d1 <= new real_t(0) && d2 <= new real_t(0)) return a;

        vector3_t bp = p - b;
        real_t d3 = ab.dot(bp);
        real_t d4 = ac.dot(bp);

        if (d3 >= new real_t(0) && d4 <= d3) return b;

        real_t vc = d1 * d4 - d3 * d2;
        if (vc <= new real_t(0) && d1 >= new real_t(0) && d3 <= new real_t(0))
        {
            real_t v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        vector3_t cp = p - c;
        real_t d5 = ab.dot(cp);
        real_t d6 = ac.dot(cp);

        if (d6 >= new real_t(0) && d5 <= d6) return c;

        real_t vb = d5 * d2 - d1 * d6;
        if (vb <= new real_t(0) && d2 >= new real_t(0) && d6 <= new real_t(0))
        {
            real_t w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        real_t va = d3 * d6 - d5 * d4;
        if (va <= new real_t(0) && (d4 - d3) >= new real_t(0) && (d5 - d6) >= new real_t(0))
        {
            real_t w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        // do not factorize the divisions : Fixed point precision requires it
        real_t denom = va + vb + vc;
        real_t vf = vb / denom;
        real_t wf = vc / denom;

        return a + ab * vf + ac * wf;
    }

    // PointInTriangle (2D)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointInTriangle(vector2_t p, vector2_t a, vector2_t b, vector2_t c)
    {
        bool e0 = (p - a).cross(a - b) >= new real_t(0);
        bool e1 = (p - b).cross(b - c) >= new real_t(0);
        bool e2 = (p - c).cross(c - a) >= new real_t(0);

        return (e0 == e1) && (e0 == e2);
    }

    // ClosestPtSegmentSegment
    public static real_t ClosestPtSegmentSegment3(vector3_t a0, vector3_t a1, vector3_t b0, vector3_t b1,
        ref real_t s, ref real_t t, ref vector3_t closesta, ref vector3_t closestb)
    {
        vector3_t da = a1 - a0;
        vector3_t db = b1 - b0;
        vector3_t r = a0 - b0;
        real_t a = da.dot(da); // lenSq of da
        real_t e = db.dot(db); // lenSq of db  (called 'e' in C++ to match variable naming)
        real_t f = db.dot(r);

        if (a == new real_t(0) && e == new real_t(0))
        {
            s = t = new real_t(0);
            closesta = a0;
            closestb = b0;
            return (closesta - closestb).lenSq();
        }

        if (a == new real_t(0))
        {
            s = new real_t(0);
            t = f / e;
            t = clampunit(t);
        }
        else
        {
            real_t c = da.dot(r);

            if (e == new real_t(0))
            {
                t = new real_t(0);
                s = clampunit(-c / a);
            }
            else
            {
                real_t bv = da.dot(db);
                real_t denom = (a * e) - (bv * bv);

                if (denom != new real_t(0))
                    s = clampunit(((bv * f) - (c * e)) / denom);
                else
                    s = new real_t(0);

                real_t tnom = (bv * s) + f;

                if (tnom < new real_t(0))
                {
                    t = new real_t(0);
                    s = clampunit(-c / a);
                }
                else if (tnom > e)
                {
                    t = new real_t(1);
                    s = clampunit((bv - c) / a);
                }
                else
                    t = tnom / e;
            }
        }

        closesta = a0 + da * s;
        closestb = b0 + db * t;

        return (closesta - closestb).lenSq();
    }

    // ClosestPtPointSegment — returns vector2_t
    public static vector2_t ClosestPtPointSegment2(vector2_t p, vector2_t a0, vector2_t a1, ref real_t t)
    {
        vector2_t a = a1 - a0;
        t = (p - a0).dot(a);

        if (t <= new real_t(0))
        {
            t = new real_t(0);
            return a0;
        }
        else
        {
            real_t denom = a.lenSq();

            if (t >= denom)
            {
                t = new real_t(1);
                return a1;
            }
            else
            {
                t = t / denom;
                return a0 + (a * t);
            }
        }
    }

    // Intersection between two segments in 2D
    public enum EIntersectionResult
    {
        eIR_NoIntersection,
        eIR_Intersection,
        eIR_ParallelOrCollinearSegments,
    }

    public static EIntersectionResult DetailedIntersectSegmentSegment2(vector2_t a0, vector2_t a1, vector2_t b0, vector2_t b1,
        ref real_t s, ref real_t t)
    {
        vector2_t e = b0 - a0;
        vector2_t da = a1 - a0;
        vector2_t db = b1 - b0;
        real_t det = da.x * db.y - da.y * db.x;
        real_t tolerance = real_t.epsilon();
        real_t signAdjustment = det >= new real_t(0) ? new real_t(1.0f) : new real_t(-1.0f);
        real_t minAllowedValue = new real_t(0.0f) - tolerance;
        real_t maxAllowedValue = (new real_t(1.0f) * det * signAdjustment) + tolerance;
        if (det != new real_t(0))
        {
            s = (e.x * db.y - e.y * db.x) * signAdjustment;

            if (s < minAllowedValue || s > maxAllowedValue)
                return EIntersectionResult.eIR_NoIntersection;

            t = (e.x * da.y - e.y * da.x) * signAdjustment;
            if (t < minAllowedValue || t > maxAllowedValue)
                return EIntersectionResult.eIR_NoIntersection;

            s = clampunit(s / det * signAdjustment);
            t = clampunit(t / det * signAdjustment);
            return EIntersectionResult.eIR_Intersection;
        }

        return EIntersectionResult.eIR_ParallelOrCollinearSegments;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IntersectSegmentSegment2(vector2_t a0, vector2_t a1, vector2_t b0, vector2_t b1,
        ref real_t s, ref real_t t)
    {
        return DetailedIntersectSegmentSegment2(a0, a1, b0, b1, ref s, ref t) == EIntersectionResult.eIR_Intersection;
    }

    // 3D overload — C++ template uses only .x and .y (2D cross product) even for vector3_t.
    // Literal port: project vector3_t to vector2_t and delegate to the 2D version.
    public static EIntersectionResult DetailedIntersectSegmentSegment(vector3_t a0, vector3_t a1, vector3_t b0, vector3_t b1,
        ref real_t s, ref real_t t)
    {
        return DetailedIntersectSegmentSegment2(new vector2_t(a0), new vector2_t(a1), new vector2_t(b0), new vector2_t(b1), ref s, ref t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IntersectSegmentSegment(vector3_t a0, vector3_t a1, vector3_t b0, vector3_t b1,
        ref real_t s, ref real_t t)
    {
        return DetailedIntersectSegmentSegment(a0, a1, b0, b1, ref s, ref t) == EIntersectionResult.eIR_Intersection;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BreakOnInvalidTriangle(uint triangleID, uint tileCapacity) { /* no-op in release */ }

    // MNMUtils functions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t CalculateMinHorizontalRange(ushort radiusVoxelUnits, float voxelHSize)
    {
        Debug.Assert(voxelHSize > 0.0f);
        return new real_t(Math.Max(radiusVoxelUnits * voxelHSize * 2.0f, 1.0f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t CalculateMinVerticalRange(ushort agentHeightVoxelUnits, float voxelVSize)
    {
        Debug.Assert(voxelVSize > 0.0f);
        return new real_t(Math.Max(agentHeightVoxelUnits * voxelVSize, 1.0f));
    }

    // NextPowerOfTwo helper (used by AStarOpenList)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextPowerOfTwo(int n)
    {
        n = n - 1;
        n = n | (n >> 1);
        n = n | (n >> 2);
        n = n | (n >> 4);
        n = n | (n >> 8);
        n = n | (n >> 16);
        n = n + 1;
        return n;
    }
}

// ============================================================================
// AStarContention
// ============================================================================
public class AStarContention
{
    public void SetFrameTimeQuota(float frameTimeQuota)
    {
        m_frameTimeQuota.SetSeconds(frameTimeQuota);
    }

    public struct ContentionStats
    {
        public float frameTimeQuota;
        public uint averageSearchSteps;
        public uint peakSearchSteps;
        public float averageSearchTime;
        public float peakSearchTime;
    }

    public ContentionStats GetContentionStats()
    {
        ContentionStats stats;
        stats.frameTimeQuota = m_frameTimeQuota.GetMilliSeconds();
        stats.averageSearchSteps = m_totalSearchCount > 0 ? m_totalSearchSteps / m_totalSearchCount : 0;
        stats.peakSearchSteps = m_peakSearchSteps;
        stats.averageSearchTime = m_totalSearchCount > 0 ? m_totalComputationTime / (float)m_totalSearchCount : 0.0f;
        stats.peakSearchTime = m_peakSearchTime;
        return stats;
    }

    public void ResetConsumedTimeDuringCurrentFrame()
    {
        m_consumedFrameTime.SetValue(0);
    }

    protected AStarContention(float frameTimeQuota = 0.001f)
    {
        m_frameTimeQuota.SetSeconds(frameTimeQuota);
        ResetContentionStats();
    }

    protected void StartSearch()
    {
        m_currentSearchSteps = 0;
        m_currentSearchTime.SetValue(0);
    }

    protected void EndSearch()
    {
        m_totalSearchCount++;
        m_totalSearchSteps += m_currentSearchSteps;
        m_peakSearchSteps = Math.Max(m_peakSearchSteps, m_currentSearchSteps);

        float lastSearchTime = m_currentSearchTime.GetMilliSeconds();
        m_totalComputationTime += lastSearchTime;
        m_peakSearchTime = Math.Max(m_peakSearchTime, lastSearchTime);
    }

    protected void StartStep()
    {
        m_currentStepStartTime = gEnv.pTimer.GetAsyncTime();
        m_currentSearchSteps++;
    }

    protected void EndStep()
    {
        CryAISystem.CryCommon.CTimeValue stepTime = gEnv.pTimer.GetAsyncTime() - m_currentStepStartTime;
        m_consumedFrameTime = m_consumedFrameTime + stepTime;
        m_currentSearchTime = m_currentSearchTime + stepTime;
    }

    protected bool FrameQuotaReached()
    {
        return (m_frameTimeQuota > new CryAISystem.CryCommon.CTimeValue()) ? (m_consumedFrameTime >= m_frameTimeQuota) : false;
    }

    protected void ResetContentionStats()
    {
        m_consumedFrameTime.SetValue(0);
        m_currentStepStartTime.SetValue(0);
        m_totalSearchCount = 0;
        m_currentSearchSteps = 0;
        m_totalSearchSteps = 0;
        m_peakSearchSteps = 0;
        m_totalComputationTime = 0.0f;
        m_currentSearchTime.SetValue(0);
        m_peakSearchTime = 0.0f;
    }

    protected CryAISystem.CryCommon.CTimeValue m_frameTimeQuota;
    protected CryAISystem.CryCommon.CTimeValue m_consumedFrameTime;
    protected CryAISystem.CryCommon.CTimeValue m_currentStepStartTime;
    protected CryAISystem.CryCommon.CTimeValue m_currentSearchTime;

    protected uint m_totalSearchCount;
    protected uint m_currentSearchSteps;
    protected uint m_totalSearchSteps;
    protected uint m_peakSearchSteps;

    protected float m_totalComputationTime;
    protected float m_peakSearchTime;
}

// ============================================================================
// AStarOpenList
// ============================================================================
public class AStarOpenList : AStarContention
{
    public class Node
    {
        public WayTriangleData prevTriangle;
        public vector3_t location;
        public real_t cost;
        public real_t estimatedTotalCost;
        public bool open;

        public Node()
        {
            prevTriangle = new WayTriangleData(0, 0);
            open = false;
        }

        public Node(uint prevTriangleID, uint prevOffMeshLinkID, vector3_t _location, real_t _cost,
            bool _open = false)
        {
            prevTriangle = new WayTriangleData(prevTriangleID, prevOffMeshLinkID);
            location = _location;
            cost = _cost;
            estimatedTotalCost = real_t.max();
            open = _open;
        }

        public Node(uint prevTriangleID, uint prevOffMeshLinkID, vector3_t _location, real_t _cost,
            real_t _estimatedTotalCost, bool _open = false)
        {
            prevTriangle = new WayTriangleData(prevTriangleID, prevOffMeshLinkID);
            location = _location;
            cost = _cost;
            estimatedTotalCost = _estimatedTotalCost;
            open = _open;
        }
    }

    public struct OpenNodeListElement
    {
        public WayTriangleData triData;
        public Node pNode;
        public real_t fCost;

        public OpenNodeListElement(WayTriangleData _triData, Node _pNode, real_t _fCost)
        {
            triData = _triData;
            pNode = _pNode;
            fCost = _fCost;
        }
    }

    // Using SortedDictionary for ordered map; C++ uses std::map<WayTriangleData, Node>
    private List<OpenNodeListElement> m_openList = new();
    private SortedDictionary<WayTriangleData, Node> m_nodeLookUp = new();

    public AStarOpenList() : base() { }

    public void SetUpForPathSolving(uint triangleCount, uint fromTriangleID, vector3_t startLocation, real_t dist_start_to_end)
    {
        StartSearch();

        int estimatedNodeCount = (int)triangleCount + 64;
        int minOpenListSize = MNMUtils.NextPowerOfTwo(estimatedNodeCount);

        m_openList.Clear();
        m_openList.Capacity = minOpenListSize;
        m_nodeLookUp.Clear();

        var key = new WayTriangleData(fromTriangleID, 0);
        var node = new Node(fromTriangleID, 0, startLocation, new real_t(0), dist_start_to_end, true);
        m_nodeLookUp[key] = node;
        m_openList.Add(new OpenNodeListElement(key, node, dist_start_to_end));
    }

    public void PathSolvingDone()
    {
        EndSearch();
    }

    public OpenNodeListElement PopBestNode()
    {
        StartStep();

        // Find min element
        int minIdx = 0;
        for (int i = 1; i < m_openList.Count; i++)
        {
            if (m_openList[i].fCost < m_openList[minIdx].fCost)
                minIdx = i;
        }

        OpenNodeListElement element = m_openList[minIdx];
        // Swap with last and remove last
        m_openList[minIdx] = m_openList[m_openList.Count - 1];
        m_openList.RemoveAt(m_openList.Count - 1);

        return element;
    }

    public bool InsertNode(WayTriangleData triangle, out Node pNextNode)
    {
        if (m_nodeLookUp.TryGetValue(triangle, out pNextNode))
        {
            return false; // already existed
        }
        pNextNode = new Node();
        m_nodeLookUp[triangle] = pNextNode;
        return true;
    }

    public Node FindNode(WayTriangleData triangle)
    {
        m_nodeLookUp.TryGetValue(triangle, out Node node);
        return node;
    }

    public void AddToOpenList(WayTriangleData triangle, Node pNode, real_t cost)
    {
        Debug.Assert(pNode != null);
        m_openList.Add(new OpenNodeListElement(triangle, pNode, cost));
    }

    public bool CanDoStep()
    {
        return m_openList.Count > 0 && !FrameQuotaReached();
    }

    public void StepDone()
    {
        EndStep();
    }

    public bool Empty() => m_openList.Count == 0;

    public void Reset()
    {
        ResetContentionStats();
        m_openList.Clear();
        m_openList.TrimExcess();
        m_nodeLookUp.Clear();
    }

    public bool TileWasVisited(uint tileID)
    {
        foreach (var kvp in m_nodeLookUp)
        {
            if (MNMUtils.ComputeTileID(kvp.Key.triangleID) == tileID)
                return true;
        }
        return false;
    }
}

// ============================================================================
// HashComputer — MNM::HashComputer
// ============================================================================
public class HashComputer
{
    private uint hash;
    private uint len;

    public HashComputer(uint seed = 0) { hash = seed; len = 0; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint x, int k) => (x << k) | (x >> (32 - k));

    public void Add(float key)
    {
        // union f32_u { float fv; uint32 uv; }
        uint uv = BitConverter.SingleToUInt32Bits(key);
        Add(uv);
    }

    public void Add(uint key)
    {
        key *= 0xcc9e2d51;
        key = RotateLeft(key, 15);
        key *= 0x1b873593;

        hash ^= key;
        hash = RotateLeft(hash, 13);
        hash = hash * 5 + 0xe6546b64;

        len += 4;
    }

    public void Complete()
    {
        hash ^= len;
        hash ^= hash >> 16;
        hash *= 0x85ebca6b;
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35;
        hash ^= hash >> 16;
    }

    public void Add(Vec3 key) { Add(key.x); Add(key.y); Add(key.z); }

    public uint GetValue() => hash;
}

// ============================================================================
// OpenList<ElementNode, BestNodePredicate> — generic open list
// ============================================================================
public class OpenList<TElement> where TElement : IComparable<TElement>
{
    private List<TElement> openElements;

    public OpenList(int maxExpectedSize)
    {
        openElements = new List<TElement>(maxExpectedSize);
    }

    public void SetupOpenList(int maxExpectedSize)
    {
        openElements.Capacity = maxExpectedSize;
    }

    public TElement PopBestElement()
    {
        Debug.Assert(openElements.Count > 0, "PopBestElement has been requested for an empty ElementNode open list.");
        int bestIdx = 0;
        for (int i = 1; i < openElements.Count; i++)
        {
            if (openElements[i].CompareTo(openElements[bestIdx]) < 0)
                bestIdx = i;
        }
        TElement best = openElements[bestIdx];
        openElements[bestIdx] = openElements[openElements.Count - 1];
        openElements.RemoveAt(openElements.Count - 1);
        return best;
    }

    public void Reset() { openElements.Clear(); }
    public bool IsEmpty() => openElements.Count == 0;

    public void InsertElement(TElement newElement)
    {
        // push_back_unique
        if (!openElements.Contains(newElement))
            openElements.Add(newElement);
    }
}

// ============================================================================
// Profiler — MNM::Profiler<MemoryUsers, TimerNames, StatNames>
// ============================================================================
public class Profiler<TMemoryUsers, TTimerNames, TStatNames>
    where TMemoryUsers : Enum
    where TTimerNames : Enum
    where TStatNames : Enum
{
    public const int MaxTimers = 32;
    public const int MaxUsers = 32;
    public const int MaxStats = 32;

    public struct TimerInfo
    {
        public CryAISystem.CryCommon.CTimeValue elapsed;
    }

    public struct MemoryInfo
    {
        public int used; // size_t -> int for C# simplicity (values are small)
        public int peak;
    }

    private CryAISystem.CryCommon.CTimeValue elapsed;
    private int memoryUsed;
    private int memoryPeak;

    private TimerInfo[] timers = new TimerInfo[MaxTimers];
    private MemoryInfo[] memoryUsage = new MemoryInfo[MaxUsers];
    private int[] stats = new int[MaxStats];
    private CryAISystem.CryCommon.CTimeValue[] runningTimer = new CryAISystem.CryCommon.CTimeValue[MaxTimers];

    public Profiler()
    {
        elapsed = new CryAISystem.CryCommon.CTimeValue(0L);
        memoryUsed = 0;
        memoryPeak = 0;
    }

    public void AddMemory(TMemoryUsers user, int amount)
    {
        int idx = Convert.ToInt32(user);
        memoryUsed += amount;
        if (memoryUsed > memoryPeak) memoryPeak = memoryUsed;
        memoryUsage[idx].used += amount;
        if (memoryUsage[idx].used > memoryUsage[idx].peak) memoryUsage[idx].peak = memoryUsage[idx].used;
    }

    public void FreeMemory(TMemoryUsers user, int amount)
    {
        int idx = Convert.ToInt32(user);
        memoryUsed -= amount;
        memoryUsage[idx].used -= amount;
    }

    public void StartTimer(TTimerNames timer)
    {
        int idx = Convert.ToInt32(timer);
        runningTimer[idx] = gEnv.pTimer.GetAsyncTime();
    }

    public void StopTimer(TTimerNames timer)
    {
        int idx = Convert.ToInt32(timer);
        CryAISystem.CryCommon.CTimeValue end = gEnv.pTimer.GetAsyncTime();
        Debug.Assert(runningTimer[idx].GetValue() != 0);
        CryAISystem.CryCommon.CTimeValue timerElapsed = end - runningTimer[idx];
        timers[idx].elapsed = timers[idx].elapsed + timerElapsed;
        elapsed = elapsed + timerElapsed;
    }

    public void AddTime(TTimerNames timer, CryAISystem.CryCommon.CTimeValue amount)
    {
        int idx = Convert.ToInt32(timer);
        elapsed = elapsed + amount;
        timers[idx].elapsed = timers[idx].elapsed + amount;
    }

    public void AddStat(TStatNames stat, int amount)
    {
        int idx = Convert.ToInt32(stat);
        stats[idx] += amount;
    }

    public MemoryInfo GetMemoryInfo(TMemoryUsers user) => memoryUsage[Convert.ToInt32(user)];
    public TimerInfo GetTimerInfo(TTimerNames timer) => timers[Convert.ToInt32(timer)];
    public int GetStat(TStatNames stat) => stats[Convert.ToInt32(stat)];
    public CryAISystem.CryCommon.CTimeValue GetTotalElapsed() => elapsed;
    public int GetMemoryUsage() => memoryUsed;
    public int GetMemoryPeak() => memoryPeak;
}

// ============================================================================
// Identifier wrapper types — C++ typedefs these as uint32 but existing C# code
// uses struct wrappers with an .id field. Keep for backward compat.
// ============================================================================
public struct TriangleID
{
    public uint id;
    public TriangleID(uint v) { id = v; }
    public static implicit operator uint(TriangleID t) => t.id;
    public static implicit operator TriangleID(uint v) => new TriangleID(v);
}

public struct TileID
{
    public uint id;
    public TileID(uint v) { id = v; }
    public static implicit operator uint(TileID t) => t.id;
    public static implicit operator TileID(uint v) => new TileID(v);
}

public struct OffMeshLinkID
{
    public uint id;
    public OffMeshLinkID(uint v) { id = v; }
    public static implicit operator uint(OffMeshLinkID t) => t.id;
    public static implicit operator OffMeshLinkID(uint v) => new OffMeshLinkID(v);
    public static bool operator ==(OffMeshLinkID a, OffMeshLinkID b) => a.id == b.id;
    public static bool operator !=(OffMeshLinkID a, OffMeshLinkID b) => a.id != b.id;
    public override bool Equals(object obj) => obj is OffMeshLinkID o && o.id == id;
    public override int GetHashCode() => (int)id;
}

// OffMeshLinkPtr — DECLARE_BOOST_POINTERS(OffMeshLink) -> shared_ptr
// In C# this is just the reference. We use a simple wrapper for type compat.
public class OffMeshLinkPtr
{
    private OffMeshLink _link;
    public OffMeshLinkPtr() { }
    public OffMeshLinkPtr(OffMeshLink link) { _link = link; }
    public void reset(OffMeshLink link) { _link = link; }
    public OffMeshLink get() => _link;
    public static implicit operator OffMeshLink(OffMeshLinkPtr p) => p?._link;
}

// IslandConnections.TIslandsWay — used externally
public static class IslandConnectionsTypes
{
    // typedef std::list<MNM::GlobalIslandID> TIslandsWay
    // Using LinkedList in C# — provide an alias for existing code
}

// ============================================================================
// Off-mesh link request types (used by OffMeshNavigationManager)
// ============================================================================
public enum EOffMeshOperationType
{
    eOffMeshOperationType_Add,
    eOffMeshOperationType_Remove,
}

public class OffMeshOperationRequestBase
{
    public EOffMeshOperationType operationType;
    public uint objectId;
    // Alias used by OffMeshNavigationManager
    public uint requestOwnerId { get => objectId; set => objectId = value; }
}

public class LinkAdditionRequest : OffMeshOperationRequestBase
{
    public NavigationMeshID meshId;
    public OffMeshLink pLinkData;
    public uint linkId;
    public bool dataExists;
    public bool trimExcess;
    public Action<uint> callback;

    public LinkAdditionRequest(uint entityId, NavigationMeshID meshID, OffMeshLink linkData, uint existingLinkID)
    {
        operationType = EOffMeshOperationType.eOffMeshOperationType_Add;
        objectId = entityId;
        meshId = meshID;
        pLinkData = linkData;
        linkId = existingLinkID;
        dataExists = existingLinkID != 0;
        trimExcess = false;
        callback = null;
    }

    public LinkAdditionRequest(uint entityId, NavigationMeshID meshID, OffMeshLink linkData)
    {
        operationType = EOffMeshOperationType.eOffMeshOperationType_Add;
        objectId = entityId;
        meshId = meshID;
        pLinkData = linkData;
        linkId = 0;
        dataExists = false;
        trimExcess = false;
        callback = null;
    }
}

public class LinkRemovalRequest : OffMeshOperationRequestBase
{
    public uint linkId;

    public LinkRemovalRequest(uint objectId, uint linkID)
    {
        operationType = EOffMeshOperationType.eOffMeshOperationType_Remove;
        this.objectId = objectId;
        linkId = linkID;
    }
}

// MeshGrid — full literal port in MeshGrid.cs
// TileGenerator — full literal port in TileGenerator.cs
// Voxelizer — full literal port in Voxelizer.cs

// DangerousAreasList — full definition in MeshGrid.cs
