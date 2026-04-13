// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/OffGridLinks.h (152L)
// and OffGridLinks.cpp (497L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CryAISystem.Navigation.MNM.MNMUtils;
using CryAISystem.Navigation.NavigationSystem;

namespace CryAISystem.Navigation.MNM;

/// <summary>
/// One of these objects is bound to every Navigation Mesh.
/// Keeps track of off-mesh links per Tile.
/// Each tile can have up to 1024 Triangle links.
/// </summary>
public class OffMeshNavigation
{
    // TriangleLink — internal link data (public access needed by QueryLinksResult)
    internal class TriangleLink
    {
        public uint startTriangleID;  // TriangleID
        public uint endTriangleID;    // TriangleID
        public uint linkID;           // OffMeshLinkID
    }

    // TileLinks — links for a specific tile
    private class TileLinks
    {
        public TriangleLink[] triangleLinks;
        public ushort triangleLinkCount;

        public TileLinks() { triangleLinks = null; triangleLinkCount = 0; }

        public void CopyLinks(TriangleLink[] links, ushort linkCount)
        {
            if (triangleLinkCount != linkCount)
            {
                triangleLinks = null;
                triangleLinkCount = linkCount;
                if (linkCount > 0)
                    triangleLinks = new TriangleLink[linkCount];
            }

            if (linkCount > 0)
                Array.Copy(links, triangleLinks, linkCount);
        }
    }

    // STileLinks — both links and lookups
    private class STileLinks
    {
        public TileLinks links = new TileLinks();
        public TileLinks lookups = new TileLinks();
    }

    // QueryLinksResult
    public class QueryLinksResult
    {
        private TriangleLink[] pFirstLink;
        private int startIndex;
        private int currentLink;
        private ushort linkCount;

        internal QueryLinksResult(TriangleLink[] _firstLink, int _startIndex, ushort _linkCount)
        {
            pFirstLink = _firstLink;
            startIndex = _startIndex;
            currentLink = 0;
            linkCount = _linkCount;
        }

        public WayTriangleData GetNextTriangle()
        {
            if (currentLink < linkCount)
            {
                currentLink++;
                return new WayTriangleData(
                    pFirstLink[startIndex + currentLink - 1].endTriangleID,
                    pFirstLink[startIndex + currentLink - 1].linkID);
            }
            return new WayTriangleData(0, 0);
        }
    }

    private Dictionary<uint, STileLinks> m_tilesLinks = new();
    private static uint m_linkIDGenerator = Constants.eOffMeshLinks_InvalidOffMeshLinkID;
    private Dictionary<uint, OffMeshLinkPtr> m_offmeshLinks = new();

    public OffMeshNavigation() { }

    public OffMeshLinkPtr AddLink(NavigationMesh navigationMesh, uint startTriangleID, uint endTriangleID,
        OffMeshLink linkData, ref uint linkID, bool refData)
    {
        const int kMaxTileLinks = 1024;

        uint tileID = ComputeTileID(startTriangleID);

        // Find or create tile links
        TileLinks pTileLinks;
        if (m_tilesLinks.TryGetValue(tileID, out var stLinks))
        {
            pTileLinks = stLinks.links;
        }
        else
        {
            stLinks = new STileLinks();
            m_tilesLinks[tileID] = stLinks;
            pTileLinks = stLinks.links;
        }

        Debug.Assert(pTileLinks != null && (pTileLinks.triangleLinkCount + 1) < kMaxTileLinks);

        // Find insert position
        ushort triangleIdx = pTileLinks.triangleLinkCount; // Default to end
        for (ushort idx = 0; idx < pTileLinks.triangleLinkCount; ++idx)
        {
            if (pTileLinks.triangleLinks[idx].startTriangleID == startTriangleID)
            {
                triangleIdx = idx;
                break;
            }
        }

        // Generate link ID if needed
        if (linkID == Constants.eOffMeshLinks_InvalidOffMeshLinkID)
        {
            do { ++m_linkIDGenerator; } while (m_linkIDGenerator == Constants.eOffMeshLinks_InvalidOffMeshLinkID);
            linkID = m_linkIDGenerator;
        }
        Debug.Assert(linkID <= m_linkIDGenerator);

        // Begin insert/copy process
        TriangleLink[] tempTriangleLinks = new TriangleLink[kMaxTileLinks];

        if (triangleIdx > 0 && pTileLinks.triangleLinks != null)
        {
            Array.Copy(pTileLinks.triangleLinks, tempTriangleLinks, triangleIdx);
        }

        tempTriangleLinks[triangleIdx] = new TriangleLink();
        tempTriangleLinks[triangleIdx].startTriangleID = startTriangleID;
        tempTriangleLinks[triangleIdx].endTriangleID = endTriangleID;
        tempTriangleLinks[triangleIdx].linkID = linkID;

        int diffCount = pTileLinks.triangleLinkCount - triangleIdx;
        if (diffCount > 0)
        {
            Array.Copy(pTileLinks.triangleLinks, triangleIdx, tempTriangleLinks, triangleIdx + 1, diffCount);
        }

        pTileLinks.CopyLinks(tempTriangleLinks, (ushort)(pTileLinks.triangleLinkCount + 1));

        // Update tile mesh
        if (triangleIdx < (pTileLinks.triangleLinkCount - 1))
        {
            uint currentTriangleID = startTriangleID;
            for (ushort idx = (ushort)(triangleIdx + 1); idx < pTileLinks.triangleLinkCount; ++idx)
            {
                if (pTileLinks.triangleLinks[idx].startTriangleID != currentTriangleID)
                {
                    currentTriangleID = pTileLinks.triangleLinks[idx].startTriangleID;
                    navigationMesh.grid.UpdateOffMeshLinkForTile(tileID, currentTriangleID, idx);
                }
            }
        }
        else
        {
            navigationMesh.grid.AddOffMeshLinkToTile(tileID, startTriangleID, triangleIdx);
        }

        // Create reverse lookup
        AddLookupLink(endTriangleID, startTriangleID, linkID);

        // Resolve and store the link data
        OffMeshLinkPtr pOffMeshLink = new OffMeshLinkPtr();
        if (refData)
        {
            pOffMeshLink.reset(linkData);
        }
        else
        {
            var cloned = linkData.Clone();
            cloned.SetLinkID(linkID);
            pOffMeshLink.reset(cloned);
        }

        m_offmeshLinks[linkID] = pOffMeshLink;

        return pOffMeshLink;
    }

    public void RemoveLink(NavigationMesh navigationMesh, uint boundTriangleID, uint linkID)
    {
        uint tileID = ComputeTileID(boundTriangleID);

        if (!m_tilesLinks.TryGetValue(tileID, out var stLinks))
            return;

        const int maxTileLinks = 1024;
        TriangleLink[] tempTriangleLinks = new TriangleLink[maxTileLinks];

        TileLinks tileLinks = stLinks.links;

        ushort copyCount = 0;
        for (ushort triIdx = 0; triIdx < tileLinks.triangleLinkCount; ++triIdx)
        {
            TriangleLink triangleLink = tileLinks.triangleLinks[triIdx];
            if (triangleLink.linkID != linkID)
            {
                tempTriangleLinks[copyCount] = triangleLink;
                copyCount++;
            }
            else
            {
                RemoveLookupLink(triangleLink.endTriangleID, linkID);
                RemoveLinkData(linkID);
            }
        }

        tileLinks.CopyLinks(tempTriangleLinks, copyCount);

        // Update triangle off-mesh indices
        ushort boundTriangleLeftLinks = 0;
        uint currentTriangleID = 0;
        for (ushort triIdx = 0; triIdx < tileLinks.triangleLinkCount; ++triIdx)
        {
            TriangleLink triangleLink = tileLinks.triangleLinks[triIdx];

            if (currentTriangleID != triangleLink.startTriangleID)
            {
                currentTriangleID = triangleLink.startTriangleID;
                navigationMesh.grid.UpdateOffMeshLinkForTile(tileID, currentTriangleID, triIdx);
            }
            boundTriangleLeftLinks += (ushort)((currentTriangleID == boundTriangleID) ? 1 : 0);
        }

        if (boundTriangleLeftLinks == 0)
        {
            navigationMesh.grid.RemoveOffMeshLinkFromTile(tileID, boundTriangleID);
        }
    }

    private void AddLookupLink(uint startTriangleID, uint endTriangleID, uint linkID)
    {
        const int kMaxTileLinks = 1024;

        uint tileID = ComputeTileID(startTriangleID);

        TileLinks pTileLinks;
        if (m_tilesLinks.TryGetValue(tileID, out var stLinks))
        {
            pTileLinks = stLinks.lookups;
        }
        else
        {
            stLinks = new STileLinks();
            m_tilesLinks[tileID] = stLinks;
            pTileLinks = stLinks.lookups;
        }

        Debug.Assert(pTileLinks != null && (pTileLinks.triangleLinkCount + 1) < kMaxTileLinks);

        ushort triangleIdx = pTileLinks.triangleLinkCount;
        for (ushort idx = 0; idx < pTileLinks.triangleLinkCount; ++idx)
        {
            if (pTileLinks.triangleLinks[idx].startTriangleID == startTriangleID)
            {
                triangleIdx = idx;
                break;
            }
        }

        TriangleLink[] tempTriangleLinks = new TriangleLink[kMaxTileLinks];

        if (triangleIdx > 0 && pTileLinks.triangleLinks != null)
        {
            Array.Copy(pTileLinks.triangleLinks, tempTriangleLinks, triangleIdx);
        }

        tempTriangleLinks[triangleIdx] = new TriangleLink();
        tempTriangleLinks[triangleIdx].startTriangleID = startTriangleID;
        tempTriangleLinks[triangleIdx].endTriangleID = endTriangleID;
        tempTriangleLinks[triangleIdx].linkID = linkID;

        int diffCount = pTileLinks.triangleLinkCount - triangleIdx;
        if (diffCount > 0)
        {
            Array.Copy(pTileLinks.triangleLinks, triangleIdx, tempTriangleLinks, triangleIdx + 1, diffCount);
        }

        pTileLinks.CopyLinks(tempTriangleLinks, (ushort)(pTileLinks.triangleLinkCount + 1));
    }

    private void RemoveLookupLink(uint boundTriangleID, uint linkID)
    {
        uint tileID = ComputeTileID(boundTriangleID);

        if (!m_tilesLinks.TryGetValue(tileID, out var stLinks))
            return;

        const int maxTileLinks = 1024;
        TriangleLink[] tempTriangleLinks = new TriangleLink[maxTileLinks];

        TileLinks tileLinks = stLinks.lookups;

        ushort copyCount = 0;
        for (ushort triIdx = 0; triIdx < tileLinks.triangleLinkCount; ++triIdx)
        {
            TriangleLink triangleLink = tileLinks.triangleLinks[triIdx];
            if (triangleLink.linkID != linkID)
            {
                tempTriangleLinks[copyCount] = triangleLink;
                copyCount++;
            }
        }

        tileLinks.CopyLinks(tempTriangleLinks, copyCount);
    }

    private bool RemoveLinkData(uint linkID)
    {
        return m_offmeshLinks.Remove(linkID);
    }

    public void InvalidateLinks(uint tileID)
    {
        if (m_tilesLinks.TryGetValue(tileID, out var stLinks))
        {
            TileLinks tileLinks = stLinks.links;
            for (ushort triangleIdx = 0; triangleIdx < tileLinks.triangleLinkCount; ++triangleIdx)
            {
                TriangleLink link = tileLinks.triangleLinks[triangleIdx];
                RemoveLookupLink(link.endTriangleID, link.linkID);
                RemoveLinkData(link.linkID);
            }

            m_tilesLinks.Remove(tileID);
        }
    }

    public QueryLinksResult GetLinksForTriangle(uint triangleID, ushort index)
    {
        uint tileID = ComputeTileID(triangleID);

        if (m_tilesLinks.TryGetValue(tileID, out var stLinks))
        {
            TileLinks tileLinks = stLinks.links;
            if (index < tileLinks.triangleLinkCount)
            {
                ushort linkCount = 1;

                for (ushort triIdx = (ushort)(index + 1); triIdx < tileLinks.triangleLinkCount; ++triIdx)
                {
                    if (tileLinks.triangleLinks[triIdx].startTriangleID == triangleID)
                        linkCount++;
                    else
                        break;
                }

                return new QueryLinksResult(tileLinks.triangleLinks, index, linkCount);
            }
        }

        return new QueryLinksResult(null, 0, 0);
    }

    public QueryLinksResult GetLookupsForTriangle(uint triangleID)
    {
        uint tileID = ComputeTileID(triangleID);

        if (m_tilesLinks.TryGetValue(tileID, out var stLinks))
        {
            TileLinks tileLinks = stLinks.lookups;
            int firstIndex = -1;
            ushort linkCount = 0;

            for (ushort idx = 0; idx < tileLinks.triangleLinkCount; ++idx)
            {
                if (tileLinks.triangleLinks[idx].startTriangleID == triangleID)
                {
                    if (firstIndex == -1)
                        firstIndex = idx;
                    ++linkCount;
                }
                else if (firstIndex >= 0)
                    break;
            }

            if (firstIndex >= 0)
                return new QueryLinksResult(tileLinks.triangleLinks, firstIndex, linkCount);
        }

        return new QueryLinksResult(null, 0, 0);
    }

    public OffMeshLink GetObjectLinkInfo(uint linkID)
    {
        if (m_offmeshLinks.TryGetValue(linkID, out var ptr))
            return ptr.get();
        return null;
    }

    public bool CanUseLink(IEntity pRequester, uint linkID, float[] costMultiplier, float pathSharingPenalty = 0)
    {
        OffMeshLink pOffMeshLink = GetObjectLinkInfo(linkID);
        if (pOffMeshLink != null && pRequester != null)
        {
            return pOffMeshLink.CanUse(pRequester, costMultiplier);
        }
        return false;
    }
}
