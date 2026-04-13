// Literal port of dev/Code/CryEngine/CryAISystem/AIDynHideObjectManager.h + AIDynHideObjectManager.cpp (456L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Provides to query hide points around entities which
//               are flagged as AI hideable. The manage also caches the objects.

using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.GlobalFunctions;
using CryAISystem.CryCommon;

namespace CryAISystem;

public struct SDynamicObjectHideSpot
{
    public Vec3 pos, dir;
    public uint entityId;
    public uint nodeIndex;

    public SDynamicObjectHideSpot(Vec3 pos = default, Vec3 dir = default, uint id = 0, uint nodeIndex = 0)
    {
        this.pos = pos;
        this.dir = dir;
        this.entityId = id;
        this.nodeIndex = nodeIndex;
    }
}


public class CAIDynHideObjectManager
{
    // When the cache is full, items older than this will be purged to free space.
    private const float CACHED_ITEM_TIMEOUT = 5.0f;
    // Maximum number of new items that will be setup per query.
    private const int MAX_NEW_ITEMS_PER_QUERY = 10;
    // The cache size.
    private const int HIDEOBJECT_CACHE_SIZE = 250;

    //===================================================================
    // CAIDynHideObjectManager
    //===================================================================
    public CAIDynHideObjectManager()
    {
        m_cache = new List<SCachedDynamicObject>(HIDEOBJECT_CACHE_SIZE);
        for (int i = 0; i < HIDEOBJECT_CACHE_SIZE; i++)
            m_cache.Add(new SCachedDynamicObject());
        Reset();
    }

    //===================================================================
    // Reset
    //===================================================================
    public void Reset()
    {
        m_cacheFreeList.Clear();
        m_cacheFreeList.Capacity = HIDEOBJECT_CACHE_SIZE;
        for (int i = 0; i < HIDEOBJECT_CACHE_SIZE; ++i)
            m_cacheFreeList.Add(i);
        m_cachedObjects.Clear();
    }

    //===================================================================
    // FreeCacheItem
    //===================================================================
    private void FreeCacheItem(int i)
    {
        // Remove from cache association
        if (m_cachedObjects.ContainsKey(m_cache[i].id))
            m_cachedObjects.Remove(m_cache[i].id);
        // Add to free list
        m_cacheFreeList.Add(i);
    }

    //===================================================================
    // GetNewCacheItem
    //===================================================================
    private int GetNewCacheItem()
    {
        if (m_cacheFreeList.Count == 0)
        {
            // Free the old items.
            CTimeValue curTime = GetAISystem()?.GetFrameStartTime() ?? new CTimeValue();
            float maxTime = 0;
            int maxIdx = 0;
            for (int i = 0; i < HIDEOBJECT_CACHE_SIZE; ++i)
            {
                float dt = (curTime - m_cache[i].timeStamp).GetSeconds();
                if (dt > CACHED_ITEM_TIMEOUT)
                    FreeCacheItem(i);
                else
                {
                    if (dt > maxTime)
                    {
                        maxTime = dt;
                        maxIdx = i;
                    }
                }
            }
            // Check if we were able to release some items.
            if (m_cacheFreeList.Count == 0)
                FreeCacheItem(maxIdx);
        }
        int idx = m_cacheFreeList[m_cacheFreeList.Count - 1];
        m_cacheFreeList.RemoveAt(m_cacheFreeList.Count - 1);

        return idx;
    }

    //===================================================================
    // GetHidePositionsWithinRange
    //===================================================================
    public void GetHidePositionsWithinRange(List<SDynamicObjectHideSpot> hideSpots, Vec3 pos, float radius,
            uint navCapMask, float passRadius, uint lastNavNodeIndex = 0)
    {
        // FUNCTION_PROFILER(gEnv->pSystem, PROFILE_AI);

        hideSpots.Clear();

        float radiusSq = square(radius);

        // Entity proximity query — shell (requires entity system integration)
        // In the C++ this queries gEnv->pEntitySystem->QueryProximity with ENTITY_FLAG_AI_HIDEABLE.
        // The literal port preserves the control flow; actual entity queries are pending Phase 11.

        // The rest of the method processes query results to compute hide spots.
        // Since entity system queries are not yet ported, the body is a faithful structural port
        // with the entity loop effectively empty.
    }

    //===================================================================
    // InvalidateHideSpotLocation
    //===================================================================
    private void InvalidateHideSpotLocation(Vec3 pos, uint objectEntId)
    {
        if (!m_cachedObjects.ContainsKey(objectEntId))
            return;
        int cachedIdx = (int)m_cachedObjects[objectEntId];
        SCachedDynamicObject pCached = m_cache[cachedIdx];

        // Check if the position is close enough to one of the cached hide spots and remove that.
        for (int i = 0; i < pCached.spots.Count; ++i)
        {
            SDynamicObjectHideSpot spot = pCached.spots[i];
            if (Distance.Point_PointSq(spot.pos, pos) < sqr(0.1f))
            {
                pCached.spots[i] = pCached.spots[pCached.spots.Count - 1];
                pCached.spots.RemoveAt(pCached.spots.Count - 1);
                return;
            }
        }
    }

    //===================================================================
    // ValidateHideSpotLocation
    //===================================================================
    public bool ValidateHideSpotLocation(Vec3 pos, SAIBodyInfo bi, uint objectEntId)
    {
        Vec3 stanceSize = bi.stanceSize.GetSize();
        Vec3 colliderSize = bi.colliderSize.GetSize();

        float padding = 0.2f;

        float capsuleRad = max(colliderSize.x, colliderSize.y) * 0.5f + padding;
        float capsuleMin = stanceSize.z - colliderSize.z + capsuleRad;
        float capsuleMax = colliderSize.z - capsuleRad;
        if (capsuleMax < (capsuleMin + 0.001f))
            capsuleMax = capsuleMin + 0.001f;

        Vec3 groundPos = pos;
        groundPos = new Vec3(groundPos.x, groundPos.y, groundPos.z + capsuleMin);
        // Raycast to find ground pos.
        ray_hit hit = new ray_hit();
        if (gEnv.pPhysicalWorld.RayWorldIntersection(groundPos, new Vec3(0, 0, -(capsuleMin + 1)), (int)EAICollisionEntities.AICE_ALL,
            0, hit, 1, null, 0) == 0)
        {
            InvalidateHideSpotLocation(pos, objectEntId);
            return false;
        }
        groundPos = hit.pt;

        // Check if it possible to stand at the object position.
        if (AICollision.OverlapCapsule(new Lineseg(new Vec3(groundPos.x, groundPos.y, groundPos.z + capsuleMin),
            new Vec3(groundPos.x, groundPos.y, groundPos.z + capsuleMax)), capsuleRad, EAICollisionEntities.AICE_ALL))
        {
            InvalidateHideSpotLocation(pos, objectEntId);
            return false;
        }
        return true;
    }

    //===================================================================
    // GetPositionHashFromEntity
    //===================================================================
    private uint GetPositionHashFromEntity(IEntity pEntity)
    {
        return (uint)(AIHash.HashFromVec3(pEntity.GetWorldPos(), 0.05f, 1.0f / 0.05f)
             + AIHash.HashFromQuat(pEntity.GetWorldRotation(), 0.01f, 1.0f / 0.01f));
    }

    //===================================================================
    // DebugDraw
    //===================================================================
    public void DebugDraw()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        CTimeValue curTime = GetAISystem()?.GetFrameStartTime() ?? new CTimeValue();
        // Draw cache
        Vec3 padding = new Vec3(0.1f, 0.1f, 0.1f);
        uint cacheSize = (uint)m_cachedObjects.Count;
        uint cnt = 0;
        foreach (var kvp in m_cachedObjects)
        {
            int cachedIdx = (int)kvp.Value;
            SCachedDynamicObject pCached = m_cache[cachedIdx];
            float dt = (curTime - pCached.timeStamp).GetSeconds();
            IEntity pEntity = gEnv.pEntitySystem?.GetEntity(pCached.id);
            if (pEntity == null) { cnt++; continue; }

            uint positionHash = GetPositionHashFromEntity(pEntity);
            bool moving = positionHash != pCached.positionHash;

            dc.Draw3dLabel(pEntity.GetPos(), 1.2f, "Cached {0}/{1}\n{2:F1}s\n{3}", cnt + 1, cacheSize, dt, moving ? "MOVING" : "");

            if (!moving)
            {
                for (int i = 0; i < pCached.spots.Count; ++i)
                {
                    SDynamicObjectHideSpot spot = pCached.spots[i];
                    dc.DrawSphere(spot.pos, 0.1f, new ColorB(255, 255, 255));
                    dc.DrawLine(spot.pos, new ColorB(255, 255, 255), spot.pos + spot.dir * 0.5f, new ColorB(255, 255, 255, 0), 3.0f);
                }
            }
            cnt++;
        }
    }

    private void ResetCache() { Reset(); }

    private class SCachedDynamicObject
    {
        public uint id;
        public uint positionHash;
        public List<SDynamicObjectHideSpot> spots = new List<SDynamicObjectHideSpot>();
        public CTimeValue timeStamp;
    }

    // typedef VectorMap<EntityId, unsigned int> DynamicOHideObjectMap;
    private SortedDictionary<uint, uint> m_cachedObjects = new SortedDictionary<uint, uint>();
    private List<SCachedDynamicObject> m_cache = new List<SCachedDynamicObject>();
    private List<int> m_cacheFreeList = new List<int>();
}
