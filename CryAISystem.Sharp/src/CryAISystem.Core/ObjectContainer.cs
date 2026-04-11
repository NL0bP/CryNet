// Literal port of dev/Code/CryEngine/CryAISystem/ObjectContainer.{h,cpp}.
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// Description : Manages the stubs and pointers to all AI objects

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem;

// Free function from ObjectContainer.cpp
public static class ObjectContainerFreeFunctions
{
    public static string GetNameFromType(EAIClass type)
    {
        switch (type)
        {
            case EAIClass.eAIC_AIVehicle: return "CAIVehicle";
            case EAIClass.eAIC_Puppet: return "CPuppet";
            case EAIClass.eAIC_PipeUser: return "CPipeUser";
            case EAIClass.eAIC_AIPlayer: return "CAIPlayer";
            case EAIClass.eAIC_Leader: return "CLeader";
            case EAIClass.eAIC_AIActor: return "CAIActor";
            case EAIClass.eAIC_AIObject: return "CAIObject";
            case EAIClass.eAIC_AIFlyingVehicle: return "CAIFlyingVehicle";
            default: System.Diagnostics.Debug.Assert(false); return "<UNKNOWN>";
        }
    }
}

/// <summary>
/// Container for AI Objects.
/// All AI objects should be registered through the the ObjectContainer, which is the only way strong references
/// can be assigned. The container manages the objects themselves, their stubs, their validity status
/// (though the use of salts) and the destruction of the objects at the end of an AI frame.
/// </summary>
public partial class CObjectContainer
{
    public const int MAX_AI_OBJECTS = 1 << 13;

    // typedef std::vector<IAIObject*> TAIObjectVec;

    public CObjectContainer()
    {
        m_objects = new id_map<uint, CAIObject>(MAX_AI_OBJECTS);
    }

    public void Reset()
    {
        // unreserve all IDs first
        for (int i = 0; i < m_reservedIDs.Count; ++i)
        {
            m_objects.erase(m_reservedIDs[i]);
            m_snObjectsDeregistered++;
        }
        m_reservedIDs.Clear();

        int numRegistered = GetNumRegistered();
        if (numRegistered != 0)
        {
            DumpRegistered();
        }
        // CRY_ASSERT_MESSAGE(numRegistered == 0, ...) — Something has leaked AI objects
        System.Diagnostics.Debug.Assert(numRegistered == 0);

        m_objects.clear();

        m_DeregisteredBuffer.Clear();
        m_DeregisteredWorkingBuffer.Clear();
    }

    public bool IsValid(CAbstractUntypedRef refArg)
    {
        return GetAIObject(refArg) != null;
    }

    public bool IsValid<T>(CWeakRef<T> refArg) where T : class
    {
        return GetAIObject((CAbstractUntypedRef)(object)refArg) != null;
    }

    public CAIObject GetAIObject(CAbstractUntypedRef refArg)
    {
        uint objectID = refArg.GetObjectID();
        return m_objects.validate(objectID) ? m_objects[objectID] : null;
    }

    public CAIObject GetAIObjectById(uint objectID)
    {
        return m_objects.validate(objectID) ? m_objects[objectID] : null;
    }

    // pObject is essential - have to register something of course
    // ref is to set a strong reference if we have one, otherwise we accept it is unowned
    // inId is optional, if specified will attempt to register the object under that ID. Mostly this is used for serialization.
    public bool RegisterObject(CAIObject pObject, CStrongRef<CAIObject> refArg, uint inId = 0 /*INVALID_AIOBJECTID*/)
    {
        // First, check and release the reference if it is already used
        // Recreating an object like this usually isn't necessary but the semantics make sense
        if (!refArg.IsNil())
        {
            refArg.Release();
        }

        m_snObjectsRegistered++;

#if DEBUG
        if (pObject != null)
        {
            int capacity = m_objects.capacity();

            uint prevID = 0;
            for (int i = 0; i < capacity; ++i)
            {
                if (!m_objects.index_free(i) && (m_objects.get_index(i) == pObject))
                {
                    prevID = m_objects.get_index_id(i);
                    break;
                }
            }

            System.Diagnostics.Debug.Assert(prevID == 0);
            if (prevID != 0)
            {
                gEnv.pLog?.LogError("AI: CObjectContainer::RegisterObjectUntyped - Object already registered - {0:X} @{1,6} \"{2}\" ", pObject, prevID, pObject.GetName());
                // intentionally not calling pObject->SetSelfReference(ref); here, since that would be bad.
                return false;
            }
        }
#endif

        uint id = inId;
        if (id != 0 /*INVALID_AIOBJECTID*/)
        {
            // Registering an object with a specified ID. This usually means the object was serialized out
            //	with a specific ID (eg to a pool bookmark) and now needs to be recreated using the same ID.

            // In this case the ID should have been reserved earlier, and there should be a null object
            //	in the object map. UnreserveID will remove that, so we can add the new object in its place.
            UnreserveID(id);

            m_objects.insert(id, pObject);
        }
        else
        {
            id = m_objects.insert(pObject);
        }

        System.Diagnostics.Debug.Assert(id != 0);

        refArg.Assign(id);

        AILog.AILogComment("Registered object {0} @{1,6} \"{2}\" ", pObject, id, pObject != null ? pObject.GetName() : "NULL");

        if (pObject != null)
        {
            pObject.SetSelfReference(refArg);
        }

        return true;
    }

    // Remove an object from the manager via its handle
    public bool DeregisterObject<T>(CStrongRef<T> refArg) where T : class
    {
        // The templating is really just a wrapper, so call an untyped private function to do the real work
        return DeregisterObjectUntyped((CAbstractUntypedRef)(object)refArg);
    }

    public bool DeregisterObjectUntyped(CAIObject pObject)
    {
        CWeakRef<CAIObject> refLocal = GetWeakRef(pObject);
        return DeregisterObjectUntyped((CAbstractUntypedRef)(object)refLocal);
    }

    // Only strong refs should ever be passed in
    public bool DeregisterObjectUntyped(CAbstractUntypedRef refArg)
    {
        // (MATT) Checks for double-deregister in debug might be helpful here - but if the mechanisms are enforced it shouldn't be possible {2009/03/30}

        // (MATT) Perhaps this isn't the right place to increment - they are only pushed on a list, after all {2009/04/07}
        m_snObjectsDeregistered++;
        System.Diagnostics.Debug.Assert(m_snObjectsRegistered >= m_snObjectsDeregistered);

        uint id = refArg.GetObjectID();
        bool validID = m_objects.validate(id);

        // CRY_ASSERT_TRACE(validID, ("Multiple AI objects with id %i, dangling pointers or corruption imminent", id));
        System.Diagnostics.Debug.Assert(validID);

        if (validID)
        {
            // CRY_ASSERT_MESSAGE(!stl::find(m_DeregisteredBuffer, id), "Double deregistering object!");
            System.Diagnostics.Debug.Assert(!m_DeregisteredBuffer.Contains(id));
            m_DeregisteredBuffer.Add(id);
            refArg.Assign(0 /*INVALID_AIOBJECTID*/);

#if DEBUG
            CAIObject obj = m_objects[id];
            string name = obj != null ? obj.GetName() : "<NULL OBJECT>";
            AILog.AILogComment("Deregistered object {0} @{1,6} \"{2}\" ", obj, id, name);
#endif
            return true;
        }
        else
        {
            int prevIndex = m_objects.get_index_for_id(id);
            CAIObject pObject = m_objects.get_index(prevIndex);
            // CRY_ASSERT_TRACE(false, ("Previous object was %s (%d)", ...));
            System.Diagnostics.Debug.Assert(false);

            return false;
        }
    }

    // We (should!) call this at the end of each AI frame
    public int ReleaseDeregisteredObjects(bool checkForLeaks)
    {
        int nReleased = 0;

        // We use a double-buffer approach
        // Currently, deleting objects currently triggers deregistration of any sub-objects
        // It could be made to work with one vector but this seems more debuggable
        int loopLimit = 100;
        while (m_DeregisteredBuffer.Count > 0 && loopLimit > 0)
        {
            System.Diagnostics.Debug.Assert(--loopLimit > 0);
            // m_DeregisteredBuffer.swap(m_DeregisteredWorkingBuffer);
            List<uint> swapTmp = m_DeregisteredBuffer;
            m_DeregisteredBuffer = m_DeregisteredWorkingBuffer;
            m_DeregisteredWorkingBuffer = swapTmp;

            for (int itO = 0; itO < m_DeregisteredWorkingBuffer.Count; ++itO)
            {
                uint id = m_DeregisteredWorkingBuffer[itO];
                bool validHandle = m_objects.validate(id);

                System.Diagnostics.Debug.Assert(validHandle);
                if (validHandle)
                {
                    CAIObject obj = m_objects[id];

#if DEBUG
                    // Before we start removing the object check for Proxy and release if necessary
                    // This allows us to prepare for any proxy queries during remove procedure, such as checking health
                    CAIActor actor = obj != null ? obj.CastToCAIActor() : null;
                    IAIActorProxy proxy = actor != null ? actor.GetProxy() : null;
                    string name = obj != null ? obj.GetName() : "<NULL OBJECT>";
                    AILog.AILogComment("Releasing object {0} @{1,6} proxy {2} \"{3}\" ", obj, id, proxy, name);
#endif
                    if (obj != null)
                    {
                        // For transitional purposes (at least) call the remove code now
                        gAIEnv.pAIObjectManager?.OnObjectRemoved(obj);

                        // Delete the object. Past this point, weak refs will still function and you can still fetch them for a given pointer, but the object itself is gone
                        // It might be better to put off that delete until we wipe pointers from the stubs, but if GetWeakRef disappeared, so would the need for all that.
                        obj.Release();
                    }

                    m_objects.erase(id);

                    // Special case: if this ID is in the reserve list, we now need to add a null object to reserve the ID.
                    // This is because the object's ID has been reserved while the object still existed.
                    if (m_reservedIDs.Contains(id))
                    {
                        m_objects.insert(id, null);
                        m_snObjectsRegistered++;
                    }

                    nReleased++;
                }
            }
            m_DeregisteredWorkingBuffer.Clear();
        }

#if CRYAISYSTEM_DEBUG
        if (checkForLeaks)
        {
            int totalObjects = GetNumRegistered();
            int count = m_objects.size();

            if (count != totalObjects)
                DumpRegistered();

            System.Diagnostics.Debug.Assert(count == totalObjects);
        }
#endif

        return nReleased;
    }

    public CWeakRef<T> GetWeakRef<T>(T pObject) where T : class
    {
        return pObject != null ? GetWeakRef<T>(((dynamic)pObject).GetAIObjectID()) : new CWeakRef<T>();
    }

    public CWeakRef<T> GetWeakRef<T>(uint nID) where T : class
    {
        return new CWeakRef<T>(nID);
    }

    public CWeakRef<CAIObject> GetWeakRef(uint id)
    {
        if (m_objects.validate(id))
            return new CWeakRef<CAIObject>(id);
        return new CWeakRef<CAIObject>();
    }

    public int GetNumRegistered() { return m_snObjectsRegistered - m_snObjectsDeregistered; }

    public void DumpRegistered()
    {
#if DEBUG
        int count = 0;
        gEnv.pLog?.Log("Listing all current AI objects:");
        for (int i = 0; i < m_objects.capacity(); ++i)
        {
            if (!m_objects.index_free(i))
            {
                CAIObject obj = m_objects.get_index(i);
                CWeakRef<CAIObject> weakRef = GetWeakRef(obj);
                gEnv.pLog?.Log("Slot {0}: Object {1} @{2,6} \"{3}\" ", i, obj, weakRef.GetObjectID(), obj != null ? obj.GetName() : "NULL");
                ++count;
            }
        }

        gEnv.pLog?.Log("Total object count {0}", count);
#endif
    }

    public void Serialize(TSerialize ser)
    {
        CAISystem pAISystem = GetAISystem();

        // Deal with the deregistration list as it makes no sense to serialize those objects
        ReleaseDeregisteredObjects(true);

        ser.BeginGroup("ObjectContainer");

        bool bReading = ser.IsReading();

        uint totalObjects = (uint)GetNumRegistered();
        uint capacity = (uint)m_objects.capacity();

        if (ser.IsWriting())
        {
            for (uint i = 0; i < capacity; ++i)
            {
                if (!m_objects.index_free((int)i))
                {
                    CAIObject obj = m_objects.get_index((int)i);

                    // AI objects associated with pooled entities are serialized as
                    //	 part of the entity bookmark: skip them here
                    if (obj == null || obj.ShouldSerialize() == false)
                    {
                        if (obj != null)
                        {
                            AILog.AILogComment("Serialization skipping no-save object {0} @{1,6} \"{2}\" ", obj, obj.GetAIObjectID(), obj.GetName());
                        }

                        totalObjects--;
                        continue;
                    }
                }
            }
        }

        ser.Value("total", ref totalObjects);

        for (uint i = 0, totalSerialised = 0; totalSerialised < totalObjects && i < capacity; ++i)
        {
            CAIObject obj = null;

            if (ser.IsWriting())
            {
                // First, is there a valid object here?
                if (m_objects.index_free((int)i))
                    continue;

                obj = m_objects.get_index((int)i);

                // AI objects associated with pooled entities are serialized as
                //	 part of the entity bookmark: skip them here
                if (obj == null || !obj.ShouldSerialize())
                    continue;
            }

            uint id = 0 /*INVALID_AIOBJECTID*/;

            if (ser.IsWriting())
                id = m_objects.get_index_id((int)i);

            // If writing, we've established this is a valid index
            // If reading, we will read the next valid index and skip ahead
            ser.BeginGroup("Entry");
            ser.Value("index", ref i);      // Reading may change the loop iterator
            ser.Value("id", ref id);

            // Serialize basic data (enough to allow recreation of the object)
            SAIObjectCreationHelper objHeader = new SAIObjectCreationHelper(obj);
            objHeader.Serialize(ser);
            System.Diagnostics.Debug.Assert(id == objHeader.objectId);

            if (bReading)
            {
                //Read type for creation, skipping if the object already exists
                if (obj == null && m_objects.get(id) == null)
                {
                    obj = objHeader.RecreateObject();

                    // all IDs for objects to serialize should have been reserved earlier
                    UnreserveID(id);

                    m_objects.insert(id, obj);
                    m_snObjectsRegistered++;
                }
                else
                {
                    obj = m_objects.get(id);
                }
            }

            if (obj != null)
            {
                // (MATT) Note that this call may reach CAIActor, which currently handles serialising the proxies {2009/04/30}
                obj.Serialize(ser);
            }
            totalSerialised++;

            // Simple back-and-forth test, should be valid at this point
            System.Diagnostics.Debug.Assert(GetWeakRef(obj).IsValid());

            if (bReading)
                AILog.AILogComment("Serialisation created object {0} @{1,6} \"{2}\" ", obj, id, obj != null ? obj.GetName() : "");

            ser.EndGroup();
        }

        ser.EndGroup();
    }

    public void SerializeObjectIDs(TSerialize ser)
    {
        if (ser.IsReading())
        {
            // AI flush should have reset everything by this point
            System.Diagnostics.Debug.Assert(m_objects.size() == 0);

            // read in the reserved object IDs and the used IDs
            //	before the rest of the system is serialized

            // add NULL objects for each of the reserved IDs
            ser.Value("reservedObjects", ref m_reservedIDs);
            for (int i = 0; i < m_reservedIDs.Count; ++i)
            {
                m_objects.insert(m_reservedIDs[i], null);
                m_snObjectsRegistered++;
            }

            ser.BeginGroup("existingObjects");
            uint objectCount = 0;
            ser.Value("count", ref objectCount);
            for (uint i = 0; i < objectCount; ++i)
            {
                ser.BeginGroup("object");
                uint id = 0 /*INVALID_AIOBJECTID*/;
                ser.Value("id", ref id);

                // again, add a NULL object for each one.
                ReserveID(id);

                ser.EndGroup();
            }
            ser.EndGroup();
        }
        else
        {
            // write out:
            //	- the list of reserved object IDs
            //	- a list of all currently registered objects
            ser.Value("reservedObjects", ref m_reservedIDs);

            ser.BeginGroup("existingObjects");
            uint objectCount = 0;
            uint totalObjects = (uint)m_objects.size();
            uint capacity = (uint)m_objects.capacity();
            for (uint i = 0; i < capacity && objectCount < totalObjects; ++i)
            {
                if (!m_objects.index_free((int)i))
                {
                    CAIObject obj = m_objects.get_index((int)i);
                    if (obj != null)
                    {
                        ++objectCount;

                        ser.BeginGroup("object");
                        uint id = obj.GetAIObjectID();
                        ser.Value("id", ref id);

                        ser.EndGroup();
                    }
                }
            }
            ser.Value("count", ref objectCount);
            ser.EndGroup();
        }
    }

    public void PostSerialize()
    {
        int capacity = m_objects.capacity();
        for (int i = 0; i < capacity; ++i)
        {
            if (!m_objects.index_free(i))
            {
                CAIObject obj = m_objects.get_index(i);
                if (obj != null)
                {
                    obj.PostSerialize();
                }
                else
                {
                    // NULL object in m_objects map: verify that this object is on the reserved list
                    System.Diagnostics.Debug.Assert(m_reservedIDs.Contains(m_objects.get_index_id(i)));
                }
            }
        }
    }

    public void RebuildObjectMaps(SortedDictionary<short, CCountedRef<CAIObject>> objectMap, SortedDictionary<short, CWeakRef<CAIObject>> dummyMap)
    {
        // C++ uses std::multimap which allows duplicate keys; SortedDictionary in C# does not.
        // Literal port preserves the C++ structure choice — at the call site this matches the
        // header signature; if multimap semantics are needed later we can swap to a List-of-pairs
        // helper container as the AICollision-style approach has shown.
        int capacity = m_objects.capacity();

        for (int i = 0; i < capacity; ++i)
        {
            if (!m_objects.index_free(i))
            {
                CAIObject obj = m_objects.get_index(i);
                if (obj == null)
                    continue;

                ushort type = obj.GetType();

                bool bIsDummy = false;
                if (type == (ushort)EAIObjectType.AIOBJECT_DUMMY)
                    bIsDummy = true;
                else if (type == (ushort)EAIObjectType.AIOBJECT_WAYPOINT && obj.GetSubType() == ESubType.STP_BEACON)
                    bIsDummy = true;

                if (bIsDummy)
                {
                    CWeakRef<CAIObject> refLocal = GetWeakRef(obj.GetAIObjectID());
                    dummyMap[(short)obj.GetSubType()] = refLocal;
                }
                else if (!obj.IsFromPool())   // pooled objects will already be in the objectMap
                {
                    CStrongRef<CAIObject> refLocal = new CStrongRef<CAIObject>();
                    refLocal.Assign(obj.GetAIObjectID());
                    objectMap[(short)obj.GetType()] = new CCountedRef<CAIObject>();
                }
            }
        }
    }

    public void ReserveID(uint id)
    {
        System.Diagnostics.Debug.Assert(id != 0 /*INVALID_AIOBJECTID*/);

        // Objects may already be in the reserve list - this happens for instance when an AI is
        //	active at a checkpoint save and then is later deactivated (returned to pool): both will
        //	cause a serialize-to-bookmark, which will reserve the ID. So just ignore the second reserve.
        if (!m_reservedIDs.Contains(id))
            m_reservedIDs.Add(id);

        // If an object still exists using this ID, leave the existing one there.
        //	When removed, ReleaseDeregisteredObjects will add the NULL entry.
        if (m_objects.free(id))
        {
            m_objects.insert(id, null);

            m_snObjectsRegistered++;    // prevents asserts about leaking objects
        }
    }

    public void UnreserveID(uint id)
    {
        System.Diagnostics.Debug.Assert(id != 0 /*INVALID_AIOBJECTID*/);
        System.Diagnostics.Debug.Assert(m_reservedIDs.Contains(id));
        System.Diagnostics.Debug.Assert(m_objects.get(id) == null);

        if (m_objects.get(id) != null)
        {
            // this would suggest a code error: something was added to the reserve list, which should have then placed a NULL
            //	AI object in m_objects. That should mean that no other object can take that slot.
            CAIObject pPrevObject = m_objects[id];
            AILog.AILogAlways("Error: Trying to unreserve existing AI Object id {0}, object is {1}", id, pPrevObject != null ? pPrevObject.GetName() : "NULL");

            // Returning now probably means the request won't be able to register a new object using this ID.
            // That's bad, but probably better than removing some other object.
            return;
        }

        m_reservedIDs.Remove(id);
        m_objects.erase(id);
        m_snObjectsDeregistered++;
    }

    protected id_map<uint, CAIObject> m_objects;

    // typedef std::vector<tAIObjectID> TVecAIObjects;
    protected List<uint> m_DeregisteredBuffer = new List<uint>();        // When deregistered, CAIObjects are added here
    protected List<uint> m_DeregisteredWorkingBuffer = new List<uint>(); // Working buffer during deletion
    protected static int m_snObjectsRegistered;
    protected static int m_snObjectsDeregistered;

    // certain objects (eg those for pooled entities) may be removed temporarily during gameplay,
    // yet need to be recreated later using the same object ID.
    protected List<uint> m_reservedIDs = new List<uint>();
}
