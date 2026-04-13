// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/TileGenerator.h (576L)
// and TileGenerator.cpp (2980L) + TileGeneratorDraw.cpp (720L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CryAISystem.CryCommon;
using static CryAISystem.CryMath;
using static CryAISystem.Navigation.MNM.MNMUtils;
using static CryAISystem.Navigation.MNM.FixedPointMath;

namespace CryAISystem.Navigation.MNM;

public class TileGenerator
{
    public const int MaxTileSizeX = 18;
    public const int MaxTileSizeY = 18;
    public const int MaxTileSizeZ = 18;

    // ============================================================================
    // Params
    // ============================================================================

    public class Params
    {
        [Flags]
        public enum Flags : ushort
        {
            NoBorder = 1 << 0,
            NoErosion = 1 << 1,
            NoHashTest = 1 << 2,
            BuildBVTree = 1 << 3,
            DebugInfo = 1 << 7,
        }

        public Vec3 origin;
        public Vec3 voxelSize;
        public float climbableInclineGradient;
        public float climbableStepRatio;

        public ushort flags;
        public ushort minWalkableArea = 16;
        public ushort exclusionCount;

        public byte blurAmount;
        public byte sizeX = 8;
        public byte sizeY = 8;
        public byte sizeZ = 8;

        public class AgentSettings
        {
            public uint radius = 4;
            public uint height = 18;
            public uint climbableHeight = 4;
            public uint maxWaterDepth = 8;
            public WorldVoxelizer.NavigationMeshEntityCallback callback;
        }

        public AgentSettings agent = new();
        public BoundingVolume boundary;
        public BoundingVolume[] exclusions;
        public uint hashValue;

        public Params()
        {
            origin = new Vec3(0, 0, 0);
            voxelSize = new Vec3(0.1f, 0.1f, 0.1f);
        }
    }

    // ============================================================================
    // Enums
    // ============================================================================

    public enum ProfilerTimers
    {
        Voxelization = 0, Filter, DistanceTransform, Blur,
        ContourExtraction, Simplification, Triangulation, BVTreeConstruction,
    }

    public enum ProfilerMemoryUsers
    {
        DynamicSpanGridMemory = 0, CompactSpanGridMemory, SegmentationMemory,
        RegionMemory, PolygonMemory, TriangulationMemory, VertexMemory,
        TriangleMemory, BVTreeConstructionMemory, BVTreeMemory,
    }

    public enum ProfilerStats
    {
        VoxelizationTriCount = 0, RegionCount, PolygonCount,
        TriangleCount, VertexCount, BVTreeNodeCount,
    }

    public enum DrawMode
    {
        DrawNone = 0, DrawRawVoxels, DrawFlaggedVoxels, DrawDistanceTransform,
        DrawSegmentation, DrawContourVertices, DrawNumberedContourVertices,
        DrawTracers, DrawSimplifiedContours, DrawTriangulation, DrawBVTree,
        LastDrawMode,
    }

    public enum SpanFlags : uint
    {
        NotWalkable = 1 << 0,
        TileBoundary = 1 << 1,
    }

    public const ushort NotWalkable = 1 << 0;
    public const ushort TileBoundaryFlag = 1 << 1;

    public enum Labels : ushort
    {
        FirstInvalidLabel = (1 << 12) - 1,
        NoLabel = (1 << 12) - 1,
        ExternalContour = 1 << 12,
        InternalContour = 1 << 13,
        BorderLabelH = 1 << 14,
        BorderLabelV = 1 << 15,
    }

    public const ushort FirstInvalidLabel = (1 << 12) - 1;
    public const ushort NoLabel = (1 << 12) - 1;
    public const ushort ExternalContour = 1 << 12;
    public const ushort InternalContour = 1 << 13;
    public const ushort BorderLabelH = unchecked((ushort)(1 << 14));
    public const ushort BorderLabelV = unchecked((ushort)(1 << 15));

    public enum Paint : ushort
    {
        NoPaint = 0,
        BadPaint,
        OkPaintStart,
    }

    public const ushort NoPaint = 0;
    public const ushort BadPaint = 1;
    public const ushort OkPaintStart = 2;

    // ============================================================================
    // Internal types
    // ============================================================================

    public struct ContourVertex : IComparable<ContourVertex>, IEquatable<ContourVertex>
    {
        public ushort x, y, z, flags;

        [Flags]
        public enum VertexFlags : ushort
        {
            TileBoundary = 1 << 0,
            TileBoundaryV = 1 << 1,
            Unremovable = 1 << 2,
            TileSideA = 1 << 4,
            TileSideB = 1 << 5,
            TileSideC = 1 << 6,
            TileSideD = 1 << 7,
            TileSides = TileSideA | TileSideB | TileSideC | TileSideD,
        }

        // Named constants for use without enum cast
        public const ushort FlagTileBoundary = 1 << 0;
        public const ushort FlagTileBoundaryV = 1 << 1;
        public const ushort FlagUnremovable = 1 << 2;

        public ContourVertex(ushort _x, ushort _y, ushort _z)
        { x = _x; y = _y; z = _z; flags = 0; }

        public int CompareTo(ContourVertex other)
        {
            if (x == other.x) return y.CompareTo(other.y);
            return x.CompareTo(other.x);
        }

        public bool Equals(ContourVertex other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is ContourVertex v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public static bool operator ==(ContourVertex a, ContourVertex b) => a.Equals(b);
        public static bool operator !=(ContourVertex a, ContourVertex b) => !a.Equals(b);
        public static bool operator <(ContourVertex a, ContourVertex b) => a.CompareTo(b) < 0;
        public static bool operator >(ContourVertex a, ContourVertex b) => a.CompareTo(b) > 0;
    }

    public struct PolygonVertex : IComparable<ContourVertex>, IEquatable<ContourVertex>
    {
        public enum VertexFlags : ushort
        {
            Reflex = 1 << 0,
            Ear = 1 << 1,
        }

        public const ushort Reflex = 1 << 0;
        public const ushort Ear = 1 << 1;

        public ushort x, y, z, flags;

        public PolygonVertex(ushort _x, ushort _y, ushort _z)
        { x = _x; y = _y; z = _z; flags = 0; }

        public int CompareTo(ContourVertex other)
        {
            if (x == other.x) return y.CompareTo(other.y);
            return x.CompareTo(other.x);
        }

        public bool Equals(ContourVertex other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is ContourVertex v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }

    public class Hole
    {
        public List<PolygonVertex> verts = new();
        public Vec2i center;
        public int rad;
    }

    public class Polygon
    {
        public List<PolygonVertex> contour = new();
        public List<Hole> holes = new();
    }

    public class Region
    {
        public List<ContourVertex> contour = new();
        public List<List<ContourVertex>> holes = new();
        public int spanCount;
        public int flags;

        [Flags]
        public enum RegionFlags
        {
            TileBoundary = 1 << 0,
            TileBoundaryV = 1 << 1,
        }

        public void Swap(Region other)
        {
            (spanCount, other.spanCount) = (other.spanCount, spanCount);
            (flags, other.flags) = (other.flags, flags);
            (contour, other.contour) = (other.contour, contour);
            (holes, other.holes) = (other.holes, holes);
        }
    }

    public enum TracerDir { N, E, S, W, TOTAL_DIR }

    public struct Tracer : IEquatable<Tracer>
    {
        public Vec3i pos;
        public int dir;
        public int indexIn, indexOut;
        public bool bPinchPoint;

        public bool Equals(Tracer other) => pos == other.pos && dir == other.dir;
        public override bool Equals(object obj) => obj is Tracer t && Equals(t);
        public override int GetHashCode() => HashCode.Combine(pos, dir);
        public static bool operator ==(Tracer a, Tracer b) => a.Equals(b);
        public static bool operator !=(Tracer a, Tracer b) => !a.Equals(b);

        public void SetPos(int x, int y, int z) { pos = new Vec3i(x, y, z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPos(NeighbourInfo info) { pos = new Vec3i(info.pos.X, info.pos.Y, (int)info.top); }
        public void TurnRight() { dir = (dir + 1) & 3; }
        public void TurnLeft() { dir = (dir + 3) & 3; }
        public Vec2i GetDir()
        {
            return new Vec2i(
                (dir & 1) * CryMath.sgn(2 - dir),
                ((dir + 1) & 1) * CryMath.sgn(1 - dir));
        }
        public Vec3i GetFront() { Vec2i d = GetDir(); return pos + new Vec3i(d.X, d.Y, 0); }
        public Vec3i GetLeft() { Vec2i d = GetDir(); Vec2i r = d.rot90ccw(); return pos + new Vec3i(r.X, r.Y, 0); }
        public Vec3i GetFrontLeft()
        {
            Vec2i dv = GetDir();
            Vec2i r = dv.rot90ccw();
            return pos + new Vec3i(dv.X + r.X, dv.Y + r.Y, 0);
        }
    }

    public class TracerPath
    {
        public List<Tracer> steps = new();
        public int turns;
    }

    // ============================================================================
    // NeighbourInfo types (from C++ header)
    // ============================================================================

    public struct NeighbourInfoRequirements
    {
        public ushort paint;
        public ushort notPaint;

        public NeighbourInfoRequirements() { paint = NoPaint; notPaint = NoPaint; }
    }

    public struct NeighbourInfo
    {
        public Vec2i pos;
        public int top;
        public int index;
        public ushort label;
        public ushort paint;
        public bool isValid;

        public NeighbourInfo(Vec3i p)
        {
            pos = new Vec2i(p.x, p.y);
            top = p.z;
            index = -1;
            label = NoLabel;
            paint = NoPaint;
            isValid = false;
        }

        public NeighbourInfo(Vec2i xy, int z)
        {
            pos = xy;
            top = z;
            index = -1;
            label = NoLabel;
            paint = NoPaint;
            isValid = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Check(in NeighbourInfoRequirements req)
        {
            return isValid &&
                (req.paint == NoPaint || req.paint == paint) &&
                (req.notPaint == NoPaint || req.notPaint != paint);
        }
    }

    // ============================================================================
    // SurroundingSpanInfo (from C++ header)
    // ============================================================================

    public struct SurroundingSpanInfo
    {
        public int flags;
        public int index;
        public ushort label;

        public SurroundingSpanInfo(ushort _label, int _index, int _flags = 0)
        {
            flags = _flags;
            index = _index;
            label = _label;
        }
    }

    public enum NeighbourClassification
    {
        UW = 0, // not walkable
        NB = 1, // walkable, not border
        WB = 2, // walkable, border
    }

    public class ProfilerType : Profiler<ProfilerMemoryUsers, ProfilerTimers, ProfilerStats> { }

    // ============================================================================
    // Neighbour offset table
    // ============================================================================

    private static readonly int[][] NeighbourOffset_TileGenerator =
    {
        new[]{ 0, 1}, new[]{-1, 0}, new[]{ 1, 0}, new[]{ 0,-1},
        new[]{-1,-1}, new[]{ 1,-1}, new[]{ 1, 1}, new[]{-1, 1},
    };

    private static readonly byte[][][] CornerTable =
    {
        new[] { new byte[]{0,0,1}, new byte[]{0,0,0}, new byte[]{1,1,1} },
        new[] { new byte[]{0,0,1}, new byte[]{0,0,0}, new byte[]{1,0,0} },
        new[] { new byte[]{1,1,0}, new byte[]{0,0,1}, new byte[]{1,0,0} },
    };

    // ============================================================================
    // Fields
    // ============================================================================

    protected Params m_params = new();
    protected ProfilerType m_profiler = new();
    protected int m_top;

    protected List<Tile.Triangle> m_triangles = new();
    protected List<Tile.Vertex> m_vertices = new();
    protected List<Tile.BVNode> m_bvtree = new();

    protected CompactSpanGrid m_spanGrid = new();
    protected List<ushort> m_distances = new();
    protected List<ushort> m_labels = new();
    protected List<ushort> m_paint = new();
    protected List<Region> m_regions = new();
    protected List<Polygon> m_polygons = new();
    protected List<TracerPath> m_tracerPaths = new();
    protected CompactSpanGrid m_spanGridFlagged = new();

    // ============================================================================
    // Private helper functions
    // ============================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int sqr_i(int x) => x * x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int int_ceil(float x) => (int)MathF.Ceiling(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float sqrt_tpl(float x) => MathF.Sqrt(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBorderLabel(ushort label)
    {
        return (label & (BorderLabelH | BorderLabelV)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLabelValid(ushort label)
    {
        return label < FirstInvalidLabel;
    }

    // Vec3i dot and len2 helpers
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Vec3i_dot(Vec3i a, Vec3i b) => a.x * b.x + a.y * b.y + a.z * b.z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Vec3i_len2(Vec3i a) => a.x * a.x + a.y * a.y + a.z * a.z;

    // Vec2i dot and len2 helpers
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Vec2i_dot(Vec2i a, Vec2i b) => a.X * b.X + a.Y * b.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Vec2i_len2(Vec2i a) => a.X * a.X + a.Y * a.Y;

    // ============================================================================
    // DistVertexToLineSq — free function from C++
    // ============================================================================

    private static real_t DistVertexToLineSq(int x, int y, int z, int ax, int ay, int az, int bx, int by, int bz)
    {
        Vec3i diff = new Vec3i(x - ax, y - ay, z - az);
        Vec3i dir = new Vec3i(bx - ax, by - ay, bz - az);
        int t = Vec3i_dot(diff, dir);

        if (t > 0)
        {
            int lenSq = Vec3i_len2(dir);

            if (t >= lenSq)
            {
                Vec3i d = diff - dir;
                int lenSq_t_raw = Vec3i_len2(d);
                real_t lenSq_t = new real_t(lenSq_t_raw);
                return lenSq_t >= new real_t(0) ? lenSq_t : real_t.max();
            }
            else
            {
                vector3_t diff_t = new vector3_t(new real_t(diff.x), new real_t(diff.y), new real_t(diff.z));
                vector3_t dir_t = new vector3_t(new real_t(dir.x), new real_t(dir.y), new real_t(dir.z));
                real_t lenSq_t = (diff_t - (dir_t * real_t.fraction(t, lenSq))).lenSq();
                return lenSq_t >= new real_t(0) ? lenSq_t : real_t.max();
            }
        }

        int lenSq_t_final = Vec3i_len2(diff);
        real_t result = new real_t(lenSq_t_final);
        return result >= new real_t(0) ? result : real_t.max();
    }

    private static real_t DistVertexToLineSq(int x, int y, int ax, int ay, int bx, int by)
    {
        return DistVertexToLineSq(x, y, 0, ax, ay, 0, bx, by, 0);
    }

    private static readonly real_t AddContourVertexThreshold = real_t.fraction(15, 1000);

    // ============================================================================
    // IsWalkable helpers (free functions from C++)
    // ============================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWalkable(ushort label, ushort distance, int erosion)
    {
        if ((label & (BorderLabelH | BorderLabelV)) != 0)
            return false;
        if (distance < erosion)
            return false;
        return true;
    }

    // ============================================================================
    // Public API
    // ============================================================================

    public bool Generate(Params p, Tile tile, uint[] hashValue)
    {
        if (p.sizeX > MaxTileSizeX || p.sizeY > MaxTileSizeY || p.sizeZ > MaxTileSizeZ)
            return false;

        const float MinVoxelSize = 0.025f;
        if (p.voxelSize.x < MinVoxelSize || p.voxelSize.y < MinVoxelSize || p.voxelSize.z < MinVoxelSize)
            return false;

        Clear();

        m_params = p;
        m_profiler = new ProfilerType();
        m_top = (int)(m_params.sizeZ / m_params.voxelSize.z + 0.10f);

        AABB aabb = new AABB(
            m_params.origin,
            m_params.origin + new Vec3(m_params.sizeX, m_params.sizeY, m_params.sizeZ));

        if (m_params.boundary != null && !m_params.boundary.Overlaps(aabb))
            return false;

        int border = BorderSizeH();
        int borderV = BorderSizeV();
        if ((border | borderV) != 0)
            aabb.Expand(new Vec3(border * m_params.voxelSize.x, border * m_params.voxelSize.y, borderV * m_params.voxelSize.z));

        aabb.max = aabb.max + new Vec3(0, 0, m_params.agent.height * m_params.voxelSize.z);

        bool fullyContained = true;

        if (m_params.boundary != null || m_params.exclusionCount > 0)
        {
            if (m_params.exclusions != null)
            {
                for (int e = 0; e < m_params.exclusionCount; ++e)
                {
                    BoundingVolume.ExtendedOverlap eoverlap = m_params.exclusions[e].ContainsAABB(aabb);
                    if (eoverlap == BoundingVolume.ExtendedOverlap.FullOverlap)
                        return false;
                    else if (eoverlap == BoundingVolume.ExtendedOverlap.PartialOverlap)
                        fullyContained = false;
                }
            }

            BoundingVolume.ExtendedOverlap ioverlap =
                m_params.boundary != null ? m_params.boundary.ContainsAABB(aabb) : BoundingVolume.ExtendedOverlap.FullOverlap;

            if (ioverlap == BoundingVolume.ExtendedOverlap.NoOverlap)
                return false;
            else if (ioverlap == BoundingVolume.ExtendedOverlap.PartialOverlap)
                fullyContained = false;
        }

        // Reset working data
        m_spanGrid = new CompactSpanGrid();
        m_distances.Clear();
        m_labels.Clear();
        m_regions.Clear();
        m_polygons.Clear();
        m_vertices.Clear();
        m_triangles.Clear();
        m_bvtree.Clear();
        m_spanGridFlagged = new CompactSpanGrid();

        // Compute hash
        uint hashSeed = fullyContained ? 0xf007b00bu : 0u;
        if (!fullyContained)
        {
            HashComputer hash = new();
            if (m_params.boundary != null || m_params.exclusionCount > 0)
            {
                if (m_params.exclusions != null)
                {
                    for (int e = 0; e < m_params.exclusionCount; ++e)
                    {
                        BoundingVolume volume = m_params.exclusions[e];
                        for (int v = 0; v < volume.vertices.Count; ++v)
                            hash.Add(volume.vertices[v]);
                        hash.Add(volume.height);
                    }
                }
                if (m_params.boundary != null)
                {
                    for (int v = 0; v < m_params.boundary.vertices.Count; ++v)
                        hash.Add(m_params.boundary.vertices[v]);
                    hash.Add(m_params.boundary.height);
                }
            }
            hash.Complete();
            hashSeed = hash.GetValue();
        }

        uint hashVal = 0;
        int triCount = VoxelizeVolume(aabb, hashSeed, ref hashVal);

        if (hashValue != null && hashValue.Length > 0)
            hashValue[0] = hashVal;

        if (triCount == 0) return false;

        tile.hashValue = hashVal;

        FilterWalkable(aabb, fullyContained);
        if (m_spanGrid.GetSpanCount() == 0) return false;

        ComputeDistanceTransform();

        if (ExtractContours() == 0) return false;

        FilterBadRegions(m_params.minWalkableArea);
        SimplifyContours();
        Triangulate();

        if ((m_params.flags & (ushort)Params.Flags.BuildBVTree) != 0)
            BuildBVTree();

        if (m_vertices.Count == 0) return false;

        // Copy results to tile
        const int MaxTriangleCount = 1024;

        if (m_triangles.Count > MaxTriangleCount)
        {
            // AIWarning: Too many triangles in one tile
        }

        tile.CopyTriangles(m_triangles.ToArray(), (ushort)Math.Min(MaxTriangleCount, m_triangles.Count));
        tile.CopyVertices(m_vertices.ToArray(), (ushort)m_vertices.Count);

        if ((m_params.flags & (ushort)Params.Flags.BuildBVTree) != 0 && m_bvtree.Count > 0)
            tile.CopyNodes(m_bvtree.ToArray(), (ushort)m_bvtree.Count);

        return true;
    }

    public void Draw(DrawMode mode)
    {
        // Debug visualization — requires engine render interface. No-op in C#.
    }

    public ProfilerType GetProfiler() => m_profiler;

    // ============================================================================
    // Helper inline methods
    // ============================================================================

    protected int BorderSizeH()
    {
        return ((m_params.flags & (ushort)Params.Flags.NoBorder) != 0) ? 0 : (int)(((m_params.agent.radius & ~1u) + 2));
    }

    protected int BorderSizeV()
    {
        return ((m_params.flags & (ushort)Params.Flags.NoBorder) != 0) ? 0 : (int)(((m_params.agent.radius & ~1u) + 2));
    }

    protected bool IsBorderCell(int x, int y)
    {
        int border = BorderSizeH();
        int width = m_spanGrid.GetWidth();
        int height = m_spanGrid.GetHeight();
        return (x < border) || (x >= width - border) || (y < border) || (y >= height - border);
    }

    protected bool IsBoundaryCell_Static(int x, int y, int border, int width, int height)
    {
        return (((x == border) || (x == width - border - 1)) && (y >= border) && (y <= height - border - 1))
            || (((y == border) || (y == height - border - 1)) && (x >= border) && (x <= width - border - 1));
    }

    protected bool IsBoundaryCell(int x, int y)
    {
        int border = BorderSizeH();
        int width = m_spanGrid.GetWidth();
        int height = m_spanGrid.GetHeight();
        return (((x == border) || (x == width - border - 1)) && (y >= border) && (y <= height - border - 1))
            || (((y == border) || (y == height - border - 1)) && (x >= border) && (x <= width - border - 1));
    }

    protected bool IsBoundaryVertex(int x, int y)
    {
        int border = BorderSizeH();
        int width = m_spanGrid.GetWidth();
        int height = m_spanGrid.GetHeight();
        return (((x == border) || (x == width - border)) && (y >= border) && (y <= height - border))
            || (((y == border) || (y == height - border)) && (x >= border) && (x <= width - border));
    }

    protected bool IsCornerVertex(int x, int y)
    {
        int border = BorderSizeH();
        int width = m_spanGrid.GetWidth();
        int height = m_spanGrid.GetHeight();
        return ((x == border) || (x == width - border)) && ((y == border) || (y == height - border));
    }

    protected bool IsBoundaryVertexV(int z)
    {
        int borderV = BorderSizeV();
        return (z == borderV) || (z == m_top + borderV);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NeighbourClassification ClassifyNeighbour(in SurroundingSpanInfo neighbour, int erosion, int borderFlag)
    {
        if (((neighbour.flags & NotWalkable) != 0) || (neighbour.index >= 0 && neighbour.index < m_distances.Count && m_distances[neighbour.index] < erosion))
            return NeighbourClassification.UW;
        return ((neighbour.label & borderFlag) != 0) ? NeighbourClassification.WB : NeighbourClassification.NB;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool ContourVertexRemovable(in ContourVertex contourVertex)
    {
        return ((contourVertex.flags & ContourVertex.FlagTileBoundary) == 0)
            && ((contourVertex.flags & ContourVertex.FlagUnremovable) == 0);
    }

    protected void CacheTracerPath(TracerPath path)
    {
        if ((m_params.flags & (ushort)Params.Flags.DebugInfo) != 0)
        {
            TracerPath copy = new TracerPath();
            copy.turns = path.turns;
            copy.steps = new List<Tracer>(path.steps);
            m_tracerPaths.Add(copy);
        }
    }

    // ============================================================================
    // Protected methods
    // ============================================================================

    protected void Clear()
    {
        m_profiler = new ProfilerType();
        m_triangles.Clear();
        m_vertices.Clear();
        m_bvtree.Clear();
        m_spanGrid.Clear();
        m_distances.Clear();
        m_labels.Clear();
        m_paint.Clear();
        m_regions.Clear();
        m_polygons.Clear();
        m_tracerPaths.Clear();
        m_spanGridFlagged.Clear();
    }

    protected int VoxelizeVolume(AABB volume, uint hashValueSeed, ref uint hashValue)
    {
        m_profiler.StartTimer(ProfilerTimers.Voxelization);

        WorldVoxelizer voxelizer = new();
        voxelizer.Start(volume, m_params.voxelSize);

        int triCount = voxelizer.ProcessGeometry(hashValueSeed, m_params.hashValue, new uint[] { 0 },
            m_params.agent.callback);

        voxelizer.CalculateWaterDepth();

        DynamicSpanGrid spanGrid = voxelizer.GetSpanGrid();
        m_spanGrid.BuildFrom(spanGrid);

        m_profiler.AddStat(ProfilerStats.VoxelizationTriCount, triCount);
        m_profiler.StopTimer(ProfilerTimers.Voxelization);

        return triCount;
    }

    // ============================================================================
    // FilterWalkable — full implementation from C++ TileGenerator.cpp:283-682
    // ============================================================================

    protected void FilterWalkable(AABB aabb, bool fullyContained)
    {
        m_profiler.StartTimer(ProfilerTimers.Filter);

        int gridWidth = m_spanGrid.GetWidth();
        int gridHeight = m_spanGrid.GetHeight();
        int gridSize = gridWidth * gridHeight;

        int heightVoxelCount = (int)m_params.agent.height;
        int climbableVoxelCount = (int)m_params.agent.climbableHeight;
        int border = BorderSizeH();
        float climbableInclineGradient = m_params.climbableInclineGradient;
        float climbableStepRatio = m_params.climbableStepRatio;

        int inclineTestCount = climbableVoxelCount + 1;
        int climbableInclineGradientLowerBound = (int)MathF.Floor(climbableInclineGradient);
        float climbableInclineGradientSquared = climbableInclineGradient * climbableInclineGradient;
        int extraHeight = (int)m_params.agent.height;
        int spaceTop = (2 * BorderSizeV()) + extraHeight + m_top;

        int nonWalkableCount = 0;

        const int axisNeighbourCount = 4;

        for (int y = 0; y < gridHeight; ++y)
        {
            int ymult = y * gridWidth;

            for (int x = 0; x < gridWidth; ++x)
            {
                CompactSpanGrid.Cell cell = m_spanGrid.GetCellByIndex(x + ymult);
                if (cell.IsValid)
                {
                    bool boundaryCell = IsBoundaryCell_Static(x, y, border, gridWidth, gridHeight);
                    uint boundaryFlag = boundaryCell ? (uint)SpanFlags.TileBoundary : 0u;

                    int count = (int)cell.count;

                    for (int s = 0; s < count; ++s)
                    {
                        ref CompactSpanGrid.Span span = ref m_spanGrid.GetSpan((int)cell.index + s);

                        span.flags |= boundaryFlag;

                        if (span.backface != 0 || span.depth > m_params.agent.maxWaterDepth)
                        {
                            span.flags |= NotWalkable;
                            ++nonWalkableCount;
                        }
                        else
                        {
                            int top = (int)(span.bottom + span.height);

                            int nextBottom = spaceTop;

                            if (s + 1 < count)
                            {
                                CompactSpanGrid.Span nextSpan = m_spanGrid.GetSpanReadOnly((int)cell.index + s + 1);
                                nextBottom = (int)nextSpan.bottom;
                            }

                            int clearance = nextBottom - top;

                            if (clearance < heightVoxelCount)
                            {
                                span.flags |= NotWalkable;
                                ++nonWalkableCount;
                            }
                            else
                            {
                                bool neighbourTest = true;
                                float[] pVars = new float[axisNeighbourCount];

                                for (int n = 0; n < axisNeighbourCount; ++n)
                                {
                                    int nx = x + NeighbourOffset_TileGenerator[n][0];
                                    int ny = y + NeighbourOffset_TileGenerator[n][1];

                                    // Neighbour off grid - pass
                                    if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight)
                                        continue;

                                    int neighbourSpanGridIndex = ny * gridWidth + nx;
                                    CompactSpanGrid.Cell ncell = m_spanGrid.GetCellByIndex(neighbourSpanGridIndex);
                                    if (!ncell.IsValid)
                                    {
                                        neighbourTest = false;
                                        break;
                                    }

                                    int ncount = (int)ncell.count;
                                    int nindex = (int)ncell.index;

                                    int ptopLast = 0;
                                    int pnextBottomLast = 0;
                                    int dpTopFirst = 0;
                                    int dpTopLast = 0;
                                    int sdpTopFirst = 0;
                                    int sdpTopLast = 0;

                                    bool nCellValid = false;

                                    for (int ns = 0; ns < ncount; ++ns)
                                    {
                                        int nsindex = nindex + ns;
                                        CompactSpanGrid.Span nspan = m_spanGrid.GetSpanReadOnly(nsindex);

                                        int ntop = (int)(nspan.bottom + nspan.height);

                                        int nnextBottom = spaceTop;

                                        if (ns + 1 < ncount)
                                        {
                                            CompactSpanGrid.Span nnextSpan = m_spanGrid.GetSpanReadOnly((int)ncell.index + ns + 1);
                                            nnextBottom = (int)nnextSpan.bottom;
                                        }

                                        int dTop = Math.Abs(ntop - top);

                                        if (dTop <= climbableVoxelCount
                                            && Math.Min(nextBottom, nnextBottom) >= Math.Max(top, ntop) + heightVoxelCount)
                                        {
                                            ptopLast = ntop;
                                            pnextBottomLast = nnextBottom;
                                            dpTopFirst = dpTopLast = dTop;
                                            sdpTopFirst = sdpTopLast = ntop - top;

                                            nCellValid = true;
                                            break;
                                        }
                                    }

                                    if (!nCellValid)
                                    {
                                        neighbourTest = false;
                                        break;
                                    }

                                    if (dpTopFirst > 0)
                                    {
                                        int stepTestCount = int_ceil(dpTopFirst * climbableStepRatio);
                                        int stepTestTolerance = stepTestCount - 1;
                                        bool isStep = dpTopFirst > (climbableInclineGradientLowerBound + 1);
                                        bool stepTest = true;

                                        int ptopMin = ptopLast;
                                        int ptopMax = ptopLast;
                                        int pTopOffsMax = dpTopFirst;

                                        int pc = 2;

                                        for (; pc <= inclineTestCount; ++pc)
                                        {
                                            int px = x + NeighbourOffset_TileGenerator[n][0] * pc;
                                            int py = y + NeighbourOffset_TileGenerator[n][1] * pc;

                                            CompactSpanGrid.Cell pcell = m_spanGrid.GetCell(px, py);

                                            if (!pcell.IsValid)
                                                break;

                                            int pcount = (int)pcell.count;
                                            int pindex = (int)pcell.index;

                                            bool pCellValid = false;

                                            for (int ps = 0; ps < pcount; ++ps)
                                            {
                                                int psindex = pindex + ps;
                                                CompactSpanGrid.Span pspan = m_spanGrid.GetSpanReadOnly(psindex);

                                                int ptop = (int)(pspan.bottom + pspan.height);

                                                int pnextBottom = spaceTop;

                                                if (ps + 1 < pcount)
                                                {
                                                    CompactSpanGrid.Span pnextSpan = m_spanGrid.GetSpanReadOnly((int)pcell.index + ps + 1);
                                                    pnextBottom = (int)pnextSpan.bottom;
                                                }

                                                int dpTop = Math.Abs(ptop - ptopLast);

                                                if (dpTop <= climbableVoxelCount
                                                    && Math.Min(pnextBottomLast, pnextBottom) >= Math.Max(ptopLast, ptop) + heightVoxelCount)
                                                {
                                                    ptopMin = Math.Min(ptopMin, ptop);
                                                    ptopMax = Math.Max(ptopMax, ptop);

                                                    int pTopOffs = Math.Abs(ptop - top);
                                                    pTopOffsMax = Math.Max(pTopOffsMax, pTopOffs);

                                                    sdpTopLast = ptop - ptopLast;

                                                    ptopLast = ptop;
                                                    pnextBottomLast = pnextBottom;
                                                    dpTopLast = dpTop;

                                                    nCellValid = true;
                                                    pCellValid = true;
                                                    break;
                                                }
                                            }

                                            if (!pCellValid) // C++ just did: if (!nCellValid) break; -- but nCellValid is set inside, and pCellValid tracks the inner loop
                                                break;

                                            if (isStep)
                                            {
                                                if (pc <= stepTestCount
                                                    && pTopOffsMax > stepTestTolerance + dpTopFirst)
                                                {
                                                    stepTest = false;
                                                }
                                            }
                                            else
                                            {
                                                if (sdpTopLast > sdpTopFirst + 1
                                                    || sdpTopLast < sdpTopFirst - 1)
                                                {
                                                    break;
                                                }

                                                pVars[n] = (float)(ptopMax - ptopMin);
                                            }
                                        }

                                        if (isStep
                                            && !stepTest
                                            && pTopOffsMax > climbableVoxelCount)
                                        {
                                            neighbourTest = false;
                                        }
                                        else
                                        {
                                            pVars[n] /= pc - 1;
                                        }
                                    }
                                }

                                if (neighbourTest)
                                {
                                    float previousProbeGainSquared = pVars[axisNeighbourCount - 1] * pVars[axisNeighbourCount - 1];

                                    for (int n = 0; n < axisNeighbourCount; ++n)
                                    {
                                        float thisProbeGainSquared = pVars[n] * pVars[n];
                                        if ((thisProbeGainSquared + previousProbeGainSquared) > climbableInclineGradientSquared)
                                        {
                                            neighbourTest = false;
                                            break;
                                        }
                                        previousProbeGainSquared = thisProbeGainSquared;
                                    }
                                }

                                if (!neighbourTest)
                                {
                                    span.flags |= NotWalkable;
                                    ++nonWalkableCount;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Apply boundaries
        if (!fullyContained)
        {
            float convX = 1.0f / m_params.voxelSize.x;
            float convY = 1.0f / m_params.voxelSize.y;
            float convZ = 1.0f / m_params.voxelSize.z;

            Vec3 voxelSize = m_params.voxelSize;
            Vec3 halfVoxel = m_params.voxelSize * 0.5f;

            Vec3 bmax = aabb.max - aabb.min;

            if (m_params.exclusions != null)
            {
                for (int e = 0; e < m_params.exclusionCount; ++e)
                {
                    BoundingVolume exclusion = m_params.exclusions[e];

                    if (exclusion.Overlaps(aabb))
                    {
                        Vec3 emin = exclusion.aabb.min - aabb.min;
                        Vec3 emax = exclusion.aabb.max - aabb.min;

                        ushort xmin = (ushort)(Math.Max(0.0f, emin.x) * convX);
                        ushort xmax = (ushort)(Math.Min(bmax.x, emax.x) * convX);

                        ushort ymin = (ushort)(Math.Max(0.0f, emin.y) * convY);
                        ushort ymax = (ushort)(Math.Min(bmax.y, emax.y) * convY);

                        ushort zmin = (ushort)(Math.Max(0.0f, emin.z) * convZ);
                        ushort zmax = (ushort)(Math.Min(bmax.z, emax.z) * convZ);

                        for (ushort by2 = ymin; by2 <= ymax; ++by2)
                        {
                            int bymult = by2 * gridWidth;

                            for (ushort bx = xmin; bx <= xmax; ++bx)
                            {
                                CompactSpanGrid.Cell bcell = m_spanGrid.GetCellByIndex(bx + bymult);
                                if (bcell.IsValid)
                                {
                                    for (int bs = 0; bs < (int)bcell.count; ++bs)
                                    {
                                        ref CompactSpanGrid.Span bspan = ref m_spanGrid.GetSpan((int)bcell.index + bs);

                                        if ((bspan.flags & NotWalkable) != 0)
                                            continue;

                                        int btop = (int)(bspan.bottom + bspan.height);

                                        if (btop >= zmin && btop <= zmax)
                                        {
                                            Vec3 point = aabb.min + halfVoxel + new Vec3(bx * voxelSize.x, by2 * voxelSize.y, btop * voxelSize.z);

                                            if (exclusion.Contains(point))
                                                bspan.flags |= NotWalkable;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (m_params.boundary != null)
            {
                BoundingVolume boundary = m_params.boundary;

                for (int by2 = 0; by2 < gridHeight; ++by2)
                {
                    int bymult = by2 * gridWidth;

                    for (int bx = 0; bx < gridWidth; ++bx)
                    {
                        CompactSpanGrid.Cell bcell = m_spanGrid.GetCellByIndex(bx + bymult);

                        if (bcell.IsValid)
                        {
                            for (int bs = 0; bs < (int)bcell.count; ++bs)
                            {
                                ref CompactSpanGrid.Span bspan = ref m_spanGrid.GetSpan((int)bcell.index + bs);

                                if ((bspan.flags & NotWalkable) != 0)
                                    continue;

                                int btop = (int)(bspan.bottom + bspan.height);

                                Vec3 point = aabb.min + halfVoxel + new Vec3(bx * voxelSize.x, by2 * voxelSize.y, btop * voxelSize.z);

                                if (!boundary.Contains(point))
                                    bspan.flags |= NotWalkable;
                            }
                        }
                    }
                }
            }
        }

        CompactSpanGrid compact = new CompactSpanGrid();
        compact.CompactExcluding(m_spanGrid, NotWalkable, m_spanGrid.GetSpanCount() - nonWalkableCount);

        m_profiler.AddMemory(ProfilerMemoryUsers.CompactSpanGridMemory, compact.GetMemoryUsage());

        compact.Swap(m_spanGrid);

        m_profiler.StopTimer(ProfilerTimers.Filter);

        if ((m_params.flags & (ushort)Params.Flags.DebugInfo) != 0)
            m_spanGridFlagged.Swap(compact);
        else
            m_profiler.FreeMemory(ProfilerMemoryUsers.CompactSpanGridMemory, compact.GetMemoryUsage());
    }

    // ============================================================================
    // ComputeDistanceTransform — full two-pass sweep from C++ TileGenerator.cpp:701-830
    // ============================================================================

    protected void ComputeDistanceTransform()
    {
        m_profiler.StartTimer(ProfilerTimers.DistanceTransform);

        int gridWidth = m_spanGrid.GetWidth();
        int gridHeight = m_spanGrid.GetHeight();
        int spanCount = m_spanGrid.GetSpanCount();

        int climbableVoxelCount = (int)m_params.agent.climbableHeight;

        m_distances.Clear();
        for (int i = 0; i < spanCount; i++)
            m_distances.Add(NoLabel); // fill with max (NoLabel = 0xFFF)

        m_profiler.AddMemory(ProfilerMemoryUsers.SegmentationMemory, m_distances.Count * 2);

        const int KStraight = 2;
        const int KDiagonal = 3;

        // Down sweep (forward pass)
        int[][] downSweep = new int[][]
        {
            new[] { -1, 0, KStraight },
            new[] { -1,-1, KDiagonal },
            new[] {  0,-1, KStraight },
            new[] {  1,-1, KDiagonal },
        };

        for (int y = 0; y < gridHeight; ++y)
        {
            for (int x = 0; x < gridWidth; ++x)
            {
                CompactSpanGrid.Cell cell = m_spanGrid.GetCell(x, y);
                if (cell.IsValid)
                {
                    int index = (int)cell.index;
                    int count = (int)cell.count;

                    for (int s = 0; s < count; ++s)
                    {
                        CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index + s);
                        int top = (int)(span.bottom + span.height);
                        ushort current = m_distances[index + s];

                        ushort[] sweepValues = new ushort[4];

                        for (int p = 0; p < 4; ++p)
                        {
                            sweepValues[p] = 0;

                            int nx = x + downSweep[p][0];
                            int ny = y + downSweep[p][1];

                            int nindex = 0;
                            if (m_spanGrid.GetSpanAt(nx, ny, top, climbableVoxelCount, ref nindex))
                                sweepValues[p] = (ushort)(m_distances[nindex] + downSweep[p][2]);
                        }

                        ushort minimum = sweepValues[0];
                        for (int p = 1; p < 4; ++p)
                            minimum = Math.Min(minimum, sweepValues[p]);

                        if (minimum < current)
                            m_distances[index + s] = minimum;
                    }
                }
            }
        }

        // Up sweep (backward pass)
        int[][] upSweep = new int[][]
        {
            new[] { 1, 0, KStraight },
            new[] { 1, 1, KDiagonal },
            new[] { 0, 1, KStraight },
            new[] {-1, 1, KDiagonal },
        };

        for (int y = gridHeight - 1; y > 0; --y)
        {
            for (int x = gridWidth - 1; x >= 0; --x)
            {
                CompactSpanGrid.Cell cell = m_spanGrid.GetCell(x, y);
                if (cell.IsValid)
                {
                    int index = (int)cell.index;
                    int count = (int)cell.count;

                    for (int s = 0; s < count; ++s)
                    {
                        CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index + s);
                        int top = (int)(span.bottom + span.height);
                        ushort current = m_distances[index + s];

                        ushort[] sweepValues = new ushort[4];

                        for (int p = 0; p < 4; ++p)
                        {
                            sweepValues[p] = 0;

                            int nx = x + upSweep[p][0];
                            int ny = y + upSweep[p][1];

                            int nindex = 0;
                            if (m_spanGrid.GetSpanAt(nx, ny, top, climbableVoxelCount, ref nindex))
                                sweepValues[p] = (ushort)(m_distances[nindex] + upSweep[p][2]);
                        }

                        ushort minimum = sweepValues[0];
                        for (int p = 1; p < 4; ++p)
                            minimum = Math.Min(minimum, sweepValues[p]);

                        if (minimum < current)
                            m_distances[index + s] = minimum;
                    }
                }
            }
        }

        m_profiler.StopTimer(ProfilerTimers.DistanceTransform);
    }

    // ============================================================================
    // BlurDistanceTransform — from C++ TileGenerator.cpp:832-895
    // ============================================================================

    protected void BlurDistanceTransform()
    {
        if (m_params.blurAmount == 0)
            return;

        m_profiler.StartTimer(ProfilerTimers.Blur);

        int threshold = 1;
        int gridWidth = m_spanGrid.GetWidth();
        int gridHeight = m_spanGrid.GetHeight();
        int climbableVoxelCount = (int)m_params.agent.climbableHeight;

        m_labels.Clear();
        for (int i = 0; i < m_distances.Count; i++)
            m_labels.Add(0);

        for (int iter = 0; iter < m_params.blurAmount; ++iter)
        {
            for (int y = 0; y < gridHeight; ++y)
            {
                for (int x = 0; x < gridWidth; ++x)
                {
                    CompactSpanGrid.Cell cell = m_spanGrid.GetCell(x, y);
                    if (cell.IsValid)
                    {
                        int index = (int)cell.index;
                        int count = (int)cell.count;

                        for (int s = 0; s < count; ++s)
                        {
                            CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index + s);
                            int orig = m_distances[index + s];

                            if (orig > threshold)
                            {
                                int top = (int)(span.bottom + span.height);
                                int accum = orig;

                                for (int n = 0; n < 8; ++n)
                                {
                                    int nx = x + NeighbourOffset_TileGenerator[n][0];
                                    int ny = y + NeighbourOffset_TileGenerator[n][1];

                                    int nindex = 0;
                                    if (m_spanGrid.GetSpanAt(nx, ny, top, climbableVoxelCount, ref nindex))
                                        accum += m_distances[nindex];
                                    else
                                        accum += orig;
                                }

                                int c = (accum + 5) / 9;
                                m_labels[index + s] = (ushort)c;
                            }
                            else
                            {
                                m_labels[index + s] = (ushort)orig;
                            }
                        }
                    }
                }
            }

            if ((iter & 1) == 0)
            {
                // swap labels and distances
                var tmp = m_labels;
                m_labels = m_distances;
                m_distances = tmp;
            }
        }

        m_profiler.StopTimer(ProfilerTimers.Blur);
    }

    // ============================================================================
    // PaintBorder — from C++ TileGenerator.cpp:1031-1106
    // ============================================================================

    protected void PaintBorder(int borderH, int borderV)
    {
        int width = m_spanGrid.GetWidth();
        int height = m_spanGrid.GetHeight();

        if (borderH > 0)
        {
            for (int y = 0; y < height; ++y)
            {
                for (int b = 0; b < 2; ++b)
                {
                    int xoffset = b * (width - borderH);

                    for (int x = 0; x < borderH; ++x)
                    {
                        CompactSpanGrid.Cell cell = m_spanGrid.GetCell(x + xoffset, y);
                        if (cell.IsValid)
                        {
                            int index = (int)cell.index;
                            int count = (int)cell.count;

                            for (int s = 0; s < count; ++s)
                                m_labels[index + s] |= BorderLabelH;
                        }
                    }
                }
            }

            int hwidth = width - borderH;

            for (int b = 0; b < 2; ++b)
            {
                int yoffset = b * (height - borderH);

                for (int y = 0; y < borderH; ++y)
                {
                    for (int x = borderH; x < hwidth; ++x)
                    {
                        CompactSpanGrid.Cell cell = m_spanGrid.GetCell(x, y + yoffset);
                        if (cell.IsValid)
                        {
                            int index = (int)cell.index;
                            int count = (int)cell.count;

                            for (int s = 0; s < count; ++s)
                                m_labels[index + s] |= BorderLabelH;
                        }
                    }
                }
            }
        }

        if (borderV > 0)
        {
            int maxTop = borderV + m_top;

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    CompactSpanGrid.Cell cell = m_spanGrid.GetCell(x, y);
                    if (cell.IsValid)
                    {
                        int index = (int)cell.index;
                        int count = (int)cell.count;

                        for (int s = 0; s < count; ++s)
                        {
                            CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index + s);
                            int spanTop = (int)(span.bottom + span.height);

                            if (spanTop < borderV || spanTop >= maxTop)
                                m_labels[index + s] |= BorderLabelV;
                        }
                    }
                }
            }
        }
    }

    // ============================================================================
    // GetPaintVal — from C++ TileGenerator.cpp:1495-1504
    // ============================================================================

    protected ushort GetPaintVal(int x, int y, int z, int index, int borderH, int borderV, int erosion)
    {
        if (m_distances[index] < erosion)
            return BadPaint;

        if ((m_labels[index] & (BorderLabelH | BorderLabelV)) != 0)
            return BadPaint;

        return OkPaintStart;
    }

    // ============================================================================
    // CalcPaintValues — from C++ TileGenerator.cpp:1506-1537
    // ============================================================================

    protected void CalcPaintValues()
    {
        int gridWidth = m_spanGrid.GetWidth();
        int gridHeight = m_spanGrid.GetHeight();

        m_paint.Clear();
        for (int i = 0; i < m_distances.Count; i++)
            m_paint.Add(NoPaint);

        int borderH = BorderSizeH();
        int borderV = BorderSizeV();
        int erosion = (m_params.flags & (ushort)Params.Flags.NoErosion) != 0 ? 0 : (int)(m_params.agent.radius << 1);
        int climbableVoxelCount = (int)m_params.agent.climbableHeight;

        for (int y = borderV; y < gridHeight - borderV; ++y)
        {
            for (int x = borderH; x < gridWidth - borderH; ++x)
            {
                int spanGridIndex = y * gridWidth + x;
                CompactSpanGrid.Cell cell = m_spanGrid.GetCellByIndex(spanGridIndex);
                for (int s = 0; s < (int)cell.count; ++s)
                {
                    int index = (int)cell.index + s;
                    CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index);
                    m_paint[index] = GetPaintVal(x, y, (int)(span.bottom + span.height), index, borderH, borderV, erosion);
                }
            }
        }
    }

    // ============================================================================
    // AssessNeighbour — from C++ TileGenerator.cpp:1318-1327
    // ============================================================================

    protected void AssessNeighbour(ref NeighbourInfo info, int erosion, int climbableVoxelCount)
    {
        int idx = 0;
        info.isValid = m_spanGrid.GetSpanAt(info.pos.X, info.pos.Y, info.top, climbableVoxelCount, ref idx);
        if (info.isValid)
        {
            info.index = idx;
            CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(idx);
            info.top = (int)(span.bottom + span.height);
            info.label = m_labels[idx];
            info.paint = m_paint[idx];
        }
    }

    // ============================================================================
    // AddContourVertex — from C++ TileGenerator.cpp:1143-1166
    // ============================================================================

    protected void AddContourVertex(in ContourVertex contourVertex, Region region, List<ContourVertex> contour)
    {
        if (contour.Count < 2 || !ContourVertexRemovable(contour[contour.Count - 1]))
        {
            contour.Add(contourVertex);
        }
        else
        {
            ContourVertex middle = contour[contour.Count - 1];
            ContourVertex left = contour[contour.Count - 2];

            real_t distSq = DistVertexToLineSq(middle.x, middle.y, middle.z, left.x, left.y, left.z,
                contourVertex.x, contourVertex.y, contourVertex.z);

            if (distSq <= AddContourVertexThreshold)
                contour[contour.Count - 1] = contourVertex;
            else
                contour.Add(contourVertex);
        }

        if ((contourVertex.flags & ContourVertex.FlagTileBoundary) != 0)
            region.flags |= (int)Region.RegionFlags.TileBoundary;

        if ((contourVertex.flags & ContourVertex.FlagTileBoundaryV) != 0)
            region.flags |= (int)Region.RegionFlags.TileBoundaryV;
    }

    // ============================================================================
    // GatherSurroundingInfo — from C++ TileGenerator.cpp:1168-1215
    // ============================================================================

    protected bool GatherSurroundingInfo(Vec2i vertex, Vec2i direction, int top,
        int climbableVoxelCount, ref int height, ref SurroundingSpanInfo left, ref SurroundingSpanInfo front,
        ref SurroundingSpanInfo frontLeft)
    {
        Vec2i external = new Vec2i(vertex.X + direction.X, vertex.Y + direction.Y);
        bool result = false;

        height = top;

        int index = 0;
        if (m_spanGrid.GetSpanAt(external.X, external.Y, top, climbableVoxelCount, ref index))
        {
            CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index);
            int spanTop = (int)(span.bottom + span.height);
            if (spanTop > height) height = spanTop;

            front.label = m_labels[index];
            front.flags = (int)span.flags;
            front.index = index;
            result = true;
        }

        Vec2i dirRot = direction.rot90ccw();
        external = new Vec2i(vertex.X + direction.X + dirRot.X, vertex.Y + direction.Y + dirRot.Y);
        if (m_spanGrid.GetSpanAt(external.X, external.Y, top, climbableVoxelCount, ref index))
        {
            CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index);
            int spanTop = (int)(span.bottom + span.height);
            if (spanTop > height) height = spanTop;

            frontLeft.label = m_labels[index];
            frontLeft.flags = (int)span.flags;
            frontLeft.index = index;
            result = true;
        }

        external = new Vec2i(vertex.X + dirRot.X, vertex.Y + dirRot.Y);
        if (m_spanGrid.GetSpanAt(external.X, external.Y, top, climbableVoxelCount, ref index))
        {
            CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index);
            int spanTop = (int)(span.bottom + span.height);
            if (spanTop > height) height = spanTop;

            left.label = m_labels[index];
            left.flags = (int)span.flags;
            left.index = index;
            result = true;
        }

        return result;
    }

    // ============================================================================
    // DetermineContourVertex — from C++ TileGenerator.cpp:1217-1316
    // ============================================================================

    protected void DetermineContourVertex(Vec2i vertex, Vec2i direction, int top,
        int climbableVoxelCount, ref ContourVertex contourVertex)
    {
        int xoffs = ((direction.X == 1) || (direction.Y == -1)) ? 1 : 0;
        int yoffs = ((direction.Y == 1) || (direction.X == 1)) ? 1 : 0;

        int cx = vertex.X + xoffs;
        int cy = vertex.Y + yoffs;
        int cz = top;

        bool internalBorderV = false;
        {
            int index = 0;
            if (m_spanGrid.GetSpanAt(vertex.X, vertex.Y, top, climbableVoxelCount, ref index))
                internalBorderV = (m_labels[index] & BorderLabelV) != 0;
        }

        SurroundingSpanInfo front = new SurroundingSpanInfo(NoLabel, -1, NotWalkable);
        SurroundingSpanInfo frontLeft = new SurroundingSpanInfo(NoLabel, -1, NotWalkable);
        SurroundingSpanInfo left = new SurroundingSpanInfo(NoLabel, -1, NotWalkable);

        int borderV = BorderSizeV();
        GatherSurroundingInfo(vertex, direction, top, climbableVoxelCount, ref cz, ref left, ref front, ref frontLeft);
        if (cz > borderV + m_top) cz = borderV + m_top;

        int flags = 0;

        int erosion = (m_params.flags & (ushort)Params.Flags.NoErosion) != 0 ? 0 : (int)(m_params.agent.radius * 2);

        int walkableBit = 0;
        walkableBit = (((frontLeft.flags & NotWalkable) == 0) && frontLeft.index >= 0 && (m_distances[frontLeft.index] >= erosion)) ? 1 : 0;
        walkableBit |= (((left.flags & NotWalkable) == 0) && left.index >= 0 && (m_distances[left.index] >= erosion) ? 1 : 0) << 1;
        walkableBit |= (((front.flags & NotWalkable) == 0) && front.index >= 0 && (m_distances[front.index] >= erosion) ? 1 : 0) << 2;

        int borderBitH = 0;
        borderBitH = ((frontLeft.label & BorderLabelH) != 0) ? 1 : 0;
        borderBitH |= (((left.label & BorderLabelH) != 0) ? 1 : 0) << 1;
        borderBitH |= (((front.label & BorderLabelH) != 0) ? 1 : 0) << 2;

        int borderBitV = 0;
        borderBitV = ((frontLeft.label & BorderLabelV) != 0) ? 1 : 0;
        borderBitV |= (((left.label & BorderLabelV) != 0) ? 1 : 0) << 1;
        borderBitV |= (((front.label & BorderLabelV) != 0) ? 1 : 0) << 2;

        int borderBit = borderBitH | borderBitV;

        NeighbourClassification lclass = ClassifyNeighbour(in left, erosion, BorderLabelV | BorderLabelH);
        NeighbourClassification flclass = ClassifyNeighbour(in frontLeft, erosion, BorderLabelV | BorderLabelH);
        NeighbourClassification fclass = ClassifyNeighbour(in front, erosion, BorderLabelV | BorderLabelH);

        // Horizontal border
        {
            if (IsCornerVertex(cx, cy))
                flags |= ContourVertex.FlagTileBoundary;
            else
            {
                bool boundary = IsBoundaryVertex(cx, cy);

                if (boundary)
                {
                    bool frontBoundary = IsBoundaryCell(vertex.X + direction.X, vertex.Y + direction.Y)
                        || IsBorderCell(vertex.X + direction.X, vertex.Y + direction.Y);
                    bool connection = (borderBitH == 7) && (walkableBit != 0);

                    if (frontBoundary && connection)
                        flags |= ContourVertex.FlagTileBoundary;

                    if (CornerTable[(int)lclass][(int)flclass][(int)fclass] != 0 || ((borderBit == 7) && (borderBitH != 0) && (borderBitV != 0)))
                        flags |= ContourVertex.FlagUnremovable;
                }
            }
        }

        // Vertical border
        {
            if (borderBitV != 0 || internalBorderV)
            {
                flags |= ContourVertex.FlagTileBoundaryV;

                if (!internalBorderV)
                {
                    if (CornerTable[(int)lclass][(int)flclass][(int)fclass] != 0 || ((borderBit == 7) && (borderBitH != 0) && (borderBitV != 0)))
                        flags |= ContourVertex.FlagUnremovable;
                }
            }

            if ((flags & ContourVertex.FlagTileBoundaryV) != 0)
            {
                if (cz < borderV + m_top)
                    cz = borderV;
            }
        }

        contourVertex.x = (ushort)cx;
        contourVertex.y = (ushort)cy;
        contourVertex.z = (ushort)cz;
        contourVertex.flags = (ushort)flags;
    }

    // ============================================================================
    // TraceContour — from C++ TileGenerator.cpp:1329-1395
    // ============================================================================

    protected void TraceContour(TracerPath path, Tracer start, int erosion, int climbableVoxelCount, in NeighbourInfoRequirements contourReq)
    {
        Tracer tracer = start;
        path.steps.Clear();
        path.steps.Capacity = Math.Max(path.steps.Capacity, 2048);
        path.turns = 0;

        do
        {
            tracer.bPinchPoint = false;

            NeighbourInfo left_info = new NeighbourInfo(tracer.GetLeft());
            NeighbourInfo frontLeft_info = new NeighbourInfo(tracer.GetFrontLeft());
            NeighbourInfo front_info = new NeighbourInfo(tracer.GetFront());

            AssessNeighbour(ref left_info, erosion, climbableVoxelCount);
            AssessNeighbour(ref frontLeft_info, erosion, climbableVoxelCount);
            AssessNeighbour(ref front_info, erosion, climbableVoxelCount);

            if (left_info.Check(in contourReq))
            {
                // Shouldn't ever happen
                Debug.Assert(false);
                tracer.SetPos(left_info);
                tracer.indexIn = left_info.index;
                tracer.indexOut = left_info.index;
            }
            else if (front_info.Check(in contourReq))
            {
                if (frontLeft_info.Check(in contourReq))
                {
                    // Turn left
                    tracer.SetPos(frontLeft_info);
                    tracer.TurnLeft();
                    tracer.indexIn = frontLeft_info.index;
                    tracer.indexOut = left_info.index;
                    path.turns--;
                }
                else
                {
                    // Go forward
                    tracer.SetPos(front_info);
                    tracer.indexIn = front_info.index;
                    tracer.indexOut = frontLeft_info.index;
                }
            }
            else
            {
                if (frontLeft_info.Check(in contourReq))
                {
                    tracer.bPinchPoint = true;
                }

                // Turn right
                tracer.TurnRight();
                tracer.indexOut = front_info.index;
                path.turns++;
            }

            path.steps.Add(tracer);

        } while (tracer != start && path.steps.Count < 65536);
    }

    // ============================================================================
    // LabelTracerPath — from C++ TileGenerator.cpp:1397-1438
    // ============================================================================

    protected int LabelTracerPath(TracerPath path, int climbableVoxelCount, Region region, List<ContourVertex> contour,
        ushort internalLabel, ushort internalLabelFlags, ushort externalLabel)
    {
        int numSteps = path.steps.Count;
        contour.Capacity = Math.Max(contour.Capacity, numSteps);

        for (int i = numSteps - 1, j = 0; j < numSteps; i = j++)
        {
            Tracer curr = path.steps[i];
            Tracer next = path.steps[j];

            ContourVertex vertex = new ContourVertex();
            Vec2i currPos = new Vec2i(curr.pos.x, curr.pos.y);
            DetermineContourVertex(currPos, curr.GetDir(), curr.pos.z, climbableVoxelCount, ref vertex);

            bool bImportantVert = (m_paint[curr.indexOut] != m_paint[next.indexOut]);
            if (bImportantVert)
            {
                vertex.flags |= ContourVertex.FlagUnremovable;
            }
            if (next.bPinchPoint)
            {
                vertex.flags |= ContourVertex.FlagUnremovable;
            }
            AddContourVertex(in vertex, region, contour);

            // Apply labels
            if (internalLabel != NoLabel)
                m_labels[curr.indexIn] = internalLabel;
            m_labels[curr.indexIn] |= internalLabelFlags;

            if (externalLabel != NoLabel)
            {
                if ((m_labels[curr.indexOut] & ExternalContour) == 0)
                    m_labels[curr.indexOut] = externalLabel;
            }
        }

        TidyUpContourEnd(contour);
        return numSteps;
    }

    // ============================================================================
    // TidyUpContourEnd — from C++ TileGenerator.cpp:1467-1493
    // ============================================================================

    protected void TidyUpContourEnd(List<ContourVertex> contour)
    {
        if (contour.Count > 2)
        {
            ContourVertex left = contour[contour.Count - 2];
            ContourVertex middle = contour[contour.Count - 1];
            ContourVertex right = contour[0];

            if ((DistVertexToLineSq(middle.x, middle.y, middle.z, left.x, left.y, left.z,
                right.x, right.y, right.z) <= AddContourVertexThreshold) && ContourVertexRemovable(in middle))
                contour.RemoveAt(contour.Count - 1);
        }

        if (contour.Count > 2)
        {
            ContourVertex left = contour[contour.Count - 1];
            ContourVertex middle = contour[0];
            ContourVertex right = contour[1];

            if ((DistVertexToLineSq(middle.x, middle.y, middle.z, left.x, left.y, left.z,
                right.x, right.y, right.z) <= AddContourVertexThreshold) && ContourVertexRemovable(in middle))
            {
                contour[0] = left;
                contour.RemoveAt(contour.Count - 1);
            }
        }
    }

    // ============================================================================
    // ExtractContours — from C++ TileGenerator.cpp:1539-1745
    // ============================================================================

    protected int ExtractContours()
    {
        m_profiler.StartTimer(ProfilerTimers.ContourExtraction);

        int gridWidth = m_spanGrid.GetWidth();
        int gridHeight = m_spanGrid.GetHeight();

        m_labels.Clear();
        for (int i = 0; i < m_distances.Count; i++)
            m_labels.Add(NoLabel);

        m_profiler.AddMemory(ProfilerMemoryUsers.SegmentationMemory, m_labels.Count * 2);

        int borderH = BorderSizeH();
        int borderV = BorderSizeV();

        PaintBorder(borderH, borderV);

        CalcPaintValues();

        int regionCount = 0;
        m_regions.Capacity = Math.Max(m_regions.Capacity, 128);

        TracerPath path = new TracerPath();

        int erosion = (m_params.flags & (ushort)Params.Flags.NoErosion) != 0 ? 0 : (int)(m_params.agent.radius << 1);
        int climbableVoxelCount = (int)m_params.agent.climbableHeight;

        for (int y = borderV; y < gridHeight - borderV; ++y)
        {
            for (int x = borderH; x < gridWidth - borderH; ++x)
            {
                int spanGridIndex = y * gridWidth + x;
                CompactSpanGrid.Cell cell = m_spanGrid.GetCellByIndex(spanGridIndex);
                for (int s = 0; s < (int)cell.count; ++s)
                {
                    int index = (int)cell.index + s;
                    ushort label = m_labels[index];
                    ushort labelsafe = (ushort)(label & NoLabel);

                    if (labelsafe != NoLabel)
                        continue;

                    ushort paint = m_paint[index];

                    CompactSpanGrid.Span span = m_spanGrid.GetSpanReadOnly(index);
                    int top = (int)(span.bottom + span.height);

                    NeighbourInfo prev = new NeighbourInfo(new Vec2i(x - 1, y), top);
                    AssessNeighbour(ref prev, erosion, climbableVoxelCount);

                    ushort prevLabelSafe = (ushort)(prev.label & NoLabel);

                    bool walkable = (paint >= OkPaintStart);
                    bool prevwalkable = (prev.paint >= OkPaintStart);
                    bool bothwalkable = (walkable && prevwalkable);
                    bool paintcontinuation = (paint == prev.paint);

                    Tracer startTracer = new Tracer();
                    startTracer.pos = new Vec3i(x, y, top);
                    startTracer.dir = (int)TracerDir.N;
                    startTracer.indexIn = index;
                    startTracer.indexOut = prev.index;

                    if (bothwalkable)
                    {
                        if (paintcontinuation)
                        {
                            if (prevLabelSafe < m_regions.Count)
                            {
                                m_labels[index] = prevLabelSafe;
                                Region region = m_regions[prevLabelSafe];
                                ++region.spanCount;
                            }
                        }
                        else
                        {
                            NeighbourInfoRequirements contourReq = new NeighbourInfoRequirements();
                            contourReq.paint = paint;

                            TraceContour(path, startTracer, erosion, climbableVoxelCount, in contourReq);
                            if (path.turns > 0)
                            {
                                CacheTracerPath(path);

                                ushort newLabel = (ushort)regionCount;
                                ++regionCount;
                                m_regions.Add(new Region());

                                Region region = m_regions[m_regions.Count - 1];

                                region.spanCount += LabelTracerPath(path, climbableVoxelCount, region, region.contour, newLabel, ExternalContour, NoLabel);

                                if ((prev.label & ExternalContour) == 0 && (label & InternalContour) == 0)
                                {
                                    if (prevLabelSafe < m_regions.Count)
                                    {
                                        Region holeRegion = m_regions[prevLabelSafe];
                                        holeRegion.holes.Add(new List<ContourVertex>());

                                        NeighbourInfoRequirements holeReq = new NeighbourInfoRequirements();
                                        holeReq.notPaint = prev.paint;

                                        TraceContour(path, startTracer, erosion, climbableVoxelCount, in holeReq);
                                        LabelTracerPath(path, climbableVoxelCount, holeRegion, holeRegion.holes[holeRegion.holes.Count - 1], NoLabel, InternalContour, prevLabelSafe);

                                        CacheTracerPath(path);
                                    }
                                }
                            }
                        }
                    }
                    else if (walkable)
                    {
                        NeighbourInfoRequirements contourReq = new NeighbourInfoRequirements();
                        contourReq.paint = paint;
                        TraceContour(path, startTracer, erosion, climbableVoxelCount, in contourReq);

                        if (path.turns > 0)
                        {
                            CacheTracerPath(path);

                            ushort newLabel = (ushort)regionCount;
                            ++regionCount;
                            m_regions.Add(new Region());

                            Region region = m_regions[m_regions.Count - 1];

                            region.spanCount += LabelTracerPath(path, climbableVoxelCount, region, region.contour, newLabel, ExternalContour, NoLabel);
                        }
                    }
                    else if (prevwalkable)
                    {
                        if ((prev.label & ExternalContour) == 0 && (label & InternalContour) == 0)
                        {
                            if (prevLabelSafe < m_regions.Count)
                            {
                                Region holeRegion = m_regions[prevLabelSafe];
                                holeRegion.holes.Add(new List<ContourVertex>());

                                NeighbourInfoRequirements holeReq = new NeighbourInfoRequirements();
                                holeReq.notPaint = prev.paint;

                                TraceContour(path, startTracer, erosion, climbableVoxelCount, in holeReq);
                                LabelTracerPath(path, climbableVoxelCount, holeRegion, holeRegion.holes[holeRegion.holes.Count - 1], NoLabel, InternalContour, prevLabelSafe);

                                CacheTracerPath(path);
                            }
                        }
                    }
                }
            }
        }

        m_profiler.StopTimer(ProfilerTimers.ContourExtraction);

        m_profiler.AddMemory(ProfilerMemoryUsers.RegionMemory, m_regions.Count * 64); // approximate

        if ((m_params.flags & (ushort)Params.Flags.DebugInfo) == 0)
        {
            m_labels.Clear();
            m_labels.TrimExcess();

            m_distances.Clear();
            m_distances.TrimExcess();
        }

        return regionCount;
    }

    // ============================================================================
    // FilterBadRegions — from C++ TileGenerator.cpp:1771-1784
    // ============================================================================

    protected void FilterBadRegions(int minSpanCount)
    {
        for (int i = 0; i < m_regions.Count; ++i)
        {
            Region region = m_regions[i];

            if (((region.flags & (int)Region.RegionFlags.TileBoundary) == 0)
                && ((region.flags & (int)Region.RegionFlags.TileBoundaryV) == 0)
                && (region.spanCount > 0 && region.spanCount <= minSpanCount))
            {
                region.Swap(new Region());
            }
        }
    }

    // ============================================================================
    // SimplifyContour — from C++ TileGenerator.cpp:1786-1994
    // ============================================================================

    protected bool SimplifyContour(List<ContourVertex> contour, real_t tolerance2DSq, real_t tolerance3DSq,
        List<PolygonVertex> poly)
    {
        const int MaxSimplifiedCount = 2048;
        ushort[] simplified = new ushort[MaxSimplifiedCount];
        int simplifiedCount = 0;
        int contourSize = contour.Count;
        bool boundary = (contour[contourSize - 1].flags & ContourVertex.FlagTileBoundaryV) != 0;

        for (int i = 0; i < contourSize; ++i)
        {
            ContourVertex v = contour[i];

            if ((v.flags & ContourVertex.FlagTileBoundaryV) != 0)
            {
                if (!boundary || !ContourVertexRemovable(in v))
                    simplified[simplifiedCount++] = (ushort)i;

                boundary = true;
            }
            else
            {
                if (boundary && i > 0 && simplified[simplifiedCount - 1] != (i - 1))
                    simplified[simplifiedCount++] = (ushort)(i - 1);

                if (!ContourVertexRemovable(in v))
                    simplified[simplifiedCount++] = (ushort)i;

                boundary = false;
            }
        }

        if (simplifiedCount > 0 && simplified[simplifiedCount - 1] != (contourSize - 1))
        {
            if ((contour[contourSize - 1].flags & ContourVertex.FlagTileBoundaryV) != 0
                && ((contour[0].flags & ContourVertex.FlagTileBoundaryV) == 0))
            {
                simplified[simplifiedCount++] = (ushort)(contourSize - 1);
            }
        }

        if (simplifiedCount == 0)
        {
            Vec3i minVal = new Vec3i(int.MaxValue, int.MaxValue, int.MaxValue);
            int minVertex = 0;

            Vec3i maxVal = new Vec3i(int.MinValue, int.MinValue, int.MinValue);
            int maxVertex = 0;

            for (int i = 0; i < contourSize; ++i)
            {
                ContourVertex v = contour[i];

                if ((v.x < minVal.x) || ((v.x == minVal.x) && (v.y > minVal.y)))
                {
                    minVertex = i;
                    minVal = new Vec3i(v.x, v.y, v.z);
                }

                if ((v.x > maxVal.x) || ((v.x == maxVal.x) && (v.y > minVal.y)))
                {
                    maxVertex = i;
                    maxVal = new Vec3i(v.x, v.y, v.z);
                }
            }

            simplified[simplifiedCount++] = (ushort)minVertex;
            simplified[simplifiedCount++] = (ushort)maxVertex;
        }

        // Douglas-Peucker subdivision
        for (int s0 = 0; s0 < simplifiedCount; )
        {
            int s1 = (s0 + 1) % simplifiedCount;
            int i0 = simplified[s0];
            int i1 = simplified[s1];
            int last = (i0 < i1) ? i1 : contourSize + i1;

            ContourVertex v0 = contour[i0];
            ContourVertex v1 = contour[i1];

            real_t dmax2DSq = real_t.min();
            real_t dmax3DSq = real_t.min();
            int index2D = 0;
            int index3D = 0;

            if (v0 < v1)
            {
                for (int v = i0 + 1; v < last; ++v)
                {
                    int vi = v % contourSize;
                    ContourVertex vp = contour[vi];
                    real_t d3DSq = DistVertexToLineSq(vp.x, vp.y, vp.z, v0.x, v0.y, v0.z, v1.x, v1.y, v1.z);
                    real_t d2DSq = DistVertexToLineSq(vp.x, vp.y, v0.x, v0.y, v1.x, v1.y);

                    if (d2DSq > dmax2DSq)
                    {
                        index2D = vi;
                        dmax2DSq = d2DSq;
                    }

                    if (d3DSq > dmax3DSq)
                    {
                        index3D = vi;
                        dmax3DSq = d3DSq;
                    }
                }
            }
            else
            {
                for (int v = last - 1; v > i0; --v)
                {
                    int vi = v % contourSize;
                    ContourVertex vp = contour[vi];
                    real_t d3DSq = DistVertexToLineSq(vp.x, vp.y, vp.z, v1.x, v1.y, v1.z, v0.x, v0.y, v0.z);
                    real_t d2DSq = DistVertexToLineSq(vp.x, vp.y, v1.x, v1.y, v0.x, v0.y);

                    if (d2DSq > dmax2DSq)
                    {
                        index2D = vi;
                        dmax2DSq = d2DSq;
                    }

                    if (d3DSq > dmax3DSq)
                    {
                        index3D = vi;
                        dmax3DSq = d3DSq;
                    }
                }
            }

            int insertIndex = -1;
            if (dmax3DSq >= tolerance3DSq)
                insertIndex = index3D;
            else if (dmax2DSq >= tolerance2DSq)
                insertIndex = index2D;

            if (insertIndex >= 0)
            {
                Debug.Assert(simplifiedCount + 1 < MaxSimplifiedCount);
                if (simplifiedCount + 1 == MaxSimplifiedCount)
                    break;

                for (int k = 0, ii = s0 + 1; ii < simplifiedCount; ++ii, ++k)
                    simplified[simplifiedCount - k] = simplified[simplifiedCount - k - 1];

                simplified[s0 + 1] = (ushort)insertIndex;
                ++simplifiedCount;

                continue;
            }

            ++s0;
        }

        // Remove degenerate verts for [a,b,c] where: a==c or a==b
        {
            int a = simplifiedCount - 2;
            int b = simplifiedCount - 1;
            int c = 0;
            while (simplifiedCount > 2 && c < simplifiedCount)
            {
                ushort asIdx = simplified[a];
                ushort bsIdx = simplified[b];
                ushort csIdx = simplified[c];

                ContourVertex av = contour[asIdx];
                ContourVertex bv = contour[bsIdx];
                ContourVertex cv = contour[csIdx];

                if (asIdx == bsIdx || ((av.x == bv.x) && (av.y == bv.y) && (av.z == bv.z)))
                {
                    // Remove b
                    if (b + 1 < simplifiedCount)
                    {
                        int numShift = simplifiedCount - (b + 1);
                        Array.Copy(simplified, b + 1, simplified, b, numShift);
                    }
                    simplifiedCount -= 1;
                    b = (a + 1) % simplifiedCount;
                    c = (a + 2) % simplifiedCount;
                }
                else if (asIdx == csIdx || ((av.x == cv.x) && (av.y == cv.y) && (av.z == cv.z)))
                {
                    // Remove b and c
                    if (b + 2 < simplifiedCount)
                    {
                        int numShift = simplifiedCount - (b + 2);
                        Array.Copy(simplified, b + 2, simplified, b, numShift);
                    }
                    simplifiedCount -= 2;
                    b = (a + 1) % simplifiedCount;
                    c = (a + 2) % simplifiedCount;
                }
                else
                {
                    a = b;
                    b = c++;
                }
            }
        }

        if (simplifiedCount > 2)
        {
            List<PolygonVertex> spoly = new List<PolygonVertex>(simplifiedCount);

            for (int i = 0; i < simplifiedCount; ++i)
            {
                ContourVertex cv = contour[simplified[i]];
                spoly.Add(new PolygonVertex(cv.x, cv.y, cv.z));
            }

            poly.Clear();
            poly.AddRange(spoly);

            return true;
        }

        return false;
    }

    // ============================================================================
    // SimplifyContours — from C++ TileGenerator.cpp:1996-2086
    // ============================================================================

    protected void SimplifyContours()
    {
        m_profiler.StartTimer(ProfilerTimers.Simplification);

        real_t tolerance2DSq = new real_t(7);
        real_t tolerance3DSq = new real_t(11);

        int polygonCount = 0;
        for (int r = 0; r < m_regions.Count; ++r)
        {
            Region region = m_regions[r];
            if (region.spanCount > 0)
                polygonCount++;
        }

        m_polygons.Capacity = Math.Max(m_polygons.Capacity, polygonCount);

        for (int r = 0; r < m_regions.Count; ++r)
        {
            Region region = m_regions[r];
            List<ContourVertex> contour = region.contour;

            if (contour.Count < 3)
                continue;

            Polygon poly = new Polygon();

            if (!SimplifyContour(region.contour, tolerance2DSq, tolerance3DSq, poly.contour))
                continue;

            m_polygons.Add(poly);

            if (region.holes.Count == 0)
                continue;

            for (int h = 0; h < region.holes.Count; ++h)
            {
                Hole hole = new Hole();

                if (!SimplifyContour(region.holes[h], tolerance2DSq, tolerance3DSq, hole.verts))
                    continue;

                int holeSize = hole.verts.Count;
                Vec2i avg = new Vec2i(0, 0);
                for (int hvi = 0; hvi < holeSize; ++hvi)
                {
                    avg = new Vec2i(avg.X + hole.verts[hvi].x, avg.Y + hole.verts[hvi].y);
                }
                hole.center = new Vec2i(avg.X / holeSize, avg.Y / holeSize);
                hole.rad = 0;
                for (int hvi = 0; hvi < holeSize; ++hvi)
                {
                    Vec2i hv = new Vec2i(hole.verts[hvi].x, hole.verts[hvi].y);
                    Vec2i diff = new Vec2i(hv.X - hole.center.X, hv.Y - hole.center.Y);
                    int vertDist = int_ceil(sqrt_tpl((float)Vec2i_len2(diff)));
                    hole.rad = Math.Max(hole.rad, vertDist);
                }

                poly.holes.Add(hole);
            }
        }

        m_profiler.StopTimer(ProfilerTimers.Simplification);

        if ((m_params.flags & (ushort)Params.Flags.DebugInfo) == 0)
        {
            m_regions.Clear();
            m_regions.TrimExcess();
        }

        m_profiler.AddStat(ProfilerStats.PolygonCount, m_polygons.Count);
    }

    // ============================================================================
    // IsReflex helper — from C++ TileGenerator.cpp:2088-2116
    // ============================================================================

    private static bool IsReflex(int vertex, List<PolygonVertex> vertices, int vertexCount)
    {
        int i0 = vertex > 0 ? vertex - 1 : vertexCount - 1;
        int i1 = vertex;
        int i2 = (vertex + 1) % vertexCount;

        PolygonVertex v0 = vertices[i0];
        PolygonVertex v1 = vertices[i1];
        PolygonVertex v2 = vertices[i2];

        int area = ((int)v1.x - (int)v0.x) * ((int)v2.y - (int)v0.y) - ((int)v1.y - (int)v0.y) * ((int)v2.x - (int)v0.x);
        return area > 0;
    }

    private static bool IsReflex(int vertex, List<PolygonVertex> vertices, ushort[] indices, int indexCount)
    {
        int i0 = vertex > 0 ? vertex - 1 : indexCount - 1;
        int i1 = vertex;
        int i2 = (vertex + 1) % indexCount;

        PolygonVertex v0 = vertices[indices[i0]];
        PolygonVertex v1 = vertices[indices[i1]];
        PolygonVertex v2 = vertices[indices[i2]];

        int area = ((int)v1.x - (int)v0.x) * ((int)v2.y - (int)v0.y) - ((int)v1.y - (int)v0.y) * ((int)v2.x - (int)v0.x);
        return area > 0;
    }

    // ============================================================================
    // IsEar helper — from C++ TileGenerator.cpp:2118-2203
    // ============================================================================

    private static bool IsEar(int vertex, List<PolygonVertex> vertices, int vertexCount, int agentHeight)
    {
        int i0 = vertex > 0 ? vertex - 1 : vertexCount - 1;
        int i1 = vertex;
        int i2 = (vertex + 1) % vertexCount;

        PolygonVertex v0 = vertices[i0];
        PolygonVertex v1 = vertices[i1];
        PolygonVertex v2 = vertices[i2];

        int minZ = Math.Min(Math.Min(v0.z, v1.z), v2.z);
        int maxZ = Math.Max(Math.Max(v0.z, v1.z), v2.z);

        int x01 = v0.x - v1.x;
        int y01 = v0.y - v1.y;
        int x12 = v1.x - v2.x;
        int y12 = v1.y - v2.y;
        int x20 = v2.x - v0.x;
        int y20 = v2.y - v0.y;

        for (int k = 0; k < vertexCount; ++k)
        {
            PolygonVertex cv = vertices[k];

            if (cv.z > maxZ + agentHeight || cv.z < minZ - 1)
                continue;

            bool e0 = ((cv.x - v0.x) * y01 - (cv.y - v0.y) * x01) < 0;
            bool e1 = ((cv.x - v1.x) * y12 - (cv.y - v1.y) * x12) < 0;
            bool e2 = ((cv.x - v2.x) * y20 - (cv.y - v2.y) * x20) < 0;

            if (e0 && e1 && e2)
                return false;
        }

        return true;
    }

    private static bool IsEar(int vertex, List<PolygonVertex> vertices, int vertexCount, ushort[] indices, int indexCount, int agentHeight)
    {
        int i0 = vertex > 0 ? vertex - 1 : indexCount - 1;
        int i1 = vertex;
        int i2 = (vertex + 1) % indexCount;

        PolygonVertex v0 = vertices[indices[i0]];
        PolygonVertex v1 = vertices[indices[i1]];
        PolygonVertex v2 = vertices[indices[i2]];

        int minZ = Math.Min(Math.Min(v0.z, v1.z), v2.z);
        int maxZ = Math.Max(Math.Max(v0.z, v1.z), v2.z);

        int x01 = v0.x - v1.x;
        int y01 = v0.y - v1.y;
        int x12 = v1.x - v2.x;
        int y12 = v1.y - v2.y;
        int x20 = v2.x - v0.x;
        int y20 = v2.y - v0.y;

        for (int k = 0; k < vertexCount; ++k)
        {
            PolygonVertex cv = vertices[k];

            if (cv.z > maxZ + agentHeight || cv.z < minZ - 1)
                continue;

            bool e0 = ((cv.x - v0.x) * y01 - (cv.y - v0.y) * x01) < 0;
            bool e1 = ((cv.x - v1.x) * y12 - (cv.y - v1.y) * x12) < 0;
            bool e2 = ((cv.x - v2.x) * y20 - (cv.y - v2.y) * x20) < 0;

            if (e0 && e1 && e2)
                return false;
        }

        return true;
    }

    // ============================================================================
    // Intersects helper — from C++ TileGenerator.cpp:2390-2414
    // ============================================================================

    private static bool Intersects(Vec2i a0, Vec2i a1, Vec2i b0, Vec2i b1)
    {
        Vec2i e = new Vec2i(b0.X - a0.X, b0.Y - a0.Y);
        Vec2i da = new Vec2i(a1.X - a0.X, a1.Y - a0.Y);
        Vec2i db = new Vec2i(b1.X - b0.X, b1.Y - b0.Y);
        int det = da.X * db.Y - da.Y * db.X;
        int signAdjustment = det >= 0 ? 1 : -1;
        int minAllowedValue = 0;
        int maxAllowedValue = 1 * det * signAdjustment;
        if (det != 0)
        {
            int s = (e.X * db.Y - e.Y * db.X) * signAdjustment;
            if (s < minAllowedValue || s > maxAllowedValue)
                return false;

            int t = (e.X * da.Y - e.Y * da.X) * signAdjustment;
            if (t < minAllowedValue || t > maxAllowedValue)
                return false;

            return true;
        }

        return false;
    }

    // ============================================================================
    // MergeHole — from C++ TileGenerator.cpp:2416-2447
    // ============================================================================

    protected void MergeHole(List<PolygonVertex> contour, int contourVertex, List<PolygonVertex> hole, int holeVertex, int distSqr)
    {
        int holeSize = hole.Count;
        int contourSize = contour.Count;

        if (distSqr == 0)
        {
            // No joint edges
            contour.Capacity = Math.Max(contour.Capacity, contourSize + holeSize);

            int ip = contourVertex + 1;

            // Insert hole reversed starting from (holeSize - holeVertex) to end
            List<PolygonVertex> reversed = new List<PolygonVertex>(hole);
            reversed.Reverse();

            // hole.rbegin() + (holeSize - holeVertex) means starting from index holeVertex in the reversed list
            // which maps to reversed[holeSize - holeVertex - 1]... Actually let's follow the C++ exactly.
            // hole.rbegin() + (holeSize - holeVertex) = reverse iterator starting at offset (holeSize - holeVertex)
            // rbegin + k = hole[holeSize - 1 - k]
            // So inserting from rbegin + (holeSize - holeVertex) to rend:
            //   items: hole[holeSize - 1 - (holeSize - holeVertex)], ..., hole[0] = hole[holeVertex - 1], ..., hole[0]
            // That's: holeVertex items from hole[holeVertex-1] down to hole[0]
            List<PolygonVertex> part1 = new List<PolygonVertex>();
            for (int i = holeVertex - 1; i >= 0; --i)
                part1.Add(hole[i]);

            // Then inserting from rbegin to rbegin + (holeSize - holeVertex):
            // items: hole[holeSize - 1], ..., hole[holeVertex]
            List<PolygonVertex> part2 = new List<PolygonVertex>();
            for (int i = holeSize - 1; i >= holeVertex; --i)
                part2.Add(hole[i]);

            contour.InsertRange(ip, part1);
            ip += part1.Count;

            contour.InsertRange(ip, part2);
        }
        else
        {
            // Insert extra joint verts
            contour.Capacity = Math.Max(contour.Capacity, contourSize + holeSize + 2);

            int ip = contourVertex + 1;

            // rbegin + (holeSize - (holeVertex + 1)) to rend
            // = hole[holeVertex], hole[holeVertex - 1], ..., hole[0]
            List<PolygonVertex> part1 = new List<PolygonVertex>();
            for (int i = holeVertex; i >= 0; --i)
                part1.Add(hole[i]);

            // rbegin to rbegin + (holeSize - holeVertex)
            // = hole[holeSize - 1], ..., hole[holeVertex]
            List<PolygonVertex> part2 = new List<PolygonVertex>();
            for (int i = holeSize - 1; i >= holeVertex; --i)
                part2.Add(hole[i]);

            contour.InsertRange(ip, part1);
            ip += part1.Count;

            contour.InsertRange(ip, part2);
            ip += part2.Count;

            contour.Insert(ip, contour[contourVertex]);
        }
    }

    // ============================================================================
    // InsertUniqueVertex — from C++ TileGenerator.cpp:1747-1769
    // ============================================================================

    protected int InsertUniqueVertex(Dictionary<uint, ushort> lookUp, int x, int y, int z)
    {
        const int zmask = (1 << 11) - 1;
        const int xmask = (1 << 10) - 1;
        const int ymask = (1 << 10) - 1;

        uint vertexID = ((uint)(z & zmask) << 20) | ((uint)(x & xmask) << 10) | (uint)(y & ymask);

        if (lookUp.TryGetValue(vertexID, out ushort existingIndex))
            return existingIndex;

        ushort newIndex = (ushort)m_vertices.Count;

        m_vertices.Add(new Tile.Vertex(
            new fixed_t_u16_5(x * m_params.voxelSize.x),
            new fixed_t_u16_5(y * m_params.voxelSize.y),
            new fixed_t_u16_5(z * m_params.voxelSize.z)));

        lookUp[vertexID] = newIndex;
        return newIndex;
    }

    // ============================================================================
    // Triangulate (single polygon) — from C++ TileGenerator.cpp:2205-2388
    // ============================================================================

    protected int Triangulate(List<PolygonVertex> contour, int agentHeight, int borderH, int borderV,
        Dictionary<uint, ushort> lookUp)
    {
        int triCount = 0;
        int vertexCount = contour.Count;

        const int MaxIndices = 4096;
        ushort[] indices = new ushort[MaxIndices];

        for (int i = 0; i < vertexCount; ++i)
            indices[i] = (ushort)i;

        while (true)
        {
            int bestInsideCount = int.MaxValue;
            int bestDiagonalIdx = -1;
            int bestDiagonalSq = int.MaxValue;

            int bestDelaugnayIdx = -1;
            int bestDelaugnaySq = int.MaxValue;

            int ii = vertexCount - 1;

            for (int i = 0; i < vertexCount; ++i)
            {
                ushort ii0 = (ushort)ii;
                ushort i0 = indices[ii];
                ushort i1 = indices[i];
                ii = i;

                PolygonVertex v1 = contour[i1];

                if ((v1.flags & PolygonVertex.Ear) == 0)
                    continue;

                ushort i2 = indices[(i + 1) % vertexCount];

                PolygonVertex v0 = contour[i0];
                PolygonVertex v2 = contour[i2];

                int minZ = Math.Min(Math.Min(v0.z, v1.z), v2.z);
                int maxZ = Math.Max(Math.Max(v0.z, v1.z), v2.z);

                int x13 = v0.x - v1.x;
                int y13 = v0.y - v1.y;
                int x23 = v2.x - v1.x;
                int y23 = v2.y - v1.y;

                int a = (x13 * x23 + y13 * y23);
                int b = (y13 * x23 - x13 * y23);

                int insideCircumcircleCount = 0;

                int k = 0;
                for (; k < vertexCount - 3; ++k)
                {
                    PolygonVertex cv = contour[indices[(ii0 + 3 + k) % vertexCount]];

                    if (cv.z > maxZ + agentHeight || cv.z + agentHeight < minZ)
                        continue;

                    int x1p = v0.x - cv.x;
                    int y1p = v0.y - cv.y;
                    int x2p = v2.x - cv.x;
                    int y2p = v2.y - cv.y;

                    if ((a * (x2p * y1p - x1p * y2p)) <= (b * (x2p * x1p + y1p * y2p)))
                        ++insideCircumcircleCount;
                }

                if (k == (vertexCount - 3))
                {
                    int diagonalSq = sqr_i(v2.x - v0.x) + sqr_i(v2.y - v0.y) + sqr_i(5 * (v2.z - v0.z));

                    if (insideCircumcircleCount < bestInsideCount)
                    {
                        bestInsideCount = insideCircumcircleCount;
                        bestDiagonalSq = diagonalSq;
                        bestDiagonalIdx = i;
                    }
                    else if (insideCircumcircleCount == bestInsideCount)
                    {
                        if (diagonalSq < bestDiagonalSq)
                        {
                            bestDiagonalSq = diagonalSq;
                            bestDiagonalIdx = i;
                        }
                    }

                    if (insideCircumcircleCount == 0)
                    {
                        if (diagonalSq < bestDelaugnaySq)
                        {
                            bestDelaugnaySq = diagonalSq;
                            bestDelaugnayIdx = i;
                        }
                    }
                }
            }

            int bestIdx = bestDelaugnayIdx;

            if (bestIdx < 0)
            {
                bestIdx = bestDiagonalIdx;

                if (bestIdx < 0)
                {
                    if (vertexCount != 3)
                        break;

                    bestIdx = 1;
                }
            }

            {
                int aidx = bestIdx > 0 ? (bestIdx - 1) : (vertexCount - 1);
                int bidx = bestIdx;
                int cidx = (bestIdx + 1) % vertexCount;

                ushort i0 = indices[aidx];
                ushort i1 = indices[bidx];
                ushort i2 = indices[cidx];

                PolygonVertex v0 = contour[i0];
                PolygonVertex v1 = contour[i1];
                PolygonVertex v2 = contour[i2];

                ushort v0i = (ushort)InsertUniqueVertex(lookUp, v0.x - borderH, v0.y - borderH, v0.z - borderV);
                ushort v2i = (ushort)InsertUniqueVertex(lookUp, v1.x - borderH, v1.y - borderH, v1.z - borderV);
                ushort v1i = (ushort)InsertUniqueVertex(lookUp, v2.x - borderH, v2.y - borderH, v2.z - borderV);

                --vertexCount;

                if ((v0i == v1i) | (v0i == v2i) | (v1i == v2i))
                {
                    if (vertexCount < 3)
                        break;

                    // Remove the vertex from indices and continue
                    for (int k = bestIdx; k < vertexCount; ++k)
                        indices[k] = indices[k + 1];

                    continue;
                }

                Tile.Triangle triangle = new Tile.Triangle();
                triangle.linkCount = 0;
                triangle.firstLink = 0;
                triangle.vertex[0] = v0i;
                triangle.vertex[1] = v1i;
                triangle.vertex[2] = v2i;
                m_triangles.Add(triangle);

                if (vertexCount < 3)
                    break;

                for (int k = bestIdx; k < vertexCount; ++k)
                    indices[k] = indices[k + 1];

                int updi0 = bestIdx > 0 ? (bestIdx - 1) : (vertexCount - 1);
                int updi2 = bestIdx % vertexCount;

                {
                    PolygonVertex pv = contour[indices[updi0]];
                    pv.flags &= unchecked((ushort)~(PolygonVertex.Reflex | PolygonVertex.Ear));
                    if (IsReflex(updi0, contour, indices, vertexCount))
                        pv.flags |= PolygonVertex.Reflex;
                    else if (IsEar(updi0, contour, contour.Count, indices, vertexCount, agentHeight))
                        pv.flags |= PolygonVertex.Ear;
                    contour[indices[updi0]] = pv;
                }

                {
                    PolygonVertex pv = contour[indices[updi2]];
                    pv.flags &= unchecked((ushort)~(PolygonVertex.Reflex | PolygonVertex.Ear));
                    if (IsReflex(updi2, contour, indices, vertexCount))
                        pv.flags |= PolygonVertex.Reflex;
                    else if (IsEar(updi2, contour, contour.Count, indices, vertexCount, agentHeight))
                        pv.flags |= PolygonVertex.Ear;
                    contour[indices[updi2]] = pv;
                }

                ++triCount;
            }
        }

        return triCount;
    }

    // ============================================================================
    // Triangulate (all polygons) — from C++ TileGenerator.cpp:2449-2695
    // ============================================================================

    protected void Triangulate()
    {
        m_profiler.StartTimer(ProfilerTimers.Triangulation);

        int totalIndexCount = 0;
        int vertexCount = 0;

        for (int i = 0; i < m_polygons.Count; ++i)
        {
            List<PolygonVertex> contour = m_polygons[i].contour;
            List<Hole> holes = m_polygons[i].holes;

            totalIndexCount += (contour.Count - 2) * 3;
            vertexCount += contour.Count;

            for (int h = 0; h < holes.Count; ++h)
            {
                totalIndexCount += holes[h].verts.Count * 3;
                vertexCount += holes[h].verts.Count + 2;
            }
        }

        Dictionary<uint, ushort> lookUp = new Dictionary<uint, ushort>(vertexCount);

        m_triangles.Clear();
        m_triangles.Capacity = Math.Max(m_triangles.Capacity, totalIndexCount);

        m_vertices.Clear();
        m_vertices.Capacity = Math.Max(m_vertices.Capacity, vertexCount);

        int triCount = 0;

        int agentHeight = (int)m_params.agent.height;
        int borderH = BorderSizeH();
        int borderV = BorderSizeV();

        for (int p = 0; p < m_polygons.Count; ++p)
        {
            Polygon polygon = m_polygons[p];
            List<Hole> holes = polygon.holes;
            List<PolygonVertex> contour = polygon.contour;

            int contourSize = contour.Count;

            int finalVertexCount = contourSize;
            for (int h = 0; h < holes.Count; ++h)
                finalVertexCount += holes[h].verts.Count + 2;
            contour.Capacity = Math.Max(contour.Capacity, finalVertexCount);

            for (int v = 0; v < contourSize; v++)
            {
                PolygonVertex pv = contour[v];
                pv.flags &= unchecked((ushort)~PolygonVertex.Reflex);
                pv.flags |= (ushort)(IsReflex(v, contour, contourSize) ? PolygonVertex.Reflex : 0);
                contour[v] = pv;
            }

            while (holes.Count > 0)
            {
                int bestContourIdx = 0;
                int bestHoleIdx = 0;
                int bestHole = 0;
                const int kMaxDist = 10000;
                int bestDist = kMaxDist;

                int contourNum = contour.Count;
                int numHoles = holes.Count;
                for (int ci0 = contourNum - 2, ci1 = contourNum - 1, ci2 = 0; ci2 < contourNum; ci0 = ci1, ci1 = ci2++)
                {
                    Vec3i cv0 = new Vec3i(contour[ci0].x, contour[ci0].y, contour[ci0].z);
                    Vec3i cv1 = new Vec3i(contour[ci1].x, contour[ci1].y, contour[ci1].z);
                    Vec3i cv2 = new Vec3i(contour[ci2].x, contour[ci2].y, contour[ci2].z);
                    Vec2i cIn0 = new Vec2i(cv1.y - cv0.y, cv0.x - cv1.x);
                    Vec2i cIn1 = new Vec2i(cv2.y - cv1.y, cv1.x - cv2.x);
                    Vec2i cv2_cv1 = new Vec2i(cv2.x - cv1.x, cv2.y - cv1.y);
                    bool bCVReflex = Vec2i_dot(cIn0, cv2_cv1) < 0;
                    Vec2i cv1_2D = new Vec2i(cv1.x, cv1.y);
                    for (int hi = 0; hi < numHoles; hi++)
                    {
                        Hole hole = holes[hi];
                        Vec2i holeDiff = new Vec2i(hole.center.X - cv1_2D.X, hole.center.Y - cv1_2D.Y);
                        int holeDistSqr = Vec2i_len2(holeDiff);
                        if (bestDist == kMaxDist || holeDistSqr < sqr_i(hole.rad + bestDist))
                        {
                            int vertNum = hole.verts.Count;
                            for (int vi0 = vertNum - 2, vi1 = vertNum - 1, vi2 = 0; vi2 < vertNum; vi0 = vi1, vi1 = vi2++)
                            {
                                Vec3i hv1 = new Vec3i(hole.verts[vi1].x, hole.verts[vi1].y, hole.verts[vi1].z);
                                Vec3i diffV = hv1 - cv1;
                                Vec2i diff_2D = new Vec2i(diffV.x, diffV.y);

                                int holeVertDistSqr = Vec3i_len2(diffV);

                                if (bestDist != kMaxDist && holeVertDistSqr >= sqr_i(bestDist))
                                    continue;

                                if (holeVertDistSqr > 0)
                                {
                                    bool inC0 = Vec2i_dot(cIn0, diff_2D) > 0;
                                    bool inC1 = Vec2i_dot(cIn1, diff_2D) > 0;
                                    if (bCVReflex)
                                    {
                                        if (!inC0 && !inC1)
                                            continue;
                                    }
                                    else if (!inC0 || !inC1)
                                        continue;

                                    Vec3i hv0 = new Vec3i(hole.verts[vi0].x, hole.verts[vi0].y, hole.verts[vi0].z);
                                    Vec3i hv2 = new Vec3i(hole.verts[vi2].x, hole.verts[vi2].y, hole.verts[vi2].z);
                                    Vec2i hIn0 = new Vec2i(hv1.y - hv0.y, hv0.x - hv1.x);
                                    Vec2i hIn1 = new Vec2i(hv2.y - hv1.y, hv1.x - hv2.x);
                                    Vec2i hv2_hv1 = new Vec2i(hv2.x - hv1.x, hv2.y - hv1.y);
                                    bool bHVReflex = Vec2i_dot(hIn0, hv2_hv1) > 0;

                                    bool outH0 = Vec2i_dot(hIn0, diff_2D) > 0;
                                    bool outH1 = Vec2i_dot(hIn1, diff_2D) > 0;
                                    if (bCVReflex)
                                    {
                                        if (!outH0 && !outH1)
                                            continue;
                                    }
                                    else if (!outH0 || !outH1)
                                        continue;

                                    if (Math.Abs(diffV.z) > agentHeight)
                                        continue;
                                }

                                bestDist = int_ceil(sqrt_tpl((float)holeVertDistSqr));
                                bestHole = hi;
                                bestHoleIdx = vi1;
                                bestContourIdx = ci1;
                            }
                        }
                    }
                }

                List<PolygonVertex> holeVerts = holes[bestHole].verts;

                bool bValid = (bestDist != kMaxDist);
                if (bValid)
                {
                    Vec2i besthv = new Vec2i(holeVerts[bestHoleIdx].x, holeVerts[bestHoleIdx].y);
                    Vec2i bestcv = new Vec2i(contour[bestContourIdx].x, contour[bestContourIdx].y);
                    int heightMin = Math.Min(holeVerts[bestHoleIdx].z, contour[bestContourIdx].z) - agentHeight;
                    int heightRange = (Math.Max(holeVerts[bestHoleIdx].z, contour[bestContourIdx].z) - heightMin) + agentHeight;

                    // Check for intersection with other contour edges
                    for (int ci = contourNum - 1, cj = 0; cj < contourNum; ci = cj++)
                    {
                        if (ci == bestContourIdx || cj == bestContourIdx)
                            continue;

                        int z0 = contour[ci].z - heightMin;
                        int z1 = contour[cj].z - heightMin;
                        if ((z0 > heightRange && z1 > heightRange) || (z0 < 0 && z1 < 0))
                            continue;

                        Vec2i v0 = new Vec2i(contour[ci].x, contour[ci].y);
                        Vec2i v1 = new Vec2i(contour[cj].x, contour[cj].y);
                        if (Intersects(besthv, bestcv, v0, v1))
                        {
                            bValid = false;
                            break;
                        }
                    }

                    if (bValid)
                    {
                        int holeNum = holeVerts.Count;
                        for (int hi = holeNum - 1, hj = 0; hj < holeNum; hi = hj++)
                        {
                            if (hi == bestHoleIdx || hj == bestHoleIdx)
                                continue;

                            int z0 = holeVerts[hi].z - heightMin;
                            int z1 = holeVerts[hj].z - heightMin;
                            if ((z0 > heightRange && z1 > heightRange) || (z0 < 0 && z1 < 0))
                                continue;

                            Vec2i v0 = new Vec2i(holeVerts[hi].x, holeVerts[hi].y);
                            Vec2i v1 = new Vec2i(holeVerts[hj].x, holeVerts[hj].y);
                            if (Intersects(besthv, bestcv, v0, v1))
                            {
                                bValid = false;
                                break;
                            }
                        }
                    }
                }

                if (!bValid)
                {
                    // AIWarning: Hole Merge selected possibly invalid verts
                }

                MergeHole(contour, bestContourIdx, holeVerts, bestHoleIdx, bestDist);

                // swap and pop
                if (bestHole < holes.Count - 1)
                {
                    Hole tmp = holes[bestHole];
                    holes[bestHole] = holes[holes.Count - 1];
                    holes[holes.Count - 1] = tmp;
                }
                holes.RemoveAt(holes.Count - 1);

                contourSize = contour.Count;

                for (int v = 0; v < contourSize; v++)
                {
                    PolygonVertex pv = contour[v];
                    pv.flags &= unchecked((ushort)~PolygonVertex.Reflex);
                    pv.flags |= (ushort)(IsReflex(v, contour, contourSize) ? PolygonVertex.Reflex : 0);
                    contour[v] = pv;
                }
            }

            for (int v = 0; v < contourSize; v++)
            {
                PolygonVertex vv = contour[v];
                if ((vv.flags & PolygonVertex.Reflex) == 0)
                {
                    vv.flags |= (ushort)(IsEar(v, contour, contourSize, agentHeight) ? PolygonVertex.Ear : 0);
                    contour[v] = vv;
                }
            }

            triCount += Triangulate(polygon.contour, agentHeight, borderH, borderV, lookUp);
        }

        m_profiler.StopTimer(ProfilerTimers.Triangulation);

        m_profiler.AddStat(ProfilerStats.VertexCount, m_vertices.Count);
        m_profiler.AddStat(ProfilerStats.TriangleCount, m_triangles.Count);
    }

    // ============================================================================
    // BVTree types — from C++ TileGenerator.cpp:2697-2756
    // ============================================================================

    private struct BVTriangle : IComparable<BVTriangle>
    {
        public ushort triangleID;
        public ushort binID;
        public vector3_t centroid;
        public aabb_t aabb;

        public BVTriangle(ushort _triangleID = 0, ushort _binID = 0)
        {
            triangleID = _triangleID;
            binID = _binID;
            centroid = default;
            aabb = default;
        }

        public int CompareTo(BVTriangle other) => binID.CompareTo(other.binID);
    }

    private struct BVBin
    {
        public aabb_t aabb;
        public real_t area;
        public int triCount;
        public real_t leftArea;
        public int leftTriCount;

        public static BVBin Create()
        {
            BVBin bin = new BVBin();
            bin.aabb = new aabb_t(new aabb_t.init_empty());
            bin.area = new real_t(0);
            bin.triCount = 0;
            bin.leftArea = new real_t(0);
            bin.leftTriCount = 0;
            return bin;
        }
    }

    private class SortTriangleByDimension : IComparer<BVTriangle>
    {
        private int dim;
        public SortTriangleByDimension(int _dim) { dim = _dim; }
        public int Compare(BVTriangle left, BVTriangle right)
        {
            if (left.centroid[dim] == right.centroid[dim])
                return left.aabb.min[dim].CompareTo(right.aabb.min[dim]);
            return left.centroid[dim].CompareTo(right.centroid[dim]);
        }
    }

    // ============================================================================
    // CalculateVolume / SplitSAH — from C++ TileGenerator.cpp:2757-2917
    // ============================================================================

    private static void CalculateVolume(BVTriangle[] triangles, int firstTri, int triCount, ref aabb_t aabb)
    {
        aabb = new aabb_t(new aabb_t.init_empty());

        int triEnd = firstTri + triCount;
        for (int i = firstTri; i < triEnd; ++i)
            aabb.AddAABB(triangles[i].aabb);
    }

    private void SplitSAH(aabb_t aabb, BVTriangle[] triangles, int firstTri, int triCount,
        List<Tile.BVNode> nodes)
    {
        Tile.BVNode node = new Tile.BVNode();
        node.aabb = new Tile.TileAABB(
            new Tile.Vertex(
                new fixed_t_u16_5(aabb.min.x.as_float()),
                new fixed_t_u16_5(aabb.min.y.as_float()),
                new fixed_t_u16_5(aabb.min.z.as_float())),
            new Tile.Vertex(
                new fixed_t_u16_5(aabb.max.x.as_float()),
                new fixed_t_u16_5(aabb.max.y.as_float()),
                new fixed_t_u16_5(aabb.max.z.as_float())));
        int currentNode = nodes.Count;
        nodes.Add(node);

        if (triCount == 1)
        {
            node = nodes[currentNode];
            node.leaf = 1;
            node.offset = triangles[firstTri].triangleID;
            nodes[currentNode] = node;
            return;
        }

        node = nodes[currentNode];
        node.leaf = 0;
        nodes[currentNode] = node;

        aabb_t caabb = new aabb_t(new aabb_t.init_empty());

        for (int i = firstTri; i < firstTri + triCount; ++i)
            caabb.AddPoint(triangles[i].centroid);

        vector3_t size = aabb.size();
        int dimSplit = 0;

        if (size.y >= size.z)
        {
            if (size.y > size.x)
                dimSplit = 1;
        }
        else if (size.z > size.x)
            dimSplit = 2;

        real_t distCentroid = caabb.max[dimSplit] - caabb.min[dimSplit];

        const int MaxBinCount = 8;
        BVBin[] bins = new BVBin[MaxBinCount];
        for (int i = 0; i < MaxBinCount; i++)
            bins[i] = BVBin.Create();

        real_t BinEpsilon = real_t.fraction(1, 1000);
        int BinCount = distCentroid > BinEpsilon ? 8 : 2;
        real_t BinConv = (distCentroid > new real_t(0)) ? (new real_t((uint)BinCount) - BinEpsilon) / distCentroid : new real_t(0);

        for (int i = firstTri; i < firstTri + triCount; ++i)
        {
            ref BVTriangle tri = ref triangles[i];

            real_t centroid = tri.centroid[dimSplit] - caabb.min[dimSplit];
            int binID = (int)(BinConv * centroid).as_uint();
            if (binID >= BinCount) binID = BinCount - 1;

            tri.binID = (ushort)binID;

            ref BVBin bin = ref bins[binID];
            ++bin.triCount;
            bin.aabb.AddAABB(tri.aabb);
        }

        // Find split plane
        real_t leftArea = new real_t(0);
        int leftTriCount = 0;

        {
            aabb_t laabb = new aabb_t(new aabb_t.init_empty());

            for (int i = 0; i < BinCount; ++i)
            {
                ref BVBin bin = ref bins[i];

                if (bin.triCount > 0)
                {
                    vector3_t dims = bin.aabb.size();
                    real_t area = (dims.x * dims.y + dims.x * dims.z + dims.z * dims.y) * new real_t(2);
                    bin.area = area;

                    leftArea = leftArea + area;
                    leftTriCount += bin.triCount;

                    bin.leftArea = leftArea;
                    bin.leftTriCount = leftTriCount;

                    laabb.AddAABB(bin.aabb);
                }

                bin.aabb = laabb;
            }
        }

        real_t costLowest = leftArea * new real_t((uint)leftTriCount);
        int split = BinCount - 1;

        aabb_t raabbLowest = new aabb_t(new aabb_t.init_empty());
        aabb_t raabb = new aabb_t(new aabb_t.init_empty());

        real_t rightArea = new real_t(0);

        for (int i = 0; i < BinCount - 2; ++i)
        {
            ref BVBin bin = ref bins[BinCount - i - 1];
            if (bin.triCount == 0)
                continue;

            raabb.AddAABB(bin.aabb);

            ref BVBin leftBin = ref bins[BinCount - i - 2];

            rightArea = rightArea + bin.area;

            real_t cost = rightArea * new real_t((uint)bin.triCount) + bin.leftArea * new real_t((uint)bin.leftTriCount);

            if (cost < costLowest)
            {
                costLowest = cost;
                split = BinCount - i - 2;

                raabbLowest.AddAABB(raabb);
            }
        }

        if (split == BinCount - 1)
        {
            Array.Sort(triangles, firstTri, triCount, new SortTriangleByDimension(dimSplit));

            int splitTri = triCount >> 1;

            aabb_t laabb = default;
            CalculateVolume(triangles, firstTri, splitTri, ref laabb);
            SplitSAH(laabb, triangles, firstTri, splitTri, nodes);

            CalculateVolume(triangles, firstTri + splitTri, triCount - splitTri, ref raabb);
            SplitSAH(raabb, triangles, firstTri + splitTri, triCount - splitTri, nodes);

            node = nodes[currentNode];
            node.offset = (ushort)(nodes.Count - currentNode);
            nodes[currentNode] = node;
        }
        else
        {
            Array.Sort(triangles, firstTri, triCount);

            // Find split point using lower_bound on binID
            int splitTri = 0;
            for (int i = firstTri; i < firstTri + triCount; ++i)
            {
                if (triangles[i].binID >= split + 1)
                {
                    splitTri = i - firstTri;
                    break;
                }
            }
            if (splitTri == 0) splitTri = triCount >> 1; // fallback
            Debug.Assert(splitTri < triCount);

            SplitSAH(bins[split].aabb, triangles, firstTri, splitTri, nodes);
            SplitSAH(raabbLowest, triangles, firstTri + splitTri, triCount - splitTri, nodes);

            node = nodes[currentNode];
            node.offset = (ushort)(nodes.Count - currentNode);
            nodes[currentNode] = node;
        }
    }

    // ============================================================================
    // BuildBVTree — from C++ TileGenerator.cpp:2919-2979
    // ============================================================================

    protected void BuildBVTree()
    {
        m_profiler.StartTimer(ProfilerTimers.BVTreeConstruction);

        int triangleCount = m_triangles.Count;

        if (triangleCount == 0)
        {
            m_bvtree.Clear();
            m_profiler.StopTimer(ProfilerTimers.BVTreeConstruction);
            return;
        }

        aabb_t aabb = new aabb_t(new aabb_t.init_empty());

        BVTriangle[] bvTriangles = new BVTriangle[triangleCount];

        for (int i = 0; i < triangleCount; ++i)
        {
            Tile.Triangle triangle = m_triangles[i];
            ref BVTriangle bvTri = ref bvTriangles[i];

            vector3_t[] vertices = new vector3_t[3];
            for (int v = 0; v < 3; ++v)
                vertices[v] = new vector3_t(m_vertices[triangle.vertex[v]]);

            bvTri.aabb = new aabb_t(
                vector3_t.minimize(vertices[0], vertices[1], vertices[2]),
                vector3_t.maximize(vertices[0], vertices[1], vertices[2]));
            bvTri.centroid = bvTri.aabb.center();
            bvTri.triangleID = (ushort)i;

            aabb.AddAABB(bvTri.aabb);
        }

        List<Tile.BVNode> nodes = new List<Tile.BVNode>(2 * triangleCount);

        SplitSAH(aabb, bvTriangles, 0, bvTriangles.Length, nodes);

        m_profiler.StopTimer(ProfilerTimers.BVTreeConstruction);

        m_profiler.AddMemory(ProfilerMemoryUsers.BVTreeMemory, nodes.Count * 32);
        m_profiler.AddStat(ProfilerStats.BVTreeNodeCount, nodes.Count);

        m_bvtree = nodes;
    }
}
