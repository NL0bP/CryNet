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

    public float GetRadius() { var s = GetSize(); return s.Length() * 0.5f; }

    public void Move(Vec3 v) { min = min + v; max = max + v; }

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

    /// Port of `bool AABB::ContainsBox(const AABB& b) const` from Cry_Geo.h
    public bool ContainsBox(AABB b)
    {
        return b.min.x >= min.x && b.min.y >= min.y && b.min.z >= min.z
            && b.max.x <= max.x && b.max.y <= max.y && b.max.z <= max.z;
    }

    /// Port of `float AABB::GetVolume() const` from Cry_Geo.h
    public float GetVolume()
    {
        return (max.x - min.x) * (max.y - min.y) * (max.z - min.z);
    }

    /// Port of `void AABB::Expand(Vec3 v)` from Cry_Geo.h
    public void Expand(Vec3 v)
    {
        min = min - v;
        max = max + v;
    }
}

// Sphere is defined in PathObstacles.cs

// Lineseg — Cry_Geo.h
public struct Lineseg
{
    public Vec3 start;
    public Vec3 end;

    public Lineseg(Vec3 s, Vec3 e) { start = s; end = e; }

    /// Port of `Vec3 Lineseg::GetPoint(float t) const` from Cry_Geo.h
    public Vec3 GetPoint(float t) { return start + (end - start) * t; }
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

    /// Port of `Matrix34::SetTranslation()` — used by Movement system
    public void SetTranslation(Vec3 v) { t = v; }

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
public static partial class Overlap
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

// Overlap::Point_Polygon2D — Cry_GeoOverlap.h
public static partial class Overlap
{
    /// Returns true if the 2D point lies inside the convex/simple polygon.
    public static bool Point_Polygon2D(Vec3 pt, IList<Vec3> polygon)
    {
        if (polygon == null || polygon.Count < 3) return false;
        int n = polygon.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((polygon[i].y > pt.y) != (polygon[j].y > pt.y)) &&
                (pt.x < (polygon[j].x - polygon[i].x) * (pt.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// Overload used by Shape.cpp: `Overlap::Point_Polygon2D(pt, m_points, &m_aabb)`
    public static bool Point_Polygon2D(Vec3 pt, IList<Vec3> polygon, AABB? aabb)
    {
        if (aabb.HasValue && !Point_AABB2D(pt, aabb.Value))
            return false;
        return Point_Polygon2D(pt, polygon);
    }

    /// Port of `Overlap::Sphere_AABB2D` from Cry_GeoOverlap.h
    public static bool Sphere_AABB2D(Sphere s, AABB aabb)
    {
        float dx = 0, dy = 0;
        if (s.center.x < aabb.min.x) dx = s.center.x - aabb.min.x;
        else if (s.center.x > aabb.max.x) dx = s.center.x - aabb.max.x;
        if (s.center.y < aabb.min.y) dy = s.center.y - aabb.min.y;
        else if (s.center.y > aabb.max.y) dy = s.center.y - aabb.max.y;
        return (dx * dx + dy * dy) <= (s.radius * s.radius);
    }

    /// Port of `Overlap::Lineseg_AABB2D` from Cry_GeoOverlap.h (2D AABB overlap test)
    public static bool Lineseg_AABB2D(Lineseg ls, AABB aabb)
    {
        // Cohen-Sutherland clipping in 2D
        float x0 = ls.start.x, y0 = ls.start.y;
        float x1 = ls.end.x, y1 = ls.end.y;
        int ComputeOutCode(float x, float y)
        {
            int code = 0;
            if (x < aabb.min.x) code |= 1;
            else if (x > aabb.max.x) code |= 2;
            if (y < aabb.min.y) code |= 4;
            else if (y > aabb.max.y) code |= 8;
            return code;
        }
        int outcode0 = ComputeOutCode(x0, y0);
        int outcode1 = ComputeOutCode(x1, y1);
        while (true)
        {
            if ((outcode0 | outcode1) == 0) return true;
            if ((outcode0 & outcode1) != 0) return false;
            int outcodeOut = outcode0 != 0 ? outcode0 : outcode1;
            float x = 0, y = 0;
            if ((outcodeOut & 8) != 0) { x = x0 + (x1 - x0) * (aabb.max.y - y0) / (y1 - y0); y = aabb.max.y; }
            else if ((outcodeOut & 4) != 0) { x = x0 + (x1 - x0) * (aabb.min.y - y0) / (y1 - y0); y = aabb.min.y; }
            else if ((outcodeOut & 2) != 0) { y = y0 + (y1 - y0) * (aabb.max.x - x0) / (x1 - x0); x = aabb.max.x; }
            else if ((outcodeOut & 1) != 0) { y = y0 + (y1 - y0) * (aabb.min.x - x0) / (x1 - x0); x = aabb.min.x; }
            if (outcodeOut == outcode0) { x0 = x; y0 = y; outcode0 = ComputeOutCode(x0, y0); }
            else { x1 = x; y1 = y; outcode1 = ComputeOutCode(x1, y1); }
        }
    }
}

// Intersect helpers — Cry_GeoIntersect.h subset
public static partial class Intersect
{
    /// Ray_AABB — returns true if the ray intersects the AABB, optionally returning the hit point.
    public static bool Ray_AABB(CryAISystem.Ray ray, AABB box, out Vec3 hitPoint)
    {
        hitPoint = new Vec3(0, 0, 0);
        float tmin = float.MinValue;
        float tmax = float.MaxValue;

        float[] origin = { ray.origin.x, ray.origin.y, ray.origin.z };
        float[] dir = { ray.direction.x, ray.direction.y, ray.direction.z };
        float[] bmin = { box.min.x, box.min.y, box.min.z };
        float[] bmax = { box.max.x, box.max.y, box.max.z };

        for (int i = 0; i < 3; i++)
        {
            if (System.Math.Abs(dir[i]) < 1e-10f)
            {
                if (origin[i] < bmin[i] || origin[i] > bmax[i]) return false;
            }
            else
            {
                float ood = 1.0f / dir[i];
                float t1 = (bmin[i] - origin[i]) * ood;
                float t2 = (bmax[i] - origin[i]) * ood;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                if (t1 > tmin) tmin = t1;
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
        }
        hitPoint = new Vec3(ray.origin.x + ray.direction.x * tmin,
                            ray.origin.y + ray.direction.y * tmin,
                            ray.origin.z + ray.direction.z * tmin);
        return true;
    }

    /// Lineseg_Lineseg2D — 2D segment-segment intersection test. Returns tA and tB parametrics.
    public static bool Lineseg_Lineseg2D(Lineseg a, Lineseg b, out float tA, out float tB)
    {
        const float epsilon = 0.0000001f;
        float d1x = a.end.x - a.start.x, d1y = a.end.y - a.start.y;
        float d2x = b.end.x - b.start.x, d2y = b.end.y - b.start.y;
        float denom = d1x * d2y - d1y * d2x;
        if (System.Math.Abs(denom) < epsilon) { tA = tB = 0.5f; return false; }
        float sx = b.start.x - a.start.x, sy = b.start.y - a.start.y;
        tA = (sx * d2y - sy * d2x) / denom;
        tB = (sx * d1y - sy * d1x) / denom;
        return tA >= 0f && tA <= 1f && tB >= 0f && tB <= 1f;
    }

    /// Line_Plane — intersect an infinite line with a plane, return intersection point.
    public static bool Line_Plane(Line line, Plane plane, out Vec3 pt)
    {
        Vec3 dir = line.direction;
        float denom = plane.n.Dot(dir);
        pt = line.origin;
        if (System.Math.Abs(denom) < 1e-10f) return false;
        float t = -(plane.n.Dot(line.origin) + plane.d) / denom;
        pt = line.origin + dir * t;
        return true;
    }

    /// Ray_Plane — intersect a ray with a plane, return intersection point.
    public static bool Ray_Plane(CryAISystem.Ray ray, Plane plane, out Vec3 pt)
    {
        float denom = plane.n.Dot(ray.direction);
        pt = ray.origin;
        if (System.Math.Abs(denom) < 1e-10f) return false;
        float t = -(plane.n.Dot(ray.origin) + plane.d) / denom;
        if (t < 0f) return false;
        pt = ray.origin + ray.direction * t;
        return true;
    }

    /// Lineseg_Polygon2D — intersect a 2D line segment with a convex polygon, return intersection point.
    public static bool Lineseg_Polygon2D(Lineseg ls, IList<Vec3> polygon, out Vec3 hitPoint)
    {
        hitPoint = new Vec3(0, 0, 0);
        if (polygon == null || polygon.Count < 2) return false;
        int n = polygon.Count;
        float closestT = float.MaxValue;
        bool found = false;

        float d1x = ls.end.x - ls.start.x;
        float d1y = ls.end.y - ls.start.y;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float d2x = polygon[i].x - polygon[j].x;
            float d2y = polygon[i].y - polygon[j].y;
            float denom = d1x * d2y - d1y * d2x;
            if (System.Math.Abs(denom) < 1e-10f) continue;
            float sx = polygon[j].x - ls.start.x;
            float sy = polygon[j].y - ls.start.y;
            float t1 = (sx * d2y - sy * d2x) / denom;
            float t2 = (sx * d1y - sy * d1x) / denom;
            if (t1 >= 0f && t1 <= 1f && t2 >= 0f && t2 <= 1f)
            {
                if (t1 < closestT)
                {
                    closestT = t1;
                    hitPoint = new Vec3(ls.start.x + d1x * t1, ls.start.y + d1y * t1, 0);
                    found = true;
                }
            }
        }
        return found;
    }
}

// Distance helpers — Cry_GeoDistance.h subset
public static class Distance
{
    /// Squared distance between two 3D points.
    public static float Point_PointSq(Vec3 a, Vec3 b)
    {
        return (a - b).GetLengthSquared();
    }

    /// Distance between two 3D points.
    public static float Point_Point(Vec3 a, Vec3 b)
    {
        return (a - b).Length();
    }

    /// Squared 2D distance between two points (XY plane only, ignoring Z).
    public static float Point_Point2DSq(Vec3 a, Vec3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    /// Squared distance from a point to a 2D polygon (XY plane). Returns closest point in `closestPt`.
    public static float Point_Polygon2D(Vec3 pt, IList<Vec3> polygon, out Vec3 closestPt)
    {
        closestPt = pt;
        if (polygon == null || polygon.Count < 2) return 0f;
        float minDistSq = float.MaxValue;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vec3 a = polygon[j];
            Vec3 b = polygon[i];
            // project pt onto edge a->b in 2D
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float lenSq = dx * dx + dy * dy;
            float t = 0f;
            if (lenSq > 1e-10f)
                t = ((pt.x - a.x) * dx + (pt.y - a.y) * dy) / lenSq;
            t = System.Math.Clamp(t, 0f, 1f);
            float cx = a.x + t * dx;
            float cy = a.y + t * dy;
            float dSq = (pt.x - cx) * (pt.x - cx) + (pt.y - cy) * (pt.y - cy);
            if (dSq < minDistSq)
            {
                minDistSq = dSq;
                closestPt = new Vec3(cx, cy, 0);
            }
        }
        return (float)System.Math.Sqrt(minDistSq);
    }

    /// Squared distance from a point to a line segment. `t` is the parametric position [0,1].
    public static float Point_LinesegSq(Vec3 pt, Lineseg ls, out float t)
    {
        Vec3 diff = pt - ls.start;
        Vec3 dir = ls.end - ls.start;
        float fT = diff.Dot(dir);
        float lenSq = dir.GetLengthSquared();
        if (lenSq > 0f)
        {
            fT /= lenSq;
            fT = System.Math.Clamp(fT, 0f, 1f);
        }
        else
        {
            fT = 0f;
        }
        t = fT;
        Vec3 closest = ls.start + dir * fT;
        return (pt - closest).GetLengthSquared();
    }

    /// Distance from point to lineseg (3D). Returns distance, sets t.
    public static float Point_Lineseg(Vec3 pt, Lineseg ls, out float t)
    {
        return (float)System.Math.Sqrt(Point_LinesegSq(pt, ls, out t));
    }

    /// 2D distance from point to lineseg (XY). Returns distance, sets t.
    public static float Point_Lineseg2D(Vec3 pt, Lineseg ls, out float t)
    {
        Vec2 diff = new Vec2(pt.x - ls.start.x, pt.y - ls.start.y);
        Vec2 dir = new Vec2(ls.end.x - ls.start.x, ls.end.y - ls.start.y);
        float fT = diff.Dot(dir);
        float lenSq = dir.GetLength2();
        if (lenSq > 0f) { fT /= lenSq; fT = System.Math.Clamp(fT, 0f, 1f); }
        else fT = 0f;
        t = fT;
        Vec2 closest = new Vec2(ls.start.x + dir.x * fT, ls.start.y + dir.y * fT);
        float dx = pt.x - closest.x, dy = pt.y - closest.y;
        return (float)System.Math.Sqrt(dx * dx + dy * dy);
    }

    /// Squared 2D distance between two line segments (XY plane).
    public static float Lineseg_Lineseg2DSq(Lineseg a, Lineseg b)
    {
        float tA, tB;
        // Try intersection first
        if (Intersect.Lineseg_Lineseg2D(a, b, out tA, out tB))
            return 0f;
        // Otherwise, min of point-to-segment distances
        float d1 = Point_Lineseg2D_Sq(a.start, b);
        float d2 = Point_Lineseg2D_Sq(a.end, b);
        float d3 = Point_Lineseg2D_Sq(b.start, a);
        float d4 = Point_Lineseg2D_Sq(b.end, a);
        return System.Math.Min(System.Math.Min(d1, d2), System.Math.Min(d3, d4));
    }

    private static float Point_Lineseg2D_Sq(Vec3 pt, Lineseg ls)
    {
        Vec2 diff = new Vec2(pt.x - ls.start.x, pt.y - ls.start.y);
        Vec2 dir = new Vec2(ls.end.x - ls.start.x, ls.end.y - ls.start.y);
        float fT = diff.Dot(dir);
        float lenSq = dir.GetLength2();
        if (lenSq > 0f) { fT /= lenSq; fT = System.Math.Clamp(fT, 0f, 1f); }
        else fT = 0f;
        float cx = ls.start.x + dir.x * fT;
        float cy = ls.start.y + dir.y * fT;
        float dx = pt.x - cx, dy = pt.y - cy;
        return dx * dx + dy * dy;
    }

    // Template-style Lineseg_Lineseg2DSq used by Shape.cpp
    public static float Lineseg_Lineseg2DSq<T>(Lineseg a, Lineseg b) where T : struct
    {
        return Lineseg_Lineseg2DSq(a, b);
    }

    /// Distance between two points projected on XY plane.
    public static float Point_Point2D(Vec3 a, Vec3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)System.Math.Sqrt(dx * dx + dy * dy);
    }

    /// Squared 2D distance from a point to a line segment (XY). Returns squared distance, sets t.
    public static float Point_Lineseg2DSq(Vec3 pt, Lineseg ls, out float t)
    {
        Vec2 diff = new Vec2(pt.x - ls.start.x, pt.y - ls.start.y);
        Vec2 dir = new Vec2(ls.end.x - ls.start.x, ls.end.y - ls.start.y);
        float fT = diff.Dot(dir);
        float lenSq = dir.GetLength2();
        if (lenSq > 0f) { fT /= lenSq; fT = System.Math.Clamp(fT, 0f, 1f); }
        else fT = 0f;
        t = fT;
        float cx = ls.start.x + dir.x * fT;
        float cy = ls.start.y + dir.y * fT;
        float dx = pt.x - cx, dy = pt.y - cy;
        return dx * dx + dy * dy;
    }
}

// Plane — Cry_Geo.h
public struct Plane
{
    public Vec3 n;
    public float d;

    public void SetPlane(Vec3 normal, Vec3 point)
    {
        n = normal;
        d = -normal.Dot(point);
    }

    public static Plane CreatePlane(Vec3 normal, Vec3 point)
    {
        Plane p = new Plane();
        p.SetPlane(normal, point);
        return p;
    }
}

// Line — Cry_Geo.h (infinite line: origin + direction)
public struct Line
{
    public Vec3 origin;
    public Vec3 direction;

    public Line(Vec3 o, Vec3 d) { origin = o; direction = d; }
}

// Overlap 3D helpers — Cry_GeoOverlap.h subset (needed by MNM::BoundingVolume)
public static partial class Overlap
{
    /// AABB_AABB — 3D AABB overlap test
    public static bool AABB_AABB(AABB a, AABB b)
    {
        return !(a.max.x < b.min.x || a.min.x > b.max.x
              || a.max.y < b.min.y || a.min.y > b.max.y
              || a.max.z < b.min.z || a.min.z > b.max.z);
    }

    /// Point_AABB — 3D point-in-AABB test
    public static bool Point_AABB(Vec3 point, AABB aabb)
    {
        return point.x >= aabb.min.x && point.x <= aabb.max.x
            && point.y >= aabb.min.y && point.y <= aabb.max.y
            && point.z >= aabb.min.z && point.z <= aabb.max.z;
    }
}

// Overlap::Point_Polygon2D for Vec2 arrays — used by CollisionAvoidanceSystem.cpp
public static partial class Overlap
{
    /// Returns true if the 2D point lies inside the convex polygon (Vec2 arrays, counter-clockwise winding).
    public static bool Point_Polygon2D(Vec2 pt, Vec2[] polygon, int vertexCount)
    {
        if (polygon == null || vertexCount < 3) return false;
        bool inside = false;
        for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
        {
            if (((polygon[i].y > pt.y) != (polygon[j].y > pt.y)) &&
                (pt.x < (polygon[j].x - polygon[i].x) * (pt.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}

// Vec3 helper extension methods to mirror C++ Vec3 methods used by AIQuadTree etc.
public static class Vec3Extensions
{
    public static float GetLength2D(this Vec3 v)
    {
        return (float)System.Math.Sqrt(v.x * v.x + v.y * v.y);
    }

    public static float GetSquaredDistance2D(this Vec3 a, Vec3 b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    /// Port of Vec3::CompMul — component-wise multiplication
    public static Vec3 CompMul(this Vec3 a, Vec3 b) => new Vec3(a.x * b.x, a.y * b.y, a.z * b.z);

    /// Port of Vec3::IsZero()
    public static bool IsZeroVec(this Vec3 v) => v.x == 0f && v.y == 0f && v.z == 0f;

    /// Port of Vec3::abs()
    public static Vec3 Abs(this Vec3 v) => new Vec3(System.Math.Abs(v.x), System.Math.Abs(v.y), System.Math.Abs(v.z));
}

// Vec3i — integer 3D vector (Cry_Vector3.h)
public struct Vec3i : IEquatable<Vec3i>
{
    public int x, y, z;

    public Vec3i(int v) { x = v; y = v; z = v; }
    public Vec3i(int _x, int _y, int _z) { x = _x; y = _y; z = _z; }

    public static Vec3i operator +(Vec3i a, Vec3i b) => new Vec3i(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vec3i operator -(Vec3i a, Vec3i b) => new Vec3i(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vec3i operator +(Vec3i a, Vec2i b) => new Vec3i(a.x + b.X, a.y + b.Y, a.z);

    public static bool operator ==(Vec3i a, Vec3i b) => a.x == b.x && a.y == b.y && a.z == b.z;
    public static bool operator !=(Vec3i a, Vec3i b) => !(a == b);

    public static implicit operator Vec3i(int v) => new Vec3i(v, v, v);

    public bool Equals(Vec3i other) => x == other.x && y == other.y && z == other.z;
    public override bool Equals(object obj) => obj is Vec3i v && Equals(v);
    public override int GetHashCode() => System.HashCode.Combine(x, y, z);
    public override string ToString() => $"({x}, {y}, {z})";
}

// Vec2i extensions for TileGenerator tracer support
public static class Vec2iExtensions
{
    /// Port of Vec2i::rot90ccw (rotate 90 degrees counter-clockwise)
    public static Vec2i rot90ccw(this Vec2i v) => new Vec2i(-v.Y, v.X);

    /// Port of Vec2i::rot90cw (rotate 90 degrees clockwise)
    public static Vec2i rot90cw(this Vec2i v) => new Vec2i(v.Y, -v.X);
}

// Matrix34 extensions for TransformPoint/TransformVector/AddTranslation
public static class Matrix34Extensions
{
    /// Port of Matrix34::TransformPoint
    public static Vec3 TransformPoint(this Matrix34 m, Vec3 p)
    {
        return new Vec3(
            m.m33.M00 * p.x + m.m33.M01 * p.y + m.m33.M02 * p.z + m.t.x,
            m.m33.M10 * p.x + m.m33.M11 * p.y + m.m33.M12 * p.z + m.t.y,
            m.m33.M20 * p.x + m.m33.M21 * p.y + m.m33.M22 * p.z + m.t.z);
    }

    /// Port of Matrix34::TransformVector
    public static Vec3 TransformVector(this Matrix34 m, Vec3 p)
    {
        return new Vec3(
            m.m33.M00 * p.x + m.m33.M01 * p.y + m.m33.M02 * p.z,
            m.m33.M10 * p.x + m.m33.M11 * p.y + m.m33.M12 * p.z,
            m.m33.M20 * p.x + m.m33.M21 * p.y + m.m33.M22 * p.z);
    }

    /// Port of Matrix34::AddTranslation
    public static void AddTranslation(ref this Matrix34 m, Vec3 offset)
    {
        m.t = m.t + offset;
    }
}

// Matrix33 static helpers
public static class Matrix33Helpers
{
    /// Port of HasNoRotOrScale — checks if m is identity
    public static bool HasNoRotOrScale(Matrix33 m)
    {
        return (m.M00 == 1.0f) && (m.M11 == 1.0f) && (m.M22 == 1.0f) &&
               (m.M01 == 0.0f) && (m.M02 == 0.0f) &&
               (m.M10 == 0.0f) && (m.M12 == 0.0f) &&
               (m.M20 == 0.0f) && (m.M21 == 0.0f);
    }
}

// Vec2i ctor that takes (Vec3i) — implicit conversion used by TileGenerator
// already defined as CryPhysics.Math.Vector2i — we'll add overloads via Vec2i alias
public static class Vec2iConstructors
{
    public static Vec2i FromVec3i(Vec3i v) => new Vec2i(v.x, v.y);
}
