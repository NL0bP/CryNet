// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/IslandConnections.h (125L)
// and IslandConnections.cpp (201L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CryAISystem.Navigation.MNM;

public class IslandConnections
{
    // TIslandsWay — std::list<MNM::GlobalIslandID>
    // Using LinkedList<GlobalIslandID> in C#
    // Provide a type alias for external references
    public class TIslandsWay : LinkedList<GlobalIslandID> { }

    public struct Link : IEquatable<Link>
    {
        public uint toTriangleID;       // MNM::TriangleID
        public uint offMeshLinkID;      // MNM::OffMeshLinkID
        public GlobalIslandID toIsland;
        public uint objectIDThatCreatesTheConnection;

        public Link(uint _toTriangleID, uint _offMeshLinkID, GlobalIslandID _toIsland, uint _objectIDThatCreatesTheConnection)
        {
            toTriangleID = _toTriangleID;
            offMeshLinkID = _offMeshLinkID;
            toIsland = _toIsland;
            objectIDThatCreatesTheConnection = _objectIDThatCreatesTheConnection;
        }

        public bool Equals(Link rhs) =>
            toIsland == rhs.toIsland && toTriangleID == rhs.toTriangleID && offMeshLinkID == rhs.offMeshLinkID &&
                objectIDThatCreatesTheConnection == rhs.objectIDThatCreatesTheConnection;

        public override bool Equals(object obj) => obj is Link l && Equals(l);
        public override int GetHashCode() => HashCode.Combine(toTriangleID, offMeshLinkID, toIsland.id, objectIDThatCreatesTheConnection);
    }

    // IslandNode — private type used in path-finding
    private struct IslandNode : IComparable<IslandNode>, IEquatable<IslandNode>
    {
        public GlobalIslandID id;
        public float cost;

        public IslandNode(GlobalIslandID _id, float _cost) { id = _id; cost = _cost; }

        public int CompareTo(IslandNode other) => id.id.CompareTo(other.id.id);
        public bool Equals(IslandNode other) => id == other.id;
        public override bool Equals(object obj) => obj is IslandNode n && Equals(n);
        public override int GetHashCode() => id.GetHashCode();
    }

    private SortedDictionary<GlobalIslandID, List<Link>> m_islandConnections = new();

    public IslandConnections() { }

    public void Reset()
    {
        m_islandConnections.Clear();
    }

    public void SetOneWayConnectionBetweenIsland(GlobalIslandID fromIsland, Link link)
    {
        if (!m_islandConnections.TryGetValue(fromIsland, out var links))
        {
            links = new List<Link>();
            m_islandConnections[fromIsland] = links;
        }
        // stl::push_back_unique
        if (!links.Contains(link))
            links.Add(link);
    }

    public void RemoveOneWayConnectionBetweenIsland(GlobalIslandID fromIsland, Link link)
    {
        if (m_islandConnections.TryGetValue(fromIsland, out var links))
        {
            links.RemoveAll(l => l.Equals(link));
            if (links.Count == 0)
                m_islandConnections.Remove(fromIsland);
        }
    }

    public void RemoveAllIslandConnectionsForObject(NavigationMeshID meshID, uint objectId)
    {
        var keysToRemove = new List<GlobalIslandID>();
        foreach (var kvp in m_islandConnections)
        {
            if (new NavigationMeshID { id = kvp.Key.GetNavigationMeshIDAsUint32() }.id == meshID.id)
            {
                kvp.Value.RemoveAll(l => l.objectIDThatCreatesTheConnection == objectId);
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
            m_islandConnections.Remove(key);
    }

    public bool CanNavigateBetweenIslands(IEntity pEntityToTestOffGridLinks, GlobalIslandID fromIsland, GlobalIslandID toIsland, LinkedList<GlobalIslandID> way)
    {
        GlobalIslandID invalidID = new GlobalIslandID(Constants.eGlobalIsland_InvalidIslandID);
        if (fromIsland == invalidID || toIsland == invalidID)
            return false;

        if (fromIsland == toIsland)
            return true;

        NavigationMeshID startingMeshID = new NavigationMeshID { id = fromIsland.GetNavigationMeshIDAsUint32() };
        OffMeshNavigation offMeshLink = gAIEnv.pNavigationSystem.GetOffMeshNavigationManager().GetOffMeshNavigationForMesh(startingMeshID);

        int maxConnectedIsland = m_islandConnections.Count;

        var closedSet = new List<GlobalIslandID>(maxConnectedIsland);
        var openList = new OpenList<IslandNode>(maxConnectedIsland);
        openList.InsertElement(new IslandNode(fromIsland, 0));
        var cameFrom = new SortedDictionary<IslandNode, IslandNode>();

        while (!openList.IsEmpty())
        {
            IslandNode currentItem = openList.PopBestElement();

            if (currentItem.id == toIsland)
            {
                ReconstructWay(cameFrom, fromIsland, toIsland, way);
                return true;
            }

            closedSet.Add(currentItem.id);

            if (m_islandConnections.TryGetValue(currentItem.id, out var links))
            {
                foreach (var linkEntry in links)
                {
                    if (closedSet.Contains(linkEntry.toIsland))
                        continue;

                    bool canUseLink = pEntityToTestOffGridLinks != null
                        ? offMeshLink.CanUseLink(pEntityToTestOffGridLinks, linkEntry.offMeshLinkID, null)
                        : true;

                    if (canUseLink)
                    {
                        IslandNode nextIslandNode = new IslandNode(linkEntry.toIsland, currentItem.cost + 1.0f);

                        if (cameFrom.ContainsKey(nextIslandNode))
                            continue;

                        cameFrom[nextIslandNode] = currentItem;
                        openList.InsertElement(nextIslandNode);
                    }
                }
            }
        }

        way.Clear();
        return false;
    }

    private void ReconstructWay(SortedDictionary<IslandNode, IslandNode> cameFromMap, GlobalIslandID fromIsland, GlobalIslandID toIsland, LinkedList<GlobalIslandID> way)
    {
        IslandNode currentIsland = new IslandNode(toIsland, 0.0f);
        way.AddFirst(currentIsland.id);

        while (cameFromMap.TryGetValue(currentIsland, out IslandNode prev))
        {
            currentIsland = prev;
            way.AddFirst(currentIsland.id);
        }
    }

    public void DebugDraw()
    {
        // Debug drawing — requires CDebugDrawContext, deferred
    }
}
