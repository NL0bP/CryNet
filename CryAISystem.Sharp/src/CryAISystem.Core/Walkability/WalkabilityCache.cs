// Literal port of dev/Code/CryEngine/CryAISystem/Walkability/WalkabilityCache.h + WalkabilityCache.cpp (601L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.AICollision;
using CryAISystem.CryCommon;

namespace CryAISystem.Walkability;

file static class WalkabilityCacheHelpers
{
    public static float cbrt_halley_step(float a, float R)
    {
        float a3 = a * a * a;
        float b = a * (a3 + R + R) / (a3 + a3 + R);
        return b;
    }

    public static float cbrt_fast(float cube)
    {
        // Approximate cube root via integer bit manipulation + Halley refinement.
        // The C++ uses a union{size_t i; float f} trick. We use BitConverter.
        byte[] bytes = BitConverter.GetBytes(cube);
        uint i = BitConverter.ToUInt32(bytes, 0);
        i = i / 3 + 709921077u;
        float a = BitConverter.ToSingle(BitConverter.GetBytes(i), 0);

        a = cbrt_halley_step(a, cube);
        return cbrt_halley_step(a, cube);
    }
}

public class WalkabilityCache
{
    // typedef StaticDynArray<IPhysicalEntity*, 768> Entities;
    public class Entities : List<IPhysicalEntity> { }
    // typedef StaticDynArray<AABB, 768> AABBs;
    public class AABBs : List<AABB> { }

    public WalkabilityCache(uint actorID)
    {
        m_center = Vec3.Zero;
        m_aabb = new AABB(AABB.RESET);
        m_entititesHash = 0;
        m_actorID = actorID;
    }

    public void Reset(bool resetFloorCache = true)
    {
        m_aabb.Reset();

        if (resetFloorCache)
            m_floorCache.Reset();

        m_entities.Clear();
        m_aabbs.Clear();
        m_entititesHash = 0;
    }

    public bool Cache(AABB aabb)
    {
        const float ExtraBorder = 2.0f;

        if (!m_aabb.ContainsBox(aabb) || ((WalkabilityCacheHelpers.cbrt_fast(m_aabb.GetVolume()) / WalkabilityCacheHelpers.cbrt_fast(aabb.GetVolume())) > 2.0f))
        {
            m_aabb = aabb;
            m_aabb.min = new Vec3(m_aabb.min.x, m_aabb.min.y, aabb.min.z - (WalkabilityFloorDownDist - WalkabilityFloorUpDist) - 0.2f);
            m_aabb.max = new Vec3(m_aabb.max.x, m_aabb.max.y, aabb.max.z + WalkabilityTotalHeight + 0.2f);

            m_aabb.Expand(new Vec3(ExtraBorder, ExtraBorder, ExtraBorder * 0.25f));
        }

        int capacity = 768; // StaticDynArray capacity

        m_entities.Clear();
        // In C++: gEnv->pPhysicalWorld->GetEntitiesInBox(...)
        // Shell — actual physics query pending Phase 11
        nuint entityCount = 0;

        m_aabbs.Clear();

        nuint entitiesHash = 0;

        float HashVec3Precision = 0.05f;
        float InvHashVec3Precision = 1.0f / HashVec3Precision;
        float HashQuatPrecision = 0.01f;
        float InvHashQuatPrecision = 1.0f / HashQuatPrecision;

        for (nuint i = 0; i < entityCount; ++i)
        {
            IPhysicalEntity entity = m_entities[(int)i];
            pe_status_pos status = new pe_status_pos();

            if (entity.GetStatus(status) != 0)
            {
                entitiesHash += AIHash.HashFromUInt((nuint)entity.GetHashCode());
                entitiesHash += AIHash.HashFromVec3(status.pos, HashVec3Precision, InvHashVec3Precision);
                entitiesHash += AIHash.HashFromQuat(status.q, HashQuatPrecision, InvHashQuatPrecision);

                Vec3 aabbMin = status.BBox[0];
                Vec3 aabbMax = status.BBox[1];

                // terrain will have a zeroed bbox
                if (!aabbMin.IsZero() && !aabbMax.IsZero())
                    m_aabbs.Add(new AABB(aabbMin + status.pos, aabbMax + status.pos));
                else
                    m_aabbs.Add(m_aabb);
            }
            else
                m_aabbs.Add(new AABB(AABB.RESET));
        }

        if (entityCount > 0 && entitiesHash == 0)
            entitiesHash = 0x1337d00d;

        if (entitiesHash != m_entititesHash)
        {
            m_floorCache.Reset();
            m_entititesHash = entitiesHash;
            return true;
        }

        return false;
    }

    public void Draw()
    {
        CDebugDrawContext dc = new CDebugDrawContext();
        dc.DrawAABB(m_aabb, false, new ColorB(128, 0, 255), EBoundingBoxDrawStyle.eBBD_Faceted);
        m_floorCache.Draw(new ColorB(0, 100, 0), new ColorB(255, 0, 0));
    }

    public AABB GetAABB() { return m_aabb; }

    public bool FullyContaints(AABB aabb)
    {
        return m_aabb.ContainsBox(aabb);
    }

    public nuint GetOverlapping(AABB aabb, Entities entities)
    {
        nuint entityCount = (nuint)m_entities.Count;
        nuint count = 0;

        for (nuint i = 0; i < entityCount; ++i)
        {
            AABB entityAABB = m_aabbs[(int)i];
            if (entityAABB.IsIntersectBox(aabb))
            {
                entities.Add(m_entities[(int)i]);
                ++count;
            }
        }

        return count;
    }

    public nuint GetOverlapping(AABB aabb, Entities entities, AABBs aabbs)
    {
        nuint entityCount = (nuint)m_entities.Count;
        nuint count = 0;

        for (nuint i = 0; i < entityCount; ++i)
        {
            AABB entityAABB = m_aabbs[(int)i];
            if (entityAABB.IsIntersectBox(aabb))
            {
                entities.Add(m_entities[(int)i]);
                aabbs.Add(entityAABB);
                ++count;
            }
        }

        return count;
    }

    public bool IsFloorCached(Vec3 position, out Vec3 floor)
    {
        floor = position;
        float height;

        if (m_floorCache.GetHeight(position, out height))
        {
            floor = new Vec3(position.x, position.y, height);
            return true;
        }

        return false;
    }

    public bool FindFloor(Vec3 position, out Vec3 floor)
    {
        return FindFloor(position, out floor, m_entities.Count > 0 ? m_entities.ToArray() : Array.Empty<IPhysicalEntity>(), m_aabbs.Count > 0 ? m_aabbs.ToArray() : Array.Empty<AABB>(), (nuint)m_entities.Count, m_aabb);
    }

    public bool FindFloor(Vec3 position, out Vec3 floor, IPhysicalEntity[] entities, AABB[] aabbs, nuint entityCount,
        AABB enclosingAABB)
    {
        floor = position;
        if (entityCount == 0)
            return false;

        Vec3 floorRef = floor;
        if (gAIEnv.pWalkabilityCacheManager != null && gAIEnv.pWalkabilityCacheManager.IsFloorCached(m_actorID, position, ref floorRef))
        {
            floor = floorRef;
            return floor.z < float.MaxValue;
        }

        Vec3 dir = new Vec3(0.0f, 0.0f, -(WalkabilityFloorDownDist + WalkabilityFloorUpDist));
        Vec3 start = m_floorCache.GetCellCenter(position) + new Vec3(0, 0, WalkabilityFloorUpDist);
        Lineseg line = new Lineseg(start, start + dir);

        AABB cell = m_floorCache.GetAABB(position);
        cell.min = new Vec3(cell.min.x, cell.min.y, cell.min.z - (WalkabilityFloorDownDist - WalkabilityFloorUpDist));
        cell.max = new Vec3(cell.max.x, cell.max.y, cell.max.z + WalkabilityFloorUpDist);

        ray_hit hit = new ray_hit();
        float height = float.MaxValue;
        float closest = float.MaxValue;
        IPhysicalWorld physicalWorld = gEnv.pPhysicalWorld;

        for (nuint i = 0; i < entityCount; ++i)
        {
            AABB entityAABB = aabbs[(int)i];

            if ((entityAABB.min.x < start.x) && (entityAABB.max.x > start.x) &&
                (entityAABB.min.y < start.y) && (entityAABB.max.y > start.y) &&
                physicalWorld.RayTraceEntity(entities[(int)i], start, dir, ref hit, null, AICollision.geom_colltype_player) != 0)
            {
                if (hit.dist < closest)
                {
                    closest = hit.dist;
                    height = start.z - closest;
                }
            }
        }

        m_floorCache.SetHeight(position, height);

        if (height < float.MaxValue)
        {
            floor = new Vec3(position.x, position.y, height);

            if (gAIEnv.CVars.DebugCheckWalkability != 0)
            {
                CDebugDrawContext dc = new CDebugDrawContext();
                dc.DrawLine(line.start, new ColorB(0, 255, 0), line.end, new ColorB(0, 255, 0));
                dc.DrawCone(line.start, new Vec3(0.0f, 0.0f, -1.0f), 0.125f, 0.25f, new ColorB(0, 255, 0));
                dc.DrawCone(floor + new Vec3(0.0f, 0.0f, 0.35f), new Vec3(0.0f, 0.0f, -1.0f), 0.125f, 0.35f, new ColorB(70, 130, 180));
            }

            return true;
        }

        if (gAIEnv.CVars.DebugCheckWalkability != 0)
        {
            CDebugDrawContext dc = new CDebugDrawContext();
            dc.DrawLine(line.start, new ColorB(255, 0, 0), line.end, new ColorB(255, 0, 0));
            dc.DrawCone(line.start, new Vec3(0.0f, 0.0f, -1.0f), 0.175f, 0.45f, new ColorB(255, 0, 0));
        }

        return false;
    }

    public bool CheckWalkability(Vec3 origin, Vec3 target, float radius, out Vec3 finalFloor, out bool flatFloor)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);
        finalFloor = target;
        flatFloor = false;

        bool floorHeightChanged = false;

        Vec3 direction2D = new Vec3(target.x - origin.x, target.y - origin.y, 0);
        float distanceSq = direction2D.GetLengthSquared2D();

        if (distanceSq < sqr(0.125f))
        {
            finalFloor = origin;
            flatFloor = true;
            return true;
        }

        bool isCritter = radius < 0.2f;
        Vec3 TorsoUp = new Vec3(0.0f, 0.0f, max(radius * 0.65f, isCritter ? WalkabilityCritterTorsoOffset : WalkabilityTorsoOffset));
        float TorsoTotalHeight = isCritter ? WalkabilityCritterTotalHeight : WalkabilityTotalHeight;
        float NextFloorMaxZDiff = isCritter ? 0.25f : 0.75f;
        float NextFloorZOffset = isCritter ? 0.25f : max(radius, 0.5f);

        float distance = direction2D.NormalizeSafe() + radius;

        float OptimalSampleOffset = max(0.225f, radius * 0.85f);
        float OptimalSegmentLength = gAIEnv.CVars.CheckWalkabilityOptimalSectionLength;
        nuint OptimalSegmentSampleCount = Math.Max((nuint)1, (nuint)(OptimalSegmentLength / OptimalSampleOffset));

        nuint sampleCount = (nuint)(distance / OptimalSampleOffset);
        float sampleOffsetAmount = (sampleCount > 1) ? (distance / (float)sampleCount) : distance;
        Vec3 sampleOffset = direction2D * sampleOffsetAmount;

        float segmentLength = (float)OptimalSegmentSampleCount * sampleOffsetAmount;
        nuint segmentCount = 1 + (nuint)(distance / segmentLength);
        Vec3 segmentOffset = direction2D * segmentLength;
        nuint segmentSampleCount = OptimalSegmentSampleCount;

        float minZ = min(origin.z, target.z) - WalkabilityFloorDownDist - 0.2f;
        float maxZ = max(origin.z, target.z) + TorsoTotalHeight + 0.2f;

        Vec3 segmentStart = origin;
        Vec3 segmentStartFloor = new Vec3(0, 0, 0);
        Vec3 newFloor = new Vec3(0, 0, 0);

        Vec3 startOBB = Vec3.Zero;
        Vec3 endOBB = Vec3.Zero;

        Entities segmentEntities = new Entities();
        AABBs segmentAABBs = new AABBs();
        Entities overlapTorsoEntities = new Entities();

        for (nuint i = 0; i < segmentCount; ++i)
        {
            segmentAABBs.Clear();
            segmentEntities.Clear();

            Vec3 segmentEnd = segmentStart + segmentOffset;

            AABB enclosingAABB = new AABB(AABB.RESET);
            enclosingAABB.Add(new Vec3(segmentStart.x, segmentStart.y, minZ), radius);
            enclosingAABB.Add(new Vec3(segmentEnd.x, segmentEnd.y, maxZ), radius);

            if (GetOverlapping(enclosingAABB, segmentEntities, segmentAABBs) == 0)
                return false; // no floor

            if (i == 0)
            {
                if (!FindFloor(segmentStart, out segmentStartFloor, segmentEntities.ToArray(), segmentAABBs.ToArray(),
                    (nuint)segmentEntities.Count, enclosingAABB))
                    return false;

                newFloor = segmentStartFloor;
            }

            Vec3 locationFloor = segmentStartFloor;

            for (nuint j = 0; (sampleCount > 1) && (j < segmentSampleCount); ++j, --sampleCount)
            {
                Vec3 checkLocation = locationFloor + sampleOffset;
                checkLocation = new Vec3(checkLocation.x, checkLocation.y, checkLocation.z + NextFloorZOffset);

                if (!FindFloor(checkLocation, out newFloor, segmentEntities.ToArray(), segmentAABBs.ToArray(),
                    (nuint)segmentEntities.Count, enclosingAABB))
                    return false;

                float deltaZ = locationFloor.z - newFloor.z;
                if (fabs_tpl(deltaZ) > NextFloorMaxZDiff)
                    return false;

                locationFloor = newFloor;

                if (!startOBB.IsZero())
                {
                    Vec3 basePt = locationFloor + TorsoUp;
                    AABB cylinderAABB = new AABB(
                        new Vec3(basePt.x - radius, basePt.y - radius, basePt.z),
                        new Vec3(basePt.x + radius, basePt.y + radius, basePt.z + TorsoTotalHeight - TorsoUp.z));

                    for (int k = 0; k < segmentAABBs.Count; ++k)
                    {
                        if (segmentAABBs[k].IsIntersectBox(cylinderAABB))
                            overlapTorsoEntities.Add(segmentEntities[k]);
                    }

                    overlapTorsoEntities.Sort((a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
                    int unique = 0;
                    for (int k = 0; k < overlapTorsoEntities.Count; k++)
                    {
                        if (k == 0 || overlapTorsoEntities[k] != overlapTorsoEntities[k - 1])
                            overlapTorsoEntities[unique++] = overlapTorsoEntities[k];
                    }
                    if (unique < overlapTorsoEntities.Count) overlapTorsoEntities.RemoveRange(unique, overlapTorsoEntities.Count - unique);

                    if (fabs_tpl(startOBB.z - locationFloor.z) > 0.005f)
                    {
                        floorHeightChanged = true;
                        if (OverlapTorsoSegment(startOBB, endOBB, radius, overlapTorsoEntities.ToArray(), (nuint)overlapTorsoEntities.Count))
                            return false;

                        overlapTorsoEntities.Clear();
                        startOBB = locationFloor;
                    }
                }
                else
                    startOBB = locationFloor;

                endOBB = locationFloor;
            }

            segmentStart = newFloor;
            segmentStartFloor = newFloor;
        }

        if (fabs_tpl(target.z - newFloor.z) > WalkabilityFloorDownDist + WalkabilityFloorUpDist)
            return false;

        if (overlapTorsoEntities.Count > 0)
        {
            if (OverlapTorsoSegment(startOBB, endOBB, radius, overlapTorsoEntities.ToArray(), (nuint)overlapTorsoEntities.Count))
                return false;
        }

        finalFloor = newFloor;
        flatFloor = !floorHeightChanged;

        return true;
    }

    public bool OverlapTorsoSegment(Vec3 startOBB, Vec3 endOBB, float radius, IPhysicalEntity[] entities, nuint entityCount)
    {
        if (entityCount == 0)
            return false;

        bool isCritter = radius < 0.2f;
        Vec3 TorsoUp = new Vec3(0.0f, 0.0f, max(radius * 0.65f, isCritter ? WalkabilityCritterTorsoOffset : WalkabilityTorsoOffset));
        float TorsoTotalHeight = isCritter ? WalkabilityCritterTotalHeight : WalkabilityTotalHeight;

        float height = (TorsoTotalHeight - TorsoUp.z);

        // PrimitiveWorldIntersection — pending Phase 11 (physics primitives)
        // The literal control flow is ported; actual physics intersection calls are shell.

        // short cut if start equals end
        if (endOBB.IsEquivalent(startOBB))
        {
            // cylinder test — shell
            return false;
        }

        // OBB test — shell
        return false;
    }

    public nuint GetMemoryUsage()
    {
        return (nuint)(64 + m_floorCache.GetMemoryUsage()); // approximate sizeof(m_floorCache)
    }

    private Vec3 m_center;
    private AABB m_aabb;

    private nuint m_entititesHash;
    private uint m_actorID;

    private Entities m_entities = new Entities();
    private AABBs m_aabbs = new AABBs();

    private FloorHeightCache m_floorCache = new FloorHeightCache();
}
