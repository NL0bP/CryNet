// Literal port of dev/Code/CryEngine/CryAISystem/Shape.h + Shape.cpp (863L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Polygon shapes used for different purposes in the AI system and various shape containers.
//
//               The bin sort point-in-polygon check is based on:
//               Graphics Gems IV: Point in Polygon Strategies by Eric Haines
//               http://tog.acm.org/GraphicsGems/gemsiv/ptpoly_haines/

using System.Collections.Generic;
using static CryAISystem.CryMath;
using CryAISystem.CryCommon;

namespace CryAISystem;

// typedef std::vector<Vec3> ShapePointContainer;
public class ShapePointContainer : List<Vec3> { }

public static class ShapeHelpers
{
    //====================================================================
    // DistancePointLinesegSq
    //====================================================================
    public static float DistancePointLinesegSq(Vec3 p, Vec3 start, Vec3 end, out float t)
    {
        Vec2 diff = new Vec2(p.x - start.x, p.y - start.y);
        Vec2 dir = new Vec2(end.x - start.x, end.y - start.y);
        t = diff.Dot(dir);

        if (t <= 0.0f)
        {
            t = 0.0f;
        }
        else
        {
            float sqrLen = dir.GetLength2();
            if (t >= sqrLen)
            {
                t = 1.0f;
                diff = diff - dir;
            }
            else
            {
                t /= sqrLen;
                diff = diff - dir * t;
            }
        }

        return diff.GetLength2();
    }

    //====================================================================
    // IntersectLinesegLineseg (internal helper used by CAIShape)
    //====================================================================
    internal static bool IntersectLinesegLineseg(Vec3 startA, Vec3 endA,
                                                  Vec3 startB, Vec3 endB,
                                                  out float tA, out float tB)
    {
        // used for parallel testing
        const float epsilon = 0.0000001f;

        Vec2 delta = new Vec2(startB.x - startA.x, startB.y - startA.y);
        Vec2 lineADir = new Vec2(endA.x - startA.x, endA.y - startA.y);
        Vec2 lineBDir = new Vec2(endB.x - startB.x, endB.y - startB.y);
        float crossD = lineADir.x * lineBDir.y - lineADir.y * lineBDir.x;
        float crossDelta1 = delta.x * lineBDir.y - delta.y * lineBDir.x;
        float crossDelta2 = delta.x * lineADir.y - delta.y * lineADir.x;

        if (fabs(crossD) > epsilon)
        {
            // intersection
            tA = crossDelta1 / crossD;
            tB = crossDelta2 / crossD;
        }
        else
        {
            // parallel - maybe should really test for lines overlapping each other?
            tA = tB = 0.5f;
            return false;
        }

        if (tA > 1.0f || tA < 0.0f || tB > 1.0f || tB < 0.0f)
            return false;

        return true;
    }
}

public class CAIShape
{
    private const float BIN_WIDTH = 3.0f;

    //====================================================================
    // CAIShape
    //====================================================================
    public CAIShape()
    {
        m_binSort = null;
        m_aabb = new AABB(AABB.RESET);
    }

    //====================================================================
    // DuplicateData
    //====================================================================
    public CAIShape DuplicateData()
    {
        CAIShape pShape = new CAIShape();
        pShape.m_name = m_name;
        pShape.m_aabb = m_aabb;
        pShape.m_points = new ShapePointContainer();
        pShape.m_points.AddRange(m_points);
        return pShape;
    }

    public void SetName(string name) { m_name = name; }
    public string GetName() { return m_name; }

    //====================================================================
    // SetPoints
    //====================================================================
    public void SetPoints(List<Vec3> points)
    {
        m_points.Clear();
        m_points.Capacity = points.Count;
        for (int i = 0, ni = points.Count; i < ni; ++i)
            m_points.Add(points[i]);
        BuildAABB();
    }

    //====================================================================
    // BuildAABB
    //====================================================================
    public void BuildAABB()
    {
        // Build AABB
        m_aabb.Reset();
        for (int i = 0, ni = m_points.Count; i < ni; ++i)
            m_aabb.Add(m_points[i]);

        // add a little to the bounds to ensure everything falls inside area
        const float EPSILON = 0.00001f;
        float rangex = m_aabb.max.x - m_aabb.min.x;
        float rangey = m_aabb.max.y - m_aabb.min.y;
        m_aabb.min = new Vec3(m_aabb.min.x - EPSILON * rangex, m_aabb.min.y - EPSILON * rangey, m_aabb.min.z);
        m_aabb.max = new Vec3(m_aabb.max.x + EPSILON * rangex, m_aabb.max.y + EPSILON * rangey, m_aabb.max.z);
    }

    //====================================================================
    // BuildBins
    //====================================================================
    public void BuildBins()
    {
        m_binSort = null;

        // Do not build the bins if too few points.
        if (m_points.Count < 15)
            return;

        float minxVal = m_aabb.min.x;
        float maxxVal = m_aabb.max.x;
        float miny = m_aabb.min.y;
        float maxy = m_aabb.max.y;

        int bins = (int)ceilf((maxy - miny) / BIN_WIDTH);
        if (bins < 2)
            return;

        // Limit the size of bins so that the acceleration structure does not take more memory than the points.
        if (bins > m_points.Count / 2)
            bins = m_points.Count / 2;

        m_binSort = new BinSort();

        for (int idx = 0; idx < bins; idx++)
            m_binSort.bins.Add(new Bin());

        m_binSort.ydelta = (maxy - miny) / (float)bins;
        m_binSort.invYdelta = 1.0f / m_binSort.ydelta;

        // find how many locations to allocate for each bin
        int[] binTot = new int[bins];

        Vec3 p0 = m_points[m_points.Count - 1];
        for (int i = 0, ni = m_points.Count; i < ni; ++i)
        {
            Vec3 p1 = m_points[i];

            // skip if Y's identical (edge has no effect)
            if (p0.y != p1.y)
            {
                Vec3 pa = p0;
                Vec3 pb = p1;
                if (pa.y > pb.y)
                { Vec3 tmp = pa; pa = pb; pb = tmp; }

                float fba = (pa.y - miny) * m_binSort.invYdelta;
                int ba = (int)fba;
                float fbb = (pb.y - miny) * m_binSort.invYdelta;
                int bb = (int)fbb;
                // if high vertex ends on a boundary, don't go into next boundary
                if (fbb == (float)bb)
                    bb--;

                if (ba < 0) ba = 0;
                if (bb >= bins) bb = bins - 1;

                // mark the bins with this edge
                for (int j = ba; j <= bb; j++)
                    binTot[j]++;
            }

            p0 = p1;
        }

        // allocate the bin contents and fill in some basics
        for (int i = 0, ni = m_binSort.bins.Count; i < ni; ++i)
        {
            Bin bin = m_binSort.bins[i];
            bin.edges.Capacity = binTot[i];
            // start these off at some awful values; refined below
            bin.minx = maxxVal;
            bin.maxx = minxVal;
        }

        // now go through list yet again, putting edges in bins
        p0 = m_points[m_points.Count - 1];
        ushort id = (ushort)(m_points.Count - 1);
        for (int i = 0, ni = m_points.Count; i < ni; ++i)
        {
            Vec3 p1 = m_points[i];

            // skip if Y's identical (edge has no effect)
            if (p0.y != p1.y)
            {
                Vec3 pa = p0;
                Vec3 pb = p1;
                if (pa.y > pb.y)
                { Vec3 tmp = pa; pa = pb; pb = tmp; }

                float fba = (pa.y - miny) * m_binSort.invYdelta;
                int ba = (int)fba;
                float fbb = (pb.y - miny) * m_binSort.invYdelta;
                int bb = (int)fbb;
                // if high vertex ends on a boundary, don't go into it
                if (bb > ba && fbb == (float)bb)
                    bb--;

                if (ba < 0) ba = 0;
                if (bb >= bins) bb = bins - 1;

                float vx0 = pa.x;
                float dy = pb.y - pa.y;
                float slope = m_binSort.ydelta * (pb.x - pa.x) / dy;

                // set vx1 in case loop is not entered
                float vx1 = vx0;
                bool fullCross = false;
                for (int j = ba; j < bb; j++, vx0 = vx1)
                {
                    // could increment vx1, but for greater accuracy recompute it
                    vx1 = pa.x + ((float)(j + 1) - fba) * slope;
                    m_binSort.bins[j].AddEdge(vx0, vx1, id, fullCross);
                    fullCross = true;
                }

                // at last bin - fill as above, but with vx1 = p1->x
                vx0 = vx1;
                vx1 = pb.x;
                m_binSort.bins[bb].AddEdge(vx0, vx1, id, false); // the last bin is never a full crossing
            }

            id = (ushort)i;
            p0 = p1;
        }

        // finally, sort the bins' contents by minx
        for (int i = 0, ni = m_binSort.bins.Count; i < ni; ++i)
            m_binSort.bins[i].edges.Sort((a, b) => a.minx.CompareTo(b.minx));
    }

    //====================================================================
    // IsPointInsideSlow
    //====================================================================
    private bool IsPointInsideSlow(Vec3 pt)
    {
        return Overlap.Point_Polygon2D(pt, m_points, m_aabb);
    }

    //====================================================================
    // IsPointInside
    //====================================================================
    public bool IsPointInside(Vec3 pt)
    {
        if (m_binSort == null)
        {
            return IsPointInsideSlow(pt);
        }

        bool insideFlag = false;

        // what bin are we in?
        int b = (int)((pt.y - m_aabb.min.y) * m_binSort.invYdelta);

        // Outside the bin range, outside the shape.
        if (b < 0 || b >= m_binSort.bins.Count)
            return false;

        Bin bin = m_binSort.bins[b];
        // find if we're inside this bin's bounds
        if (pt.x < bin.minx || pt.x > bin.maxx)
            return false;

        // now search bin for crossings
        int npts = m_points.Count;
        for (int j = 0, nj = bin.edges.Count; j < nj; ++j)
        {
            Edge edge = bin.edges[j];
            if (pt.x < edge.minx)
            {
                // all remaining edges are to right of point, so test them
                do
                {
                    if (edge.fullCross)
                    {
                        insideFlag = !insideFlag;
                    }
                    else
                    {
                        int eid = edge.id;
                        if ((pt.y <= m_points[eid].y) != (pt.y <= m_points[(eid + 1) % npts].y))
                        {
                            // point crosses edge in Y, so must cross.
                            insideFlag = !insideFlag;
                        }
                    }
                    j++;
                    if (j < nj)
                        edge = bin.edges[j];
                }
                while (j < nj);

                return insideFlag;
            }
            else if (pt.x < edge.maxx)
            {
                // edge is overlapping point in X, check it
                int eid = edge.id;
                Vec3 vtx0 = m_points[eid];
                Vec3 vtx1 = m_points[(eid + 1) % npts];

                if (edge.fullCross || (pt.y <= vtx0.y) != (pt.y <= vtx1.y))
                {
                    // edge crosses in Y, so have to do full crossings test
                    if ((vtx0.x - (vtx0.y - pt.y) * (vtx1.x - vtx0.x) / (vtx1.y - vtx0.y)) >= pt.x)
                        insideFlag = !insideFlag;
                }
            } // else edge is to left of point, ignore it
        }

        return insideFlag;
    }

    //====================================================================
    // IsPointOnEdgeSlow
    //====================================================================
    private bool IsPointOnEdgeSlow(Vec3 pt, float tol, out Vec3 outNormal)
    {
        outNormal = new Vec3(0, 0, 0);
        float tolSq = sqr(tol);

        int j2 = m_points.Count - 1;
        for (int i = 0, ni = m_points.Count; i < ni; j2 = i, ++i)
        {
            Vec3 sa = m_points[j2];
            Vec3 sb = m_points[i];
            float t;
            if (ShapeHelpers.DistancePointLinesegSq(pt, sa, sb, out t) < tolSq)
            {
                Vec3 polySeg = sb - sa;
                Vec3 intersectionPoint = sa + polySeg * t;
                Vec3 intSeg = (intersectionPoint - pt);

                Vec3 normal = new Vec3(polySeg.y, -polySeg.x, 0.0f);
                normal = normal.GetNormalized();
                // returns the normal towards the start point of the intersecting segment
                if ((intSeg.Dot(normal)) > 0.0f)
                {
                    normal = new Vec3(-normal.x, -normal.y, normal.z);
                }

                outNormal = normal;
                return true;
            }
        }

        return false;
    }

    //====================================================================
    // IsPointOnEdge
    //====================================================================
    public bool IsPointOnEdge(Vec3 pt, float tol, out Vec3 outNormal)
    {
        outNormal = new Vec3(0, 0, 0);

        if (!Overlap.Sphere_AABB2D(new Sphere(pt, tol), m_aabb))
            return false;

        if (m_binSort == null)
        {
            return IsPointOnEdgeSlow(pt, tol, out outNormal);
        }

        int binCount = m_binSort.bins.Count;

        // Calculate x and y ranges.
        float xrmin = pt.x - tol;
        float xrmax = pt.x + tol;
        float yrmin = pt.y - tol;
        float yrmax = pt.y + tol;

        // Check all bins that overlap the circle.
        float fba = (yrmin - m_aabb.min.y) * m_binSort.invYdelta;
        int ba = (int)fba;
        float fbb = (yrmax - m_aabb.min.y) * m_binSort.invYdelta;
        int bb = (int)fbb;
        // if high vertex ends on a boundary, don't go into it
        if (bb > ba && fbb == (float)bb)
            bb--;

        // Sanity check for index ranges.
        if (ba < 0) ba = 0;
        if (bb >= binCount) bb = binCount - 1;

        float tolSq = sqr(tol);

        for (int i = ba; i <= bb; i++)
        {
            Bin binRef = m_binSort.bins[i];

            // Check the bins.
            int npts = m_points.Count;
            for (int j = 0, nj = binRef.edges.Count; j < nj; ++j)
            {
                Edge edge = binRef.edges[j];
                // Skip if the X range does not overlap.
                if (xrmin > edge.maxx || xrmax < edge.minx)
                    continue;

                int eid = edge.id;
                Vec3 sa = m_points[eid];
                Vec3 sb = m_points[(eid + 1) % npts];

                float t;
                if (ShapeHelpers.DistancePointLinesegSq(pt, sa, sb, out t) < tolSq)
                {
                    Vec3 polySeg = sb - sa;
                    Vec3 intersectionPoint = sa + polySeg * t;
                    Vec3 intSeg = (intersectionPoint - pt);

                    Vec3 normal = new Vec3(polySeg.y, -polySeg.x, 0.0f);
                    normal = normal.GetNormalized();
                    // returns the normal towards the start point of the intersecting segment
                    if ((intSeg.Dot(normal)) > 0.0f)
                    {
                        normal = new Vec3(-normal.x, -normal.y, normal.z);
                    }

                    outNormal = normal;
                    return true;
                }
            }
        }

        return false;
    }

    //====================================================================
    // IntersectLineSegSlow
    //====================================================================
    private bool IntersectLineSegSlow(Vec3 start, Vec3 end, ref float tmin,
                                       out Vec3 outClosestPoint, out Vec3 outNormal, bool bForceNormalOutwards = false)
    {
        Vec3 isa = default;
        Vec3 isb = default;
        bool isaSet = false;

        outClosestPoint = new Vec3(0, 0, 0);
        outNormal = new Vec3(0, 0, 0);

        tmin = 1.0f;
        bool intersect = false;

        int pointCount = m_points.Count;

        for (int i = 1; i <= pointCount; ++i)
        {
            Vec3 sa = m_points[i - 1];
            Vec3 sb = m_points[i % pointCount];

            float s, t;
            if (ShapeHelpers.IntersectLinesegLineseg(start, end, sa, sb, out s, out t))
            {
                if (s < 0.00001f || s > 0.99999f || t < 0.00001f || t > 0.99999f)
                    continue;

                if (s < tmin)
                {
                    tmin = s;
                    intersect = true;
                    isa = sa;
                    isb = sb;
                    isaSet = true;
                }
            }
        }

        if (intersect)
            outClosestPoint = start + (end - start) * tmin;

        if (intersect && isaSet)
        {
            Vec3 polyseg = isb - isa;
            Vec3 intSeg = end - start;
            outNormal = new Vec3(polyseg.y, -polyseg.x, 0);
            outNormal = outNormal.GetNormalized();
            // returns the normal towards the start point of the intersecting segment (if it's not forced to be outwards)
            if (!bForceNormalOutwards && intSeg.Dot(outNormal) > 0)
            {
                outNormal = new Vec3(-outNormal.x, -outNormal.y, outNormal.z);
            }
        }

        return intersect;
    }

    //====================================================================
    // IntersectLineSegBin
    //====================================================================
    private bool IntersectLineSegBin(Bin bin, float xrmin, float xrmax,
                                      Vec3 start, Vec3 end,
                                      ref float tmin, ref Vec3 isa, ref Vec3 isb, ref bool isaSet)
    {
        if (xrmin > xrmax)
        { float tmp = xrmin; xrmin = xrmax; xrmax = tmp; }

        const float EPSILON = 0.00001f;
        xrmin -= EPSILON;
        xrmax += EPSILON;

        bool intersect = false;

        // Check the bins.
        int npts = m_points.Count;
        for (int j = 0, nj = bin.edges.Count; j < nj; ++j)
        {
            Edge edge = bin.edges[j];
            // Skip if the X range does not overlap.
            if (xrmin > edge.maxx || xrmax < edge.minx)
                continue;

            int eid = edge.id;
            Vec3 sa = m_points[eid];
            Vec3 sb = m_points[(eid + 1) % npts];

            float s, t;
            if (ShapeHelpers.IntersectLinesegLineseg(start, end, sa, sb, out s, out t))
            {
                if (s < 0.00001f || s > 0.99999f || t < 0.00001f || t > 0.99999f)
                    continue;
                if (s < tmin)
                {
                    tmin = s;
                    intersect = true;
                    isa = sa;
                    isb = sb;
                    isaSet = true;
                }
            }
        }

        return intersect;
    }

    //====================================================================
    // IntersectLineSeg (full version)
    //====================================================================
    public bool IntersectLineSeg(Vec3 start, Vec3 end, out float tmin,
                                 out Vec3 outClosestPoint, out Vec3 outNormal, bool bForceNormalOutwards = false)
    {
        tmin = 1.0f;
        outClosestPoint = new Vec3(0, 0, 0);
        outNormal = new Vec3(0, 0, 0);

        if (!Overlap.Lineseg_AABB2D(new Lineseg(start, end), m_aabb))
            return false;

        if (m_binSort == null)
        {
            return IntersectLineSegSlow(start, end, ref tmin, out outClosestPoint, out outNormal, bForceNormalOutwards);
        }

        int binCount = m_binSort.bins.Count;

        Vec3 isa = default;
        Vec3 isb = default;
        bool isaSet = false;

        bool intersect = false;

        Vec3 pa = start;
        Vec3 pb = end;

        // skip if Y's identical (edge has no effect)
        if (pa.y > pb.y)
        { Vec3 tmp = pa; pa = pb; pb = tmp; }

        float fba = (pa.y - m_aabb.min.y) * m_binSort.invYdelta;
        int ba = (int)fba;
        float fbb = (pb.y - m_aabb.min.y) * m_binSort.invYdelta;
        int bb = (int)fbb;
        // if high vertex ends on a boundary, don't go into it
        if (bb > ba && fbb == (float)bb)
            bb--;

        if (ba < 0) ba = 0;
        if (bb >= binCount) bb = binCount - 1;

        float vx0 = pa.x;
        float dy = pb.y - pa.y;
        float slope = dy < 0.000001f ? 0.0f : m_binSort.ydelta * (pb.x - pa.x) / dy;

        // set vx1 in case loop is not entered
        float vx1 = vx0;
        for (int i = ba; i < bb; i++, vx0 = vx1)
        {
            Bin binRef = m_binSort.bins[i];

            // could increment vx1, but for greater accuracy recompute it
            vx1 = pa.x + ((float)(i + 1) - fba) * slope;

            if (IntersectLineSegBin(binRef, vx0, vx1, start, end, ref tmin, ref isa, ref isb, ref isaSet))
                intersect = true;
        }

        // at last bin - fill as above, but with vx1 = p1->x
        if (IntersectLineSegBin(m_binSort.bins[bb], vx1, pb.x, start, end, ref tmin, ref isa, ref isb, ref isaSet))
            intersect = true;

        if (intersect)
            outClosestPoint = start + (end - start) * tmin;

        if (intersect && isaSet)
        {
            Vec3 polyseg = isb - isa;
            outNormal = new Vec3(polyseg.y, -polyseg.x, 0);
            outNormal = outNormal.GetNormalized();
        }

        return intersect;
    }

    //====================================================================
    // IntersectLineSeg (radius version)
    //====================================================================
    public bool IntersectLineSeg(Vec3 start, Vec3 end, float radius)
    {
        AABB aabb = m_aabb;
        aabb.Expand(new Vec3(radius, radius, radius));

        Lineseg line = new Lineseg(start, end);

        if (!Overlap.Lineseg_AABB2D(line, aabb))
            return false;

        int pointCount = m_points.Count;
        float radiusSq = sqr(radius);

        for (int i = 1; i <= pointCount; ++i)
        {
            float distanceSq = Distance.Lineseg_Lineseg2DSq<float>(line, new Lineseg(m_points[i - 1], m_points[i % pointCount]));

            if (distanceSq <= radiusSq)
                return true;
        }

        return false;
    }

    //====================================================================
    // OverlapAABB
    //====================================================================
    public bool OverlapAABB(AABB aabb)
    {
        if (!Overlap.AABB_AABB2D(aabb, m_aabb))
            return false;

        ShapePointContainer pts = m_points;
        int npts = m_points.Count;

        // Trivial reject if all points are outside any of the edges of the AABB.
        int cxmin = 0, cxmax = 0, cymin = 0, cymax = 0;
        for (int i = 0; i < npts; ++i)
        {
            Vec3 v = pts[i];
            int inside = 0;
            if (v.x < aabb.min.x)
                cxmin++;
            else if (v.x > aabb.max.x)
                cxmax++;
            else
                inside++;

            if (v.y < aabb.min.y)
                cymin++;
            else if (v.y > aabb.max.y)
                cymax++;
            else
                inside++;

            // The vertex is inside the AABB, there is be overlap.
            if (inside == 2)
                return true;
        }
        if (cxmin == npts || cxmax == npts || cymin == npts || cymax == npts)
            return false;

        // If any edge intersects the aabb, return true.
        for (int i = 0; i < npts; ++i)
        {
            int ine = i + 1;
            if (ine >= npts) ine = 0;
            if (AICollision.OverlapLinesegAABB2D(pts[i], pts[ine], aabb))
                return true;
        }

        // If any aabb edge is inside the poly, return true.
        if (IsPointInside(new Vec3(aabb.min.x, aabb.min.y, 0)))
            return true;
        if (IsPointInside(new Vec3(aabb.max.x, aabb.min.y, 0)))
            return true;
        if (IsPointInside(new Vec3(aabb.max.x, aabb.max.y, 0)))
            return true;
        if (IsPointInside(new Vec3(aabb.min.x, aabb.max.y, 0)))
            return true;

        return false;
    }

    public ShapePointContainer GetPoints() { return m_points; }
    public AABB GetAABB() { return m_aabb; }

    //====================================================================
    // DebugDraw
    //====================================================================
    public void DebugDraw()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        AABB bounds = m_aabb;

        // Draw shape
        dc.DrawPolyline(m_points, m_points.Count, true, new ColorB(255, 255, 255), 3.0f);

        dc.DrawAABB(bounds, false, new ColorB(255, 255, 255, 128), EBoundingBoxDrawStyle.eBBD_Faceted);

        // Draw bins
        if (m_binSort != null)
        {
            AABB aabb2 = new AABB();
            int npts = m_points.Count;
            for (int i = 0, ni = m_binSort.bins.Count; i < ni; ++i)
            {
                Bin binRef = m_binSort.bins[i];

                float y = bounds.min.y + i * m_binSort.ydelta;
                dc.DrawLine(new Vec3(binRef.minx, y, bounds.min.z), new ColorB(255, 0, 0, 128),
                    new Vec3(binRef.maxx, y, bounds.min.z), new ColorB(255, 0, 0, 128));

                aabb2.min = new Vec3(aabb2.min.x, y, aabb2.min.z);
                aabb2.max = new Vec3(aabb2.max.x, y + m_binSort.ydelta, aabb2.max.z);

                // Draw edge aabbs
                for (int j = 0, nj = binRef.edges.Count; j < nj; ++j)
                {
                    Edge e = binRef.edges[j];
                    Vec3 va = m_points[e.id];
                    Vec3 vb = m_points[(e.id + 1) % npts];

                    aabb2.min = new Vec3(e.minx, aabb2.min.y, min(va.z, vb.z));
                    aabb2.max = new Vec3(e.maxx, aabb2.max.y, max(va.z, vb.z));

                    dc.DrawAABB(aabb2, true, new ColorB(255, 0, 0, 128), EBoundingBoxDrawStyle.eBBD_Faceted);
                }
            }
        }
    }

    //====================================================================
    // GetDrawZ
    //====================================================================
    private float GetDrawZ(float x, float y)
    {
        I3DEngine pEngine = gEnv.p3DEngine;
        float terrainZ = pEngine.GetTerrainElevation(x, y);
        Vec3 pt2 = new Vec3(x, y, 0);
        float waterZ = pEngine.GetWaterLevel(pt2);
        return max(terrainZ, waterZ);
    }

    //====================================================================
    // MemStats
    //====================================================================
    public nuint MemStats()
    {
        nuint size = (nuint)128; // approximate sizeof(*this)
        size += (nuint)(m_points.Capacity * 12); // sizeof(Vec3) ~ 12
        if (m_binSort != null)
        {
            size += (nuint)16; // approximate sizeof(*m_binSort)
            size += (nuint)(m_binSort.bins.Capacity * 16); // approximate sizeof(Bin)
            for (int i = 0, ni = m_binSort.bins.Count; i < ni; ++i)
                size += (nuint)(m_binSort.bins[i].edges.Capacity * 12); // approximate sizeof(Edge)
        }
        return size;
    }

    private struct Edge
    {
        public Edge(ushort id, float minx, float maxx, bool fullCross) { this.id = id; this.fullCross = fullCross; this.minx = minx; this.maxx = maxx; }
        public float minx, maxx;
        public ushort id;
        public bool fullCross;
    }

    private class Bin
    {
        public void AddEdge(float vx0, float vx1, ushort id, bool fullCross)
        {
            if (vx0 > vx1)
            {
                float tmp = vx0; vx0 = vx1; vx1 = tmp;
            }
            // Update bin range
            if (minx > vx0)
                minx = vx0;
            if (maxx < vx1)
                maxx = vx1;
            // Insert edge
            edges.Add(new Edge(id, vx0, vx1, fullCross));
        }

        public float minx, maxx;
        public List<Edge> edges = new List<Edge>();
    }

    // Holds the bin sort acceleration structure for the polygon.
    private class BinSort
    {
        public List<Bin> bins = new List<Bin>();
        public float ydelta, invYdelta;
    }

    private BinSort m_binSort;
    private ShapePointContainer m_points = new ShapePointContainer();
    private AABB m_aabb;
    private string m_name = "";
}
