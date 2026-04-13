// Literal port of dev/Code/CryEngine/CryAISystem/CAISystemUpdate.cpp (764 lines).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// Description : all the update related functionality here
//
// This is a partial class file — the main CAISystem body is in CAISystem.cs / Environment.cs.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.CryRandom;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.CAIPlayerCastHelper;
using static CryAISystem.CAIVehicleCastHelpers;
using CryAISystem.CryCommon;

namespace CryAISystem;

//
//-----------------------------------------------------------------------------------------------------------
// static bool IsPuppetOnScreen(CPuppet* pPuppet)  — CAISystemUpdate.cpp lines 88-100
static class CAISystemUpdateHelpers
{
    public static bool IsPuppetOnScreen(CPuppet pPuppet)
    {
        IEntity pEntity = pPuppet.GetEntity();
        if (pEntity == null)
            return false;
        IComponentRender pRenderComponent = pEntity.GetComponent<IComponentRender>();
        if (pRenderComponent == null || pRenderComponent.GetRenderNode() == null)
            return false;
        int frameDiff = gEnv.pRenderer.GetFrameID(false) - pRenderComponent.GetRenderNode().GetDrawFrame(0);
        if (frameDiff > 2)
            return false;
        return true;
    }
}

// struct SSortedPuppetB — CAISystemUpdate.cpp lines 439-446
public struct SSortedPuppetB : IComparable<SSortedPuppetB>
{
    public SSortedPuppetB(CPuppet o, float dot, float d) { obj = o; weight = 0.0f; this.dot = dot; dist = d; }
    public int CompareTo(SSortedPuppetB rhs) { return weight.CompareTo(rhs.weight); }

    public float weight, dot, dist;
    public CPuppet obj;
}

// struct SAIDelayedExpAccessoryUpdate — CAISystem.h lines 933-944
public struct SAIDelayedExpAccessoryUpdate
{
    public SAIDelayedExpAccessoryUpdate(CPuppet pPuppet, int timeMs, bool state)
    {
        this.pPuppet = pPuppet;
        this.timeMs = timeMs;
        this.state = state;
    }
    public CPuppet pPuppet;
    public int timeMs;
    public bool state;
}

// struct AuxSignalDesc — CAISystem.h lines 948-957
public struct AuxSignalDesc
{
    public float fTimeout;
    public string strMessage;
    public void Serialize(TSerialize ser)
    {
        ser.Value("AuxSignalDescTimeOut", ref fTimeout);
        ser.Value("AuxSignalDescMessage", ref strMessage);
    }
}

// typedef std::multimap<short, AuxSignalDesc> MapSignalStrings;
// C# equivalent: SortedDictionary<short, List<AuxSignalDesc>>
public class MapSignalStrings : SortedDictionary<short, List<AuxSignalDesc>> { }

// inline bool PuppetFloatSorter — CAISystemUpdate.cpp lines 614-617
static class PuppetFloatSorterHelper
{
    public static int PuppetFloatSorter(KeyValuePair<CPuppet, float> lhs, KeyValuePair<CPuppet, float> rhs)
    {
        return lhs.Value.CompareTo(rhs.Value);
    }
}

// AIGroupMap typedef — CAISystem.h line 798
// typedef std::map<int, CAIGroup*> AIGroupMap;
public class AIGroupMap : Dictionary<int, CAIGroup> { }

// FormationDescriptor — CFormationDescriptor from Formation.h / CAISystem.h
public class FormationDescriptor
{
    public string m_sName = "";
    public uint m_nNameCRC32;
    public System.Collections.Generic.List<FormationNode> m_Nodes = new System.Collections.Generic.List<FormationNode>();
}

// FormationNode — formation descriptor node (Formation.h)
public struct FormationNode
{
    public Vec3 vOffset;
    public Vec3 vSightDirection;
    public float fFollowDistance;
    public float fFollowOffset;
    public int eClass;
}

// CastToCAIVehicleSafe helper — mirrors CAIVehicleCastHelper in AIVehicle.cs
public static class CAIVehicleCastHelpers
{
    public static CAIVehicle CastToCAIVehicleSafe(IAIObject pAI) { return pAI?.CastToCAIVehicle(); }
}

// CastToCPuppetSafe — helper matching C++ CastToCAIPlayerSafe / CastToCAIVehicleSafe pattern
public static class CastToCPuppetSafeHelper
{
    public static CPuppet CastToCPuppetSafe(IAIObject pAI) { return pAI?.CastToCPuppet(); }
}

// Col_Green / Col_Red — color constants used by UpdateDebugStuff
public static class Col_UpdateColors
{
    public static readonly ColorB Col_Green = new ColorB(0, 255, 0);
    public static readonly ColorB Col_Red = new ColorB(255, 0, 0);
}

public partial class CAISystem
{
    //
    //-----------------------------------------------------------------------------------------------------------
    // CAISystem::CalcPuppetUpdatePriority — CAISystemUpdate.cpp lines 104-147
    public EPuppetUpdatePriority CalcPuppetUpdatePriority(CPuppet pPuppet)
    {
        float fMinDistSq = float.MaxValue;
        bool bOnScreen = false;
        Vec3 pos = pPuppet.GetPos();

        if (gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_CRYSIS && gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_CRYSIS2)
        {
            // find closest player distance (better than using the camera pos in coop / dedicated server)
            //	and check visibility against all players

            if (gAIEnv.pAIObjectManager != null && gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)EAIObjectType.AIOBJECT_PLAYER, out var playerBucket))
            {
                foreach (var countedRef in playerBucket)
                {
                    CAIPlayer pPlayer = CastToCAIPlayerSafe(countedRef.GetAIObject());
                    if (pPlayer != null)
                    {
                        fMinDistSq = min(fMinDistSq, (pos - pPlayer.GetPos()).GetLengthSquared());

                        if (!bOnScreen)
                            bOnScreen = (EFieldOfViewResult.eFOV_Outside != pPlayer.IsPointInFOV(pos, 2.0f)); // double range for this check, real sight range is used below.
                    }
                }
            }
        }
        else
        {
            // previous behavior retained for Crysis compatibility
            Vec3 camPos = m_viewCameraPos; // gEnv->pSystem->GetViewCamera().GetPosition();
            fMinDistSq = Distance.Point_PointSq(camPos, pos);
            bOnScreen = CAISystemUpdateHelpers.IsPuppetOnScreen(pPuppet);
        }

        // Calculate the update priority of the puppet.
        float fSightRangeSq = sqr(pPuppet.GetParameters().m_PerceptionParams.sightRange);
        bool bInSightRange = (fMinDistSq < fSightRangeSq);
        if (bOnScreen)
        {
            return (bInSightRange ? EPuppetUpdatePriority.AIPUP_VERY_HIGH : EPuppetUpdatePriority.AIPUP_HIGH);
        }
        else
        {
            return (bInSightRange ? EPuppetUpdatePriority.AIPUP_MED : EPuppetUpdatePriority.AIPUP_LOW);
        }
    }

    //
    //-----------------------------------------------------------------------------------------------------------
    // #ifdef CRYAISYSTEM_DEBUG
    // CAISystem::UpdateDebugStuff — CAISystemUpdate.cpp lines 152-428
    public void UpdateDebugStuff()
    {
        // FUNCTION_PROFILER( gEnv->pSystem,PROFILE_AI );
        // Delete the debug lines if the debug draw is not on.
        if ((gAIEnv.CVars.DebugDraw == 0))
        {
            m_vecDebugLines.Clear();
            m_vecDebugBoxes.Clear();
        }

        bool drawCover = (gAIEnv.CVars.DebugDraw != 0) && (gAIEnv.CVars.DebugDrawCover != 0);

        string debugHideSpotName = gAIEnv.CVars.DebugHideSpotName;
        drawCover &= (!string.IsNullOrEmpty(debugHideSpotName) && debugHideSpotName != "0");

        if (drawCover)
        {
            uint[] anchorTypes = new uint[]
            {
                (uint)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT,
                (uint)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT_SECONDARY
            };

            uint anchorTypeCount = (uint)anchorTypes.Length;

            bool all = string.Equals(debugHideSpotName, "all", StringComparison.OrdinalIgnoreCase);
            if (!all && (m_DebugHideObjects.Count > 1))
                m_DebugHideObjects.Clear();

            float maxDistanceSq = sqr(75.0f);
            Vec3 cameraPosition = m_viewCameraPos;

            bool found = false;
            for (uint at = 0; at < anchorTypeCount; ++at)
            {
                if (gAIEnv.pAIObjectManager == null)
                    break;
                if (!gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)anchorTypes[at], out var bucket))
                    continue;

                foreach (var countedRef in bucket)
                {
                    CAIObject pObject = countedRef.GetAIObject();
                    if (pObject == null)
                        continue;
                    if (pObject.GetType() != (ushort)anchorTypes[at])
                        break;

                    if (!pObject.IsEnabled())
                        continue;

                    GraphNode pNode = gAIEnv.pGraph != null ? gAIEnv.pGraph.GetNode((uint)pObject.GetNavNodeIndex()) : null;
                    if (pNode == null)
                        continue;

                    if (all)
                    {
                        if ((pObject.GetPos() - cameraPosition).GetLengthSquared() > maxDistanceSq)
                        {
                            uint objId = pObject.GetAIObjectID();
                            if (m_DebugHideObjects.ContainsKey(objId))
                                m_DebugHideObjects.Remove(objId);

                            continue;
                        }

                        // Marcio: Assume max radius for now!
                        // Sphere sphere(pObject->GetPos(), 12.0f);
                        // if (!camera.IsSphereVisible_F(sphere)) — camera visibility deferred
                    }
                    else
                    {
                        if (!string.Equals(pObject.GetName(), debugHideSpotName, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    SHideSpot hideSpot = new SHideSpot(SHideSpotInfo.EHideSpotType.eHST_ANCHOR, pObject.GetPos(), pObject.GetMoveDir());
                    hideSpot.pNavNode = pNode;
                    hideSpot.pAnchorObject = pObject;

                    uint aiObjId = pObject.GetAIObjectID();
                    if (!m_DebugHideObjects.ContainsKey(aiObjId))
                        m_DebugHideObjects[aiObjId] = new CAIHideObject();

                    CAIHideObject hideObject = m_DebugHideObjects[aiObjId];
                    hideObject.Set(hideSpot, pObject.GetPos(), pObject.GetMoveDir());

                    if (!all)
                    {
                        found = true;
                        break;
                    }
                }

                if (!all && found)
                    break;
            }

            // clean up removed objects
            {
                List<uint> toRemove = new List<uint>();
                foreach (var kvp in m_DebugHideObjects)
                {
                    CAIObject pAIObject = gAIEnv.pObjectContainer != null ? gAIEnv.pObjectContainer.GetAIObjectById(kvp.Key) : null;

                    bool ok = false;
                    if (pAIObject != null && pAIObject.IsEnabled())
                    {
                        for (uint at2 = 0; at2 < anchorTypeCount; ++at2)
                        {
                            if (pAIObject.GetType() == (ushort)anchorTypes[at2])
                            {
                                ok = true;
                                break;
                            }
                        }
                    }

                    if (!ok)
                        toRemove.Add(kvp.Key);
                }
                foreach (var key in toRemove)
                    m_DebugHideObjects.Remove(key);
            }

            // update and draw them
            foreach (var kvp in m_DebugHideObjects)
            {
                CAIHideObject debugHideObject = kvp.Value;

                if (debugHideObject.IsValid())
                {
                    debugHideObject.HurryUpCoverPathGen();
                    while (!debugHideObject.IsCoverPathComplete())
                        debugHideObject.Update(0);
                    debugHideObject.DebugDraw();
                }
            }
        }
        else
            m_DebugHideObjects.Clear();

        // Update fake tracers
        if (gAIEnv.CVars.DrawFakeTracers > 0)
        {
            for (int i = 0; i < m_DEBUG_fakeTracers.Count;)
            {
                var tracer = m_DEBUG_fakeTracers[i];
                tracer.t -= m_frameDeltaTime;
                m_DEBUG_fakeTracers[i] = tracer;
                if (m_DEBUG_fakeTracers[i].t < 0.0f)
                {
                    m_DEBUG_fakeTracers[i] = m_DEBUG_fakeTracers[m_DEBUG_fakeTracers.Count - 1];
                    m_DEBUG_fakeTracers.RemoveAt(m_DEBUG_fakeTracers.Count - 1);
                }
                else
                {
                    ++i;
                }
            }
        }
        else
        {
            m_DEBUG_fakeTracers.Clear();
        }

        // Update fake hit effects
        if (gAIEnv.CVars.DrawFakeHitEffects > 0)
        {
            for (int i = 0; i < m_DEBUG_fakeHitEffect.Count;)
            {
                var effect = m_DEBUG_fakeHitEffect[i];
                effect.t -= m_frameDeltaTime;
                m_DEBUG_fakeHitEffect[i] = effect;
                if (m_DEBUG_fakeHitEffect[i].t < 0.0f)
                {
                    m_DEBUG_fakeHitEffect[i] = m_DEBUG_fakeHitEffect[m_DEBUG_fakeHitEffect.Count - 1];
                    m_DEBUG_fakeHitEffect.RemoveAt(m_DEBUG_fakeHitEffect.Count - 1);
                }
                else
                {
                    ++i;
                }
            }
        }
        else
        {
            m_DEBUG_fakeHitEffect.Clear();
        }

        // Update fake damage indicators
        if (gAIEnv.CVars.DrawFakeDamageInd > 0)
        {
            for (int i = 0; i < m_DEBUG_fakeDamageInd.Count;)
            {
                var ind = m_DEBUG_fakeDamageInd[i];
                ind.t -= m_frameDeltaTime;
                m_DEBUG_fakeDamageInd[i] = ind;
                if (m_DEBUG_fakeDamageInd[i].t < 0)
                {
                    m_DEBUG_fakeDamageInd[i] = m_DEBUG_fakeDamageInd[m_DEBUG_fakeDamageInd.Count - 1];
                    m_DEBUG_fakeDamageInd.RemoveAt(m_DEBUG_fakeDamageInd.Count - 1);
                }
                else
                    ++i;
            }
            m_DEBUG_screenFlash = max(0.0f, m_DEBUG_screenFlash - m_frameDeltaTime);
        }
        else
        {
            m_DEBUG_fakeDamageInd.Clear();
            m_DEBUG_screenFlash = 0.0f;
        }

        if (gAIEnv.CVars.DebugCheckWalkability != 0)
        {
            CAIObject startObject = gAIEnv.pAIObjectManager?.GetAIObjectByName("CheckWalkabilityTestStart");
            CAIObject endObject = gAIEnv.pAIObjectManager?.GetAIObjectByName("CheckWalkabilityTestEnd");

            if (startObject != null && endObject != null)
            {
                bool result = false;
                float radius = gAIEnv.CVars.DebugCheckWalkabilityRadius;

                if (gAIEnv.CVars.DebugCheckWalkability == 1)
                {
                    // Full physics query path deferred (Phase 11)
                    result = AICollision.CheckWalkability(startObject.GetPos(), endObject.GetPos(), radius);
                }
                else if (gAIEnv.CVars.DebugCheckWalkability == 2)
                    result = AICollision.CheckWalkability(startObject.GetPos(), endObject.GetPos(), radius);

                CDebugDrawContext dc = new CDebugDrawContext();
                dc.Draw2dLabel(400.0f, 100.0f, 5.0f, result ? Col_UpdateColors.Col_Green : Col_UpdateColors.Col_Red, true, "{0} {1}!", "CheckWalkability ", result ? "passed" : "failed");
            }
        }

        if (gAIEnv.CVars.DebugWalkabilityCache != 0)
        {
            // gAIEnv.pWalkabilityCacheManager->Draw(); — Draw() method pending
        }
    }
    // #endif //CRYAISYSTEM_DEBUG

    //===================================================================
    // GetUpdateAllAlways — CAISystemUpdate.cpp lines 433-437
    //===================================================================
    public bool GetUpdateAllAlways()
    {
        bool updateAllAlways = gAIEnv.CVars.UpdateAllAlways != 0;
        return updateAllAlways;
    }

    //
    //-----------------------------------------------------------------------------------------------------------
    // CAISystem::UpdateAmbientFire — CAISystemUpdate.cpp lines 450-611
    public void UpdateAmbientFire()
    {
        // FUNCTION_PROFILER( gEnv->pSystem,PROFILE_AI );

        if (gAIEnv.CVars.AmbientFireEnable == 0)
            return;

        long dt = (long)(GetFrameStartTime() - m_lastAmbientFireUpdateTime).GetMilliSecondsAsInt64();
        if (dt < (int)(gAIEnv.CVars.AmbientFireUpdateInterval * 1000.0f))
            return;

        if (gAIEnv.pAIObjectManager == null)
            return;

        // Marcio: Update ambient fire towards all players.
        if (gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)EAIObjectType.AIOBJECT_ACTOR, out var actorBucket))
        {
            foreach (var countedRef in actorBucket)
            {
                CAIObject obj = countedRef.GetAIObject();
                if (obj == null || !obj.IsEnabled())
                    continue;

                CPuppet pPuppet = obj.CastToCPuppet();
                if (pPuppet == null)
                    continue;

                // By default make every AI in ambient fire
                pPuppet.SetAllowedToHitTarget(false);
            }
        }

        if (gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)EAIObjectType.AIOBJECT_VEHICLE, out var vehicleBucket))
        {
            foreach (var countedRef in vehicleBucket)
            {
                CAIVehicle obj = CAIVehicleCastHelpers.CastToCAIVehicleSafe(countedRef.GetAIObject());
                if (obj == null || !obj.IsDriverInside())
                    continue;
                CPuppet pPuppet = obj.CastToCPuppet();
                if (pPuppet == null)
                    continue;

                pPuppet.SetAllowedToHitTarget(false);
            }
        }

        if (!gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)EAIObjectType.AIOBJECT_PLAYER, out var playerBucket))
            return;

        foreach (var plCountedRef in playerBucket)
        {
            CAIPlayer pPlayer = CastToCAIPlayerSafe(plCountedRef.GetAIObject());
            if (pPlayer == null)
                return;

            m_lastAmbientFireUpdateTime = GetFrameStartTime();

            Vec3 playerPos = pPlayer.GetPos();
            Vec3 playerDir = pPlayer.GetMoveDir();

            List<SSortedPuppetB> shooters = new List<SSortedPuppetB>(32);
            float maxDist = 0.0f;

            // Update — actors
            if (gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)EAIObjectType.AIOBJECT_ACTOR, out var actorBucket2))
            {
                foreach (var countedRef in actorBucket2)
                {
                    CAIObject obj = countedRef.GetAIObject();
                    if (obj == null || !obj.IsEnabled())
                        continue;
                    CPuppet pPuppet = obj.CastToCPuppet();
                    if (pPuppet == null)
                        continue;

                    CAIObject pTarget = (CAIObject)pPuppet.GetAttentionTarget();

                    if ((pTarget != null) && pTarget.IsAgent())
                    {
                        if (pTarget == pPlayer)
                        {
                            Vec3 dirPlayerToPuppet = pPuppet.GetPos() - playerPos;
                            float dist = dirPlayerToPuppet.NormalizeSafe();
                            if (dist > 0.01f && dist < pPuppet.GetParameters().m_fAttackRange)
                            {
                                maxDist = max(maxDist, dist);
                                float dot = playerDir.Dot(dirPlayerToPuppet);
                                shooters.Add(new SSortedPuppetB(pPuppet, dot, dist));
                            }
                            continue;
                        }
                    }

                    // Shooting something else than player, allow to hit.
                    pPuppet.SetAllowedToHitTarget(true);
                }
            }

            // Update — vehicles
            if (gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)EAIObjectType.AIOBJECT_VEHICLE, out var vehicleBucket2))
            {
                foreach (var countedRef in vehicleBucket2)
                {
                    CAIObject objRaw = countedRef.GetAIObject();
                    if (objRaw == null) continue;
                    CAIVehicle obj = objRaw.CastToCAIVehicle();
                    if (obj == null || !obj.IsDriverInside())
                        continue;
                    CPuppet pPuppet = obj.CastToCPuppet();
                    if (pPuppet == null)
                        continue;

                    CAIObject pTarget = (CAIObject)pPuppet.GetAttentionTarget();

                    if ((pTarget != null) && pTarget.IsAgent())
                    {
                        if (pTarget == pPlayer)
                        {
                            Vec3 dirPlayerToPuppet = pPuppet.GetPos() - playerPos;
                            float dist = dirPlayerToPuppet.NormalizeSafe();

                            if ((dist > 0.01f) && (dist < pPuppet.GetParameters().m_fAttackRange) && pPuppet.AllowedToFire())
                            {
                                maxDist = max(maxDist, dist);
                                float dot = playerDir.Dot(dirPlayerToPuppet);
                                shooters.Add(new SSortedPuppetB(pPuppet, dot, dist));
                            }

                            continue;
                        }
                    }
                    pPuppet.SetAllowedToHitTarget(true);
                }
            }

            if (shooters.Count > 0 && maxDist > 0.01f)
            {
                // Find nearest shooter
                int nearestIdx = 0;
                float nearestWeight = sqr((1.0f - shooters[0].dot) / 2) * (0.3f + 0.7f * shooters[0].dist / maxDist);
                for (int it = 1; it < shooters.Count; ++it)
                {
                    float weight = sqr((1.0f - shooters[it].dot) / 2) * (0.3f + 0.7f * shooters[it].dist / maxDist);
                    if (weight < nearestWeight)
                    {
                        nearestWeight = weight;
                        nearestIdx = it;
                    }
                }

                Vec3 dirToNearest = shooters[nearestIdx].obj.GetPos() - playerPos;
                dirToNearest.NormalizeSafe();

                for (int it = 0; it < shooters.Count; ++it)
                {
                    Vec3 dirPlayerToPuppet = shooters[it].obj.GetPos() - playerPos;
                    float dist = dirPlayerToPuppet.NormalizeSafe();
                    float dot = dirToNearest.Dot(dirPlayerToPuppet);
                    var s = shooters[it];
                    s.weight = sqr((1.0f - dot) / 2) * (dist / maxDist);
                    shooters[it] = s;
                }

                shooters.Sort();

                uint i = 0;
                uint quota = (uint)gAIEnv.CVars.AmbientFireQuota;

                for (int it = 0; it < shooters.Count; ++it)
                {
                    shooters[it].obj.SetAllowedToHitTarget(true);
                    if ((++i >= quota) && (shooters[it].dist > 7.5f)) // Always allow to hit if in 2.5 meter radius
                        break;
                }
            }
        }
    }

    //
    //-----------------------------------------------------------------------------------------------------------
    // CAISystem::UpdateExpensiveAccessoryQuota — CAISystemUpdate.cpp lines 621-729
    public void UpdateExpensiveAccessoryQuota()
    {
        // FUNCTION_PROFILER( gEnv->pSystem,PROFILE_AI );

        for (int i = 0; i < m_delayedExpAccessoryUpdates.Count;)
        {
            var update = m_delayedExpAccessoryUpdates[i];
            update.timeMs -= (int)(m_frameDeltaTime * 1000.0f);
            m_delayedExpAccessoryUpdates[i] = update;

            if (update.timeMs < 0)
            {
                update.pPuppet.SetAllowedToUseExpensiveAccessory(update.state);
                m_delayedExpAccessoryUpdates[i] = m_delayedExpAccessoryUpdates[m_delayedExpAccessoryUpdates.Count - 1];
                m_delayedExpAccessoryUpdates.RemoveAt(m_delayedExpAccessoryUpdates.Count - 1);
            }
            else
                ++i;
        }

        int UpdateTimeMs = 3000;
        float fUpdateTimeMs = (float)UpdateTimeMs;

        long dt = (long)(GetFrameStartTime() - m_lastExpensiveAccessoryUpdateTime).GetMilliSecondsAsInt64();
        if (dt < UpdateTimeMs)
            return;

        m_lastExpensiveAccessoryUpdateTime = GetFrameStartTime();

        m_delayedExpAccessoryUpdates.Clear();

        Vec3 interestPos = m_viewCameraPos; // gEnv->pSystem->GetViewCamera().GetPosition();

        List<KeyValuePair<CPuppet, float>> puppets = new List<KeyValuePair<CPuppet, float>>();
        SortedSet<CPuppet> stateRemoved = new SortedSet<CPuppet>(Comparer<CPuppet>.Create((a, b) =>
            a.GetAIObjectID().CompareTo(b.GetAIObjectID())));

        // Choose the best of each group, then best of the best.
        foreach (var kvp in m_mapAIGroups)
        {
            CAIGroup pGroup = kvp.Value;

            CPuppet pBestUnit = null;
            float bestVal = FLT_MAX;

            foreach (var itu in pGroup.GetUnits())
            {
                CPuppet pPuppet = CastToCPuppetSafeHelper.CastToCPuppetSafe(itu.m_refUnit.GetAIObject());
                if (pPuppet == null)
                    continue;
                if (!pPuppet.IsEnabled())
                    continue;

                if (pPuppet.IsAllowedToUseExpensiveAccessory())
                    stateRemoved.Add(pPuppet);

                int accessories = (int)pPuppet.GetParameters().m_weaponAccessories;
                if ((accessories & ((int)EAIWeaponAccessories.AIWEPA_COMBAT_LIGHT | (int)EAIWeaponAccessories.AIWEPA_PATROL_LIGHT)) == 0)
                    continue;

                if (pPuppet.GetProxy() != null)
                {
                    SAIWeaponInfo wi = new SAIWeaponInfo();
                    pPuppet.GetProxy().QueryWeaponInfo(wi);
                    if (!wi.hasLightAccessory)
                        continue;
                }

                float val = Distance.Point_Point(interestPos, pPuppet.GetPos());

                if (pPuppet.GetAttentionTargetThreat() == EAITargetThreat.AITHREAT_AGGRESSIVE)
                    val *= 0.5f;
                else if (pPuppet.GetAttentionTargetThreat() >= EAITargetThreat.AITHREAT_INTERESTING)
                    val *= 0.8f;

                if (val < bestVal)
                {
                    bestVal = val;
                    pBestUnit = pPuppet;
                }
            }

            if (pBestUnit != null)
            {
                CCCPOINT("UpdateExpensiveAccessoryQuota");
                puppets.Add(new KeyValuePair<CPuppet, float>(pBestUnit, bestVal));
            }
        }

        puppets.Sort(PuppetFloatSorterHelper.PuppetFloatSorter);

        uint maxExpensiveAccessories = 3;
        for (int i = 0; i < puppets.Count && i < (int)maxExpensiveAccessories; ++i)
        {
            stateRemoved.Remove(puppets[i].Key);

            if (!puppets[i].Key.IsAllowedToUseExpensiveAccessory())
            {
                int timeMs = (int)(fUpdateTimeMs * 0.5f + cry_random(0.0f, fUpdateTimeMs) * 0.4f);
                m_delayedExpAccessoryUpdates.Add(new SAIDelayedExpAccessoryUpdate(puppets[i].Key, timeMs, true));
            }
        }

        foreach (var puppet in stateRemoved)
        {
            int timeMs = (int)(cry_random(0.0f, fUpdateTimeMs) * 0.4f);
            m_delayedExpAccessoryUpdates.Add(new SAIDelayedExpAccessoryUpdate(puppet, timeMs, false));
        }
    }

    //
    //-----------------------------------------------------------------------------------------------------------
    // CAISystem::SingleDryUpdate — CAISystemUpdate.cpp lines 733-740
    public void SingleDryUpdate(CAIActor pAIActor)
    {
        // FUNCTION_PROFILER( gEnv->pSystem,PROFILE_AI );
        if (pAIActor.IsEnabled())
            pAIActor.Update(EObjectUpdate.AIUPDATE_DRY);
        else
            pAIActor.UpdateDisabled(EObjectUpdate.AIUPDATE_DRY);
    }

    //
    //-----------------------------------------------------------------------------------------------------------
    // CAISystem::UpdateAuxSignalsMap — CAISystemUpdate.cpp lines 744-764
    public void UpdateAuxSignalsMap()
    {
        // FUNCTION_PROFILER( gEnv->pSystem,PROFILE_AI );

        if (m_mapAuxSignalsFired.Count > 0)
        {
            List<short> bucketsToRemove = new List<short>();
            foreach (var bucketKvp in m_mapAuxSignalsFired)
            {
                short key = bucketKvp.Key;
                List<AuxSignalDesc> bucket = bucketKvp.Value;
                for (int i = bucket.Count - 1; i >= 0; --i)
                {
                    var desc = bucket[i];
                    desc.fTimeout -= m_frameDeltaTime;
                    if (desc.fTimeout < 0)
                    {
                        bucket.RemoveAt(i);
                    }
                    else
                    {
                        bucket[i] = desc;
                    }
                }
                if (bucket.Count == 0)
                    bucketsToRemove.Add(key);
            }
            foreach (var key in bucketsToRemove)
                m_mapAuxSignalsFired.Remove(key);
        }
    }

    // ===================================================================
    // Fields used by the update methods above — CAISystem.h
    // m_DebugHideObjects, m_delayedExpAccessoryUpdates, m_mapAuxSignalsFired
    // are declared in CAISystem.cs (public, same partial class).
    // ===================================================================

    // CAISystem.h lines 1066-1093 — debug structs and their containers
    public struct SDebugFakeTracer
    {
        public SDebugFakeTracer(Vec3 p0, Vec3 p1, float a, float t) { this.p0 = p0; this.p1 = p1; this.a = a; this.t = t; this.tmax = t; }
        public Vec3 p0, p1;
        public float a;
        public float t, tmax;
    }
    private List<SDebugFakeTracer> m_DEBUG_fakeTracers = new List<SDebugFakeTracer>();

    public struct SDebugFakeDamageInd
    {
        public SDebugFakeDamageInd() { verts = new List<Vec3>(); p = new Vec3(0, 0, 0); t = 0; tmax = 0; }
        public SDebugFakeDamageInd(Vec3 pos, float t) { verts = new List<Vec3>(); p = pos; this.t = t; tmax = t; }
        public List<Vec3> verts;
        public Vec3 p;
        public float t, tmax;
    }
    private List<SDebugFakeDamageInd> m_DEBUG_fakeDamageInd = new List<SDebugFakeDamageInd>();

    public struct SDebugFakeHitEffect
    {
        public SDebugFakeHitEffect() { p = new Vec3(0, 0, 0); n = new Vec3(0, 0, 0); r = 0; t = 0; tmax = 0; c = new ColorB(0, 0, 0); }
        public SDebugFakeHitEffect(Vec3 p, Vec3 n, float r, float t, ColorB c) { this.p = p; this.n = n; this.r = r; this.t = t; this.tmax = t; this.c = c; }
        public Vec3 p, n;
        public float r, t, tmax;
        public ColorB c;
    }
    private List<SDebugFakeHitEffect> m_DEBUG_fakeHitEffect = new List<SDebugFakeHitEffect>();

    // CAISystem.h lines 1185-1214 — debug lines and boxes
    public struct SDebugLine
    {
        public SDebugLine(Vec3 start, Vec3 end, ColorB color, float time, float thickness)
        {
            this.start = start; this.end = end; this.color = color; this.time = time; this.thickness = thickness;
        }
        public Vec3 start, end;
        public ColorB color;
        public float time;
        public float thickness;
    }
    private List<SDebugLine> m_vecDebugLines = new List<SDebugLine>();

    public struct SDebugBox
    {
        public SDebugBox(Vec3 pos, OBB obb, ColorB color, float time)
        {
            this.pos = pos; this.obb = obb; this.color = color; this.time = time;
        }
        public Vec3 pos;
        public OBB obb;
        public ColorB color;
        public float time;
    }
    private List<SDebugBox> m_vecDebugBoxes = new List<SDebugBox>();

    // Camera position cache — used in place of gEnv->pSystem->GetViewCamera().GetPosition()
    // Set externally before update. The C++ code calls GetViewCamera() directly; the C# port
    // stores it because ISystem.GetViewCamera() is not yet ported.
    public Vec3 m_viewCameraPos = new Vec3(0, 0, 0);
}
