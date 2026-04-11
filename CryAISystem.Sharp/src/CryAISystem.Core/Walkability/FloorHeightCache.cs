// Literal port of dev/Code/CryEngine/CryAISystem/Walkability/FloorHeightCache.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem.Walkability;

public class FloorHeightCache
{
    private const float CellSize = 0.25f;
    private const float InvCellSize = 1.0f / CellSize;
    private const float HalfCellSize = CellSize * 0.5f;

    public void Reset()
    {
        m_floorHeights.Clear();
    }

    public void SetHeight(Vec3 position, float height)
    {
        m_floorHeights.Add(new CacheEntry(position, height));
    }

    public bool GetHeight(Vec3 position, out float height)
    {
        // std::set::find returns iterator at position if present, end() if not.
        // C# SortedSet doesn't expose lookup-by-comparable directly; we use TryGetValue (.NET 4.7.2+).
        CacheEntry probe = new CacheEntry(position);
        if (m_floorHeights.TryGetValue(probe, out CacheEntry it))
        {
            height = it.height;
            return true;
        }
        height = 0.0f;
        return false;
    }

    public Vec3 GetCellCenter(Vec3 position)
    {
        float x = (float)System.Math.Floor(position.x * InvCellSize) * CellSize + HalfCellSize;
        float y = (float)System.Math.Floor(position.y * InvCellSize) * CellSize + HalfCellSize;

        return new Vec3(x, y, position.z);
    }

    public AABB GetAABB(Vec3 position)
    {
        float x = (float)System.Math.Floor(position.x * InvCellSize) * CellSize;
        float y = (float)System.Math.Floor(position.y * InvCellSize) * CellSize;

        return new AABB(new Vec3(x, y, position.z), new Vec3(x + CellSize, y + CellSize, position.z));
    }

    public void Draw(ColorB colorGood, ColorB colorBad)
    {
        foreach (CacheEntry entry in m_floorHeights)
        {
            ColorB color = entry.height < float.MaxValue ? colorGood : colorBad;
            Vec3 center = GetCellCenter(entry.x, entry.y);
            center.z = entry.height < float.MaxValue ? entry.height : (float)entry.z + 0.5f;

            Vec3 v0 = new Vec3(center.x - HalfCellSize, center.y - HalfCellSize, center.z + 0.015f);
            Vec3 v1 = new Vec3(center.x - HalfCellSize, center.y + HalfCellSize, center.z + 0.015f);
            Vec3 v2 = new Vec3(center.x + HalfCellSize, center.y + HalfCellSize, center.z + 0.015f);
            Vec3 v3 = new Vec3(center.x + HalfCellSize, center.y - HalfCellSize, center.z + 0.015f);

            CDebugDrawContext dc = new CDebugDrawContext();

            dc.SetBackFaceCulling(false);
            dc.SetDepthWrite(false);
            dc.DrawTriangle(v0, color, v1, color, v2, color);
            dc.DrawTriangle(v0, color, v2, color, v3, color);
        }
    }

    public nuint GetMemoryUsage()
    {
        // not sure if its only 3 pointers overhead for each node?
        return (nuint)(m_floorHeights.Count * (System.Runtime.InteropServices.Marshal.SizeOf<CacheEntry>() + System.IntPtr.Size * 3));
    }

    private Vec3 GetCellCenter(ushort x, ushort y)
    {
        return new Vec3(x * CellSize + HalfCellSize, y * CellSize + HalfCellSize, 0.0f);
    }

    private struct CacheEntry : System.IComparable<CacheEntry>
    {
        public CacheEntry(Vec3 position, float _height = 0.0f)
        {
            height = _height;
            x = (ushort)(position.x * InvCellSize);
            y = (ushort)(position.y * InvCellSize);
            z = (ushort)(position.z);
        }

        public int CompareTo(CacheEntry rhs)
        {
            if (x != rhs.x)
                return x < rhs.x ? -1 : 1;

            if (y != rhs.y)
                return y < rhs.y ? -1 : 1;

            return z < rhs.z ? -1 : (z > rhs.z ? 1 : 0);
        }

        public ushort x;
        public ushort y;
        public ushort z;

        public float height;
    }

    // typedef std::set<CacheEntry> FloorHeights;
    private SortedSet<CacheEntry> m_floorHeights = new SortedSet<CacheEntry>();
}
