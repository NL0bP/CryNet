// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/NavigationSystem/NavigationSystem.h (706L)
// and NavigationSystem.cpp (4265L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CryAISystem.CryCommon;
using MNM = CryAISystem.Navigation.MNM;

namespace CryAISystem.Navigation.NavigationSystem;

// NavigationMesh — NavigationSystem.h lines 245-278
public class NavigationMesh
{
    public NavigationMesh(NavigationAgentTypeID agentTypeID) { this.agentTypeID = agentTypeID; }
    public NavigationMesh() : this(new NavigationAgentTypeID()) { }

    public NavigationAgentTypeID agentTypeID;
    public int version;
    public MNM.MeshGrid grid = new MNM.MeshGrid();
    public NavigationVolumeID boundary;
    public List<NavigationVolumeID> exclusions = new List<NavigationVolumeID>();
    public string name = "";
}

// AgentType — NavigationSystem.h lines 281-365
public class AgentType
{
    public struct Settings
    {
        public Vec3 voxelSize;
        public ushort radiusVoxelCount, climbableVoxelCount, heightVoxelCount, maxWaterDepthVoxelCount;
        public float climbableInclineGradient, climbableStepRatio;
    }
    public struct MeshInfo
    {
        public NavigationMeshID id; public uint name;
        public MeshInfo(NavigationMeshID id, uint name) { this.id = id; this.name = name; }
    }

    public Settings settings;
    public List<MeshInfo> meshes = new();
    public List<NavigationVolumeID> exclusions = new();
    public List<Action<NavigationAgentTypeID, NavigationMeshID, uint>> callbacks = new();
    public Func<IPhysicalEntity, uint, bool> meshEntityCallback;
    public List<string> smartObjectUserClasses = new();
    public string name = "";

    public AgentType() { }
    public AgentType(AgentType o) { MakeDeepCopy(o); }
    public void MakeDeepCopy(AgentType o)
    {
        settings = o.settings; meshes = new(o.meshes); exclusions = new(o.exclusions);
        callbacks = new(o.callbacks); meshEntityCallback = o.meshEntityCallback;
        smartObjectUserClasses = new(); foreach (var s in o.smartObjectUserClasses) smartObjectUserClasses.Add(string.Copy(s));
        name = string.Copy(o.name ?? "");
    }
}

// INavigationSystem — INavigationSystem.h
public interface INavigationSystem
{
    enum ENavigationEvent { MeshReloaded = 0, MeshReloadedAfterExporting, NavigationCleared }
    enum EAccessbilityDir { AccessibilityToward, AccessibilityAway }
    interface INavigationSystemListener { void OnNavigationEvent(ENavigationEvent e); }
    enum WorkingState { Idle = 0, Working }
    struct CreateAgentTypeParams
    {
        public Vec3 voxelSize; public float climbableInclineGradient, climbableStepRatio;
        public ushort radiusVoxelCount, climbableVoxelCount, heightVoxelCount, maxWaterDepthVoxelCount;
    }
    struct CreateMeshParams { public Vec3 origin; public Vec3 tileSize; public uint tileCount; }
}

public interface INavigationSystemUser
{
    void Reset(); void UpdateForSynchronousOrAsynchronousReadingOperation();
    void UpdateForSynchronousWritingOperations(); void CompleteRunningTasks();
}

// NavigationSystem — NavigationSystem.h/cpp
public class NavigationSystem : INavigationSystem
{
    public struct TileTask : IEquatable<TileTask>, IComparable<TileTask>
    {
        public NavigationMeshID meshID;
        public ushort x, y, z;
        public bool aborted;

        public bool Equals(TileTask other) =>
            meshID.id == other.meshID.id && x == other.y && y == other.y && z == other.z;
        public int CompareTo(TileTask other)
        {
            if (meshID.id != other.meshID.id) return meshID.id < other.meshID.id ? -1 : 1;
            if (x != other.x) return x < other.x ? -1 : 1;
            if (y != other.y) return y < other.y ? -1 : 1;
            if (z != other.z) return z < other.z ? -1 : 1;
            return 0;
        }
    }
    public class TileTaskResult
    {
        public const int Running = 0, Completed = 1, NoChanges = 2, Failed = 3;
        public MNM.Tile tile = new(); public uint hashValue; public NavigationMeshID meshID;
        public ushort x, y, z, volumeCopy; public volatile int state; public ushort next;
    }

    // VolumeDefCopy — NavigationSystem.h lines 659-675
    private class VolumeDefCopy
    {
        public int version = ~0;
        public int refCount;
        public NavigationMeshID meshID;
        public MNM.BoundingVolume boundary = new();
        public List<MNM.BoundingVolume> exclusions = new();
    }

    // Constants — NavigationSystem.cpp lines 59-60
    private const int MaxTaskCountPerWorkerThread = 12;
    private const int MaxVolumeDefCopyCount = 8;
    private const int BAI_NAVIGATION_FILE_VERSION = 7;
    private const int MAX_NAME_LENGTH = 512;

    // Fields
    private LinkedList<TileTask> m_tileQueue = new();
    private List<ushort> m_runningTasks = new();
    private int m_maxRunningTaskCount; private float m_cacheHitRate, m_throughput;
    private List<TileTaskResult> m_results = new(); private ushort m_free;
    private INavigationSystem.WorkingState m_state;
    private id_map<uint, NavigationMesh> m_meshes;
    private id_map<uint, MNM.BoundingVolume> m_volumes;
    private List<AgentType> m_agentTypes = new(); private uint m_configurationVersion;
    private AABB m_worldAABB; private WorldMonitor m_worldMonitor;
    private OffMeshNavigationManager m_offMeshNavigationManager;
    private IslandConnectionsManager m_islandConnectionsManager;
    private string m_configName;
    private List<INavigationSystem.INavigationSystemListener> m_listenersList;
    private List<INavigationSystemUser> m_users;
    private CVolumesManager m_volumesManager = new(); private bool m_isNavigationUpdatePaused;
    private List<VolumeDefCopy> m_volumeDefCopy;

    // Constructor
    public NavigationSystem(string configName)
    {
        m_configName = configName; m_throughput = 0; m_cacheHitRate = 0; m_free = 0;
        m_state = INavigationSystem.WorkingState.Idle;
        m_meshes = new id_map<uint, NavigationMesh>(256);
        m_offMeshNavigationManager = new OffMeshNavigationManager(256);
        m_islandConnectionsManager = new IslandConnectionsManager();
        m_volumes = new id_map<uint, MNM.BoundingVolume>(512);
        m_worldAABB = new AABB(AABB.RESET);
        m_volumeDefCopy = new List<VolumeDefCopy>(MaxVolumeDefCopyCount);
        for (int i = 0; i < MaxVolumeDefCopyCount; i++) m_volumeDefCopy.Add(new VolumeDefCopy());
        m_listenersList = new(10); m_users = new(10);
        m_configurationVersion = 0; m_isNavigationUpdatePaused = false;
        SetupTasks(); m_worldMonitor = new WorldMonitor(WorldChanged); StartWorldMonitoring(); ReloadConfig();
    }
    public NavigationSystem() : this("") { }

    // Agent Types
    public NavigationAgentTypeID CreateAgentType(string name, INavigationSystem.CreateAgentTypeParams p)
    {
        foreach (var a in m_agentTypes) if (string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase)) { AILog.AIWarning("Duplicate AgentType '{0}'", name); return new(); }
        var at = new AgentType { name = name }; at.settings.voxelSize = p.voxelSize; at.settings.radiusVoxelCount = p.radiusVoxelCount;
        at.settings.climbableVoxelCount = p.climbableVoxelCount; at.settings.climbableInclineGradient = p.climbableInclineGradient;
        at.settings.climbableStepRatio = p.climbableStepRatio; at.settings.heightVoxelCount = p.heightVoxelCount;
        at.settings.maxWaterDepthVoxelCount = p.maxWaterDepthVoxelCount; m_agentTypes.Add(at);
        return new NavigationAgentTypeID { id = (uint)m_agentTypes.Count };
    }

    public NavigationAgentTypeID GetAgentTypeID(string name) { for (int i = 0; i < m_agentTypes.Count; i++) if (string.Equals(m_agentTypes[i].name, name, StringComparison.OrdinalIgnoreCase)) return new() { id = (uint)(i + 1) }; return new(); }
    public NavigationAgentTypeID GetAgentTypeID(int index) => index < m_agentTypes.Count ? new() { id = (uint)(index + 1) } : new();
    public string GetAgentTypeName(NavigationAgentTypeID id) => (id.id != 0 && id.id <= (uint)m_agentTypes.Count) ? m_agentTypes[(int)(id.id - 1)].name : null;
    public int GetAgentTypeCount() => m_agentTypes.Count;
    public bool GetAgentTypeProperties(NavigationAgentTypeID id, out AgentType p) { if (id.id != 0 && id.id <= (uint)m_agentTypes.Count) { p = new AgentType(m_agentTypes[(int)(id.id - 1)]); return true; } p = null; return false; }
    public bool AgentTypeSupportSmartObjectUserClass(NavigationAgentTypeID id, string cls) { if (id.id != 0 && id.id <= (uint)m_agentTypes.Count) foreach (var u in m_agentTypes[(int)(id.id - 1)].smartObjectUserClasses) if (u == cls) return true; return false; }
    public ushort GetAgentRadiusInVoxelUnits(NavigationAgentTypeID id) => (id.id != 0 && id.id <= (uint)m_agentTypes.Count) ? m_agentTypes[(int)(id.id - 1)].settings.radiusVoxelCount : (ushort)0;
    public ushort GetAgentHeightInVoxelUnits(NavigationAgentTypeID id) => (id.id != 0 && id.id <= (uint)m_agentTypes.Count) ? m_agentTypes[(int)(id.id - 1)].settings.heightVoxelCount : (ushort)0;

    // Mesh Management
    static uint NameHash(string n) { uint h = 0; if (n != null) foreach (char c in n) { h += (byte)char.ToLowerInvariant(c); h += h << 10; h ^= h >> 6; } h += h << 3; h ^= h >> 11; h += h << 15; return h; }

    public NavigationMeshID CreateMesh(string name, NavigationAgentTypeID atId, INavigationSystem.CreateMeshParams p) => CreateMesh(name, atId, p, new());
    public NavigationMeshID CreateMesh(string name, NavigationAgentTypeID atId, INavigationSystem.CreateMeshParams p, NavigationMeshID reqId)
    {
        if (atId.id != 0 && atId.id <= (uint)m_agentTypes.Count)
        {
            var at = m_agentTypes[(int)(atId.id - 1)]; var gp = new MNM.MeshGrid.Params { tileSize = new Vec3i((int)p.tileSize.x, (int)p.tileSize.y, (int)p.tileSize.z), voxelSize = at.settings.voxelSize, tileCount = p.tileCount };
            NavigationMeshID id = reqId; if (reqId.id == 0) id.id = m_meshes.insert(new NavigationMesh(atId)); else m_meshes.insert(reqId.id, new NavigationMesh(atId));
            var mesh = m_meshes[id.id]; mesh.grid.Init(gp); mesh.name = name; mesh.exclusions = new(at.exclusions);
            at.meshes.Add(new AgentType.MeshInfo(id, NameHash(name))); m_offMeshNavigationManager.OnNavigationMeshCreated(id); return id;
        }
        return new();
    }

    public void DestroyMesh(NavigationMeshID mid)
    {
        if (mid.id != 0 && m_meshes.validate(mid.id))
        {
            var mesh = m_meshes[mid.id];
            for (int t = 0; t < m_runningTasks.Count; t++) if (m_results[m_runningTasks[t]].meshID.id == mid.id) m_results[m_runningTasks[t]].state = TileTaskResult.Failed;
            var at = m_agentTypes[(int)(mesh.agentTypeID.id - 1)];
            for (int i = 0; i < at.meshes.Count; i++) if (at.meshes[i].id.id == mid.id) { at.meshes[i] = at.meshes[^1]; at.meshes.RemoveAt(at.meshes.Count - 1); break; }
            for (var n = m_tileQueue.First; n != null; n = n.Next) if (n.Value.meshID.id == mid.id) { var v = n.Value; v.aborted = true; n.Value = v; }
            m_meshes.erase(mid.id); m_offMeshNavigationManager.OnNavigationMeshDestroyed(mid); ComputeWorldAABB();
        }
    }

    // Mesh Accessors
    public NavigationMesh GetMesh(NavigationMeshID mid) { if (mid.id != 0 && m_meshes.validate(mid.id)) return m_meshes[mid.id]; Debug.Assert(false); return new(); }
    public NavigationMeshID GetEnclosingMeshID(NavigationAgentTypeID atId, Vec3 loc)
    {
        if (atId.id != 0 && atId.id <= (uint)m_agentTypes.Count) foreach (var mi in m_agentTypes[(int)(atId.id - 1)].meshes)
            { var mesh = m_meshes[mi.id.id]; if (mesh.boundary.id != 0 && m_volumes[mesh.boundary.id].Contains(loc)) return mi.id; }
        return new();
    }
    public bool IsLocationInMesh(NavigationMeshID mid, Vec3 loc) { if (mid.id != 0 && m_meshes.validate(mid.id)) { var m = m_meshes[mid.id]; return m.boundary.id != 0 && m_volumes[m.boundary.id].Contains(loc); } return false; }
    public NavigationMeshID GetMeshID(string name, NavigationAgentTypeID atId) { if (atId.id != 0 && atId.id <= (uint)m_agentTypes.Count) { uint h = NameHash(name); foreach (var mi in m_agentTypes[(int)(atId.id - 1)].meshes) if (mi.name == h) return mi.id; } return new(); }
    public string GetMeshName(NavigationMeshID mid) => (mid.id != 0 && m_meshes.validate(mid.id)) ? m_meshes[mid.id].name : null;
    public void SetMeshName(NavigationMeshID mid, string name) { if (mid.id != 0 && m_meshes.validate(mid.id)) { var m = m_meshes[mid.id]; m.name = name; var at = m_agentTypes[(int)(m.agentTypeID.id - 1)]; for (int i = 0; i < at.meshes.Count; i++) if (at.meshes[i].id.id == mid.id) { var mi = at.meshes[i]; mi.name = NameHash(name); at.meshes[i] = mi; break; } } }

    // Volume Management
    public NavigationVolumeID CreateVolume(Vec3[] v, int vc, float h) => CreateVolume(v, vc, h, new());
    public NavigationVolumeID CreateVolume(Vec3[] v, int vc, float h, NavigationVolumeID req) { var id = req; if (req.id == 0) id = new(m_volumes.insert(new MNM.BoundingVolume())); else m_volumes.insert(req.id, new MNM.BoundingVolume()); SetVolume(id, v, vc, h); return id; }
    public void DestroyVolume(NavigationVolumeID vid) { if (vid.id != 0 && m_volumes.validate(vid.id)) { var vol = m_volumes[vid.id]; foreach (var at in m_agentTypes) foreach (var mi in at.meshes) { var m = m_meshes[mi.id.id]; if (m.boundary == vid) { m.version++; continue; } if (m.exclusions.Remove(vid)) { QueueMeshUpdate(mi.id, vol.aabb); m.version++; } } if (gEnv.IsEditor()) m_volumesManager.InvalidateID(vid); m_volumes.erase(vid.id); } }
    public void SetVolume(NavigationVolumeID vid, Vec3[] verts, int vc, float h) { if (vid.id != 0 && m_volumes.validate(vid.id)) { bool recompute = false; var nv = new MNM.BoundingVolume(); var ab = new AABB(AABB.RESET); for (int i = 0; i < vc; i++) { ab.Add(verts[i]); nv.vertices.Add(verts[i]); } ab.Add(verts[0] + new Vec3(0, 0, h)); nv.height = h; nv.aabb = ab; var vol = m_volumes[vid.id]; if (vol.vertices.Count > 0) foreach (var at in m_agentTypes) foreach (var mi in at.meshes) { var m = m_meshes[mi.id.id]; if (m.boundary == vid) { m.version++; recompute = true; QueueDifferenceUpdate(mi.id, vol, nv); } if (m.exclusions.Contains(vid)) { QueueMeshUpdate(mi.id, vol.aabb); QueueMeshUpdate(mi.id, ab); m.version++; } } if (recompute) ComputeWorldAABB(); nv.Swap(vol); } }
    public bool ValidateVolume(NavigationVolumeID id) => m_volumes.validate(id.id);
    public NavigationVolumeID GetVolumeID(NavigationMeshID mid) => (mid.id != 0 && m_meshes.validate(mid.id)) ? m_meshes[mid.id].boundary : new();
    public void SetMeshBoundaryVolume(NavigationMeshID mid, NavigationVolumeID vid) { if (mid.id != 0 && m_meshes.validate(mid.id)) { var m = m_meshes[mid.id]; if (m.boundary.id != 0) m.version++; m.boundary = vid; ComputeWorldAABB(); } }
    public void SetExclusionVolume(NavigationAgentTypeID[] atIds, int cnt, NavigationVolumeID vid)
    { if (vid.id != 0 && m_volumes.validate(vid.id)) { var vol = m_volumes[vid.id]; bool rc = false; foreach (var at in m_agentTypes) { at.exclusions.Remove(vid); foreach (var mi in at.meshes) { var m = m_meshes[mi.id.id]; if (m.exclusions.Remove(vid)) { QueueMeshUpdate(mi.id, vol.aabb); m.version++; } } }
      for (int i = 0; i < cnt; i++) if (atIds[i].id != 0 && atIds[i].id <= (uint)m_agentTypes.Count) { var at = m_agentTypes[(int)(atIds[i].id - 1)]; at.exclusions.Add(vid); foreach (var mi in at.meshes) { var m = m_meshes[mi.id.id]; m.exclusions.Add(vid); if (m.boundary != vid) QueueMeshUpdate(mi.id, vol.aabb); else { m.version++; m.boundary = new(); rc = true; } } } if (rc) ComputeWorldAABB(); } }

    // State / Update
    public INavigationSystem.WorkingState GetState() => m_state;
    public INavigationSystem.WorkingState Update(bool blocking = false) { WaitForAll(); UpdateInternal(blocking); UpdateSubsystems(); UpdateWriting(); UpdateReading(); return m_state; }
    public void PauseNavigationUpdate() => m_isNavigationUpdatePaused = true;
    public void RestartNavigationUpdate() => m_isNavigationUpdatePaused = false;

    // QueueMeshUpdate — NavigationSystem.cpp lines 1192-1281
    public int QueueMeshUpdate(NavigationMeshID meshID, AABB aabb)
    {
        Debug.Assert(meshID.id != 0);
        int affectedCount = 0;

        if (meshID.id != 0 && m_meshes.validate(meshID.id))
        {
            var mesh = m_meshes[meshID.id];
            var grid = mesh.grid;
            var paramsGrid = grid.GetParams();

            if (aabb.IsReset() || mesh.boundary.id == 0)
                return 0;

            var boundary = m_volumes[mesh.boundary.id].aabb;
            var agentType = m_agentTypes[(int)(mesh.agentTypeID.id - 1)];

            float extraH = MathF.Max(paramsGrid.voxelSize.x, paramsGrid.voxelSize.y) * (agentType.settings.radiusVoxelCount + 1);
            float extraV = paramsGrid.voxelSize.z * (agentType.settings.heightVoxelCount + 1);
            float extraVM = paramsGrid.voxelSize.z; // tiles above are not directly influenced

            Vec3 bmin = new Vec3(
                MathF.Max(0.0f, MathF.Max(boundary.min.x, aabb.min.x - extraH) - paramsGrid.origin.x),
                MathF.Max(0.0f, MathF.Max(boundary.min.y, aabb.min.y - extraH) - paramsGrid.origin.y),
                MathF.Max(0.0f, MathF.Max(boundary.min.z, aabb.min.z - extraV) - paramsGrid.origin.z));

            Vec3 bmax = new Vec3(
                MathF.Max(0.0f, MathF.Min(boundary.max.x, aabb.max.x + extraH) - paramsGrid.origin.x),
                MathF.Max(0.0f, MathF.Min(boundary.max.y, aabb.max.y + extraH) - paramsGrid.origin.y),
                MathF.Max(0.0f, MathF.Min(boundary.max.z, aabb.max.z + extraVM) - paramsGrid.origin.z));

            ushort xmin = (ushort)MathF.Floor(bmin.x / paramsGrid.tileSize.x);
            ushort xmax = (ushort)MathF.Floor(bmax.x / paramsGrid.tileSize.x);

            ushort ymin = (ushort)MathF.Floor(bmin.y / paramsGrid.tileSize.y);
            ushort ymax = (ushort)MathF.Floor(bmax.y / paramsGrid.tileSize.y);

            ushort zmin = (ushort)MathF.Floor(bmin.z / paramsGrid.tileSize.z);
            ushort zmax = (ushort)MathF.Floor(bmax.z / paramsGrid.tileSize.z);

            // Remove existing tasks in this range
            var node = m_tileQueue.First;
            while (node != null)
            {
                var next = node.Next;
                var task = node.Value;
                if (task.meshID.id == meshID.id && task.x >= xmin && task.x <= xmax &&
                    task.y >= ymin && task.y <= ymax &&
                    task.z >= zmin && task.z <= zmax)
                {
                    m_tileQueue.Remove(node);
                }
                node = next;
            }

            for (int y = ymin; y <= ymax; y++)
            {
                for (int x = xmin; x <= xmax; x++)
                {
                    for (int z = zmin; z <= zmax; z++)
                    {
                        var task = new TileTask();
                        task.meshID = meshID;
                        task.x = (ushort)x;
                        task.y = (ushort)y;
                        task.z = (ushort)z;
                        m_tileQueue.AddLast(task);
                        affectedCount++;
                    }
                }
            }
        }

        return affectedCount;
    }

    // ProcessQueuedMeshUpdates — NavigationSystem.cpp lines 1283-1291
    public void ProcessQueuedMeshUpdates()
    {
        do
        {
            UpdateMeshes(0.0333f, false, false, false);
        } while (m_state == INavigationSystem.WorkingState.Working);
    }

    // QueueDifferenceUpdate — NavigationSystem.cpp lines 1293-1380
    public void QueueDifferenceUpdate(NavigationMeshID meshID, MNM.BoundingVolume oldVolume, MNM.BoundingVolume newVolume)
    {
        if (meshID.id != 0 && m_meshes.validate(meshID.id))
        {
            var mesh = m_meshes[meshID.id];
            var grid = mesh.grid;
            var paramsGrid = grid.GetParams();

            AABB aabb = oldVolume.aabb;
            aabb.Add(newVolume.aabb);

            var agentType = m_agentTypes[(int)(mesh.agentTypeID.id - 1)];

            float extraH = MathF.Max(paramsGrid.voxelSize.x, paramsGrid.voxelSize.y) * (agentType.settings.radiusVoxelCount + 1);
            float extraV = paramsGrid.voxelSize.z * (agentType.settings.heightVoxelCount + 1);
            float extraVM = paramsGrid.voxelSize.z;

            Vec3 bmin = new Vec3(
                MathF.Max(0.0f, (aabb.min.x - extraH) - paramsGrid.origin.x),
                MathF.Max(0.0f, (aabb.min.y - extraH) - paramsGrid.origin.y),
                MathF.Max(0.0f, (aabb.min.z - extraV) - paramsGrid.origin.z));

            Vec3 bmax = new Vec3(
                MathF.Max(0.0f, (aabb.max.x + extraH) - paramsGrid.origin.x),
                MathF.Max(0.0f, (aabb.max.y + extraH) - paramsGrid.origin.y),
                MathF.Max(0.0f, (aabb.max.z + extraVM) - paramsGrid.origin.z));

            ushort xmin = (ushort)MathF.Floor(bmin.x / paramsGrid.tileSize.x);
            ushort xmax = (ushort)MathF.Floor(bmax.x / paramsGrid.tileSize.x);

            ushort ymin = (ushort)MathF.Floor(bmin.y / paramsGrid.tileSize.y);
            ushort ymax = (ushort)MathF.Floor(bmax.y / paramsGrid.tileSize.y);

            ushort zmin = (ushort)MathF.Floor(bmin.z / paramsGrid.tileSize.z);
            ushort zmax = (ushort)MathF.Floor(bmax.z / paramsGrid.tileSize.z);

            // Remove existing tasks in this range
            var node = m_tileQueue.First;
            while (node != null)
            {
                var next = node.Next;
                var task = node.Value;
                if (task.meshID.id == meshID.id && task.x >= xmin && task.x <= xmax &&
                    task.y >= ymin && task.y <= ymax &&
                    task.z >= zmin && task.z <= zmax)
                {
                    m_tileQueue.Remove(node);
                }
                node = next;
            }

            for (int y = ymin; y <= ymax; y++)
            {
                for (int x = xmin; x <= xmax; x++)
                {
                    for (int z = zmin; z <= zmax; z++)
                    {
                        var task = new TileTask();
                        task.meshID = meshID;
                        task.x = (ushort)x;
                        task.y = (ushort)y;
                        task.z = (ushort)z;
                        m_tileQueue.AddLast(task);
                    }
                }
            }
        }
    }

    // WorldChanged — NavigationSystem.cpp lines 1382-1408
    public void WorldChanged(AABB aabb)
    {
        if (!aabb.IsReset() && Overlap.AABB_AABB(m_worldAABB, aabb))
        {
            foreach (var at in m_agentTypes)
            {
                foreach (var mi in at.meshes)
                {
                    var meshID = mi.id;
                    var mesh = m_meshes[meshID.id];
                    if (mesh.boundary.id != 0 && Overlap.AABB_AABB(aabb, m_volumes[mesh.boundary.id].aabb))
                        QueueMeshUpdate(meshID, aabb);
                }
            }
        }
    }

    // Island Connections
    public void ComputeIslands() { m_islandConnectionsManager.Reset(); foreach (var at in m_agentTypes) foreach (var mi in at.meshes) if (mi.id.id != 0 && m_meshes.validate(mi.id.id)) { var ic = m_islandConnectionsManager.GetIslandConnections(); var omn = m_offMeshNavigationManager.GetOffMeshNavigationForMesh(mi.id); m_meshes[mi.id.id].grid.ComputeStaticIslandsAndConnections(mi.id, omn, ic); } }
    public void AddIslandConnectionsBetweenTriangles(NavigationMeshID mid, uint startTri, uint endTri) { if (m_meshes.validate(mid.id)) { var mesh = m_meshes[mid.id]; MNM.Tile.Triangle st, et; if (mesh.grid.GetTriangle(startTri, out st) && mesh.grid.GetTriangle(endTri, out et)) { var si = new MNM.GlobalIslandID(mid.id, st.islandID); var tile = mesh.grid.GetTile(MNM.MNMUtils.ComputeTileID(startTri)); for (ushort l = 0; l < st.linkCount; l++) { var lk = tile.links[st.firstLink + l]; if (lk.side == MNM.Tile.Link.OffMesh) { var ei = new MNM.GlobalIslandID(mid.id, et.islandID); var omn = m_offMeshNavigationManager.GetOffMeshNavigationForMesh(mid); var lr = omn.GetLinksForTriangle(startTri, lk.triangle); MNM.WayTriangleData nt; while ((nt = lr.GetNextTriangle()).IsValid) if (nt.triangleID == endTri) { var pl = omn.GetObjectLinkInfo(nt.offMeshLinkID); Debug.Assert(pl != null); m_islandConnectionsManager.GetIslandConnections().SetOneWayConnectionBetweenIsland(si, new MNM.IslandConnections.Link(nt.triangleID, nt.offMeshLinkID, ei, pl.GetEntityIdForOffMeshLink())); } } } } } }
    public void RemoveIslandsConnectionBetweenTriangles(NavigationMeshID mid, uint startTri, uint endTri = 0) { if (m_meshes.validate(mid.id)) { var mesh = m_meshes[mid.id]; MNM.Tile.Triangle st, et; if (mesh.grid.GetTriangle(startTri, out st) && mesh.grid.GetTriangle(endTri, out et)) { var si = new MNM.GlobalIslandID(mid.id, st.islandID); var tile = mesh.grid.GetTile(MNM.MNMUtils.ComputeTileID(startTri)); for (ushort l = 0; l < st.linkCount; l++) { var lk = tile.links[st.firstLink + l]; if (lk.side == MNM.Tile.Link.OffMesh) { var ei = new MNM.GlobalIslandID(mid.id, et.islandID); var omn = m_offMeshNavigationManager.GetOffMeshNavigationForMesh(mid); var lr = omn.GetLinksForTriangle(startTri, lk.triangle); MNM.WayTriangleData nt; while ((nt = lr.GetNextTriangle()).IsValid) if (nt.triangleID == endTri) { var pl = omn.GetObjectLinkInfo(nt.offMeshLinkID); Debug.Assert(pl != null); m_islandConnectionsManager.GetIslandConnections().RemoveOneWayConnectionBetweenIsland(si, new MNM.IslandConnections.Link(nt.triangleID, nt.offMeshLinkID, ei, pl.GetEntityIdForOffMeshLink())); } } } } } }
    public void RemoveAllIslandConnectionsForObject(NavigationMeshID mid, uint objId) => m_islandConnectionsManager.GetIslandConnections().RemoveAllIslandConnectionsForObject(mid, objId);

    // Closest Point / Reachability
    public uint GetClosestMeshLocation(NavigationMeshID mid, Vec3 loc, float vr, float hr, out Vec3 ml, out float dSq) { ml = loc; dSq = 0; if (mid.id != 0 && m_meshes.validate(mid.id)) { var l = new MNM.vector3_t(loc); var m = m_meshes[mid.id]; var r = new MNM.real_t(vr); uint e = m.grid.GetTriangleAt(l, r, r); if (e != 0) return e; var ds = new MNM.real_t(0); var cl = new MNM.vector3_t(); uint ct = m.grid.GetClosestTriangle(l, new MNM.real_t(vr), new MNM.real_t(hr), ref ds, ref cl); if (ct != 0) { ml = cl.GetVec3(); dSq = ds.as_float(); return ct; } } return 0; }

    // GetGroundLocationInMesh — NavigationSystem.cpp lines 1842-1876
    public bool GetGroundLocationInMesh(NavigationMeshID meshID, Vec3 location, float vDownwardRange, float hRange, out Vec3 meshLocation)
    {
        meshLocation = location;
        if (meshID.id != 0 && m_meshes.validate(meshID.id))
        {
            var loc = new MNM.vector3_t(location);
            var mesh = m_meshes[meshID.id];
            var verticalRange = new MNM.real_t(vDownwardRange);
            uint enclosingTriID = mesh.grid.GetTriangleAt(loc, verticalRange, new MNM.real_t(0.05f));
            if (enclosingTriID != 0)
            {
                MNM.vector3_t v0 = default, v1 = default, v2 = default;
                mesh.grid.GetVertices(enclosingTriID, ref v0, ref v1, ref v2);
                var closest = MNM.MNMUtils.ClosestPtPointTriangle(loc, v0, v1, v2);
                meshLocation = closest.GetVec3();
                return true;
            }
            else
            {
                var dSq = new MNM.real_t(0);
                var closest = new MNM.vector3_t();
                uint closestTriID = mesh.grid.GetClosestTriangle(loc, verticalRange, new MNM.real_t(hRange), ref dSq, ref closest);
                if (closestTriID != 0)
                {
                    meshLocation = closest.GetVec3();
                    return true;
                }
            }
        }
        return false;
    }

    public bool GetClosestPointInNavigationMesh(NavigationAgentTypeID aid, Vec3 loc, float vr, float hr, out Vec3 ml, float minIA = 0) { ml = loc; var mid = GetEnclosingMeshID(aid, loc); if (mid.id != 0 && m_meshes.validate(mid.id)) { var l = new MNM.vector3_t(loc); var m = m_meshes[mid.id]; var r = new MNM.real_t(vr); uint e = m.grid.GetTriangleAt(l, r, r, minIA); if (e != 0) { MNM.vector3_t v0 = default, v1 = default, v2 = default; m.grid.GetVertices(e, ref v0, ref v1, ref v2); ml = MNM.MNMUtils.ClosestPtPointTriangle(l, v0, v1, v2).GetVec3(); return true; } var ds = new MNM.real_t(0); var cl = new MNM.vector3_t(); uint ct = m.grid.GetClosestTriangle(l, new MNM.real_t(vr), new MNM.real_t(hr), ref ds, ref cl, minIA); if (ct != 0) { ml = cl.GetVec3(); return true; } } return false; }
    public bool IsLocationValidInNavigationMesh(NavigationAgentTypeID aid, Vec3 loc) { var mid = GetEnclosingMeshID(aid, loc); if (mid.id != 0) { Vec3 ml; float acc; uint tri = GetClosestMeshLocation(mid, loc, 1, 1, out ml, out acc); return tri != 0 && acc == 0; } return false; }
    public bool IsPointReachableFromPosition(NavigationAgentTypeID aid, IEntity ent, Vec3 start, Vec3 end) { var si = new MNM.GlobalIslandID(); var ei = new MNM.GlobalIslandID(); var sm = GetEnclosingMeshID(aid, start); if (sm.id != 0) { Vec3 d1; float d2; uint tri = GetClosestMeshLocation(sm, start, 1, 1, out d1, out d2); MNM.Tile.Triangle t; if (tri != 0 && m_meshes[sm.id].grid.GetTriangle(tri, out t) && t.islandID != MNM.Constants.eStaticIsland_InvalidIslandID) si = new MNM.GlobalIslandID(sm.id, t.islandID); } var em = GetEnclosingMeshID(aid, end); if (em.id != 0) { Vec3 d1; float d2; uint tri = GetClosestMeshLocation(em, end, 1, 1, out d1, out d2); MNM.Tile.Triangle t; if (tri != 0 && m_meshes[em.id].grid.GetTriangle(tri, out t) && t.islandID != MNM.Constants.eStaticIsland_InvalidIslandID) ei = new MNM.GlobalIslandID(em.id, t.islandID); } return m_islandConnectionsManager.AreIslandsConnected(ent, si, ei); }
    public MNM.GlobalIslandID GetGlobalIslandIdAtPosition(NavigationAgentTypeID aid, Vec3 loc) { var r = new MNM.GlobalIslandID(); var mid = GetEnclosingMeshID(aid, loc); if (mid.id != 0) { Vec3 d1; float d2; uint tri = GetClosestMeshLocation(mid, loc, 1, 1, out d1, out d2); MNM.Tile.Triangle t; if (tri != 0 && m_meshes[mid.id].grid.GetTriangle(tri, out t) && t.islandID != MNM.Constants.eStaticIsland_InvalidIslandID) r = new MNM.GlobalIslandID(mid.id, t.islandID); } return r; }
    public bool IsInUse() => m_meshes.size() != 0;
    public bool IsLocationContainedWithinTriangleInNavigationMesh(NavigationAgentTypeID aid, Vec3 loc, float dr, float ur) { var mid = GetEnclosingMeshID(aid, loc); return mid.id != 0 && m_meshes.validate(mid.id) && m_meshes[mid.id].grid.GetTriangleAt(new MNM.vector3_t(loc), new MNM.real_t(dr), new MNM.real_t(ur)) != 0; }

    // RaycastWorld — NavigationSystem.cpp lines 1727-1765
    public (bool hit, NavigationMeshID meshID, Vec3 point) RaycastWorld(Vec3 segP0, Vec3 segP1)
    {
        // Grab all candidates whose bounding box we intersect
        // note that since agent type is not specified in this api, we need to raycast through all agent types:
        var candidates = new List<uint>();

        foreach (var at in m_agentTypes)
        {
            foreach (var mi in at.meshes)
            {
                if (mi.id.id != 0 && m_meshes.validate(mi.id.id))
                {
                    if (m_volumes[m_meshes[mi.id.id].boundary.id].IntersectLineSeg(segP0, segP1).hit)
                        candidates.Add(mi.id.id);
                }
            }
        }

        bool minHit = false;
        float minTime = float.MaxValue;
        Vec3 minPoint = new Vec3(0, 0, 0);
        uint minMeshID = 0;

        foreach (var meshID in candidates)
        {
            var meshResult = m_meshes[meshID].grid.RayCastWorld(segP0, segP1);
            if (meshResult.hit && meshResult.time < minTime)
            {
                minHit = meshResult.hit;
                minTime = meshResult.time;
                minPoint = meshResult.point;
                minMeshID = meshID;
            }
        }

        return (minHit, new NavigationMeshID { id = minMeshID }, minPoint);
    }

    // GetTriangleCenterLocationsInMesh — NavigationSystem.cpp lines 2425-2459
    public int GetTriangleCenterLocationsInMesh(NavigationMeshID meshID, Vec3 location, AABB searchAABB,
        Vec3[] centerLocations, int maxCenterLocationCount, float minIslandArea = 0f)
    {
        if (m_meshes.validate(meshID.id))
        {
            var min = new MNM.vector3_t(new MNM.real_t(searchAABB.min.x), new MNM.real_t(searchAABB.min.y), new MNM.real_t(searchAABB.min.z));
            var max = new MNM.vector3_t(new MNM.real_t(searchAABB.max.x), new MNM.real_t(searchAABB.max.y), new MNM.real_t(searchAABB.max.z));
            var mesh = m_meshes[meshID.id];
            var aabb = new MNM.aabb_t(min, max);
            const int maxTriangleCount = 4096;
            var triangleIDs = new uint[maxTriangleCount];
            int triangleCount = mesh.grid.GetTriangles(aabb, triangleIDs, maxTriangleCount, minIslandArea);

            if (triangleCount > 0)
            {
                MNM.vector3_t a = default, b = default, c = default;
                int num_tris = 0;
                for (int i = 0; i < triangleCount; i++)
                {
                    mesh.grid.GetVertices(triangleIDs[i], ref a, ref b, ref c);
                    centerLocations[num_tris] = ((a + b + c) * new MNM.real_t(0.33333f)).GetVec3();
                    num_tris++;
                    if (num_tris == maxCenterLocationCount)
                        return num_tris;
                }
                return num_tris;
            }
        }
        return 0;
    }

    // GetTriangleBorders — NavigationSystem.cpp lines 2461-2517
    public int GetTriangleBorders(NavigationMeshID meshID, AABB aabb, Vec3[] pBorders, int maxBorderCount, float minIslandArea = 0f)
    {
        int numBorders = 0;

        if (m_meshes.validate(meshID.id))
        {
            var min = new MNM.vector3_t(new MNM.real_t(aabb.min.x), new MNM.real_t(aabb.min.y), new MNM.real_t(aabb.min.z));
            var max = new MNM.vector3_t(new MNM.real_t(aabb.max.x), new MNM.real_t(aabb.max.y), new MNM.real_t(aabb.max.z));
            var mesh = m_meshes[meshID.id];
            var mnmAabb = new MNM.aabb_t(min, max);
            const int maxTriangleCount = 4096;
            var triangleIDs = new uint[maxTriangleCount];
            int triangleCount = mesh.grid.GetTriangles(mnmAabb, triangleIDs, maxTriangleCount, minIslandArea);

            if (triangleCount > 0)
            {
                MNM.vector3_t[] verts = new MNM.vector3_t[3];

                for (int i = 0; i < triangleCount; i++)
                {
                    int linkedEdges = 0;
                    mesh.grid.GetLinkedEdges(triangleIDs[i], ref linkedEdges);
                    mesh.grid.GetVertices(triangleIDs[i], ref verts[0], ref verts[1], ref verts[2]);

                    for (int e = 0; e < 3; e++)
                    {
                        if ((linkedEdges & (1 << e)) == 0)
                        {
                            if (pBorders != null)
                            {
                                Vec3 v0 = verts[e].GetVec3();
                                Vec3 v1 = verts[(e + 1) % 3].GetVec3();
                                Vec3 vOther = verts[(e + 2) % 3].GetVec3();

                                Vec3 edge = (v0 - v1).GetNormalized();
                                Vec3 otherEdge = (v0 - vOther).GetNormalized();

                                Vec3 up = edge.Cross(otherEdge);
                                Vec3 outDir = up.Cross(edge);

                                pBorders[numBorders * 3 + 0] = v0;
                                pBorders[numBorders * 3 + 1] = v1;
                                pBorders[numBorders * 3 + 2] = outDir;
                            }

                            numBorders++;

                            if (pBorders != null && numBorders == maxBorderCount)
                                return numBorders;
                        }
                    }
                }
            }
        }
        return numBorders;
    }

    // GetTriangleInfo — NavigationSystem.cpp lines 2519-2555
    public int GetTriangleInfo(NavigationMeshID meshID, AABB aabb, Vec3[] centerLocations, uint[] islandids,
        int max_count, float minIslandArea = 0f)
    {
        if (m_meshes.validate(meshID.id))
        {
            var min = new MNM.vector3_t(new MNM.real_t(aabb.min.x), new MNM.real_t(aabb.min.y), new MNM.real_t(aabb.min.z));
            var max = new MNM.vector3_t(new MNM.real_t(aabb.max.x), new MNM.real_t(aabb.max.y), new MNM.real_t(aabb.max.z));
            var mesh = m_meshes[meshID.id];
            var mnmAabb = new MNM.aabb_t(min, max);
            const int maxTriangleCount = 4096;
            var triangleIDs = new uint[maxTriangleCount];
            int triangleCount = mesh.grid.GetTriangles(mnmAabb, triangleIDs, maxTriangleCount, minIslandArea);
            MNM.Tile.Triangle triangle;

            if (triangleCount > 0)
            {
                MNM.vector3_t a = default, b = default, c = default;
                int num_tris = 0;
                for (int i = 0; i < triangleCount; i++)
                {
                    mesh.grid.GetTriangle(triangleIDs[i], out triangle);
                    mesh.grid.GetVertices(triangleIDs[i], ref a, ref b, ref c);
                    centerLocations[num_tris] = ((a + b + c) * new MNM.real_t(0.33333f)).GetVec3();
                    islandids[num_tris] = triangle.islandID;
                    num_tris++;
                    if (num_tris == max_count)
                        return num_tris;
                }
                return num_tris;
            }
        }
        return 0;
    }

    // GetTileIdWhereLocationIsAtForMesh — NavigationSystem.cpp lines 1688-1696
    public uint GetTileIdWhereLocationIsAtForMesh(NavigationMeshID meshID, Vec3 location)
    {
        var mesh = GetMesh(meshID);
        var range = new MNM.real_t(1.0f);
        uint triangleID = mesh.grid.GetTriangleAt(new MNM.vector3_t(location), range, range);
        return MNM.MNMUtils.ComputeTileID(triangleID);
    }

    // GetTileBoundsForMesh — NavigationSystem.cpp lines 1698-1709
    public void GetTileBoundsForMesh(NavigationMeshID meshID, uint tileID, out AABB bounds)
    {
        var mesh = GetMesh(meshID);
        var coords = mesh.grid.GetTileContainerCoordinates(tileID);
        var pars = mesh.grid.GetParams();

        Vec3 minPos = new Vec3(
            pars.tileSize.x * coords.x.as_float(),
            pars.tileSize.y * coords.y.as_float(),
            pars.tileSize.z * coords.z.as_float());
        minPos = minPos + pars.origin;

        bounds = new AABB(minPos, minPos + new Vec3(pars.tileSize.x, pars.tileSize.y, pars.tileSize.z));
    }

    // GetTriangleIDWhereLocationIsAtForMesh — NavigationSystem.cpp lines 2397-2423
    public uint GetTriangleIDWhereLocationIsAtForMesh(NavigationAgentTypeID agentID, Vec3 location)
    {
        var meshId = GetEnclosingMeshID(agentID, location);
        if (meshId.id != 0)
        {
            var mesh = GetMesh(meshId);
            var paramsGrid = mesh.grid.GetParams();
            var voxelSize = paramsGrid.voxelSize;
            ushort agentHeightUnits = GetAgentHeightInVoxelUnits(agentID);

            var verticalRange = MNM.MNMUtils.CalculateMinVerticalRange(agentHeightUnits, voxelSize.z);
            var verticalDownwardRange = verticalRange;

            AgentType agentTypeProperties;
            bool arePropertiesValid = GetAgentTypeProperties(agentID, out agentTypeProperties);
            Debug.Assert(arePropertiesValid);
            ushort minZOffsetMultiplier = 2;
            ushort zOffsetMultiplier = Math.Min(minZOffsetMultiplier, agentTypeProperties.settings.heightVoxelCount);
            var verticalUpwardRange = arePropertiesValid
                ? new MNM.real_t(zOffsetMultiplier * agentTypeProperties.settings.voxelSize.z)
                : new MNM.real_t(0.2f);

            var locOffset = location - paramsGrid.origin;
            return mesh.grid.GetTriangleAt(new MNM.vector3_t(locOffset), verticalDownwardRange, verticalUpwardRange);
        }
        return 0;
    }

    // Callbacks / Listeners
    public void SetMeshEntityCallback(NavigationAgentTypeID id, Func<IPhysicalEntity, uint, bool> cb) { if (id.id != 0 && id.id <= (uint)m_agentTypes.Count) m_agentTypes[(int)(id.id - 1)].meshEntityCallback = cb; }
    public void AddMeshChangeCallback(NavigationAgentTypeID id, Action<NavigationAgentTypeID, NavigationMeshID, uint> cb) { if (id.id != 0 && id.id <= (uint)m_agentTypes.Count) { var at = m_agentTypes[(int)(id.id - 1)]; if (!at.callbacks.Contains(cb)) at.callbacks.Add(cb); } }
    public void RemoveMeshChangeCallback(NavigationAgentTypeID id, Action<NavigationAgentTypeID, NavigationMeshID, uint> cb) { if (id.id != 0 && id.id <= (uint)m_agentTypes.Count) m_agentTypes[(int)(id.id - 1)].callbacks.Remove(cb); }
    public void RegisterListener(INavigationSystem.INavigationSystemListener l, string n = null) { if (!m_listenersList.Contains(l)) m_listenersList.Add(l); }
    public void UnRegisterListener(INavigationSystem.INavigationSystemListener l) => m_listenersList.Remove(l);
    public void RegisterUser(INavigationSystemUser u, string n = null) { if (!m_users.Contains(u)) m_users.Add(u); }
    public void UnRegisterUser(INavigationSystemUser u) => m_users.Remove(u);

    // Area registration
    public void RegisterArea(string s) => m_volumesManager.RegisterArea(s);
    public void UnRegisterArea(string s) => m_volumesManager.UnRegisterArea(s);
    public NavigationVolumeID GetAreaId(string s) => m_volumesManager.GetAreaID(s);
    public void SetAreaId(string s, NavigationVolumeID id) => m_volumesManager.SetAreaID(s, id);
    public void UpdateAreaNameForId(NavigationVolumeID id, string n) => m_volumesManager.UpdateNameForAreaID(id, n);

    // World Monitoring / Sub-managers
    public void StartWorldMonitoring() => m_worldMonitor.Start();
    public void StopWorldMonitoring() => m_worldMonitor.Stop();
    public WorldMonitor GetWorldMonitor() => m_worldMonitor;
    public OffMeshNavigationManager GetOffMeshNavigationManager() => m_offMeshNavigationManager;
    public IslandConnectionsManager GetIslandConnectionsManager() => m_islandConnectionsManager;
    public IOffMeshNavigationManager GetIOffMeshNavigationManager() => m_offMeshNavigationManager;

    // Clear / Reset — NavigationSystem.cpp lines 1920-1969
    public void Clear()
    {
        StopAllTasks();
        SetupTasks();

        foreach (var at in m_agentTypes) { at.meshes.Clear(); at.exclusions.Clear(); }

        for (int i = 0; i < m_meshes.capacity(); i++)
            if (!m_meshes.index_free(i))
                DestroyMesh(new NavigationMeshID { id = m_meshes.get_index_id(i) });

        for (int i = 0; i < m_volumes.capacity(); i++)
            if (!m_volumes.index_free(i))
                DestroyVolume(new NavigationVolumeID(m_volumes.get_index_id(i)));

        m_worldAABB = new AABB(AABB.RESET);
        m_tileQueue.Clear();

        m_volumeDefCopy.Clear();
        for (int i = 0; i < MaxVolumeDefCopyCount; i++) m_volumeDefCopy.Add(new VolumeDefCopy());

        m_offMeshNavigationManager.Clear();
        m_islandConnectionsManager.Reset();
        ResetAll();
    }

    public void ClearAndNotify() { Clear(); NotifyAll(INavigationSystem.ENavigationEvent.NavigationCleared); m_offMeshNavigationManager.Enable(); gAIEnv.pSmartObjectManager?.SoftReset(); }
    public bool ReloadConfig() => true;

    // DebugDraw — NavigationSystem.cpp lines 3150-3153
    // Debug draw requires render context; no-op in C# port
    public void DebugDraw() { }
    public void Reset() => ResetAll();
    public void SetDebugDisplayAgentType(NavigationAgentTypeID id) { }
    public NavigationAgentTypeID GetDebugDisplayAgentType() => new();

    // CalculateAccessibility / ComputeAccessibility — NavigationSystem.cpp lines 1542-1640
    // These depend on MNM_USE_EXPORT_INFORMATION which is compile-time-conditional
    // and require ComputeAccessibility on MeshGrid which is not yet ported.
    // Kept as no-op.
    public void CalculateAccessibility() { }
    public void ComputeAccessibility(Vec3 p, NavigationAgentTypeID a = default, float r = 0, INavigationSystem.EAccessbilityDir d = default) { }

    // ReadFromFile — NavigationSystem.cpp lines 2557-2843
    public bool ReadFromFile(string fileName, bool bAfterExporting)
    {
        bool fileLoaded = false;

        try
        {
            if (!File.Exists(fileName))
                return false;

            using var stream = File.OpenRead(fileName);
            using var reader = new BinaryReader(stream);

            bool fileVersionCompatible = true;

            ushort nFileVersion = reader.ReadUInt16();
            if (nFileVersion != BAI_NAVIGATION_FILE_VERSION)
            {
                AILog.AIWarning("Wrong BAI file version (found {0} expected {1})!! Regenerate Navigation data in the editor.",
                    nFileVersion, BAI_NAVIGATION_FILE_VERSION);
                fileVersionCompatible = false;
            }
            else
            {
                uint nConfigurationVersion = reader.ReadUInt32();
                if (nConfigurationVersion != m_configurationVersion)
                {
                    AILog.AIWarning("Navigation.xml config version mismatch (found {0} expected {1})!! Regenerate Navigation data in the editor.",
                        nConfigurationVersion, m_configurationVersion);
                    if (gEnv.IsEditor())
                        fileVersionCompatible = false;
                }
            }

            if (fileVersionCompatible)
            {
                // Reading areas Names/ID
                uint areasCount = reader.ReadUInt32();
                for (uint i = 0; i < areasCount; i++)
                {
                    uint areaNameLength = reader.ReadUInt32();
                    areaNameLength = Math.Min(areaNameLength, (uint)MAX_NAME_LENGTH - 1);
                    byte[] nameBytes = reader.ReadBytes((int)areaNameLength);
                    string areaName = System.Text.Encoding.ASCII.GetString(nameBytes);
                    uint areaIDUint32 = reader.ReadUInt32();

                    if (gEnv.IsEditor())
                    {
                        m_volumesManager.SetAreaID(areaName, new NavigationVolumeID(areaIDUint32));
                    }
                }

                uint agentsCount = reader.ReadUInt32();
                for (uint i = 0; i < agentsCount; i++)
                {
                    uint nameLength = reader.ReadUInt32();
                    nameLength = Math.Min(nameLength, (uint)MAX_NAME_LENGTH - 1);
                    byte[] nameBytes = reader.ReadBytes((int)nameLength);
                    string agentName = System.Text.Encoding.ASCII.GetString(nameBytes);

                    // Reading total amount of memory used for the current agent
                    uint totalAgentMemory = reader.ReadUInt32();
                    long fileSeekPositionForNextAgent = stream.Position + totalAgentMemory;

                    var agentTypeID = GetAgentTypeID(agentName);
                    if (agentTypeID.id == 0)
                    {
                        AILog.AIWarning("The agent '{0}' doesn't exist between the ones loaded from the Navigation.xml", agentName);
                        stream.Seek(fileSeekPositionForNextAgent, SeekOrigin.Begin);
                        continue;
                    }

                    // Reading navmesh for the different agents type
                    uint meshesCount = reader.ReadUInt32();
                    for (uint meshCounter = 0; meshCounter < meshesCount; meshCounter++)
                    {
                        // Reading mesh id
                        uint meshIDuint32 = reader.ReadUInt32();

                        // Reading mesh name
                        uint meshNameLength = reader.ReadUInt32();
                        meshNameLength = Math.Min(meshNameLength, (uint)MAX_NAME_LENGTH - 1);
                        byte[] meshNameBytes = reader.ReadBytes((int)meshNameLength);
                        string meshName = System.Text.Encoding.ASCII.GetString(meshNameBytes);

                        // Reading the amount of islands in the mesh
                        uint totalIslands = reader.ReadUInt32();

                        // Reading total mesh memory
                        uint totalMeshMemory = reader.ReadUInt32();
                        long fileSeekPositionForNextMesh = stream.Position + totalMeshMemory;

                        if (gEnv.IsEditor() && !m_volumesManager.IsAreaPresent(meshName))
                        {
                            stream.Seek(fileSeekPositionForNextMesh, SeekOrigin.Begin);
                            continue;
                        }

                        // Reading mesh boundary
                        uint boundaryIDuint32 = reader.ReadUInt32();
                        var boundaryID = new NavigationVolumeID(boundaryIDuint32);

                        // Saving the volume used by the boundary
                        float volumeHeight = reader.ReadSingle();
                        uint totalVertices = reader.ReadUInt32();
                        var boundaryVertexBuffer = new Vec3[totalVertices];
                        for (uint vc = 0; vc < totalVertices; vc++)
                        {
                            float vx = reader.ReadSingle();
                            float vy = reader.ReadSingle();
                            float vz = reader.ReadSingle();
                            boundaryVertexBuffer[vc] = new Vec3(vx, vy, vz);
                        }

                        if (!m_volumes.validate(boundaryID.id))
                        {
                            CreateVolume(boundaryVertexBuffer, (int)totalVertices, volumeHeight, boundaryID);
                        }

                        // Reading mesh exclusion shapes
                        uint exclusionShapesCount = reader.ReadUInt32();
                        var exclusions = new List<NavigationVolumeID>();
                        for (uint ec = 0; ec < exclusionShapesCount; ec++)
                        {
                            uint exclusionIDuint32 = reader.ReadUInt32();
                            exclusions.Add(new NavigationVolumeID(exclusionIDuint32));
                        }

                        // Reading tile count
                        uint tilesCount = reader.ReadUInt32();

                        // Reading mesh params
                        var meshParams = new MNM.MeshGrid.Params();
                        meshParams.origin.x = reader.ReadSingle();
                        meshParams.origin.y = reader.ReadSingle();
                        meshParams.origin.z = reader.ReadSingle();
                        meshParams.tileSize.x = reader.ReadInt32();
                        meshParams.tileSize.y = reader.ReadInt32();
                        meshParams.tileSize.z = reader.ReadInt32();
                        meshParams.voxelSize.x = reader.ReadSingle();
                        meshParams.voxelSize.y = reader.ReadSingle();
                        meshParams.voxelSize.z = reader.ReadSingle();
                        meshParams.tileCount = reader.ReadUInt32();

                        var createParams = new INavigationSystem.CreateMeshParams();
                        createParams.origin = meshParams.origin;
                        createParams.tileSize = new Vec3(meshParams.tileSize.x, meshParams.tileSize.y, meshParams.tileSize.z);
                        createParams.tileCount = tilesCount;

                        var newMeshID = new NavigationMeshID { id = meshIDuint32 };
                        if (!m_meshes.validate(meshIDuint32))
                            newMeshID = CreateMesh(meshName, agentTypeID, createParams, newMeshID);

                        if (newMeshID.id == 0)
                        {
                            AILog.AIWarning("Unable to create mesh '{0}'", meshName);
                            stream.Seek(fileSeekPositionForNextMesh, SeekOrigin.Begin);
                            continue;
                        }

                        if (newMeshID.id != meshIDuint32)
                        {
                            AILog.AIWarning("The restored mesh has a different ID compared to the saved one.");
                        }

                        var mesh = m_meshes[newMeshID.id];
                        SetMeshBoundaryVolume(newMeshID, boundaryID);

                        mesh.exclusions = exclusions;
                        mesh.grid.SetTotalIslands(totalIslands);

                        for (uint j = 0; j < tilesCount; j++)
                        {
                            // Reading Tile indexes
                            ushort tx = reader.ReadUInt16();
                            ushort ty = reader.ReadUInt16();
                            ushort tz = reader.ReadUInt16();
                            uint hashValue = reader.ReadUInt32();

                            // Reading triangles
                            ushort triangleCount = reader.ReadUInt16();
                            MNM.Tile.Triangle[] pTriangles = null;
                            if (triangleCount > 0)
                            {
                                pTriangles = new MNM.Tile.Triangle[triangleCount];
                                for (int ti = 0; ti < triangleCount; ti++)
                                    pTriangles[ti] = MNM.Tile.Triangle.Read(reader);
                            }

                            // Reading Vertices
                            ushort vertexCount = reader.ReadUInt16();
                            MNM.Tile.Vertex[] pVertices = null;
                            if (vertexCount > 0)
                            {
                                pVertices = new MNM.Tile.Vertex[vertexCount];
                                for (int vi = 0; vi < vertexCount; vi++)
                                    pVertices[vi] = MNM.Tile.Vertex.Read(reader);
                            }

                            // Reading Links
                            ushort linkCount = reader.ReadUInt16();
                            MNM.Tile.Link[] pLinks = null;
                            if (linkCount > 0)
                            {
                                pLinks = new MNM.Tile.Link[linkCount];
                                for (int li = 0; li < linkCount; li++)
                                    pLinks[li] = MNM.Tile.Link.Read(reader);
                            }

                            // Reading nodes
                            ushort nodeCount = reader.ReadUInt16();
                            MNM.Tile.BVNode[] pNodes = null;
                            if (nodeCount > 0)
                            {
                                pNodes = new MNM.Tile.BVNode[nodeCount];
                                for (int ni = 0; ni < nodeCount; ni++)
                                    pNodes[ni] = MNM.Tile.BVNode.Read(reader);
                            }

                            // Creating and swapping the tile
                            var tile = new MNM.Tile();
                            tile.triangleCount = triangleCount;
                            tile.triangles = pTriangles;
                            tile.vertexCount = vertexCount;
                            tile.vertices = pVertices;
                            tile.linkCount = linkCount;
                            tile.links = pLinks;
                            tile.nodeCount = nodeCount;
                            tile.nodes = pNodes;
                            tile.hashValue = hashValue;
                            mesh.grid.SetTile(tx, ty, tz, tile);
                        }
                    }
                }

                fileLoaded = true;
            }
        }
        catch (Exception ex)
        {
            AILog.AIWarning("Failed to read navigation file '{0}': {1}", fileName, ex.Message);
        }

        var navigationEvent = bAfterExporting
            ? INavigationSystem.ENavigationEvent.MeshReloadedAfterExporting
            : INavigationSystem.ENavigationEvent.MeshReloaded;
        NotifyAll(navigationEvent);

        m_offMeshNavigationManager.OnNavigationLoadedComplete();

        return fileLoaded;
    }

    // FilterOffMeshLinksForTile — NavigationSystem.cpp lines 2850-2911
    private static ushort FilterOffMeshLinksForTile(MNM.Tile tile,
        MNM.Tile.Triangle[] pTrianglesBuffer, ushort trianglesBufferSize,
        MNM.Tile.Link[] pLinksBuffer, ushort linksBufferSize)
    {
        Debug.Assert(pTrianglesBuffer != null);
        Debug.Assert(pLinksBuffer != null);
        Debug.Assert(tile.triangleCount <= trianglesBufferSize);
        Debug.Assert(tile.linkCount <= linksBufferSize);

        ushort newLinkCount = 0;
        ushort offMeshLinksCount = 0;

        if (tile.links != null)
        {
            // Re-adjust link indices for triangles
            for (ushort t = 0; t < tile.triangleCount; t++)
            {
                var triangle = tile.triangles[t];
                pTrianglesBuffer[t] = triangle;

                pTrianglesBuffer[t].firstLink = (ushort)(triangle.firstLink - offMeshLinksCount);

                if (triangle.linkCount > 0 && tile.links[triangle.firstLink].side == MNM.Tile.Link.OffMesh)
                {
                    pTrianglesBuffer[t].linkCount--;
                    offMeshLinksCount++;
                }
            }

            // Now copy links except off-mesh ones
            for (ushort l = 0; l < tile.linkCount; l++)
            {
                var link = tile.links[l];
                if (link.side != MNM.Tile.Link.OffMesh)
                {
                    pLinksBuffer[newLinkCount] = link;
                    newLinkCount++;
                }
            }
        }
        else
        {
            // Just copy the triangles as they are
            if (tile.triangles != null)
                Array.Copy(tile.triangles, pTrianglesBuffer, tile.triangleCount);
        }

        Debug.Assert(newLinkCount == (tile.linkCount - offMeshLinksCount));
        return newLinkCount;
    }

    // SaveToFile — NavigationSystem.cpp lines 2913-3140
    public bool SaveToFile(string fileName)
    {
        try
        {
            const int maxTriangles = 1024;
            const int maxLinks = maxTriangles * 6;
            var triangleBuffer = new MNM.Tile.Triangle[maxTriangles];
            var linkBuffer = new MNM.Tile.Link[maxLinks];

            using var stream = File.Create(fileName);
            using var writer = new BinaryWriter(stream);

            // Saving file data version
            writer.Write((ushort)BAI_NAVIGATION_FILE_VERSION);
            writer.Write(m_configurationVersion);

            // Saving areas Names/ID
            var areas = new List<string>();
            m_volumesManager.GetVolumesNames(areas);
            writer.Write((uint)areas.Count);
            foreach (var area in areas)
            {
                uint areaIDUint32 = m_volumesManager.GetAreaID(area).id;
                uint areaNameLength = (uint)Math.Min(area.Length, MAX_NAME_LENGTH - 1);
                writer.Write(areaNameLength);
                writer.Write(System.Text.Encoding.ASCII.GetBytes(area), 0, (int)areaNameLength);
                writer.Write(areaIDUint32);
            }

            // Saving number of agents
            uint agentsCount = (uint)GetAgentTypeCount();
            writer.Write(agentsCount);

            foreach (var agentType in m_agentTypes)
            {
                uint nameLength = (uint)Math.Min(agentType.name.Length, MAX_NAME_LENGTH - 1);
                writer.Write(nameLength);
                writer.Write(System.Text.Encoding.ASCII.GetBytes(agentType.name), 0, (int)nameLength);

                // Saving the amount of memory this agent is using
                uint totalAgentMemory = 0;
                long totalAgentMemoryPositionInFile = stream.Position;
                writer.Write(totalAgentMemory);

                uint meshesCount = (uint)agentType.meshes.Count;
                writer.Write(meshesCount);

                foreach (var mi in agentType.meshes)
                {
                    uint meshIDuint32 = mi.id.id;
                    var mesh = m_meshes[new NavigationMeshID { id = meshIDuint32 }.id];
                    var volume = m_volumes[mesh.boundary.id];
                    var grid = mesh.grid;

                    // Saving mesh id
                    writer.Write(meshIDuint32);

                    // Saving mesh name
                    uint meshNameLength = (uint)Math.Min(mesh.name.Length, MAX_NAME_LENGTH - 1);
                    writer.Write(meshNameLength);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes(mesh.name), 0, (int)meshNameLength);

                    // Saving total islands
                    uint totalIslands = mesh.grid.GetTotalIslands();
                    writer.Write(totalIslands);

                    uint totalMeshMemory = 0;
                    long totalMeshMemoryPositionInFile = stream.Position;
                    writer.Write(totalMeshMemory);

                    // Saving mesh boundary id
                    uint boundaryIDuint32 = mesh.boundary.id;
                    writer.Write(boundaryIDuint32);

                    // Saving the volume used by the boundary
                    writer.Write(volume.height);
                    uint totalVertices = (uint)volume.vertices.Count;
                    writer.Write(totalVertices);
                    foreach (var vertex in volume.vertices)
                    {
                        writer.Write(vertex.x);
                        writer.Write(vertex.y);
                        writer.Write(vertex.z);
                    }

                    // Saving mesh exclusion shapes
                    uint exclusionShapesCount = (uint)mesh.exclusions.Count;
                    writer.Write(exclusionShapesCount);
                    for (int ec = 0; ec < exclusionShapesCount; ec++)
                    {
                        writer.Write(mesh.exclusions[ec].id);
                    }

                    // Saving tiles count
                    uint tileCount = (uint)grid.GetTileCount();
                    writer.Write(tileCount);

                    // Saving grid params
                    var paramsGrid = grid.GetParams();
                    writer.Write(paramsGrid.origin.x);
                    writer.Write(paramsGrid.origin.y);
                    writer.Write(paramsGrid.origin.z);
                    writer.Write(paramsGrid.tileSize.x);
                    writer.Write(paramsGrid.tileSize.y);
                    writer.Write(paramsGrid.tileSize.z);
                    writer.Write(paramsGrid.voxelSize.x);
                    writer.Write(paramsGrid.voxelSize.y);
                    writer.Write(paramsGrid.voxelSize.z);
                    writer.Write(paramsGrid.tileCount);

                    var boundary_aabb = m_volumes[mesh.boundary.id].aabb;

                    Vec3 bmin = new Vec3(
                        MathF.Max(0.0f, boundary_aabb.min.x - paramsGrid.origin.x),
                        MathF.Max(0.0f, boundary_aabb.min.y - paramsGrid.origin.y),
                        MathF.Max(0.0f, boundary_aabb.min.z - paramsGrid.origin.z));
                    Vec3 bmax = new Vec3(
                        MathF.Max(0.0f, boundary_aabb.max.x - paramsGrid.origin.x),
                        MathF.Max(0.0f, boundary_aabb.max.y - paramsGrid.origin.y),
                        MathF.Max(0.0f, boundary_aabb.max.z - paramsGrid.origin.z));

                    ushort xmin = (ushort)MathF.Floor(bmin.x / paramsGrid.tileSize.x);
                    ushort xmax = (ushort)MathF.Floor(bmax.x / paramsGrid.tileSize.x);
                    ushort ymin = (ushort)MathF.Floor(bmin.y / paramsGrid.tileSize.y);
                    ushort ymax = (ushort)MathF.Floor(bmax.y / paramsGrid.tileSize.y);
                    ushort zmin = (ushort)MathF.Floor(bmin.z / paramsGrid.tileSize.z);
                    ushort zmax = (ushort)MathF.Floor(bmax.z / paramsGrid.tileSize.z);

                    for (ushort x = xmin; x <= xmax; x++)
                    {
                        for (ushort y = ymin; y <= ymax; y++)
                        {
                            for (ushort z = zmin; z <= zmax; z++)
                            {
                                uint tileID = grid.GetTileID(x, y, z);
                                if (tileID == 0)
                                    continue;

                                // Saving tile indexes
                                writer.Write(x);
                                writer.Write(y);
                                writer.Write(z);
                                var tile = grid.GetTile(tileID);
                                writer.Write(tile.hashValue);

                                ushort saveLinkCount = FilterOffMeshLinksForTile(tile, triangleBuffer, maxTriangles, linkBuffer, (ushort)maxLinks);

                                // Saving triangles
                                writer.Write(tile.triangleCount);
                                for (int ti = 0; ti < tile.triangleCount; ti++)
                                    triangleBuffer[ti].Write(writer);

                                // Saving vertices
                                writer.Write(tile.vertexCount);
                                if (tile.vertices != null)
                                    for (int vi = 0; vi < tile.vertexCount; vi++)
                                        tile.vertices[vi].Write(writer);

                                // Saving links
                                writer.Write(saveLinkCount);
                                for (int li = 0; li < saveLinkCount; li++)
                                    linkBuffer[li].Write(writer);

                                // Saving nodes
                                writer.Write(tile.nodeCount);
                                if (tile.nodes != null)
                                    for (int ni = 0; ni < tile.nodeCount; ni++)
                                        tile.nodes[ni].Write(writer);
                            }
                        }
                    }

                    long endingMeshDataPosition = stream.Position;
                    totalMeshMemory = (uint)(endingMeshDataPosition - totalMeshMemoryPositionInFile - sizeof(uint));
                    stream.Seek(totalMeshMemoryPositionInFile, SeekOrigin.Begin);
                    writer.Write(totalMeshMemory);
                    stream.Seek(endingMeshDataPosition, SeekOrigin.Begin);
                }

                long endingAgentDataPosition = stream.Position;
                totalAgentMemory = (uint)(endingAgentDataPosition - totalAgentMemoryPositionInFile - sizeof(uint));
                stream.Seek(totalAgentMemoryPositionInFile, SeekOrigin.Begin);
                writer.Write(totalAgentMemory);
                stream.Seek(endingAgentDataPosition, SeekOrigin.Begin);
            }
        }
        catch (Exception ex)
        {
            AILog.AIWarning("Failed to save navigation file '{0}': {1}", fileName, ex.Message);
            return false;
        }

        return true;
    }

    // ========================================================================
    // Mesh Update Pipeline (private)
    // ========================================================================

    // UpdateMeshes — NavigationSystem.cpp lines 827-995
    private void UpdateMeshes(float frameTime, bool blocking, bool multiThreaded, bool bBackground)
    {
        if (m_isNavigationUpdatePaused || frameTime == 0.0f)
            return;

        if (m_tileQueue.Count == 0 && m_runningTasks.Count == 0)
        {
            if (m_state != INavigationSystem.WorkingState.Idle)
            {
                // We just finished the processing of the tiles, so before being in Idle
                // we need to recompute the Islands detection
                ComputeIslands();
            }
            m_state = INavigationSystem.WorkingState.Idle;
            return;
        }

        int completed = 0;
        int cacheHit = 0;

        while (true)
        {
            if (m_runningTasks.Count > 0)
            {
                for (int i = 0; i < m_runningTasks.Count; )
                {
                    int resultSlot = m_runningTasks[i];
                    Debug.Assert(resultSlot < m_results.Count);

                    var result = m_results[resultSlot];
                    if (result.state == TileTaskResult.Running)
                    {
                        i++;
                        continue;
                    }

                    CommitTile(result);

                    completed++;
                    cacheHit += result.state == TileTaskResult.NoChanges ? 1 : 0;

                    new MNM.Tile().Swap(result.tile);

                    var def = m_volumeDefCopy[result.volumeCopy];
                    def.refCount--;

                    result.state = TileTaskResult.Running;
                    result.next = m_free;
                    m_free = (ushort)resultSlot;

                    bool reachedLast = (i == m_runningTasks.Count - 1);

                    // swap-and-pop
                    m_runningTasks[i] = m_runningTasks[^1];
                    m_runningTasks.RemoveAt(m_runningTasks.Count - 1);

                    if (reachedLast)
                        break;
                }
            }

            m_throughput = completed / frameTime;
            m_cacheHitRate = cacheHit / frameTime;

            if (m_tileQueue.Count == 0 && m_runningTasks.Count == 0)
            {
                if (m_state != INavigationSystem.WorkingState.Idle)
                {
                    ComputeIslands();
                }
                m_state = INavigationSystem.WorkingState.Idle;
                return;
            }

            if (m_tileQueue.Count > 0)
            {
                m_state = INavigationSystem.WorkingState.Working;

                int idealMinimumTaskCount = 2;
                int MaxRunningTaskCount = multiThreaded ? m_maxRunningTaskCount : Math.Min(m_maxRunningTaskCount, idealMinimumTaskCount);

                while (m_tileQueue.Count > 0 && m_runningTasks.Count < MaxRunningTaskCount)
                {
                    var task = m_tileQueue.First.Value;
                    m_tileQueue.RemoveFirst();

                    if (task.aborted)
                        continue;

                    var mesh = m_meshes[task.meshID.id];
                    var paramsGrid = mesh.grid.GetParams();

                    m_runningTasks.Add(m_free);
                    Debug.Assert(m_free < m_results.Count, "Index out of array bounds!");
                    var result = m_results[m_free];
                    m_free = result.next;

                    if (!SpawnJob(result, task.meshID, paramsGrid, task.x, task.y, task.z, multiThreaded))
                    {
                        result.state = TileTaskResult.Running;
                        result.next = m_free;
                        m_free = (ushort)m_runningTasks[^1];
                        m_runningTasks.RemoveAt(m_runningTasks.Count - 1);
                        break;
                    }
                }

                // keep main thread busy too if we're blocking
                if (blocking && m_tileQueue.Count > 0 && multiThreaded)
                {
                    while (m_tileQueue.Count > 0)
                    {
                        var task = m_tileQueue.First.Value;
                        m_tileQueue.RemoveFirst();

                        if (task.aborted)
                            continue;

                        var mesh = m_meshes[task.meshID.id];
                        var paramsGrid = mesh.grid.GetParams();

                        var result = new TileTaskResult();
                        if (!SpawnJob(result, task.meshID, paramsGrid, task.x, task.y, task.z, false))
                            break;

                        CommitTile(result);
                        break;
                    }
                }
            }

            if (blocking && (m_tileQueue.Count > 0 || m_runningTasks.Count > 0))
                continue;

            m_state = m_runningTasks.Count == 0 ? INavigationSystem.WorkingState.Idle : INavigationSystem.WorkingState.Working;
            return;
        }
    }

    // SetupGenerator — NavigationSystem.cpp lines 997-1027
    private void SetupGenerator(NavigationMeshID meshID, MNM.MeshGrid.Params paramsGrid,
        ushort x, ushort y, ushort z, MNM.TileGenerator.Params @params,
        MNM.BoundingVolume boundary, MNM.BoundingVolume[] exclusions,
        int exclusionCount)
    {
        var mesh = m_meshes[meshID.id];

        @params.origin = paramsGrid.origin + new Vec3(
            x * paramsGrid.tileSize.x,
            y * paramsGrid.tileSize.y,
            z * paramsGrid.tileSize.z);
        @params.voxelSize = paramsGrid.voxelSize;
        @params.sizeX = (byte)paramsGrid.tileSize.x;
        @params.sizeY = (byte)paramsGrid.tileSize.y;
        @params.sizeZ = (byte)paramsGrid.tileSize.z;
        @params.boundary = boundary;
        @params.exclusions = exclusions;
        @params.exclusionCount = (ushort)exclusionCount;

        var agentType = m_agentTypes[(int)(mesh.agentTypeID.id - 1)];

        @params.agent.radius = agentType.settings.radiusVoxelCount;
        @params.agent.height = agentType.settings.heightVoxelCount;
        @params.agent.climbableHeight = agentType.settings.climbableVoxelCount;
        @params.agent.maxWaterDepth = agentType.settings.maxWaterDepthVoxelCount;
        @params.climbableInclineGradient = agentType.settings.climbableInclineGradient;
        @params.climbableStepRatio = agentType.settings.climbableStepRatio;
        // agent.callback not set — requires WorldVoxelizer.NavigationMeshEntityCallback which is a different delegate type

        uint tileID = mesh.grid.GetTileID(x, y, z);
        if (tileID != 0)
            @params.hashValue = mesh.grid.GetTile(tileID).hashValue;
        else
            @params.flags |= (ushort)MNM.TileGenerator.Params.Flags.NoHashTest;
    }

    // SpawnJob — NavigationSystem.cpp lines 1029-1124
    // In C# we run tile generation synchronously (no job system).
    private bool SpawnJob(TileTaskResult result, NavigationMeshID meshID, MNM.MeshGrid.Params paramsGrid,
        ushort x, ushort y, ushort z, bool mt)
    {
        result.x = x;
        result.y = y;
        result.z = z;
        result.meshID = meshID;
        result.state = TileTaskResult.Running;
        result.hashValue = 0;

        var mesh = m_meshes[meshID.id];

        int firstFree = -1;
        int index = -1;

        for (int i = 0; i < MaxVolumeDefCopyCount; i++)
        {
            var def = m_volumeDefCopy[i];
            if (def.meshID.id == meshID.id && def.version == mesh.version)
            {
                index = i;
                break;
            }
            else if (firstFree == -1 && def.refCount == 0)
                firstFree = i;
        }

        if (index == -1 && firstFree == -1)
            return false;

        VolumeDefCopy volDef;

        if (index != -1)
        {
            volDef = m_volumeDefCopy[index];
        }
        else
        {
            index = firstFree;
            volDef = m_volumeDefCopy[index];
            volDef.meshID = meshID;
            volDef.version = mesh.version;
            volDef.boundary = m_volumes[mesh.boundary.id];
            volDef.exclusions.Clear();

            foreach (var exclVolumeID in mesh.exclusions)
            {
                if (m_volumes.validate(exclVolumeID.id))
                {
                    volDef.exclusions.Add(m_volumes[exclVolumeID.id]);
                }
            }
        }

        result.volumeCopy = (ushort)index;
        volDef.refCount++;

        var genParams = new MNM.TileGenerator.Params();
        SetupGenerator(meshID, paramsGrid, x, y, z, genParams, volDef.boundary,
            volDef.exclusions.Count == 0 ? null : volDef.exclusions.ToArray(), volDef.exclusions.Count);

        // Run tile generation synchronously (no CryJobManager in C# port)
        GenerateTileJob(genParams, result);

        return true;
    }

    // GenerateTileJob — NavigationSystem.cpp lines 63-77
    private static void GenerateTileJob(MNM.TileGenerator.Params genParams, TileTaskResult result)
    {
        if (result.state != TileTaskResult.Failed)
        {
            var generator = new MNM.TileGenerator();
            uint[] hashOut = new uint[1];
            bool genResult = generator.Generate(genParams, result.tile, hashOut);
            result.hashValue = hashOut[0];
            if (genResult)
                result.state = TileTaskResult.Completed;
            else if (((genParams.flags & (ushort)MNM.TileGenerator.Params.Flags.NoHashTest) == 0) && (hashOut[0] == genParams.hashValue))
                result.state = TileTaskResult.NoChanges;
            else
                result.state = TileTaskResult.Failed;
        }
    }

    // CommitTile — NavigationSystem.cpp lines 1126-1189
    private void CommitTile(TileTaskResult result)
    {
        // The mesh for this tile has been destroyed, it doesn't make sense to commit the tile
        if (!m_meshes.validate(result.meshID.id))
            return;

        var mesh = m_meshes[result.meshID.id];

        switch (result.state)
        {
            case TileTaskResult.Completed:
            {
                uint tileID = mesh.grid.SetTile(result.x, result.y, result.z, result.tile);
                mesh.grid.ConnectToNetwork(tileID);

                m_offMeshNavigationManager.RefreshConnections(result.meshID, tileID);
                gAIEnv.pMNMPathfinder?.OnNavigationMeshChanged(result.meshID, new MNM_TileID { id = tileID });

                var agentType = m_agentTypes[(int)(mesh.agentTypeID.id - 1)];
                foreach (var callback in agentType.callbacks)
                {
                    callback(mesh.agentTypeID, result.meshID, tileID);
                }
            }
            break;

            case TileTaskResult.Failed:
            {
                uint tileID = mesh.grid.GetTileID(result.x, result.y, result.z);
                if (tileID != 0)
                {
                    mesh.grid.ClearTile(tileID);

                    m_offMeshNavigationManager.RefreshConnections(result.meshID, tileID);
                    gAIEnv.pMNMPathfinder?.OnNavigationMeshChanged(result.meshID, new MNM_TileID { id = tileID });

                    var agentType = m_agentTypes[(int)(mesh.agentTypeID.id - 1)];
                    foreach (var callback in agentType.callbacks)
                    {
                        callback(mesh.agentTypeID, result.meshID, tileID);
                    }
                }
            }
            break;

            case TileTaskResult.NoChanges:
                break;

            default:
                Debug.Assert(false);
                break;
        }
    }

    // Private helpers
    private void ResetAll() { foreach (var u in m_users) u.Reset(); }
    private void WaitForAll() { foreach (var u in m_users) u.CompleteRunningTasks(); }
    private void UpdateWriting() { foreach (var u in m_users) u.UpdateForSynchronousWritingOperations(); }
    private void UpdateReading() { foreach (var u in m_users) u.UpdateForSynchronousOrAsynchronousReadingOperation(); }

    // UpdateInternal — NavigationSystem.cpp lines 758-786
    private void UpdateInternal(bool blocking)
    {
        UpdateMeshes(0.0333f, blocking, false, false);
    }

    private void UpdateSubsystems() { m_offMeshNavigationManager.ProcessQueuedRequests(); }
    private void ComputeWorldAABB() { m_worldAABB = new AABB(AABB.RESET); foreach (var at in m_agentTypes) foreach (var mi in at.meshes) { var m = m_meshes[mi.id.id]; if (m.boundary.id != 0) m_worldAABB.Add(m_volumes[m.boundary.id].aabb); } }

    // SetupTasks — NavigationSystem.cpp lines 2207-2226
    private void SetupTasks()
    {
        m_maxRunningTaskCount = 4;
        m_results.Clear();
        for (int i = 0; i < m_maxRunningTaskCount; i++)
            m_results.Add(new TileTaskResult { next = (ushort)(i + 1) });
        m_runningTasks.Clear();
        m_free = 0;
    }

    // StopAllTasks — NavigationSystem.cpp lines 1410-1422
    private void StopAllTasks()
    {
        for (int t = 0; t < m_runningTasks.Count; t++)
            m_results[m_runningTasks[t]].state = TileTaskResult.Failed;
        for (int t = 0; t < m_runningTasks.Count; t++)
            m_results[m_runningTasks[t]].tile?.Destroy();
        m_runningTasks.Clear();
    }

    private void NotifyAll(INavigationSystem.ENavigationEvent e) { foreach (var l in m_listenersList) l.OnNavigationEvent(e); }
}
