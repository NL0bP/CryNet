// Literal port shells of dev/Code/CryEngine/CryCommon/physinterface.h types used by AIObject.cpp.
// Full literal port deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// pe_status_living — port of physinterface.h
// Inherits PhysicsStatusBase from CryPhysics.Sharp so it can be passed to IPhysicalEntity.GetStatus.
public class pe_status_living : CryPhysics.Params.PhysicsStatusBase
{
    public override int TypeId => 1;

    public Vec3 vel;
    public Vec3 velUnconstrained;
    public Vec3 velRequested;
    public Vec3 velGround;
    public float groundHeight;
    public Vec3 groundSlope;
    public int groundSurfaceIdx;
    public int bFlying;
    public float timeFlying;
    public Vec3 camOffset;
    public bool isStuck;
    public bool isSquashed;
}

// pe_status_dynamics — port of physinterface.h
public class pe_status_dynamics : CryPhysics.Params.PhysicsStatusBase
{
    public override int TypeId => 2;

    public Vec3 v;
    public Vec3 w;
    public Vec3 a;
    public Vec3 wa;
    public Vec3 centerOfMass;
    public float submergedFraction;
    public float mass;
    public float energy;
    public int nContacts;
    public float time_interval;
}
