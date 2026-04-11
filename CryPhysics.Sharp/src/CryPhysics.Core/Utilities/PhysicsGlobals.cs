// Port of CryPhysics CryPhysics.cpp - entry point and global initialization
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.World;

namespace CryPhysics.Utilities;

/// <summary>
/// Global physics initialization and factory.
/// Port of CryPhysics.cpp entry point.
/// </summary>
public static class PhysicsFactory
{
    /// <summary>
    /// Create a new physics world instance.
    /// Port of CreatePhysicalWorld from CryPhysics.cpp.
    /// </summary>
    public static PhysicalWorld CreatePhysicalWorld()
    {
        var world = new PhysicalWorld();
        // Default surface params
        world.SetSurfaceParameters(0, 0.3f, 0.5f);
        return world;
    }
}
