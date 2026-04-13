// Literal port of:
//   IslandConnectionsManager.cpp (50L) + .h (41L)
//   VolumesManager.cpp (121L) + .h (49L)
//   WorldMonitor.cpp (158L) + .h (41L)
//   OffMeshNavigationManager.cpp (793L) + .h (173L)
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CryAISystem.CryCommon;
using MNM = CryAISystem.Navigation.MNM;

namespace CryAISystem.Navigation.NavigationSystem;

// ======================================================================
// IslandConnectionsManager — IslandConnectionsManager.h/cpp (41L + 50L)
// ======================================================================
public class IslandConnectionsManager
{
    private MNM.IslandConnections m_globalIslandConnections = new MNM.IslandConnections();

    public IslandConnectionsManager()
    {
    }

    public void Reset()
    {
        m_globalIslandConnections.Reset();
    }

    public MNM.IslandConnections GetIslandConnections()
    {
        return m_globalIslandConnections;
    }

    public void SetOneWayConnectionBetweenIsland(MNM.GlobalIslandID fromIsland, MNM.IslandConnections.Link link)
    {
        m_globalIslandConnections.SetOneWayConnectionBetweenIsland(fromIsland, link);
    }

    public bool AreIslandsConnected(IEntity pEntityToTestOffGridLinks, MNM.GlobalIslandID startIsland, MNM.GlobalIslandID endIsland)
    {
        // FUNCTION_PROFILER(gEnv->pSystem, PROFILE_AI);

        MNM.IslandConnections.TIslandsWay way = new MNM.IslandConnections.TIslandsWay();
        return m_globalIslandConnections.CanNavigateBetweenIslands(pEntityToTestOffGridLinks, startIsland, endIsland, way);
    }

#if CRYAISYSTEM_DEBUG
    public void DebugDraw()
    {
        m_globalIslandConnections.DebugDraw();
    }
#endif
}

// ======================================================================
// CVolumesManager — VolumesManager.h/cpp (49L + 121L)
// ======================================================================
public class CVolumesManager
{
    // typedef std::map<string, NavigationVolumeID> VolumesMap;
    private Dictionary<string, NavigationVolumeID> m_volumeAreas = new Dictionary<string, NavigationVolumeID>();

    public CVolumesManager() { }

    public bool RegisterArea(string volumeName)
    {
        if (!m_volumeAreas.ContainsKey(volumeName))
        {
            m_volumeAreas[volumeName] = new NavigationVolumeID();
        }
        else
        {
            AILog.AIWarning("You are trying to register the area {0} but it's already registered.", volumeName);
            return false;
        }
        return true;
    }

    public bool SetAreaID(string volumeName, NavigationVolumeID id)
    {
        if (IsAreaPresent(volumeName))
        {
            m_volumeAreas[volumeName] = id;
        }
        else
        {
            AILog.AIWarning("The area {0} is not registered in the system.", volumeName);
            return false;
        }
        return true;
    }

    public void UnRegisterArea(string volumeName)
    {
        m_volumeAreas.Remove(volumeName);
    }

    public bool IsAreaPresent(string volumeName)
    {
        return m_volumeAreas.ContainsKey(volumeName);
    }

    public NavigationVolumeID GetAreaID(string volumeName)
    {
        NavigationVolumeID areaID = new NavigationVolumeID();
        if (m_volumeAreas.TryGetValue(volumeName, out NavigationVolumeID val))
            areaID = val;
        return areaID;
    }

    public bool GetAreaName(NavigationVolumeID id, ref string name)
    {
        foreach (var kvp in m_volumeAreas)
        {
            if (kvp.Value == id)
            {
                name = kvp.Key;
                return true;
            }
        }
        return false;
    }

    public void UpdateNameForAreaID(NavigationVolumeID id, string newName)
    {
        string keyToRemove = null;
        foreach (var kvp in m_volumeAreas)
        {
            if (kvp.Value == id)
            {
                keyToRemove = kvp.Key;
                break;
            }
        }
        if (keyToRemove != null)
        {
            m_volumeAreas.Remove(keyToRemove);
        }
        m_volumeAreas[newName] = id;
    }

    public void InvalidateID(NavigationVolumeID id)
    {
        string keyToInvalidate = null;
        foreach (var kvp in m_volumeAreas)
        {
            if (kvp.Value == id)
            {
                keyToInvalidate = kvp.Key;
                break;
            }
        }
        if (keyToInvalidate != null)
        {
            m_volumeAreas[keyToInvalidate] = new NavigationVolumeID();
            return;
        }
        AILog.AIWarning("There is no navigation shape with assigned the ID:{0}", (uint)id);
    }

    public void GetVolumesNames(List<string> names)
    {
        names.Capacity = Math.Max(names.Capacity, names.Count + m_volumeAreas.Count);
        foreach (var kvp in m_volumeAreas)
        {
            names.Add(kvp.Key);
        }
    }
}

// ======================================================================
// WorldMonitor — WorldMonitor.h/cpp (41L + 158L)
// ======================================================================
public class WorldMonitor
{
    // typedef Functor1<const AABB&> Callback;
    public delegate void Callback(AABB aabb);

    private Callback m_callback;
    private bool m_enabled;

    public WorldMonitor()
    {
        m_callback = null;
        m_enabled = gEnv.IsEditor();
    }

    public WorldMonitor(Callback callback)
    {
        m_callback = callback;
        m_enabled = gEnv.IsEditor();
    }

    public void Start()
    {
        if (IsEnabled())
        {
            if (m_callback != null)
            {
                // gEnv->pPhysicalWorld->AddEventClient(EventPhysStateChange::id, WorldMonitor::StateChangeHandler, 1, 1.0f);
                // gEnv->pPhysicalWorld->AddEventClient(EventPhysEntityDeleted::id, WorldMonitor::EntityRemovedHandler, 1, 1.0f);
                // Physics event client registration — requires IPhysicalWorld.AddEventClient (Phase 12)
            }
        }
    }

    public void Stop()
    {
        if (IsEnabled())
        {
            if (m_callback != null)
            {
                // gEnv->pPhysicalWorld->RemoveEventClient(EventPhysStateChange::id, WorldMonitor::StateChangeHandler, 1);
                // gEnv->pPhysicalWorld->RemoveEventClient(EventPhysEntityDeleted::id, WorldMonitor::EntityRemovedHandler, 1);
                // Physics event client deregistration — requires IPhysicalWorld.RemoveEventClient (Phase 12)
            }
        }
    }

    public bool IsEnabled()
    {
        return m_enabled;
    }

    // static int StateChangeHandler(const EventPhys* pPhysEvent)
    // The C++ version is a static handler registered with the physics world.
    // In C# we keep the same structure/logic as a static method.
    public static int StateChangeHandler(object pPhysEvent)
    {
        WorldMonitor pthis = gAIEnv.pNavigationSystem.GetWorldMonitor();

        Debug.Assert(pthis != null);
        Debug.Assert(pthis.IsEnabled());

        // The C++ implementation casts the EventPhys* to EventPhysStateChange* and checks
        // simulation class. Full implementation requires physics event types from CryPhysics (Phase 12).
        // The structure is faithfully preserved here:
        //
        // EventPhysStateChange event = (EventPhysStateChange)pPhysEvent;
        // bool consider = false;
        // if (event.iSimClass[1] == SC_STATIC) consider = true;
        // else { ... check SC_SLEEPING_RIGID, SC_ACTIVE_RIGID, PE_RIGID ... }
        // if (consider) {
        //     AABB aabbOld(event.BBoxOld[0], event.BBoxOld[1]);
        //     AABB aabbNew(event.BBoxNew[0], event.BBoxNew[1]);
        //     if (((aabbOld.min - aabbNew.min).len2() + (aabbOld.max - aabbNew.max).len2()) > 0.0f) {
        //         pthis.m_callback(aabbOld);
        //         pthis.m_callback(aabbNew);
        //     }
        // }

        return 1;
    }

    // static int EntityRemovedHandler(const EventPhys* pPhysEvent)
    public static int EntityRemovedHandler(object pPhysEvent)
    {
        WorldMonitor pthis = gAIEnv.pNavigationSystem.GetWorldMonitor();

        Debug.Assert(pthis != null);
        Debug.Assert(pthis.IsEnabled());

        // EventPhysEntityDeleted event = (EventPhysEntityDeleted)pPhysEvent;
        // bool consider = false;
        // IPhysicalEntity physEnt = event.pEntity;
        // pe_type type = physEnt.GetType();
        // if (type == PE_STATIC) consider = true;
        // else if (type == PE_RIGID) consider = NavigationSystemUtils.IsDynamic...
        // if (consider) { pe_status_pos sp; if (physEnt.GetStatus(&sp)) { ... } }

        return 1;
    }
}

// ======================================================================
// OffMeshNavigationManager — OffMeshNavigationManager.h/cpp (173L + 793L)
// ======================================================================
public class OffMeshNavigationManager : IOffMeshNavigationManager
{
    // ----- Private types -----

    private class SLinkInfo
    {
        public SLinkInfo() { }
        public SLinkInfo(NavigationMeshID meshId, uint triangleID, uint linkID, MNM.OffMeshLink offMeshLink)
        {
            this.meshID = meshId;
            this.triangleID = triangleID;
            this.linkID = linkID;
            this.offMeshLink = offMeshLink;
        }

        public NavigationMeshID meshID;
        public uint triangleID;   // MNM::TriangleID
        public uint linkID;       // MNM::OffMeshLinkID
        public MNM.OffMeshLink offMeshLink; // MNM::OffMeshLinkPtr
    }

    // Tracking of objects registered — OffMeshLinkIDList
    private class OffMeshLinkIDList
    {
        // typedef std::vector<MNM::OffMeshLinkID> TLinkIDList;
        private List<uint> offMeshLinkIDList = new List<uint>();

        public List<uint> GetLinkIDList() { return offMeshLinkIDList; }

        public void OnLinkAddedSuccesfullyForSmartObject(uint linkID)
        {
            Debug.Assert(linkID != MNM.Constants.eOffMeshLinks_InvalidOffMeshLinkID);
            offMeshLinkIDList.Add(linkID);
        }
    }

    // ----- Private fields -----

    // typedef id_map<uint32, MNM::OffMeshNavigation> TOffMeshMap;
    private id_map<uint, MNM.OffMeshNavigation> m_offMeshMap;

    private MNM.OffMeshNavigation m_emptyOffMeshNavigation = new MNM.OffMeshNavigation();

    // typedef std::map<EntityId, TSOClassInfo> TRegisteredObjects;
    // TSOClassInfo = std::map<uint32, OffMeshLinkIDList>
    private Dictionary<uint, Dictionary<uint, OffMeshLinkIDList>> m_registeredObjects = new Dictionary<uint, Dictionary<uint, OffMeshLinkIDList>>();

    // typedef std::map<MNM::OffMeshLinkID, SLinkInfo> TLinkInfoMap;
    private Dictionary<uint, SLinkInfo> m_links = new Dictionary<uint, SLinkInfo>();

    private bool m_objectRegistrationEnabled;

    // typedef std::vector<MNM::OffMeshOperationRequestBase> Operations;
    private List<MNM.OffMeshOperationRequestBase> m_operations;

    // typedef CListenerSet<IOffMeshNavigationListener*> Listeners;
    private List<IOffMeshNavigationListener> m_listeners;

    // ----- Constructor -----

    public OffMeshNavigationManager(int offMeshMapSize)
    {
        m_offMeshMap = new id_map<uint, MNM.OffMeshNavigation>(offMeshMapSize);
        m_objectRegistrationEnabled = false;
        m_operations = new List<MNM.OffMeshOperationRequestBase>(128);
        m_listeners = new List<IOffMeshNavigationListener>(32);
    }

    // Default ctor for shells
    public OffMeshNavigationManager() : this(256) { }

    // ----- Public methods -----

    public MNM.OffMeshNavigation GetOffMeshNavigationForMesh(NavigationMeshID meshID)
    {
        if (meshID.id != 0 && m_offMeshMap.validate(meshID.id))
        {
            return m_offMeshMap[meshID.id];
        }
        else
        {
            Debug.Assert(false);
            AILog.AIWarning("No off-mesh navigation available, returning empty one! Navigation might not be able to use external links when path-finding");
            return m_emptyOffMeshNavigation;
        }
    }

    // IOffMeshNavigationManager
    public virtual void QueueCustomLinkAddition(MNM.LinkAdditionRequest request)
    {
        m_operations.Add(request);
    }

    public virtual void QueueCustomLinkRemoval(MNM.LinkRemovalRequest request)
    {
        m_operations.Add(request);
    }

    public virtual MNM.OffMeshLink GetOffMeshLink(uint linkID)
    {
        if (m_links.TryGetValue(linkID, out SLinkInfo linkInfo))
        {
            Debug.Assert(m_offMeshMap.validate(linkInfo.meshID.id));
            MNM.OffMeshNavigation offMeshNavigation = m_offMeshMap[linkInfo.meshID.id];

            return offMeshNavigation.GetObjectLinkInfo(linkID);
        }

        return null;
    }

    public virtual void RegisterListener(IOffMeshNavigationListener pListener, string listenerName)
    {
        if (!m_listeners.Contains(pListener))
            m_listeners.Add(pListener);
    }

    public virtual void UnregisterListener(IOffMeshNavigationListener pListener)
    {
        m_listeners.Remove(pListener);
    }

    public virtual void RemoveAllQueuedAdditionRequestForEntity(uint requestOwner)
    {
        m_operations.RemoveAll(op =>
            op.operationType == MNM.EOffMeshOperationType.eOffMeshOperationType_Add &&
            op.requestOwnerId == requestOwner);
    }
    // ~IOffMeshNavigationManager

    public void ProcessQueuedRequests()
    {
        if (m_operations.Count > 0)
        {
            foreach (var op in m_operations)
            {
                switch (op.operationType)
                {
                    case MNM.EOffMeshOperationType.eOffMeshOperationType_Add:
                    {
                        MNM.LinkAdditionRequest addReq = (MNM.LinkAdditionRequest)op;
                        uint linkId = addReq.linkId;
                        if (AddCustomLink(addReq.meshId, addReq.pLinkData, ref linkId, addReq.dataExists, addReq.trimExcess))
                        {
                            addReq.linkId = linkId;
                            if (addReq.callback != null)
                            {
                                addReq.callback(addReq.linkId);
                            }
                        }
                        break;
                    }
                    case MNM.EOffMeshOperationType.eOffMeshOperationType_Remove:
                    {
                        MNM.LinkRemovalRequest remReq = (MNM.LinkRemovalRequest)op;
                        NotifyAllListenerAboutLinkDeletion(remReq.linkId);
                        RemoveCustomLink(remReq.linkId);
                        break;
                    }
                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            m_operations.Clear();
            m_operations.Capacity = 0; // stl::free_container
        }
    }

    public void RefreshConnections(NavigationMeshID meshID, uint tileID)
    {
        if (m_offMeshMap.validate(meshID.id))
        {
            MNM.OffMeshNavigation offMeshNavigation = m_offMeshMap[meshID.id];

            // Invalidate links for the tile.
            offMeshNavigation.InvalidateLinks(tileID);

            // Refresh all links tied to this mesh and tile
            var linksToRefresh = new List<KeyValuePair<uint, SLinkInfo>>();
            foreach (var kvp in m_links)
            {
                SLinkInfo linkInfo = kvp.Value;
                if (linkInfo.meshID.id != meshID.id)
                    continue;

                if (tileID != MNM.MNMUtils.ComputeTileID(linkInfo.triangleID))
                    continue;

                linksToRefresh.Add(kvp);
            }

            foreach (var kvp in linksToRefresh)
            {
                SLinkInfo linkInfo = kvp.Value;
                MNM.LinkAdditionRequest request = new MNM.LinkAdditionRequest(
                    linkInfo.offMeshLink.GetEntityIdForOffMeshLink(), meshID, linkInfo.offMeshLink, linkInfo.linkID);
                QueueCustomLinkAddition(request);
            }

            List<uint> tempObjectIds = new List<uint>();
            tempObjectIds.Capacity = m_registeredObjects.Count;

            // Find object's smart classes which were unable to register before
            foreach (var objectKvp in m_registeredObjects)
            {
                var classInfos = objectKvp.Value;
                foreach (var classKvp in classInfos)
                {
                    List<uint> linkList = classKvp.Value.GetLinkIDList();
                    if (linkList.Count != 0)
                        continue;

                    tempObjectIds.Add(objectKvp.Key);
                }
            }

            // Register again those objects which could have been affected by the change
            // Full implementation requires CSmartObject.GetClasses() and CSmartObjectClass (Phase 11)
            foreach (uint objectId in tempObjectIds)
            {
                CSmartObject pSmartObject = CSmartObjectManager.GetSmartObject(objectId);
                Debug.Assert(pSmartObject != null);
                // if (pSmartObject != null) { ... RegisterSmartObject for each class ... }
                // Deferred — CSmartObject.GetClasses() / CSmartObjectClass not yet ported
            }
        }
    }

    public void Clear()
    {
        m_offMeshMap.clear();
        m_registeredObjects.Clear();
        m_links.Clear();
        m_objectRegistrationEnabled = false;

        m_operations.Clear();
        m_operations.Capacity = 0; // stl::free_container
    }

    public void Enable()
    {
        m_objectRegistrationEnabled = true;
    }

    public void OnNavigationMeshCreated(NavigationMeshID meshID)
    {
        Debug.Assert(!m_offMeshMap.validate(meshID.id));

        m_offMeshMap.insert(meshID.id, new MNM.OffMeshNavigation());
    }

    public void OnNavigationMeshDestroyed(NavigationMeshID meshID)
    {
        Debug.Assert(m_offMeshMap.validate(meshID.id));

        m_offMeshMap.erase(meshID.id);

        // Remove all existing links for this mesh
        List<uint> removedLinkIDs = new List<uint>();
        List<uint> keysToRemove = new List<uint>();
        foreach (var kvp in m_links)
        {
            SLinkInfo linkInfo = kvp.Value;
            if (linkInfo.meshID.id != meshID.id)
                continue;

            removedLinkIDs.Add(linkInfo.linkID);
            keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
        {
            m_links.Remove(key);
        }

        // Remove smart object references to removed link IDs
        foreach (var objectKvp in m_registeredObjects)
        {
            foreach (var classInfoKvp in objectKvp.Value)
            {
                List<uint> linkList = classInfoKvp.Value.GetLinkIDList();
                for (int i = 0; i < removedLinkIDs.Count; ++i)
                {
                    linkList.Remove(removedLinkIDs[i]);
                }
            }
        }
    }

    public void OnNavigationLoadedComplete()
    {
        // Only after the navigation loaded process is completed off-mesh links can be created
        Enable();
    }

    public bool IsObjectLinkedWithNavigationMesh(uint objectId)
    {
        if (m_registeredObjects.TryGetValue(objectId, out var classInfo))
        {
            return classInfo.Count > 0;
        }
        return false;
    }

    public void RegisterSmartObject(CSmartObject pSmartObject, CSmartObjectClass pSmartObjectClass)
    {
        Debug.Assert(pSmartObject != null && pSmartObjectClass != null);

        if (!CanRegisterObject())
            return;

        // Full implementation requires CSmartObjectClass.IsSmartObjectUser(), CSmartObject.GetEntityId(),
        // CSmartObjectClass.GetName(), CRegisterSOHelper, m_vHelperLinks, m_setNavHelpers etc.
        // All deferred to Phase 11 when CSmartObject/CSmartObjectClass are fully ported.
        //
        // C++ structure:
        // if (pSmartObjectClass.IsSmartObjectUser()) return;
        // CRegisterSOHelper registerSOHelper(...);
        // if (!registerSOHelper.CanRegister()) return;
        // if (ObjectRegistered(entityId, className)) UnregisterSmartObject(pSmartObject, className);
        // ... register and create off-mesh links ...
    }

    public void UnregisterSmartObjectForAllClasses(CSmartObject pSmartObject)
    {
        // Full implementation requires CSmartObject.GetClasses() / CSmartObjectClass.GetName()
        // Deferred to Phase 11 when CSmartObject/CSmartObjectClass are fully ported.
        //
        // C++ structure:
        // CSmartObjectClasses& classes = pSmartObject->GetClasses();
        // for (auto it = classes.begin(); it != classes.end(); ++it)
        //     UnregisterSmartObject(pSmartObject, (*it)->GetName());
    }

#if DEBUG_MNM_ENABLED
    public void UpdateEditorDebugHelpers()
    {
        if (!gEnv.IsEditing())
            return;

        // Debug rendering code — requires IRenderAuxGeom, IEntity rendering etc.
        // Faithfully structured but rendering calls are deferred (Phase 12 debug rendering).
    }
#else
    public void UpdateEditorDebugHelpers() { }
#endif

    // ----- Private methods -----

    private bool AddCustomLink(NavigationMeshID meshID, MNM.OffMeshLink linkData, ref uint linkID, bool dataExists = false, bool trimExcess = false)
    {
        // Grab the navigation mesh
        NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(meshID);

        // Query the entry/exit positions
        Vec3 startPoint = linkData.GetStartPosition();
        Vec3 endPoint = linkData.GetEndPosition();

        MNM.vector3_t fixedStartPoint = new MNM.vector3_t(startPoint);
        MNM.vector3_t fixedEndPoint = new MNM.vector3_t(endPoint);
        MNM.vector3_t fixedMidPoint = new MNM.vector3_t((startPoint + endPoint) * 0.5f);

        uint startTriangleID, endTriangleID;

        MNM.real_t range = new MNM.real_t(1.0f);
        MNM.vector3_t startEdgePoint = new MNM.vector3_t(), endEdgePoint = new MNM.vector3_t();

        // Get entry triangle
        if (trimExcess)
        {
            startTriangleID = mesh.grid.GetTriangleEdgeAlongLine(fixedMidPoint, fixedStartPoint, range, range, ref startEdgePoint);
        }
        else
        {
            startTriangleID = mesh.grid.GetTriangleAt(fixedStartPoint, range, range);
        }

        if (startTriangleID == 0)
        {
            return false;
        }

        // Get exit triangle
        if (trimExcess)
        {
            endTriangleID = mesh.grid.GetTriangleEdgeAlongLine(fixedMidPoint, fixedEndPoint, range, range, ref endEdgePoint);
        }
        else
        {
            endTriangleID = mesh.grid.GetTriangleAt(fixedEndPoint, range, range);
        }

        if (endTriangleID == 0)
        {
            return false;
        }

        if (startTriangleID == endTriangleID)
        {
            return false;
        }

        // If trimming excess length, set the new entry/exit
        if (trimExcess)
        {
            linkData.SetStartPosition(startEdgePoint.GetVec3());
            linkData.SetEndPosition(endEdgePoint.GetVec3());
        }

        // Select the corresponding off-mesh navigation object
        Debug.Assert(m_offMeshMap.validate(meshID.id));
        MNM.OffMeshNavigation offMeshNavigation = m_offMeshMap[meshID.id];

        // Register the new link with the off-mesh navigation system
        MNM.OffMeshLink pClonedData = offMeshNavigation.AddLink(mesh, startTriangleID, endTriangleID, linkData, ref linkID, dataExists);
        Debug.Assert(linkID > 0);

        // If not updating an existing link, cache it
        if (dataExists == false)
        {
            m_links[linkID] = new SLinkInfo(meshID, startTriangleID, linkID, pClonedData);
        }
        else
        {
            if (m_links.TryGetValue(linkID, out SLinkInfo existingInfo))
            {
                existingInfo.triangleID = startTriangleID;
            }
        }

        gAIEnv.pNavigationSystem.AddIslandConnectionsBetweenTriangles(meshID, startTriangleID, endTriangleID);

        return true;
    }

    private void RemoveCustomLink(uint linkID)
    {
        if (m_links.TryGetValue(linkID, out SLinkInfo linkInfo))
        {
            Debug.Assert(m_offMeshMap.validate(linkInfo.meshID.id));
            MNM.OffMeshNavigation offMeshNavigation = m_offMeshMap[linkInfo.meshID.id];
            NavigationMesh mesh = gAIEnv.pNavigationSystem.GetMesh(linkInfo.meshID);

            offMeshNavigation.RemoveLink(mesh, linkInfo.triangleID, linkID);

            gAIEnv.pNavigationSystem.RemoveAllIslandConnectionsForObject(linkInfo.meshID, linkID);

            m_links.Remove(linkID);
        }
    }

    private void UnregisterSmartObject(uint objectId, string smartObjectClassName)
    {
        if (m_registeredObjects.TryGetValue(objectId, out var soClassInfoMap))
        {
            uint smartObjectClassNameCrC = CCrc32.ComputeLowercase(smartObjectClassName);
            if (soClassInfoMap.TryGetValue(smartObjectClassNameCrC, out var linkIdList))
            {
                List<uint> linkList = linkIdList.GetLinkIDList();
                for (int i = 0; i < linkList.Count; ++i)
                {
                    MNM.LinkRemovalRequest request = new MNM.LinkRemovalRequest(objectId, linkList[i]);
                    QueueCustomLinkRemoval(request);
                }

                soClassInfoMap.Remove(smartObjectClassNameCrC);
            }

            if (soClassInfoMap.Count == 0)
            {
                RemoveAllQueuedAdditionRequestForEntity(objectId);
                m_registeredObjects.Remove(objectId);
            }
        }
    }

    private bool ObjectRegistered(uint objectId, string smartObjectClassName)
    {
        uint smartObjectClassNameCrC = CCrc32.ComputeLowercase(smartObjectClassName);
        if (m_registeredObjects.TryGetValue(objectId, out var soClassInfoMap))
        {
            return soClassInfoMap.ContainsKey(smartObjectClassNameCrC);
        }
        return false;
    }

    private bool CanRegisterObject() { return m_objectRegistrationEnabled; }

    private void NotifyAllListenerAboutLinkDeletion(uint linkID)
    {
        foreach (var listener in m_listeners)
        {
            listener.OnOffMeshLinkGoingToBeRemoved(linkID);
        }
    }

    private void NotifyAllListenerAboutLinkAddition(uint linkID)
    {
        // Not implemented in original C++
    }
}

// ======================================================================
// Supporting types — NavigationVolumeID
// ======================================================================

// NavigationVolumeID — literal from INavigationSystem.h
public struct NavigationVolumeID : IEquatable<NavigationVolumeID>
{
    public uint id;
    public NavigationVolumeID(uint id = 0) { this.id = id; }
    public static implicit operator uint(NavigationVolumeID v) => v.id;
    public static implicit operator bool(NavigationVolumeID v) => v.id != 0;
    public static bool operator ==(NavigationVolumeID a, NavigationVolumeID b) => a.id == b.id;
    public static bool operator !=(NavigationVolumeID a, NavigationVolumeID b) => a.id != b.id;
    public override bool Equals(object obj) => obj is NavigationVolumeID other && id == other.id;
    public bool Equals(NavigationVolumeID other) => id == other.id;
    public override int GetHashCode() => (int)id;
}

// IOffMeshNavigationManager interface — from IOffMeshNavigationManager.h
public interface IOffMeshNavigationManager
{
    void QueueCustomLinkAddition(MNM.LinkAdditionRequest request);
    void QueueCustomLinkRemoval(MNM.LinkRemovalRequest request);
    MNM.OffMeshLink GetOffMeshLink(uint linkID);
    void RegisterListener(IOffMeshNavigationListener pListener, string listenerName);
    void UnregisterListener(IOffMeshNavigationListener pListener);
    void RemoveAllQueuedAdditionRequestForEntity(uint requestOwner);
}

// IOffMeshNavigationListener — from IOffMeshNavigationManager.h
public interface IOffMeshNavigationListener
{
    void OnOffMeshLinkGoingToBeRemoved(uint linkID);
}

// NavigationSystemUtils — from NavigationSystem.h (inline function)
public static class NavigationSystemUtils
{
    public static bool IsDynamicObjectPartOfTheMNMGenerationProcess(IPhysicalEntity pPhysicalEntity)
    {
        if (pPhysicalEntity != null)
        {
            pe_status_dynamics dyn = new pe_status_dynamics();
            if (pPhysicalEntity.GetStatus(dyn) != 0 && (dyn.mass <= 1e-6f))
                return true;
        }

        return false;
    }
}

// AILog — wrapper for AIWarning calls
public static class AILog
{
    public static void AIWarning(string format, params object[] args)
    {
        System.Diagnostics.Debug.WriteLine(string.Format("[AIWarning] " + format, args));
    }

    public static void AIError(string format, params object[] args)
    {
        System.Diagnostics.Debug.WriteLine(string.Format("[AIError] " + format, args));
    }
}

// CSmartObjectClasses — used by OffMeshNavigationManager
public class CSmartObjectClasses : List<CSmartObjectClass> { }
