// Literal port of dev/Code/CryEngine/CryAISystem/AIDynHideObjectManager.h
// .cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Provides to query hide points around entities which
//               are flagged as AI hideable. The manage also caches the objects.

using System.Collections.Generic;

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
    public CAIDynHideObjectManager() { /* impl in .cpp */ }

    public void Reset() { /* impl in .cpp */ }

    public void GetHidePositionsWithinRange(List<SDynamicObjectHideSpot> hideSpots, Vec3 pos, float radius,
            uint navCapMask, float passRadius, uint lastNavNodeIndex = 0)
    { /* impl in .cpp */ }

    public bool ValidateHideSpotLocation(Vec3 pos, SAIBodyInfo bi, uint objectEntId) { return false; /* impl in .cpp */ }

    public void DebugDraw() { /* impl in .cpp */ }

    private void ResetCache() { /* impl in .cpp */ }
    private void FreeCacheItem(int i) { /* impl in .cpp */ }
    private int GetNewCacheItem() { return 0; /* impl in .cpp */ }
    private uint GetPositionHashFromEntity(IEntity pEntity) { return 0; /* impl in .cpp */ }
    private void InvalidateHideSpotLocation(Vec3 pos, uint objectEntId) { /* impl in .cpp */ }

    private struct SCachedDynamicObject
    {
        public uint id;
        public uint positionHash;
        public List<SDynamicObjectHideSpot> spots;
        public CTimeValue timeStamp;
    }

    // typedef VectorMap<EntityId, unsigned int> DynamicOHideObjectMap;
    private SortedDictionary<uint, uint> m_cachedObjects = new SortedDictionary<uint, uint>();
    private List<SCachedDynamicObject> m_cache = new List<SCachedDynamicObject>();
    private List<int> m_cacheFreeList = new List<int>();
}
