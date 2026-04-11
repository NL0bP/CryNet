// Literal port of dev/Code/CryEngine/CryAISystem/PolygonSetOps/LineSeg.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.PolygonSetOps;

/**
* @brief A simple oriented 2D line segment implementation.
*/
public class LineSeg<VectorT> where VectorT : struct
{
    private VectorT m_start;
    private VectorT m_end;

    public LineSeg(VectorT start, VectorT end)
    {
        m_start = start;
        m_end = end;
    }

    public VectorT Start() { return m_start; }
    public VectorT End() { return m_end; }

    /// Inverts this line segment's direction.
    public void Invert()
    {
        VectorT tmp = m_start;
        m_start = m_end;
        m_end = tmp;
    }

    public VectorT Dir()
    {
        // C++: return m_end - m_start;
        // C# generics cannot use operator- directly on a generic struct without dynamic dispatch.
        return (VectorT)(object)((dynamic)m_end - (dynamic)m_start);
    }

    /// Takes parameter and returns point corresponding to that parameter.
    public VectorT Pt(float t)
    {
        // C++: return m_start + Dir() * t;
        return (VectorT)(object)((dynamic)m_start + (dynamic)Dir() * t);
    }

    public float GetParam(VectorT pt)
    {
        dynamic toPt = (dynamic)pt - (dynamic)m_start;
        dynamic thisDir = Dir();
        int sign = toPt.Dot(thisDir) > 0.0f ? 1 : -1;
        return (float)(sign * toPt.GetLength() / thisDir.GetLength());
    }

    /// Returns (unnormalized) normal vector to this lineseg.
    public VectorT Normal()
    {
        return (VectorT)(object)((dynamic)Dir()).Perp();
    }
}

// typedef LineSeg<Vector2d> LineSeg2d;
public class LineSeg2d : LineSeg<Vector2d>
{
    public LineSeg2d(Vector2d start, Vector2d end) : base(start, end) { }
}

// typedef std::vector<LineSeg2d> LineSegVec;
public class LineSegVec : List<LineSeg2d> { }
