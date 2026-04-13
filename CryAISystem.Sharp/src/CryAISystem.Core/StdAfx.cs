// Literal port of dev/Code/CryEngine/CryAISystem/StdAfx.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// stdafx.h : include file for standard system include files,
//  or project specific include files that are used frequently, but
//      are changed infrequently
//
// Most of the original StdAfx.h is just `#include` directives that have no C# equivalent.
// The non-trivial content is the inline helper functions and the GetAISystem() global accessor,
// which are ported here.

namespace CryAISystem;

// cry_random helpers — port of CryCommon/CryRandom.h
public static class CryRandom
{
    [System.ThreadStatic] private static System.Random _rng;
    private static System.Random Rng => _rng ??= new System.Random();

    /// cry_random(min, max) for float — uniform in [min, max]
    public static float cry_random(float min, float max)
    {
        return min + (float)Rng.NextDouble() * (max - min);
    }

    /// cry_random(min, max) for int — uniform in [min, max] inclusive
    public static int cry_random(int min, int max)
    {
        return Rng.Next(min, max + 1);
    }

    /// cry_random(min, max) for nuint — uniform in [min, max] inclusive
    public static nuint cry_random(nuint min, nuint max)
    {
        return (nuint)Rng.Next((int)min, (int)max + 1);
    }
}

// PNoise3 — shell port of CryCommon/PNoise3.h
public static class PNoise3
{
    /// Noise1D returns deterministic procedural noise in [-1, 1]. Shell returns 0 for now.
    public static float Noise1D(float x) { return 0.0f; }
}

public static partial class CryMath
{
    public const float gf_PI = 3.14159265358979323846f;
    public const float gf_PI2 = gf_PI * 2.0f;
    public static float sqr(float x) { return x * x; }
    public static float DEG2RAD(float x) { return x * (gf_PI / 180.0f); }
    public static float RAD2DEG(float x) { return x * (180.0f / gf_PI); }
    public static float clamp_tpl(float val, float min, float max) { return val < min ? min : (val > max ? max : val); }
    public static int clamp_tpl(int val, int min, int max) { return val < min ? min : (val > max ? max : val); }
    public static void Limit(ref float val, float min, float max) { val = clamp_tpl(val, min, max); }
    public static void Limit(ref int val, int min, int max) { val = clamp_tpl(val, min, max); }
    public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    public static Vec3 Lerp(Vec3 a, Vec3 b, float t) { return a + (b - a) * t; }
    public static float floor_tpl(float x) { return System.MathF.Floor(x); }
    public static float IsEquivalent(Vec3 a, Vec3 b, float epsilon) { return ((a - b).GetLengthSquared() < epsilon * epsilon) ? 1.0f : 0.0f; }
    public static bool IsEquivalent_b(Vec3 a, Vec3 b, float epsilon) { return (a - b).GetLengthSquared() < epsilon * epsilon; }
    public static float fabsf(float x) { return System.MathF.Abs(x); }
    public static float cosf(float x) { return System.MathF.Cos(x); }
    public static float sinf(float x) { return System.MathF.Sin(x); }
    public static float tanf(float x) { return System.MathF.Tan(x); }
    public static float atan2f(float y, float x) { return System.MathF.Atan2(y, x); }
    public static float fmodf(float a, float b) { return a % b; }
    public static float sqrtf(float x) { return System.MathF.Sqrt(x); }
    public static float cos_tpl(float x) { return System.MathF.Cos(x); }
    public static float sin_tpl(float x) { return System.MathF.Sin(x); }
    public static float atan2_tpl(float y, float x) { return System.MathF.Atan2(y, x); }
    public static float fabs(float x) { return System.MathF.Abs(x); }
    public static float fabs_tpl(float x) { return System.MathF.Abs(x); }
    public static float sqrt_tpl(float x) { return System.MathF.Sqrt(x); }
    public static float acosf(float x) { return System.MathF.Acos(x); }
    public static float min(float a, float b) { return a < b ? a : b; }
    public static float max(float a, float b) { return a > b ? a : b; }
    public static int min(int a, int b) { return a < b ? a : b; }
    public static int max(int a, int b) { return a > b ? a : b; }
    public static nuint max(nuint a, nuint b) { return a > b ? a : b; }
    public static float square(float x) { return x * x; }
    public static float floorf(float x) { return System.MathF.Floor(x); }
    public static float ceilf(float x) { return System.MathF.Ceiling(x); }
    public const float FLT_MAX = float.MaxValue;
    public static void sincos_tpl(float angle, out float sinVal, out float cosVal) { sinVal = System.MathF.Sin(angle); cosVal = System.MathF.Cos(angle); }
    public static int sgn(int x) { return (x > 0) ? 1 : ((x < 0) ? -1 : 0); }
    public static float fsel(float a, float b, float c) { return a >= 0.0f ? b : c; }
}

public static class Ang3
{
    /// C++ Ang3::CreateRadZ — angle in radians between two directions projected on XY plane
    public static float CreateRadZ(Vec3 v0, Vec3 v1)
    {
        float cz = v0.x * v1.y - v0.y * v1.x;
        float c = v0.x * v1.x + v0.y * v1.y;
        return System.MathF.Atan2(cz, c);
    }
}

// AISIGNAL signal IDs — from IAgent.h
public static class AISignalConstants
{
    public const int AISIGNAL_INCLUDE_DISABLED = 0;
    public const int AISIGNAL_DEFAULT = 1;
    public const int AISIGNAL_PROCESS_NEXT_UPDATE = 3;
    public const int AISIGNAL_NOTIFY_ONLY = 9;
    public const int AISIGNAL_ALLOW_DUPLICATES = 10;
    public const int AISIGNAL_RECEIVED_PREV_UPDATE = 11;

    // AISPEED values
    public const float AISPEED_ZERO = 0.0f;
    public const float AISPEED_SLOW = 0.21f;
    public const float AISPEED_WALK = 0.4f;
    public const float AISPEED_RUN = 1.0f;
    public const float AISPEED_SPRINT = 1.4f;
}

// COVER_OBJECT_TYPES bitmask — used by GetPhysicalSkipEntities
public static class AIPhysConstants
{
    public const int COVER_OBJECT_TYPES = (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4); // simclass bitmask
    // HIT_COVER — CAISystem.h line 77: physics raycast flags for cover queries
    public const int HIT_COVER = 0x0101; // geom_colltype_ray << rwi_colltype_bit | rwi_colltype_any | pierceability
}

// Forward decl: WalkabilityFloorUpDist/DownDist/DownRadius — used by GetFloorPosition
public static class WalkabilityConstants
{
    public const float WalkabilityFloorUpDist = 1.5f;
    public const float WalkabilityFloorDownDist = 5.0f;
    public const float WalkabilityDownRadius = 0.25f;
}

// CCCPOINT — CodeCoverageCheckPoint macro, no-op in C# (debug instrumentation)
public static class CCCPOINT_HELPER
{
    public static void CCCPOINT(object label) { /* no-op — code coverage instrumentation */ }
}

// AISIGNAL struct — IAgent.h lines 1319-1343
public struct AISIGNAL
{
    public const int SIGNAL_NAME_LENGTH = 50;
    public int nSignal;
    public uint m_nCrcText;
    public uint senderID; // EntityId
    public IAISignalExtraData pEData;
    public string strText;

    public bool Compare(uint crc) { return m_nCrcText == crc; }

    public void Serialize(TSerialize ser)
    {
        ser.Value("nSignal", ref nSignal);
        ser.Value("m_nCrcText", ref m_nCrcText);
        ser.Value("senderID", ref senderID);
        ser.Value("strText", ref strText);
    }
}

// NILREF — C++ global constant for null weak references
public static class NilRefHelper
{
    public static CWeakRef<CAIObject> NILREF = new CWeakRef<CAIObject>();
}

public static class StdAfx
{
    /// This frees the memory allocation for a vector (or similar), rather than just erasing the contents
    public static void ClearVectorMemory<T>(System.Collections.Generic.List<T> container)
    {
        container.Clear();
        container.Capacity = 0;
    }

    //====================================================================
    // SetAABBCornerPoints
    //====================================================================
    public static void SetAABBCornerPoints(AABB b, Vec3[] pts)
    {
        pts[0] = new Vec3(b.min.x, b.min.y, b.min.z);
        pts[1] = new Vec3(b.max.x, b.min.y, b.min.z);
        pts[2] = new Vec3(b.max.x, b.max.y, b.min.z);
        pts[3] = new Vec3(b.min.x, b.max.y, b.min.z);

        pts[4] = new Vec3(b.min.x, b.min.y, b.max.z);
        pts[5] = new Vec3(b.max.x, b.min.y, b.max.z);
        pts[6] = new Vec3(b.max.x, b.max.y, b.max.z);
        pts[7] = new Vec3(b.min.x, b.max.y, b.max.z);
    }


    public static float LinStep(float a, float b, float x)
    {
        float w = (b - a);
        if (w != 0.0f)
        {
            x = (x - a) / w;
            return System.Math.Min(1.0f, System.Math.Max(x, 0.0f));
        }
        return 0.0f;
    }

    //===================================================================
    // HasPointInRange
    // (To be replaced)
    //===================================================================
    public static bool HasPointInRange(System.Collections.Generic.List<Vec3> list, Vec3 pos, float range)
    {
        float r = range * range;
        for (uint i = 0; i < (uint)list.Count; ++i)
        {
            float dx = list[(int)i].x - pos.x;
            float dy = list[(int)i].y - pos.y;
            float dz = list[(int)i].z - pos.z;
            if ((dx * dx + dy * dy + dz * dz) < r)
                return true;
        }
        return false;
    }
}
