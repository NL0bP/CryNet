// Literal port of dev/Code/CryEngine/CryAISystem/Free2DNavRegion.h
// .cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public class CFree2DNavRegion : CNavRegion
{
    public CFree2DNavRegion(CGraph pGraph) { /* impl in .cpp */ }
    // virtual ~CFree2DNavRegion();

    public override void BeautifyPath(
        VectorConstNodeIndices inPath, TPathPoints outPath,
        Vec3 startPos, Vec3 startDir,
        Vec3 endPos, Vec3 endDir,
        float radius,
        AgentMovementAbility movementAbility,
        NavigationBlockers navigationBlockers)
    { /* impl in .cpp */ }

    public override void UglifyPath(VectorConstNodeIndices inPath, TPathPoints outPath,
        Vec3 startPos, Vec3 startDir,
        Vec3 endPos, Vec3 endDir)
    { /* impl in .cpp */ }

    public override uint GetEnclosing(Vec3 pos, float passRadius = 0.0f, uint startIndex = 0,
        float range = -1.0f, Vec3? closestValid = null, bool returnSuspect = false, string requesterName = "", bool omitWalkabilityTest = false)
    { return 0; /* impl in .cpp */ }

    public override void Clear() { /* impl in .cpp */ }

    public override void Serialize(TSerialize ser) { }

    public override bool CheckPassability(Vec3 from, Vec3 to, float radius, NavigationBlockers navigationBlockers, uint navCapMask) { return false; /* impl in .cpp */ }

    public override bool GetSingleNodePath(GraphNode pNode, Vec3 startPos, Vec3 endPos, float radius,
        NavigationBlockers navigationBlockers, List<PathPointDescriptor> points, uint navCapMask)
    { return false; /* impl in .cpp */ }

    public override nuint MemStats() { return 0; /* impl in .cpp */ }

    private GraphNode m_pDummyNode;
    private uint m_dummyNodeIndex;
}

// Forward decls / shells
public class CNavRegion
{
    public virtual void BeautifyPath(VectorConstNodeIndices inPath, TPathPoints outPath, Vec3 startPos, Vec3 startDir, Vec3 endPos, Vec3 endDir, float radius, AgentMovementAbility movementAbility, NavigationBlockers navigationBlockers) { }
    public virtual void UglifyPath(VectorConstNodeIndices inPath, TPathPoints outPath, Vec3 startPos, Vec3 startDir, Vec3 endPos, Vec3 endDir) { }
    public virtual uint GetEnclosing(Vec3 pos, float passRadius = 0.0f, uint startIndex = 0, float range = -1.0f, Vec3? closestValid = null, bool returnSuspect = false, string requesterName = "", bool omitWalkabilityTest = false) { return 0; }
    public virtual void Clear() { }
    public virtual void Serialize(TSerialize ser) { }
    public virtual bool CheckPassability(Vec3 from, Vec3 to, float radius, NavigationBlockers navigationBlockers, uint navCapMask) { return false; }
    public virtual bool GetSingleNodePath(GraphNode pNode, Vec3 startPos, Vec3 endPos, float radius, NavigationBlockers navigationBlockers, List<PathPointDescriptor> points, uint navCapMask) { return false; }
    public virtual nuint MemStats() { return 0; }
}
// CGraph literal port lives in Graph.cs
// VectorConstNodeIndices defined in Graph.cs
