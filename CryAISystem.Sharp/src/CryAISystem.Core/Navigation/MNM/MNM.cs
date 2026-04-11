// Literal port shells for dev/Code/CryEngine/CryAISystem/Navigation/MNM/* headers (subset).
// Full literal ports of MNM.h (792L), MeshGrid.h (573L), TileGenerator.h (576L),
// Voxelizer.h (98L), Tile.h (200L), CompactSpanGrid.h (210L), DynamicSpanGrid.h (126L),
// IslandConnections.h (125L), OffGridLinks.h (152L), BoundingVolume.h (58L),
// FixedAABB.h (131L), FixedVec2.h (199L), FixedVec3.h (268L), HashComputer.h (104L),
// OpenList.h (73L), Profiler.h (148L), MNM_Type_info.h (38L) deferred — see deferred.md
//
// This file establishes the MNM:: namespace types as forward declaration shells so
// downstream files can reference them. Bodies will be filled in subsequent passes.
//
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.Navigation.MNM;

// Identifiers
public struct TriangleID { public uint id; }
public struct TileID { public uint id; }
public struct EdgeID { public uint id; }
public struct VertexID { public uint id; }
public struct OffMeshLinkID { public uint id; }

// Fixed-point primitives
public struct FixedVec2 { public int x, y; }
public struct FixedVec3 { public int x, y, z; }
public struct FixedAABB { public FixedVec3 min, max; }

// Tile/grid containers
public class CompactSpanGrid { }
public class DynamicSpanGrid { }
public class Tile { }
public class TileGenerator { }
public class Voxelizer { }
public class MeshGrid { }
public class BoundingVolume { }
public class IslandConnections { }
public class OffGridLinks { }
public class HashComputer { }
public class OpenList { }
public class Profiler { }

// Danger areas
public class DangerousAreasList : List<object> { }
