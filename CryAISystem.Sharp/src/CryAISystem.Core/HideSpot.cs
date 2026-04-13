// Literal port of dev/Code/CryEngine/CryAISystem/HideSpot.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// Description : Hidespot-related structures

using System.Collections.Generic;

namespace CryAISystem;

// Description:
//	 Structure that contains critical hidespot information.
public struct SHideSpotInfo
{
    public enum EHideSpotType
    {
        eHST_TRIANGULAR,
        eHST_WAYPOINT,
        eHST_ANCHOR,
        eHST_SMARTOBJECT,
        eHST_VOLUME,
        eHST_DYNAMIC,
        eHST_INVALID,
    }

    public EHideSpotType type;
    public Vec3 pos;
    public Vec3 dir;

    // Default ctor: all-zero with eHST_INVALID type. C# default(SHideSpotInfo) gives the same.
    public SHideSpotInfo(EHideSpotType type, Vec3 pos, Vec3 dir) { this.type = type; this.pos = pos; this.dir = dir; }
}


public class SHideSpot
{
    public SHideSpot()
    {
        pNavNode = null;
        pNavNodes = null;
        pAnchorObject = null;
        pObstacle = null;
        entityId = 0;
    }

    public SHideSpot(SHideSpotInfo.EHideSpotType type, Vec3 pos, Vec3 dir)
    {
        info = new SHideSpotInfo(type, pos, dir);
        pNavNode = null;
        pNavNodes = null;
        pAnchorObject = null;
        pObstacle = null;
        entityId = 0;
    }

    public bool IsSecondary()
    {
        switch (info.type)
        {
            case SHideSpotInfo.EHideSpotType.eHST_TRIANGULAR:
                return pObstacle != null && !pObstacle.IsCollidable();
            case SHideSpotInfo.EHideSpotType.eHST_WAYPOINT:
                return pNavNode != null && (pNavNode.navType == IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN) &&
                    (pNavNode.GetWaypointNavData().type == EWaypointNodeType.WNT_HIDESECONDARY);
            case SHideSpotInfo.EHideSpotType.eHST_ANCHOR:
                return pAnchorObject != null && (pAnchorObject.GetType() == (ushort)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT_SECONDARY);
        }

        return false;
    }

    //////////////////////////////////////////////////////////////////////////

    public SHideSpotInfo info;

    // optional parameters - can be used with multiple hide spot types
    public GraphNode pNavNode;
    public List<GraphNode> pNavNodes;
    public uint entityId;       // The entity id of the source object for dynamic hidepoints.

    // parameters used only with one specific hide spot type
    public ObstacleData pObstacle;      // triangular
    public CQueryEvent SOQueryEvent;    // smart objects
    public CAIObject pAnchorObject;     // anchors
}

// typedef std::multimap<float, SHideSpot> MultimapRangeHideSpots;
// (already defined in Puppet.cs as alias)

// GraphNode, WaypointNavData, EWaypointNodeType are defined in GraphStructures.cs
public class CQueryEvent { }
