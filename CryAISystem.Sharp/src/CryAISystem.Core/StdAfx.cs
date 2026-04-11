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
