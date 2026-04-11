// Literal port of dev/Code/CryEngine/CryAISystem/Reference.{h,inl}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : References to AI objects

// Notes: this-> is used when referring to members to compile on GCC - due to specialization rules compiler
//        cannot assume any inherited members exist in a templated base (Meyer, Item 43, Effective C++)


/**
 * References to AI objects.
 *
 * (More accurately: smartpointers)
 * These references can be preserved across frames and dereferenced to give an AIStub * or AIObject *, which should
 * always be discarded between frames.
 * There are four main types:
 * tAIObjectID -  A simple integer ID. This is the only type that should pass outside the AI system.
 * WeakRef -      Wraps an ID with templated type. For use within the AI system.
 * StrongRef -    Wraps an ID with templated type and auto_ptr semantics. Uniquely owns the object, controlling its validity. Within AI.
 * CountedRef -   Wraps a StrongRef for reference-counting semantics. Within AI.
 *
 * Notes:
 * Strong should be preferred but are not compatible (and will not compile) with STL containers. Counted can be used in this case.
 * Counted is rather bolted on and should be properly integrated!
 */

namespace CryAISystem;

//#define DEBUG_REFERENCES


/**
* The simple integer AI object ID, for use outside the AI system.
*/
public static class TypeAlias
{
    public const uint32 INVALID_AIOBJECTID = (uint32)0;
}

/**
* Enum whose single member, NILREF, represents an empty, untyped reference.
* Only for syntactic sugar, to avoid the use of CWeakRef<CMyClass>() where, with pointers, NULL would have sufficed.
* It is silently converted into a Nil weak reference of any type, hence very useful as a parameter when calling a function.
*/
public enum type_nil_ref { NILREF }

/**
 * An abstract typeless base class for references.
 * Some functionality of the references is independent of type and so is defined here.
 */
public class CAbstractUntypedRef
{
    // friend class CObjectContainer;

    /**
     * Test if reference is currently unassigned.
     */
    public bool IsNil()
    {
        return (m_nID == TypeAlias.INVALID_AIOBJECTID);
    }

    /**
    * Test if reference is currently assigned.
    */
    public bool IsSet()
    {
        return (m_nID != TypeAlias.INVALID_AIOBJECTID);
    }

    /**
     * Return a simple AI object ID.
     */
    public uint32 GetObjectID()
    {
        return m_nID;
    }

    /**
     * Return the AI object, if reference is valid.
     */
    public IAIObject GetIAIObject()
    {
        CryAssert.Check(gAIEnv.pObjectContainer != null);

        CAIObject obj = gAIEnv.pObjectContainer.GetAIObject(this);
        if (obj == null)
            return null;

        return (IAIObject)obj;
    }

    /**
     * Tests whether a reference and a pointer refer to the same object.
     */
    public static bool operator ==(CAbstractUntypedRef self, IAIObject pThatObject)
    {
        return self.GetIAIObject() == pThatObject;
    }

    public static bool operator !=(CAbstractUntypedRef self, IAIObject pThatObject)
    {
        return !(self == pThatObject);
    }

    public static bool operator ==(CAbstractUntypedRef self, CAbstractUntypedRef that)
    {
        return (self.m_nID == that.m_nID);
    }

    public static bool operator !=(CAbstractUntypedRef self, CAbstractUntypedRef that)
    {
        return !(self == that);
    }

    public static bool operator <(CAbstractUntypedRef self, CAbstractUntypedRef that)
    {
        return self.m_nID < that.m_nID;
    }

    public static bool operator >(CAbstractUntypedRef self, CAbstractUntypedRef that)
    {
        return self.m_nID > that.m_nID;
    }

    // Allow just CObjectContainer to create these, and we trust it to have ensured the type is appropriate
    internal void Assign(uint32 nID)
    {
        m_nID = nID;
    }

    protected uint32 m_nID;
}



/**
 * An abstract typed base class for references.
 */
public class CAbstractRef<T> : CAbstractUntypedRef where T : class
{
    /**
     * Get a typed weak reference instance from any existing reference.
     */
    public CWeakRef<T> GetWeakRef()
    {
        return new CWeakRef<T>(m_nID);
    }
}



/**
 * Typed strong reference for defining ownership, with auto_ptr semantics.
 */
public class CStrongRef<T> : CAbstractRef<T> where T : class
{
    /**
    * Construct an unassigned (nil) reference.
    */
    public CStrongRef()
    {
        this.m_nID = TypeAlias.INVALID_AIOBJECTID;
    }

    /**
    * Single transferable ownership constructor.
    */
    public CStrongRef(CStrongRef<T> ref_)
    {
        this.m_nID = ref_.GiveOwnership();
    }

    /**
    * Convert a NilRef into an unassigned strong reference.
    */
    public CStrongRef(type_nil_ref nil)
    {
        this.m_nID = TypeAlias.INVALID_AIOBJECTID;
    }

    /**
    * Destructor automatically releases any object owned.
    */
    ~CStrongRef()
    {
        Release();
    }

    /**
    * Single transferable ownership assignment.
    */
    public CStrongRef<T> Assign(CStrongRef<T> ref_)
    {
        // Do nothing if assigned to self
        if (this != ref_)
        {
            // Release any object currently owned
            Release();

            // Take ownership of object
            this.m_nID = ref_.GiveOwnership();
        }

        return this;
    }

    // Don't perform any stub checking as this is strong
    public new T GetAIObject()
    {
        return (T)(object)this.GetIAIObject();
    }

    // Don't perform any stub checking as this is strong
    public new IAIObject GetIAIObject()
    {
        return base.GetIAIObject();
    }

    /**
    * Release any object owned.
    */
    public bool Release()
    {
        if (this.m_nID != TypeAlias.INVALID_AIOBJECTID)
        {
            CryAssert.Check(gAIEnv.pObjectContainer != null);
            gAIEnv.pObjectContainer.DeregisterObject(this);
            this.m_nID = TypeAlias.INVALID_AIOBJECTID;
            return true;
        }
        return false;
    }

    public static implicit operator bool(CStrongRef<T> self)
    {
        return !self.IsNil();
    }

    public void Serialize(TSerialize ser, string sName = null)
    {
#if _DEBUG
        if (ser.IsWriting() && this.m_nID != TypeAlias.INVALID_AIOBJECTID)
        {
            CAIObject pObject = GetAIObject() as CAIObject;
            if (pObject != null)
            {
                // serializing a strong ref to a pooled object is a bug
                System.Diagnostics.Debug.Assert(!pObject.IsFromPool());
            }
            else
            {
                System.Diagnostics.Debug.Assert(false);
                CryLog.CryLogAlways("Saving an AI strong ref to an object that doesn't exist - code bug");
            }
        }
#endif

        // Need to take care serializing strong refs into / out of bookmarks. If the owning
        //	object is destroyed (returned to the pool) then the ref will be released,
        //	destroying the referenced object. Then when loading from the pool again
        //	this code cannot recreate the object properly. In that case the reference shouldn't
        //	be serialized as an ID, instead we actually serialize the entire object into the
        //	bookmark as well.
        if (gAIEnv.pAIObjectManager.IsSerializingBookmark())
        {
            ser.BeginGroup("SubAIObject");

            T pObject = GetAIObject();
            SAIObjectCreationHelper objHeader = new SAIObjectCreationHelper((CAIObject)(object)pObject);
            objHeader.Serialize(ser);

            if (ser.IsWriting() && this.IsSet())
            {
                // reserve the object ID. This means no other object will steal the ID later on
                gAIEnv.pObjectContainer.ReserveID(objHeader.objectId);
            }

            if (ser.IsReading() && pObject == null && objHeader.objectId != TypeAlias.INVALID_AIOBJECTID)
            {
                pObject = (T)(object)objHeader.RecreateObject();

                // reregister with the same ID as previously (will work even though the ID is reserved)
                gAIEnv.pObjectContainer.RegisterObject((CAIObject)(object)pObject, (CStrongRef<CAIObject>)(object)this, objHeader.objectId);
            }

            if (pObject != null)
            {
                ((CAIObject)(object)pObject).Serialize(ser);

                // Tell the object manager not to serialize this
                ((CAIObject)(object)pObject).SetShouldSerialize(false);
            }

            ser.EndGroup();

            return;
        }

        // [AlexMcC|19.03.10] Make sure we release this object before overwriting it.
        if (ser.IsReading())
            Release();

        // Will the compiler coalesce all these? Or, would #T be useful?
        ser.Value((sName != null ? sName : "strongRef"), ref this.m_nID);
    }

    // operator->
    public T Deref()
    {
        return this.GetAIObject();
    }

    internal uint32 GiveOwnership()
    {
        // Here, obviously, we quietly revert to an empty reference without destroying the object we own.
        uint32 nID = this.m_nID;
        this.m_nID = TypeAlias.INVALID_AIOBJECTID;
        return nID;
    }

    public override bool Equals(object obj) { return base.Equals(obj); }
    public override int GetHashCode() { return base.GetHashCode(); }
}

/**
 * Template function to convert a typed weak reference to another type.
 */
public static class RefCast
{
    public static CWeakRef<S> StaticCast<S, U>(CAbstractRef<U> ref_)
        where S : class
        where U : class
    {
        // Allow any cast that static_cast would allow on a bare pointer
        // S *pDummy = static_cast<S*>( (U*)0 );
        return new CWeakRef<S>(ref_.GetObjectID());
    }
}


/**
* Typed weak reference.
*/
public class CWeakRef<T> : CAbstractRef<T> where T : class
{
    // friend class CObjectContainer;
    // friend class CAbstractRef<T>;
    // friend class CCountedRef<T>;

    /**
    * Construct an unassigned weak reference.
    */
    public CWeakRef()
    {
        this.m_nID = TypeAlias.INVALID_AIOBJECTID;
    }

    /**
    * Construct a weak reference from any typed reference.
    */
    public CWeakRef(CAbstractUntypedRef ref_)
    {
        // Allow this for anywhere an implicit pointer cast would succeed.
        this.m_nID = ref_.GetObjectID();
    }

    /**
    * Implicit conversion from CStrongRef&lt;T&gt; — port of the C++ template ctor
    * `CWeakRef(const CAbstractRef<S>& ref)`. C# doesn't allow templated implicit conversions
    * across separate generic class hierarchies, so this is the explicit one for the common case.
    */
    public static implicit operator CWeakRef<T>(CStrongRef<T> strongRef)
    {
        return new CWeakRef<T>((CAbstractUntypedRef)strongRef);
    }

    /**
    * Convert a NilRef into an unassigned weak reference.
    */
    public CWeakRef(type_nil_ref nil)
    {
        this.m_nID = TypeAlias.INVALID_AIOBJECTID;
    }

    /**
    * Deassign this weak reference.
    */
    public void Reset()
    {
        this.m_nID = TypeAlias.INVALID_AIOBJECTID;
    }

    public bool IsReset()
    {
        return (this.m_nID == TypeAlias.INVALID_AIOBJECTID);
    }

    public bool ValidateOrReset()
    {
        if (IsValid())
            return true;

        Reset();

        return false;
    }

    public T GetAIObjectOrReset()
    {
        T pObject = this.GetAIObject();
        if (pObject == null)
            Reset();

        return pObject;
    }

    public new T GetAIObject()
    {
        T pObject = (T)(object)this.GetIAIObject();
        return pObject;
    }

    public bool IsValid()
    {
        return gAIEnv.pObjectContainer.IsValid(this);
    }

    /**
    * Assign a weak weak reference from any typed reference.
    */
    public void AssignFrom(CAbstractRef<T> ref_)
    {
        this.m_nID = ref_.GetObjectID();
    }

    public void Serialize(TSerialize ser, string sName = null)
    {
        if (ser.IsWriting())
            this.ValidateOrReset();
        ser.Value((sName != null ? sName : "weakRef"), ref this.m_nID);
    }

    // Allow just CObjectManager to create in this way and we trust it to have ensured the type is appropriate
    internal CWeakRef(uint32 nID)
    {
        this.m_nID = nID;
    }
}

/**
 * Get a weak reference to the given object, of the same type as the object pointer.
 */
public static class WeakRefHelpers
{
    public static CWeakRef<T> GetWeakRef<T>(T pObject) where T : class
    {
        if (pObject == null)
            return new CWeakRef<T>(type_nil_ref.NILREF);

        return RefCast.StaticCast<T, CAIObject>(((dynamic)pObject).GetSelfReference());
    }

    public static void SerialisationHack<T>(TSerialize ser, string sName, ref T pObj) where T : class
    {
        CWeakRef<T> ref_ = new CWeakRef<T>();
        if (ser.IsReading())
        {
            ref_.Serialize(ser, sName);
            pObj = ref_.GetAIObject();
        }

        if (ser.IsWriting())
        {
            if (pObj != null)
                ref_ = GetWeakRef(pObj);
            ref_.Serialize(ser, sName);
        }
    }
}


/**
* Typed counted reference for defining ownership in objects while being STL-compatible.
*/

// Implementation notes:
// Example reference counting classes often must be assigned and assume a valid counter object. Here we can be unassigned, like Strong.
// This _isn't_ an abstract ref! It shouldn't directly contain an ID. A bit of restructuring might be required here.
public class CCountedRef<T> where T : class
{
#if DEBUG_REFERENCES
    public CAIObject pObj;
#endif

    /**
    * Construct an unassigned (nil) reference.
    */
    public CCountedRef()
    {
        this.m_pCounter = null;
        // SET_DEBUG_OBJ(NULL);
    }

    /**
    * Construction from a strong ref (which is loses its single-transferable ownership)
    */
    public CCountedRef(CStrongRef<T> ref_)
    {
        this.m_pCounter = null;
        // Remember there's no point counting Nil references. We also don't check validity, just like Strong.
        if (!ref_.IsNil())
        {
            ObtainCounter();    // Get a reference counting object (starts at 1)
            this.m_pCounter.m_strongRef = ref_;   // Acquire the ownership - note we don't need to be friends
            // SET_DEBUG_OBJ(ref.GetAIObject());
        }
    }

    /**
    * Copy constructor.
    */
    public CCountedRef(CCountedRef<T> ref_)
    {
        this.m_pCounter = null;
        // If the other instance is empty, we don't need to do anything.
        if (ref_.m_pCounter != null)
        {
            this.m_pCounter = ref_.m_pCounter; // Share the counter
            ++(this.m_pCounter.m_nRefs);     // Increment the count
            // SET_DEBUG_OBJ(ref.GetAIObject());
        }
    }

    /**
    * Convert a NilRef into an unassigned counted reference.
    */
    public CCountedRef(type_nil_ref nil)
    {
        this.m_pCounter = null;
    }

    /**
    * Counted reference assignment.
    */
    public CCountedRef<T> AssignFrom(CCountedRef<T> ref_)
    {
        // Do nothing if assigned to same counter
        if (m_pCounter == ref_.m_pCounter)
            return this;

        // Release one count on any object currently owned and possibly release object itself
        Release();

        if (ref_.m_pCounter != null)
        {
            this.m_pCounter = ref_.m_pCounter;
            ++(this.m_pCounter.m_nRefs);
            // SET_DEBUG_OBJ(ref.GetAIObject());
        }

        return this;
    }

    /**
    * Destructor, of course, decrements the reference count and possibly deregisters the object.
    */
    ~CCountedRef()
    {
        Release();
    }

    public bool IsNil()
    {
        return (this.m_pCounter == null);
    }

    /**
    * Decrement the count on any object owned, possibly causing the object to be deregistered.
    */
    public bool Release()
    {
        if (this.m_pCounter == null)
            return false;

        --(this.m_pCounter.m_nRefs);
        if (this.m_pCounter.m_nRefs == 0)
        {
            ReleaseCounter();
        }
        else
        {
            this.m_pCounter = null;
            // SET_DEBUG_OBJ(NULL);
        }
        return true;
    }

    public static implicit operator bool(CCountedRef<T> self)
    {
        return self.m_pCounter != null;
    }

    public void Serialize(TSerialize ser, string sName = null)
    {
        // (MATT) Perhaps this could be tidied up {2009/04/02}

        // I think that when reading, an initial count greater than one is a very bad thing
        if (ser.IsReading())
            System.Diagnostics.Debug.Assert(this.m_pCounter == null || this.m_pCounter.m_nRefs == 1);

        // Easiest efficient way to do this: a local copy of our Strong ref
        CStrongRef<T> strongRef = new CStrongRef<T>();
        if (this.m_pCounter != null)
        {
            strongRef = this.m_pCounter.m_strongRef; // Note this takes ownership
        }

        // Serialise that, which of course will write it out or read it in, which will replace ownership if need be
        strongRef.Serialize(ser, (sName != null ? sName : "countedRef"));

        // Now we must translate back into a counter object, if any is needed

        // If we need a counter now and we don't already have one, create one
        if (strongRef.IsSet() && this.m_pCounter == null)
            ObtainCounter();

        if (this.m_pCounter != null)
        {
            // If we had a counter but don't need it anymore, because the object we owned has been replaced by Nil, we must delete the counter
            if (strongRef.IsNil())
                ForceReleaseCounter();
            else
                this.m_pCounter.m_strongRef = strongRef; // Note this passes any ownership back
        }

        // We shouldn't have local ownership when we finish
        System.Diagnostics.Debug.Assert(strongRef.IsNil());
    }

    public static bool operator ==(CCountedRef<T> self, IAIObject pThatObject)
    {
        // No reference is equivalent to Nil is equivalent to a NULL pointer
        if (self.m_pCounter == null)
            return (pThatObject == null);

        return self.m_pCounter.m_strongRef == pThatObject;
    }

    public static bool operator !=(CCountedRef<T> self, IAIObject pThatObject)
    {
        return !(self == pThatObject);
    }

    public static bool operator ==(CCountedRef<T> self, CAbstractUntypedRef that)
    {
        // No reference is equivalent to Nil
        if (self.m_pCounter == null)
            return (that.IsNil());

        return self.m_pCounter.m_strongRef == that;
    }

    public static bool operator !=(CCountedRef<T> self, CAbstractUntypedRef that)
    {
        return !(self == that);
    }

    /**
    * Get a typed weak reference instance from any existing reference.
    */
    public CWeakRef<T> GetWeakRef()
    {
        return (m_pCounter != null ? new CWeakRef<T>(m_pCounter.m_strongRef.GetObjectID()) : new CWeakRef<T>(type_nil_ref.NILREF));
    }

    public T GetAIObject()
    {
        return (m_pCounter != null ? m_pCounter.m_strongRef.GetAIObject() : null);
    }


    // Could define deref here - semantics are same as pointer for strong
    public T Deref()
    {
        return this.m_pCounter.m_strongRef.GetAIObject();
    }

    /**
    * Return a simple AI object ID.
    */
    public uint32 GetObjectID()
    {
        return (m_pCounter != null ? m_pCounter.m_strongRef.GetObjectID() : TypeAlias.INVALID_AIOBJECTID);
    }

    public override bool Equals(object obj) { return base.Equals(obj); }
    public override int GetHashCode() { return base.GetHashCode(); }

    protected class SRefCounter
    {
        public SRefCounter()
        {
            m_nRefs = 1;
        } // Start at 1

        // Strong ref released automatically
        public CStrongRef<T> m_strongRef;
        public int m_nRefs;                  // Since this isn't in the object itself, it needn't be mutable
    }

    protected SRefCounter m_pCounter;

    // For now, we just use new and delete, but a pool seems sensible
    protected void ObtainCounter()
    {
        System.Diagnostics.Debug.Assert(m_pCounter == null);
        m_pCounter = new SRefCounter();
    }

    protected void ReleaseCounter()
    {
        System.Diagnostics.Debug.Assert(m_pCounter != null && m_pCounter.m_nRefs == 0);
        m_pCounter = null;
        // SET_DEBUG_OBJ(NULL);
    }

    // (MATT) Force a release to a strong ref. Only makes sense when count is 1 - or we will leave dangling pointers to a deleted counter {2009/03/30}
    protected void ForceReleaseCounter()
    {
        System.Diagnostics.Debug.Assert(m_pCounter != null && m_pCounter.m_nRefs == 1);
        m_pCounter = null;
        // SET_DEBUG_OBJ(NULL);
    }
}
