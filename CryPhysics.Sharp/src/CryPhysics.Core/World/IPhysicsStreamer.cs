// Literal port of dev/Code/CryEngine/CryCommon/physinterface.h:262-278 IPhysicsStreamer.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Entities;
using CryPhysics.Math;

namespace CryPhysics.World;

/// <summary>
/// Callback interface for on-demand physicalization. Physics holds a pointer to the
/// streamer implementation and calls back when placeholders need their full entity
/// or when on-demand boxes activate. Port of `struct IPhysicsStreamer`.
/// </summary>
public interface IPhysicsStreamer
{
    /// Called whenever a placeholder (created through CreatePhysicalPlaceholder) requests a full entity.
    int CreatePhysicalEntity(PhysicsForeignData foreignData, int iForeignData, int iForeignFlags);

    /// Called whenever a placeholder-owned entity expires.
    int DestroyPhysicalEntity(IPhysicalEntity pent);

    /// Called when on-demand entities in a box need to be physicalized
    /// (the grid is activated once RegisterBBoxInPODGrid is called).
    int CreatePhysicalEntitiesInBox(in PhysVector3 boxMin, in PhysVector3 boxMax);

    /// Called when on-demand physicalized box expires. The streamer is expected to
    /// delete those that have a 0 refcounter, and keep the rest.
    int DestroyPhysicalEntitiesInBox(in PhysVector3 boxMin, in PhysVector3 boxMax);
}
