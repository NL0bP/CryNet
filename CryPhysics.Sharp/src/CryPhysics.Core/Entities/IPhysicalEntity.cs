// Port of physinterface.h - IPhysicalEntity interface
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Geometry;
using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

/// <summary>
/// Core interface for all physics entities.
/// Port of IPhysicalEntity from CryEngine.
/// Uses command pattern: SetParams/GetParams/DoAction/GetStatus.
/// </summary>
public interface IPhysicalEntity
{
    /// <summary>Entity type (Static, Rigid, Living, etc.).</summary>
    PhysicsEntityType Type { get; }

    /// <summary>Reference counting.</summary>
    int AddRef();
    int Release();

    /// <summary>Set entity parameters.</summary>
    int SetParams(PhysicsParamsBase parameters, bool threadSafe = false);

    /// <summary>Get entity parameters.</summary>
    int GetParams(PhysicsParamsBase parameters);

    /// <summary>Execute a physics action on the entity.</summary>
    int DoAction(PhysicsActionBase action, bool threadSafe = false);

    /// <summary>Query entity status.</summary>
    int GetStatus(PhysicsStatusBase status);

    /// <summary>Add a geometry part to the entity.</summary>
    int AddGeometry(PhysGeometry geometry, ParamsPart parameters, int id = -1, bool threadSafe = false);

    /// <summary>Remove a geometry part.</summary>
    void RemoveGeometry(int id, bool threadSafe = false);

    /// <summary>Get foreign data associated with this entity.</summary>
    object? GetForeignData(int type = 0);

    /// <summary>Get foreign data type identifier.</summary>
    int GetForeignDataType();

    /// <summary>Advance simulation by one step.</summary>
    int DoStep(float timeInterval, int callerIndex = 0);

    /// <summary>
    /// Phase 1 of stepping: must be called before DoStep. Port of CPhysicalEntity::StartStep
    /// virtual (physinterface.h:2446). Default no-op.
    /// </summary>
    void StartStep(float timeInterval) { }

    /// <summary>
    /// Composite step (StartStep + DoStep + post-processing). Port of CPhysicalEntity::Step
    /// virtual. Returns 1 on success.
    /// </summary>
    int Step(float timeInterval) => DoStep(timeInterval);

    /// <summary>
    /// Roll the simulation back by `timeInterval`. Port of CPhysicalEntity::StepBack virtual
    /// (physinterface.h:2447). Default no-op.
    /// </summary>
    void StepBack(float timeInterval) { }

    /// <summary>Get the physics world this entity belongs to.</summary>
    IPhysicalWorld? GetWorld();

    /// <summary>Awake/sleep the entity.</summary>
    void Awake(bool awake = true, float minTime = 0f);

    /// <summary>Check if the entity is awake.</summary>
    bool IsAwake { get; }

    /// <summary>Get unique entity ID.</summary>
    int Id { get; }
}

/// <summary>
/// Physics world interface. Port of IPhysicalWorld from CryEngine (simplified).
/// </summary>
public interface IPhysicalWorld
{
    /// <summary>Create a physical entity.</summary>
    IPhysicalEntity CreatePhysicalEntity(PhysicsEntityType type,
        PhysicsParamsBase? initialParams = null, object? foreignData = null, int foreignDataType = 0, int id = -1);

    /// <summary>Destroy a physical entity.</summary>
    void DestroyPhysicalEntity(IPhysicalEntity entity);

    /// <summary>Advance the simulation.</summary>
    void TimeStep(float dt, int flags = 0);

    /// <summary>Get simulation time.</summary>
    float PhysicsTime { get; }

    /// <summary>Ray world intersection.</summary>
    int RayWorldIntersection(in PhysVector3 origin, in PhysVector3 dir, int objectTypes,
        uint flags, RayHit[] hits, int maxHits, IPhysicalEntity[]? skipEntities = null);

    /// <summary>Get entities in a bounding box.</summary>
    int GetEntitiesInBox(in PhysVector3 min, in PhysVector3 max, IPhysicalEntity[] entities, int objectTypes);

    /// <summary>Get the geometry manager.</summary>
    GeometryManager GeomManager { get; }

    /// <summary>Simulate an explosion.</summary>
    void SimulateExplosion(in PhysVector3 epicenter, float radius, float pressure);

    /// <summary>Set surface parameters for a material index.</summary>
    void SetSurfaceParameters(int surfIdx, float bounciness, float friction, uint flags = 0);
}

/// <summary>
/// Ray hit result. Port of ray_hit from physinterface.h.
/// </summary>
public struct RayHit
{
    public float Distance;
    public PhysVector3 Point;
    public PhysVector3 Normal;
    public int SurfaceIdx;
    public IPhysicalEntity? Entity;
    public int PartId;
    public int IdMaterial;
    public int ForeignIdx;
    public float Len;
}
