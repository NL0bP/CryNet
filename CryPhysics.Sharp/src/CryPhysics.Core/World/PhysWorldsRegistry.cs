// Port of physicalworld.cpp globals: g_pPhysWorlds[], g_nPhysWorlds.
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryPhysics.World;

/// <summary>
/// Global registry of all live PhysicalWorld instances. Mirrors the C++ globals
/// `g_pPhysWorlds[64]` + `g_nPhysWorlds` from physicalworld.cpp. Used by
/// `CPhysicalPlaceholder::GetWorld` to locate the owning world without a back pointer.
/// </summary>
public static class PhysWorldsRegistry
{
    private const int MaxWorlds = 64;
    private static readonly PhysicalWorld?[] _worlds = new PhysicalWorld?[MaxWorlds];
    private static int _count;
    private static readonly object _lock = new();

    public static int Count => _count;
    public static PhysicalWorld? Get(int i) => (i >= 0 && i < _count) ? _worlds[i] : null;

    /// Register a world on creation. Matches the body of CPhysicalWorld constructor.
    internal static void Register(PhysicalWorld w)
    {
        lock (_lock)
        {
            if (_count >= MaxWorlds) return;
            _worlds[_count++] = w;
        }
    }

    /// Unregister a world on destruction. Matches CPhysicalWorld destructor.
    internal static void Unregister(PhysicalWorld w)
    {
        lock (_lock)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_worlds[i], w))
                {
                    for (int j = i; j < _count - 1; j++) _worlds[j] = _worlds[j + 1];
                    _worlds[--_count] = null;
                    return;
                }
            }
        }
    }
}
