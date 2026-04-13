// Literal port of dev/Code/CryEngine/CryAISystem/Shape2.h + Shape2.cpp (435L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Temp file holding code extracted from CAISystem.h/cpp

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using CryAISystem.CryCommon;
using CryAISystem.Walkability;

namespace CryAISystem;

file static class Shape2Statics
{
    public static bool useForbiddenMask = false;
    public static uint forbiddenMaskMaxDimension = 256; // max side of the mask (256->16KB)
    public static float forbiddenMaskGranularity = 2.0f;
    public static float criticalForbiddenSize = 30.0f;
}

public class CShapeMask
{
    //===================================================================
    // CShapeMask
    //===================================================================
    public CShapeMask()
    {
        m_nx = 0;
        m_ny = 0;
        m_dx = 0;
        m_dy = 0;
    }

    //===================================================================
    // Build
    //===================================================================
    public void Build(SShape pShape, float granularity)
    {
        // AIAssert(pShape);
        // AIAssert(granularity > 0.0f);
        m_aabb = pShape.aabb;
        Vec3 delta = m_aabb.max - m_aabb.min;
        m_nx = 1 + (uint)(delta.x / granularity);
        m_ny = 1 + (uint)(delta.y / granularity);

        if (m_nx > Shape2Statics.forbiddenMaskMaxDimension)
            m_nx = Shape2Statics.forbiddenMaskMaxDimension;
        if (m_ny > Shape2Statics.forbiddenMaskMaxDimension)
            m_ny = Shape2Statics.forbiddenMaskMaxDimension;

        m_dx = delta.x / m_nx;
        m_dy = delta.y / m_ny;

        m_bytesPerRow = (m_nx + 3) >> 2;

        m_container.Clear();
        m_container.AddRange(new byte[m_ny * m_bytesPerRow]);

        for (uint j = 0; j != m_ny; ++j)
        {
            for (uint i = 0; i != m_nx; ++i)
            {
                uint index = (i >> 2) + m_bytesPerRow * j;
                // assert(index < m_container.Count);

                uint offset = (i & 3) * 2;

                Vec3 bmin, bmax;
                GetBox(out bmin, out bmax, (int)i, (int)j);

                byte value = (byte)((byte)GetType(pShape, bmin, bmax) << (int)offset);
                byte mask = (byte)(3 << (int)offset);
                m_container[(int)index] = (byte)((m_container[(int)index] & ~mask) + value);
            }
        }
    }

    public enum EType { TYPE_IN, TYPE_EDGE, TYPE_OUT }

    //===================================================================
    // GetType (point)
    //===================================================================
    public EType GetType(Vec3 pt)
    {
        if (!Shape2Statics.useForbiddenMask)
            return EType.TYPE_EDGE;
        int i = (int)((pt.x - m_aabb.min.x) / m_dx);
        if (i < 0 || i >= (int)m_nx)
            return EType.TYPE_OUT;
        int j = (int)((pt.x - m_aabb.min.y) / m_dx);
        if (j < 0 || j >= (int)m_ny)
            return EType.TYPE_OUT;

        uint index = ((uint)i >> 2) + m_bytesPerRow * (uint)j;
        // assert(index < m_container.Count);
        byte b = m_container[(int)index];

        uint offset = ((uint)i & 2) * 2;
        byte mask = (byte)(3 << (int)offset);

        byte value = (byte)(b & mask);
        value = (byte)(value >> (int)offset);
        return (EType)value;
    }

    //===================================================================
    // MemStats
    //===================================================================
    public nuint MemStats()
    {
        nuint size = (nuint)64; // approximate sizeof(*this)
        size += (nuint)m_container.Capacity;
        return size;
    }

    //===================================================================
    // GetType (shape, box)
    //===================================================================
    private EType GetType(SShape pShape, Vec3 bmin, Vec3 bmax)
    {
        Vec3 delta = bmax - bmin;
        if (Overlap.Lineseg_Polygon2D(new Lineseg(bmin, bmin + new Vec3(delta.x, 0.0f, 0.0f)), pShape.shape, pShape.aabb))
            return EType.TYPE_EDGE;
        if (Overlap.Lineseg_Polygon2D(new Lineseg(bmin + new Vec3(0.0f, delta.y, 0.0f), bmin + new Vec3(delta.x, delta.y, 0.0f)), pShape.shape, pShape.aabb))
            return EType.TYPE_EDGE;
        if (Overlap.Lineseg_Polygon2D(new Lineseg(bmin, bmin + new Vec3(0.0f, delta.y, 0.0f)), pShape.shape, pShape.aabb))
            return EType.TYPE_EDGE;
        if (Overlap.Lineseg_Polygon2D(new Lineseg(bmin + new Vec3(delta.x, 0.0f, 0.0f), bmin + new Vec3(delta.x, delta.y, 0.0f)), pShape.shape, pShape.aabb))
            return EType.TYPE_EDGE;

        Vec3 mid = (bmin + bmax) * 0.5f;
        if (Overlap.Point_Polygon2D(mid, pShape.shape, pShape.aabb))
            return EType.TYPE_IN;
        else
            return EType.TYPE_OUT;
    }

    //===================================================================
    // GetBox
    //===================================================================
    private void GetBox(out Vec3 bmin, out Vec3 bmax, int i, int j)
    {
        bmin = m_aabb.min + new Vec3(i * m_dx, j * m_dy, 0.0f);
        bmax = bmin + new Vec3(m_dx, m_dy, 0.0f);
    }

    private AABB m_aabb;
    private uint m_nx, m_ny;
    private float m_dx, m_dy;

    private uint m_bytesPerRow;
    // typedef std::vector<unsigned char> TContainer;
    private List<byte> m_container = new List<byte>();
}

public class SShape
{
    //===================================================================
    // SShape() default
    //===================================================================
    public SShape()
    {
        navType = IAISystem_ENavigationType.NAV_UNSET;
        type = 0;
        devalueTime = 0.0f;
        height = 0.0f;
        temporary = false;
        aabb = new AABB(AABB.RESET);
        enabled = true;
        shapeMask = null;
        lightLevel = EAILightLevel.AILL_NONE;
        closed = false;
        shape = new ListPositions();
    }

    //===================================================================
    // SShape(shape_, aabb_, ...)
    //===================================================================
    public SShape(
        ListPositions shape_,
        AABB aabb_,
        IAISystem_ENavigationType navType_ = IAISystem_ENavigationType.NAV_UNSET,
        int type_ = 0,
        bool closed_ = false,
        float height_ = 0.0f,
        EAILightLevel lightLevel_ = EAILightLevel.AILL_NONE,
        bool temp = false)
    {
        shape = shape_;
        aabb = aabb_;
        navType = IAISystem_ENavigationType.NAV_UNSET;
        type = 0;
        devalueTime = 0;
        height = height_;
        temporary = temp;
        enabled = true;
        shapeMask = null;
        lightLevel = lightLevel_;
        closed = closed_;
    }

    //===================================================================
    // SShape(shape_, allowMask, ...)
    //===================================================================
    public SShape(
        ListPositions shape_,
        bool allowMask = false,
        IAISystem_ENavigationType navType_ = IAISystem_ENavigationType.NAV_UNSET,
        int type_ = 0,
        bool closed_ = false,
        float height_ = 0.0f,
        EAILightLevel lightLevel_ = EAILightLevel.AILL_NONE,
        bool temp = false)
    {
        shape = shape_;
        navType = navType_;
        type = type_;
        devalueTime = 0;
        height = height_;
        temporary = temp;
        enabled = true;
        shapeMask = null;
        lightLevel = lightLevel_;
        closed = closed_;

        RecalcAABB();
        if (allowMask)
            BuildMask(Shape2Statics.forbiddenMaskGranularity);
    }

    //===================================================================
    // RecalcAABB
    //===================================================================
    public void RecalcAABB()
    {
        aabb.Reset();
        aabb.min = new Vec3(aabb.min.x, aabb.min.y, 10000.0f); // avoid including limit
        aabb.max = new Vec3(aabb.max.x, aabb.max.y, -10000.0f);
        foreach (Vec3 v in shape)
            aabb.Add(v);
    }

    //===================================================================
    // BuildMask
    //===================================================================
    public void BuildMask(float granularity)
    {
        Vec3 delta = aabb.max - aabb.min;
        if (Shape2Statics.useForbiddenMask && (delta.x > Shape2Statics.criticalForbiddenSize || delta.y > Shape2Statics.criticalForbiddenSize))
        {
            if (shapeMask == null)
                shapeMask = new CShapeMask();
            shapeMask.Build(this, Shape2Statics.forbiddenMaskGranularity);
        }
        else
        {
            ReleaseMask();
        }
    }

    //===================================================================
    // ReleaseMask
    //===================================================================
    public void ReleaseMask()
    {
        shapeMask = null;
    }

    //===================================================================
    // OffsetShape
    //===================================================================
    public void OffsetShape(Vec3 offset)
    {
        for (int i = 0; i < shape.Count; i++)
        {
            shape[i] = shape[i] + offset;
        }
        aabb.Move(offset);
    }

    //===================================================================
    // NearestPointOnPath
    //===================================================================
    public int NearestPointOnPath(Vec3 pos, bool forceLoop, out float dist, out Vec3 nearestPt, out float distAlongPath,
        out Vec3 segmentDir, out uint segmentStartIndex, out float pathLength, out float segmentFraction)
    {
        dist = 0; nearestPt = pos; distAlongPath = 0; segmentDir = new Vec3(0, 0, 0);
        segmentStartIndex = 0; pathLength = 0; segmentFraction = 0;

        if (shape.Count == 0)
            return shape.Count; // past end

        dist = float.MaxValue;

        int nearest = 0;
        int cur = 0;
        int next = 1;
        int size = shape.Count;
        int loopEnd = size;

        if (forceLoop && !closed)
            loopEnd += 1;

        float pathLen = 0.0f;

        while (next != loopEnd)
        {
            Lineseg seg = new Lineseg(shape[cur], shape[next % size]);
            float t;
            float d = Distance.Point_Lineseg(pos, seg, out t);
            if (d < dist)
            {
                dist = d;
                nearestPt = seg.GetPoint(t);
                distAlongPath = pathLen + Distance.Point_Point(seg.start, nearestPt);
                segmentDir = seg.end - seg.start;
                segmentStartIndex = (uint)cur;
                segmentFraction = t;
                nearest = next;
            }
            pathLen += Distance.Point_Point(seg.start, seg.end);
            cur = next;
            ++next;
        }

        pathLength = pathLen;

        return nearest;
    }

    // Simplified overload used by CPipeUser::GetPathEntryPoint
    public int NearestPointOnPath(Vec3 pos, bool forceLoop, out float dist, out Vec3 nearestPt)
    {
        float distAlongPath; Vec3 segmentDir; uint segmentStartIndex; float pathLength; float segmentFraction;
        return NearestPointOnPath(pos, forceLoop, out dist, out nearestPt, out distAlongPath, out segmentDir, out segmentStartIndex, out pathLength, out segmentFraction);
    }

    //===================================================================
    // GetIntersectionDistances
    //===================================================================
    public int GetIntersectionDistances(Vec3 start, Vec3 end, float[] intersectDistArray, int maxIntersections, bool testHeight = false, bool testTopBottom = false)
    {
        Lineseg ray = new Lineseg(start, end);
        float rayLength = sqrtf(start.GetSquaredDistance2D(end));

        int intersects = 0;
        float tA, tB; // Intersection parameters as distances along each line

        int idx = 0;
        while (idx + 1 < shape.Count)
        {
            Vec3 curPt = shape[idx];
            Vec3 nextPt = shape[idx + 1];
            Lineseg seg = new Lineseg(curPt, nextPt);
            bool hit = Intersect.Lineseg_Lineseg2D(ray, seg, out tA, out tB);

            // Test height
            if (hit && testHeight)
            {
                // Treat all sides as being the full AABB height, so we don't get any gaps
                // between sides and top and bottom planes
                float z = ray.GetPoint(tA).z;
                if (z < aabb.min.z || z > aabb.max.z)
                    hit = false;
            }

            if (hit)
            {
                intersectDistArray[intersects++] = tA * rayLength;
                if (intersects == maxIntersections) break;
            }
            ++idx;
        }

        // Test top and bottom sides of shape for intersection
        if (testTopBottom)
        {
            Vec3 pt; Vec3 norm;

            // test top
            norm = new Vec3(0.0f, 0.0f, (end.z > start.z) ? -1.0f : 1.0f); // because plane test is one sided
            if (Intersect.Line_Plane(new Line(start, end), Plane.CreatePlane(norm, aabb.max), out pt) &&
                IsPointInsideShape(pt, false) &&
                intersects < maxIntersections)
                intersectDistArray[intersects++] = (pt - start).Length();

            // test bottom
            norm = new Vec3(norm.x, norm.y, norm.z * -1);
            if (Intersect.Line_Plane(new Line(start, end), Plane.CreatePlane(norm, aabb.min), out pt) &&
                IsPointInsideShape(pt, false) &&
                intersects < maxIntersections)
                intersectDistArray[intersects++] = (pt - start).Length();
        }

        Array.Sort(intersectDistArray, 0, intersects);

        return intersects;
    }

    //===================================================================
    // GetPointAlongPath
    //===================================================================
    public Vec3 GetPointAlongPath(float dist)
    {
        if (shape.Count == 0)
            return Vec3.Zero;

        if (dist < 0.0f)
            return shape[0];

        int cur = 0;
        int next = 1;

        float d = 0.0f;
        while (next < shape.Count)
        {
            Vec3 delta = shape[next] - shape[cur];
            float len = delta.Length();
            if (len > 0.0f && dist >= d && dist < d + len)
            {
                float t = (dist - d) / len;
                return shape[cur] + delta * t;
            }
            d += len;
            cur = next;
            ++next;
        }
        return shape[cur];
    }

    //===================================================================
    // IsPointInsideShape
    //===================================================================
    public bool IsPointInsideShape(Vec3 pos, bool checkHeight)
    {
        // Check height.
        if (checkHeight && height > 0.01f)
        {
            float h = pos.z - aabb.min.z;
            if (h < 0.0f || h > height)
                return false;
        }
        // Is the request point inside the shape.
        if (Overlap.Point_Polygon2D(pos, shape, aabb))
            return true;
        return false;
    }

    //===================================================================
    // ConstrainPointInsideShape
    //===================================================================
    public bool ConstrainPointInsideShape(ref Vec3 pos, bool checkHeight)
    {
        if (!IsPointInsideShape(pos, checkHeight))
        {
            // Target is outside the territory, find closest point on the edge of the territory.
            float dist = 0;
            Vec3 nearest;
            NearestPointOnPath(pos, false, out dist, out nearest);
            // adjust height.
            nearest = new Vec3(nearest.x, nearest.y, pos.z);
            if (checkHeight && height > 0.00001f)
                nearest = new Vec3(nearest.x, nearest.y, clamp_tpl(nearest.z, aabb.min.z, aabb.min.z + height));
            pos = nearest;
            return true;
        }
        return false;
    }

    //===================================================================
    // MemStats
    //===================================================================
    public nuint MemStats()
    {
        nuint size = (nuint)128; // approximate sizeof(*this)
        size += (nuint)(shape.Count * 12); // sizeof(Vec3)
        if (shapeMask != null)
            size += shapeMask.MemStats();
        return size;
    }

    public ListPositions shape;
    public AABB aabb;
    public IAISystem_ENavigationType navType;
    public int type;
    public float height;
    public float devalueTime;
    public bool temporary;
    public bool enabled;
    public EAILightLevel lightLevel;
    public bool closed;

    // ShapeMaskPtr — wrapper for CShapeMask*. C# uses direct ref.
    public CShapeMask shapeMask;
}

// typedef std::map<string, SShape> ShapeMap;
public class ShapeMap : SortedDictionary<string, SShape> { }

public class SPerceptionModifierShape : SShape
{
    public SPerceptionModifierShape(ListPositions shapeIn, float reductionPerMetre, float reductionMax, float fHeight, bool isClosed)
        : base(shapeIn)
    {
        height = fHeight;
        closed = isClosed;
        fReductionPerMetre = clamp_tpl(reductionPerMetre, 0.0f, 1.0f);
        fReductionMax = clamp_tpl(reductionMax, 0.0f, 1.0f);

        // Recalc AABB
        aabb.Reset();
        foreach (Vec3 v in shape)
            aabb.Add(v);
        aabb.max = new Vec3(aabb.max.x, aabb.max.y, aabb.min.z + height);
    }

    public float fReductionPerMetre;
    public float fReductionMax;
}

// typedef std::map<string, SPerceptionModifierShape> PerceptionModifierShapeMap;
public class PerceptionModifierShapeMap : SortedDictionary<string, SPerceptionModifierShape> { }
