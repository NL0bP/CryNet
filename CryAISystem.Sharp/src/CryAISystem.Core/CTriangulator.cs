// Literal port of dev/Code/CryEngine/CryAISystem/CTriangulator.h + CTriangulator.cpp (120L + 462L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CryAISystem.CryMath;

namespace CryAISystem;

// real = double on Windows (CryCommon platform headers)
// Vec3r = Vec3_tpl<real> = double-precision Vec3; we use Vec3 with implicit double operations

public struct Vtx
{
    public double x, y, z;
    public List<int> m_lstTris; // triangles that contain this point
    public bool bCollidable;
    public bool bHideable;

    public Vtx(double x = 0.0, double y = 0.0, double z = 0.0)
    {
        this.x = x; this.y = y; this.z = z;
        bCollidable = true; bHideable = false;
        m_lstTris = new List<int>();
    }

    public Vtx(Vec3 v)
    {
        x = v.x; y = v.y; z = v.z;
        bCollidable = true; bHideable = false;
        m_lstTris = new List<int>();
    }

    public bool EqualsXY(Vtx other)
    {
        return (Math.Abs(x - other.x) < 0.001) && (Math.Abs(y - other.y) < 0.001);
    }
}

public class Tri
{
    public int[] v = new int[3];
    public Vtx center;
    public double radiusSq;

    public uint graphNodeIndex;

    public Tri(int v0 = 0, int v1 = 0, int v2 = 0)
    {
        v[0] = v0;
        v[1] = v1;
        v[2] = v2;
        radiusSq = 0.0;
        center = new Vtx(0, 0, 0);
        graphNodeIndex = 0;
    }
}

public struct SPOINT { public int x, y; }

public struct MYPOINT : IEquatable<MYPOINT>
{
    public int x, y;
    public bool Equals(MYPOINT other) { return this.x == other.x && this.y == other.y; }
    public override bool Equals(object obj) => obj is MYPOINT m && Equals(m);
    public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 16);
}

// typedef std::list<Tri*> TARRAY;
// typedef std::vector<Vtx> VARRAY;
public class TARRAY : LinkedList<Tri> { }
public class VARRAY : List<Vtx> { }

public class CTriangulator
{
    // line segment
    private struct SSegment
    {
        public Vec3 m_p1;
        public Vec3 m_p2;
        public SSegment(Vec3 p1, Vec3 p2) { m_p1 = p1; m_p2 = p2; }
    }

    private VARRAY m_vProcessed = new VARRAY();
    private TARRAY m_vTriangles = new TARRAY();

    private List<MYPOINT> m_uniquePts = new List<MYPOINT>();

    private List<SSegment> m_cuts = new List<SSegment>();
    private int m_curCutIdx;

    public VARRAY m_vVertices = new VARRAY();
    public Vtx m_vtxBBoxMin;
    public Vtx m_vtxBBoxMax;

    //====================================================================
    // CTriangulator
    //====================================================================
    public CTriangulator()
    {
        m_vtxBBoxMin.x = 3000;
        m_vtxBBoxMin.y = 3000;
        m_vtxBBoxMin.z = 0;

        m_vtxBBoxMax.x = 0;
        m_vtxBBoxMax.y = 0;
        m_vtxBBoxMax.z = 0;
    }

    //====================================================================
    // DoesVertexExist2D
    //====================================================================
    public bool DoesVertexExist2D(double x, double y, double tol)
    {
        for (int i = 0; i < m_vVertices.Count; ++i)
        {
            Vtx v = m_vVertices[i];
            if ((Math.Abs(x - v.x) < tol) && (Math.Abs(y - v.y) < tol))
                return true;
        }
        return false;
    }

    //====================================================================
    // AddVertex
    //====================================================================
    public int AddVertex(double x, double y, double z, bool bCollidable, bool bHideable)
    {
        Vtx newvertex = new Vtx();
        newvertex.x = x;
        newvertex.y = y;
        newvertex.z = z;
        newvertex.bCollidable = bCollidable;
        newvertex.bHideable = bHideable;
        newvertex.m_lstTris = new List<int>();

        if (newvertex.x < m_vtxBBoxMin.x)
            m_vtxBBoxMin.x = newvertex.x;
        if (newvertex.x > m_vtxBBoxMax.x)
            m_vtxBBoxMax.x = newvertex.x;

        if (newvertex.y < m_vtxBBoxMin.y)
            m_vtxBBoxMin.y = newvertex.y;
        if (newvertex.y > m_vtxBBoxMax.y)
            m_vtxBBoxMax.y = newvertex.y;

        for (int i = 0; i < m_vVertices.Count; ++i)
        {
            if (m_vVertices[i].EqualsXY(newvertex))
                return -1;
        }

        m_vVertices.Add(newvertex);

        return (m_vVertices.Count - 1);
    }

    //====================================================================
    // AddSegment
    //====================================================================
    public void AddSegment(Vec3 p1, Vec3 p2)
    {
        m_cuts.Add(new SSegment(p1, p2));
        m_curCutIdx = 0;
    }

    //====================================================================
    // GetSegment
    //====================================================================
    public bool GetSegment(ref Vec3 p1, ref Vec3 p2)
    {
        if (m_curCutIdx >= m_cuts.Count)
            return false;
        p1 = m_cuts[m_curCutIdx].m_p1;
        p2 = m_cuts[m_curCutIdx].m_p2;
        ++m_curCutIdx;
        return true;
    }

    //====================================================================
    // Triangulate
    //====================================================================
    public bool Triangulate()
    {
        if (m_vVertices.Count == 0) return true;

        // init supertriangle and structures
        if (!PrepForTriangulation())
            return false;

        // perform triangulation on any new vertices
        if (!TriangulateNew())
            return false;

        return true;
    }

    //====================================================================
    // GetVertices
    //====================================================================
    public VARRAY GetVertices()
    {
        return m_vProcessed;
    }

    //====================================================================
    // GetTriangles
    //====================================================================
    public TARRAY GetTriangles()
    {
        return m_vTriangles;
    }

    //====================================================================
    // PushUnique
    //====================================================================
    public void PushUnique(int a, int b)
    {
        MYPOINT newpoint, oldpoint;
        newpoint.x = a;
        newpoint.y = b;
        oldpoint.x = b;
        oldpoint.y = a;

        int idx = m_uniquePts.IndexOf(oldpoint);
        if (idx == -1)
            m_uniquePts.Add(newpoint);
        else
            m_uniquePts.RemoveAt(idx);
    }

    //====================================================================
    // IsAntiClockwise
    //====================================================================
    public bool IsAntiClockwise(Tri who)
    {
        Vtx v1, v2, v3;

        v1 = m_vProcessed[who.v[0]];
        v2 = m_vProcessed[who.v[1]];
        v3 = m_vProcessed[who.v[2]];

        double vec1x = v1.x - v2.x;
        double vec1y = v1.y - v2.y;

        double vec2x = v3.x - v2.x;
        double vec2y = v3.y - v2.y;

        double f = vec1x * vec2y - vec2x * vec1y;

        if (f > 0) return true;

        return false;
    }

    //====================================================================
    // CalcCircle
    //====================================================================
    public void CalcCircle(Vtx v1, Vtx v2, Vtx v3, Tri pTri)
    {
        double yDelta_a = v2.y - v1.y;
        double xDelta_a = v2.x - v1.x;
        double yDelta_b = v3.y - v2.y;
        double xDelta_b = v3.x - v2.x;

        if (Math.Abs(xDelta_a) <= 0.000000001 && Math.Abs(yDelta_b) <= 0.000000001)
        {
            pTri.center.x = 0.5 * (v2.x + v3.x);
            pTri.center.y = 0.5 * (v1.y + v2.y);
            pTri.center.z = v1.z;
            pTri.radiusSq = (pTri.center.x - v1.x) * (pTri.center.x - v1.x) + (pTri.center.y - v1.y) * (pTri.center.y - v1.y);
            return;
        }

        // IsPerpendicular() assure that xDelta(s) are not zero
        Debug.Assert(xDelta_a != 0.0);
        Debug.Assert(xDelta_b != 0.0);
        double aSlope = yDelta_a / xDelta_a;
        double bSlope = yDelta_b / xDelta_b;
        if (Math.Abs(aSlope - bSlope) <= 0.000000001)
        {
            AILog.AIError($"CTriangulator::CalcCircle vertices ({v1.x:F2},{v1.y:F2},{v1.z:F2}),({v2.x:F2},{v2.y:F2},{v2.z:F2}),({v3.x:F2},{v3.y:F2},{v3.z:F2}) caused problems [Code bug]");
            return;
        }

        // calc center
        Debug.Assert(2.0 * (bSlope - aSlope) != 0);
        pTri.center.x = (aSlope * bSlope * (v1.y - v3.y) + bSlope * (v1.x + v2.x) - aSlope * (v2.x + v3.x)) / (2.0 * (bSlope - aSlope));
        Debug.Assert(aSlope != 0);
        pTri.center.y = -(pTri.center.x - (v1.x + v2.x) / 2.0) / aSlope + (v1.y + v2.y) / 2.0;
        pTri.center.z = v1.z;

        pTri.radiusSq = (pTri.center.x - v1.x) * (pTri.center.x - v1.x) + (pTri.center.y - v1.y) * (pTri.center.y - v1.y);
    }

    //====================================================================
    // PrepForTriangulation
    //====================================================================
    public bool PrepForTriangulation()
    {
        m_vProcessed.Clear();
        // m_vProcessed.reserve(m_vVertices.size());

        // calculate super-triangle
        double minX = 100000, minY = 100000;
        double maxX = -100000, maxY = -100000;

        // bounding rectangle
        for (int i = 0; i < m_vVertices.Count; ++i)
        {
            Vtx current = m_vVertices[i];
            if (current.x < minX) minX = current.x;
            if (current.y < minY) minY = current.y;
            if (current.x > maxX) maxX = current.x;
            if (current.y > maxY) maxY = current.y;
        }

        // Add 4 corner points that are a little bit outside min/max
        double offsetVal = 10;

        Vtx v00 = new Vtx(minX - offsetVal, minY - offsetVal, 0);
        Vtx v11 = new Vtx(maxX + offsetVal, maxY + offsetVal, 0);
        Vtx v10 = new Vtx(v11.x, v00.y, 0);
        Vtx v01 = new Vtx(v00.x, v11.y, 0);

        m_vProcessed.Add(v00);
        m_vProcessed.Add(v10);
        m_vProcessed.Add(v11);
        m_vProcessed.Add(v01);

        Tri tri0 = new Tri(0, 1, 3);
        Tri tri1 = new Tri(1, 2, 3);

        m_vTriangles.AddLast(tri0);
        m_vTriangles.AddLast(tri1);

        if (!Calculate(tri0))
            return false;
        if (!Calculate(tri1))
            return false;

        return true;
    }

    //====================================================================
    // TriangulateNew
    //====================================================================
    public bool TriangulateNew()
    {
        int vertCounter = 0;
        // for every vertex
        for (int vi = 0; vi < m_vVertices.Count; ++vi, ++vertCounter)
        {
            Vtx current = m_vVertices[vi];

            if (0 == (vertCounter % 500))
            {
                AILog.AILogAlways($"Now on vertex {vertCounter} of {m_vVertices.Count} ({100.0f * vertCounter / m_vVertices.Count:F2} percent)");
            }

            m_uniquePts.Clear();
            // find enclosing circles
            var ti = m_vTriangles.First;

            while (ti != null)
            {
                var nextNode = ti.Next;
                Tri triangle = ti.Value;

                double distSq = (current.x - triangle.center.x) * (current.x - triangle.center.x) + (current.y - triangle.center.y) * (current.y - triangle.center.y);

                if (distSq <= triangle.radiusSq)
                {
                    PushUnique(triangle.v[0], triangle.v[1]);
                    PushUnique(triangle.v[1], triangle.v[2]);
                    PushUnique(triangle.v[2], triangle.v[0]);
                    m_vTriangles.Remove(ti);
                }

                ti = nextNode;
            }

            // add new triangles
            int pos = m_vProcessed.Count;
            m_vProcessed.Add(current);

            for (int ui = 0; ui < m_uniquePts.Count; ++ui)
            {
                MYPOINT curr = m_uniquePts[ui];

                Tri newone = new Tri();
                newone.v[0] = curr.x;
                newone.v[1] = curr.y;
                newone.v[2] = pos;

                if (!IsAntiClockwise(newone))
                {
                    newone.v[0] = curr.y;
                    newone.v[1] = curr.x;
                }

                if (!Calculate(newone))
                    return false;

                m_vTriangles.AddLast(newone);
            }
        }
        m_vVertices.Clear();

        return true;
    }

    //====================================================================
    // IsPerpendicular
    //====================================================================
    private bool IsPerpendicular(Vtx v1, Vtx v2, Vtx v3)
    {
        double yDelta_a = v2.y - v1.y;
        double xDelta_a = v2.x - v1.x;
        double yDelta_b = v3.y - v2.y;
        double xDelta_b = v3.x - v2.x;

        // checking whether the line of the two pts are vertical
        if (Math.Abs(xDelta_a) <= 0.000000001 && Math.Abs(yDelta_b) <= 0.000000001)
            return false;

        if (Math.Abs(yDelta_a) <= 0.0000001)
            return true;
        else if (Math.Abs(yDelta_b) <= 0.0000001)
            return true;
        else if (Math.Abs(xDelta_a) <= 0.000000001)
            return true;
        else if (Math.Abs(xDelta_b) <= 0.000000001)
            return true;
        else
            return false;
    }

    //====================================================================
    // Calculate
    //====================================================================
    private bool Calculate(Tri pTri)
    {
        Vtx v1 = m_vProcessed[pTri.v[0]];
        Vtx v2 = m_vProcessed[pTri.v[1]];
        Vtx v3 = m_vProcessed[pTri.v[2]];

        if (!IsPerpendicular(v1, v2, v3))
            CalcCircle(v1, v2, v3, pTri);
        else if (!IsPerpendicular(v1, v3, v2))
            CalcCircle(v1, v3, v2, pTri);
        else if (!IsPerpendicular(v2, v1, v3))
            CalcCircle(v2, v1, v3, pTri);
        else if (!IsPerpendicular(v2, v3, v1))
            CalcCircle(v2, v3, v1, pTri);
        else if (!IsPerpendicular(v3, v2, v1))
            CalcCircle(v3, v2, v1, pTri);
        else if (!IsPerpendicular(v3, v1, v2))
            CalcCircle(v3, v1, v2, pTri);
        else
        {
            // should not get here. However, we do sometimes... but by setting the radius to very
            // large this "triangle" can still get broken down and hopefully triangulated better.
            pTri.radiusSq = double.MaxValue;
            return true;
        }
        return true;
    }
}
