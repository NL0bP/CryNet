// Literal port shell of dev/Code/CryEngine/CryAISystem/FlightNavRegion2.h
// Full literal port + .cpp impl (1434L combined) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public class CFlightNavRegion2 : CNavRegion
{
    public CFlightNavRegion2(IPhysicalWorld physWorld, CGraph pGraph) { /* impl in .cpp */ }
    // virtual ~CFlightNavRegion2();

    // (port: full member set deferred — large file with internal grid/voxel state)
}

// IPhysicalWorld lives in CryCommon/PhysInterface.cs (literal port subset).
