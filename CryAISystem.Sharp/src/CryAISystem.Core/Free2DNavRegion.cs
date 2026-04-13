// Literal port of dev/Code/CryEngine/CryAISystem/Free2DNavRegion.h + Free2DNavRegion.cpp (60L + 327L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using CryAISystem.Walkability;

namespace CryAISystem;

public class CFree2DNavRegion : CNavRegion
{
    // Free2DNavRegion.cpp — CFree2DNavRegion::CFree2DNavRegion
    public CFree2DNavRegion(CGraph pGraph)
    {
        m_pDummyNode = null;
        m_dummyNodeIndex = 0;
    }

    // Free2DNavRegion.cpp — BeautifyPath
    public override void BeautifyPath(
        VectorConstNodeIndices inPath, TPathPoints outPath,
        Vec3 startPos, Vec3 startDir,
        Vec3 endPos, Vec3 endDir,
        float radius,
        AgentMovementAbility movementAbility,
        NavigationBlockers navigationBlockers)
    {
        AILog.AIWarning("CFree2DNavRegion::BeautifyPath should never be called");
    }

    // Free2DNavRegion.cpp — UglifyPath
    public override void UglifyPath(VectorConstNodeIndices inPath, TPathPoints outPath,
        Vec3 startPos, Vec3 startDir,
        Vec3 endPos, Vec3 endDir)
    {
        AILog.AIWarning("CFree2DNavRegion::UglifyPath should never be called");
    }

    // Free2DNavRegion.cpp — GetEnclosing
    public override uint GetEnclosing(Vec3 pos, float passRadius = 0.0f, uint startIndex = 0,
        float range = -1.0f, Vec3? closestValid = null, bool returnSuspect = false, string requesterName = "", bool omitWalkabilityTest = false)
    {
        if (m_pDummyNode == null)
        {
            m_dummyNodeIndex = gAIEnv.pGraph.CreateNewNode((uint)IAISystem_ENavigationType.NAV_FREE_2D, new Vec3(0, 0, 0));
            m_pDummyNode = gAIEnv.pGraph.GetNode(m_dummyNodeIndex);
        }
        return m_dummyNodeIndex;
    }

    // Free2DNavRegion.cpp — Clear
    public override void Clear()
    {
        if (m_pDummyNode != null)
        {
            gAIEnv.pGraph.Disconnect(m_dummyNodeIndex);
            m_pDummyNode = null;
            m_dummyNodeIndex = 0;
        }
    }

    public override void Serialize(TSerialize ser) { }

    // Free2DNavRegion.cpp — CheckPassability
    public override bool CheckPassability(Vec3 from, Vec3 to, float radius, NavigationBlockers navigationBlockers, uint navCapMask)
    {
        SpecialArea sa = gAIEnv.pNavigation?.GetSpecialArea(from, SpecialArea.EType.TYPE_FREE_2D);
        if (sa == null)
            return false;
        return !Overlap.Lineseg_Polygon2D(new Lineseg(from, to), sa.GetPolygon(), sa.GetAABB());
    }

    // Free2DNavRegion.cpp — ShrinkShape (static helper)
    private static void ShrinkShape(ListPositions shapeIn, ListPositions shapeOut, float radius)
    {
        shapeOut.Clear();
        float radiusScale = 0.5f;

        for (int i = 0; i < shapeIn.Count; ++i)
        {
            int iNext = (i + 1) % shapeIn.Count;
            int iNextNext = (iNext + 1) % shapeIn.Count;

            Vec3 pos = shapeIn[i]; pos.z = 0;
            Vec3 posNext = shapeIn[iNext]; posNext.z = 0;
            Vec3 posNextNext = shapeIn[iNextNext]; posNextNext.z = 0;

            Vec3 segDirPrev = (posNext - pos).GetNormalizedSafe();
            Vec3 segDirNext = (posNextNext - posNext).GetNormalizedSafe();

            Vec3 normalInPrev = new Vec3(-segDirPrev.y, segDirPrev.x, 0.0f);
            Vec3 normalInNext = new Vec3(-segDirNext.y, segDirNext.x, 0.0f);
            Vec3 normalAv = (normalInPrev + normalInNext).GetNormalizedSafe();

            Vec3 cross = segDirPrev.Cross(segDirNext);
            bool convex = cross.z < 0.0f;

            if (convex)
            {
                Vec3 newPtPrev = posNext + normalInPrev * (radius * radiusScale);
                newPtPrev.z = shapeIn[i].z;
                Vec3 newPtMid = posNext + normalAv * (radius * radiusScale);
                newPtMid.z = shapeIn[iNext].z;
                Vec3 newPtNext = posNext + normalInNext * (radius * radiusScale);
                newPtNext.z = shapeIn[iNextNext].z;
                shapeOut.Add(newPtPrev);
                shapeOut.Add(newPtMid);
                shapeOut.Add(newPtNext);
            }
            else
            {
                float dot = segDirPrev.Dot(segDirNext);
                float extraRadiusScale = (float)Math.Sqrt(2.0f / (1.0f + dot));
                Vec3 newPtMid = posNext + normalAv * (radius * radiusScale * extraRadiusScale);
                newPtMid.z = shapeIn[iNext].z;
                shapeOut.Add(newPtMid);
            }
        }
    }

    // Free2DNavRegion.cpp — GetSingleNodePath
    public override bool GetSingleNodePath(GraphNode pNode, Vec3 startPos, Vec3 endPos, float radius,
        NavigationBlockers navigationBlockers, List<PathPointDescriptor> points, uint navCapMask)
    {
        SpecialArea sa = gAIEnv.pNavigation?.GetSpecialAreaNearestPos(startPos, SpecialArea.EType.TYPE_FREE_2D);
        if (sa == null)
            return false;

        ListPositions origShape = sa.GetPolygon();
        ListPositions shape = new ListPositions();
        ShrinkShape(origShape, shape, radius);

        // simplest straight-line case
        if (!Overlap.Lineseg_Polygon2D(new Lineseg(startPos, endPos), shape))
        {
            points.Clear();
            points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, startPos));
            points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, endPos));
            return true;
        }

        if (!Overlap.Point_Polygon2D(endPos, origShape, sa.GetAABB()))
            return false;

        // Build safe path through vertices
        int countToStart = -1, countToEnd = -1;
        float bestDistStartSq = float.MaxValue, bestDistEndSq = float.MaxValue;
        int itStartIdx = 0, itEndIdx = 0;
        float frac = 0.99f;

        for (int count = 0; count < shape.Count; ++count)
        {
            Vec3 itPos = shape[count];
            bool reachableStart = !Overlap.Lineseg_Polygon2D(new Lineseg(startPos, startPos + frac * (itPos - startPos)), origShape, sa.GetAABB());
            bool reachableEnd = !Overlap.Lineseg_Polygon2D(new Lineseg(endPos, endPos + frac * (itPos - endPos)), origShape, sa.GetAABB());
            if (reachableStart)
            {
                float d = sqr(startPos.x - itPos.x) + sqr(startPos.y - itPos.y);
                if (d < bestDistStartSq) { bestDistStartSq = d; itStartIdx = count; countToStart = count; }
            }
            if (reachableEnd)
            {
                float d = sqr(endPos.x - itPos.x) + sqr(endPos.y - itPos.y);
                if (d < bestDistEndSq) { bestDistEndSq = d; itEndIdx = count; countToEnd = count; }
            }
        }

        if (countToStart < 0 || countToEnd < 0)
        {
            AILog.AIWarning($"CFree2DNavRegion::GetSingleNodePath failed from ({startPos.x:F2}, {startPos.y:F2}, {startPos.z:F2}) to ({endPos.x:F2}, {endPos.y:F2}, {endPos.z:F2})");
            return false;
        }

        // add the points
        points.Clear();
        points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, startPos));
        bool walkingFwd = true;
        if (itStartIdx == itEndIdx)
        {
            points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, shape[itStartIdx]));
        }
        else
        {
            int shapeCount = shape.Count;
            int countFwd = (shapeCount + countToEnd - countToStart) % shapeCount;
            int countBwd = shapeCount - countFwd;
            walkingFwd = countFwd < countBwd;

            int it = itStartIdx;
            do
            {
                points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, shape[it]));
                if (walkingFwd)
                {
                    ++it;
                    if (it >= shapeCount) it = 0;
                }
                else
                {
                    --it;
                    if (it < 0) it = shapeCount - 1;
                }
            } while (it != itEndIdx);
            points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, shape[itEndIdx]));
        }
        points.Add(new PathPointDescriptor(IAISystem_ENavigationType.NAV_FREE_2D, endPos));

        // cutting optimization
        bool cutOne = true;
        while (cutOne && points.Count > 2)
        {
            cutOne = false;
            for (int ci = 0; ci < points.Count - 2; ++ci)
            {
                Vec3 pos = points[ci].vPos;
                Vec3 posNextNext = points[ci + 2].vPos;

                float fracCut = 0.001f;
                Vec3 p1 = pos + fracCut * (posNextNext - pos);
                Vec3 p2 = posNextNext + fracCut * (pos - posNextNext);

                Lineseg seg = new Lineseg(p1, p2);
                bool doingEnd = ci == 0 || ci + 3 >= points.Count;
                ListPositions thisShape = doingEnd ? origShape : shape;
                if (Overlap.Point_Polygon2D(p1, thisShape) && !Overlap.Lineseg_Polygon2D(seg, thisShape))
                {
                    points.RemoveAt(ci + 1);
                    cutOne = true;
                    ci = -1; // restart from beginning
                }
            }
        }

        return true;
    }

    // Free2DNavRegion.cpp — MemStats
    public override nuint MemStats()
    {
        return (nuint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(CFree2DNavRegion));
    }

    private GraphNode m_pDummyNode;
    private uint m_dummyNodeIndex;
}

// CNavRegion base class
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
    public virtual void NodeCreated(uint nodeIndex) { }
    public virtual void NodeMoved(uint nodeIndex) { }
    public virtual void NodeAboutToBeDeleted(GraphNode pNode) { }
}
