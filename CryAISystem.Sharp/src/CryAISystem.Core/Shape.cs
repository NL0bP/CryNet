// Literal port of dev/Code/CryEngine/CryAISystem/Shape.h
// Shape.cpp impl (863L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Polygon shapes used for different purposes in the AI system and various shape containers.

using System.Collections.Generic;

namespace CryAISystem;

// typedef std::vector<Vec3> ShapePointContainer;
public class ShapePointContainer : List<Vec3> { }

public static class ShapeHelpers
{
    public static float DistancePointLinesegSq(Vec3 p, Vec3 start, Vec3 end, out float t)
    {
        t = 0.0f; return 0; /* impl in .cpp */
    }
}

public class CAIShape
{
    public CAIShape() { /* impl in .cpp */ }
    // ~CAIShape();

    public CAIShape DuplicateData() { return null; /* impl in .cpp */ }

    public void SetName(string name) { m_name = name; }
    public string GetName() { return m_name; }

    public void SetPoints(List<Vec3> points) { /* impl in .cpp */ }
    public void BuildBins() { /* impl in .cpp */ }
    public void BuildAABB() { /* impl in .cpp */ }

    public bool IsPointInside(Vec3 pt) { return false; /* impl in .cpp */ }
    public bool IsPointOnEdge(Vec3 pt, float tol, out Vec3 outNormal) { outNormal = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public bool IntersectLineSeg(Vec3 start, Vec3 end, out float tmin, out Vec3 outClosestPoint, out Vec3 outNormal, bool bForceNormalOutwards = false)
    { tmin = 0; outClosestPoint = end; outNormal = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public bool IntersectLineSeg(Vec3 start, Vec3 end, float radius) { return false; /* impl in .cpp */ }

    public bool OverlapAABB(AABB aabb) { return false; /* impl in .cpp */ }

    public ShapePointContainer GetPoints() { return m_points; }
    public AABB GetAABB() { return m_aabb; }

    public void DebugDraw() { /* impl in .cpp */ }

    public nuint MemStats() { return 0; /* impl in .cpp */ }

    private struct Edge
    {
        public Edge(ushort id, float minx, float maxx, bool fullCross) { this.id = id; this.fullCross = fullCross; this.minx = minx; this.maxx = maxx; }
        public static bool operator <(Edge a, Edge b) { return a.minx < b.minx; }
        public static bool operator >(Edge a, Edge b) { return a.minx > b.minx; }
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

    private bool IsPointInsideSlow(Vec3 pt) { return false; /* impl in .cpp */ }
    private bool IsPointOnEdgeSlow(Vec3 pt, float tol, out Vec3 outNormal) { outNormal = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    private bool IntersectLineSegSlow(Vec3 start, Vec3 end, out float tmin, out Vec3 outClosestPoint, out Vec3 outNormal, bool bForceNormalOutwards = false)
    { tmin = 0; outClosestPoint = end; outNormal = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }

    private bool IntersectLineSegBin(Bin bin, float xrmin, float xrmax, Vec3 start, Vec3 end, out float tmin, out Vec3 isa, out Vec3 isb)
    { tmin = 0; isa = new Vec3(0, 0, 0); isb = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }

    private float GetDrawZ(float x, float y) { return 0; /* impl in .cpp */ }

    private BinSort m_binSort;
    private ShapePointContainer m_points = new ShapePointContainer();
    private AABB m_aabb;
    private string m_name = "";
}
