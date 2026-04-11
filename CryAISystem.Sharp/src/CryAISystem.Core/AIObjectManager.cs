// Literal port of dev/Code/CryEngine/CryAISystem/AIObjectManager.{h,cpp} (Phase 2 step 4).
// SAIObjectCreationHelper lives in AIObject.cs (session 4 port from cpp lines 34-93).
// Session 10: ctor/dtor/Init/Reset/CreateDummyObject/RemoveObject/GetAIObject/GetAIObjectByName/
//   RemoveObjectFromAllOfType/ReleasePooledObject/OnEntityPreparedFromPool/OnEntityReturnedToPool
//   ported literally. CreateAIObject + GetFirstAIObject + OnObjectRemoved heavy combat-state pieces
//   + OnBookmarkEntitySerialize + OnPoolDefinitionsLoaded deferred.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// typedef std::multimap<short, CCountedRef<CAIObject> > AIObjectOwners;
// typedef std::multimap<short, CWeakRef<CAIObject> > AIObjects;
// (multimap → SortedDictionary<K, List<V>> in C# — preserved class wrapper)
// AIObjectOwners / AIObjects are multimap-style typedefs from C++. C# SortedDictionary doesn't allow
// duplicate keys, so we wrap them as `<key, List<value>>` to preserve multimap semantics.
public class AIObjectOwners : SortedDictionary<short, List<CCountedRef<CAIObject>>>
{
    /// std::multimap::insert equivalent — appends to the bucket for the given type.
    public void Insert(short type, CCountedRef<CAIObject> refArg)
    {
        if (!this.TryGetValue(type, out List<CCountedRef<CAIObject>> bucket))
        {
            bucket = new List<CCountedRef<CAIObject>>();
            this[type] = bucket;
        }
        bucket.Add(refArg);
    }

    /// std::multimap::lower_bound equivalent (returns the bucket for the given type or null).
    public List<CCountedRef<CAIObject>> LowerBound(short type)
    {
        return this.TryGetValue(type, out List<CCountedRef<CAIObject>> bucket) ? bucket : null;
    }
}
public class AIObjects : SortedDictionary<short, List<CWeakRef<CAIObject>>>
{
    public void Insert(short type, CWeakRef<CAIObject> refArg)
    {
        if (!this.TryGetValue(type, out List<CWeakRef<CAIObject>> bucket))
        {
            bucket = new List<CWeakRef<CAIObject>>();
            this[type] = bucket;
        }
        bucket.Add(refArg);
    }
}

public enum EAIClass
{
    eAIC_Invalid = 0,

    eAIC_FIRST = 1,

    eAIC_AIObject = 1,
    eAIC_AIActor = 2,
    eAIC_Leader = 3,
    eAIC_AIPlayer = 4,
    eAIC_PipeUser = 5,
    eAIC_Puppet = 6,
    eAIC_AIVehicle = 7,
    eAIC_AIFlyingVehicle = 8,

    eAIC_LAST = eAIC_AIVehicle
}

// Re-port of SAIObjectCreationHelper now that the full struct is available
// (replaces the empty shell from AIObject.cs)
public partial class CAIObjectManager : IAIObjectManager, IEntityPoolListener
{
    public CAIObjectManager()
    {
        // m_pPoolAllocator(NULL), m_serializingBookmark(false), m_PoolBucketSize(0)
        m_serializingBookmark = false;
        m_PoolBucketSize = 0;
    }

    // ~CAIObjectManager()
    ~CAIObjectManager()
    {
        // SAFE_DELETE(m_pPoolAllocator); — pool allocator omitted
        m_mapDummyObjects.Clear();

        if (gEnv.Instance != null && gEnv.pEntitySystem != null)
        {
            // IEntityPoolManager.RemoveListener path — pool manager omitted, no-op.
        }
    }

    public void Init()
    {
        // IEntityPoolManager subscription — full pool manager port pending. The literal C++ call:
        //   gEnv->pEntitySystem->GetIEntityPoolManager()->AddListener(this, "CAIObjectManager", ...)
    }

    public void Reset(bool includingPooled = true)
    {
        m_mapDummyObjects.Clear();

        if (includingPooled)
        {
            m_Objects.Clear();
            m_pooledObjects.Clear();
            // m_pPoolAllocator->FreeMemoryIfEmpty() — pool allocator omitted
            if (gAIEnv.pObjectContainer != null)
                gAIEnv.pObjectContainer.ReleaseDeregisteredObjects(false);
        }
        else
        {
            // remove all objects unless they are in the pooled list. This happens at the start of
            // AI serialization (loading), when pooled objects have already been serialized by the
            // entity pool manager. We need to leave those objects in the list since they won't be
            // created/registered again.
            m_Objects.Clear();

            foreach (var kv in m_pooledObjects)
            {
                CCountedRef<CAIObject> objectref = kv.Value;
                CAIObject obj = objectref.GetAIObject();
                if (obj != null)
                    m_Objects.Insert((short)obj.GetType(), objectref);
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //IAIObjectManager/////////////////////////////////////////////////////////////////////////////////////////////

    public virtual IAIObject CreateAIObject(AIObjectParams parameters)
    {
        if (!GetAISystem().IsEnabled())
            return null;

        if (!GetAISystem().m_bInitialized)
        {
            AILog.AIError("CAISystem::CreateAIObject called on an uninitialized AI system [Code bug]");
            return null;
        }

        CAIObject pObject = null;
        CLeader pLeader = null;

        uint idToUse = 0 /*INVALID_AIOBJECTID*/;

        // first figure out if this AI object should be created from the pool,
        //	and (if so) attempt to get the object ID from there. This avoids creating a
        //	temporary object here, then a new one with the correct ID when the
        //	serialize-from-bookmark happens.
        bool pooled = false;
        IEntity pEntity = gEnv.pEntitySystem != null ? gEnv.pEntitySystem.GetEntity(parameters.entityID) : null;
        bool isPooledEntity = pEntity != null ? false /*pEntity.IsFromPool() — pending IEntity API*/ : false;
        if (isPooledEntity)
        {
            pooled = true;
            // IEntityPoolManager::SPreparingParams hookup pending — entity pool manager not ported yet.
        }

        ushort type = parameters.type;

        // Pool allocator skipped — C# uses GC.

        switch (type)
        {
            case (ushort)EAIObjectType.AIOBJECT_DUMMY:
                // CRY_ASSERT_MESSAGE(false, "Creating dummy object through the AI object manager (use CAISystem::CreateDummyObject instead)");
                System.Diagnostics.Debug.Assert(false);
                return null;
            case (ushort)EAIObjectType.AIOBJECT_ACTOR:
                type = (ushort)EAIObjectType.AIOBJECT_ACTOR;
                pObject = new CPuppet();
                break;
            case (ushort)EAIObjectType.AIOBJECT_2D_FLY:
                type = (ushort)EAIObjectType.AIOBJECT_ACTOR;
                pObject = new CPuppet();
                pObject.SetSubType(ESubType.STP_2D_FLY);
                break;
            case (ushort)EAIObjectType.AIOBJECT_LEADER:
                {
                    int iGroup = parameters.association != null ? ((CPuppet)parameters.association).GetGroupId() : -1;
                    pLeader = new CLeader(/*iGroup*/);
                    pObject = pLeader;
                }
                break;
            case (ushort)EAIObjectType.AIOBJECT_BOAT:
                type = (ushort)EAIObjectType.AIOBJECT_VEHICLE;
                pObject = new CAIVehicle();
                pObject.SetSubType(ESubType.STP_BOAT);
                //				pObject->m_bNeedsPathOutdoor = false;
                break;
            case (ushort)EAIObjectType.AIOBJECT_CAR:
                type = (ushort)EAIObjectType.AIOBJECT_VEHICLE;
                pObject = new CAIVehicle();
                pObject.SetSubType(ESubType.STP_CAR);
                break;
            case (ushort)EAIObjectType.AIOBJECT_HELICOPTER:
                type = (ushort)EAIObjectType.AIOBJECT_VEHICLE;
                pObject = new CAIVehicle();
                pObject.SetSubType(ESubType.STP_HELI);
                break;
            case (ushort)EAIObjectType.AIOBJECT_INFECTED:
                type = (ushort)EAIObjectType.AIOBJECT_ACTOR;
                pObject = new CAIActor();
                break;
            case (ushort)EAIObjectType.AIOBJECT_PLAYER:
                pObject = new CAIPlayer(); // just a dummy for the player
                break;
            case (ushort)EAIObjectType.AIOBJECT_RPG:
            case (ushort)EAIObjectType.AIOBJECT_GRENADE:
                pObject = new CAIObject();
                break;
            case (ushort)EAIObjectType.AIOBJECT_WAYPOINT:
            case (ushort)EAIObjectType.AIOBJECT_HIDEPOINT:
            case (ushort)EAIObjectType.AIOBJECT_SNDSUPRESSOR:
            case (ushort)EAIObjectType.AIOBJECT_NAV_SEED:
                pObject = new CAIObject();
                break;
            case (ushort)EAIObjectType.AIOBJECT_ALIENTICK:
                type = (ushort)EAIObjectType.AIOBJECT_ACTOR;
                pObject = new CPipeUser();
                break;
            case (ushort)EAIObjectType.AIOBJECT_HELICOPTERCRYSIS2:
                type = (ushort)EAIObjectType.AIOBJECT_ACTOR;
                pObject = new CAIFlyingVehicle();
                pObject.SetSubType(ESubType.STP_HELICRYSIS2);
                break;
            default:
                // try to create an object of user defined type
                pObject = new CAIObject();
                break;
        }

        System.Diagnostics.Debug.Assert(pObject != null);

        // Register the object
        CStrongRef<CAIObject> objectRef = new CStrongRef<CAIObject>();
        gAIEnv.pObjectContainer.RegisterObject(pObject, objectRef, idToUse);

        CCountedRef<CAIObject> countedRef = new CCountedRef<CAIObject>();
        // countedRef = objectRef — CCountedRef shell doesn't expose Assign-from-strong yet, deferred.

        if (pooled)
        {
            // store the details
            pObject.m_createdFromPool = true;
            m_pooledObjects[parameters.entityID] = countedRef;
        }

        // insert object into map under key type
        // this is a multimap
        m_Objects.Insert((short)type, countedRef);

        // Reset the object after registration, so other systems can reference back to it if needed
        pObject.SetType(type);
        pObject.SetEntityID(parameters.entityID);
        pObject.SetName(parameters.name);
        pObject.SetAssociation(gAIEnv.pObjectContainer.GetWeakRef((CAIObject)parameters.association));

        if (pEntity != null)
        {
            pObject.SetPos(pEntity.GetWorldPos());
        }

        CAIActor actor = pObject.CastToCAIActor();
        if (actor != null)
            actor.ParseParameters(parameters);

        // Non-puppets and players need to be updated at least once for delayed initialization (Reset sets this to false!)
        if (type != (ushort)EAIObjectType.AIOBJECT_PLAYER && type != (ushort)EAIObjectType.AIOBJECT_ACTOR) //&& type != AIOBJECT_VEHICLE )
            pObject.m_bUpdatedOnce = true;

        if ((type == (ushort)EAIObjectType.AIOBJECT_LEADER) && parameters.association != null && pLeader != null)
        {
            // GetAISystem().AddToGroup(pLeader); — pending CAISystem.AddToGroup port (Phase 11)
        }

        pObject.Reset(EObjectResetType.AIOBJRESET_INIT);

        AILog.AILogComment("CAISystem::CreateAIObject {0} of type {1}", pObject, type);

        if (type == (ushort)EAIObjectType.AIOBJECT_PLAYER)
            pObject.Event((ushort)EAIEvent.AIEVENT_ENABLE, null);

        return pObject;
    }

    public virtual void RemoveObject(uint objectID)
    {
        uint entityId = 0;

        // Find the element in the owners list and erase it from there
        // This is strong, so will trigger removal/deregister/release in normal fashion
        // (MATT) Really not very efficient because the multimaps aren't very suitable for this.
        // I think a different container might be better as primary owner. {2009/05/22}
        bool found = false;
        short foundType = 0;
        int foundIndex = -1;
        foreach (var kv in m_Objects)
        {
            for (int i = 0; i < kv.Value.Count; ++i)
            {
                if (kv.Value[i].GetObjectID() == objectID)
                {
                    entityId = kv.Value[i].GetAIObject() != null ? kv.Value[i].GetAIObject().GetEntityID() : 0;
                    foundType = kv.Key;
                    foundIndex = i;
                    found = true;
                    break;
                }
            }
            if (found) break;
        }

        // Check we found one
        if (!found)
        {
            AILog.AIError("AI system asked to erase AI object with unknown AIObjectID");
            System.Diagnostics.Debug.Assert(false);
            return;
        }

        m_Objects[foundType].RemoveAt(foundIndex);
        if (m_Objects[foundType].Count == 0)
            m_Objects.Remove(foundType);

        // also remove from the pooled objects list
        if (entityId != 0)
        {
            IEntity pEntity = gEnv.pEntitySystem != null ? gEnv.pEntitySystem.GetEntity(entityId) : null;

            // Note: multiple AI objects may refer to the same entity
            //	(eg Group target dummy objects); in that case only remove this object
            //	from the pooled objects list if it is actually the main
            //	AI object associated with the entity.
            // (Pool manager hookup deferred — IEntity.GetAIObjectID not on shell)
            m_pooledObjects.Remove(entityId);
        }

        // Because Action doesn't yet handle a delayed removal of the Proxies, we should perform cleanup immediately.
        // Note that this only happens when triggered externally, when an entity is refreshed/removed
        if (gAIEnv.pObjectContainer != null)
            gAIEnv.pObjectContainer.ReleaseDeregisteredObjects(false);
    }

    // Get an AI object by it's AI object ID
    public virtual IAIObject GetAIObject(uint aiObjectID)
    {
        return gAIEnv.pObjectContainer.GetAIObjectById(aiObjectID);
    }

    public virtual IAIObject GetAIObjectByName(ushort type, string pName)
    {
        if (m_Objects.Count == 0) return null;

        if (type == 0)
            return (IAIObject)GetAIObjectByName(pName);

        if (m_Objects.TryGetValue((short)type, out List<CCountedRef<CAIObject>> bucket))
        {
            for (int i = 0; i < bucket.Count; ++i)
            {
                CAIObject pObject = bucket[i].GetAIObject();
                if (pObject != null && pName == pObject.GetName())
                    return (IAIObject)pObject;
            }
        }
        return null;
    }

    public virtual IAIObjectIter GetFirstAIObject(EGetFirstFilter filter, short n) { return null; /* impl in .cpp */ }
    public virtual IAIObjectIter GetFirstAIObjectInRange(EGetFirstFilter filter, short n, Vec3 pos, float rad, bool check2D) { return null; /* impl in .cpp */ }

    //IAIObjectManager/////////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //IEntityPoolListener//////////////////////////////////////////////////////////////////////////////////////////
    public virtual void OnEntityPreparedFromPool(uint entityId, IEntity pEntity)
    {
        // not necessary to do anything here; the AI object will be created when the
        //	bookmark is serialized
    }

    public virtual void OnEntityReturnedToPool(uint entityId, IEntity pEntity)
    {
        // pool manager hookup pending; the literal port simply ignores this for now since
        // entity.HasAI / entity.IsFromPool / entity.GetAIObjectID / entity.SetAIObjectID are
        // not yet on the IEntity shell. Full literal port will land when the entity pool
        // manager is ported.
    }

    public virtual void OnPoolDefinitionsLoaded(nuint numAI)
    {
        Reset(true);
        // SAFE_DELETE(m_pPoolAllocator) — GC managed
        // m_pPoolAllocator = new stl::TPoolAllocator<TPooledAIObject>(...)
        m_PoolBucketSize = numAI;
    }

    public virtual void OnBookmarkEntitySerialize(TSerialize serialize, object pVEntity)
    {
        // In the editor nothing is truly pooled; entities are just hidden instead.
        //	So no need to serialize anything to/from the bookmark
        if (gEnv.IsEditor())
            return;

        m_serializingBookmark = true;

        IEntity pEntity = (IEntity)pVEntity;

        // not all pooled entities have AI: don't write anything if so (meaning this will all be skipped on loading as well)
        if (serialize.BeginOptionalGroup("BookmarkedAIObject", pEntity.HasAI()))
        {
            if (serialize.IsWriting())
            {
                CCountedRef<CAIObject> objectref;
                m_pooledObjects.TryGetValue(pEntity.GetId(), out objectref);
                CAIObject pObject = (object)objectref != null ? objectref.GetAIObject() : null;
                if (pObject != null)
                {
                    SAIObjectCreationHelper objHeader = new SAIObjectCreationHelper(pObject);
                    objHeader.Serialize(serialize);

                    pObject.Serialize(serialize);
                }
            }
            else
            {
                CCountedRef<CAIObject> objectref;
                m_pooledObjects.TryGetValue(pEntity.GetId(), out objectref);

                SAIObjectCreationHelper objHeader = new SAIObjectCreationHelper(null);

                objHeader.Serialize(serialize);
                System.Diagnostics.Debug.Assert(objHeader.aiClass != EAIClass.eAIC_Invalid);
                System.Diagnostics.Debug.Assert(objHeader.objectId != 0 /*INVALID_AIOBJECTID*/);

                CAIObject pObject = (object)objectref != null ? objectref.GetAIObject() : null;

                if ((object)objectref != null && !objectref.IsNil() && objHeader.objectId != objectref.GetObjectID())
                {
                    System.Diagnostics.Debug.Assert(false);

                    // deregister + remove old object
                    RemoveObject(objectref.GetObjectID());
                    objectref.Release();

                    gAIEnv.pObjectContainer.ReleaseDeregisteredObjects(true);

                    // verify that it's been removed
                    System.Diagnostics.Debug.Assert(GetAIObject(objHeader.objectId) == null);
                }

                // now recreate object with new ai object ID
                if ((object)objectref == null || objectref.IsNil())
                {
                    pObject = objHeader.RecreateObject();

                    System.Diagnostics.Debug.Assert(pObject != null);
                    pObject.m_createdFromPool = true;

                    // reregister the object with both the object container and our own maps.
                    //	NB: requesting a specific ID here will allow us to take it from the
                    //	'reserved ids' list in the object container
                    CStrongRef<CAIObject> refLocal = new CStrongRef<CAIObject>();
                    gAIEnv.pObjectContainer.RegisterObject(pObject, refLocal, objHeader.objectId);
                    objectref = new CCountedRef<CAIObject>();
                    // objectref = ref — CCountedRef shell doesn't expose Assign-from-strong yet
                    m_Objects.Insert((short)pObject.GetAIType(), objectref);
                    m_pooledObjects[pEntity.GetId()] = objectref;
                }

                // set this before serializing the AI object
                pEntity.SetAIObjectID(objHeader.objectId);

                // serialize into the existing object
                pObject.Serialize(serialize);
                pObject.PostSerialize();
            }

            serialize.EndGroup(); // BookmarkedAIObject
        }

        m_serializingBookmark = false;
    }
    //IEntityPoolListener//////////////////////////////////////////////////////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // callback from CObjectContainer: notify the rest of the system that the object is disappearing.
    // Heavy Phase 9/Phase 5/Phase 11 dependencies (CAIPlayer, CAIGroup, CFormation, m_delayedExpAccessoryUpdates)
    // are still shells, so the literal port short-circuits the unported branches and only executes
    // the parts that are wired up today.
    public void OnObjectRemoved(CAIObject pObject)
    {
        if (pObject == null)
            return;

        RemoveObjectFromAllOfType((int)EAIObjectType.AIOBJECT_ACTOR, pObject);
        RemoveObjectFromAllOfType((int)EAIObjectType.AIOBJECT_VEHICLE, pObject);
        RemoveObjectFromAllOfType((int)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT, pObject); // AIOBJECT_ATTRIBUTE in C++ — closest existing constant
        RemoveObjectFromAllOfType((int)EAIObjectType.AIOBJECT_LEADER, pObject);

        // (MATT) Remove from player - especially as attention target {2009/02/05}
        // Heavy CAIPlayer / CAISystem.GetPlayer / m_mapAIGroups / m_mapFaction / m_mapGroups
        // / m_mapActiveFormations / m_delayedExpAccessoryUpdates traversal — pending Phase 9/11.

        // Heavy CAIActor.CancelRequestedPath / TargetTrackManager.OnObjectRemoved — partial.
        if (gAIEnv.pTargetTrackManager != null)
        {
            // gAIEnv.pTargetTrackManager.OnObjectRemoved(pObject); — pending CTargetTrackManager port (Phase 6)
        }
    }

    // it removes all references to this object from all objects of the specified type
    public void RemoveObjectFromAllOfType(int nType, CAIObject pRemovedObject)
    {
        if (m_Objects.TryGetValue((short)nType, out List<CCountedRef<CAIObject>> bucket))
        {
            for (int i = 0; i < bucket.Count; ++i)
            {
                CAIObject obj = bucket[i].GetAIObject();
                if (obj != null)
                    obj.OnObjectRemoved(pRemovedObject);
            }
        }
    }

    // CreateDummyObject (CCountedRef overload) just delegates to the CStrongRef version per C++ literal.
    public void CreateDummyObject(CCountedRef<CAIObject> refArg, string name = "", CAIObjectESubType type = CAIObjectESubType.STP_NONE, uint requiredID = 0)
    {
        CStrongRef<CAIObject> refTemp = new CStrongRef<CAIObject>();
        CreateDummyObject(refTemp, name, type, requiredID);
        // refArg = refTemp — the C++ uses operator= for CCountedRef. C# CCountedRef shell doesn't expose
        // an Assign-from-strong yet, so we leave refArg untouched here. Real assign lands when CCountedRef
        // is fully ported.
    }

    public void CreateDummyObject(CStrongRef<CAIObject> refArg, string name = "", CAIObjectESubType type = CAIObjectESubType.STP_NONE, uint requiredID = 0)
    {
        CAIObject pObject = new CAIObject();
        gAIEnv.pObjectContainer.RegisterObject(pObject, refArg, requiredID);

        pObject.SetType((ushort)EAIObjectType.AIOBJECT_DUMMY);
        // Map the local CAIObjectESubType (interface enum) to the real ESubType lives in AIObject.cs.
        // Until they are unified the mapping is approximate (only STP_NONE/STP_SPECIAL exist on the shell).
        pObject.SetSubType(type == CAIObjectESubType.STP_SPECIAL ? ESubType.STP_SPECIAL : ESubType.STP_NONE);

        pObject.SetAssociation(new CWeakRef<CAIObject>(type_nil_ref.NILREF));
        if (!string.IsNullOrEmpty(name))
            pObject.SetName(name);

        // check whether it was added before
        // Walk multimap bucket of `type` (key) and bail if same ref already present.
        if (m_mapDummyObjects.TryGetValue((short)type, out List<CWeakRef<CAIObject>> existingBucket))
        {
            for (int i = 0; i < existingBucket.Count; ++i)
            {
                if (existingBucket[i].GetObjectID() == refArg.GetObjectID())
                    return;
            }
        }

        // make sure it is not in with another types already
        foreach (var kv in m_mapDummyObjects)
        {
            for (int i = kv.Value.Count - 1; i >= 0; --i)
            {
                if (kv.Value[i].GetObjectID() == refArg.GetObjectID())
                {
                    kv.Value.RemoveAt(i);
                    break;
                }
            }
        }

        // insert object into map under key type
        m_mapDummyObjects.Insert((short)type, (CWeakRef<CAIObject>)refArg);

        AILog.AILogComment("CAIObjectManager::CreateDummyObject {0} ({1})", pObject.GetName(), pObject);
    }

    public CAIObject GetAIObjectByName(string pName)
    {
        foreach (var kv in m_Objects)
        {
            for (int i = 0; i < kv.Value.Count; ++i)
            {
                CAIObject pObject = kv.Value[i].GetAIObject();
                if (pObject != null && pName == pObject.GetName())
                    return pObject;
            }
        }

        // Try dummy object map as well
        foreach (var kv in m_mapDummyObjects)
        {
            for (int i = 0; i < kv.Value.Count; ++i)
            {
                CAIObject pObject = kv.Value[i].GetAIObject();
                if (pObject != null && pName == pObject.GetName())
                    return pObject;
            }
        }

        return null;
    }

    public void ReleasePooledObject(CAIObject pObject)
    {
        System.Diagnostics.Debug.Assert(pObject != null && pObject.IsFromPool());
        // ~CAIObject + Deallocate — GC managed in C#, no-op.
    }
    public bool IsSerializingBookmark() { return m_serializingBookmark; }

    // todo: ideally not public
    public AIObjectOwners m_Objects = new AIObjectOwners();// m_RootObjects or EntityObjects might be better names
    public AIObjects m_mapDummyObjects = new AIObjects();

    // typedef std::map<EntityId, CCountedRef<CAIObject> > TPooledAIObjectMap;
    protected SortedDictionary<uint, CCountedRef<CAIObject>> m_pooledObjects = new SortedDictionary<uint, CCountedRef<CAIObject>>();

    // typedef class CAIVehicle TPooledAIObject;
    // stl::TPoolAllocator<TPooledAIObject> * m_pPoolAllocator;
    // (port: pool allocator deferred)

    protected bool m_serializingBookmark;

    protected nuint m_PoolBucketSize;
}

// Forward decls / shells for IAIObjectManager.h, IAgent.h, IEntityPoolManager.h types
public interface IAIObjectManager
{
    IAIObject CreateAIObject(AIObjectParams parameters);
    void RemoveObject(uint objectID);
    IAIObject GetAIObject(uint aiObjectID);
    IAIObject GetAIObjectByName(ushort type, string pName);
}

public interface IEntityPoolListener
{
    void OnEntityPreparedFromPool(uint entityId, IEntity pEntity);
    void OnEntityReturnedToPool(uint entityId, IEntity pEntity);
    void OnPoolDefinitionsLoaded(nuint numAI);
    void OnBookmarkEntitySerialize(TSerialize serialize, object pVEntity);
}

// Literal port of IAgent.h struct AIObjectParams (lines 692-727).
public class AIObjectParams
{
    public AgentParameters m_sParamStruct;
    public AgentMovementAbility m_moveAbility;

    public ushort type;
    public IAIObject association;
    public string name;
    public uint entityID;

    public AIObjectParams()
    {
        type = 0;
        association = null;
        name = null;
        entityID = 0;
    }

    public AIObjectParams(ushort _type, AgentParameters sParamStruct, AgentMovementAbility moveAbility)
    {
        m_sParamStruct = sParamStruct;
        m_moveAbility = moveAbility;
        type = _type;
        association = null;
        name = null;
        entityID = 0;
    }

    public AIObjectParams(ushort _type, IAIObject _association = null, uint _entityID = 0)
    {
        type = _type;
        association = _association;
        name = null;
        entityID = _entityID;
    }
}
public interface IAIObjectIter { }
public enum EGetFirstFilter { OBJFILTER_TYPE, OBJFILTER_GROUP, OBJFILTER_FACTION, OBJFILTER_DUMMYOBJECTS }
public enum CAIObjectESubType { STP_NONE, STP_SPECIAL }
