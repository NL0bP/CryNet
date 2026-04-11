// Literal port shells of dev/Code/CryEngine/CryAISystem/{VisionMap,AIRadialOcclusion,AIRadialOcclusionRaycast,
// PerceptionManager,GlobalPerceptionScaleHandler,CentralInterestManager,PersonalInterestManager,
// MissLocationSensor,AILightManager}.h (Phase 5 — entire perception subsystem).
// Full literal ports + .cpp impls (~5000L combined) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// CVisionMap shell (~1618L .h+.cpp combined) — note: forward decl exists in AIObject.cs as IVisionMap
public class CVisionMap : IVisionMap
{
    public CVisionMap() { /* impl in .cpp */ }
    // virtual ~CVisionMap();

    public virtual void Reset() { /* impl in .cpp */ }

    public virtual VisionID CreateVisionID(string name) { return new VisionID(); /* impl in .cpp */ }

    public virtual void RegisterObserver(ObserverID observerID, ObserverParams observerParams) { /* impl in .cpp */ }
    public virtual void UnregisterObserver(ObserverID observerID) { /* impl in .cpp */ }

    public virtual void RegisterObservable(ObservableID observableID, ObservableParams observerParams) { /* impl in .cpp */ }
    public virtual void UnregisterObservable(ObservableID observableID) { /* impl in .cpp */ }

    public virtual void ObserverChanged(ObserverID observerID, ObserverParams observerParams, uint hint) { /* impl in .cpp */ }
    public virtual void ObservableChanged(ObservableID observableID, ObservableParams newObservableParams, uint hint) { /* impl in .cpp */ }

    public virtual bool IsVisible(ObserverID observerID, ObservableID observableID) { return false; /* impl in .cpp */ }

    public virtual ObserverParams GetObserverParams(ObserverID observerID) { return null; /* impl in .cpp */ }
    public virtual ObservableParams GetObservableParams(ObservableID observableID) { return null; /* impl in .cpp */ }

    public virtual void AddPriorityMapEntry(PriorityMapEntry priorityMapEntry) { /* impl in .cpp */ }
    public virtual void ClearPriorityMap() { /* impl in .cpp */ }

    public virtual void Update(float frameTime) { /* impl in .cpp */ }

#if VISIONMAP_DEBUG
    public void DebugDraw() { /* impl in .cpp */ }
#endif
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
}

// CPersonalInterestManager shell — note: forward decl exists in Puppet.cs
// public class CPersonalInterestManager already declared

// CMissLocationSensor shell — note: forward decl exists in AIPlayer.cs
// public class CMissLocationSensor already declared

// CAILightManager shell
public class CAILightManager
{
    public CAILightManager() { /* impl in .cpp */ }
}

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
public struct ObserverID
{
    public uint id;
    public static implicit operator ObserverID(VisionID v) => new ObserverID { id = v.id };
    public static implicit operator VisionID(ObserverID o) => new VisionID { id = o.id };
}
public struct ObservableID
{
    public uint id;
    public static implicit operator ObservableID(VisionID v) => new ObservableID { id = v.id };
    public static implicit operator VisionID(ObservableID o) => new VisionID { id = o.id };
}
public class PriorityMapEntry { }
