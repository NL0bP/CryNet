// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/Tile.h (200L)
// and Tile.cpp (554L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static CryAISystem.Navigation.MNM.MNMUtils;
using static CryAISystem.Navigation.MNM.FixedPointMath;

namespace CryAISystem.Navigation.MNM;

public class Tile
{
    // Tile::Index
    // typedef uint16 Index

    // Tile::Vertex = FixedVec3<uint16, 5>
    public struct Vertex
    {
        public fixed_t_u16_5 x, y, z;

        public Vertex(fixed_t_u16_5 _x, fixed_t_u16_5 _y, fixed_t_u16_5 _z) { x = _x; y = _y; z = _z; }

        public Vec3 GetVec3() => new Vec3(x.as_float(), y.as_float(), z.as_float());

        // Binary serialization (6 bytes: 3 * uint16 raw fixed-point values)
        public static Vertex Read(BinaryReader r) { var v = new Vertex(); v.x.v = r.ReadUInt16(); v.y.v = r.ReadUInt16(); v.z.v = r.ReadUInt16(); return v; }
        public void Write(BinaryWriter w) { w.Write(x.v); w.Write(y.v); w.Write(z.v); }
    }

    // Tile::AABB = FixedAABB<uint16, 5>
    public struct TileAABB
    {
        public Vertex min, max;
        public TileAABB(Vertex _min, Vertex _max) { min = _min; max = _max; }
    }

    // Tile::Triangle
    public class Triangle
    {
        public ushort[] vertex = new ushort[3]; // Index into Tile::vertices array
        public ushort linkCount;    // 4 bits in C++
        public ushort firstLink;    // 12 bits in C++
        public uint islandID;       // StaticIslandID

        public Triangle() { }

        // Binary serialization (12 bytes: 3*uint16 vertex + uint16 packed linkCount/firstLink + uint32 islandID)
        public static Triangle Read(BinaryReader r)
        {
            var t = new Triangle();
            t.vertex[0] = r.ReadUInt16();
            t.vertex[1] = r.ReadUInt16();
            t.vertex[2] = r.ReadUInt16();
            ushort packed = r.ReadUInt16();
            t.linkCount = (ushort)(packed & 0xF);
            t.firstLink = (ushort)((packed >> 4) & 0xFFF);
            t.islandID = r.ReadUInt32();
            return t;
        }
        public void Write(BinaryWriter w)
        {
            w.Write(vertex[0]); w.Write(vertex[1]); w.Write(vertex[2]);
            ushort packed = (ushort)((linkCount & 0xF) | ((firstLink & 0xFFF) << 4));
            w.Write(packed);
            w.Write(islandID);
        }
    }

    // Tile::BVNode
    public class BVNode
    {
        public ushort leaf;   // 1 bit in C++
        public ushort offset; // 15 bits in C++
        public TileAABB aabb;

        // Binary serialization (14 bytes: uint16 packed leaf/offset + 6*uint16 aabb)
        public static BVNode Read(BinaryReader r)
        {
            var n = new BVNode();
            ushort packed = r.ReadUInt16();
            n.leaf = (ushort)(packed & 0x1);
            n.offset = (ushort)((packed >> 1) & 0x7FFF);
            n.aabb.min = Vertex.Read(r);
            n.aabb.max = Vertex.Read(r);
            return n;
        }
        public void Write(BinaryWriter w)
        {
            ushort packed = (ushort)((leaf & 0x1) | ((offset & 0x7FFF) << 1));
            w.Write(packed);
            aabb.min.Write(w);
            aabb.max.Write(w);
        }
    }

    // Tile::Link
    public class Link
    {
        public ushort side;      // 4 bits: side of the tile
        public ushort edge;      // 2 bits: local edge index
        public ushort triangle;  // 10 bits: index into Tile::triangles

        public const int OffMesh = 0xe;  // A link to a nonadjacent triangle
        public const int Internal = 0xf; // A link to an adjacent triangle

        public Link() { }

        public Link(Link other)
        {
            side = other.side;
            edge = other.edge;
            triangle = other.triangle;
        }

        // Binary serialization (2 bytes: uint16 packed side/edge/triangle)
        public static Link Read(BinaryReader r)
        {
            var l = new Link();
            ushort packed = r.ReadUInt16();
            l.side = (ushort)(packed & 0xF);
            l.edge = (ushort)((packed >> 4) & 0x3);
            l.triangle = (ushort)((packed >> 6) & 0x3FF);
            return l;
        }
        public void Write(BinaryWriter w)
        {
            ushort packed = (ushort)((side & 0xF) | ((edge & 0x3) << 4) | ((triangle & 0x3FF) << 6));
            w.Write(packed);
        }
    }

    // DrawFlags
    [Flags]
    public enum DrawFlags : uint
    {
        DrawTriangles      = 1 << 0,
        DrawInternalLinks  = 1 << 1,
        DrawExternalLinks  = 1 << 2,
        DrawOffMeshLinks   = 1 << 3,
        DrawMeshBoundaries = 1 << 4,
        DrawBVTree         = 1 << 5,
        DrawAccessibility  = 1 << 6,
        DrawTrianglesId    = 1 << 7,
        DrawIslandsId      = 1 << 8,
        DrawAll            = ~0u,
    }

    // Data
    public Triangle[] triangles;
    public Vertex[] vertices;
    public BVNode[] nodes;
    public Link[] links;

    public ushort triangleCount;
    public ushort vertexCount;
    public ushort nodeCount;
    public ushort linkCount;
    public uint hashValue;

    // Export information (MNM_USE_EXPORT_INFORMATION)
    private byte tileAccessible = 1;
    private byte[] trianglesAccessible;
    private ushort connectivityTriangleCount;

    public Tile()
    {
        triangles = null;
        vertices = null;
        nodes = null;
        links = null;
        triangleCount = 0;
        vertexCount = 0;
        nodeCount = 0;
        linkCount = 0;
        hashValue = 0;
    }

    public void CopyTriangles(Triangle[] _triangles, ushort count)
    {
        // MNM_USE_EXPORT_INFORMATION
        InitConnectivity(triangleCount, count);

        if (triangleCount != count)
        {
            triangles = null;
            triangleCount = count;
            if (count > 0)
                triangles = new Triangle[count];
        }

        if (count > 0)
            Array.Copy(_triangles, triangles, count);
    }

    public void CopyVertices(Vertex[] _vertices, ushort count)
    {
        if (vertexCount != count)
        {
            vertices = null;
            vertexCount = count;
            if (count > 0)
                vertices = new Vertex[count];
        }

        if (count > 0)
            Array.Copy(_vertices, vertices, count);
    }

    public void CopyNodes(BVNode[] _nodes, ushort count)
    {
        if (nodeCount != count)
        {
            nodes = null;
            nodeCount = count;
            if (count > 0)
                nodes = new BVNode[count];
        }

        if (count > 0)
            Array.Copy(_nodes, nodes, count);
    }

    public void CopyLinks(Link[] _links, ushort count)
    {
        if (linkCount != count)
        {
            links = null;
            linkCount = count;
            if (count > 0)
                links = new Link[count];
        }

        if (count > 0)
            Array.Copy(_links, links, count);
    }

    public void AddOffMeshLink(uint triangleID, ushort offMeshIndex)
    {
        ushort triangleIdx = ComputeTriangleIndex(triangleID);
        Debug.Assert(triangleIdx < triangleCount);
        if (triangleIdx < triangleCount)
        {
            Triangle triangle = triangles[triangleIdx];

            const int MaxLinkCount = 1024 * 6;
            Link[] tempLinks = new Link[MaxLinkCount];

            bool hasOffMeshLink = links != null && triangle.linkCount > 0 && triangle.firstLink < linkCount && links[triangle.firstLink].side == Link.OffMesh;

            Debug.Assert(!hasOffMeshLink, "Not adding offmesh link, already exists");

            if (!hasOffMeshLink)
            {
                // Add off-mesh link for triangle
                if (triangle.firstLink > 0)
                {
                    Debug.Assert(links != null);
                    for (int i = 0; i < triangle.firstLink; i++)
                        tempLinks[i] = new Link(links[i]);
                }

                tempLinks[triangle.firstLink] = new Link();
                tempLinks[triangle.firstLink].side = Link.OffMesh;
                tempLinks[triangle.firstLink].triangle = offMeshIndex;
                tempLinks[triangle.firstLink].edge = 0;

                int countDiff = linkCount - triangle.firstLink;
                if (countDiff > 0)
                {
                    Debug.Assert(links != null);
                    for (int i = 0; i < countDiff; i++)
                        tempLinks[triangle.firstLink + 1 + i] = new Link(links[triangle.firstLink + i]);
                }

                CopyLinks(tempLinks, (ushort)(linkCount + 1));

                // Re-arrange link indices for triangles
                triangle.linkCount++;

                for (ushort tIdx = (ushort)(triangleIdx + 1); tIdx < triangleCount; ++tIdx)
                {
                    triangles[tIdx].firstLink++;
                }
            }
        }
    }

    public void UpdateOffMeshLink(uint triangleID, ushort offMeshIndex)
    {
        ushort triangleIndex = ComputeTriangleIndex(triangleID);
        Debug.Assert(triangleIndex < triangleCount);
        if (triangleIndex < triangleCount)
        {
            ushort linkIdx = triangles[triangleIndex].firstLink;
            Debug.Assert(linkIdx < linkCount);
            if (linkIdx < linkCount)
            {
                Link link = links[linkIdx];
                Debug.Assert(link.side == Link.OffMesh);
                if (link.side == Link.OffMesh)
                {
                    link.triangle = offMeshIndex;
                }
            }
        }
    }

    public void RemoveOffMeshLink(uint triangleID)
    {
        ushort linkToRemoveIdx = 0xFFFF;
        ushort boundTriangleIdx = ComputeTriangleIndex(triangleID);
        if (boundTriangleIdx < triangleCount)
        {
            ushort firstLink = triangles[boundTriangleIdx].firstLink;
            if (triangles[boundTriangleIdx].linkCount > 0 && firstLink < linkCount && links[firstLink].side == Link.OffMesh)
                linkToRemoveIdx = firstLink;
        }

        Debug.Assert(linkToRemoveIdx != 0xFFFF, "Trying to remove off mesh link that doesn't exist");

        if (linkToRemoveIdx != 0xFFFF)
        {
            Debug.Assert(linkCount > 1);

            const int MaxLinkCount = 1024 * 6;
            Link[] tempLinks = new Link[MaxLinkCount];

            if (linkToRemoveIdx > 0)
            {
                for (int i = 0; i < linkToRemoveIdx; i++)
                    tempLinks[i] = new Link(links[i]);
            }

            int diffCount = linkCount - (linkToRemoveIdx + 1);
            if (diffCount > 0)
            {
                for (int i = 0; i < diffCount; i++)
                    tempLinks[linkToRemoveIdx + i] = new Link(links[linkToRemoveIdx + 1 + i]);
            }

            CopyLinks(tempLinks, (ushort)(linkCount - 1));

            // Re-arrange link indices for triangles
            triangles[boundTriangleIdx].linkCount--;

            for (ushort tIdx = (ushort)(boundTriangleIdx + 1); tIdx < triangleCount; ++tIdx)
            {
                triangles[tIdx].firstLink--;
            }
        }
    }

    public void Swap(Tile other)
    {
        var tmpTri = triangles; triangles = other.triangles; other.triangles = tmpTri;
        var tmpVert = vertices; vertices = other.vertices; other.vertices = tmpVert;
        var tmpNodes = nodes; nodes = other.nodes; other.nodes = tmpNodes;
        var tmpLinks = links; links = other.links; other.links = tmpLinks;

        // MNM_USE_EXPORT_INFORMATION
        InitConnectivity(triangleCount, other.triangleCount);

        (triangleCount, other.triangleCount) = (other.triangleCount, triangleCount);
        (vertexCount, other.vertexCount) = (other.vertexCount, vertexCount);
        (nodeCount, other.nodeCount) = (other.nodeCount, nodeCount);
        (linkCount, other.linkCount) = (other.linkCount, linkCount);
        (hashValue, other.hashValue) = (other.hashValue, hashValue);
    }

    public void Destroy()
    {
        triangles = null;
        vertices = null;
        nodes = null;
        links = null;

        // MNM_USE_EXPORT_INFORMATION
        trianglesAccessible = null;
        tileAccessible = 0;

        triangleCount = 0;
        vertexCount = 0;
        nodeCount = 0;
        linkCount = 0;
        hashValue = 0;
    }

    public real_t GetTriangleArea(uint triangleID)
    {
        Triangle triangle = triangles[ComputeTriangleIndex(triangleID)];

        vector3_t v0 = new vector3_t(vertices[triangle.vertex[0]]);
        vector3_t v1 = new vector3_t(vertices[triangle.vertex[1]]);
        vector3_t v2 = new vector3_t(vertices[triangle.vertex[2]]);

        real_t len0 = (v0 - v1).len();
        real_t len1 = (v0 - v2).len();
        real_t len2 = (v1 - v2).len();

        real_t s = (len0 + len1 + len2) / new real_t(2);

        return sqrtf(s * (s - len0) * (s - len1) * (s - len2));
    }

    // MNM_USE_EXPORT_INFORMATION
    private bool ConsiderExportInformation() => true;

    private void InitConnectivity(ushort oldTriangleCount, ushort newTriangleCount)
    {
        if (ConsiderExportInformation())
        {
            tileAccessible = 1;
            if (oldTriangleCount != newTriangleCount)
            {
                trianglesAccessible = null;
                if (newTriangleCount > 0)
                    trianglesAccessible = new byte[newTriangleCount];
            }

            if (newTriangleCount > 0 && trianglesAccessible != null)
            {
                for (int i = 0; i < newTriangleCount; i++)
                    trianglesAccessible[i] = 1;
            }
            connectivityTriangleCount = newTriangleCount;
        }
    }

    public void ResetConnectivity(byte accessible)
    {
        if (ConsiderExportInformation())
        {
            Debug.Assert(connectivityTriangleCount == triangleCount);
            tileAccessible = accessible;
            if (trianglesAccessible != null)
            {
                for (int i = 0; i < connectivityTriangleCount; i++)
                    trianglesAccessible[i] = accessible;
            }
        }
    }

    public bool IsTriangleAccessible(ushort triangleIdx)
    {
        Debug.Assert(triangleIdx >= 0 && triangleIdx < connectivityTriangleCount);
        return trianglesAccessible[triangleIdx] != 0;
    }

    public bool IsTileAccessible() => tileAccessible != 0;

    public void SetTriangleAccessible(ushort triangleIdx)
    {
        Debug.Assert(triangleIdx >= 0 && triangleIdx < connectivityTriangleCount);
        tileAccessible = 1;
        trianglesAccessible[triangleIdx] = 1;
    }

    // Draw is a debug rendering function — deferred (requires IRenderAuxGeom)
    public void Draw(uint drawFlags, vector3_t origin, uint tileID, List<float> islandAreas)
    {
        // Debug drawing — deferred to Phase 3c-draw
    }
}

// CompareLink and BreakOnMultipleAdjacencyLinkage — debug helpers, no-op in release
public static class TileDebug
{
    public static void BreakOnMultipleAdjacencyLinkage(Tile.Link[] start, int startIdx, int endIdx, Tile.Link linkToTest) { }
}
