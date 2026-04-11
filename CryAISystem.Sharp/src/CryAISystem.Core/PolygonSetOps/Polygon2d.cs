// Literal port of dev/Code/CryEngine/CryAISystem/PolygonSetOps/Polygon2d.h (header only — .cpp pending Phase 3)
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.PolygonSetOps;

public class BspLineSegSplitter { }
public class BspTree2d { }

/**
 * @brief 2D polygon class with support for set operations and subsequent contour extraction.
 */
public class Polygon2d
{
    /**
     * Not really a stand-alone polygon edge class, vertices are only stored as
     * indices into the owning Polygon2d::m_vertices.
     */
    public class Edge : System.IComparable<Edge>
    {
        public Edge() : this(-1, -1) { }
        public Edge(int ind0, int ind1) { m_vertIndex0 = ind0; m_vertIndex1 = ind1; }

        public int CompareTo(Edge rhs)
        {
            if (m_vertIndex0 != rhs.m_vertIndex0) return m_vertIndex0 < rhs.m_vertIndex0 ? -1 : 1;
            if (m_vertIndex1 != rhs.m_vertIndex1) return m_vertIndex1 < rhs.m_vertIndex1 ? -1 : 1;
            return 0;
        }

        public static bool operator <(Edge a, Edge b) { return a.CompareTo(b) < 0; }
        public static bool operator >(Edge a, Edge b) { return a.CompareTo(b) > 0; }
        public static bool operator ==(Edge a, Edge b) { return a.m_vertIndex0 == b.m_vertIndex0 && a.m_vertIndex1 == b.m_vertIndex1; }
        public static bool operator !=(Edge a, Edge b) { return !(a == b); }
        public override bool Equals(object obj) { return obj is Edge e && this == e; }
        public override int GetHashCode() { return (m_vertIndex0, m_vertIndex1).GetHashCode(); }

        public int m_vertIndex0;
        public int m_vertIndex1;
    }

    public Polygon2d() { }
    public Polygon2d(LineSegVec edges) { /* body in .cpp */ }
    public Polygon2d(Polygon2d rhs) { /* body in .cpp */ }
    // ~Polygon2d() — managed

    public Polygon2d AssignFrom(Polygon2d rhs) { return this; /* body in .cpp */ }

    public int AddVertex(Vector2d v) { return 0; }
    public int AddEdge(Edge e) { return 0; }
    public int AddEdge(Vector2d v0, Vector2d v1) { return 0; }

    public bool GetVertex(int i, out Vector2d vertex) { vertex = default; return false; }
    public bool GetEdge(int i, out Edge edge) { edge = default; return false; }

    public int NumVertices() { return 0; }
    public int NumEdges() { return 0; }

    // calculates the contiguous pts from the edges. If removeInterior then any contours that are encircled
    // by another contour get removed. All are guaranteed to be anti-clockwise wound
    public void CalculateContours(bool removeInterior) { }
    // returns the number of unique contours
    public uint GetContourQuantity() { return 0; }
    // gets the ith contour - returns the number of points
    public uint GetContour(uint i, out Vector2d[] ppPts) { ppPts = null; return 0; }

    /// Set inversion operator.
    public static Polygon2d operator ~(Polygon2d self) { return null; }

    /// Set union operator.
    public static Polygon2d operator |(Polygon2d self, Polygon2d rhs) { return null; }

    /// Set intersection operator.
    public static Polygon2d operator &(Polygon2d self, Polygon2d rhs) { return null; }

    /// Symmetric difference (AKA xor) operator.
    public static Polygon2d operator ^(Polygon2d self, Polygon2d rhs) { return null; }

    /// Complement (or set difference) operator.
    public static Polygon2d operator -(Polygon2d self, Polygon2d rhs) { return null; }

    // this only makes sense if there's only one contour
    public bool IsWoundAnticlockwise() { return false; }

    public void CollapseVertices(double tol) { }

    /// Computes BspTree2d using this polygon's edges as dividing hyperplanes and caches it in m_bsp.
    private void ComputeBspTree() { }
    /// Cuts all of this polygon's edges by edge splitter passed in the argument.
    private void Cut(BspLineSegSplitter splitter) { }

    private BidirectionalMap<int, Vector2d> m_vertices;
    private BidirectionalMap<int, Edge> m_edges;

    /// Cached corresponding BSP tree, useful during set operations.
    private /*mutable*/ BspTree2d m_bsp;

    private List<List<Vector2d>> m_contours;
}
