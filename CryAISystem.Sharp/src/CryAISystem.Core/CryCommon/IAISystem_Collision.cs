// Literal partial port of dev/Code/CryEngine/CryCommon/IAISystem.h (EAICollisionEntities enum)
// and dev/Code/CryEngine/CryCommon/physinterface.h (entity_query_flags enum used as bit-mix
// inputs by EAICollisionEntities). Full literal port of IAISystem.h (1003L) and physinterface.h
// is deferred — see deferred.md.
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// physinterface.h — entity_query_flags  (literal subset)
public static class EntityQueryFlags
{
    public const int ent_static = 1;
    public const int ent_sleeping_rigid = 2;
    public const int ent_rigid = 4;
    public const int ent_living = 8;
    public const int ent_independent = 16;
    public const int ent_areas = 32;
    public const int ent_triggers = 64;
    public const int ent_deleted = 128;
    public const int ent_terrain = 0x100;
    public const int ent_water = 0x200;
    public const int ent_ignore_noncolliding = 0x10000;
    public const int ent_sort_by_mass = 0x20000;
    public const int ent_allocate_list = 0x40000;
    public const int ent_no_ondemand_activation = 0x80000;
    public const int ent_delayed_deformations = 0x80000;
    public const int ent_addref_results = 0x100000;
}

// IAISystem.h — EAICollisionEntities  (literal port)
//
// if this is changed be sure to change the table aiCollisionEntitiesTable in AICollision.cpp
public enum EAICollisionEntities
{
    AICE_STATIC = EntityQueryFlags.ent_static | EntityQueryFlags.ent_terrain | EntityQueryFlags.ent_ignore_noncolliding,
    AICE_ALL = EntityQueryFlags.ent_static | EntityQueryFlags.ent_sleeping_rigid | EntityQueryFlags.ent_rigid | EntityQueryFlags.ent_terrain | EntityQueryFlags.ent_ignore_noncolliding,
    AICE_ALL_SOFT = EntityQueryFlags.ent_static | EntityQueryFlags.ent_sleeping_rigid | EntityQueryFlags.ent_rigid | EntityQueryFlags.ent_terrain,
    AICE_DYNAMIC = EntityQueryFlags.ent_sleeping_rigid | EntityQueryFlags.ent_rigid | EntityQueryFlags.ent_ignore_noncolliding,
    AICE_STATIC_EXCEPT_TERRAIN = EntityQueryFlags.ent_static | EntityQueryFlags.ent_ignore_noncolliding,
    AICE_ALL_EXCEPT_TERRAIN = EntityQueryFlags.ent_static | EntityQueryFlags.ent_sleeping_rigid | EntityQueryFlags.ent_rigid | EntityQueryFlags.ent_ignore_noncolliding,
    AICE_ALL_INLUDING_LIVING = EntityQueryFlags.ent_static | EntityQueryFlags.ent_sleeping_rigid | EntityQueryFlags.ent_rigid | EntityQueryFlags.ent_terrain | EntityQueryFlags.ent_ignore_noncolliding | EntityQueryFlags.ent_living,
    AICE_ALL_EXCEPT_TERRAIN_AND_STATIC = EntityQueryFlags.ent_sleeping_rigid | EntityQueryFlags.ent_rigid | EntityQueryFlags.ent_ignore_noncolliding,
}
