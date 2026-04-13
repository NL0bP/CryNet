// Literal port of dev/Code/CryEngine/CryAISystem/AIMemStats.cpp (361 lines).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// NOTE: Many types referenced by AIMemStats (CGraph, CGoalPipe, CPuppet, CNavigation, etc.)
// are already declared in their respective source files (Graph.cs, PipeUser.cs, Puppet.cs,
// Navigation.cs). Those files already contain shell implementations of MemStats() and
// GetMemoryStatistics(). This file only adds the CAISystem.GetMemoryStatistics method
// via partial class, plus the ICrySizer interface and helper structs not declared elsewhere.

using System.Collections.Generic;
using CryAISystem.CryCommon;

namespace CryAISystem;

// ICrySizer — CrySizer.h shell for memory statistics gathering
public interface ICrySizer
{
    void AddObject(object obj, ulong size);
    void AddContainer<T>(ICollection<T> container);
}

// SIZER_COMPONENT_NAME / SIZER_SUBCOMPONENT_NAME are C++ macros that set
// a label on the sizer for the duration of the scope. In C# they are no-ops.
public readonly struct SizerComponentNameScope : System.IDisposable
{
    public SizerComponentNameScope(ICrySizer pSizer, string name) { }
    public void Dispose() { }
}

public partial class CAISystem
{
    // Field declared here because CAISystem.cs defers it to this file (AIMemStats.cs partial)
    public Dictionary<string, FormationDescriptor> m_mapFormationDescriptors =
        new Dictionary<string, FormationDescriptor>();

    //===================================================================
    // CAISystem::GetMemoryStatistics — AIMemStats.cpp lines 28-121
    //===================================================================
    public void GetMemoryStatistics(ICrySizer pSizer)
    {
        ulong size = 0;

        // size = sizeof(*this);
        // size += m_disabledAIActorsSet.size()*sizeof(CAIActor*);
        // size += m_enabledAIActorsSet.size()*sizeof(CAIActor*);
        pSizer.AddObject(this, size);

        {
            // SIZER_SUBCOMPONENT_NAME(pSizer,"AIObjects");
        }

        {
            // SIZER_SUBCOMPONENT_NAME(pSizer,"NavGraph");
            // if(m_pGraph) { pSizer->AddObject(m_pGraph, sizeof(*m_pGraph)); m_pGraph->GetMemoryStatistics(pSizer); }
            // if ( m_pNavigation ) { m_pNavigation->GetMemoryStatistics(pSizer); }
            // Graph and Navigation already have GetMemoryStatistics shells in their own files.
        }

        size = 0;

        {
            // SIZER_SUBCOMPONENT_NAME(pSizer,"Goals");
            // GoalMap iteration deferred — CPipeManager.m_mapGoals iteration pending Phase 7.
        }

        {
            // SIZER_SUBCOMPONENT_NAME(pSizer,"PerceptionManager");
            if (gAIEnv.pPerceptionManager != null)
                pSizer.AddObject(gAIEnv.pPerceptionManager, 0);
        }

        {
            // SIZER_SUBCOMPONENT_NAME(pSizer,"ObjectContainer");
            if (gAIEnv.pObjectContainer != null)
                pSizer.AddObject(gAIEnv.pObjectContainer, 0);
        }

        size = 0;
        // Formation descriptors iteration — m_mapFormationDescriptors is private in CAISystem.cs
        // so direct access is fine here (same partial class).
        foreach (var kvp in m_mapFormationDescriptors)
        {
            size += (ulong)(kvp.Key?.Length ?? 0);
            // size += sizeof(CFormationDescriptor)
        }
        pSizer.AddObject(m_mapFormationDescriptors, size);

        // m_mapGroups — already declared in Environment.cs
        // size = m_mapGroups.size()*(sizeof(unsigned short)+sizeof(CAIObject*));
        // pSizer.AddObject(m_mapGroups, size);

        {
            // SIZER_SUBCOMPONENT_NAME(pSizer,"MNM Navigation System");
            if (gAIEnv.pNavigationSystem != null)
            {
                pSizer.AddObject(gAIEnv.pNavigationSystem, 0);
                // gAIEnv.pNavigationSystem->GetMemoryStatistics(pSizer);
            }
        }
    }
}

// GraphNode.MemStats — AIMemStats.cpp lines 123-164
// GraphNode is already defined in GraphStructures.cs. Provide a static helper.
public static class GraphNodeMemStatsHelper
{
    public static ulong MemStats(GraphNode node)
    {
        if (node == null) return 0;

        ulong size = 0;

        // switch (navType) — size varies by nav type
        // Simplified: return a base size estimate
        size += 128; // approximate base GraphNode size

        return size;
    }
}

// CPuppet.MemStats — AIMemStats.cpp lines 279-324
// CPuppet is defined in Puppet.cs (DO NOT modify). Provide a static helper.
public static class CPuppetMemStatsHelper
{
    public static ulong MemStats(CPuppet puppet)
    {
        if (puppet == null) return 0;

        ulong size = 0;

        // if(m_pCurrentGoalPipe) size += m_pCurrentGoalPipe->MemStats();
        // if(m_mapDevaluedPoints.size()<1000)
        //   size += (sizeof(CAIObject*)+sizeof(float))*m_mapDevaluedPoints.size();

        return size;
    }
}

// CAStarSolver.MemStats — AIMemStats.cpp lines 331-339
public static class CAStarSolverMemStatsHelper
{
    public static ulong MemStats(object solver)
    {
        if (solver == null) return 0;
        return 0; // full impl requires CAStarSolver literal port
    }
}

// CWorldOctree.MemStats — AIMemStats.cpp lines 344-361
public static class CWorldOctreeMemStatsHelper
{
    public static ulong MemStats(object octree)
    {
        if (octree == null) return 0;
        return 0; // full impl requires CWorldOctree literal port
    }
}
