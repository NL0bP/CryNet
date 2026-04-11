// Literal port of dev/Code/CryEngine/CryCommon/Cry_Geo.h (subset — AABB, OBB, Lineseg, Matrix34).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.CryCommon;

public struct AABB
{
    public Vec3 min;
    public Vec3 max;

    public enum type_reset { RESET_VAL }
    /// Sentinel mirroring the C++ `AABB::RESET` static const flag used as `AABB(AABB::RESET)`.
    public static readonly type_reset RESET = type_reset.RESET_VAL;

    public AABB(type_reset r) { min = default; max = default; Reset(); }
    public AABB(float radius) { max = new Vec3(radius, radius, radius); min = new Vec3(-radius, -radius, -radius); }
    public AABB(Vec3 v) { min = v; max = v; }
    public AABB(Vec3 v, float radius) { Vec3 ext = new Vec3(radius, radius, radius); min = v - ext; max = v + ext; }
    public AABB(Vec3 vmin, Vec3 vmax) { min = vmin; max = vmax; }

    //! Reset Bounding box before calculating bounds.
    public void Reset() { min = new Vec3(1e15f, 1e15f, 1e15f); max = new Vec3(-1e15f, -1e15f, -1e15f); }

    public bool IsReset() { return min.x > max.x; }

    public Vec3 GetCenter() { return (min + max) * 0.5f; }
    public Vec3 GetSize() { return IsReset() ? new Vec3(0, 0, 0) : (max - min); }

    public void Add(Vec3 v)
    {
        min = new Vec3(System.Math.Min(min.x, v.x), System.Math.Min(min.y, v.y), System.Math.Min(min.z, v.z));
        max = new Vec3(System.Math.Max(max.x, v.x), System.Math.Max(max.y, v.y), System.Math.Max(max.z, v.z));
    }

    /// Port of `void AABB::Add(const Vec3& v, float radius)` from Cry_Geo.h
    public void Add(Vec3 v, float radius)
    {
        Vec3 ext = new Vec3(radius, radius, radius);
        Add(v - ext);
        Add(v + ext);
    }

    public void Add(AABB bb)
    {
        Add(bb.min);
        Add(bb.max);
    }

    public bool IsContainPoint(Vec3 pos)
    {
        if (pos.x < min.x) return false;
        if (pos.y < min.y) return false;
        if (pos.z < min.z) return false;
        if (pos.x > max.x) return false;
        if (pos.y > max.y) return false;
        if (pos.z > max.z) return false;
        return true;
    }

    /// Port of `bool AABB::IsIntersectBox(const AABB& b) const` from Cry_Geo.h
    public bool IsIntersectBox(AABB b)
    {
        if (min.x > b.max.x) return false;
        if (min.y > b.max.y) return false;
        if (min.z > b.max.z) return false;
        if (max.x < b.min.x) return false;
        if (max.y < b.min.y) return false;
        if (max.z < b.min.z) return false;
        return true;
    }
}

// Lineseg — Cry_Geo.h
public struct Lineseg
{
    public Vec3 start;
    public Vec3 end;

    public Lineseg(Vec3 s, Vec3 e) { start = s; end = e; }
}

// OBB — Cry_Geo.h subset
public struct OBB
{
    public Matrix33 m33; // OBB rotation
    public Vec3 h;       // half-lengths
    public Vec3 c;       // centre

    public void SetOBB(Matrix33 matrix, Vec3 halfLengths, Vec3 center)
    {
        m33 = matrix; h = halfLengths; c = center;
    }

    public static OBB CreateOBB(Matrix33 matrix, Vec3 halfLengths, Vec3 center)
    {
        OBB obb = new OBB();
        obb.SetOBB(matrix, halfLengths, center);
        return obb;
    }
}

// Matrix34 — Cry_Math.h subset (3x4 affine transform).
// Stored as 3x3 rotation/scale `m33` plus translation `t`.
public struct Matrix34
{
    public Matrix33 m33;
    public Vec3 t;

    public Matrix34(Matrix33 r, Vec3 translation) { m33 = r; t = translation; }

    /// Port of `Matrix34::CreateTranslationMat(Vec3 v)`
    public static Matrix34 CreateTranslationMat(Vec3 v)
    {
        Matrix34 m = new Matrix34();
        m.m33 = CryPhysics.Math.PhysMatrix33.Identity;
        m.t = v;
        return m;
    }

    /// Port of `Matrix34::GetTranslation()`
    public Vec3 GetTranslation() { return t; }

    /// Port of `Matrix34::GetColumn1()` — second column of the 3x3 rotation portion.
    public Vec3 GetColumn1()
    {
        return new Vec3(m33.M01, m33.M11, m33.M21);
    }
}

// Bounding box draw style enum used by CDebugDrawContext.DrawOBB
public enum EBoundingBoxDrawStyle
{
    eBBD_Faceted,
    eBBD_Extremes_Color_Encoded,
}

// Overlap helpers — port subset
public static class Overlap
{
    public static bool Point_AABB2D(Vec3 point, AABB aabb)
    {
        return point.x >= aabb.min.x && point.x <= aabb.max.x
            && point.y >= aabb.min.y && point.y <= aabb.max.y;
    }

    public static bool AABB_AABB2D(AABB a, AABB b)
    {
        return !(a.max.x < b.min.x || a.min.x > b.max.x
              || a.max.y < b.min.y || a.min.y > b.max.y);
    }

    /// Port of `Overlap::Lineseg_Polygon2D(Lineseg ls, ListPositions polygon, AABB* aabb=0)`.
    /// Returns true if the line segment overlaps the 2D polygon. Boundary AABB is an optional
    /// fast-reject hint.
    public static bool Lineseg_Polygon2D(Lineseg ls, IList<Vec3> polygon, AABB? aabb = null)
    {
        // Fast reject if endpoint outside boundary AABB.
        if (aabb.HasValue)
        {
            AABB bb = aabb.Value;
            float xmin = System.Math.Min(ls.start.x, ls.end.x);
            float xmax = System.Math.Max(ls.start.x, ls.end.x);
            float ymin = System.Math.Min(ls.start.y, ls.end.y);
            float ymax = System.Math.Max(ls.start.y, ls.end.y);
            if (xmax < bb.min.x || xmin > bb.max.x) return false;
            if (ymax < bb.min.y || ymin > bb.max.y) return false;
        }

        if (polygon == null || polygon.Count < 2) return false;

        // Test segment vs each polygon edge for 2D intersection.
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vec3 a = polygon[j];
            Vec3 b = polygon[i];
            // 2D segment-segment intersection (start->end vs a->b).
            float d1x = ls.end.x - ls.start.x;
            float d1y = ls.end.y - ls.start.y;
            float d2x = b.x - a.x;
            float d2y = b.y - a.y;
            float denom = d1x * d2y - d1y * d2x;
            if (denom == 0f) continue;
            float sx = a.x - ls.start.x;
            float sy = a.y - ls.start.y;
            float t1 = (sx * d2y - sy * d2x) / denom;
            float t2 = (sx * d1y - sy * d1x) / denom;
            if (t1 >= 0f && t1 <= 1f && t2 >= 0f && t2 <= 1f) return true;
        }
        return false;
    }
}

// Vec3 helper extension methods to mirror C++ Vec3 methods used by AIQuadTree etc.
public static class Vec3Extensions
{
    public static float GetLength2D(this Vec3 v)
    {
        return (float)System.Math.Sqrt(v.x * v.x + v.y * v.y);
    }
}
