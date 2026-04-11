// Literal port shell of dev/Code/CryEngine/CryAISystem/Navigation/NavigationSystem/NavigationSystem.h
// Full literal port (706L) + .cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.Navigation.NavigationSystem;

public class NavigationSystem : INavigationSystem
{
    public NavigationSystem() { /* impl in .cpp */ }

    public NavigationMeshID GetEnclosingMeshID(NavigationAgentTypeID agentTypeID, Vec3 location) { return new NavigationMeshID(); /* impl pending */ }
    public NavigationMesh GetMesh(NavigationMeshID meshID) { return new NavigationMesh(); /* impl pending */ }
}

// NavigationMesh literal port (subset) of MNM::NavigationMesh
public class NavigationMesh
{
    public NavigationMeshGrid grid = new NavigationMeshGrid();
}

public class NavigationMeshGrid
{
    public uint GetTriangleAt(Vec3 location, float verticalRange1, float verticalRange2) { return 0; /* impl pending */ }
    public uint GetClosestTriangle(Vec3 location, float verticalRange, float horizontalRange, out float distSq, out Vec3 closestLocation)
    { distSq = 0; closestLocation = location; return 0; /* impl pending */ }
}

// Forward decls / shells
public interface INavigationSystem { }
