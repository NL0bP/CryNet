// Literal port of dev/Code/CryEngine/CryAISystem/Walkability/WalkabilityCacheManager.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem.Walkability;

public class WalkabilityCacheManager : IWalkabilityCacheManager
{
    public WalkabilityCacheManager()
    {
        m_walkabilityRequestCount = 0;
        m_walkabilityCacheHitCount = 0;
        m_floorRequestCount = 0;
        m_floorCacheHitCount = 0;
        m_preservedFloorCache = 0;
    }

    ~WalkabilityCacheManager()
    {
        Reset();
    }

    public void Reset()
    {
        while (m_caches.Count > 0)
        {
            var first = System.Linq.Enumerable.First(m_caches);
            EnableActor(first.Key, false);
        }

        // m_alloc.FreeMemory(); — pool allocator omitted (GC managed)

        m_walkabilityRequestCount = 0;
        m_walkabilityCacheHitCount = 0;
        m_floorRequestCount = 0;
        m_floorCacheHitCount = 0;
        m_preservedFloorCache = 0;
    }

    public void PreUpdate()
    {
        m_currentFrameID = (nuint)gEnv.pRenderer.GetFrameID(false);

        m_walkabilityRequestCount = 0;
        m_walkabilityCacheHitCount = 0;
        m_floorRequestCount = 0;
        m_floorCacheHitCount = 0;
        m_preservedFloorCache = 0;
    }

    public void PostUpdate()
    {
    }

    public void Draw()
    {
        nuint memoryUsage = 0;

        foreach (var kv in m_caches)
        {
            ActorWalkabilityCache actorCache = kv.Value;
            actorCache.cache.Draw();

            memoryUsage += actorCache.cache.GetMemoryUsage();
        }

        // memoryUsage += m_alloc.GetTotalMemory().nAlloc;  — pool allocator omitted

        const float startY = 380.0f;
        float x = 1024.0f - 10.0f - 175.0f;
        float y = startY;

        CDebugDrawContext dc = new CDebugDrawContext();
        const float FontSize = 1.2f;
        const float LineHeight = 11.25f * FontSize;

        dc.Draw2dLabel(x, y, FontSize * 1.25f, WalkabilityColors.Col_BlueViolet, false, "WalkabilityCheck Stats");
        y += LineHeight * 1.25f;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Actors: {0}", m_caches.Count);
        y += LineHeight;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Memory: {0:F2}K", memoryUsage / 1024.0f);
        y += LineHeight;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Requested: {0}", m_walkabilityRequestCount);
        y += LineHeight;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Hit Cache: {0} ({1:F1}%)",
            m_walkabilityCacheHitCount,
            m_walkabilityRequestCount != 0 ? (m_walkabilityCacheHitCount / (float)m_walkabilityRequestCount) * 100.0f : 0.0f);
        y += LineHeight;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Preserved Floor Caches: {0}", m_preservedFloorCache);
        y += LineHeight;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Floor Checks: {0}", m_floorRequestCount);
        y += LineHeight;
        dc.Draw2dLabel(x, y, FontSize, WalkabilityColors.Col_BlueViolet, false, "Floor Hit Cache: {0} ({1:F1}%)",
            m_floorCacheHitCount,
            m_floorRequestCount != 0 ? (m_floorCacheHitCount / (float)m_floorRequestCount) * 100.0f : 0.0f);
        y += LineHeight;
    }

    public void EnableActor(uint actorID, bool enabled)
    {
        if (!enabled)
        {
            if (m_caches.TryGetValue(actorID, out ActorWalkabilityCache actorCache))
            {
                if (actorCache.cache != null)
                {
                    // ~WalkabilityCache + Deallocate — GC managed in C#
                    actorCache.cache = null;
                }

                m_caches.Remove(actorID);
            }
        }
    }

    public void PrepareActor(uint actorID, AABB aabb)
    {
        if (!m_caches.TryGetValue(actorID, out ActorWalkabilityCache actorCache))
        {
            actorCache = new ActorWalkabilityCache();
            m_caches[actorID] = actorCache;
        }

        if (actorCache.cache == null)
            actorCache.cache = new WalkabilityCache(actorID);

        if (m_currentFrameID != actorCache.frameID)
        {
            IAIObject actorObject = gAIEnv.pObjectContainer.GetAIObjectById(actorID);
            System.Diagnostics.Debug.Assert(actorObject != null);
            if (actorObject != null)
            {
                if (!actorCache.cache.Cache(aabb))
                    ++m_preservedFloorCache;
            }

            actorCache.frameID = m_currentFrameID;
        }

        m_caches[actorID] = actorCache; // store back since ActorWalkabilityCache is a class — assignment by ref
    }

    public bool IsFloorCached(uint actorID, Vec3 position, ref Vec3 floor)
    {
        ++m_floorRequestCount;

        if (m_caches.Count > 0)
        {
            if (m_caches.TryGetValue(actorID, out ActorWalkabilityCache actorCache))
            {
                if (actorCache.frameID == m_currentFrameID)
                {
                    if (actorCache.cache.IsFloorCached(position, out Vec3 cachedFloor))
                    {
                        floor = cachedFloor;
                        ++m_floorCacheHitCount;
                        return true;
                    }
                }
            }

            // check other actors' aabbs
            foreach (var kv in m_caches)
            {
                ActorWalkabilityCache other = kv.Value;
                if ((other.frameID == m_currentFrameID) && (kv.Key != actorID))
                {
                    if (other.cache.IsFloorCached(position, out Vec3 cachedFloor))
                    {
                        floor = cachedFloor;
                        ++m_floorCacheHitCount;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool FindFloor(int agentId, Vec3 position, ref Vec3 floor)
    {
        return FindFloor((uint)agentId, position, ref floor);
    }

    public bool FindFloor(uint actorID, Vec3 position, ref Vec3 floor)
    {
        ++m_floorRequestCount;

        if (m_caches.Count > 0)
        {
            if (m_caches.TryGetValue(actorID, out ActorWalkabilityCache actorCache))
            {
                if (actorCache.frameID == m_currentFrameID)
                {
                    if (actorCache.cache.IsFloorCached(position, out Vec3 cachedFloor))
                    {
                        floor = cachedFloor;
                        ++m_floorCacheHitCount;
                        return floor.z < float.MaxValue;
                    }
                }
            }

            // check other actors' aabbs
            foreach (var kv in m_caches)
            {
                ActorWalkabilityCache other = kv.Value;
                if ((other.frameID == m_currentFrameID) && (kv.Key != actorID))
                {
                    if (other.cache.IsFloorCached(position, out Vec3 cachedFloor))
                    {
                        floor = cachedFloor;
                        ++m_floorCacheHitCount;
                        return floor.z < float.MaxValue;
                    }
                }
            }
        }

        // TODO: Keep track of the best containing cache and perform the floor search in there, so it's stored in the cache and
        // uses the already filtered physical entities

        return AICollision.FindFloor(position, ref floor);
    }

    public bool CheckWalkability(uint actorID, Vec3 origin, Vec3 target, float radius,
                                 ref Vec3 finalFloor, ref bool flatFloor)
    {
        ++m_walkabilityRequestCount;

        if (m_caches.Count > 0)
        {
            float minZ = System.Math.Min(origin.z, target.z) - AICollision.WalkabilityFloorDownDist;
            float maxZ = System.Math.Max(origin.z, target.z) + AICollision.WalkabilityTotalHeight;

            AABB enclosingAABB = new AABB(AABB.RESET);
            enclosingAABB.Add(new Vec3(origin.x, origin.y, minZ), radius);
            enclosingAABB.Add(new Vec3(target.x, target.y, maxZ), radius);

            if (m_caches.TryGetValue(actorID, out ActorWalkabilityCache actorCache))
            {
                if (actorCache.frameID == m_currentFrameID)
                {
                    if (actorCache.cache.FullyContaints(enclosingAABB))
                    {
                        ++m_walkabilityCacheHitCount;
                        bool fl = false;
                        Vec3 ff = new Vec3();
                        bool r = actorCache.cache.CheckWalkability(origin, target, radius, out ff, out fl);
                        finalFloor = ff;
                        flatFloor = fl;
                        return r;
                    }
                }
            }

            // check other actors' aabbs
            foreach (var kv in m_caches)
            {
                ActorWalkabilityCache other = kv.Value;
                if ((other.frameID == m_currentFrameID) && (kv.Key != actorID))
                {
                    if (other.cache.FullyContaints(enclosingAABB))
                    {
                        ++m_walkabilityCacheHitCount;
                        bool fl = false;
                        Vec3 ff = new Vec3();
                        bool r = other.cache.CheckWalkability(origin, target, radius, out ff, out fl);
                        finalFloor = ff;
                        flatFloor = fl;
                        return r;
                    }
                }
            }
        }

        // need to add floor flatness computation
        return AICollision.CheckWalkability(origin, target, radius, ref finalFloor, ref flatFloor);
    }

    public bool CheckWalkability(uint actorID, Vec3 origin, Vec3 target, float radius,
                                 ListPositions boundary, ref Vec3 finalFloor, ref bool flatFloor,
                                 AABB? boundaryAABB = null)
    {
        if (Overlap.Lineseg_Polygon2D(new Lineseg(origin, target), boundary, boundaryAABB))
            return false;

        return CheckWalkability(actorID, origin, target, radius, ref finalFloor, ref flatFloor);
    }

    // ActorWalkabilityCache is a class (not struct) so we can mutate cache/frameID after dictionary fetch
    // without having to write back. C++ uses a struct but the value is accessed via iterator->second
    // which mutates in place.
    private class ActorWalkabilityCache
    {
        public WalkabilityCache cache;
        public nuint frameID;
    }

    private nuint m_currentFrameID;

    // typedef VectorMap<tAIObjectID, ActorWalkabilityCache> ActorWalkabilityCaches;
    // C# SortedDictionary preserves the ordered-key semantics of VectorMap<K, V>.
    private SortedDictionary<uint, ActorWalkabilityCache> m_caches = new SortedDictionary<uint, ActorWalkabilityCache>();

    // stl::PoolAllocatorNoMT<sizeof(WalkabilityCache)> m_alloc — omitted (GC managed)

    private nuint m_walkabilityRequestCount;
    private nuint m_walkabilityCacheHitCount;
    private nuint m_floorRequestCount;
    private nuint m_floorCacheHitCount;
    private nuint m_preservedFloorCache;
}

// Forward decl shell — full literal port pending
public class ListPositions : List<Vec3> { }

// Color constants used by the literal port of WalkabilityCacheManager.Draw().
public static class WalkabilityColors
{
    public static readonly ColorB Col_BlueViolet = new ColorB(138, 43, 226, 255);
}
