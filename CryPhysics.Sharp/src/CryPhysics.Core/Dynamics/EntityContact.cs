// Port of entity_contact from rigidbody.h
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Dynamics;

/// <summary>
/// Contact between two entities for the contact solver.
/// Faithful port of entity_contact struct from rigidbody.h.
/// </summary>
public class EntityContact
{
    public EntityContact? Next, Prev;
    public EntityContact? NextAux;

    public PhysVector3 Pt0, Pt1;          // Contact points on body 0 and body 1
    public PhysVector3 N;                 // Contact normal
    public RigidBody? PBody0, PBody1;     // Rigid bodies involved
    public object? PEnt0, PEnt1;          // Entity pointers (pent[0..1]) - typed as object to avoid Entities<->Dynamics dep
    public int IPart0, IPart1;            // Part indices
    public int IPrim0, IPrim1;            // Primitive indices (pcontacts[idx].iPrim[0..1])
    public int IFeature0, IFeature1;      // Feature indices (pcontacts[idx].iFeature[0..1])
    public PhysVector3 Nloc;              // Local normal (for constraint projection)
    public float Friction;
    public int Flags;
    public float Vrel;                    // Normal relative velocity
    public PhysVector3 Vreq;              // Required velocity correction
    public float Pspare;                  // Spare impulse magnitude (for friction budget)
    public float Penetration;

    // Bitfield emulation (C++ uses bitfields)
    public int INormal;                   // 2 bits
    public int Id0;                       // 15 bits
    public int Id1;                       // 15 bits
    public int BProcessed;                // 16 bits (used as index into solver arrays)
    public int IConstraint;               // 14 bits
    public bool BConstraint;
    public bool BChunkStart;
    public int ICount;
    public int BounceCount;               // replaces pBounceCount pointer
}
