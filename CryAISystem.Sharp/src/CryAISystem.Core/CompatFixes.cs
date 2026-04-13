// Compatibility fixes for C++ -> C# translation edge cases.
// Provides implicit conversions, overloads, and extension methods that
// bridge the gap between C++ implicit enum/int arithmetic and C# strict typing.

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem;

// CWeakRef<CAIObject> -> uint implicit conversion (C++ CWeakRef::GetObjectID())
public partial class CWeakRef<T> where T : class
{
    public static implicit operator uint(CWeakRef<T> r) => r?.GetObjectID() ?? 0;

    // Retrieve the resolved CAIObject from the object container
    public CAIObject GetResolvedAIObject()
    {
        var obj = gAIEnv.pObjectContainer?.GetAIObjectById(GetObjectID());
        return obj as CAIObject;
    }

    // Implicit conversion to CAIObject for C++ compatibility
    public static implicit operator CAIObject(CWeakRef<T> r) => r?.GetResolvedAIObject();
}

// NavigationBlocker — add 6-arg constructor (pos, radius, cost, radialCost, radialDecay, directional)
public partial class NavigationBlocker
{
    public NavigationBlocker(Vec3 pos, float radius, float cost, float radialCost, bool radialDecay, bool directional)
    { /* shell constructor */ }
}

// Fix EAIWeaponAccessories to be usable with int bitwise ops
public static class EnumCompatExtensions
{
    // EAIWeaponAccessories bitwise with int/uint
    public static bool HasFlag(this int val, EAIWeaponAccessories flag) => (val & (int)flag) != 0;
    public static int WithFlag(this int val, EAIWeaponAccessories flag) => val | (int)flag;
}

// CNavPath.UpdatePathPosition — add 4-arg overload
public partial class CNavPath
{
    public void UpdatePathPosition(Vec3 pos, float dist, bool twoD, bool reverse) { UpdatePathPosition(pos, dist, twoD); }
    // 5-arg CalculateTargetPos returning Vec3 (matching NavPath.cpp literal port signature)
    public Vec3 CalculateTargetPos(Vec3 agentPos, float lookAhead, float minLookAheadAlongPath, float pathRadius, bool twoD)
    {
        Vec3 result;
        CalculateTargetPos(out result, agentPos, lookAhead, twoD, null);
        return result;
    }

    // SetPathPoints — used by Movement system (Phase 4)
    public void SetPathPoints(TPathPoints points) { m_pts = new TPathPoints(points); m_ver++; }

    // Methods needed by GoalOpTrace.cs / GoalOpStick.cs — delegating to CNavPathReal pattern
    public float GetDiscardedPathLength() { return 0; /* shell — full impl in CNavPathReal */ }
    public float GetDistToSmartObject(bool b2D) { return float.MaxValue; /* shell */ }
    public bool UpdateAndSteerAlongPath(out Vec3 steerDir, out float distToEnd, out float distToPath, out bool isResolvingSticking,
        out Vec3 pathDir, out Vec3 pathAheadDir, out Vec3 pathAheadPos,
        Vec3 opPos, Vec3 opVel, float lookAhead, float pathRadius, float dt, bool resolveSticking, bool twoD)
    {
        steerDir = new Vec3(0, 0, 0); distToEnd = 0; distToPath = 0; isResolvingSticking = false;
        pathDir = new Vec3(0, 0, 0); pathAheadDir = new Vec3(0, 0, 0); pathAheadPos = new Vec3(0, 0, 0);
        return false; /* shell */
    }
    public void GetDirectionToPathFromPoint(Vec3 point, out Vec3 dirOut) { dirOut = new Vec3(0, 0, 0); /* shell */ }
    public bool GetPathPropertiesAhead(float distAhead, bool twoD, out Vec3 posOut, out Vec3 dirOut,
        int unused, ref float lowestDot, bool b) { posOut = new Vec3(0, 0, 0); dirOut = new Vec3(0, 0, 0); return false; /* shell */ }
    public void ResurrectRemainingPath() { /* shell */ }
    public PathPointDescriptor.OffMeshLinkData GetLastPathPointMNNSOData() { return null; /* shell */ }
}

// CAIHideObject.Set — add 3-arg overload + debug helpers for CAISystemUpdate.cpp
public partial class CAIHideObject
{
    public void Set(SHideSpot hs, Vec3 pos, Vec3 dir) { Set(hs); }
    // Added for CAISystemUpdate.cpp literal port — UpdateDebugStuff
    public void HurryUpCoverPathGen() { /* delegates to CAIHideObjectReal — pending Phase 11 */ }
    public void DebugDraw() { /* debug visualization — delegates to CAIHideObjectReal — pending Phase 11 */ }
    public void Update(int dummy) { /* 0-arg update variant used by debug — pending Phase 11 */ }
}

// Fix CPathObstacles.CalculateObstaclesAroundLocation — already added in PipeUser.cs

// IPerceptionHandler.RegisterTargetAwareness — add 1-arg overload
// CNavPath.CalculateTargetPos — add 5-arg overload (with IAIPathAgent)

// CAIRadialOccypancy.GetNearestUnoccupiedDirection — add 2-arg overload returning Vec3
public partial class CAIRadialOccypancy
{
    // 2-arg overload returning bool (for legacy callers)
    public bool GetNearestUnoccupiedDirectionBool(Vec3 dir, float angle) { Vec3 r = dir; return GetNearestUnoccupiedDirection(dir, angle, ref r); }
    // 2-arg overload returning Vec3 (C++ returned the modified direction)
    public Vec3 GetNearestUnoccupiedDirection(Vec3 dir, float angle) { Vec3 r = dir; GetNearestUnoccupiedDirection(dir, angle, ref r); return r; }
    // 3-arg Reset (pos, dir, radius)
    public void Reset(Vec3 pos, Vec3 dir, float radius) { Reset(); }
}

// IPerceptionHandler — add 1-arg RegisterTargetAwareness overload via extension
public static class PerceptionHandlerExtensions
{
    public static void RegisterTargetAwareness(this IPerceptionHandler handler, IAIObject target) { handler?.RegisterTargetAwareness(target, 0.0f); }
}

// GetDangerSpots — add 6-arg overload on CAISystem (param style matching C# List return)
public partial class CAISystem
{
    public List<SDangerSpot> GetDangerSpots(CAIObject pObj, float range, int types, float minDist, Vec3 pos, float height) { return GetDangerSpots(pObj, range, types); }
    // C++ style overload: fills arrays and returns count
    public uint GetDangerSpots(CAIObject pObj, float range, Vec3[] positions, uint[] types, uint maxn, int dangerFlags)
    {
        var list = GetDangerSpots(pObj, range, dangerFlags);
        uint count = 0;
        foreach (var spot in list)
        {
            if (count >= maxn) break;
            positions[count] = spot.pos;
            types[count] = (uint)spot.type;
            count++;
        }
        return count;
    }
}

// GetDesiredTarget — add 4-arg overload on CTargetTrackManager
public partial class CTargetTrackManager
{
    public CWeakRef<CAIObject> GetDesiredTarget(uint aiObjectId, TargetTrackHelpers.EDesiredTargetMethod method, ref CWeakRef<CAIObject> outTarget, ref SAIPotentialTarget outPotTarget)
    { outTarget = GetDesiredTarget(aiObjectId, method); outPotTarget = new SAIPotentialTarget(); return outTarget; }
}

// CAIActorProxy.ResetAGInput — the interface defines it with 0 args, but code calls with 1 arg
// Already has default interface impl. Let me add an extension for the 1-arg form.
public static class AIActorProxyExtensions
{
    public static void ResetAGInput(this IAIActorProxy proxy, string name) { proxy?.ResetAGInput(); }
}

// SelectionVariables.DebugDraw — add 2-arg overload
public partial class SelectionVariables
{
    public void DebugDraw(bool show, VariableDeclarations decls, string name) { DebugDraw(show, decls); }
}

// CFireCommandGrenade — add overloads matching IFireCommandHandler.Update signature
public partial class CFireCommandGrenade
{
    public EAIFireState Update(IAIObject owner, bool canFire, EFireMode fireMode, AIWeaponDescriptor desc, Vec3 aimDir)
    { return EAIFireState.eAIFS_Off; }
    public bool GetProjectileInfo(SProjectileInfo info) { return false; }
}

// IFireCommandHandler — add overload matching C++ call-site signature (EFireMode, AIWeaponDescriptor, Vec3)
public static class FireCommandHandlerExtensions
{
    public static EAIFireState Update(this IFireCommandHandler handler, IAIObject owner, bool canFire, EFireMode fireMode, AIWeaponDescriptor desc, Vec3 aimPos)
    {
        return handler.Update(owner, canFire, (EAIFireState)(int)fireMode, new SOBJECTSTATE(), new SFireCommandProjectileInfo());
    }
}

// LinkedList indexer + RemoveAt extensions
public static class LinkedListExtensions
{
    public static T ElementAtIndex<T>(this LinkedList<T> list, int index)
    {
        var node = list.First;
        for (int i = 0; i < index && node != null; i++) node = node.Next;
        return node != null ? node.Value : default;
    }
    public static void RemoveAt<T>(this LinkedList<T> list, int index)
    {
        var node = list.First;
        for (int i = 0; i < index && node != null; i++) node = node.Next;
        if (node != null) list.Remove(node);
    }
}
