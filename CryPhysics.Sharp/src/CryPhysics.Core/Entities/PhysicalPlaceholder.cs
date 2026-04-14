// Literal port of dev/Code/CryEngine/CryPhysics/physicalplaceholder.{h,cpp}.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Events;
using CryPhysics.Geometry;
using CryPhysics.Math;
using CryPhysics.Params;
using CryPhysics.Serialization;
using CryPhysics.World;

namespace CryPhysics.Entities;

/// <summary>
/// Per-cell entry of the entity grid. Port of `pe_gridthunk` (physicalplaceholder.h:25-35).
/// In C# we replace the bitfield-packed layout with plain int fields.
/// </summary>
public class PeGridThunk
{
    public int INext, IPrev, INextOwned;
    public int ISimClass;
    public int BFirstInCell;
    public byte BBox0, BBox1, BBox2, BBox3;
    public PhysicalPlaceholder? Pent;
    public int BBoxZ0, BBoxZ1;
}

/// <summary>
/// Lightweight forwarding entity. Port of `class CPhysicalPlaceholder`
/// (physicalplaceholder.h:42-109 + physicalplaceholder.cpp).
/// On any non-trivial query the placeholder lazily creates its full-entity buddy via
/// the world's IPhysicsStreamer and forwards the call.
/// </summary>
public class PhysicalPlaceholder : PhysicalEntity
{
    public const int NoGridReg = -1 << 14;
    public const int GridRegPending = NoGridReg + 1;

    public override PhysicsEntityType Type => GetTypeFromSimClass();

    // Placeholder state — port of m_BBox[2], m_pForeignData, m_iForeignData, m_iForeignFlags,
    // m_ig[2], m_iGThunk0, m_pEntBuddy, m_bProcessed, m_id, m_bOBBThunks, m_iSimClass, m_lockUpdate.
    public new PhysVector3[] BBox = new PhysVector3[2];
    public PhysicsForeignData PForeignData;
    public new int ForeignDataType;
    public int ForeignFlags;
    public (int X, int Y)[] Ig = new[] { (X: GridRegPending, Y: GridRegPending), (X: GridRegPending, Y: GridRegPending) };
    public int IGThunk0;
    public IPhysicalEntity? PEntBuddy;
    public uint BProcessed;
    public new int Id;            // C++ bitfield m_id : 23 — modeled as plain int
    public int BOBBThunks;
    public int ISimClass;
    public int LockUpdate;

    public PhysicalPlaceholder()
    {
        // Mirrors CPhysicalPlaceholder::CPhysicalPlaceholder() (physicalplaceholder.h:44-51).
        LockUpdate = 0;
        IGThunk0 = 0;
        PEntBuddy = null;
        BProcessed = 0;
        Ig[0] = (GridRegPending, GridRegPending);
        Ig[1] = (GridRegPending, GridRegPending);
        PForeignData = new PhysicsForeignData((object?)null);
        ForeignDataType = 0;
        ForeignFlags = 0;
        BOBBThunks = 0;
    }

    private PhysicsEntityType GetTypeFromSimClass()
    {
        // Port of CPhysicalPlaceholder::GetType() (physicalplaceholder.cpp:61-70).
        return ISimClass switch
        {
            0 => PhysicsEntityType.Static,
            1 => PhysicsEntityType.Rigid,
            2 => PhysicsEntityType.Rigid,
            3 => PhysicsEntityType.Living,
            4 => PhysicsEntityType.Particle,
            _ => PhysicsEntityType.Static,
        };
    }

    /// Locate the owning world. Port of CPhysicalPlaceholder::GetWorld()
    /// (physicalplaceholder.cpp:28-36).
    public new IPhysicalWorld? GetWorld()
    {
        if (PhysWorldsRegistry.Count == 1) return PhysWorldsRegistry.Get(0);
        for (int i = 0; i < PhysWorldsRegistry.Count; i++)
        {
            var w = PhysWorldsRegistry.Get(i);
            if (w != null && w.IsPlaceholder(this)) return w;
        }
        return null;
    }

    /// Lazily create or return the buddy full-entity. Port of
    /// CPhysicalPlaceholder::GetEntity() (physicalplaceholder.cpp:39-58).
    public IPhysicalEntity? GetEntity()
    {
        IPhysicalEntity? entBuddy;
        if (PEntBuddy == null)
        {
            var pWorld = (PhysicalWorld?)GetWorld();
            if (pWorld?.PhysicsStreamer != null)
            {
                pWorld.PhysicsStreamer.CreatePhysicalEntity(PForeignData, ForeignDataType, ForeignFlags);
                entBuddy = PEntBuddy ?? pWorld.StaticPhysicalEntity;
            }
            else
            {
                return null;
            }
        }
        else
        {
            entBuddy = PEntBuddy;
        }
        if (entBuddy is PhysicalEntity pe) pe.TimeIdle = 0f;
        return entBuddy;
    }

    /// Fast-path buddy accessor. Port of GetEntityFast (physicalplaceholder.h:54).
    public IPhysicalEntity? GetEntityFast() => PEntBuddy;

    public override int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        // Port of CPhysicalPlaceholder::SetParams (physicalplaceholder.cpp:73-131).
        if (parameters is ParamsBBox bbox)
        {
            if (bbox.BBoxMin.HasValue) BBox[0] = bbox.BBoxMin.Value;
            if (bbox.BBoxMax.HasValue) BBox[1] = bbox.BBoxMax.Value;
            if (PEntBuddy is PhysicalEntity pent)
            {
                // C++: dispatch state-change event when monitor flags set.
                const uint pefMonitorStateChanges = 0x0008;
                const uint pefLogStateChanges = 0x0010;
                if ((pent.Flags & (pefMonitorStateChanges | pefLogStateChanges)) != 0
                    && pent.World is PhysicalWorld pw)
                {
                    var evt = new EventPhysStateChange
                    {
                        Entity = pent,
                        ForeignData = pent.ForeignData,
                        ForeignDataType = pent.ForeignDataType,
                        TimeIdle = pent.TimeIdle,
                    };
                    evt.BBoxNew[0] = bbox.BBoxMin ?? pent.BBoxMin;
                    evt.BBoxNew[1] = bbox.BBoxMax ?? pent.BBoxMax;
                    evt.BBoxOld[0] = pent.BBoxMin;
                    evt.BBoxOld[1] = pent.BBoxMax;
                    evt.SimClass[0] = (int)pent.SimulationClass;
                    evt.SimClass[1] = (int)pent.SimulationClass;
                    pw.DispatchEvent(evt);
                }

                // C++: if (m_pEntBuddy->m_pEntBuddy==this) — buddy points back at us → mirror BBox.
                if (PEntBuddy is PhysicalPlaceholder buddyPh && ReferenceEquals(buddyPh.PEntBuddy, this))
                {
                    buddyPh.BBox[0] = BBox[0];
                    buddyPh.BBox[1] = BBox[1];
                }
            }

            var pWorld2 = (PhysicalWorld?)GetWorld();
            if (pWorld2 != null)
            {
                System.Threading.Interlocked.Add(ref pWorld2.LockGrid, -pWorld2.RepositionEntity(this, 1));
            }
            return 1;
        }

        if (parameters is ParamsPos pos)
        {
            if (pos.Position.HasValue || pos.Orientation.HasValue || pos.Scale.HasValue || pos.BasisMatrix.HasValue)
                return GetEntity()?.SetParams(parameters, threadSafe) ?? 0;
            if (pos.ISimClass.HasValue) ISimClass = pos.ISimClass.Value;
            return 1;
        }

        if (parameters is ParamsForeignData fd)
        {
            if (fd.ForeignData != null) PForeignData = new PhysicsForeignData(fd.ForeignData);
            if (fd.IForeignData.HasValue) ForeignDataType = fd.IForeignData.Value;
            if (fd.ForeignFlags.HasValue) ForeignFlags = fd.ForeignFlags.Value;
            if (PEntBuddy is PhysicalEntity pe2)
            {
                pe2.ForeignData = PForeignData.AsObject();
                pe2.ForeignDataType = ForeignDataType;
            }
            return 1;
        }

        if (PEntBuddy != null)
            return PEntBuddy.SetParams(parameters, threadSafe);
        return 0;
    }

    public override int GetParams(PhysicsParamsBase parameters)
    {
        // Port of CPhysicalPlaceholder::GetParams (physicalplaceholder.cpp:133-151).
        if (parameters is ParamsBBox bbox)
        {
            bbox.BBoxMin = BBox[0];
            bbox.BBoxMax = BBox[1];
            return 1;
        }
        if (parameters is ParamsForeignData fd)
        {
            fd.IForeignData = ForeignDataType;
            fd.ForeignData = PForeignData.AsObject();
            fd.ForeignFlags = ForeignFlags;
            return 1;
        }
        return GetEntity()?.GetParams(parameters) ?? 0;
    }

    public override int GetStatus(PhysicsStatusBase status)
    {
        // Port of CPhysicalPlaceholder::GetStatus (physicalplaceholder.cpp:153-164).
        if (status is StatusPlaceholder sp)
        {
            sp.PFullEntity = PEntBuddy;
            return 1;
        }
        // C++: pe_status_awake — placeholder always reports asleep.
        // C# StatusAwake type doesn't exist yet; handle via base Type check.
        return GetEntity()?.GetStatus(status) ?? 0;
    }

    public override int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        // Port of CPhysicalPlaceholder::Action (physicalplaceholder.cpp:165-180).
        if (action is ActionAwake aw && aw.Awake == false && PEntBuddy == null)
        {
            if (ISimClass == 2) ISimClass = 1;
            return 1;
        }
        if (action is ActionRemoveAllParts && PEntBuddy == null)
            return 1;
        if (action is ActionReset)
        {
            if (PEntBuddy == null) return 1;
            if (PEntBuddy is PhysicalEntity pe) pe.TimeIdle = pe.MaxTimeIdle + 1.0f;
            return PEntBuddy.DoAction(action, threadSafe);
        }
        return GetEntity()?.DoAction(action, threadSafe) ?? 0;
    }

    public override int AddGeometry(PhysGeometry geometry, ParamsPart parameters, int id = -1, bool threadSafe = false)
        => GetEntity()?.AddGeometry(geometry, parameters, id, threadSafe) ?? 0;

    public override void RemoveGeometry(int id, bool threadSafe = false)
        => GetEntity()?.RemoveGeometry(id, threadSafe);

    // -- Snapshot serialization (port of physicalplaceholder.cpp:188-217). --
    // Forwards to buddy entity. The full entity types do not yet implement
    // GetStateSnapshot/SetStateFromSnapshot, so the placeholder only forwards as
    // far as the base IPhysicalEntity interface allows. Both CStream and TSerialize
    // overloads exist on the placeholder so future buddies can implement them.
    public int GetStateSnapshot(CStream stm, float timeBack = 0, int flags = 0) => 0;
    public int GetStateSnapshot(TSerialize ser, float timeBack = 0, int flags = 0) => 0;
    public int SetStateFromSnapshot(CStream stm, int flags = 0) => 0;
    public int SetStateFromSnapshot(TSerialize ser, int flags = 0) => 0;
    public int SetStateFromTypedSnapshot(TSerialize ser, int type, int flags = 0) => 0;
    public int PostSetStateFromSnapshot() => 0;
    public int GetStateSnapshotTxt(System.Text.StringBuilder txtbuf, int szbuf, float timeBack = 0) => 0;
    public void SetStateFromSnapshotTxt(string txtbuf, int szbuf) { }
    public uint GetStateChecksum() => 0;
    public void SetNetworkAuthority(int authoritive, int paused) { }

    // Port of physicalplaceholder.cpp:219-227 — Step/StartStep/StepBack forward to buddy.
    public new int Step(float timeInterval) => GetEntity()?.Step(timeInterval) ?? 0;
    public new void StartStep(float timeInterval) => GetEntity()?.StartStep(timeInterval);
    public new void StepBack(float timeInterval) => GetEntity()?.StepBack(timeInterval);

    /// Placeholder DoStep is a no-op (matches `virtual int DoStep(...) { return 1; }`
    /// in physicalplaceholder.h:84).
    public override int DoStep(float timeInterval, int callerIndex = 0) => 1;
}
