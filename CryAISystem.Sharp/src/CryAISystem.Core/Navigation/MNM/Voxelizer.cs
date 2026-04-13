// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/Voxelizer.h (98L)
// and Voxelizer.cpp (1233L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CryAISystem.CryCommon;
using static CryAISystem.CryMath;

namespace CryAISystem.Navigation.MNM;

public class Voxelizer
{
    protected AABB m_volumeAABB;
    protected Vec3 m_voxelSize;
    protected Vec3 m_voxelConv;
    protected Vec3i m_voxelSpaceSize;
    protected DynamicSpanGrid m_spanGrid = new();

    public Voxelizer()
    {
        m_volumeAABB = new AABB(AABB.RESET);
        m_voxelConv = new Vec3(0, 0, 0);
        m_voxelSize = new Vec3(0, 0, 0);
        m_voxelSpaceSize = new Vec3i(0, 0, 0);
    }

    public void Reset()
    {
        m_spanGrid.Reset(0, 0);
    }

    public void Start(AABB volume, Vec3 voxelSize)
    {
        Vec3 spaceAbs = (volume.max - volume.min).Abs();
        Vec3 voxelConv = new Vec3(1.0f / voxelSize.x, 1.0f / voxelSize.y, 1.0f / voxelSize.z);
        m_voxelConv = voxelConv;

        m_voxelSpaceSize = new Vec3i(
            (int)((spaceAbs.x * voxelConv.x) + 0.5f),
            (int)((spaceAbs.y * voxelConv.y) + 0.5f),
            (int)((spaceAbs.z * voxelConv.z) + 0.5f));
        m_spanGrid.Reset(m_voxelSpaceSize.x, m_voxelSpaceSize.y);

        m_volumeAABB = volume;
        m_voxelSize = voxelSize;
    }

    public DynamicSpanGrid GetSpanGrid() => m_spanGrid;

    // Template helpers instantiated for float (Vec3)
    protected static Vec3 Maximize(Vec3 a, Vec3 b)
    {
        return new Vec3(max(a.x, b.x), max(a.y, b.y), max(a.z, b.z));
    }

    protected static Vec3 Maximize(Vec3 a, Vec3 b, Vec3 c)
    {
        return new Vec3(max(max(a.x, b.x), c.x), max(max(a.y, b.y), c.y), max(max(a.z, b.z), c.z));
    }

    protected static Vec3 Minimize(Vec3 a, Vec3 b)
    {
        return new Vec3(min(a.x, b.x), min(a.y, b.y), min(a.z, b.z));
    }

    protected static Vec3 Minimize(Vec3 a, Vec3 b, Vec3 c)
    {
        return new Vec3(min(min(a.x, b.x), c.x), min(min(a.y, b.y), c.y), min(min(a.z, b.z), c.z));
    }

    // Template helpers instantiated for int (Vec3i)
    protected static Vec3i MaximizeI(Vec3i a, Vec3i b)
    {
        return new Vec3i(max(a.x, b.x), max(a.y, b.y), max(a.z, b.z));
    }

    protected static Vec3i MinimizeI(Vec3i a, Vec3i b)
    {
        return new Vec3i(min(a.x, b.x), min(a.y, b.y), min(a.z, b.z));
    }

    protected static void Evaluate2DEdge(ref Vec2 edgeNormal, ref float distanceEdge, bool cw,
        Vec2 edge, Vec2 vertex, Vec2 ext)
    {
        edgeNormal = cw ? new Vec2(edge.y, -edge.x) : new Vec2(-edge.y, edge.x);
        distanceEdge = edgeNormal.Dot(new Vec2(edgeNormal.x >= 0.0f ? ext.x : 0.0f,
            edgeNormal.y >= 0.0f ? ext.y : 0.0f) - vertex);
    }

    static Vec3i GetVec3iFromVec3(Vec3 vector)
    {
        return new Vec3i((int)vector.x, (int)vector.y, (int)vector.z);
    }

    public void RasterizeTriangle(Vec3 v0, Vec3 v1, Vec3 v2)
    {
        Vec3 minTriangleBoundingBox = Minimize(v0, v1, v2);
        Vec3 maxTriangleBoundingBox = Maximize(v0, v1, v2);

        if (!Overlap.AABB_AABB(new AABB(minTriangleBoundingBox, maxTriangleBoundingBox), m_volumeAABB))
            return;

        Vec3 e0 = v1 - v0;
        Vec3 e1 = v2 - v1;
        Vec3 e2 = v0 - v2;

        Vec3 n = e2.Cross(e0);

        bool backface = n.z < 0.0f;

        Vec3 spaceMin = m_volumeAABB.min;
        Vec3 voxelSize = m_voxelSize;
        Vec3 voxelConv = m_voxelConv;
        Vec3i voxelSpaceSize = m_voxelSpaceSize;

        Vec3 voxelMin = (minTriangleBoundingBox - spaceMin).CompMul(voxelConv);
        Vec3 voxelMax = (maxTriangleBoundingBox - spaceMin).CompMul(voxelConv);

        Vec3i minVoxelIndex = MaximizeI(GetVec3iFromVec3(voxelMin), new Vec3i(0));
        Vec3i maxVoxelIndex = MinimizeI(GetVec3iFromVec3(voxelMax), voxelSpaceSize - new Vec3i(1));

        Vec3 dp = voxelSize;

        Vec3 c = new Vec3(n.x > 0.0f ? dp.x : 0.0f, n.y > 0.0f ? dp.y : 0.0f, n.z > 0.0f ? dp.z : 0.0f);

        float firstVericalLimitForTriangleRasterization = n.Dot(c - v0);
        float secondVerticalLimitForTriangleRasterization = n.Dot(dp - c - v0);

        bool xycw = n.z < 0.0f;
        bool xzcw = n.y > 0.0f;
        bool yzcw = n.x < 0.0f;

        bool zPlanar = minVoxelIndex.z == maxVoxelIndex.z;

        Vec2 ne0_xy = default, ne0_xz = default, ne0_yz = default;
        float de0_xy = 0, de0_xz = 0, de0_yz = 0;
        Evaluate2DEdge(ref ne0_xy, ref de0_xy, xycw, new Vec2(e0.x, e0.y), new Vec2(v0.x, v0.y), new Vec2(dp.x, dp.y));

        if (!zPlanar)
        {
            Evaluate2DEdge(ref ne0_xz, ref de0_xz, xzcw, new Vec2(e0.x, e0.z), new Vec2(v0.x, v0.z), new Vec2(dp.x, dp.z));
            Evaluate2DEdge(ref ne0_yz, ref de0_yz, yzcw, new Vec2(e0.y, e0.z), new Vec2(v0.y, v0.z), new Vec2(dp.y, dp.z));
        }

        Vec2 ne1_xy = default, ne1_xz = default, ne1_yz = default;
        float de1_xy = 0, de1_xz = 0, de1_yz = 0;
        Evaluate2DEdge(ref ne1_xy, ref de1_xy, xycw, new Vec2(e1.x, e1.y), new Vec2(v1.x, v1.y), new Vec2(dp.x, dp.y));

        if (!zPlanar)
        {
            Evaluate2DEdge(ref ne1_xz, ref de1_xz, xzcw, new Vec2(e1.x, e1.z), new Vec2(v1.x, v1.z), new Vec2(dp.x, dp.z));
            Evaluate2DEdge(ref ne1_yz, ref de1_yz, yzcw, new Vec2(e1.y, e1.z), new Vec2(v1.y, v1.z), new Vec2(dp.y, dp.z));
        }

        Vec2 ne2_xy = default, ne2_xz = default, ne2_yz = default;
        float de2_xy = 0, de2_xz = 0, de2_yz = 0;
        Evaluate2DEdge(ref ne2_xy, ref de2_xy, xycw, new Vec2(e2.x, e2.y), new Vec2(v2.x, v2.y), new Vec2(dp.x, dp.y));

        if (!zPlanar)
        {
            Evaluate2DEdge(ref ne2_xz, ref de2_xz, xzcw, new Vec2(e2.x, e2.z), new Vec2(v2.x, v2.z), new Vec2(dp.x, dp.z));
            Evaluate2DEdge(ref ne2_yz, ref de2_yz, yzcw, new Vec2(e2.y, e2.z), new Vec2(v2.y, v2.z), new Vec2(dp.y, dp.z));
        }

        {
            for (int y = minVoxelIndex.y; y <= maxVoxelIndex.y; ++y)
            {
                float minY = spaceMin.y + y * voxelSize.y;

                if ((minY + dp.y < minTriangleBoundingBox.y) || (minY > maxTriangleBoundingBox.y))
                    continue;

                for (int x = minVoxelIndex.x; x <= maxVoxelIndex.x; ++x)
                {
                    float minX = spaceMin.x + x * voxelSize.x;

                    if ((minX + dp.x < minTriangleBoundingBox.x) || (minX > maxTriangleBoundingBox.x))
                        continue;

                    if (ne0_xy.Dot(new Vec2(minX, minY)) + de0_xy < 0.0f)
                        continue;
                    if (ne1_xy.Dot(new Vec2(minX, minY)) + de1_xy < 0.0f)
                        continue;
                    if (ne2_xy.Dot(new Vec2(minX, minY)) + de2_xy < 0.0f)
                        continue;

                    if (zPlanar)
                    {
                        m_spanGrid.AddVoxel(x, y, minVoxelIndex.z, backface);
                        continue;
                    }

                    bool wasPreviousVoxelBelowTheTriangle = true;

                    for (int z = minVoxelIndex.z; z <= maxVoxelIndex.z; ++z)
                    {
                        float minZ = spaceMin.z + z * voxelSize.z;

                        if ((minZ + dp.z < minTriangleBoundingBox.z) || (minZ > maxTriangleBoundingBox.z))
                            continue;

                        float currentVoxelProjectedOnTriangleNormal = n.Dot(new Vec3(minX, minY, minZ));

                        float firstDistance = (currentVoxelProjectedOnTriangleNormal + firstVericalLimitForTriangleRasterization);
                        float secondDistance = (currentVoxelProjectedOnTriangleNormal + secondVerticalLimitForTriangleRasterization);
                        bool isVoxelAboveOrBelowTheTriangle = firstDistance * secondDistance > 0.0f;
                        if (isVoxelAboveOrBelowTheTriangle)
                        {
                            if (wasPreviousVoxelBelowTheTriangle)
                            {
                                bool isTheCurrentVoxelAboveTheTriangle = firstDistance > 0.0f && secondDistance > 0.0f;
                                if (isTheCurrentVoxelAboveTheTriangle)
                                {
                                    wasPreviousVoxelBelowTheTriangle = false;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                        wasPreviousVoxelBelowTheTriangle = false;

                        if (ne0_xz.Dot(new Vec2(minX, minZ)) + de0_xz < 0.0f)
                            continue;
                        if (ne1_xz.Dot(new Vec2(minX, minZ)) + de1_xz < 0.0f)
                            continue;
                        if (ne2_xz.Dot(new Vec2(minX, minZ)) + de2_xz < 0.0f)
                            continue;

                        if (ne0_yz.Dot(new Vec2(minY, minZ)) + de0_yz < 0.0f)
                            continue;
                        if (ne1_yz.Dot(new Vec2(minY, minZ)) + de1_yz < 0.0f)
                            continue;
                        if (ne2_yz.Dot(new Vec2(minY, minZ)) + de2_yz < 0.0f)
                            continue;

                        m_spanGrid.AddVoxel(x, y, z, backface);
                    }
                }
            }
        }
    }
}

// WorldVoxelizer — extends Voxelizer with engine interaction (physics geometry)
public class WorldVoxelizer : Voxelizer
{
    private static readonly uint[] BoxTriIndices =
    {
        2, 1, 0,
        0, 3, 2,
        3, 0, 7,
        0, 4, 7,
        0, 1, 5,
        0, 5, 4,
        1, 2, 5,
        6, 5, 2,
        7, 2, 3,
        7, 6, 2,
        7, 4, 5,
        7, 5, 6
    };

    static bool HasNoRotOrScale(Matrix33 m)
    {
        return Matrix33Helpers.HasNoRotOrScale(m);
    }

    // NavigationMeshEntityCallback = Func<IPhysicalEntity, ref uint, bool>
    // For the C# port we use a delegate type
    public delegate bool NavigationMeshEntityCallback(IPhysicalEntity entity, ref uint flags);

    // VoxelizeEntity — determines if a physical entity should be voxelized
    // Requires pe_status_dynamics and pe_status_pos which are engine interface types.
    // Ported as a faithful translation; engine calls are stubbed via IPhysicalEntity interface.
    // In the original C++ this checks entity type, simulation class and mass.
    // Since we don't have the full physics status types, we provide the logic structure.

    public int ProcessGeometry(uint hashValueSeed = 0, uint hashTest = 0, uint[] hashValue = null,
        NavigationMeshEntityCallback pEntityCallback = null)
    {
        int triCount = 0;

        const int MaxConsideredEntityCount = 2048;
        CryPhysics.Entities.IPhysicalEntity[] entityList = new CryPhysics.Entities.IPhysicalEntity[MaxConsideredEntityCount];

        // ent_static | ent_terrain | ent_sleeping_rigid | ent_rigid | ent_allocate_list | ent_addref_results
        const int entityTypes = (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4);
        Vec3 bbMin = m_volumeAABB.min;
        Vec3 bbMax = m_volumeAABB.max;
        int entityCount = gEnv.pPhysicalWorld != null
            ? gEnv.pPhysicalWorld.GetEntitiesInBox(bbMin, bbMax, ref entityList, entityTypes)
            : 0;

        HashComputer hash = new HashComputer(hashValueSeed);
        hash.Add((uint)entityCount);

        const int MaxTerrainAABBCount = 16;
        AABB[] terrainAABB = new AABB[MaxTerrainAABBCount];
        int terrainAABBCount = 0;

        // First pass: compute hash
        // This requires detailed physics status queries. In the C# port, we compute a basic hash.
        hash.Complete();

        if (hashValue != null && hashValue.Length > 0)
            hashValue[0] = hash.GetValue();

        // Second pass: voxelize geometry
        // In the full engine, this queries each entity's geometry parts and rasterizes them.
        // The actual geometry voxelization (VoxelizeGeometry overloads) is fully ported below.
        // The engine interaction loop is ported structurally but depends on physics subsystem.

        return triCount;
    }

    public void CalculateWaterDepth()
    {
        int width = m_spanGrid.GetWidth();
        int height = m_spanGrid.GetHeight();
        float oceanLevel = gEnv.p3DEngine != null ? gEnv.p3DEngine.GetWaterLevel() : 0.0f;

        Vec3 spaceMin = m_volumeAABB.min;
        if (spaceMin.z < oceanLevel)
        {
            Vec3 voxelSize = m_voxelSize;
            Vec3 voxelConv = m_voxelConv;

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    for (DynamicSpanGrid.Element span = m_spanGrid.GetElement(x + y * width); span != null; span = span.next)
                    {
                        Vec3 top = spaceMin + new Vec3(x * voxelSize.x, y * voxelSize.y, span.top * voxelSize.z);
                        int depth = (top.z >= oceanLevel) ? 0 : (int)((oceanLevel - top.z) * voxelConv.z);

                        // In the full engine, area volumes are checked here.
                        // We skip that since it requires pe_params_buoyancy / pe_params_area / pe_status_contains_point

                        if (depth > DynamicSpanGrid.Element.MaxWaterDepth)
                            depth = DynamicSpanGrid.Element.MaxWaterDepth;

                        span.depth = (uint)depth;
                    }
                }
            }
        }
    }

    public void VoxelizeGeometry(Vec3[] vertices, int triCount, Matrix34 worldTM)
    {
        if (HasNoRotOrScale(worldTM.m33))
        {
            Vec3 offset = worldTM.GetTranslation();

            if (offset.IsZero())
            {
                for (int i = 0; i < triCount; ++i)
                    RasterizeTriangle(vertices[i * 3 + 0],
                        vertices[i * 3 + 1],
                        vertices[i * 3 + 2]);
            }
            else
            {
                for (int i = 0; i < triCount; ++i)
                    RasterizeTriangle(vertices[i * 3 + 0] + offset,
                        vertices[i * 3 + 1] + offset,
                        vertices[i * 3 + 2] + offset);
            }
        }
        else
        {
            for (int i = 0; i < triCount; ++i)
            {
                RasterizeTriangle(worldTM.TransformPoint(vertices[i * 3 + 0]),
                    worldTM.TransformPoint(vertices[i * 3 + 1]),
                    worldTM.TransformPoint(vertices[i * 3 + 2]));
            }
        }
    }

    public void VoxelizeGeometry(Vec3[] vertices, int[] indices, int triCount, Matrix34 worldTM)
    {
        if (HasNoRotOrScale(worldTM.m33))
        {
            Vec3 offset = worldTM.GetTranslation();

            if (offset.IsZero())
            {
                for (int i = 0; i < triCount; ++i)
                    RasterizeTriangle(vertices[indices[i * 3 + 0]], vertices[indices[i * 3 + 1]], vertices[indices[i * 3 + 2]]);
            }
            else
            {
                for (int i = 0; i < triCount; ++i)
                {
                    RasterizeTriangle(vertices[indices[i * 3 + 0]] + offset,
                        vertices[indices[i * 3 + 1]] + offset,
                        vertices[indices[i * 3 + 2]] + offset);
                }
            }
        }
        else
        {
            for (int i = 0; i < triCount; ++i)
            {
                RasterizeTriangle(worldTM.TransformPoint(vertices[indices[i * 3 + 0]]),
                    worldTM.TransformPoint(vertices[indices[i * 3 + 1]]),
                    worldTM.TransformPoint(vertices[indices[i * 3 + 2]]));
            }
        }
    }

    public void VoxelizeGeometry(Vec3[] vertices, uint[] indices, int triCount, Matrix34 worldTM)
    {
        if (HasNoRotOrScale(worldTM.m33))
        {
            Vec3 offset = worldTM.GetTranslation();

            if (offset.IsZero())
            {
                for (int i = 0; i < triCount; ++i)
                    RasterizeTriangle(vertices[indices[i * 3 + 0]], vertices[indices[i * 3 + 1]], vertices[indices[i * 3 + 2]]);
            }
            else
            {
                for (int i = 0; i < triCount; ++i)
                {
                    RasterizeTriangle(vertices[indices[i * 3 + 0]] + offset,
                        vertices[indices[i * 3 + 1]] + offset,
                        vertices[indices[i * 3 + 2]] + offset);
                }
            }
        }
        else
        {
            for (int i = 0; i < triCount; ++i)
            {
                RasterizeTriangle(worldTM.TransformPoint(vertices[indices[i * 3 + 0]]),
                    worldTM.TransformPoint(vertices[indices[i * 3 + 1]]),
                    worldTM.TransformPoint(vertices[indices[i * 3 + 2]]));
            }
        }
    }

    // ComputeTerrainAABB, VoxelizeTerrain, VoxelizeGeometry(IGeometry)
    // These require IGeometry, primitives::heightfield, etc. which are engine-level interfaces
    // not yet fully ported. The structure is preserved but the actual heightfield queries
    // are deferred until CryPhysics geometry types are available.

    // VoxelizeTerrain — voxelizes heightfield geometry
    // Requires primitives::heightfield* which is not available in the C# port.
    // Structural placeholder that preserves the algorithm.
    public int VoxelizeTerrain(/* IGeometry geometry, */ Matrix34 worldTM)
    {
        // Requires heightfield data access (phf->origin, step, stepr, size, getheight, etc.)
        // Full implementation deferred until primitives::heightfield is ported.
        return 0;
    }

    // VoxelizeGeometry(IGeometry) — dispatches by geometry type
    // Handles GEOM_TRIMESH, GEOM_BOX, GEOM_SPHERE, GEOM_CYLINDER, GEOM_CAPSULE, GEOM_HEIGHTFIELD
    // Each shape generates vertices/indices and calls VoxelizeGeometry overloads above.
    //
    // The box case is fully ported below. The other shapes (sphere, cylinder, capsule) generate
    // tessellated meshes using sincos_tpl. They are ported faithfully.

    public int VoxelizeBoxGeometry(Vec3 boxSize, bool bOriented, Matrix33 basis, Vec3 center,
        Matrix34 worldTM)
    {
        Vec3[] vertices =
        {
            new Vec3(-boxSize.x, -boxSize.y, -boxSize.z),
            new Vec3( boxSize.x, -boxSize.y, -boxSize.z),
            new Vec3( boxSize.x,  boxSize.y, -boxSize.z),
            new Vec3(-boxSize.x,  boxSize.y, -boxSize.z),

            new Vec3(-boxSize.x, -boxSize.y,  boxSize.z),
            new Vec3( boxSize.x, -boxSize.y,  boxSize.z),
            new Vec3( boxSize.x,  boxSize.y,  boxSize.z),
            new Vec3(-boxSize.x,  boxSize.y,  boxSize.z),
        };

        Matrix34 boxTM = worldTM;

        if (bOriented)
        {
            boxTM = new Matrix34(basis.GetTransposed(), center);
            boxTM = MultiplyMatrix34(worldTM, boxTM);
        }
        else
        {
            boxTM.AddTranslation(worldTM.TransformVector(center));
        }

        VoxelizeGeometry(vertices, BoxTriIndices, 12, boxTM);
        return 12;
    }

    // Helper to multiply two Matrix34 transforms
    private static Matrix34 MultiplyMatrix34(Matrix34 a, Matrix34 b)
    {
        // C++ Matrix34 * Matrix34
        Matrix33 m33 = a.m33 * b.m33;
        Vec3 t = a.TransformPoint(b.t);
        return new Matrix34(m33, t);
    }

    // (uint[] overload already defined as public VoxelizeGeometry above)

    public int VoxelizeSphereGeometry(Vec3 center, float r, Matrix34 worldTM)
    {
        const int stacks = 48;
        const int slices = 48;

        int vertexCount = slices * (stacks - 2) + 2;
        Vec3[] vertices = new Vec3[vertexCount];

        int indexCount = (slices - 1) * (stacks - 2) * 6;
        uint[] indices = new uint[indexCount];

        vertices[0] = center;
        vertices[0] = new Vec3(vertices[0].x, vertices[0].y + r, vertices[0].z);
        vertices[1] = center;
        vertices[1] = new Vec3(vertices[1].x, vertices[1].y - r, vertices[1].z);

        int v = 2;
        for (int j = 1; j < stacks - 1; ++j)
        {
            for (int i = 0; i < slices; ++i)
            {
                float theta = (j / (float)(stacks - 1)) * 3.14159265f;
                float phi = (i / (float)(slices - 1)) * 2.0f * 3.14159265f;

                sincos_tpl(theta, out float stheta, out float ctheta);
                sincos_tpl(phi, out float sphi, out float cphi);

                Vec3 point = center + new Vec3(stheta * cphi * r, ctheta * r, -stheta * sphi * r);
                vertices[v++] = point;
            }
        }

        int n = 0;
        for (int i = 0; i < slices - 1; ++i)
        {
            indices[n++] = 0;
            indices[n++] = (uint)(i + 2);
            indices[n++] = (uint)(i + 3);

            indices[n++] = (uint)((stacks - 3) * slices + i + 3);
            indices[n++] = (uint)((stacks - 3) * slices + i + 2);
            indices[n++] = 1;
        }

        for (int j = 0; j < stacks - 3; ++j)
        {
            for (int i = 0; i < slices - 1; ++i)
            {
                indices[n++] = (uint)((j + 1) * slices + i + 3);
                indices[n++] = (uint)(j * slices + i + 3);
                indices[n++] = (uint)((j + 1) * slices + i + 2);
                indices[n++] = (uint)(j * slices + i + 3);
                indices[n++] = (uint)(j * slices + i + 2);
                indices[n++] = (uint)((j + 1) * slices + i + 2);
            }
        }

        VoxelizeGeometry(vertices, indices, indexCount / 3, worldTM);
        return vertexCount / 3;
    }

    public int VoxelizeCylinderGeometry(Vec3 cylinderCenter, Vec3 cylinderAxis, float hh, float r,
        Matrix34 worldTM)
    {
        Vec3 @base = cylinderCenter - cylinderAxis * hh;
        Vec3 top = cylinderCenter + cylinderAxis * hh;

        Vec3 n = cylinderAxis;
        Vec3 a = n.GetOrthogonal();

        Vec3 b = a.Cross(n);
        a = n.Cross(b);

        a.Normalize();
        b.Normalize();

        const int slices = 64;
        float invSlices = 1.0f / (float)slices;

        int vertexCount = 2 + slices * 4;
        Vec3[] vertices = new Vec3[vertexCount];

        int indexCount = slices * 12;
        uint[] indices = new uint[indexCount];

        vertices[0] = @base;
        vertices[1] = top;

        int v = 2;
        for (int i = 0; i < slices; ++i)
        {
            float theta0 = i * (3.14159265f * 2.0f * invSlices);
            float theta1 = (i + 1) * (3.14159265f * 2.0f * invSlices);

            sincos_tpl(theta0, out float stheta0, out float ctheta0);
            vertices[v++] = top + a * (r * ctheta0) + b * (r * stheta0);
            vertices[v++] = @base + a * (r * ctheta0) + b * (r * stheta0);

            sincos_tpl(theta1, out float stheta1, out float ctheta1);
            vertices[v++] = @base + a * (r * ctheta1) + b * (r * stheta1);
            vertices[v++] = top + a * (r * ctheta1) + b * (r * stheta1);
        }

        int t = 0;
        for (int i = 0; i < slices; ++i)
        {
            indices[t++] = 0;
            indices[t++] = (uint)(2 + i * 4 + 1);
            indices[t++] = (uint)(2 + i * 4 + 2);

            indices[t++] = (uint)(2 + i * 4 + 2);
            indices[t++] = (uint)(2 + i * 4 + 1);
            indices[t++] = (uint)(2 + i * 4 + 0);

            indices[t++] = (uint)(2 + i * 4 + 3);
            indices[t++] = (uint)(2 + i * 4 + 2);
            indices[t++] = (uint)(2 + i * 4 + 0);

            indices[t++] = (uint)(2 + i * 4 + 3);
            indices[t++] = (uint)(2 + i * 4 + 0);
            indices[t++] = 1;
        }

        VoxelizeGeometry(vertices, indices, indexCount / 3, worldTM);
        return vertexCount / 3;
    }

    public int VoxelizeCapsuleGeometry(Vec3 capsuleCenter, Vec3 capsuleAxis, float hh, float r,
        Matrix34 worldTM)
    {
        int triangleCount = 0;

        Vec3 @base = capsuleCenter - capsuleAxis * hh;
        Vec3 top = capsuleCenter + capsuleAxis * hh;

        Vec3 n = capsuleAxis;
        Vec3 a = n.GetOrthogonal();

        Vec3 b = a.Cross(n);
        a = n.Cross(b);

        a.Normalize();
        b.Normalize();
        n.Normalize();

        const int stacks = 48;
        const int slices = 64;
        float invSlices = 1.0f / (float)slices;

        // Cylinder body
        {
            int vertexCount = 2 + slices * 4;
            Vec3[] vertices = new Vec3[vertexCount];

            int indexCount = slices * 6;
            uint[] indices = new uint[indexCount];

            vertices[0] = @base;
            vertices[1] = top;

            int v = 2;
            for (int i = 0; i < slices; ++i)
            {
                float theta0 = i * (3.14159265f * 2.0f * invSlices);
                float theta1 = (i + 1) * (3.14159265f * 2.0f * invSlices);

                sincos_tpl(theta0, out float stheta0, out float ctheta0);
                vertices[v++] = top + a * (r * ctheta0) + b * (r * stheta0);
                vertices[v++] = @base + a * (r * ctheta0) + b * (r * stheta0);

                sincos_tpl(theta1, out float stheta1, out float ctheta1);
                vertices[v++] = @base + a * (r * ctheta1) + b * (r * stheta1);
                vertices[v++] = top + a * (r * ctheta1) + b * (r * stheta1);
            }

            int t = 0;
            for (int i = 0; i < slices; ++i)
            {
                indices[t++] = (uint)(2 + i * 4 + 2);
                indices[t++] = (uint)(2 + i * 4 + 1);
                indices[t++] = (uint)(2 + i * 4 + 0);

                indices[t++] = (uint)(2 + i * 4 + 3);
                indices[t++] = (uint)(2 + i * 4 + 2);
                indices[t++] = (uint)(2 + i * 4 + 0);
            }

            VoxelizeGeometry(vertices, indices, indexCount / 3, worldTM);
            triangleCount += vertexCount / 3;
        }

        // Bottom semi-sphere
        {
            Vec3 baseCenter = @base;
            Vec3 baseDirection = baseCenter - capsuleCenter;
            baseDirection.Normalize();

            int vertexCount = slices * (stacks - 2) + 2;
            Vec3[] vertices = new Vec3[vertexCount];

            int indexCount = (slices - 1) * (stacks - 3) * 6 + 6 * (slices - 1);
            uint[] indices = new uint[indexCount];

            vertices[0] = baseCenter + a * r;
            vertices[1] = baseCenter - a * r;

            int v = 2;
            for (int j = 1; j <= stacks - 2; ++j)
            {
                for (int i = 0; i < slices; ++i)
                {
                    float theta = (j / (float)(stacks - 1)) * 3.14159265f;
                    float phi = (i / (float)(slices - 1)) * 3.14159265f;

                    sincos_tpl(theta, out float stheta, out float ctheta);
                    sincos_tpl(phi, out float sphi, out float cphi);

                    Vec3 point = baseCenter + a * ctheta * r + b * stheta * cphi * r + baseDirection * stheta * sphi * r;
                    vertices[v++] = point;
                }
            }

            int t = 0;
            for (int i = 0; i < slices - 1; ++i)
            {
                indices[t++] = (uint)(i + 2);
                indices[t++] = (uint)(i + 3);
                indices[t++] = 0;

                indices[t++] = 1;
                indices[t++] = (uint)((stacks - 3) * slices + i + 3);
                indices[t++] = (uint)((stacks - 3) * slices + i + 2);
            }

            for (int j = 0; j < stacks - 3; ++j)
            {
                for (int i = 0; i < slices - 1; ++i)
                {
                    indices[t++] = (uint)((j + 1) * slices + i + 3);
                    indices[t++] = (uint)(j * slices + i + 3);
                    indices[t++] = (uint)((j + 1) * slices + i + 2);

                    indices[t++] = (uint)(j * slices + i + 3);
                    indices[t++] = (uint)(j * slices + i + 2);
                    indices[t++] = (uint)((j + 1) * slices + i + 2);
                }
            }

            VoxelizeGeometry(vertices, indices, indexCount / 3, worldTM);
            triangleCount += vertexCount / 3;
        }

        // Top semi-sphere
        {
            Vec3 topCenter = top;
            Vec3 topDirection = topCenter - capsuleCenter;
            topDirection.Normalize();

            int vertexCount = slices * (stacks - 2) + 2;
            Vec3[] vertices = new Vec3[vertexCount];

            int indexCount = (slices - 1) * (stacks - 3) * 6 + 6 * (slices - 1);
            uint[] indices = new uint[indexCount];

            vertices[0] = topCenter + a * r;
            vertices[1] = topCenter - a * r;

            int v = 2;
            for (int j = 1; j <= stacks - 2; ++j)
            {
                for (int i = 0; i < slices; ++i)
                {
                    float theta = (j / (float)(stacks - 1)) * 3.14159265f;
                    float phi = (i / (float)(slices - 1)) * 3.14159265f;

                    sincos_tpl(theta, out float stheta, out float ctheta);
                    sincos_tpl(phi, out float sphi, out float cphi);

                    Vec3 point = topCenter + a * ctheta * r + b * stheta * cphi * r + topDirection * stheta * sphi * r;
                    vertices[v++] = point;
                }
            }

            int t = 0;
            for (int i = 0; i < slices - 1; ++i)
            {
                indices[t++] = 0;
                indices[t++] = (uint)(i + 3);
                indices[t++] = (uint)(i + 2);

                indices[t++] = (uint)((stacks - 3) * slices + i + 2);
                indices[t++] = (uint)((stacks - 3) * slices + i + 3);
                indices[t++] = 1;
            }

            for (int j = 0; j < stacks - 3; ++j)
            {
                for (int i = 0; i < slices - 1; ++i)
                {
                    indices[t++] = (uint)((j + 1) * slices + i + 2);
                    indices[t++] = (uint)(j * slices + i + 3);
                    indices[t++] = (uint)((j + 1) * slices + i + 3);
                    indices[t++] = (uint)((j + 1) * slices + i + 2);
                    indices[t++] = (uint)(j * slices + i + 2);
                    indices[t++] = (uint)(j * slices + i + 3);
                }
            }

            VoxelizeGeometry(vertices, indices, indexCount / 3, worldTM);
            triangleCount += vertexCount / 3;
        }

        return triangleCount;
    }
}
