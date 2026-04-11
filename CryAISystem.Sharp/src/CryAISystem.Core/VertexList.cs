// Literal port of dev/Code/CryEngine/CryAISystem/VertexList.h
// .cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public class CVertexList
{
    public CVertexList() { /* impl in .cpp */ }
    // ~CVertexList();

    public int AddVertex(ObstacleData od) { return 0; /* impl in .cpp */ }

    public ObstacleData GetVertex(int index) { return null; /* impl in .cpp */ }
    public ObstacleData ModifyVertex(int index) { return null; /* impl in .cpp */ }
    public int FindVertex(ObstacleData od) { return -1; /* impl in .cpp */ }

    public bool IsIndexValid(int index) { return index >= 0 && index < (int)m_obstacles.Count; }

    public bool ReadFromFile(string fileName) { return false; /* impl in .cpp */ }

    public void Clear() { m_obstacles.Clear(); m_obstacles.Capacity = 0; m_hashSpace?.Clear(true); }
    public void Reset() { /* impl in .cpp */ }
    public int GetSize() { return m_obstacles.Count; }
    public int GetCapacity() { return m_obstacles.Capacity; }

    public void GetVerticesInRange(List<(float, uint)> vertsOut, Vec3 pos, float range, byte flags) { /* impl in .cpp */ }
    public void GetMemoryStatistics(ICrySizer pSizer) { /* impl in .cpp */ }

    private class SVertexRecord : System.IEquatable<SVertexRecord>, IHashSpaceItem
    {
        public SVertexRecord() { vertIndex = 0; _owner = null; }
        public SVertexRecord(uint vertIndex) { this.vertIndex = vertIndex; this._owner = null; }
        public Vec3 GetPos() { return _owner != null ? _owner.GetVertex((int)vertIndex).vPos : new Vec3(0, 0, 0); }
        public bool Equals(SVertexRecord rhs) { return rhs.vertIndex == vertIndex; }
        public override bool Equals(object obj) { return obj is SVertexRecord r && Equals(r); }
        public override int GetHashCode() { return vertIndex.GetHashCode(); }
        public uint vertIndex;
        public CVertexList _owner;
    }

    private class VertexHashSpaceTraits : ICHashSpaceTraits<SVertexRecord>
    {
        public VertexHashSpaceTraits(CVertexList vertexList) { this.vertexList = vertexList; }
        public Vec3 GetPos(SVertexRecord item) { return item.GetPos(); }
        public CVertexList vertexList;
    }

    /// Our spatial structure
    private CHashSpace<SVertexRecord, VertexHashSpaceTraits> m_hashSpace;

    private Obstacles m_obstacles = new Obstacles();
}

// Literal port of dev/Code/CryEngine/CryAISystem/GraphStructures.h ObstacleData (lines 671-694)
public class ObstacleData
{
    public Vec3 vPos;
    public Vec3 vDir;
    /// this radius is approximate - it is estimated during link generation. if -ve it means
    /// that it shouldn't be used (i.e. object is significantly non-circular)
    public float fApproxRadius;
    public uint8 flags;
    public uint8 approxHeight;  // height in 4.4 fixed point format.

    public const uint8 OBSTACLE_COLLIDABLE = 1;
    public const uint8 OBSTACLE_HIDEABLE   = 2;

    public void SetCollidable(bool state) { if (state) flags |= OBSTACLE_COLLIDABLE; else flags = (uint8)(flags & ~OBSTACLE_COLLIDABLE); }
    public void SetHideable(bool state)   { if (state) flags |= OBSTACLE_HIDEABLE;   else flags = (uint8)(flags & ~OBSTACLE_HIDEABLE); }
    public bool IsCollidable() { return (flags & OBSTACLE_COLLIDABLE) != 0; }
    public bool IsHideable()   { return (flags & OBSTACLE_HIDEABLE) != 0; }

    /// Sets the approximate height and does the necessary conversion.
    public void SetApproxHeight(float h) { approxHeight = (uint8)System.Math.Clamp(h * (1 << 4), 0.0f, 255.0f); }
    /// Returns the approximate height and does the necessary conversion.
    public float GetApproxHeight() { return (float)approxHeight / (float)(1 << 4); }
}
public class Obstacles : List<ObstacleData> { }
