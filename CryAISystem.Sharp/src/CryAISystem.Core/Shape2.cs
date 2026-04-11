// Literal port of dev/Code/CryEngine/CryAISystem/Shape2.h
// Shape2.cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Temp file holding code extracted from CAISystem.h/cpp

using System.Collections.Generic;
using CryAISystem.Walkability;

namespace CryAISystem;

public class CShapeMask
{
    public CShapeMask() { /* impl in .cpp */ }
    // ~CShapeMask();

    public void Build(SShape pShape, float granularity) { /* impl in .cpp */ }

    public enum EType { TYPE_IN, TYPE_EDGE, TYPE_OUT }
    public EType GetType(Vec3 pt) { return EType.TYPE_OUT; /* impl in .cpp */ }

    public nuint MemStats() { return 0; /* impl in .cpp */ }

    private EType GetType(SShape pShape, Vec3 min, Vec3 max) { return EType.TYPE_OUT; /* impl in .cpp */ }
    private void GetBox(out Vec3 min, out Vec3 max, int i, int j) { min = new Vec3(0, 0, 0); max = new Vec3(0, 0, 0); /* impl in .cpp */ }

    private AABB m_aabb;
    private uint m_nx, m_ny;
    private float m_dx, m_dy;

    private uint m_bytesPerRow;
    // typedef std::vector<unsigned char> TContainer;
    private List<byte> m_container = new List<byte>();
}

public class SShape
{
    public SShape() { /* impl in .cpp */ }

    public SShape(
        ListPositions shape_,
        AABB aabb_,
        IAISystem_ENavigationType navType = IAISystem_ENavigationType.NAV_UNSET,
        int type = 0,
        bool closed_ = false,
        float height_ = 0.0f,
        EAILightLevel lightLevel_ = EAILightLevel.AILL_NONE,
        bool temp = false)
    { /* impl in .cpp */ }

    public SShape(
        ListPositions shape_,
        bool allowMask = false,
        IAISystem_ENavigationType navType_ = IAISystem_ENavigationType.NAV_UNSET,
        int type_ = 0,
        bool closed_ = false,
        float height_ = 0.0f,
        EAILightLevel lightLevel_ = EAILightLevel.AILL_NONE,
        bool temp = false)
    { /* impl in .cpp */ }

    // ~SShape();

    public void RecalcAABB() { /* impl in .cpp */ }

    public void BuildMask(float granularity) { /* impl in .cpp */ }

    public void ReleaseMask() { /* impl in .cpp */ }

    public void OffsetShape(Vec3 offset) { /* impl in .cpp */ }

    public int NearestPointOnPath(Vec3 pos, bool forceLoop, out float dist, out Vec3 nearestPt, out float distAlongPath,
        out Vec3 segmentDir, out uint segmentStartIndex, out float pathLength, out float segmentFraction)
    {
        dist = 0; nearestPt = pos; distAlongPath = 0; segmentDir = new Vec3(0, 0, 0); segmentStartIndex = 0; pathLength = 0; segmentFraction = 0;
        return 0; /* impl in .cpp */
    }

    public int GetIntersectionDistances(Vec3 start, Vec3 end, float[] intersectDistArray, int maxIntersections, bool testHeight = false, bool testTopBottom = false)
    { return 0; /* impl in .cpp */ }

    public Vec3 GetPointAlongPath(float dist) { return new Vec3(0, 0, 0); /* impl in .cpp */ }

    public bool IsPointInsideShape(Vec3 pos, bool checkHeight) { return false; /* impl in .cpp */ }

    public bool ConstrainPointInsideShape(ref Vec3 pos, bool checkHeight) { return false; /* impl in .cpp */ }

    public nuint MemStats() { return 0; /* impl in .cpp */ }

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
    public SPerceptionModifierShape(ListPositions shape, float reductionPerMetre, float reductionMax, float fHeight, bool isClosed)
    {
        fReductionPerMetre = reductionPerMetre;
        fReductionMax = reductionMax;
        /* impl in .cpp */
    }

    public float fReductionPerMetre;
    public float fReductionMax;
}

// typedef std::map<string, SPerceptionModifierShape> PerceptionModifierShapeMap;
public class PerceptionModifierShapeMap : SortedDictionary<string, SPerceptionModifierShape> { }
