// Port of CryPhysics spatial hash grid (m_entgrid, pe_gridthunk)
// Original: Copyright Crytek GMBH, used under license
// Thread safety: ReadWriteLock for concurrent queries during parallel stepping.
// Port of the m_lockGrid pattern from physicalworld.h.

using CryPhysics.Entities;
using CryPhysics.Math;

namespace CryPhysics.World;

/// <summary>
/// 2D spatial hash grid for fast entity spatial queries.
/// Port of the entity grid from CPhysicalWorld (m_entgrid, pe_gridthunk system).
/// Entities are bucketed by their AABB projected onto the XY plane.
///
/// Thread safety: uses ReaderWriterLockSlim so multiple threads can query
/// concurrently (read lock) while mutations (add/remove/update) take a write lock.
/// This matches the m_lockGrid pattern from CPhysicalWorld.
/// </summary>
public class SpatialGrid
{
    private readonly Dictionary<long, List<IPhysicalEntity>> _cells = new();
    private readonly Dictionary<IPhysicalEntity, List<long>> _entityCells = new();
    private readonly ReaderWriterLockSlim _gridLock = new();

    public PhysVector3 Origin { get; }
    public int Nx { get; }
    public int Ny { get; }
    public float StepX { get; }
    public float StepY { get; }
    public float InvStepX { get; }
    public float InvStepY { get; }

    public SpatialGrid(PhysVector3 origin, int nx, int ny, float stepX, float stepY)
    {
        Origin = origin;
        Nx = nx; Ny = ny;
        StepX = stepX; StepY = stepY;
        InvStepX = 1f / stepX;
        InvStepY = 1f / stepY;
    }

    /// <summary>Hash a grid cell coordinate to a dictionary key.</summary>
    private static long CellKey(int ix, int iy) => ((long)ix << 32) | (uint)iy;

    /// <summary>Get grid cell indices for a world position.</summary>
    public (int ix, int iy) WorldToCell(float x, float y)
    {
        int ix = (int)MathF.Floor((x - Origin.X) * InvStepX);
        int iy = (int)MathF.Floor((y - Origin.Y) * InvStepY);
        return (System.Math.Clamp(ix, 0, Nx - 1), System.Math.Clamp(iy, 0, Ny - 1));
    }

    /// <summary>
    /// Add an entity to the grid based on its AABB.
    /// Acquires write lock for thread safety.
    /// </summary>
    public void AddEntity(IPhysicalEntity entity)
    {
        if (entity is not PhysicalEntity pe) return;

        _gridLock.EnterWriteLock();
        try
        {
            AddEntityInternal(pe);
        }
        finally
        {
            _gridLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Remove an entity from the grid.
    /// Acquires write lock for thread safety.
    /// </summary>
    public void RemoveEntity(IPhysicalEntity entity)
    {
        _gridLock.EnterWriteLock();
        try
        {
            RemoveEntityInternal(entity);
        }
        finally
        {
            _gridLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Update an entity's grid position (remove and re-add).
    /// Acquires write lock for thread safety.
    /// </summary>
    public void UpdateEntity(IPhysicalEntity entity)
    {
        _gridLock.EnterWriteLock();
        try
        {
            RemoveEntityInternal(entity);
            if (entity is PhysicalEntity pe)
                AddEntityInternal(pe);
        }
        finally
        {
            _gridLock.ExitWriteLock();
        }
    }

    private void AddEntityInternal(PhysicalEntity pe)
    {
        var (ix0, iy0) = WorldToCell(pe.BBoxMin.X, pe.BBoxMin.Y);
        var (ix1, iy1) = WorldToCell(pe.BBoxMax.X, pe.BBoxMax.Y);

        var cells = new List<long>();
        for (int ix = ix0; ix <= ix1; ix++)
        for (int iy = iy0; iy <= iy1; iy++)
        {
            long key = CellKey(ix, iy);
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<IPhysicalEntity>(4);
                _cells[key] = list;
            }
            list.Add(pe);
            cells.Add(key);
        }
        _entityCells[pe] = cells;
    }

    private void RemoveEntityInternal(IPhysicalEntity entity)
    {
        if (!_entityCells.TryGetValue(entity, out var cells)) return;
        foreach (var key in cells)
        {
            if (_cells.TryGetValue(key, out var list))
            {
                list.Remove(entity);
                if (list.Count == 0) _cells.Remove(key);
            }
        }
        _entityCells.Remove(entity);
    }

    /// <summary>
    /// Get all entities whose grid cells overlap the given AABB.
    /// Port of GetEntitiesAround from physicalworld.cpp.
    /// Acquires read lock for thread-safe concurrent queries.
    /// </summary>
    public int GetEntitiesInBox(in PhysVector3 bmin, in PhysVector3 bmax,
                                 IPhysicalEntity[] result, int maxResults)
    {
        _gridLock.EnterReadLock();
        try
        {
            var (ix0, iy0) = WorldToCell(bmin.X, bmin.Y);
            var (ix1, iy1) = WorldToCell(bmax.X, bmax.Y);

            var seen = new HashSet<IPhysicalEntity>();
            int count = 0;

            for (int ix = ix0; ix <= ix1; ix++)
            for (int iy = iy0; iy <= iy1; iy++)
            {
                long key = CellKey(ix, iy);
                if (!_cells.TryGetValue(key, out var list)) continue;
                foreach (var ent in list)
                {
                    if (count >= maxResults) return count;
                    if (!seen.Add(ent)) continue;

                    // Fine AABB check
                    if (ent is PhysicalEntity pe)
                    {
                        if (pe.BBoxMax.X >= bmin.X && pe.BBoxMin.X <= bmax.X &&
                            pe.BBoxMax.Y >= bmin.Y && pe.BBoxMin.Y <= bmax.Y &&
                            pe.BBoxMax.Z >= bmin.Z && pe.BBoxMin.Z <= bmax.Z)
                        {
                            result[count++] = ent;
                        }
                    }
                }
            }
            return count;
        }
        finally
        {
            _gridLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Ray traversal through grid cells (DDA algorithm).
    /// Returns entities whose cells the ray passes through.
    /// Acquires read lock for thread-safe concurrent queries.
    /// </summary>
    public int RayQuery(in PhysVector3 origin, in PhysVector3 dir,
                        IPhysicalEntity[] result, int maxResults, HashSet<IPhysicalEntity>? skip = null)
    {
        _gridLock.EnterReadLock();
        try
        {
            var seen = new HashSet<IPhysicalEntity>();
            int count = 0;

            // Compute ray AABB and use grid traversal
            var end = origin + dir;
            float minX = MathF.Min(origin.X, end.X), maxX = MathF.Max(origin.X, end.X);
            float minY = MathF.Min(origin.Y, end.Y), maxY = MathF.Max(origin.Y, end.Y);
            float minZ = MathF.Min(origin.Z, end.Z), maxZ = MathF.Max(origin.Z, end.Z);

            var (ix0, iy0) = WorldToCell(minX, minY);
            var (ix1, iy1) = WorldToCell(maxX, maxY);

            for (int ix = ix0; ix <= ix1; ix++)
            for (int iy = iy0; iy <= iy1; iy++)
            {
                long key = CellKey(ix, iy);
                if (!_cells.TryGetValue(key, out var list)) continue;
                foreach (var ent in list)
                {
                    if (count >= maxResults) return count;
                    if (skip?.Contains(ent) == true) continue;
                    if (!seen.Add(ent)) continue;

                    // AABB vs ray test
                    if (ent is PhysicalEntity pe)
                    {
                        if (pe.BBoxMax.X >= minX && pe.BBoxMin.X <= maxX &&
                            pe.BBoxMax.Y >= minY && pe.BBoxMin.Y <= maxY &&
                            pe.BBoxMax.Z >= minZ && pe.BBoxMin.Z <= maxZ)
                        {
                            result[count++] = ent;
                        }
                    }
                }
            }
            return count;
        }
        finally
        {
            _gridLock.ExitReadLock();
        }
    }
}
