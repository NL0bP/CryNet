// Literal port of dev/Code/CryEngine/CryAISystem/Walkability/WalkabilityCache.h
// .cpp impl (601L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.Walkability;

public class WalkabilityCache
{
    // typedef StaticDynArray<IPhysicalEntity*, 768> Entities;
    public class Entities : List<IPhysicalEntity> { }
    // typedef StaticDynArray<AABB, 768> AABBs;
    public class AABBs : List<AABB> { }

    public WalkabilityCache(uint actorID) { m_actorID = actorID; }

    public void Reset(bool resetFloorCache = true) { /* impl in .cpp */ }
    public bool Cache(AABB aabb) { return false; /* impl in .cpp */ }
    public void Draw() { /* impl in .cpp */ }

    public AABB GetAABB() { return m_aabb; }
    public bool FullyContaints(AABB aabb) { return false; /* impl in .cpp */ }

    public nuint GetOverlapping(AABB aabb, Entities entities) { return 0; /* impl in .cpp */ }
    public nuint GetOverlapping(AABB aabb, Entities entities, AABBs aabbs) { return 0; /* impl in .cpp */ }

    public bool IsFloorCached(Vec3 position, out Vec3 floor) { floor = position; return false; /* impl in .cpp */ }

    public bool FindFloor(Vec3 position, out Vec3 floor) { floor = position; return false; /* impl in .cpp */ }
    public bool FindFloor(Vec3 position, out Vec3 floor, IPhysicalEntity[] entities, AABB[] aabbs, nuint entityCount, AABB enclosingAABB)
    { floor = position; return false; /* impl in .cpp */ }
    public bool CheckWalkability(Vec3 origin, Vec3 target, float radius, out Vec3 finalFloor, out bool flatFloor)
    { finalFloor = target; flatFloor = false; return false; /* impl in .cpp */ }
    public bool OverlapTorsoSegment(Vec3 start, Vec3 end, float radius, IPhysicalEntity[] entities, nuint entityCount) { return false; /* impl in .cpp */ }

    public nuint GetMemoryUsage() { return 0; /* impl in .cpp */ }

    private Vec3 m_center;
    private AABB m_aabb;

    private nuint m_entititesHash;
    private uint m_actorID;

    private Entities m_entities = new Entities();
    private AABBs m_aabbs = new AABBs();

    private FloorHeightCache m_floorCache = new FloorHeightCache();
}
