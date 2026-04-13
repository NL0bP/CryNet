// Literal port of dev/Code/CryEngine/CryAISystem/ActorLookUp.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public static class ActorLookUpCastHelper
{
    public static T Cast<T>(CAIActor ptr) where T : class
    {
        // The C++ uses template specialization to switch the cast at compile time.
        // C# generic dispatch can't replicate that — we runtime-dispatch.
        if (typeof(T) == typeof(CAIObject)) return (T)(object)(CAIObject)ptr;
        if (typeof(T) == typeof(CAIActor)) return (T)(object)ptr;
        if (typeof(T) == typeof(CPipeUser)) return (T)(object)ptr.CastToCPipeUser();
        if (typeof(T) == typeof(CPuppet)) return (T)(object)ptr.CastToCPuppet();
        System.Diagnostics.Debug.Assert(false, "dangerous cast!");
        return ptr as T;
    }
}

public class ActorLookUp
{
    public nuint GetActiveCount()
    {
        return (nuint)m_actors.Count;
    }

    public T GetActor<T>(uint index) where T : class
    {
        if (index < (uint)m_actors.Count)
        {
            CAIActor actor = m_actors[(int)index];
            if (actor != null)
                return ActorLookUpCastHelper.Cast<T>(actor);
        }

        return null;
    }

    public IAIActorProxy GetProxy(uint index)
    {
        return m_proxies[(int)index];
    }

    public Vec3 GetPosition(uint index)
    {
        return m_positions[(int)index];
    }

    public uint GetEntityID(uint index)
    {
        return m_entityIDs[(int)index];
    }

    public void UpdatePosition(CAIActor actor, Vec3 position)
    {
        nuint activeActorCount = GetActiveCount();

        for (nuint i = 0; i < activeActorCount; ++i)
        {
            if (m_actors[(int)i] == actor)
            {
                m_positions[(int)i] = position;
                return;
            }
        }
    }

    public void UpdateProxy(CAIActor actor)
    {
        nuint activeActorCount = GetActiveCount();

        for (nuint i = 0; i < activeActorCount; ++i)
        {
            if (m_actors[(int)i] == actor)
            {
                m_proxies[(int)i] = actor.GetProxy();
                return;
            }
        }
    }

    public const uint Proxy = 1u << 0;
    public const uint Position = 1u << 1;
    public const uint EntityID = 1u << 2;

    public void Prepare(uint lookUpFields)
    {
        if (m_actors.Count > 0)
        {
            // CryPrefetch — no-op in C#
            // (lookUpFields used to selectively prefetch in C++)
        }
    }

    public void AddActor(CAIActor actor)
    {
        System.Diagnostics.Debug.Assert(actor != null);

        nuint activeActorCount = GetActiveCount();

        for (uint i = 0; i < (uint)activeActorCount; ++i)
        {
            if (m_actors[(int)i] == actor)
            {
                m_proxies[(int)i] = actor.GetProxy();
                m_positions[(int)i] = actor.GetPos();
                m_entityIDs[(int)i] = actor.GetEntityID();
                return;
            }
        }

        m_actors.Add(actor);
        m_proxies.Add(actor.GetProxy());
        m_positions.Add(actor.GetPos());
        m_entityIDs.Add(actor.GetEntityID());
    }

    public void RemoveActor(CAIActor actor)
    {
        System.Diagnostics.Debug.Assert(actor != null);

        nuint activeActorCount = GetActiveCount();

        for (nuint i = 0; i < activeActorCount; ++i)
        {
            if (m_actors[(int)i] == actor)
            {
                m_actors[(int)i] = m_actors[m_actors.Count - 1];
                m_actors.RemoveAt(m_actors.Count - 1);

                m_proxies[(int)i] = m_proxies[m_proxies.Count - 1];
                m_proxies.RemoveAt(m_proxies.Count - 1);

                m_positions[(int)i] = m_positions[m_positions.Count - 1];
                m_positions.RemoveAt(m_positions.Count - 1);

                m_entityIDs[(int)i] = m_entityIDs[m_entityIDs.Count - 1];
                m_entityIDs.RemoveAt(m_entityIDs.Count - 1);

                return;
            }
        }
    }

    private List<CAIActor> m_actors = new List<CAIActor>();
    private List<IAIActorProxy> m_proxies = new List<IAIActorProxy>();
    private List<Vec3> m_positions = new List<Vec3>();
    private List<uint> m_entityIDs = new List<uint>();
}

// Forward decl for IAIActorProxy (CryCommon port pending)
public interface IAIActorProxy
{
    IPhysicalEntity GetPhysics(bool bWantCharacterPhysics = false);
    void Reset(EObjectResetType type);
    bool IsDead();
    void CheckUpdateStatus();
    void Update(SOBJECTSTATE state, bool fullUpdate);
    void Serialize(TSerialize ser);
    void QueryBodyInfo(SAIBodyInfo bodyInfo);
    string GetBehaviorSelectionTreeName();
    string GetNavigationTypeName();
    void SetBehaviour(string name);
    uint GetLinkedVehicleEntityId();
    uint GetLinkedDriverEntityId();
    float GetActorHealth();
    float GetActorArmor();
    int GetActorMaxHealth();
    float GetActorMaxArmor();
    bool QueryBodyInfo(SAIBodyInfoQuery query, SAIBodyInfo bodyInfo);
    void EnableWeaponListener(uint weaponId, bool signalOnShoot);
    bool GetSecWeaponDescriptor(AIWeaponDescriptor desc, ERequestedGrenadeType type);
    void GetSecWeapon(ERequestedGrenadeType type, object reserved, out uint weaponId);
    AIWeaponDescriptor GetCurrentWeaponDescriptor();
    void GetCurrentWeapon(out uint weaponId);
    // GetAndResetShotBulletCount — used by burst fire
    int GetAndResetShotBulletCount();
    // Added for AIPlayer.cpp literal port
    IEntity GetGrabbedEntity();
    void QueryWeaponInfo(SAIWeaponInfo wi);
    // Added for Puppet.cpp literal port
    bool GetActorIsFallen() { return false; }
    int GetAlertnessState() { return 0; }
    void ResetAGInput() { }
    void SetAGInput(string name, string value, bool forceUpdate = false) { }
}
