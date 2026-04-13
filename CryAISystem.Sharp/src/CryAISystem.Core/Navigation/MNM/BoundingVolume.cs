// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/BoundingVolume.h (58L)
// and BoundingVolume.cpp (194L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;

namespace CryAISystem.Navigation.MNM;

public class BoundingVolume
{
    public List<Vec3> vertices = new();
    public AABB aabb = new AABB(AABB.type_reset.RESET_VAL);
    public float height = 0.0f;

    public BoundingVolume() { }

    public void Swap(BoundingVolume other)
    {
        var tmpVerts = vertices;
        vertices = other.vertices;
        other.vertices = tmpVerts;

        var tmpAabb = aabb;
        aabb = other.aabb;
        other.aabb = tmpAabb;

        var tmpHeight = height;
        height = other.height;
        other.height = tmpHeight;
    }

    public bool Overlaps(AABB _aabb)
    {
        if (!Overlap.AABB_AABB(aabb, _aabb))
            return false;

        int vertexCount = vertices.Count;

        for (int i = 0; i < vertexCount; ++i)
        {
            Vec3 v0 = vertices[i];
            if (Overlap.Point_AABB2D(v0, _aabb))
                return true;
        }

        float midz = aabb.min.z + (aabb.max.z - aabb.min.z) * 0.5f;

        if (Contains(new Vec3(_aabb.min.x, _aabb.min.y, midz)))
            return true;
        if (Contains(new Vec3(_aabb.min.x, _aabb.max.y, midz)))
            return true;
        if (Contains(new Vec3(_aabb.max.x, _aabb.max.y, midz)))
            return true;
        if (Contains(new Vec3(_aabb.max.x, _aabb.min.y, midz)))
            return true;

        int ii = vertexCount - 1;
        for (int i = 0; i < vertexCount; ++i)
        {
            Vec3 v0 = vertices[ii];
            Vec3 v1 = vertices[i];
            ii = i;

            if (Overlap.Lineseg_AABB2D(new Lineseg(v0, v1), _aabb))
                return true;
        }

        return false;
    }

    public enum ExtendedOverlap
    {
        NoOverlap = 0,
        PartialOverlap = 1,
        FullOverlap = 2,
    }

    public ExtendedOverlap ContainsAABB(AABB _aabb)
    {
        if (!Overlap.AABB_AABB(aabb, _aabb))
            return ExtendedOverlap.NoOverlap;

        int vertexCount = vertices.Count;
        int inCount = 0;

        if (Contains(new Vec3(_aabb.min.x, _aabb.min.y, _aabb.min.z))) ++inCount;
        if (Contains(new Vec3(_aabb.min.x, _aabb.min.y, _aabb.max.z))) ++inCount;
        if (Contains(new Vec3(_aabb.min.x, _aabb.max.y, _aabb.min.z))) ++inCount;
        if (Contains(new Vec3(_aabb.min.x, _aabb.max.y, _aabb.max.z))) ++inCount;
        if (Contains(new Vec3(_aabb.max.x, _aabb.max.y, _aabb.min.z))) ++inCount;
        if (Contains(new Vec3(_aabb.max.x, _aabb.max.y, _aabb.max.z))) ++inCount;
        if (Contains(new Vec3(_aabb.max.x, _aabb.min.y, _aabb.max.z))) ++inCount;
        if (Contains(new Vec3(_aabb.max.x, _aabb.min.y, _aabb.max.z))) ++inCount;

        if (inCount != 8)
            return ExtendedOverlap.PartialOverlap;

        int ii = vertexCount - 1;
        for (int i = 0; i < vertexCount; ++i)
        {
            Vec3 v0 = vertices[ii];
            Vec3 v1 = vertices[i];
            ii = i;

            if (Overlap.Lineseg_AABB2D(new Lineseg(v0, v1), _aabb))
                return ExtendedOverlap.PartialOverlap;
        }

        return ExtendedOverlap.FullOverlap;
    }

    public bool Contains(Vec3 point)
    {
        if (!Overlap.Point_AABB(point, aabb))
            return false;

        int vertexCount = vertices.Count;
        bool count = false;

        int ii = vertexCount - 1;
        for (int i = 0; i < vertexCount; ++i)
        {
            Vec3 v0 = vertices[ii];
            Vec3 v1 = vertices[i];
            ii = i;

            if ((((v1.y <= point.y) && (point.y < v0.y)) || ((v0.y <= point.y) && (point.y < v1.y))) &&
                (point.x < (v0.x - v1.x) * (point.y - v1.y) / (v0.y - v1.y) + v1.x))
            {
                count = !count;
            }
        }

        return count;
    }

    // Separating axis test helper
    private static bool DoesAxisOverlap(float segMin, float segMax, float boxMin, float boxMax, ref float t0, ref float t1)
    {
        float rayDir = segMax - segMin;

        if (!fcmp(rayDir, 0.0f))
        {
            float s0 = (boxMin - segMin) / rayDir;
            float s1 = (boxMax - segMin) / rayDir;

            if (s1 < s0)
            {
                float tmp = s0; s0 = s1; s1 = tmp;
            }

            if (s0 < t1 && s1 > t0)
            {
                t0 = Math.Max(t0, s0);
                t1 = Math.Min(t1, s1);
                return true;
            }
        }

        return false;
    }

    public (bool hit, float t) IntersectLineSeg(Vec3 segP0, Vec3 segP1)
    {
        float t0 = 0.0f;
        float t1 = 1.0f;

        if (!DoesAxisOverlap(segP0.x, segP1.x, this.aabb.min.x, this.aabb.max.x, ref t0, ref t1))
            return (false, -1.0f);
        if (!DoesAxisOverlap(segP0.y, segP1.y, this.aabb.min.y, this.aabb.max.y, ref t0, ref t1))
            return (false, -1.0f);
        if (!DoesAxisOverlap(segP0.z, segP1.z, this.aabb.min.z, this.aabb.max.z, ref t0, ref t1))
            return (false, -1.0f);
        return (true, t0);
    }

    // fcmp — C++ `!fcmp(a, 0.0f)` means a != 0.0f (approximately)
    private static bool fcmp(float a, float b)
    {
        return MathF.Abs(a - b) < 1e-6f;
    }
}

public struct HashBoundingVolume
{
    public BoundingVolume vol;
    public uint hash;
}
