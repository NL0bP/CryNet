// Literal port of dev/Code/CryEngine/CryAISystem/{VisionMap,AIRadialOcclusion,AIRadialOcclusionRaycast,
// PerceptionManager,GlobalPerceptionScaleHandler,CentralInterestManager,PersonalInterestManager,
// MissLocationSensor,AILightManager}.h/.cpp (Phase 5 — perception subsystem).
// CVisionMap is a full literal port of VisionMap.h + VisionMap.cpp (~1618L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Linq;
using static CryAISystem.CryMath;
using CryAISystem.CryCommon;

namespace CryAISystem;

// CVisionMap — literal port of VisionMap.h + VisionMap.cpp
public class CVisionMap : IVisionMap
{
    private static readonly float positionEpsilon = 0.05f;
    private static readonly float orientationEpsilon = 0.05f;

    public CVisionMap()
    {
        m_visionIdCounter = 0;
        Reset();
    }

    // virtual ~CVisionMap() — Reset() in destructor
    // C# finalizer not needed; Reset cleans up.

    public virtual void Reset()
    {
        // COMPILE_TIME_ASSERT( ( ObserverParams::MaxSkipListSize + ObservableParams::MaxSkipListSize ) <= RayCastRequest::MaxSkipListCount );

        foreach (var kvp in m_observers)
        {
            ObserverInfo observerInfo = kvp.Value;
            ReleaseSkipList(observerInfo.observerParams.skipList, observerInfo.observerParams.skipListSize);
            DeletePendingRays(observerInfo.pvs);
        }

        foreach (var kvp in m_observables)
        {
            ObservableInfo observableInfo = kvp.Value;
            ReleaseSkipList(observableInfo.observableParams.skipList, (int)observableInfo.observableParams.skipListSize);
        }

        m_debugTimer = 0.0f;
        m_numberOfPVSUpdatesThisFrame = 0;
        m_numberOfVisibilityUpdatesThisFrame = 0;
        m_numberOfRayCastsSubmittedThisFrame = 0;
        m_debugObserverVisionID = new VisionID();
        m_debugObservableVisionID = new VisionID();

        m_observers.Clear();
        m_observables.Clear();

        m_observerPVSUpdateQueue.Clear();
        m_observerVisibilityUpdateQueue.Clear();
    }

    public virtual VisionID CreateVisionID(string name)
    {
        ++m_visionIdCounter;
        while (m_visionIdCounter == 0)
            ++m_visionIdCounter;

        return new VisionID(m_visionIdCounter, name);
    }

    public virtual void RegisterObserver(ObserverID observerID, ObserverParams observerParams)
    {
        if (!observerID)
            return;

        if (!m_observers.ContainsKey(observerID))
            m_observers[observerID] = new ObserverInfo(observerID);

        ObserverChanged(observerID, observerParams, (uint)EChangeHint.eChangedAll);
    }

    public virtual void UnregisterObserver(ObserverID observerID)
    {
        if (!observerID)
            return;

        if (!m_observers.TryGetValue(observerID, out ObserverInfo observerInfo))
            return;

        ReleaseSkipList(observerInfo.observerParams.skipList, observerInfo.observerParams.skipListSize);
        DeletePendingRays(observerInfo.pvs);

        if (observerInfo.queuedForPVSUpdate)
            m_observerPVSUpdateQueue.Remove(observerInfo.observerID);

        if (observerInfo.queuedForVisibilityUpdate)
            m_observerVisibilityUpdateQueue.Remove(observerInfo.observerID);

        m_observers.Remove(observerID);
    }

    public virtual void RegisterObservable(ObservableID observableID, ObservableParams observerParams)
    {
        if (!observableID)
            return;

        System.Diagnostics.Debug.Assert(observerParams.observablePositionsCount > 0);
        System.Diagnostics.Debug.Assert(observerParams.observablePositionsCount <= ObservableParams.MaxPositionCount);

        if (!m_observables.ContainsKey(observableID))
            m_observables[observableID] = new ObservableInfo(observableID, new ObservableParams());

        ObservableInfo insertedObservableInfo = m_observables[observableID];

        ObservableChanged(observableID, observerParams, (uint)EChangeHint.eChangedAll);

        foreach (var kvp in m_observers)
        {
            ObserverInfo observerInfo = kvp.Value;
            if (ShouldBeAddedToObserverPVS(observerInfo, insertedObservableInfo))
                AddToObserverPVS(observerInfo, insertedObservableInfo);
        }
    }

    public virtual void UnregisterObservable(ObservableID observableID)
    {
        if (!observableID)
            return;

        if (!m_observables.TryGetValue(observableID, out ObservableInfo observableInfo))
            return;

        ReleaseSkipList(observableInfo.observableParams.skipList, (int)observableInfo.observableParams.skipListSize);

        foreach (var kvp in new List<KeyValuePair<ObserverID, ObserverInfo>>(m_observers))
        {
            if (kvp.Key.id == observableID.id)
                continue;

            ObserverInfo observerInfo = kvp.Value;

            if (!observerInfo.pvs.TryGetValue(observableID, out PVSEntry pvsEntry))
                continue;

            if (pvsEntry.visible)
                TriggerObserverCallback(observerInfo, pvsEntry.observableInfo, false);

            DeletePendingRay(pvsEntry);

            observerInfo.needsPVSUpdate = true;
            observerInfo.pvs.Remove(observableID);
        }

        m_observables.Remove(observableID);
    }

    public virtual void ObserverChanged(ObserverID observerID, ObserverParams newObserverParams, uint hint)
    {
        if (!m_observers.TryGetValue(observerID, out ObserverInfo observerInfo))
            return;

        bool needsUpdate = false;
        ObserverParams currentObserverParams = observerInfo.observerParams;

        if ((hint & (uint)EChangeHint.eChangedFaction) != 0)
        {
            currentObserverParams.faction = newObserverParams.faction;
            needsUpdate = true;
        }

        if ((hint & (uint)EChangeHint.eChangedFactionsToObserveMask) != 0)
        {
            currentObserverParams.factionsToObserveMask = newObserverParams.factionsToObserveMask;
        }

        if ((hint & (uint)EChangeHint.eChangedTypesToObserveMask) != 0)
        {
            currentObserverParams.typesToObserveMask = newObserverParams.typesToObserveMask;
        }

        if ((hint & (uint)EChangeHint.eChangedSightRange) != 0)
        {
            currentObserverParams.sightRange = newObserverParams.sightRange;
            needsUpdate = true;
        }

        if ((hint & (uint)EChangeHint.eChangedFOV) != 0)
        {
            currentObserverParams.fovCos = newObserverParams.fovCos;
            needsUpdate = true;
        }

        if ((hint & (uint)EChangeHint.eChangedPosition) != 0)
        {
            if (!Vec3Helpers.IsEquivalent(currentObserverParams.eyePosition, newObserverParams.eyePosition, positionEpsilon))
            {
                currentObserverParams.eyePosition = newObserverParams.eyePosition;
                needsUpdate = true;
            }
        }

        if ((hint & (uint)EChangeHint.eChangedOrientation) != 0)
        {
            if (!Vec3Helpers.IsEquivalent(currentObserverParams.eyeDirection, newObserverParams.eyeDirection, orientationEpsilon))
            {
                currentObserverParams.eyeDirection = newObserverParams.eyeDirection;
                needsUpdate = true;
            }
        }

        if ((hint & (uint)EChangeHint.eChangedSkipList) != 0)
        {
            int skipListSize = Math.Min(newObserverParams.skipListSize, ObserverParams.MaxSkipListSize);
            AcquireSkipList(newObserverParams.skipList, skipListSize);
            ReleaseSkipList(currentObserverParams.skipList, currentObserverParams.skipListSize);

            currentObserverParams.skipListSize = skipListSize;
            for (int i = 0; i < currentObserverParams.skipListSize; ++i)
                currentObserverParams.skipList[i] = newObserverParams.skipList[i];
        }

        if ((hint & (uint)EChangeHint.eChangedCallback) != 0)
        {
            currentObserverParams.callback = newObserverParams.callback;
        }

        if ((hint & (uint)EChangeHint.eChangedUserData) != 0)
        {
            currentObserverParams.userData = newObserverParams.userData;
        }

        if ((hint & (uint)EChangeHint.eChangedTypeMask) != 0)
        {
            currentObserverParams.typeMask = newObserverParams.typeMask;
            needsUpdate = true;
        }

        if ((hint & (uint)EChangeHint.eChangedRaycastFlags) != 0)
        {
            currentObserverParams.raycastFlags = newObserverParams.raycastFlags;
            needsUpdate = true;
        }

        if ((hint & (uint)EChangeHint.eChangedEntityId) != 0)
        {
            currentObserverParams.entityId = newObserverParams.entityId;
        }

        if (needsUpdate)
        {
            observerInfo.needsPVSUpdate = true;
            observerInfo.needsVisibilityUpdate = true;
            observerInfo.updateAllVisibilityStatus = true;
        }
    }

    public virtual void ObservableChanged(ObservableID observableID, ObservableParams newObservableParams, uint hint)
    {
        if (!m_observables.TryGetValue(observableID, out ObservableInfo observableInfo))
            return;

        ObservableParams currentObservableParams = observableInfo.observableParams;

        bool observableVisibilityPotentiallyChanged = false;

        if ((hint & (uint)EChangeHint.eChangedPosition) != 0)
        {
            System.Diagnostics.Debug.Assert(newObservableParams.observablePositionsCount > 0);
            System.Diagnostics.Debug.Assert(newObservableParams.observablePositionsCount <= ObservableParams.MaxPositionCount);

            Vec3 oldPosition = currentObservableParams.observablePositions[0];

            if (!Vec3Helpers.IsEquivalent(oldPosition, newObservableParams.observablePositions[0], positionEpsilon))
            {
                currentObservableParams.observablePositions[0] = newObservableParams.observablePositions[0];
                observableVisibilityPotentiallyChanged = true;

                currentObservableParams.observablePositionsCount = newObservableParams.observablePositionsCount;

                for (int i = 1; i < currentObservableParams.observablePositionsCount; ++i)
                {
                    currentObservableParams.observablePositions[i] = newObservableParams.observablePositions[i];
                }
            }
        }

        if ((hint & (uint)EChangeHint.eChangedUserData) != 0)
        {
            currentObservableParams.userData = newObservableParams.userData;
        }

        if ((hint & (uint)EChangeHint.eChangedCallback) != 0)
        {
            currentObservableParams.callback = newObservableParams.callback;
        }

        if ((hint & (uint)EChangeHint.eChangedSkipList) != 0)
        {
            int skipListSize = (int)Math.Min(newObservableParams.skipListSize, (uint)ObserverParams.MaxSkipListSize);
            AcquireSkipList(newObservableParams.skipList, skipListSize);
            ReleaseSkipList(currentObservableParams.skipList, (int)currentObservableParams.skipListSize);

            currentObservableParams.skipListSize = (uint)skipListSize;
            for (int i = 0; i < (int)currentObservableParams.skipListSize; ++i)
                currentObservableParams.skipList[i] = newObservableParams.skipList[i];
        }

        if ((hint & (uint)EChangeHint.eChangedFaction) != 0)
        {
            currentObservableParams.faction = newObservableParams.faction;
            observableVisibilityPotentiallyChanged = true;
        }

        if ((hint & (uint)EChangeHint.eChangedTypeMask) != 0)
        {
            currentObservableParams.typeMask = newObservableParams.typeMask;
            observableVisibilityPotentiallyChanged = true;
        }

        if ((hint & (uint)EChangeHint.eChangedEntityId) != 0)
        {
            currentObservableParams.entityId = newObservableParams.entityId;
        }

        if (observableVisibilityPotentiallyChanged)
        {
            CTimeValue now = gEnv.pTimer.GetFrameStartTime();

            foreach (var kvp in m_observers)
            {
                ObserverInfo observerInfo = kvp.Value;

                if (observerInfo.observerID.id == observableID.id)
                    continue;

                if (observerInfo.needsPVSUpdate)
                    continue;

                bool inObserverPVS = observerInfo.pvs.TryGetValue(observableID, out PVSEntry pvsEntry);

                if (ShouldObserve(observerInfo, observableInfo))
                {
                    if (inObserverPVS)
                    {
                        pvsEntry.needsUpdate = true;
                        observerInfo.needsVisibilityUpdate = true;
                    }
                    else
                    {
                        AddToObserverPVS(observerInfo, observableInfo);
                    }
                }
                else
                {
                    if (inObserverPVS)
                    {
                        if (pvsEntry.visible)
                        {
                            TriggerObserverCallback(observerInfo, observableInfo, false);
                            TriggerObservableCallback(observerInfo, observableInfo, false);
                        }

                        DeletePendingRay(pvsEntry);

                        observerInfo.pvs.Remove(observableID);
                    }
                }
            }
        }
    }

    public virtual bool IsVisible(ObserverID observerID, ObservableID observableID)
    {
        if (!m_observers.TryGetValue(observerID, out ObserverInfo observerInfo))
            return false;

        if (!observerInfo.pvs.TryGetValue(observableID, out PVSEntry pvsEntry))
            return false;

        return pvsEntry.visible;
    }

    public virtual ObserverParams GetObserverParams(ObserverID observerID)
    {
        if (!m_observers.TryGetValue(observerID, out ObserverInfo observerInfo))
            return null;

        return observerInfo.observerParams;
    }

    public virtual ObservableParams GetObservableParams(ObservableID observableID)
    {
        if (!m_observables.TryGetValue(observableID, out ObservableInfo observableInfo))
            return null;

        return observableInfo.observableParams;
    }

    public virtual void AddPriorityMapEntry(PriorityMapEntry priorityMapEntry)
    {
        m_priorityMap.Add(priorityMapEntry);
    }

    public virtual void ClearPriorityMap()
    {
        m_priorityMap.Clear();
    }

    public virtual void Update(float frameTime)
    {
        m_numberOfPVSUpdatesThisFrame = 0;
        m_numberOfVisibilityUpdatesThisFrame = 0;
        m_numberOfRayCastsSubmittedThisFrame = 0;
        m_debugTimer += frameTime;

        UpdateObservers();
    }

    public void DebugDraw()
    {
        if (gAIEnv.CVars.DebugDrawVisionMap != 0)
        {
            UpdateDebugVisionMapObjects();
            DebugDrawObservers();

            if (gAIEnv.CVars.DebugDrawVisionMapObservables != 0)
                DebugDrawObservables();

            if (gAIEnv.CVars.DebugDrawVisionMapStats != 0)
                DebugDrawVisionMapStats();
        }
    }

    // ---- Private types ----

    private class ObservableInfo
    {
        public ObservableInfo(ObservableID _observableID, ObservableParams _observableParams)
        {
            observableID = _observableID;
            observableParams = _observableParams;
        }
        public ObservableID observableID;
        public ObservableParams observableParams;
    }

    private class PVSEntry
    {
        public PVSEntry(ObservableInfo _observableInfo, RayCastRequest.Priority _priority)
        {
            pendingRayID = new QueuedRayID();
            observableInfo = _observableInfo;
            visible = false;
            currentTestPositionIndex = 0;
            priority = _priority;
            needsUpdate = true;
            firstVisPos = 0;
            obstructionPosition = new Vec3(0, 0, 0);
            lastObserverPositionChecked = new Vec3(0, 0, 0);
            lastObservablePositionChecked = new Vec3(0, 0, 0);
            rayQueueTimestamp = 0.0f;
        }

        public QueuedRayID pendingRayID;
        public RayCastRequest.Priority priority;
        public ObservableInfo observableInfo;

        public bool visible;
        public bool needsUpdate;
        public int currentTestPositionIndex;
        public int firstVisPos;

        public Vec3 obstructionPosition;
        public Vec3 lastObserverPositionChecked;
        public Vec3 lastObservablePositionChecked;
        public float rayQueueTimestamp;
    }

    private class ObserverInfo
    {
        public ObserverInfo(ObserverID _observerID)
        {
            observerID = _observerID;
            needsPVSUpdate = false;
            needsVisibilityUpdate = false;
            queuedForPVSUpdate = false;
            queuedForVisibilityUpdate = false;
            updateAllVisibilityStatus = false;
            nextPVSUpdateTime = new CTimeValue(0.0f);
            nextVisibilityUpdateTime = new CTimeValue(0.0f);
        }

        public ObserverID observerID;
        public ObserverParams observerParams = new ObserverParams();

        public Dictionary<ObservableID, PVSEntry> pvs = new Dictionary<ObservableID, PVSEntry>();
        public bool needsPVSUpdate;
        public bool needsVisibilityUpdate;
        public bool queuedForPVSUpdate;
        public bool queuedForVisibilityUpdate;
        public bool updateAllVisibilityStatus;
        public CTimeValue nextPVSUpdateTime;
        public CTimeValue nextVisibilityUpdateTime;
        public CTimeValue queuedForPVSUpdateTime;
        public CTimeValue queuedForVisibilityUpdateTime;
    }

    private class PendingRayInfo
    {
        public PendingRayInfo(ObserverInfo _observerInfo, PVSEntry _pvsEntry)
        {
            observerInfo = _observerInfo;
            pvsEntry = _pvsEntry;
            observerPosition = new Vec3(0, 0, 0);
            observablePosition = new Vec3(0, 0, 0);
        }

        public ObserverInfo observerInfo;
        public PVSEntry pvsEntry;

        public Vec3 observerPosition;
        public Vec3 observablePosition;
    }

    // ---- Private methods ----

    private void AcquireSkipList(IPhysicalEntity[] skipList, int skipListSize)
    {
        for (int i = 0; i < skipListSize; ++i)
            skipList[i]?.AddRef();
    }

    private void ReleaseSkipList(IPhysicalEntity[] skipList, int skipListSize)
    {
        for (int i = 0; i < skipListSize; ++i)
            skipList[i]?.Release();
    }

    private void AddToObserverPVS(ObserverInfo observerInfo, ObservableInfo observableInfo)
    {
        observerInfo.needsVisibilityUpdate = true;
        RayCastRequest.Priority priority = GetRayCastRequestPriority(observerInfo.observerParams, observableInfo.observableParams);
        if (!observerInfo.pvs.ContainsKey(observableInfo.observableID))
            observerInfo.pvs[observableInfo.observableID] = new PVSEntry(observableInfo, priority);
    }

    private void UpdateObservers()
    {
        CTimeValue now = gEnv.pTimer.GetFrameStartTime();

        // Update PVS
        foreach (var kvp in m_observers)
        {
            ObserverInfo observerInfo = kvp.Value;

            if (observerInfo.needsPVSUpdate && !observerInfo.queuedForPVSUpdate && now > observerInfo.nextPVSUpdateTime)
            {
                m_observerPVSUpdateQueue.Add(observerInfo.observerID);
                observerInfo.queuedForPVSUpdate = true;
                observerInfo.queuedForPVSUpdateTime = now;
            }
        }

        int numberOfPVSUpdatesLeft = gAIEnv.CVars.VisionMapNumberOfPVSUpdatesPerFrame;
        while (numberOfPVSUpdatesLeft > 0 && m_observerPVSUpdateQueue.Count > 0)
        {
            ObserverID frontID = m_observerPVSUpdateQueue[0];
            if (!m_observers.TryGetValue(frontID, out ObserverInfo observerInfo))
            {
                m_observerPVSUpdateQueue.RemoveAt(0);
                continue;
            }

            UpdatePVS(observerInfo);
            observerInfo.needsPVSUpdate = false;

            m_observerPVSUpdateQueue.RemoveAt(0);
            observerInfo.queuedForPVSUpdate = false;

            numberOfPVSUpdatesLeft--;

            m_pvsUpdateQueueLatency = now - observerInfo.queuedForPVSUpdateTime;
        }

        // Update Visibility
        foreach (var kvp in m_observers)
        {
            ObserverInfo observerInfo = kvp.Value;

            if (observerInfo.needsVisibilityUpdate && !observerInfo.queuedForVisibilityUpdate && now > observerInfo.nextVisibilityUpdateTime)
            {
                m_observerVisibilityUpdateQueue.Add(observerInfo.observerID);
                observerInfo.queuedForVisibilityUpdate = true;
                observerInfo.queuedForVisibilityUpdateTime = now;
            }
        }

        int numberOfVisibilityUpdatesLeft = gAIEnv.CVars.VisionMapNumberOfVisibilityUpdatesPerFrame;
        while (numberOfVisibilityUpdatesLeft > 0 && m_observerVisibilityUpdateQueue.Count > 0)
        {
            ObserverID frontID = m_observerVisibilityUpdateQueue[0];
            if (!m_observers.TryGetValue(frontID, out ObserverInfo observerInfo))
            {
                m_observerVisibilityUpdateQueue.RemoveAt(0);
                continue;
            }

            UpdateVisibilityStatus(observerInfo);
            observerInfo.nextVisibilityUpdateTime = now + observerInfo.observerParams.updatePeriod;
            observerInfo.needsVisibilityUpdate = false;

            m_observerVisibilityUpdateQueue.RemoveAt(0);
            observerInfo.queuedForVisibilityUpdate = false;

            numberOfVisibilityUpdatesLeft--;

            m_visibilityUpdateQueueLatency = now - observerInfo.queuedForVisibilityUpdateTime;
        }
    }

    private void UpdatePVS(ObserverInfo observerInfo)
    {
        ++m_numberOfPVSUpdatesThisFrame;

        var pvs = observerInfo.pvs;

        // Step1: Make sure everything in the PVS is supposed to be there. Delete what is not.
        {
            List<ObservableID> toRemove = new List<ObservableID>();
            foreach (var kvp in pvs)
            {
                ObservableID observableID = kvp.Key;
                PVSEntry pvsEntry = kvp.Value;
                ObservableInfo observableInfo = pvsEntry.observableInfo;

                if (!ShouldObserve(observerInfo, observableInfo))
                {
                    if (pvsEntry.visible)
                    {
                        TriggerObserverCallback(observerInfo, observableInfo, false);
                        TriggerObservableCallback(observerInfo, observableInfo, false);
                    }

                    DeletePendingRay(pvsEntry);
                    toRemove.Add(observableID);
                }
            }
            foreach (var id in toRemove)
                pvs.Remove(id);
        }

        // Step2: Go through all objects in range. If object is already in PVS skip it.
        // Otherwise check if it should be added and add it.
        {
            if (observerInfo.observerParams.sightRange > 0.0f)
            {
                // Query by sight range — check all observables within range
                foreach (var kvp in m_observables)
                {
                    ObservableInfo observableInfo = kvp.Value;
                    if (ShouldBeAddedToObserverPVS(observerInfo, observableInfo))
                        AddToObserverPVS(observerInfo, observableInfo);
                }
            }
            else
            {
                // the sight range is unlimited check all observables
                foreach (var kvp in m_observables)
                {
                    ObservableInfo observableInfo = kvp.Value;
                    if (ShouldBeAddedToObserverPVS(observerInfo, observableInfo))
                        AddToObserverPVS(observerInfo, observableInfo);
                }
            }
        }
    }

    private bool ShouldBeAddedToObserverPVS(ObserverInfo observerInfo, ObservableInfo observableInfo)
    {
        if (observableInfo.observableID.id == observerInfo.observerID.id)
            return false;

        if (observerInfo.pvs.ContainsKey(observableInfo.observableID))
            return false;

        return ShouldObserve(observerInfo, observableInfo);
    }

    private void UpdateVisibilityStatus(ObserverInfo observerInfo)
    {
        ++m_numberOfVisibilityUpdatesThisFrame;

        foreach (var kvp in observerInfo.pvs)
        {
            PVSEntry pvsEntry = kvp.Value;

            if (observerInfo.updateAllVisibilityStatus || pvsEntry.needsUpdate)
            {
                pvsEntry.needsUpdate = false;

                if (pvsEntry.pendingRayID.id != 0)
                    DeletePendingRay(pvsEntry);

                QueueRay(observerInfo, pvsEntry);
            }
        }

        observerInfo.updateAllVisibilityStatus = false;
    }

    private bool ShouldObserve(ObserverInfo observerInfo, ObservableInfo observableInfo)
    {
        bool matchesType = ((observerInfo.observerParams.typesToObserveMask & observableInfo.observableParams.typeMask) != 0);
        if (!matchesType)
            return false;

        bool matchesFaction = ((observerInfo.observerParams.factionsToObserveMask & (1u << observableInfo.observableParams.faction)) != 0);
        if (!matchesFaction)
            return false;

        if (!IsInSightRange(observerInfo, observableInfo))
            return false;

        if (!IsInFoV(observerInfo, observableInfo))
            return false;

        return true;
    }

    private bool IsInSightRange(ObserverInfo observerInfo, ObservableInfo observableInfo)
    {
        if (observerInfo.observerParams.sightRange <= 0.0f)
            return true;

        float distance = (observableInfo.observableParams.observablePositions[0] - observerInfo.observerParams.eyePosition).Length();
        return distance <= observerInfo.observerParams.sightRange;
    }

    private bool IsInFoV(ObserverInfo observerInfo, ObservableInfo observableInfo)
    {
        if (observerInfo.observerParams.fovCos <= -1.0f)
            return true;

        for (int i = 0; i < observableInfo.observableParams.observablePositionsCount; i++)
        {
            Vec3 directionToObservablePosition = (observableInfo.observableParams.observablePositions[i] - observerInfo.observerParams.eyePosition);
            directionToObservablePosition = directionToObservablePosition.GetNormalizedSafe();
            float dot = directionToObservablePosition.Dot(observerInfo.observerParams.eyeDirection);
            if (observerInfo.observerParams.fovCos <= dot)
                return true;
        }

        return false;
    }

    private RayCastRequest.Priority GetRayCastRequestPriority(ObserverParams observerParams, ObservableParams observableParams)
    {
        RayCastRequest.Priority priority = RayCastRequest.Priority.MediumPriority;

        uint fromTypeMask = observerParams.typeMask;
        uint toTypeMask = observableParams.typeMask;
        byte fromFaction = observerParams.faction;
        byte toFaction = observableParams.faction;

        foreach (var priorityMapEntry in m_priorityMap)
        {
            if ((priorityMapEntry.fromTypeMask & fromTypeMask) != 0 &&
                (priorityMapEntry.fromFactionMask & (1u << fromFaction)) != 0 &&
                (priorityMapEntry.toTypeMask & toTypeMask) != 0 &&
                (priorityMapEntry.toFactionMask & (1u << toFaction)) != 0)
            {
                switch (priorityMapEntry.priority)
                {
                case EVisionPriority.eLowPriority:
                    priority = RayCastRequest.Priority.LowPriority;
                    break;

                case EVisionPriority.eMediumPriority:
                    priority = RayCastRequest.Priority.MediumPriority;
                    break;

                case EVisionPriority.eHighPriority:
                    priority = RayCastRequest.Priority.HighPriority;
                    break;

                case EVisionPriority.eVeryHighPriority:
                    priority = RayCastRequest.Priority.HighestPriority;
                    break;

                default:
                    System.Diagnostics.Debug.Assert(false, "bad priority specified in vision map priority table");
                    break;
                }
                break;
            }
        }

        return priority;
    }

    private void QueueRay(ObserverInfo observerInfo, PVSEntry pvsEntry)
    {
        System.Diagnostics.Debug.Assert(pvsEntry.pendingRayID.id == 0);

        QueuedRayID queuedRayID = gAIEnv.pRayCaster.Queue(
            pvsEntry.priority,
            (QueuedRayID rid, RayCastResult res) => RayCastComplete(rid, res),
            (QueuedRayID rid, RayCastRequestWrapper req) => RayCastSubmit(rid, req));

        if (queuedRayID.id != 0)
        {
            m_pendingRays[queuedRayID.id] = new PendingRayInfo(observerInfo, pvsEntry);
            pvsEntry.rayQueueTimestamp = m_debugTimer;
        }

        pvsEntry.pendingRayID = queuedRayID;
    }

    private void DeletePendingRay(PVSEntry pvsEntry)
    {
        if (pvsEntry.pendingRayID.id == 0)
            return;

        gAIEnv.pRayCaster?.Cancel(pvsEntry.pendingRayID);
        m_pendingRays.Remove(pvsEntry.pendingRayID.id);
        pvsEntry.pendingRayID = new QueuedRayID();
    }

    private void DeletePendingRays(Dictionary<ObservableID, PVSEntry> pvs)
    {
        foreach (var kvp in pvs)
        {
            PVSEntry pvsEntry = kvp.Value;
            DeletePendingRay(pvsEntry);
        }
    }

    private bool RayCastSubmit(QueuedRayID queuedRayID, RayCastRequestWrapper rayCastRequestWrapper)
    {
        if (!m_pendingRays.TryGetValue(queuedRayID.id, out PendingRayInfo pendingRayInfo))
            return false;

        ObserverParams observerParams = pendingRayInfo.observerInfo.observerParams;
        ObservableParams observableParams = pendingRayInfo.pvsEntry.observableInfo.observableParams;

        Vec3 observerPosition = observerParams.eyePosition;
        Vec3 observablePosition = observableParams.observablePositions[pendingRayInfo.pvsEntry.currentTestPositionIndex];

        pendingRayInfo.observerPosition = observerPosition;
        pendingRayInfo.observablePosition = observablePosition;

        rayCastRequestWrapper.request.pos = observerPosition;
        rayCastRequestWrapper.request.dir = observablePosition - observerPosition;
        rayCastRequestWrapper.request.objTypes = AIPhysConstants.COVER_OBJECT_TYPES;
        rayCastRequestWrapper.request.flags = observerParams.raycastFlags;
        rayCastRequestWrapper.request.maxHitCount = 2;

        // Build skip list
        int skipListCount = observerParams.skipListSize + (int)observableParams.skipListSize;
        skipListCount = Math.Min(skipListCount, RayCastRequest.MaxSkipListCount);
        rayCastRequestWrapper.request.skipListCount = skipListCount;

        rayCastRequestWrapper.request.skipList = new IPhysicalEntity[RayCastRequest.MaxSkipListCount];
        int observerSkipListCount = Math.Min(observerParams.skipListSize, RayCastRequest.MaxSkipListCount);
        Array.Copy(observerParams.skipList, rayCastRequestWrapper.request.skipList, observerSkipListCount);

        int observableSkipListCount = Math.Min((int)observableParams.skipListSize, RayCastRequest.MaxSkipListCount - observerSkipListCount);
        Array.Copy(observableParams.skipList, 0, rayCastRequestWrapper.request.skipList, observerSkipListCount, observableSkipListCount);

        m_numberOfRayCastsSubmittedThisFrame++;

        return true;
    }

    private void RayCastComplete(QueuedRayID queuedRayID, RayCastResult rayCastResult)
    {
        if (!m_pendingRays.TryGetValue(queuedRayID.id, out PendingRayInfo pendingRayInfo))
            return;

        PVSEntry pvsEntry = pendingRayInfo.pvsEntry;

        pvsEntry.pendingRayID = new QueuedRayID();

        bool visible = !rayCastResult;

        pvsEntry.lastObserverPositionChecked = pendingRayInfo.observerPosition;
        pvsEntry.lastObservablePositionChecked = pendingRayInfo.observablePosition;

        ObserverInfo observerInfo = pendingRayInfo.observerInfo;
        ObservableInfo observableInfo = pvsEntry.observableInfo;

        if (!visible)
        {
            if (rayCastResult != null && rayCastResult.hitCount > 0)
                pvsEntry.obstructionPosition = rayCastResult[0].pt;

            if (++pvsEntry.currentTestPositionIndex < observableInfo.observableParams.observablePositionsCount)
            {
                m_pendingRays.Remove(queuedRayID.id);
                QueueRay(observerInfo, pvsEntry);
                return;
            }
        }

        pvsEntry.currentTestPositionIndex = 0;
        if (pvsEntry.visible != visible)
        {
            pvsEntry.visible = visible;

            TriggerObserverCallback(observerInfo, observableInfo, visible);
            TriggerObservableCallback(observerInfo, observableInfo, visible);

            // find the pending ray again in case it was removed by the callback
            if (m_pendingRays.ContainsKey(queuedRayID.id))
                m_pendingRays.Remove(queuedRayID.id);
        }
        else
        {
            m_pendingRays.Remove(queuedRayID.id);
        }
    }

    private void TriggerObserverCallback(ObserverInfo observerInfo, ObservableInfo observableInfo, bool visible)
    {
        observerInfo.observerParams.callback?.Invoke(observerInfo.observerID, observerInfo.observerParams, observableInfo.observableID, observableInfo.observableParams, visible);
    }

    private void TriggerObservableCallback(ObserverInfo observerInfo, ObservableInfo observableInfo, bool visible)
    {
        observableInfo.observableParams.callback?.Invoke(observerInfo.observerID, observerInfo.observerParams, observableInfo.observableID, observableInfo.observableParams, visible);
    }

    // ---- Debug ----

    private void UpdateDebugVisionMapObjects()
    {
        {
            CAIObject debugObserverAIObject = gAIEnv.pAIObjectManager?.GetAIObjectByName("VisionMapObserver") as CAIObject;
            if (debugObserverAIObject != null)
            {
                ObserverParams observerParams = new ObserverParams();
                observerParams.eyePosition = debugObserverAIObject.GetPos();
                observerParams.eyeDirection = debugObserverAIObject.GetEntityDir();

                if (!m_debugObserverVisionID)
                {
                    m_debugObserverVisionID = CreateVisionID("DebugObserver");
                    observerParams.factionsToObserveMask = 0xffffffff;
                    observerParams.typesToObserveMask = 0xffffffff;
                    observerParams.typeMask = 0xffffffff;
                    observerParams.faction = 0xff;
                    observerParams.fovCos = 0.5f;
                    observerParams.sightRange = 25.0f;
                    RegisterObserver(m_debugObserverVisionID, observerParams);
                }

                ObserverChanged(m_debugObserverVisionID, observerParams, (uint)EChangeHint.eChangedPosition | (uint)EChangeHint.eChangedOrientation);
            }
            else if (m_debugObserverVisionID)
            {
                UnregisterObserver(m_debugObserverVisionID);
                m_debugObserverVisionID = new VisionID();
            }
        }

        {
            CAIObject debugObservableAIObject = gAIEnv.pAIObjectManager?.GetAIObjectByName("VisionMapObservable") as CAIObject;
            if (debugObservableAIObject != null)
            {
                ObservableParams observableParams = new ObservableParams();
                observableParams.observablePositionsCount = 1;
                observableParams.observablePositions[0] = debugObservableAIObject.GetPos();

                if (!m_debugObservableVisionID)
                {
                    m_debugObservableVisionID = CreateVisionID("DebugObservable");
                    observableParams.typeMask = 0xffffffff;
                    observableParams.faction = 0xff;
                    RegisterObservable(m_debugObservableVisionID, observableParams);
                }

                ObservableChanged(m_debugObservableVisionID, observableParams, (uint)EChangeHint.eChangedPosition);
            }
            else if (m_debugObservableVisionID)
            {
                UnregisterObservable(m_debugObservableVisionID);
                m_debugObservableVisionID = new VisionID();
            }
        }
    }

    private void DebugDrawObservers()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        ColorB observersColor = new ColorB(0, 191, 255, 255);

        foreach (var kvp in m_observers)
        {
            ObserverInfo observerInfo = kvp.Value;

            Vec3 currentObserverPosition = observerInfo.observerParams.eyePosition;

            // Visibility Checks
            if (gAIEnv.CVars.DebugDrawVisionMapVisibilityChecks != 0)
            {
                foreach (var pvsKvp in observerInfo.pvs)
                {
                    PVSEntry pvsEntry = pvsKvp.Value;

                    Vec3 currentObservablePosition = pvsEntry.observableInfo.observableParams.observablePositions[pvsEntry.currentTestPositionIndex];
                    Vec3 lastObserverPositionChecked = pvsEntry.lastObserverPositionChecked;
                    Vec3 lastObservablePositionChecked = pvsEntry.lastObservablePositionChecked;

                    bool pvsEntryHasBeenChecked = (!lastObserverPositionChecked.IsZero() && !lastObservablePositionChecked.IsZero());

                    // move player's observable pos down a bit
                    if ((pvsEntry.observableInfo.observableParams.typeMask & VisionMapTypes.Player) != 0)
                    {
                        float zOffset = -0.05f;
                        currentObservablePosition.z += zOffset;
                        lastObservablePositionChecked.z += zOffset;
                    }

                    dc.DrawLine(currentObserverPosition, new ColorB(211, 211, 211, 255), currentObservablePosition, new ColorB(211, 211, 211, 255));

                    if (pvsEntryHasBeenChecked)
                    {
                        if (pvsEntry.visible)
                        {
                            dc.DrawLine(lastObserverPositionChecked, new ColorB(0, 100, 0, 255), lastObservablePositionChecked, new ColorB(0, 128, 0, 255), 5.0f);
                        }
                        else
                        {
                            dc.DrawLine(lastObserverPositionChecked, new ColorB(205, 92, 92, 255), pvsEntry.obstructionPosition, new ColorB(255, 0, 0, 255), 5.0f);
                        }
                    }
                }
            }

            if (gAIEnv.CVars.DebugDrawVisionMapObservers != 0)
            {
                // Location
                dc.DrawWireSphere(currentObserverPosition, 0.15f, observersColor);

                // FOVs
                if (gAIEnv.CVars.DebugDrawVisionMapObserversFOV != 0)
                {
                    ObserverParams observerParams = observerInfo.observerParams;
                    Vec3 eyePosition = observerParams.eyePosition;
                    Vec3 eyeDirection = observerParams.eyeDirection;

                    if (eyeDirection.IsZero())
                        continue;

                    eyeDirection = eyeDirection.GetNormalizedSafe();

                    dc.DrawLine(eyePosition, observersColor, eyePosition + eyeDirection * 2.5f, observersColor, 5.0f);
                }
            }
        }
    }

    private void DebugDrawObservables()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        ColorB observablesColor = new ColorB(204, 127, 50, 255);

        foreach (var kvp in m_observables)
        {
            ObservableInfo observableInfo = kvp.Value;

            for (int i = 0; i < observableInfo.observableParams.observablePositionsCount; i++)
            {
                dc.DrawWireSphere(observableInfo.observableParams.observablePositions[i], 0.10f, observablesColor);
            }
        }
    }

    private void DebugDrawVisionMapStats()
    {
        // Stats display — simplified version of the C++ format string
        int numObservers = m_observers.Count;
        int numObservables = m_observables.Count;

        CDebugDrawContext dc = new CDebugDrawContext();

        string text = string.Format(
            "# Observers: {0}\n# Observables: {1}\n# PVS updates: {2}\n# Visibility updates: {3}\n# Raycast submits: {4}\n",
            numObservers, numObservables,
            m_numberOfPVSUpdatesThisFrame,
            m_numberOfVisibilityUpdatesThisFrame,
            m_numberOfRayCastsSubmittedThisFrame);

        dc.Draw2dLabel(400, 200, 1.5f, new ColorB(255, 255, 255, 255), false, text);
    }

    // ---- Private fields ----

    private Dictionary<ObserverID, ObserverInfo> m_observers = new Dictionary<ObserverID, ObserverInfo>();
    private Dictionary<ObservableID, ObservableInfo> m_observables = new Dictionary<ObservableID, ObservableInfo>();

    private List<ObserverID> m_observerPVSUpdateQueue = new List<ObserverID>();
    private List<ObserverID> m_observerVisibilityUpdateQueue = new List<ObserverID>();

    private Dictionary<int, PendingRayInfo> m_pendingRays = new Dictionary<int, PendingRayInfo>();
    private List<PriorityMapEntry> m_priorityMap = new List<PriorityMapEntry>();

    private float m_debugTimer;
    private uint m_numberOfPVSUpdatesThisFrame;
    private uint m_numberOfVisibilityUpdatesThisFrame;
    private uint m_numberOfRayCastsSubmittedThisFrame;

    private VisionID m_debugObserverVisionID;
    private VisionID m_debugObservableVisionID;

    private CTimeValue m_pvsUpdateQueueLatency;
    private CTimeValue m_visibilityUpdateQueueLatency;

    private uint m_visionIdCounter;
}

// CAIRadialOcclusion shell (~1012L .h+.cpp combined)
public class CAIRadialOcclusion
{
    public CAIRadialOcclusion() { /* impl in .cpp */ }
}

// CAIRadialOcclusionRaycast shell
public class CAIRadialOcclusionRaycast
{
    public CAIRadialOcclusionRaycast() { /* impl in .cpp */ }
}

// CPerceptionManager shell (~2003L .h+.cpp combined)
public class CPerceptionManager : IPerceptionManager
{
    public CPerceptionManager() { /* impl in .cpp */ }
}

// CGlobalPerceptionScaleHandler shell
public class CGlobalPerceptionScaleHandler
{
    public CGlobalPerceptionScaleHandler() { /* impl in .cpp */ }
}

// CCentralInterestManager shell (~1116L .h+.cpp combined)
public class CCentralInterestManager : ICentralInterestManager
{
    public CCentralInterestManager() { /* impl in .cpp */ }
    private static CCentralInterestManager _instance = new CCentralInterestManager();
    public static CCentralInterestManager GetInstance() { return _instance; }
    public void ChangeInterestingnessByDelta(IEntity pEntity, float delta) { /* impl pending */ }
    public CPersonalInterestManager FindPIM(CPuppet pPuppet) { return pPuppet?.m_pPersonalInterestManager; }
}

// CPersonalInterestManager shell — note: forward decl exists in Puppet.cs
// public class CPersonalInterestManager already declared

// CMissLocationSensor shell — note: forward decl exists in AIPlayer.cs
// public class CMissLocationSensor already declared

// CAILightManager now defined in Environment.cs

// Forward decls / shells from IVisionMap.h, IPerceptionHandler.h, IInterestSystem.h
public interface IVisionMap
{
    void Reset();
    VisionID CreateVisionID(string name);
    void RegisterObserver(ObserverID observerID, ObserverParams observerParams);
    void UnregisterObserver(ObserverID observerID);
    void RegisterObservable(ObservableID observableID, ObservableParams observerParams);
    void UnregisterObservable(ObservableID observableID);
    void ObserverChanged(ObserverID observerID, ObserverParams observerParams, uint hint);
    void ObservableChanged(ObservableID observableID, ObservableParams newObservableParams, uint hint);
    bool IsVisible(ObserverID observerID, ObservableID observableID);
    ObserverParams GetObserverParams(ObserverID observerID);
    ObservableParams GetObservableParams(ObservableID observableID);
    void AddPriorityMapEntry(PriorityMapEntry priorityMapEntry);
    void ClearPriorityMap();
    void Update(float frameTime);
}

public interface IPerceptionManager { }
public interface ICentralInterestManager { }

// In C++ ObserverID/ObservableID are typedefs of VisionID. The literal port mirrors that with
// implicit conversions in both directions.
public struct ObserverID : System.IEquatable<ObserverID>
{
    public uint id;
    public static implicit operator ObserverID(VisionID v) => new ObserverID { id = v.id };
    public static implicit operator VisionID(ObserverID o) => new VisionID { id = o.id };
    public static implicit operator ObserverID(uint v) => new ObserverID { id = v };
    public static implicit operator uint(ObserverID o) => o.id;
    public static implicit operator bool(ObserverID o) => o.id != 0;
    public static bool operator !(ObserverID o) => o.id == 0;
    public static bool operator ==(ObserverID a, ObserverID b) => a.id == b.id;
    public static bool operator !=(ObserverID a, ObserverID b) => a.id != b.id;
    public bool Equals(ObserverID other) => id == other.id;
    public override bool Equals(object obj) => obj is ObserverID o && id == o.id;
    public override int GetHashCode() => (int)id;
}
public struct ObservableID : System.IEquatable<ObservableID>
{
    public uint id;
    public static implicit operator ObservableID(VisionID v) => new ObservableID { id = v.id };
    public static implicit operator VisionID(ObservableID o) => new VisionID { id = o.id };
    public static implicit operator ObservableID(uint v) => new ObservableID { id = v };
    public static implicit operator uint(ObservableID o) => o.id;
    public static implicit operator bool(ObservableID o) => o.id != 0;
    public static bool operator !(ObservableID o) => o.id == 0;
    public static bool operator ==(ObservableID a, ObservableID b) => a.id == b.id;
    public static bool operator !=(ObservableID a, ObservableID b) => a.id != b.id;
    public bool Equals(ObservableID other) => id == other.id;
    public override bool Equals(object obj) => obj is ObservableID o && id == o.id;
    public override int GetHashCode() => (int)id;
}
// EVisionPriority — literal port of IVisionMap.h
public enum EVisionPriority
{
    eLowPriority = 0,
    eMediumPriority = 1,
    eHighPriority = 2,
    eVeryHighPriority = 3,
}

// VisionMapTypes — literal port of VisionMapTypes.h
public static class VisionMapTypes
{
    public const uint General = 1u << 0;
    public const uint AliveAgent = 1u << 1;
    public const uint DeadAgent = 1u << 2;
    public const uint Player = 1u << 3;
    public const uint Interesting = 1u << 4;
    public const uint SearchSpots = 1u << 5;
}

public class PriorityMapEntry
{
    public uint fromTypeMask = 0xFFFFFFFF;
    public uint fromFactionMask = 0xFFFFFFFF;
    public uint toTypeMask = 0xFFFFFFFF;
    public uint toFactionMask = 0xFFFFFFFF;
    public EVisionPriority priority = EVisionPriority.eMediumPriority;
}
