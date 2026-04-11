// Literal port of enum types from dev/Code/CryEngine/CryCommon/IAgent.h
// Each enum has its source line number from IAgent.h.
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// IAgent.h:357-365
public enum EAITargetThreat
{
    AITHREAT_NONE,
    AITHREAT_SUSPECT,
    AITHREAT_INTERESTING,
    AITHREAT_THREATENING,
    AITHREAT_AGGRESSIVE,
    AITHREAT_LAST   // For serialization.
}

// IAgent.h:367-384
public enum EAITargetType
{
    // Atomic types.
    AITARGET_NONE,                // No target.
    AITARGET_SOUND,               // Primary sensory from sound event.
    AITARGET_MEMORY,              // Primary sensory from vis event, not visible.
    AITARGET_VISUAL,              // Primary sensory from vis event, visible.

    // Backwards compatibility for scriptbind.
    AITARGET_ENEMY,
    AITARGET_FRIENDLY,
    AITARGET_BEACON,
    AITARGET_GRENADE,
    AITARGET_RPG,

    // For serialization.
    AITARGET_LAST
}

// IAgent.h:386-394
public enum EAITargetContextType
{
    // Atomic types.
    AITARGET_CONTEXT_UNKNOWN,     // No specific subtype.
    AITARGET_CONTEXT_GUNFIRE,     // Gunfire subtype.

    // For serialization.
    AITARGET_CONTEXT_LAST
}

// IAgent.h:396-407
public enum EAITargetZone
{
    AIZONE_IGNORE = 0,           // Ignoring target zones

    AIZONE_KILL,
    AIZONE_COMBAT_NEAR,
    AIZONE_COMBAT_FAR,
    AIZONE_WARN,
    AIZONE_OUT,

    AIZONE_LAST
}

// IAgent.h:409-415
public enum EAIWeaponAccessories
{
    AIWEPA_NONE = 0,
    AIWEPA_LASER = 0x0001,
    AIWEPA_COMBAT_LIGHT = 0x0002,
    AIWEPA_PATROL_LIGHT = 0x0004,
}

// IAgent.h:834-841
public enum EMemoryFireType
{
    eMFT_Disabled = 0,           // Never allowed to fire at memory
    eMFT_UseCoverFireTime,       // Can fire at memory using the weapon's cover fire time
    eMFT_Always,                 // Always allowed to fire at memory

    eMFT_COUNT,
}

// IAgent.h:845-852
public enum EAIFireState
{
    eAIFS_Off = 0,
    eAIFS_On,
    eAIFS_Blocking,  // Fire command handler is doing extra work and is not ready to fire yet

    eAIFS_COUNT,
}

// IAgent.h:1068-1073
public enum EBodyOrientationMode
{
    FullyTowardsMovementDirection,
    FullyTowardsAimOrLook,
    HalfwayTowardsAimOrLook
}

// IAgent.h:1358-1368
public enum EActorTargetPhase
{
    eATP_None,
    eATP_Waiting,
    eATP_Starting,
    eATP_Started,
    eATP_Playing,
    eATP_StartedAndFinished,
    eATP_Finished,
    eATP_Error,
}

// IAgent.h:1370-1376
public enum EAITargetStuntReaction
{
    AITSR_NONE,
    AITSR_SEE_STUNT_ACTION,
    AITSR_SEE_CLOAKED,
    AITSR_LAST
}

// IAgent.h:1380-1392
public enum ERequestedGrenadeType
{
    eRGT_INVALID = -1,

    eRGT_ANY,
    eRGT_SMOKE_GRENADE,
    eRGT_FLASHBANG_GRENADE,
    eRGT_FRAG_GRENADE,
    eRGT_EMP_GRENADE,
    eRGT_GRUNT_GRENADE,

    eRGT_COUNT,
}
