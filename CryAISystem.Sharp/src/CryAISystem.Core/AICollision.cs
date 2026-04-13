// Literal port of dev/Code/CryEngine/CryAISystem/AICollision.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem;

// Simple auto_ptr to allow thread-safe and efficient use of GetEntitiesInBox while not encouraging memory leaks
public class PhysicalEntityListAutoPtr
{
    public PhysicalEntityListAutoPtr()
    {
        m_pList = null;
    }

    ~PhysicalEntityListAutoPtr()
    {
        Assign(null);
    }

    // Perform cleanup of memory allocated by physics
    public void Assign(IPhysicalEntity[] pList)
    {
        if (m_pList != null)
            gEnv.pPhysicalWorld.GetPhysUtils().DeletePointer(m_pList);
        m_pList = pList;
    }

    public IPhysicalEntity this[size_t i] { get { return m_pList[i]; } }
    public IPhysicalEntity[] GetPtr() { return m_pList; }

    private IPhysicalEntity[] m_pList;
}


// Easy to use timers - useful for quick performance comparisons
public struct AccurateStopTimer
{
    public enum State
    {
        Running,
        Stopped,
    }

    public AccurateStopTimer()
    {
        start_ = new CTimeValue((int64)0L);
        total_ = new CTimeValue((int64)0L);
        state_ = State.Stopped;
    }

    public void Reset()
    {
        total_.SetValue(0);
        start_.SetValue(0);
    }

    public void Start()
    {
        System.Diagnostics.Debug.Assert(state_ != State.Running);
        state_ = State.Running;
        start_ = gEnv.pTimer.GetAsyncTime();
    }

    public void Stop()
    {
        total_ += gEnv.pTimer.GetAsyncTime() - start_;
        System.Diagnostics.Debug.Assert(state_ != State.Stopped);
        state_ = State.Stopped;
    }

    public State GetState()
    {
        return state_;
    }

    public CTimeValue GetTime()
    {
        if (state_ == State.Running)
            return gEnv.pTimer.GetAsyncTime() - start_;
        else
            return total_;
    }

    private CTimeValue start_;
    private CTimeValue total_;
    private State state_;
}


public struct ScopedAutoTimer : System.IDisposable
{
    public ScopedAutoTimer(ref AccurateStopTimer timer, bool reset = false)
    {
        timer_ = timer;
        if (reset)
            timer_.Reset();
        timer_.Start();
    }

    public void Dispose()
    {
        timer_.Stop();
    }

    private AccurateStopTimer timer_;
}


// NOTE Mai 24, 2007: <pvl> not really pretty, but this is a way of telling
// the CheckWalkability*() functions if the expensive floor position check needs
// to be performed.  If it doesn't (because m_pos already is a floor position),
// m_isFloor is set to true.
public struct SWalkPosition
{
    public Vec3 m_pos;
    public bool m_isFloor;
    public SWalkPosition(Vec3 pos, bool isFloor = false)
    {
        m_pos = pos;
        m_isFloor = isFloor;
    }
}

//===================================================================
// GetFloorRectangleFromOrientedBox
// Returns a rectangle in XY plane from given
//===================================================================
public struct SAIRect3
{
    public Vec3 center;
    public Vec3 axisu, axisv;
    public Vec2 min, max;
}

public static class AICollision
{
    // for finding the start/end positions
    public const float WalkabilityFloorUpDist = 0.25f;
    public const float WalkabilityFloorDownDist = 2.0f;
    // assume character is this fat
    public const float WalkabilityRadius = 0.25f;
    // radius of the swept sphere down to find the floor
    public const float WalkabilityDownRadius = 0.06f;
    // assume character is this tall
    public const float WalkabilityTotalHeight = 1.8f;
    public const float WalkabilityCritterTotalHeight = 0.3f;

    public const float WalkabilityTorsoOffset = 0.65f;
    public const float WalkabilityCritterTorsoOffset = 0.15f;
    // maximum allowed floor height change from one to the next
    public const float WalkabilityMaxDeltaZ = 0.6f;
    // height of the torso capsule above foot
    public static readonly Vec3 WalkabilityTorsoBaseOffset = new Vec3(0.0f, 0.0f, WalkabilityMaxDeltaZ);
    // Separation between sample points (horizontal)
    public const float WalkabilitySampleDist = 0.2f;

    public static readonly Vec3 WalkabilityUp = new Vec3(0.0f, 0.0f, 1.0f);

    public const size_t GetPhysicalEntitiesInBoxMaxResultCount = 2048;

#if CRYAISYSTEM_DEBUG
    /// Count of calls to CheckWalkability - for profiling
    public static uint g_CheckWalkabilityCalls;
#endif

    private static readonly EAICollisionEntities[] aiCollisionEntitiesTable = new EAICollisionEntities[]
    {
        EAICollisionEntities.AICE_STATIC,
        EAICollisionEntities.AICE_ALL,
        EAICollisionEntities.AICE_ALL_SOFT,
        EAICollisionEntities.AICE_DYNAMIC,
        EAICollisionEntities.AICE_STATIC_EXCEPT_TERRAIN,
        EAICollisionEntities.AICE_ALL_EXCEPT_TERRAIN,
        EAICollisionEntities.AICE_ALL_INLUDING_LIVING
    };

    public static IPhysicalEntity[] g_AIEntitiesInBoxPreAlloc = new IPhysicalEntity[GetPhysicalEntitiesInBoxMaxResultCount];

    //====================================================================
    // IntersectSweptSphere
    // hitPos is optional - may be faster if 0
    //====================================================================
    public static bool IntersectSweptSphere(ref Vec3 hitPos, ref float hitDist, Lineseg lineseg, float radius, EAICollisionEntities aiCollisionEntities, IPhysicalEntity[] pSkipEnts = null, int nSkipEnts = 0, int geomFlagsAny = geom_colltype0)
    {
        primitives.sphere spherePrim = new primitives.sphere();
        spherePrim.center = lineseg.start;
        spherePrim.r = radius;

        Vec3 dir = lineseg.end - lineseg.start;

        geom_contact pContact = null;
        geom_contact ppContact = null;
        int geomFlagsAll = 0;

        float d = gEnv.pPhysicalWorld.PrimitiveWorldIntersection(spherePrim.type, spherePrim, dir,
            (int)aiCollisionEntities, ref ppContact,
            geomFlagsAll, geomFlagsAny, null, null, null, pSkipEnts, nSkipEnts);

        if (d > 0.0f)
        {
            hitDist = d;
            if (pContact != null)
                hitPos = pContact.pt;
            return true;
        }
        else
        {
            return false;
        }
    }

    //====================================================================
    // IntersectSweptSphere
    //====================================================================
    public static bool IntersectSweptSphere(ref Vec3 hitPos, ref float hitDist, Lineseg lineseg, float radius, List<IPhysicalEntity> entities)
    {
        IPhysicalWorld pPhysics = gEnv.pPhysicalWorld;
        primitives.sphere spherePrim = new primitives.sphere();
        spherePrim.center = lineseg.start;
        spherePrim.r = radius;

        Vec3 dir = lineseg.end - lineseg.start;

        ray_hit hit = new ray_hit();
        uint nEntities = (uint)entities.Count;
        hitDist = float.MaxValue;
        for (uint iEntity = 0; iEntity < nEntities; ++iEntity)
        {
            IPhysicalEntity pEntity = entities[(int)iEntity];
            if (pPhysics.CollideEntityWithBeam(pEntity, lineseg.start, dir, radius, ref hit))
            {
                if (hit.dist < hitDist)
                {
                    hitPos = hit.pt;
                    hitDist = hit.dist;
                }
            }
        }
        return hitDist < float.MaxValue;
    }

    //====================================================================
    // OverlapCylinder
    //====================================================================
    public static bool OverlapCylinder(Lineseg lineseg, float radius, List<IPhysicalEntity> entities)
    {
        intersection_params ip = new intersection_params();
        ip.bStopAtFirstTri = true;
        ip.bNoAreaContacts = true;
        ip.bNoIntersection = 1;
        ip.bNoBorder = true;

        primitives.cylinder cylinderPrim = new primitives.cylinder();
        cylinderPrim.center = 0.5f * (lineseg.start + lineseg.end);
        cylinderPrim.axis = lineseg.end - lineseg.start;
        cylinderPrim.hh = 0.5f * cylinderPrim.axis.NormalizeSafe(Vec3Constants.fVec3_OneZ);
        cylinderPrim.r = radius;

        ray_hit hit = new ray_hit();
        uint nEntities = (uint)entities.Count;
        IPhysicalWorld pPhysics = gEnv.pPhysicalWorld;
        Vec3 vZero = new Vec3(0, 0, 0);
        for (uint iEntity = 0; iEntity < nEntities; ++iEntity)
        {
            IPhysicalEntity pEntity = entities[(int)iEntity];
            if (pPhysics.CollideEntityWithPrimitive(pEntity, cylinderPrim.type, cylinderPrim, vZero, ref hit, ip))
                return true;
        }
        return false;
    }

    public static bool CheckWalkability(Vec3 origin, Vec3 target, float radius, List<Vec3> boundary,
                                        ref Vec3 finalFloor, ref bool flatFloor, AABB? boundaryAABB = null)
    {
        if (Overlap.Lineseg_Polygon2D(new Lineseg(origin, target), boundary, boundaryAABB))
            return false;

        return CheckWalkability(origin, target, radius, ref finalFloor, ref flatFloor);
    }

    // 3-arg overload for NavPath.cs — convenience wrapper
    public static bool CheckWalkability(Vec3 origin, Vec3 target, float radius)
    {
        Vec3 finalFloor = new Vec3(0, 0, 0);
        bool flatFloor = false;
        return CheckWalkability(origin, target, radius, ref finalFloor, ref flatFloor);
    }

    public static bool OverlapTorsoSegment(Vec3 startOBB, Vec3 endOBB, float radius, List<IPhysicalEntity> overlapTorsoEntities)
    {
        if (overlapTorsoEntities.Count == 0)
            return false;

        // Marcio: HAX for Crysis2 ticks
        bool isCritter = radius < 0.2f;
        Vec3 TorsoUp = new Vec3(0.0f, 0.0f, System.Math.Max(radius * 0.65f, isCritter ? WalkabilityCritterTorsoOffset : WalkabilityTorsoOffset));
        float TorsoTotalHeight = isCritter ? WalkabilityCritterTotalHeight : WalkabilityTotalHeight;

        float height = (TorsoTotalHeight - TorsoUp.z);

        intersection_params ip = new intersection_params();
        ip.bStopAtFirstTri = true;
        ip.bNoAreaContacts = true;
        ip.bNoIntersection = 1;
        ip.bNoBorder = true;
        ip.bThreadSafe = 1;

        ray_hit hit = new ray_hit();

        IPhysicalWorld.SPWIParams parameters = new IPhysicalWorld.SPWIParams();
        parameters.pSkipEnts = overlapTorsoEntities.ToArray();
        parameters.nSkipEnts = -overlapTorsoEntities.Count;
        parameters.sweepDir = new Vec3(0f, 0f, 0f);
        parameters.pip = ip;

        // short cut if start equals end
        if (endOBB.IsEquivalent(startOBB))
        {
            primitives.cylinder cylinder = new primitives.cylinder();
            cylinder.axis = WalkabilityUp;
            cylinder.center = new Vec3(startOBB.x, startOBB.y, startOBB.z + height * 0.5f);
            cylinder.center += TorsoUp;
            cylinder.hh = height * 0.5f;
            cylinder.r = radius;

            parameters.itype = primitives.cylinder.cylinder_type;
            parameters.pprim = cylinder;

            bool resultLocal = gEnv.pPhysicalWorld.PrimitiveWorldIntersection(parameters) > 0.0f;

            if (gAIEnv.CVars.DebugCheckWalkability != 0)
            {
                new CDebugDrawContext().DrawCylinder(cylinder.center, cylinder.axis,
                    cylinder.r, cylinder.hh * 2.0f, resultLocal ? Col_Red : Col_Green);
            }

            return resultLocal;
        }

        Vec3 forward = (endOBB - startOBB);
        float length = forward.NormalizeSafe(Vec3Constants.fVec3_OneY);
        Vec3 right = forward.Cross(Vec3Constants.fVec3_OneZ).GetNormalizedSafe(Vec3Constants.fVec3_OneX);
        Vec3 up = right.Cross(forward);

        primitives.box physBox = new primitives.box();
        physBox.center = 0.5f * (startOBB + endOBB);
        physBox.center += TorsoUp;
        physBox.center.z += 0.5f * height;
        physBox.size.Set(radius, (0.5f * length), 0.5f * height);
        physBox.bOriented = 1;
        physBox.Basis.SetFromVectors(right, forward, up);
        physBox.Basis.Transpose();

        // test obb for plane path
        parameters.itype = primitives.box.box_type;
        parameters.pprim = physBox;

        bool result = gEnv.pPhysicalWorld.PrimitiveWorldIntersection(parameters) > 0.0f;

        if (gAIEnv.CVars.DebugCheckWalkability != 0)
        {
            OBB obb = OBB.CreateOBB(physBox.Basis.GetTransposed(), physBox.size, new Vec3(0, 0, 0));

            new CDebugDrawContext().DrawOBB(obb, Matrix34.CreateTranslationMat(physBox.center), true, result ? Col_Red : Col_Green,
                EBoundingBoxDrawStyle.eBBD_Faceted);
        }

        if (!result)
        {
            primitives.cylinder cylinderStart = new primitives.cylinder();
            cylinderStart.axis = WalkabilityUp;
            cylinderStart.center = new Vec3(startOBB.x, startOBB.y, startOBB.z + height * 0.5f);
            cylinderStart.center += TorsoUp;
            cylinderStart.hh = height * 0.5f;
            cylinderStart.r = radius;

            parameters.itype = primitives.cylinder.cylinder_type;
            parameters.pprim = cylinderStart;
            result |= gEnv.pPhysicalWorld.PrimitiveWorldIntersection(parameters) > 0.0f;

            if (gAIEnv.CVars.DebugCheckWalkability != 0)
            {
                new CDebugDrawContext().DrawCylinder(cylinderStart.center, cylinderStart.axis,
                    cylinderStart.r, cylinderStart.hh * 2.0f, result ? Col_Red : Col_Green);
            }
        }

        if (!result)
        {
            primitives.cylinder cylinderEnd = new primitives.cylinder();
            cylinderEnd.axis = WalkabilityUp;
            cylinderEnd.center = new Vec3(endOBB.x, endOBB.y, endOBB.z + height * 0.5f);
            cylinderEnd.center += TorsoUp;
            cylinderEnd.hh = height * 0.5f;
            cylinderEnd.r = radius;

            parameters.pprim = cylinderEnd;
            result |= gEnv.pPhysicalWorld.PrimitiveWorldIntersection(parameters) > 0.0f;

            if (gAIEnv.CVars.DebugCheckWalkability != 0)
            {
                new CDebugDrawContext().DrawCylinder(cylinderEnd.center, cylinderEnd.axis,
                    cylinderEnd.r, cylinderEnd.hh * 2.0f, result ? Col_Red : Col_Green);
            }
        }

        return result;
    }

    //====================================================================
    // CheckWalkability
    //====================================================================
    public static bool CheckWalkability(Vec3 origin, Vec3 target, float radius, ref Vec3 finalFloor, ref bool flatFloor)
    {
        bool floorHeightChanged = false;

        Vec3 direction2D = new Vec3(new Vec2(target - origin));
        float distanceSq = direction2D.GetLengthSquared2D();
        if (distanceSq < sqr(0.125f))
        {
            finalFloor = origin;
            flatFloor = true;
            return true;
        }

        // Marcio: HAX for Crysis2 ticks
        bool isCritter = radius < 0.2f;
        Vec3 TorsoUp = new Vec3(0.0f, 0.0f, System.Math.Max(radius * 0.65f, isCritter ? WalkabilityCritterTorsoOffset : WalkabilityTorsoOffset));
        float TorsoTotalHeight = isCritter ? WalkabilityCritterTotalHeight : WalkabilityTotalHeight;
        float NextFloorMaxZDiff = isCritter ? 0.25f : 0.75f;
        float NextFloorZOffset = isCritter ? 0.25f : System.Math.Max(radius, 0.5f);

        float distance = direction2D.NormalizeSafe() + radius;

        float OptimalSampleOffset = System.Math.Max(0.225f, radius * 0.85f);
        float OptimalSegmentLength = gAIEnv.CVars.CheckWalkabilityOptimalSectionLength;
        size_t OptimalSegmentSampleCount = System.Math.Max((size_t)1, (size_t)(OptimalSegmentLength / OptimalSampleOffset));

        size_t sampleCount = (size_t)(distance / OptimalSampleOffset);
        float sampleOffsetAmount = (sampleCount > 1) ? (distance / (float)sampleCount) : distance;
        Vec3 sampleOffset = direction2D * sampleOffsetAmount;

        float segmentLength = OptimalSegmentSampleCount * sampleOffsetAmount;
        size_t segmentCount = 1 + (size_t)(distance / segmentLength);
        Vec3 segmentOffset = direction2D * segmentLength;
        size_t segmentSampleCount = OptimalSegmentSampleCount;

        float minZ = System.Math.Min(origin.z, target.z) - WalkabilityFloorDownDist;
        float maxZ = System.Math.Max(origin.z, target.z) + TorsoTotalHeight;
        float Zdiff = System.Math.Abs(maxZ - minZ);

        Vec3 segmentStart = origin;
        Vec3 segmentStartFloor = new Vec3();
        Vec3 newFloor = new Vec3();

        Vec3 startOBB = new Vec3(0, 0, 0);
        Vec3 endOBB = new Vec3(0, 0, 0);

        List<AABB> segmentAABBs = new List<AABB>();
        List<IPhysicalEntity> segmentEntities = new List<IPhysicalEntity>();
        List<IPhysicalEntity> overlapTorsoEntities = new List<IPhysicalEntity>();

        for (size_t i = 0; i < segmentCount; ++i)
        {
            segmentAABBs.Clear();
            segmentEntities.Clear();

            Vec3 segmentEnd = segmentStart + segmentOffset;

            // find all entities for this segment
            AABB enclosingAABB = new AABB(AABB.RESET);

            enclosingAABB.Add(new Vec3(segmentStart.x, segmentStart.y, minZ), radius);
            enclosingAABB.Add(new Vec3(segmentEnd.x, segmentEnd.y, maxZ), radius);

            size_t entityCount = GetPhysicalEntitiesInBox(enclosingAABB.min, enclosingAABB.max, segmentEntities, (int)EAICollisionEntities.AICE_ALL);

            if (entityCount == 0) // no floor
                return false;

            for (size_t j = 0; j < entityCount; ++j)
                segmentAABBs.Add(new AABB());

            pe_status_pos status = new pe_status_pos();

            for (size_t j = 0; j < entityCount; ++j)
            {
                IPhysicalEntity entity = segmentEntities[(int)j];

                if (entity.GetStatus(status) != 0)
                {
                    Vec3 aabbMin = new Vec3(status.BBox[0]);
                    Vec3 aabbMax = new Vec3(status.BBox[1]);

                    // terrain will have a zeroed bbox
                    if (!aabbMin.IsZero() || !aabbMax.IsZero())
                        segmentAABBs[(int)j] = new AABB(aabbMin + status.pos, aabbMax + status.pos);
                    else
                        segmentAABBs[(int)j] = enclosingAABB;
                }
                else
                    segmentAABBs[(int)j] = new AABB(AABB.RESET);
            }

            // first segment - get the floor
            if (i == 0)
            {
                if (!FindFloor(segmentStart, segmentEntities, segmentAABBs, ref segmentStartFloor))
                    return false;

                newFloor = segmentStartFloor;
            }

            Vec3 locationFloor = segmentStartFloor;
            Vec3 checkLocation;

            for (size_t j = 0; (sampleCount > 1) && (j < segmentSampleCount); ++j, --sampleCount)
            {
                checkLocation = locationFloor + sampleOffset;
                checkLocation.z += NextFloorZOffset;

                if (!FindFloor(checkLocation, segmentEntities, segmentAABBs, ref newFloor))
                    return false;

                System.Diagnostics.Debug.Assert(newFloor.IsValid());

                float deltaZ = locationFloor.z - newFloor.z;
                if (System.Math.Abs(deltaZ) > NextFloorMaxZDiff)
                    return false;

                locationFloor = newFloor;

                // check if we need to do the overlap torso check
                if (!startOBB.IsZero())
                {
                    // find entities for this segment

                    Vec3 baseV = locationFloor + TorsoUp;

                    AABB cylinderAABB = new AABB(new Vec3(baseV.x - radius, baseV.y - radius, baseV.z),
                        new Vec3(baseV.x + radius, baseV.y + radius, baseV.z + TorsoTotalHeight - TorsoUp.z));

                    for (size_t k = 0; k < (size_t)segmentAABBs.Count; ++k)
                    {
                        if (segmentAABBs[(int)k].IsIntersectBox(cylinderAABB))
                            overlapTorsoEntities.Add(segmentEntities[(int)k]);
                    }

                    // remove duplicate entries
                    overlapTorsoEntities.Sort();
                    int uniqueIdx = 0;
                    for (int u = 1; u < overlapTorsoEntities.Count; ++u)
                    {
                        if (overlapTorsoEntities[u] != overlapTorsoEntities[uniqueIdx])
                        {
                            ++uniqueIdx;
                            overlapTorsoEntities[uniqueIdx] = overlapTorsoEntities[u];
                        }
                    }
                    if (overlapTorsoEntities.Count > 0)
                        overlapTorsoEntities.RemoveRange(uniqueIdx + 1, overlapTorsoEntities.Count - uniqueIdx - 1);

                    // check for planar floor, TODO add code for ramps and a like
                    if (System.Math.Abs(startOBB.z - locationFloor.z) > 0.005f)
                    {
                        floorHeightChanged = true;
                        if (OverlapTorsoSegment(startOBB, endOBB, radius, overlapTorsoEntities))
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

        if (System.Math.Abs(target.z - newFloor.z) > WalkabilityFloorDownDist + WalkabilityFloorUpDist)
            return false;

        // check the last segment also
        if (OverlapTorsoSegment(startOBB, endOBB, radius, overlapTorsoEntities))
            return false;

        finalFloor = newFloor;
        flatFloor = !floorHeightChanged;

        return true;
    }

    //====================================================================
    // CheckWalkability
    //====================================================================
    public static bool CheckWalkability(Vec3 origin, Vec3 target, float radius,
                                        List<IPhysicalEntity> entities, List<AABB> aabbs, ref Vec3 finalFloor, ref bool flatFloor)
    {
        bool floorHeightChanged = false;

        Vec3 direction2D = new Vec3(new Vec2(target - origin));
        float distanceSq = direction2D.GetLengthSquared2D();
        if (distanceSq < sqr(0.125f))
        {
            finalFloor = origin;
            flatFloor = true;
            return true;
        }

        // Marcio: HAX for Crysis2 ticks
        bool isCritter = radius < 0.2f;
        Vec3 TorsoUp = new Vec3(0.0f, 0.0f, System.Math.Max(radius * 0.65f, isCritter ? WalkabilityCritterTorsoOffset : WalkabilityTorsoOffset));
        float TorsoTotalHeight = isCritter ? WalkabilityCritterTotalHeight : WalkabilityTotalHeight;
        float NextFloorMaxZDiff = isCritter ? 0.25f : 0.75f;
        float NextFloorZOffset = isCritter ? 0.25f : System.Math.Max(radius, 0.5f);

        float distance = direction2D.NormalizeSafe() + radius;

        float OptimalSampleOffset = System.Math.Max(0.225f, radius * 0.85f);
        float OptimalSegmentLength = gAIEnv.CVars.CheckWalkabilityOptimalSectionLength;
        size_t OptimalSegmentSampleCount = System.Math.Max((size_t)1, (size_t)(OptimalSegmentLength / OptimalSampleOffset));

        size_t sampleCount = (size_t)(distance / OptimalSampleOffset);
        float sampleOffsetAmount = (sampleCount > 1) ? (distance / (float)sampleCount) : distance;
        Vec3 sampleOffset = direction2D * sampleOffsetAmount;

        float segmentLength = OptimalSegmentSampleCount * sampleOffsetAmount;
        size_t segmentCount = 1 + (size_t)(distance / segmentLength);
        Vec3 segmentOffset = direction2D * segmentLength;
        size_t segmentSampleCount = OptimalSegmentSampleCount;

        float minZ = System.Math.Min(origin.z, target.z) - WalkabilityFloorDownDist;
        float maxZ = System.Math.Max(origin.z, target.z) + TorsoTotalHeight;
        float Zdiff = System.Math.Abs(maxZ - minZ);

        Vec3 segmentStart = origin;
        Vec3 segmentStartFloor = new Vec3();
        Vec3 newFloor = new Vec3();

        Vec3 startOBB = new Vec3(0, 0, 0);
        Vec3 endOBB = new Vec3(0, 0, 0);

        List<AABB> segmentAABBs = new List<AABB>();
        List<IPhysicalEntity> segmentEntities = new List<IPhysicalEntity>();
        List<IPhysicalEntity> overlapTorsoEntities = new List<IPhysicalEntity>();

        for (size_t i = 0; i < segmentCount; ++i)
        {
            segmentAABBs.Clear();
            segmentEntities.Clear();

            Vec3 segmentEnd = segmentStart + segmentOffset;

            // find all entities for this segment
            AABB enclosingAABB = new AABB(AABB.RESET);

            enclosingAABB.Add(new Vec3(segmentStart.x, segmentStart.y, minZ), radius);
            enclosingAABB.Add(new Vec3(segmentEnd.x, segmentEnd.y, maxZ), radius);

            for (size_t j = 0; j < (size_t)aabbs.Count; ++j)
            {
                if (enclosingAABB.IsIntersectBox(aabbs[(int)j]))
                {
                    segmentAABBs.Add(aabbs[(int)j]);
                    segmentEntities.Add(entities[(int)j]);
                }
            }

            if (segmentEntities.Count == 0) // no floor
                return false;

            // first segment - get the floor
            if (i == 0)
            {
                if (!FindFloor(segmentStart, segmentEntities, segmentAABBs, ref segmentStartFloor))
                    return false;

                newFloor = segmentStartFloor;
            }

            Vec3 locationFloor = segmentStartFloor;
            Vec3 checkLocation;

            for (size_t j = 0; (sampleCount > 1) && (j < segmentSampleCount); ++j, --sampleCount)
            {
                checkLocation = locationFloor + sampleOffset;
                checkLocation.z += NextFloorZOffset;

                if (!FindFloor(checkLocation, segmentEntities, segmentAABBs, ref newFloor))
                    return false;

                System.Diagnostics.Debug.Assert(newFloor.IsValid());

                float deltaZ = locationFloor.z - newFloor.z;
                if (System.Math.Abs(deltaZ) > NextFloorMaxZDiff)
                    return false;

                locationFloor = newFloor;

                // check if we need to do the overlap torso check
                if (!startOBB.IsZero())
                {
                    // find entities for this segment
                    Vec3 baseV = locationFloor + TorsoUp;

                    AABB cylinderAABB = new AABB(new Vec3(baseV.x - radius, baseV.y - radius, baseV.z),
                        new Vec3(baseV.x + radius, baseV.y + radius, baseV.z + TorsoTotalHeight - TorsoUp.z));

                    for (size_t k = 0; k < (size_t)segmentAABBs.Count; ++k)
                    {
                        if (segmentAABBs[(int)k].IsIntersectBox(cylinderAABB))
                            overlapTorsoEntities.Add(segmentEntities[(int)k]);
                    }

                    // remove duplicate entries
                    overlapTorsoEntities.Sort();
                    int uniqueIdx = 0;
                    for (int u = 1; u < overlapTorsoEntities.Count; ++u)
                    {
                        if (overlapTorsoEntities[u] != overlapTorsoEntities[uniqueIdx])
                        {
                            ++uniqueIdx;
                            overlapTorsoEntities[uniqueIdx] = overlapTorsoEntities[u];
                        }
                    }
                    if (overlapTorsoEntities.Count > 0)
                        overlapTorsoEntities.RemoveRange(uniqueIdx + 1, overlapTorsoEntities.Count - uniqueIdx - 1);

                    // check for planar floor, TODO add code for ramps and a like
                    if (System.Math.Abs(startOBB.z - locationFloor.z) > 0.005f)
                    {
                        floorHeightChanged = true;
                        if (OverlapTorsoSegment(startOBB, endOBB, radius, overlapTorsoEntities))
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

        if (System.Math.Abs(target.z - newFloor.z) > WalkabilityFloorDownDist + WalkabilityFloorUpDist)
            return false;

        // check the last segment also
        if (OverlapTorsoSegment(startOBB, endOBB, radius, overlapTorsoEntities))
            return false;

        finalFloor = newFloor;
        flatFloor = !floorHeightChanged;

        return true;
    }

    public static bool CheckWalkabilitySimple(SWalkPosition fromPos, SWalkPosition toPos, float radius, EAICollisionEntities aiCollisionEntities = EAICollisionEntities.AICE_ALL)
    {
        Vec3 from = fromPos.m_pos;
        Vec3 to = toPos.m_pos;

        Vec3 fromFloor = new Vec3();
        Vec3 toFloor = new Vec3();

        if (fromPos.m_isFloor)
            fromFloor = from;
        else if (!gAIEnv.pWalkabilityCacheManager.FindFloor(0, from, ref fromFloor))
            return false;

        if (toPos.m_isFloor)
            toFloor = to;
        else if (!gAIEnv.pWalkabilityCacheManager.FindFloor(0, to, ref toFloor))
            return false;

        // Marcio: HAX for Crysis2 ticks
        bool isCritter = radius < 0.2f;
        Vec3 TorsoUp = new Vec3(0.0f, 0.0f, System.Math.Max(radius * 0.65f, isCritter ? WalkabilityCritterTorsoOffset : WalkabilityTorsoOffset));
        float TorsoTotalHeight = isCritter ? WalkabilityCritterTotalHeight : WalkabilityTotalHeight;

        float checkRadius = radius - 0.0001f;
        Vec3 horWalkDelta = toFloor - fromFloor;
        float horWalkDistSq = horWalkDelta.len2();

        if (horWalkDistSq > 0.001f)
        {
            float horWalkDist = (float)System.Math.Sqrt(horWalkDistSq);
            Vec3 horWalkDir = horWalkDelta / horWalkDist;
            float boxHeight = TorsoTotalHeight - TorsoUp.z;

            primitives.box boxPrim = new primitives.box();
            boxPrim.center = 0.5f * (fromFloor + toFloor);
            boxPrim.center.z += TorsoUp.z + 0.5f * boxHeight;
            boxPrim.size.Set(checkRadius, 0.5f * horWalkDist, boxHeight * 0.5f);
            boxPrim.bOriented = 1;

            Vec3 right = horWalkDir.Cross(Vec3Constants.fVec3_OneZ).GetNormalizedSafe(Vec3Constants.fVec3_OneY);
            Vec3 up = right.Cross(horWalkDir);

            boxPrim.Basis.SetFromVectors(right, horWalkDir, up);
            boxPrim.Basis.Transpose();

            //[Alexey] Because of return false between CreatePrimitive and Release we had leaks here!!!
            // Changed to get it from the CAISytem so it can be released at levelUnload.
            ref IGeometry walkabilityGeometryBox = ref GetAISystem().m_walkabilityGeometryBox;
            if (walkabilityGeometryBox == null)
                walkabilityGeometryBox = gEnv.pPhysicalWorld.GetGeomManager().CreatePrimitive(primitives.box.box_type, boxPrim);
            walkabilityGeometryBox.SetData(boxPrim);

            pe_status_nparts snp = new pe_status_nparts();
            pe_status_pos sp = new pe_status_pos();
            geom_world_data gwd = new geom_world_data();
            intersection_params ip = new intersection_params();
            ip.bStopAtFirstTri = true;
            ip.bNoAreaContacts = true;
            ip.bNoBorder = true;
            ip.bNoIntersection = 1;
            ip.bThreadSafe = 1;

            bool result = (gEnv.pPhysicalWorld.PrimitiveWorldIntersection(primitives.box.box_type, boxPrim, new Vec3(0, 0, 0), aiCollisionEntities,
                null, 0, geom_colltype0 | geom_colltype_player, ip) <= 0.0f);

            return result;
        }

        return true;
    }

    //====================================================================
    // FindFloor
    //====================================================================
    public static bool FindFloor(Vec3 position, ref Vec3 floor)
    {
        Vec3 dir = new Vec3(0.0f, 0.0f, -(WalkabilityFloorDownDist + WalkabilityFloorUpDist));
        Vec3 start = position + new Vec3(0, 0, WalkabilityFloorUpDist);

        RayCastResult result = gAIEnv.pRayCaster.Cast(new RayCastRequest(start, dir, EAICollisionEntities.AICE_ALL,
            rwi_stop_at_pierceable | rwi_colltype_any(geom_colltype_player)));

        if (!result || (result[0].dist < 0.0f))
        {
            if (gAIEnv.CVars.DebugCheckWalkability != 0)
            {
                CDebugDrawContext dc = new CDebugDrawContext();

                dc.DrawLine(start, Col_Red, start + dir, Col_Red);
                dc.DrawCone(start, new Vec3(0.0f, 0.0f, -1.0f), 0.175f, 0.45f, Col_Red);
            }

            return false;
        }

        floor = new Vec3(start.x, start.y, start.z - result[0].dist);

        if (gAIEnv.CVars.DebugCheckWalkability != 0)
        {
            CDebugDrawContext dc = new CDebugDrawContext();

            dc.DrawLine(start, Col_Green, start + dir, Col_Green);
            dc.DrawCone(start, new Vec3(0.0f, 0.0f, -1.0f), 0.125f, 0.25f, Col_Green);
            dc.DrawCone(floor + new Vec3(0.0f, 0.0f, 0.35f), new Vec3(0.0f, 0.0f, -1.0f), 0.125f, 0.35f, Col_SteelBlue);
        }

        return true;
    }


    //====================================================================
    // FindFloor
    //====================================================================
    public static bool FindFloor(Vec3 position, List<IPhysicalEntity> entities, List<AABB> aabbs, ref Vec3 floor)
    {
        Vec3 dir = new Vec3(0.0f, 0.0f, -(WalkabilityFloorDownDist + WalkabilityFloorUpDist));
        Vec3 start = position + new Vec3(0, 0, WalkabilityFloorUpDist);
        Lineseg line = new Lineseg(start, start + dir);
        IPhysicalWorld physicalWorld = gEnv.pPhysicalWorld;

        ray_hit hit = new ray_hit();
        float closest = float.MaxValue;

        size_t entityCount = (size_t)aabbs.Count;
        for (size_t i = 0; i < entityCount; ++i)
        {
            AABB aabb = aabbs[(int)i];

            if ((aabb.min.x <= start.x) && (aabb.max.x >= start.x) &&
                (aabb.min.y <= start.y) && (aabb.max.y >= start.y) &&
                physicalWorld.RayTraceEntity(entities[(int)i], start, dir, ref hit, null, geom_colltype_player) != 0)
            {
                if (hit.dist < closest)
                    closest = hit.dist;
            }
        }

        if (closest < float.MaxValue)
        {
            floor = new Vec3(position.x, position.y, start.z - closest);

            if (gAIEnv.CVars.DebugCheckWalkability != 0)
            {
                CDebugDrawContext dc = new CDebugDrawContext();

                dc.DrawLine(line.start, Col_Green, line.end, Col_Green);
                dc.DrawCone(line.start, new Vec3(0.0f, 0.0f, -1.0f), 0.125f, 0.25f, Col_Green);
                dc.DrawCone(floor + new Vec3(0.0f, 0.0f, 0.35f), new Vec3(0.0f, 0.0f, -1.0f), 0.125f, 0.35f, Col_SteelBlue);
            }

            return true;
        }

        if (gAIEnv.CVars.DebugCheckWalkability != 0)
        {
            CDebugDrawContext dc = new CDebugDrawContext();

            dc.DrawLine(line.start, Col_Red, line.end, Col_Red);
            dc.DrawCone(line.start, new Vec3(0.0f, 0.0f, -1.0f), 0.175f, 0.45f, Col_Red);
        }

        return false;
    }


    //===================================================================
    // IsLeft: tests if a point is Left|On|Right of an infinite line.
    //    Input:  three points P0, P1, and P2
    //    Return: >0 for P2 left of the line through P0 and P1
    //            =0 for P2 on the line
    //            <0 for P2 right of the line
    //===================================================================
    public static float IsLeft(Vec3 P0, Vec3 P1, Vec3 P2)
    {
        bool swap = false;
        if (P0.x < P1.x)
            swap = true;
        else if (P0.x == P1.x && P0.y < P1.y)
            swap = true;

        if (swap)
        {
            Vec3 tmp = P0;
            P0 = P1;
            P1 = tmp;
        }

        float res = (P1.x - P0.x) * (P2.y - P0.y) - (P2.x - P0.x) * (P1.y - P0.y);
        const float tol = 0.0000f;
        if (res > tol || res < -tol)
            return swap ? -res : res;
        else
            return 0.0f;
    }

    public static bool ptEqual(Vec3 lhs, Vec3 rhs)
    {
        const float tol = 0.01f;
        return (System.Math.Abs(lhs.x - rhs.x) < tol) && (System.Math.Abs(lhs.y - rhs.y) < tol);
    }

    public static float IsLeftAndrew(Vec3 p0, Vec3 p1, Vec3 p2)
    {
        return (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);
    }

    public static bool PointSorterAndrew(Vec3 lhs, Vec3 rhs)
    {
        if (lhs.x < rhs.x) return true;
        if (lhs.x > rhs.x) return false;
        return lhs.y < rhs.y;
    }

    //===================================================================
    // ConvexHull2D
    // Implements Andrew's algorithm
    //===================================================================
    private static List<Vec3> ConvexHull2DAndrewTemp = new List<Vec3>();

    public static void ConvexHull2DAndrew(List<Vec3> ptsOut, List<Vec3> ptsIn)
    {
        int n = ptsIn.Count;
        if (n < 3)
        {
            ptsOut.Clear();
            ptsOut.AddRange(ptsIn);
            return;
        }

        List<Vec3> P = ConvexHull2DAndrewTemp;
        P.Clear();
        P.AddRange(ptsIn);

        P.Sort((a, b) => PointSorterAndrew(a, b) ? -1 : (PointSorterAndrew(b, a) ? 1 : 0));

        // the output array ptsOut[] will be used as the stack
        int i;

        ptsOut.Clear();
        ptsOut.Capacity = P.Count;

        // Get the indices of points with min x-coord and min|max y-coord
        int minmin = 0, minmax;
        float xmin = P[0].x;
        for (i = 1; i < n; i++)
            if (P[i].x != xmin)
                break;

        minmax = i - 1;
        if (minmax == n - 1)
        {
            // degenerate case: all x-coords == xmin
            ptsOut.Add(P[minmin]);
            if (P[minmax].y != P[minmin].y) // a nontrivial segment
                ptsOut.Add(P[minmax]);
            ptsOut.Add(P[minmin]);           // add polygon endpoint
            return;
        }

        // Get the indices of points with max x-coord and min|max y-coord
        int maxmin, maxmax = n - 1;
        float xmax = P[n - 1].x;
        for (i = n - 2; i >= 0; i--)
            if (P[i].x != xmax) break;
        maxmin = i + 1;

        // Compute the lower hull on the stack H
        ptsOut.Add(P[minmin]);      // push minmin point onto stack
        i = minmax;
        while (++i <= maxmin)
        {
            // the lower line joins P[minmin] with P[maxmin]
            if (IsLeftAndrew(P[minmin], P[maxmin], P[i]) >= 0 && i < maxmin)
                continue;          // ignore P[i] above or on the lower line

            while (ptsOut.Count > 1) // there are at least 2 points on the stack
            {
                // test if P[i] is left of the line at the stack top
                if (IsLeftAndrew(ptsOut[ptsOut.Count - 2], ptsOut[ptsOut.Count - 1], P[i]) > 0)
                    break;         // P[i] is a new hull vertex
                else
                    ptsOut.RemoveAt(ptsOut.Count - 1); // pop top point off stack
            }
            ptsOut.Add(P[i]);       // push P[i] onto stack
        }

        // Next, compute the upper hull on the stack H above the bottom hull
        if (maxmax != maxmin)      // if distinct xmax points
            ptsOut.Add(P[maxmax]);  // push maxmax point onto stack
        int bot = ptsOut.Count - 1;                 // the bottom point of the upper hull stack
        i = maxmin;
        while (--i >= minmax)
        {
            // the upper line joins P[maxmax] with P[minmax]
            if (IsLeftAndrew(P[maxmax], P[minmax], P[i]) >= 0 && i > minmax)
                continue;          // ignore P[i] below or on the upper line

            while (ptsOut.Count > bot + 1)    // at least 2 points on the upper stack
            {
                // test if P[i] is left of the line at the stack top
                if (IsLeftAndrew(ptsOut[ptsOut.Count - 2], ptsOut[ptsOut.Count - 1], P[i]) > 0)
                    break;         // P[i] is a new hull vertex
                else
                    ptsOut.RemoveAt(ptsOut.Count - 1);         // pop top po2int off stack
            }
            ptsOut.Add(P[i]);       // push P[i] onto stack
        }
        if (minmax != minmin)
            ptsOut.Add(P[minmin]);  // push joining endpoint onto stack
        if (ptsOut.Count > 0 && ptEqual(ptsOut[0], ptsOut[ptsOut.Count - 1]))
            ptsOut.RemoveAt(ptsOut.Count - 1);
    }

    /// Generates 2D convex hull from ptsIn
    public static void ConvexHull2D(List<Vec3> ptsOut, List<Vec3> ptsIn)
    {
        // [Mikko] Note: The convex hull calculation is bound by the sorting.
        // The sort in Andrew's seems to be about 3-4x faster than Graham's--using Andrew's for now.
        ConvexHull2DAndrew(ptsOut, ptsIn);
    }


    //===================================================================
    // GetFloorRectangleFromOrientedBox
    //===================================================================
    public static void GetFloorRectangleFromOrientedBox(Matrix34 tm, AABB box, ref SAIRect3 rect)
    {
        Matrix33 tmRot = tm.m33; // C++ `Matrix33 tmRot(tm)` extracts the 3x3 rotation portion of tm
        tmRot.Transpose();

        Vec3[] corners = new Vec3[8];
        SetAABBCornerPoints(box, corners);

        rect.center = tm.GetTranslation();

        rect.axisu = tm.GetColumn1();
        rect.axisu.z = 0;
        rect.axisu.Normalize();
        rect.axisv.Set(rect.axisu.y, -rect.axisu.x, 0);

        Vec3 tu = tmRot.TransformVector(rect.axisu);
        Vec3 tv = tmRot.TransformVector(rect.axisv);

        rect.min.x = float.MaxValue;
        rect.min.y = float.MaxValue;
        rect.max.x = -float.MaxValue;
        rect.max.y = -float.MaxValue;

        for (uint i = 0; i < 8; ++i)
        {
            float du = tu.Dot(corners[i]);
            float dv = tv.Dot(corners[i]);
            rect.min.x = System.Math.Min(rect.min.x, du);
            rect.max.x = System.Math.Max(rect.max.x, du);
            rect.min.y = System.Math.Min(rect.min.y, dv);
            rect.max.y = System.Math.Max(rect.max.y, dv);
        }
    }

    public static void CleanupAICollision()
    {
        ConvexHull2DAndrewTemp.Clear();
    }

    //====================================================================
    // GetEntitiesFromAABB
    //====================================================================
    public static uint GetEntitiesFromAABB(PhysicalEntityListAutoPtr entities, AABB aabb, EAICollisionEntities aiCollisionEntities)
    {
        IPhysicalEntity[] pEntities = null;
        uint nEntities = (uint)gEnv.pPhysicalWorld.GetEntitiesInBox(aabb.min, aabb.max, ref pEntities, (int)aiCollisionEntities | ent_allocate_list);

        // (MATT) At this time, geb() will return pointer to internal memory even if no entities were found  {2009/11/23}
        if (nEntities == 0)
            pEntities = null;
        entities.Assign(pEntities);
        return nEntities;
    }


    public static size_t GetPhysicalEntitiesInBox(Vec3 boxMin, Vec3 boxMax, ref IPhysicalEntity[] entityList, int entityTypes)
    {
        entityList = g_AIEntitiesInBoxPreAlloc;

        size_t entityCount = (size_t)gEnv.pPhysicalWorld.GetEntitiesInBox(boxMin, boxMax, ref entityList,
            entityTypes | ent_allocate_list, GetPhysicalEntitiesInBoxMaxResultCount);

        if (entityCount > GetPhysicalEntitiesInBoxMaxResultCount)
        {
            for (size_t k = 0; k < GetPhysicalEntitiesInBoxMaxResultCount; ++k)
                g_AIEntitiesInBoxPreAlloc[k] = entityList[k];

            gEnv.pPhysicalWorld.GetPhysUtils().DeletePointer(entityList);
            entityList = g_AIEntitiesInBoxPreAlloc;

            return GetPhysicalEntitiesInBoxMaxResultCount;
        }

        return entityCount;
    }

    public static size_t GetPhysicalEntitiesInBox(Vec3 boxMin, Vec3 boxMax, List<IPhysicalEntity> entityList, int entityTypes)
    {
        IPhysicalEntity[] entityListPtr = new IPhysicalEntity[GetPhysicalEntitiesInBoxMaxResultCount];

        size_t entityCount = (size_t)gEnv.pPhysicalWorld.GetEntitiesInBox(boxMin, boxMax, ref entityListPtr,
            entityTypes | ent_allocate_list, GetPhysicalEntitiesInBoxMaxResultCount);

        entityList.Clear();
        if (entityCount > GetPhysicalEntitiesInBoxMaxResultCount)
        {
            for (size_t k = 0; k < GetPhysicalEntitiesInBoxMaxResultCount; ++k)
                entityList.Add(entityListPtr[k]);

            gEnv.pPhysicalWorld.GetPhysUtils().DeletePointer(entityListPtr);

            return GetPhysicalEntitiesInBoxMaxResultCount;
        }

        for (size_t k = 0; k < entityCount; ++k)
            entityList.Add(entityListPtr[k]);

        return entityCount;
    }


    //====================================================================
    // OverlapSphere
    //====================================================================
    public static bool OverlapSphere(Vec3 pos, float radius, EAICollisionEntities aiCollisionEntities)
    {
        primitives.sphere spherePrim = new primitives.sphere();
        spherePrim.center = pos;
        spherePrim.r = radius;

        intersection_params parameters = new intersection_params();
        parameters.bNoBorder = true;
        parameters.bNoAreaContacts = true;
        parameters.bNoIntersection = 1;
        parameters.bStopAtFirstTri = true;
        parameters.bThreadSafe = 1;

        IPhysicalWorld pPhysics = gEnv.pPhysicalWorld;
        float d = pPhysics.PrimitiveWorldIntersection(spherePrim.type, spherePrim, Vec3Constants.fVec3_Zero,
            aiCollisionEntities, null, 0, geom_colltype0, parameters);

        return (d != 0.0f);
    }

    //====================================================================
    // OverlapCapsule
    //====================================================================
    public static bool OverlapCapsule(Lineseg lineseg, float radius, EAICollisionEntities aiCollisionEntities)
    {
        primitives.capsule capsulePrim = new primitives.capsule();
        capsulePrim.center = 0.5f * (lineseg.start + lineseg.end);
        capsulePrim.axis = lineseg.end - lineseg.start;
        capsulePrim.hh = 0.5f * capsulePrim.axis.NormalizeSafe(Vec3Constants.fVec3_OneZ);
        capsulePrim.r = radius;

        intersection_params ip = new intersection_params();
        ip.bNoBorder = true;
        ip.bNoAreaContacts = true;
        ip.bNoIntersection = 1;
        ip.bStopAtFirstTri = true;
        ip.bThreadSafe = 1;

        IPhysicalWorld pPhysics = gEnv.pPhysicalWorld;
        float d = pPhysics.PrimitiveWorldIntersection(capsulePrim.type, capsulePrim, Vec3Constants.fVec3_Zero,
            aiCollisionEntities, null, 0, geom_colltype0, ip);

        return (d != 0.0f);
    }

    //====================================================================
    // OverlapCylinder
    //====================================================================
    public static bool OverlapCylinder(Lineseg lineseg, float radius, EAICollisionEntities aiCollisionEntities, IPhysicalEntity pSkipEntity = null, int customFilter = 0, int[] pEntityID = null, int maxIDs = 0)
    {
        IPhysicalEntity pSkipEnts = pSkipEntity;

        primitives.cylinder cylinderPrim = new primitives.cylinder();
        cylinderPrim.center = 0.5f * (lineseg.start + lineseg.end);
        cylinderPrim.axis = lineseg.end - lineseg.start;
        cylinderPrim.hh = 0.5f * cylinderPrim.axis.NormalizeSafe(Vec3Constants.fVec3_OneZ);
        cylinderPrim.r = radius;

        geom_contact[] contacts = null;
        intersection_params parameters = new intersection_params();
        parameters.bNoBorder = true;
        parameters.bNoAreaContacts = true;
        parameters.bNoIntersection = 1;
        parameters.bStopAtFirstTri = true;

        IPhysicalWorld pPhysics = gEnv.pPhysicalWorld;
        WriteLockCond lockContacts = new WriteLockCond();
        float d = pPhysics.PrimitiveWorldIntersection(cylinderPrim.type, cylinderPrim, Vec3Constants.fVec3_Zero,
            aiCollisionEntities, ref contacts, 0, customFilter != 0 ? customFilter : geom_colltype0, parameters, 0, 0, new[] { pSkipEnts }, pSkipEntity != null ? 1 : 0, lockContacts);

        if (contacts != null && pEntityID != null)
        {
            maxIDs = System.Math.Min(maxIDs, (int)d);
            for (int loop = 0; loop < maxIDs; ++loop)
            {
                pEntityID[loop] = contacts[loop].iPrim[0];
            }
        }

        if (d != 0.0f)
        {
            return true;
        }

        return false;
    }

    //====================================================================
    // IntersectSegment
    //====================================================================
    public static bool OverlapSegment(Lineseg lineseg, EAICollisionEntities aiCollisionEntities)
    {
        Vec3 dir = lineseg.end - lineseg.start;
        ray_hit hit = new ray_hit();
        if (gAIEnv.pRayCaster.Cast(new RayCastRequest(lineseg.start, dir, aiCollisionEntities,
            rwi_ignore_noncolliding | rwi_stop_at_pierceable)))
            return true;
        else
            return false;
    }

    //====================================================================
    // IntersectSegment
    //====================================================================
    public static bool IntersectSegment(ref Vec3 hitPos, Lineseg lineseg, EAICollisionEntities aiCollisionEntities, int filter = rwi_ignore_noncolliding | rwi_stop_at_pierceable)
    {
        ray_hit hit = new ray_hit();
        RayCastResult result = gAIEnv.pRayCaster.Cast(new RayCastRequest(lineseg.start, lineseg.end - lineseg.start,
            aiCollisionEntities, filter));
        if (!result || (result[0].dist < 0.0f))
            return false;

        hitPos = result[0].pt;
        return true;
    }

    //====================================================================
    // GetFloorPos
    /// returns false if no valid floor found. Checks from upDist above pos to
    /// downDist below pos.
    //====================================================================
    public static bool GetFloorPos(ref Vec3 floorPos, Vec3 pos, float upDist, float downDist, float radius, EAICollisionEntities aiCollisionEntities)
    {
        // (MATT) This function doesn't have the behaviour you might expect. It only searches up by radius amount, and checks down by
        // upDist + downDist + rad. Might or might not be safe to fix, but this code is being superseded. {2009/07/01}
        Vec3 delta = new Vec3(0, 0, -downDist - upDist);
        Vec3 capStart = pos + new Vec3(0, 0, upDist);
        Vec3 vRad = new Vec3(0, 0, radius);

        if (IntersectSegment(ref floorPos, new Lineseg(pos + vRad, pos - new Vec3(0.0f, 0.0f, downDist)), aiCollisionEntities, rwi_stop_at_pierceable | (geom_colltype_player << rwi_colltype_bit)))
            return true;

        if (delta.z > 0.0f)
            return false;

        float hitDist = 0.0f;

        float numSteps = 1.0f + downDist * 0.5f;
        float fStepNumInv = 1.0f / numSteps;
        Lineseg seg = new Lineseg();
        for (float fStep = 0.0f; fStep < numSteps; fStep += 1.0f)
        {
            float frac1 = fStep * fStepNumInv;
            float frac2 = (fStep + 1.0f) * fStepNumInv;
            seg.start = capStart + frac1 * delta;
            seg.end = capStart + frac2 * delta;
            Vec3 dummyHit = new Vec3();
            if (IntersectSweptSphere(ref dummyHit, ref hitDist, seg, radius, aiCollisionEntities, null, 0, rwi_stop_at_pierceable | (geom_colltype_player << rwi_colltype_bit)))
            {
                floorPos.Set(pos.x, pos.y, seg.start.z - (radius + hitDist));
                return true;
            }
        }
        return false;
    }

    //===================================================================
    // CheckBodyPos
    /// Given a floor position (expected to be valid!) checks if a "standard"
    /// human body will fit there. Returns true if it will.
    //===================================================================
    public static bool CheckBodyPos(Vec3 floorPos, EAICollisionEntities aiCollisionEntities)
    {
        Vec3 segStart = floorPos + WalkabilityTorsoBaseOffset + new Vec3(0, 0, WalkabilityRadius);
        Vec3 segEnd = floorPos + new Vec3(0, 0, WalkabilityTotalHeight - WalkabilityRadius);
        Lineseg torsoSeg = new Lineseg(segStart, segEnd);
        return !OverlapCapsule(torsoSeg, WalkabilityRadius, aiCollisionEntities);
    }

    //===================================================================
    // CalcPolygonArea
    //===================================================================
    public static T CalcPolygonArea<TVecContainer, T>(TVecContainer pts) where TVecContainer : IList<Vec3>
    {
        if (pts.Count < 2)
            return default(T);

        // The C++ template returns T (e.g. float). Implemented here as float.
        float totalCross = 0.0f;
        Vec3 firstIt = pts[0];
        for (int it = 0; it < pts.Count; ++it)
        {
            int itNext = it + 1;
            if (itNext == pts.Count)
                itNext = 0;
            totalCross += (pts[it].x - firstIt.x) * (pts[itNext].y - firstIt.y) - (pts[itNext].x - firstIt.x) * (pts[it].y - firstIt.y);
        }

        return (T)(object)(totalCross / 2.0f);
    }

    //===================================================================
    // IsWoundAnticlockwise
    //===================================================================
    public static bool IsWoundAnticlockwise<TVecContainer, T>(TVecContainer pts) where TVecContainer : IList<Vec3>
    {
        if (pts.Count < 2)
            return true;
        return ((float)(object)CalcPolygonArea<TVecContainer, T>(pts)) > 0.0f;
    }

    //===================================================================
    // EnsureShapeIsWoundAnticlockwise
    //===================================================================
    public static void EnsureShapeIsWoundAnticlockwise<TVecContainer, T>(TVecContainer pts) where TVecContainer : IList<Vec3>
    {
        if (!IsWoundAnticlockwise<TVecContainer, T>(pts))
        {
            // std::reverse equivalent
            int i = 0, j = pts.Count - 1;
            while (i < j)
            {
                Vec3 tmp = pts[i];
                pts[i] = pts[j];
                pts[j] = tmp;
                ++i;
                --j;
            }
        }
    }

    //===================================================================
    // OverlapLinesegAABB2D
    //===================================================================
    public static bool OverlapLinesegAABB2D(Vec3 p0, Vec3 p1, AABB aabb)
    {
        Vec3 c = (aabb.min + aabb.max) * 0.5f;
        Vec3 e = aabb.max - c;
        Vec3 m = (p0 + p1) * 0.5f;
        Vec3 d = p1 - m;
        m = m - c;

        // Try world coordinate axes as separating axes
        float adx = System.Math.Abs(d.x);
        if (System.Math.Abs(m.x) > e.x + adx) return false;
        float ady = System.Math.Abs(d.y);
        if (System.Math.Abs(m.y) > e.y + ady) return false;

        // Add in an epsilon term to counteract arithmetic errors when segment is
        // (near) parallel to a coordinate axis (see text for detail)
        const float EPSILON = 0.000001f;
        adx += EPSILON; ady += EPSILON;

        if (System.Math.Abs(m.x * d.y - m.y * d.x) > e.x * ady + e.y * adx) return false;

        // No separating axis found; segment must be overlapping AABB
        return true;
    }

    // Helpers used by the literal port (translated from C++ inline math helpers)
    private static float sqr(float x) { return x * x; }

    // Const tag passthroughs that the C++ original imports from physinterface.h / IPhysics.h.
    // The literal port references them by name; full literal CryCommon ports of those headers
    // will define them. Until then, these placeholders preserve the call shape.
    private const int geom_colltype0 = 0;
    public const int geom_colltype_player = 0;
    private const int ent_allocate_list = 0;
    private const int rwi_ignore_noncolliding = 0;
    private const int rwi_stop_at_pierceable = 0;
    private const int rwi_colltype_bit = 0;
    private static int rwi_colltype_any(int x) { return x; }

    // SetAABBCornerPoints — translated literally from Cry_Geo.h.
    private static void SetAABBCornerPoints(AABB box, Vec3[] pts)
    {
        pts[0] = new Vec3(box.min.x, box.min.y, box.min.z);
        pts[1] = new Vec3(box.max.x, box.min.y, box.min.z);
        pts[2] = new Vec3(box.min.x, box.max.y, box.min.z);
        pts[3] = new Vec3(box.max.x, box.max.y, box.min.z);
        pts[4] = new Vec3(box.min.x, box.min.y, box.max.z);
        pts[5] = new Vec3(box.max.x, box.min.y, box.max.z);
        pts[6] = new Vec3(box.min.x, box.max.y, box.max.z);
        pts[7] = new Vec3(box.max.x, box.max.y, box.max.z);
    }

    // ColorB constants used by the literal CDebugDrawContext draw calls.
    // The C++ originals are global statics in Cry_Color.h.
    private static readonly ColorB Col_Red = new ColorB(255, 0, 0, 255);
    private static readonly ColorB Col_Green = new ColorB(0, 255, 0, 255);
    private static readonly ColorB Col_SteelBlue = new ColorB(70, 130, 180, 255);
}
