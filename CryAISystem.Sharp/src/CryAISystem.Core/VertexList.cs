// Literal port of dev/Code/CryEngine/CryAISystem/VertexList.h + VertexList.cpp (181L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// ObstacleData, Obstacles, ObstacleDataDesc are defined in GraphStructures.cs

public class CVertexList
{
    private const int BAI_VERTEX_FILE_VERSION = 1;
    private const int BUCKET_COUNT = 8192;

    //===================================================================
    // CVertexList
    //===================================================================
    public CVertexList()
    {
        m_hashSpace = new CHashSpace<SVertexRecord, VertexHashSpaceTraits>(new Vec3(7, 7, 7), BUCKET_COUNT, new VertexHashSpaceTraits(this));
        m_obstacles.Clear();
    }

    //===================================================================
    // FindVertex
    //===================================================================
    public int FindVertex(ObstacleData od)
    {
        int index = 0;
        for (int i = 0; i < m_obstacles.Count; i++, index++)
        {
            if (m_obstacles[i] == od)
                return index;
        }
        return -1;
    }

    //===================================================================
    // AddVertex
    //===================================================================
    public int AddVertex(ObstacleData od)
    {
        int index = FindVertex(od);
        if (index < 0)
        {
            m_obstacles.Add(od);
            index = m_obstacles.Count - 1;
            m_hashSpace.AddObject(new SVertexRecord((uint)index) { _owner = this });
        }
        return index;
    }

    //===================================================================
    // GetVertex
    //===================================================================
    public ObstacleData GetVertex(int index)
    {
        if (!IsIndexValid(index))
        {
            AILog.AIError($"CVertexList::GetVertex Tried to retrieve a non existing vertex ({index}) from vertex list (size {m_obstacles.Count}).Please regenerate the triangulation [Design bug]");
            return m_obstacles[0];
        }
        return m_obstacles[index];
    }

    //===================================================================
    // ModifyVertex
    //===================================================================
    public ObstacleData ModifyVertex(int index)
    {
        if (!IsIndexValid(index))
        {
            AILog.AIError($"CVertexList::ModifyVertex Tried to retrieve a non existing vertex ({index}) from vertex list (size {m_obstacles.Count}).Please regenerate the triangulation [Design bug]");
            return new ObstacleData(); // static fallback
        }
        return m_obstacles[index];
    }

    public bool IsIndexValid(int index) { return index >= 0 && index < m_obstacles.Count; }

    //===================================================================
    // ReadFromFile
    //===================================================================
    public bool ReadFromFile(string fileName)
    {
        // MEMSTAT_CONTEXT(EMemStatContextTypes::MSC_Navigation, 0, "Triangulation vertices");

        m_obstacles.Clear();
        m_hashSpace.Clear(true);

        CCryFile file = new CCryFile();
        if (!file.Open(fileName, "rb"))
        {
            AILog.AIError("CVertexList::ReadFromFile could not open vertex file: Regenerate triangulation in the editor [Design bug]");
            return false;
        }

        int iNumber = 0;

        AILog.AILogLoading("Verifying BAI file version");
        file.ReadType(ref iNumber);
        if (iNumber != BAI_VERTEX_FILE_VERSION)
        {
            AILog.AIError($"CVertexList::ReadFromFile Wrong vertex list BAI file version - found {iNumber} expected {BAI_VERTEX_FILE_VERSION}: Regenerate triangulation in the editor [Design bug]");
            file.Close();
            return false;
        }

        // Read number of descriptors.
        file.ReadType(ref iNumber);

        if (iNumber > 0)
        {
            ObstacleDataDesc[] obDescs = new ObstacleDataDesc[iNumber];
            // file.ReadType(&obDescs[0], iNumber); — binary read not ported
            m_obstacles.Capacity = iNumber;
            for (int i = 0; i < iNumber; ++i)
                m_obstacles.Add(new ObstacleData());

            List<uint> cellCounts = new List<uint>(new uint[BUCKET_COUNT]);
            for (int i = 0; i < iNumber; ++i)
            {
                m_obstacles[i].vPos = obDescs[i].vPos;
                m_obstacles[i].vDir = obDescs[i].vDir;
                m_obstacles[i].fApproxRadius = obDescs[i].fApproxRadius;
                m_obstacles[i].flags = obDescs[i].flags;
                m_obstacles[i].approxHeight = obDescs[i].approxHeight;
                m_hashSpace.RecordBucketUsage(new SVertexRecord((uint)i) { _owner = this }, cellCounts);
            }

            m_hashSpace.ReserveSpaceInBuckets(cellCounts);

            for (int i = 0; i < iNumber; ++i)
            {
                m_hashSpace.AddObject(new SVertexRecord((uint)i) { _owner = this });
            }
        }

        file.Close();

        return true;
    }

    public void Clear() { m_obstacles.Clear(); m_obstacles.Capacity = 0; m_hashSpace?.Clear(true); }

    //===================================================================
    // Reset
    //===================================================================
    public void Reset()
    {
        for (int i = 0; i < m_obstacles.Count; i++)
        {
            ObstacleData od = m_obstacles[i];
            od.ClearNavNodes();
        }
    }

    public int GetSize() { return m_obstacles.Count; }
    public int GetCapacity() { return m_obstacles.Capacity; }

    //===================================================================
    // GetVerticesInRange
    //===================================================================
    public void GetVerticesInRange(List<(float, uint)> vertsOut, Vec3 pos, float range, byte flags)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);
        vertsOut.Clear();
        CVertexList vertList = this;
        m_hashSpace.ProcessObjectsWithinRadius(pos, range, (SVertexRecord record, float distSq) =>
        {
            if ((vertList.GetVertex((int)record.vertIndex).flags & flags) != 0)
                vertsOut.Add((distSq, record.vertIndex));
        });
    }

    //===================================================================
    // GetMemoryStatistics
    //===================================================================
    public void GetMemoryStatistics(ICrySizer pSizer)
    {
        pSizer.AddObject(m_hashSpace, m_hashSpace.MemStats());
        pSizer.AddContainer(m_obstacles);
    }

    // Internal types
    private class SVertexRecord : System.IEquatable<SVertexRecord>, IHashSpaceItem
    {
        public SVertexRecord() { vertIndex = 0; _owner = null; }
        public SVertexRecord(uint vertIndex) { this.vertIndex = vertIndex; this._owner = null; }
        public Vec3 GetPos() { return _owner != null ? _owner.GetVertex((int)vertIndex).vPos : new Vec3(0, 0, 0); }
        public bool Equals(SVertexRecord rhs) { return rhs != null && rhs.vertIndex == vertIndex; }
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
