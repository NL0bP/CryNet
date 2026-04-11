// Port of physinterface.h - EventPhys* structs
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Entities;
using CryPhysics.Math;

namespace CryPhysics.Events;

/// <summary>Base class for all physics events.</summary>
public abstract class EventPhys
{
    public abstract int TypeId { get; }
    public int IdSender;
}

/// <summary>Events with one entity. Port of EventPhysMono.</summary>
public abstract class EventPhysMono : EventPhys
{
    public IPhysicalEntity? Entity;
    public object? ForeignData;
    public int ForeignDataType;
}

/// <summary>Events with two entities. Port of EventPhysStereo.</summary>
public abstract class EventPhysStereo : EventPhys
{
    public IPhysicalEntity?[] Entities = new IPhysicalEntity?[2];
    public object?[] ForeignDatas = new object?[2];
    public int[] ForeignDataTypes = new int[2];
}

/// <summary>Collision event. Port of EventPhysCollision.</summary>
public class EventPhysCollision : EventPhysStereo
{
    public override int TypeId => 2; // C++ EventPhysCollision id = 2
    public int IdCollider;
    public PhysVector3 Pt;
    public PhysVector3 Normal;
    public PhysVector3[] VLoc = new PhysVector3[2];
    public float[] Mass = new float[2];
    public int[] PartId = new int[2];
    public short[] IdMat = new short[2];
    public short[] IPrim = new short[2];
    public float Penetration;
    public float NormImpulse;
    public float Radius;
    public object? EntContact;
    public sbyte DeferredState;
    public sbyte DeferredResult;
    public float DecalPlacementTestMaxSize;
}

/// <summary>Post-step event. Port of EventPhysPostStep.</summary>
public class EventPhysPostStep : EventPhysMono
{
    public override int TypeId => 4; // C++ EventPhysPostStep id = 4
    public float Dt;
    public PhysVector3 Pos;
    public PhysQuaternion Q;
    public int IdStep;
}

/// <summary>State change event (sleep/wake). Port of EventPhysStateChange.</summary>
public class EventPhysStateChange : EventPhysMono
{
    public override int TypeId => 8; // C++ EventPhysStateChange id = 8
    public int[] SimClass = new int[2]; // old, new
    public float TimeIdle;
    public PhysVector3[] BBoxOld = new PhysVector3[2];
    public PhysVector3[] BBoxNew = new PhysVector3[2];
}

/// <summary>Environment change event. Port of EventPhysEnvChange.</summary>
public class EventPhysEnvChange : EventPhysMono
{
    public override int TypeId => 3;
    public int Code;
    public IPhysicalEntity? EntSrc;
    public IPhysicalEntity? EntNew;
}

/// <summary>Mesh update event. Port of EventPhysUpdateMesh.</summary>
public class EventPhysUpdateMesh : EventPhysMono
{
    public override int TypeId => 5; // C++ EventPhysUpdateMesh id = 5
    public int PartId;
    public bool Invalid;
    public int Reason;
    public int Idx;
}

/// <summary>Entity part creation event (breakage). Port of EventPhysCreateEntityPart.</summary>
public class EventPhysCreateEntityPart : EventPhysMono
{
    public override int TypeId => 6; // C++ EventPhysCreateEntityPart id = 6
    public IPhysicalEntity? EntNew;
    public int PartIdSrc;
    public int PartIdNew;
    public int NTotParts;
    public bool Invalid;
    public int Reason;
    public PhysVector3 BreakImpulse;
    public PhysVector3 BreakAngImpulse;
    public PhysVector3 V;
    public PhysVector3 W;
    public float BreakSize;
    public float CutRadius;
    public PhysVector3[] CutPtLoc = new PhysVector3[2];
    public PhysVector3[] CutDirLoc = new PhysVector3[2];
    public int Idx;
}

/// <summary>Entity part removal event. Port of EventPhysRemoveEntityParts.</summary>
public class EventPhysRemoveEntityParts : EventPhysMono
{
    public override int TypeId => 7; // C++ EventPhysRemoveEntityParts id = 7
    public uint[] PartIds = new uint[4];
    public int IdOffs;
    public float MassOrg;
}

/// <summary>Joint broken event. Port of EventPhysJointBroken.</summary>
public class EventPhysJointBroken : EventPhysStereo
{
    public override int TypeId => 1; // C++ EventPhysJointBroken id = 1
    public int IdJoint;
    public bool IsJoint;
    public int PartIdEpicenter;
    public PhysVector3 Pt;
    public PhysVector3 Normal;
    public int[] PartId = new int[2];
    public int[] PartMat = new int[2];
    public IPhysicalEntity?[] NewEntities = new IPhysicalEntity?[2];
}

/// <summary>BBox overlap event. Port of EventPhysBBoxOverlap.</summary>
public class EventPhysBBoxOverlap : EventPhysStereo
{
    public override int TypeId => 0; // C++ EventPhysBBoxOverlap id = 0
}

/// <summary>Ray world intersection result event. Port of EventPhysRWIResult.</summary>
public class EventPhysRWIResult : EventPhysMono
{
    public override int TypeId => 9;
    public RayHit[] Hits = Array.Empty<RayHit>();
    public int NHits;
    public int NMaxHits;
}

/// <summary>Area event. Port of EventPhysArea.</summary>
public class EventPhysArea : EventPhysMono
{
    public override int TypeId => 11;
    public PhysVector3 Pt;
    public PhysVector3 PtRef;
    public PhysVector3 DirRef;
    public PhysVector3 Gravity;
    public IPhysicalEntity? AreaEntity;
}

/// <summary>Area change event. Port of EventPhysAreaChange.</summary>
public class EventPhysAreaChange : EventPhysMono
{
    public override int TypeId => 12;
    public PhysVector3[] BoxAffected = new PhysVector3[2];
    public PhysQuaternion Q;
    public PhysVector3 Pos;
    public float Depth;
    public IPhysicalEntity? Container;
    public PhysQuaternion QContainer;
    public PhysVector3 PosContainer;
}
