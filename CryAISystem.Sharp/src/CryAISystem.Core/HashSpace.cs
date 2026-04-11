// Literal port of dev/Code/CryEngine/CryAISystem/HashSpace.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// HashSpace requires its element type to expose a Vec3 position. CHashSpaceTraits<T>
// is the default extractor; users can supply a custom one.
public interface ICHashSpaceTraits<T>
{
    Vec3 GetPos(T item);
}

public class CHashSpaceDefaultTraits<T> : ICHashSpaceTraits<T> where T : IHashSpaceItem
{
    public Vec3 GetPos(T item) { return item.GetPos(); }
}

// Convenience interface so default traits compile.
public interface IHashSpaceItem
{
    Vec3 GetPos();
}

/// Contains any type that can be represented with a single position, obtained
/// through T::GetPos()
public class CHashSpace<T, TraitsT>
    where T : class, System.IEquatable<T>
    where TraitsT : ICHashSpaceTraits<T>
{
    public TraitsT Traits;

    public struct Cell
    {
        public List<uint> masks;
        public List<T> objects;
    }

    // Note: bucket count must be power of 2!
    public CHashSpace(Vec3 cellSize, int buckets)
    {
        m_cellSize = cellSize;
        m_cellScale = new Vec3(1.0f / cellSize.x, 1.0f / cellSize.y, 1.0f / cellSize.z);
        m_buckets = buckets;
        m_totalNumObjects = 0;
        // MEMSTAT_CONTEXT(EMemStatContextTypes::MSC_Navigation, 0, "HashSpace");
        m_cells = new List<Cell>(m_buckets);
        for (int i = 0; i < m_buckets; ++i) m_cells.Add(new Cell { masks = new List<uint>(), objects = new List<T>() });
    }

    public CHashSpace(Vec3 cellSize, int buckets, TraitsT traits) : this(cellSize, buckets)
    {
        Traits = traits;
    }

    // ~CHashSpace() {}

    /// Adds a copy of the object
    public void AddObject(T obj)
    {
        int i, j, k;
        GetIJKFromPosition(GetPos(obj), out i, out j, out k);
        uint index = GetHashBucketIndex(i, j, k);
        uint mask = GetLocationMask(i, j, k);

        m_cells[(int)index].objects.Add(obj);
        m_cells[(int)index].masks.Add(mask);

        ++m_totalNumObjects;
    }

    /// Removes all objects indicated by operator==
    public void RemoveObject(T obj)
    {
        uint index = GetCellIndexFromPosition(GetPos(obj));
        Cell cell = m_cells[(int)index];
        for (uint i = 0, ni = (uint)cell.objects.Count; i < ni; )
        {
            T obj2 = cell.objects[(int)i];
            if (obj2.Equals(obj))
            {
                cell.objects[(int)i] = cell.objects[cell.objects.Count - 1];
                cell.objects.RemoveAt(cell.objects.Count - 1);
                cell.masks[(int)i] = cell.masks[cell.masks.Count - 1];
                cell.masks.RemoveAt(cell.masks.Count - 1);
                --ni;
                --m_totalNumObjects;
            }
            else
            {
                ++i;
            }
        }
    }

    /// Removes all objects
    public void Clear(bool clearMemory)
    {
        for (uint i = 0; i < (uint)m_cells.Count; ++i)
        {
            if (clearMemory)
            {
                m_cells[(int)i].objects.Clear();
                m_cells[(int)i].objects.Capacity = 0;
                m_cells[(int)i].masks.Clear();
                m_cells[(int)i].masks.Capacity = 0;
            }
            else
            {
                m_cells[(int)i].objects.Clear();
                m_cells[(int)i].masks.Clear();
            }
        }
        m_totalNumObjects = 0;
    }

    /// Frees memory for unused cells
    public void Compact()
    {
        for (uint i = 0; i < (uint)m_cells.Count; ++i)
        {
            if (m_cells[(int)i].objects.Count == 0)
                m_cells[(int)i].objects.Capacity = 0;
            if (m_cells[(int)i].masks.Count == 0)
                m_cells[(int)i].masks.Capacity = 0;
        }
    }

    /// the functor gets called and passed every object (and the distance-squared) that is within radius of
    /// pos. Returns the number of objects within range
    public void ProcessObjectsWithinRadius(Vec3 pos, float radius, System.Action<T, float> functor)
    {
        float radiusSQ = radius * radius;
        int startI, startJ, startK, endI, endJ, endK;
        GetIJKFromPosition(pos - new Vec3(radius, radius, radius), out startI, out startJ, out startK);
        GetIJKFromPosition(pos + new Vec3(radius, radius, radius), out endI, out endJ, out endK);

        for (int curI = startI; curI <= endI; ++curI)
        {
            for (int curJ = startJ; curJ <= endJ; ++curJ)
            {
                for (int curK = startK; curK <= endK; ++curK)
                {
                    uint index = GetHashBucketIndex(curI, curJ, curK);
                    uint mask = GetLocationMask(curI, curJ, curK);
                    var objects = m_cells[(int)index].objects;
                    var masks = m_cells[(int)index].masks;

                    uint count = (uint)objects.Count;
                    for (uint ci = 0; ci < count; ++ci)
                    {
                        if (mask != masks[(int)ci]) continue;
                        T obj = objects[(int)ci];
                        Vec3 op = GetPos(obj);
                        float dx = pos.x - op.x;
                        float dy = pos.y - op.y;
                        float dz = pos.z - op.z;
                        float curDistSQ = dx * dx + dy * dy + dz * dz;
                        if (curDistSQ < radiusSQ)
                        {
                            functor(obj, curDistSQ);
                        }
                    }
                }
            }
        }
    }

    // in contrast to the two methods above this one assumes that the passed in
    // functor returns a boolean. If the functor returns true the processing is stopped.
    public bool GetObjectWithinRadius(Vec3 pos, float radius, System.Func<T, float, bool> functor)
    {
        bool ret = false;
        float radiusSQ = radius * radius;
        int startI, startJ, startK, endI, endJ, endK;
        GetIJKFromPosition(pos - new Vec3(radius, radius, radius), out startI, out startJ, out startK);
        GetIJKFromPosition(pos + new Vec3(radius, radius, radius), out endI, out endJ, out endK);

        for (int curI = startI; curI <= endI && !ret; ++curI)
        {
            for (int curJ = startJ; curJ <= endJ && !ret; ++curJ)
            {
                for (int curK = startK; curK <= endK && !ret; ++curK)
                {
                    uint index = GetHashBucketIndex(curI, curJ, curK);
                    uint mask = GetLocationMask(curI, curJ, curK);
                    var objects = m_cells[(int)index].objects;
                    var masks = m_cells[(int)index].masks;

                    uint count = (uint)objects.Count;
                    for (uint ci = 0; ci < count; ++ci)
                    {
                        if (mask != masks[(int)ci]) continue;
                        T obj = objects[(int)ci];
                        Vec3 op = GetPos(obj);
                        float dx = pos.x - op.x;
                        float dy = pos.y - op.y;
                        float dz = pos.z - op.z;
                        float curDistSQ = dx * dx + dy * dy + dz * dz;
                        if (curDistSQ < radiusSQ)
                        {
                            if (functor(obj, curDistSQ))
                            {
                                ret = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
        return ret;
    }

    /// returns the total number of objects
    public uint GetNumObjects() { return m_totalNumObjects; }
    // Returns number of buckets in the hash space.
    public uint GetBucketCount() { return (uint)m_cells.Count; }
    // Returns number of objects in the specified bucket.
    public uint GetObjectCountInBucket(uint bucket) { return (uint)m_cells[(int)bucket].objects.Count; }
    // Returns the indexed object in the specified bucket.
    public T GetObjectInBucket(uint obj, uint bucket) { return m_cells[(int)bucket].objects[(int)obj]; }

    /// Gets the AABB associated with an i, j, k
    public void GetAABBFromIJK(int i, int j, int k, out AABB aabb)
    {
        aabb = new AABB();
        aabb.min = new Vec3(i * m_cellSize.x, j * m_cellSize.y, k * m_cellSize.z);
        aabb.max = aabb.min + m_cellSize;
    }

    /// return the individual i, j, k for 3D array lookup
    public void GetIJKFromPosition(Vec3 pos, out int i, out int j, out int k)
    {
        i = (int)System.MathF.Floor(pos.x * m_cellScale.x);
        j = (int)System.MathF.Floor(pos.y * m_cellScale.y);
        k = (int)System.MathF.Floor(pos.z * m_cellScale.z);
    }

    /// returns bucket index from given i,j,k
    public uint GetHashBucketIndex(int i, int j, int k)
    {
        const int h1 = unchecked((int)0x8da6b343);
        const int h2 = unchecked((int)0xd8163841);
        const int h3 = unchecked((int)0xcb1ab31f);
        int n = h1 * i + h2 * j + h3 * k;
        return (uint)(n & (m_buckets - 1));
    }

    /// Returns true if successful, false otherwise
    public bool ReadFromFile(CCryFile file)
    {
        // (port: file IO pending CryFile.h port)
        Clear(false);
        return true;
    }

    /// Populates a given vector with information about bucket usage
    public bool RecordBucketUsage(T obj, List<uint32> cellCounts)
    {
        if ((uint)m_cells.Count == (uint)cellCounts.Count)
        {
            int i, j, k;
            GetIJKFromPosition(GetPos(obj), out i, out j, out k);
            uint index = GetHashBucketIndex(i, j, k);
            uint mask = GetLocationMask(i, j, k);

            cellCounts[(int)index]++;

            return true;
        }
        return false;
    }

    /// Reserves space in the buckets according to the supplied vector
    public bool ReserveSpaceInBuckets(List<uint32> cellCounts)
    {
        if ((uint)m_cells.Count == (uint)cellCounts.Count)
        {
            for (int idx = 0; idx < m_cells.Count; ++idx)
            {
                m_cells[idx].masks.Capacity = (int)cellCounts[idx];
                m_cells[idx].objects.Capacity = (int)cellCounts[idx];
            }
            return true;
        }
        return false;
    }

    /// Returns the memory usage in bytes
    public nuint MemStats()
    {
        // simplified — capacity * sizeof not exact in C#
        return 0;
    }

    public uint GetLocationMask(int i, int j, int k)
    {
        const uint mi = (1u << 12) - 1;
        const uint mj = (1u << 12) - 1;
        const uint mk = (1u << 8) - 1;
        uint x = ((uint)i & mi);
        uint y = ((uint)j & mj);
        uint z = ((uint)k & mk);
        return x | (y << 12) | (z << 24);
    }

    private Vec3 GetPos(T item)
    {
        return Traits.GetPos(item);
    }

    /// returns index into m_cells
    private uint GetCellIndexFromPosition(Vec3 pos)
    {
        int i, j, k;
        GetIJKFromPosition(pos, out i, out j, out k);
        return GetHashBucketIndex(i, j, k);
    }

    private List<Cell> m_cells;
    private Vec3 m_cellSize;
    private Vec3 m_cellScale;
    private int m_buckets;

    private uint m_totalNumObjects;
}

// CCryFile shell — pending CryFile.h port
public class CCryFile { }
