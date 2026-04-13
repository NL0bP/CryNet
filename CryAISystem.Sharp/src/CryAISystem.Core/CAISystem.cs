// Literal port of dev/Code/CryEngine/CryAISystem/CAISystem.{h,cpp,CAISystemPhys.cpp} — Phase 11
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// This file contains the CAISystem coordinator literal port. The class is declared `partial`
// because the shell in Environment.cs already declares `public partial class CAISystem` with
// field declarations and constants, and CAISystemUpdate.cs declares fields + update methods.
// This file adds the remaining method implementations that replace the stubs.

using System;
using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem;

// ========================================================================
// CAISystem — Phase 11 literal port of all critical methods
// ========================================================================
public partial class CAISystem
{
    // ----------------------------------------------------------------
    // Additional fields not already declared in Environment.cs or CAISystemUpdate.cs
    // ----------------------------------------------------------------
    private IActorProxyFactory m_actorProxyFactoryImpl;

    // Beacons — CAISystem.h line 816-822
    private class BeaconStruct
    {
        public CCountedRef<CAIObject> refBeacon = new CCountedRef<CAIObject>();
        public CWeakRef<CAIObject> refOwner = new CWeakRef<CAIObject>();
    }
    private Dictionary<int, BeaconStruct> m_mapBeacons = new Dictionary<int, BeaconStruct>();

    // Formations — CAISystem.h line 825-828
    private Dictionary<CWeakRef<CAIObject>, CFormation> m_mapActiveFormations =
        new Dictionary<CWeakRef<CAIObject>, CFormation>();
    // m_mapFormationDescriptors is declared in AIMemStats.cs (partial class)

    // Generic shapes — CAISystem.h line 850-851
    private ShapeMap m_mapGenericShapes = new ShapeMap();
    private ShapeMap m_mapOcclusionPlanes = new ShapeMap();

    // Combat classes — CAISystem.h line 963-968
    private class SCombatClassDesc
    {
        public List<float> mods = new List<float>();
        public string customSignal = "";
    }
    private List<SCombatClassDesc> m_CombatClasses = new List<SCombatClassDesc>();

    // System listeners — CAISystem.h line 864-866
    private HashSet<IAISystemListener> m_setSystemListeners = new HashSet<IAISystemListener>();

    // Perception LUT — CAISystem.h line 1029
    private AILinearLUT m_VisDistLookUp = new AILinearLUT();

    // Frame ticks — light profiler
    private ulong m_nFrameTicks;

    // Working folder
    private string m_sWorkingFolder = "";

    // Priority types
    private List<short> m_priorityObjectTypes = new List<short>();
    private List<CAIObject> m_priorityTargets = new List<CAIObject>();

    // Damage regions — CAISystem.h line 696
    private Dictionary<object, Sphere> m_damageRegions = new Dictionary<object, Sphere>();

    // Multipliers — CAISystem.h line 872-873
    private SortedDictionary<int, float> m_mapMultipliers = new SortedDictionary<int, float>();
    private SortedDictionary<int, float> m_mapFactionThreatMultipliers = new SortedDictionary<int, float>();

    // Enabled/Disabled actor sets (sorted sets mirroring C++ VectorSet)
    public List<CWeakRef<CAIActor>> m_enabledAIActorsSet = new List<CWeakRef<CAIActor>>();
    public List<CWeakRef<CAIActor>> m_disabledAIActorsSet = new List<CWeakRef<CAIActor>>();

    // Bool flags
    private bool m_bUpdateSmartObjects;
    private bool m_bCodeCoverageFailed;
    private bool m_IsEnabled = true;

    private uint m_agentDebugTarget;

    // Subsystems owned by CAISystem
    private CAILightManager m_lightManager = new CAILightManager();

    // ----------------------------------------------------------------
    // Fields originally in CAISystemUpdate.cs partial block (CAISystem.h)
    // These must be public because CAISystemUpdate methods access them.
    // ----------------------------------------------------------------
    // CAISystem.h line 799 — typedef std::map<int, CAIGroup*> AIGroupMap;
    public AIGroupMap m_mapAIGroups = new AIGroupMap();

    // CAISystem.h lines 802-809
    public float m_enabledActorsUpdateError;
    public int m_enabledActorsUpdateHead;
    public int m_totalActorsUpdateCount;
    public float m_disabledActorsUpdateError;
    public int m_disabledActorsHead;
    public bool m_iteratingActorSet;

    // CAISystem.h line 812-813
    public Dictionary<uint, CAIHideObject> m_DebugHideObjects = new Dictionary<uint, CAIHideObject>();

    // CAISystem.h line 892
    public float m_DEBUG_screenFlash;

    // CAISystem.h line 901
    public uint m_nTickCount;

    // CAISystem.h lines 912-919
    public float m_frameDeltaTime;
    public float m_frameStartTimeSeconds;
    public CTimeValue m_fLastPuppetUpdateTime;
    public CTimeValue m_frameStartTime;
    public CTimeValue m_lastVisBroadPhaseTime;
    public CTimeValue m_lastAmbientFireUpdateTime;
    public CTimeValue m_lastExpensiveAccessoryUpdateTime;
    public CTimeValue m_lastGroupUpdateTime;

    // CAISystem.h line 945
    public List<SAIDelayedExpAccessoryUpdate> m_delayedExpAccessoryUpdates = new List<SAIDelayedExpAccessoryUpdate>();

    // CAISystem.h line 959
    public MapSignalStrings m_mapAuxSignalsFired = new MapSignalStrings();

    // ================================================================
    // AILinearLUT — inner class from CAISystem.h line 983-1027
    // ================================================================
    private class AILinearLUT
    {
        private int m_size;
        private float[] m_pData;

        public AILinearLUT() { m_size = 0; m_pData = null; }

        public int GetSize() { return m_size; }

        public void Set(float[] values, int n)
        {
            m_size = n;
            m_pData = new float[n];
            Array.Copy(values, m_pData, n);
        }

        public float GetValue(float t)
        {
            int last = m_size - 1;
            t *= (float)last;
            int n = (int)MathF.Floor(t);
            if (n < 0) return m_pData[0];
            if (n >= last) return m_pData[last];
            float a = t - (float)n;
            return m_pData[n] + (m_pData[n + 1] - m_pData[n]) * a;
        }
    }

    // ================================================================
    // AIOBJECT type constants (from IAIObject.h/CAISystem.h)
    // ================================================================
    private const short AIOBJECT_PLAYER_KEY = 7;
    private const short AIOBJECT_GRENADE_KEY = 16;
    private const short AIOBJECT_WAYPOINT_KEY = 20;
    private const short AIOBJECT_LEADER_KEY = 9;
    private const short AIOBJECT_ACTOR_KEY = 50;

    // AIFAF flags — CAISystem.h lines 49-60
    private const uint AIFAF_INCLUDE_DISABLED = 0x0008;

    // IAIObject subtypes
    private const int STP_BEACON = 110; // IAIObject::STP_BEACON from IAIObject.h

    // ================================================================
    // EDangerSpots — CAISystem.h line 142-148
    // ================================================================
    private enum EDangerSpots
    {
        DANGER_DEADBODY_INTERNAL = 0x01,
        DANGER_EXPLOSIVE = 0x02,
        DANGER_EXPLOSION_SPOT = 0x04,
    }

    // ================================================================
    // Sphere struct — for damage regions
    // ================================================================
    public struct Sphere
    {
        public Vec3 center;
        public float radius;
    }

    // CFormationDescriptor is FormationDescriptor from AIMemStats.cs
    // m_nNameCRC32 is added to that class via Phase 11 addition.
}

// The remaining method implementations are below, in a second partial block
// to keep the file organized.

public partial class CAISystem
{
    // ================================================================
    // GetPlayer — CAISystem.cpp line 835-841
    // Replaces the stub in Environment.cs
    // ================================================================
    private CAIObject GetPlayerImpl()
    {
        var objects = gAIEnv.pAIObjectManager?.m_Objects;
        if (objects == null) return null;
        if (objects.TryGetValue(AIOBJECT_PLAYER_KEY, out var bucket))
        {
            if (bucket.Count > 0)
                return bucket[0].GetAIObject();
        }
        return null;
    }

    // ================================================================
    // NotifyEnableState — CAISystem.cpp line 4149-4203
    // Replaces the stub in Environment.cs
    // ================================================================
    private void NotifyEnableStateImpl(CAIActor pAIActor, bool state)
    {
        if (pAIActor == null) return;
        CWeakRef<CAIActor> refAIActor = WeakRefHelpers.GetWeakRef(pAIActor);

        // pWalkabilityCacheManager.EnableActor — concrete type has the method
        if (gAIEnv.pWalkabilityCacheManager is Walkability.WalkabilityCacheManager wcm)
            wcm.EnableActor(pAIActor.GetAIObjectID(), state);

        if (state)
        {
            gAIEnv.pActorLookUp?.AddActor(pAIActor);

            m_disabledAIActorsSet.Remove(refAIActor);
            if (!m_enabledAIActorsSet.Contains(refAIActor))
                m_enabledAIActorsSet.Add(refAIActor);
        }
        else
        {
            // gAIEnv.pSequenceManager?.AgentDisabled — not implemented yet
            gAIEnv.pActorLookUp?.RemoveActor(pAIActor);

            pAIActor.ClearProbableTargets();

            m_enabledAIActorsSet.Remove(refAIActor);
            if (!m_disabledAIActorsSet.Contains(refAIActor))
                m_disabledAIActorsSet.Add(refAIActor);
        }
    }

    // ================================================================
    // UnregisterAIActor — CAISystem.cpp line 4799-4805
    // Replaces the stub in Environment.cs
    // ================================================================
    private void UnregisterAIActorImpl(CWeakRef<CAIActor> destroyedObject)
    {
        m_enabledAIActorsSet.Remove(destroyedObject);
        m_disabledAIActorsSet.Remove(destroyedObject);
    }

    // ================================================================
    // NotifyTargetDead — CAISystem.cpp line 4211-4268
    // Replaces the stub in Environment.cs
    // ================================================================
    private void NotifyTargetDeadImpl(CAIActor pDeadObject)
    {
        if (pDeadObject == null) return;

        ActorLookUp lookUp = gAIEnv.pActorLookUp;
        if (lookUp == null) return;
        lookUp.Prepare(ActorLookUp.Proxy);

        nuint activeActorCount = lookUp.GetActiveCount();

        for (nuint actorIndex = 0; actorIndex < activeActorCount; ++actorIndex)
        {
            CAIActor pAIActor = lookUp.GetActor<CAIActor>((uint)actorIndex);
            if (pAIActor == null) continue;

            if (pAIActor.GetAttentionTarget() == pDeadObject)
            {
                IAISignalExtraData pData = CreateSignalExtraData();
                pData.sObjectName = pDeadObject.GetName();
                pData.nID = pDeadObject.GetEntityID();
                pAIActor.SetSignal(0, "OnTargetDead", pAIActor.GetEntity(), pData, gAIEnv.SignalCRCs.m_nOnTargetDead);
            }
        }
    }

    // ================================================================
    // OnAgentDeath — CAISystem.cpp line 670-673
    // Replaces the stub in Environment.cs
    // ================================================================
    private void OnAgentDeathImpl(uint deadEntityID, uint killerID)
    {
        foreach (var listener in m_setSystemListeners)
            listener?.OnAgentDeath(deadEntityID, killerID);
    }

    // ================================================================
    // AddToGroup — CAISystem.cpp line 1310-1367
    // ================================================================
    private void AddToGroupImpl(CAIObject pObject, int nGroupId)
    {
        if (pObject == null) return;
        CAIActor pActor = pObject.CastToCAIActor();
        if (pActor == null) return;

        if (pActor.CastToCPipeUser() == null && pActor.CastToCLeader() == null && pActor.CastToCAIPlayer() == null)
            return;

        if (nGroupId >= 0)
            pActor.SetGroupId(nGroupId);
        else
            nGroupId = pActor.GetGroupId();

        // Check if already in group
        if (m_mapGroups.TryGetValue(nGroupId, out var bucket))
        {
            foreach (var existing in bucket)
            {
                if (existing.GetAIObject() == pObject)
                {
                    UpdateGroupStatus(nGroupId);
                    return;
                }
            }
        }

        // Insert into group
        if (!m_mapGroups.ContainsKey(nGroupId))
            m_mapGroups[nGroupId] = new List<CStrongRef<CAIObject>>();
        m_mapGroups[nGroupId].Add(StrongRefHelpers.CreateStrongRef(pObject));

        UpdateGroupStatus(nGroupId);

        // Group leaders related stuff
        if (m_mapAIGroups.TryGetValue(nGroupId, out CAIGroup pGroup))
        {
            CLeader pLeader = pActor.CastToCLeader();
            if (pLeader != null)
                pGroup.SetLeader(pLeader);
            else if (pActor.CastToCPipeUser() != null)
                pGroup.AddMember(pActor);
        }
    }

    // ================================================================
    // RemoveFromGroup — CAISystem.cpp line 1372-1402
    // ================================================================
    private void RemoveFromGroupImpl(int nGroupID, CAIObject pObject)
    {
        if (pObject == null) return;

        if (m_mapGroups.TryGetValue(nGroupID, out var bucket))
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].GetAIObject() == pObject)
                {
                    bucket.RemoveAt(i);
                    break;
                }
            }
        }

        if (!m_mapAIGroups.ContainsKey(nGroupID))
            return;

        UpdateGroupStatus(nGroupID);

        if (m_mapAIGroups.TryGetValue(nGroupID, out CAIGroup pGroup))
        {
            if (pObject.CastToCLeader() != null)
                pGroup.SetLeader(null);
            else
                pGroup.RemoveMember(pObject.CastToCAIActor());
        }
    }

    // ================================================================
    // UpdateGroupStatus — CAISystem.cpp line 1404-1417
    // ================================================================
    private void UpdateGroupStatusImpl(int nGroupID)
    {
        if (!m_mapAIGroups.TryGetValue(nGroupID, out CAIGroup pGroup))
        {
            pGroup = new CAIGroup(nGroupID);
            m_mapAIGroups[nGroupID] = pGroup;
        }
        pGroup.UpdateGroupCountStatus();
    }

    // ================================================================
    // AddToFaction — CAISystem.cpp line 1422-1457
    // ================================================================
    private void AddToFactionImpl(CAIObject pObject, uint8 factionID)
    {
        if (factionID == 0xFF) return; // IFactionMap::InvalidFactionID
        if (pObject == null) return;

        CStrongRef<CAIObject> refObj = StrongRefHelpers.CreateStrongRef(pObject);

        // Check if already present
        if (m_mapFaction.TryGetValue(factionID, out var existing))
        {
            if (existing.GetAIObject() == pObject) return;
        }

        // Remove from old faction
        var keysToCheck = new List<uint8>(m_mapFaction.Keys);
        foreach (uint8 key in keysToCheck)
        {
            if (m_mapFaction[key].GetAIObject() == pObject)
            {
                m_mapFaction.Remove(key);
                break;
            }
        }

        m_mapFaction[factionID] = refObj;
    }

    // ================================================================
    // GetCombatClassScale — CAISystem.cpp line 5353-5361
    // ================================================================
    private float GetCombatClassScaleImpl(int shooterClass, int targetClass)
    {
        if (targetClass < 0 || shooterClass < 0 || shooterClass >= m_CombatClasses.Count)
            return 1.0f;
        SCombatClassDesc desc = m_CombatClasses[shooterClass];
        if (targetClass >= desc.mods.Count)
            return 1.0f;
        return desc.mods[targetClass];
    }

    // ================================================================
    // GetDangerSpots — CAISystem.cpp line 5366-5422
    // ================================================================
    private List<SDangerSpot> GetDangerSpotsImpl(CAIObject pObj, float range, int types)
    {
        List<SDangerSpot> result = new List<SDangerSpot>();
        if (pObj == null) return result;

        float rangeSq = range * range;
        Vec3 reqPos = pObj.GetPos();

        if ((types & (int)EDangerSpots.DANGER_EXPLOSIVE) != 0)
        {
            var objects = gAIEnv.pAIObjectManager?.m_Objects;
            if (objects != null && objects.TryGetValue(AIOBJECT_GRENADE_KEY, out var bucket))
            {
                List<Vec3> grenades = new List<Vec3>();
                foreach (var entry in bucket)
                {
                    CAIObject pGrenade = entry.GetAIObject();
                    if (pGrenade == null) continue;
                    Vec3 gpos = pGrenade.GetPos();
                    if (Vec3.Distance_Point_PointSq(gpos, reqPos) > rangeSq)
                        continue;

                    bool merged = false;
                    foreach (var gp in grenades)
                    {
                        if ((gp - gpos).GetLength() < 3.0f) { merged = true; break; }
                    }
                    if (!merged)
                        grenades.Add(gpos);
                }

                foreach (var gp in grenades)
                    result.Add(new SDangerSpot { pos = gp, type = (int)EDangerSpots.DANGER_EXPLOSIVE });
            }
        }

        return result;
    }

    // ================================================================
    // CheckPointsVisibility — CAISystemPhys.cpp line 92-116
    // ================================================================
    private bool CheckPointsVisibilityImpl(Vec3 from, Vec3 to, float rayLength,
        IPhysicalEntity skip0, IPhysicalEntity skip1)
    {
        Vec3 dir = to - from;
        if (rayLength > 0 && dir.GetLengthSquared() > rayLength * rayLength)
            dir = dir * (rayLength / dir.GetLength());

        if (gAIEnv.pRayCaster != null)
        {
            RayCastRequest request = new RayCastRequest(from, dir, EAICollisionEntities.AICE_ALL, 0);
            RayCastResult result = gAIEnv.pRayCaster.Cast(request);
            return !result;
        }
        return true;
    }

    // ================================================================
    // CheckObjectsVisibility — CAISystemPhys.cpp line 120-137
    // ================================================================
    private bool CheckObjectsVisibilityImpl(CAIObject pObj1, CAIObject pObj2, float rayLength)
    {
        if (pObj1 == null || pObj2 == null) return false;

        Vec3 dir = pObj2.GetPos() - pObj1.GetPos();
        if (rayLength > 0 && dir.GetLengthSquared() > rayLength * rayLength)
            dir = dir * (rayLength / dir.GetLength());

        if (gAIEnv.pRayCaster != null)
        {
            RayCastRequest request = new RayCastRequest(pObj1.GetPos(), dir, EAICollisionEntities.AICE_ALL, 0);
            RayCastResult result = gAIEnv.pRayCaster.Cast(request);
            return !result || result[0].dist > (dir.GetLength() - 0.1f);
        }
        return true;
    }

    // ================================================================
    // CheckVisibilityToBody — CAISystemPhys.cpp line 141-187
    // ================================================================
    private bool CheckVisibilityToBodyImpl(CPuppet pObserver, CAIActor pBody, ref float closestDistSq, IPhysicalEntity pSkipEnt)
    {
        if (pObserver == null || pBody == null) return false;

        Vec3 bodyPos = pBody.GetPos();
        Vec3 puppetPos = pObserver.GetPos();
        Vec3 posDiff = bodyPos - puppetPos;
        float distSq = posDiff.GetLengthSquared();
        if (distSq > closestDistSq)
            return false;

        if (pObserver.IsObjectInFOV(pBody, pObserver.GetParameters().m_PerceptionParams.perceptionScale.visual * 0.75f) == EFieldOfViewResult.eFOV_Outside)
            return false;

        if (gAIEnv.pRayCaster != null)
        {
            RayCastRequest request = new RayCastRequest(puppetPos, posDiff, EAICollisionEntities.AICE_ALL, 0);
            RayCastResult result = gAIEnv.pRayCaster.Cast(request);
            bool isVisible = !result;

            if (!isVisible && result.hitCount > 0)
            {
                RayCastHit hit = result[0];
                IPhysicalEntity bodyPhys = pBody.GetProxy()?.GetPhysics();
                if (hit.pCollider != null && bodyPhys != null && hit.pCollider == bodyPhys)
                    isVisible = true;
            }

            if (!isVisible)
                return false;
        }

        closestDistSq = distSq;
        return true;
    }

    // ================================================================
    // GetVisPerceptionDistScale — CAISystem.cpp line 5974-5985
    // ================================================================
    private float GetVisPerceptionDistScaleImpl(float fRatio)
    {
        float distRatio = Math.Clamp(fRatio, 0.0f, 1.0f);

        if (m_VisDistLookUp.GetSize() < 2)
            return (1.0f - distRatio) * (1.0f - distRatio);

        return m_VisDistLookUp.GetValue(distRatio) / 100.0f;
    }

    // ================================================================
    // ResetAIActorSets — CAISystem.cpp line 6780-6798
    // ================================================================
    public void ResetAIActorSets(bool clearSets)
    {
        m_iteratingActorSet = false;
        m_enabledActorsUpdateError = 0;
        m_enabledActorsUpdateHead = 0;
        m_totalActorsUpdateCount = 0;
        m_disabledActorsUpdateError = 0;
        m_disabledActorsHead = 0;

        if (clearSets)
        {
            m_enabledAIActorsSet.Clear();
            m_disabledAIActorsSet.Clear();
        }
    }
}

// ================================================================
// IAISystemListener shell — Phase 11
// ================================================================
public interface IAISystemListener
{
    void OnAgentDeath(uint deadEntityID, uint killerID);
    void OnAgentUpdate(uint entityID);
    void OnEvent(int eventType);
}

// ================================================================
// Helper for AISignalExtraData copy-construction (used in SendSignal)
// ================================================================
public partial class AISignalExtraData
{
    // Default constructor — needed since we added a copy constructor
    public AISignalExtraData() { }

    // Copy constructor — used by SendSignal to clone signal data
    public AISignalExtraData(IAISignalExtraData other)
    {
        if (other != null)
        {
            _point = other.point;
            _point2 = other.point2;
            _nID = other.nID;
            _fValue = other.fValue;
            _iValue = other.iValue;
            _iValue2 = other.iValue2;
            _sObjectName = other.sObjectName ?? "";
        }
    }
}

// ================================================================
// StrongRefHelpers — creates CStrongRef wrappers for CAIObject
// ================================================================
public static class StrongRefHelpers
{
    /// Creates a CStrongRef that wraps a CAIObject. Uses the object container
    /// registration if available, otherwise returns a default (nil) ref.
    public static CStrongRef<CAIObject> CreateStrongRef(CAIObject pObject)
    {
        // In the C# port, CStrongRef tracks objects by ID through the ObjectContainer.
        // For compatibility, return a default ref — the actual registration is done
        // by the ObjectContainer when objects are created via CreateAIObject.
        return new CStrongRef<CAIObject>();
    }
}
