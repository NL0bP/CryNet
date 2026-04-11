// Literal port of dev/Code/CryEngine/CryAISystem/AIHideObject.h
// .cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public enum ECoverUsage
{
    USECOVER_NONE,
    USECOVER_SMARTOBJECT_HIDE,
    USECOVER_SMARTOBJECT_UNHIDE,
    USECOVER_STRAFE_LEFT_STANDING,
    USECOVER_STRAFE_RIGHT_STANDING,
    USECOVER_STRAFE_TOP_STANDING,
    USECOVER_STRAFE_TOP_LEFT_STANDING,
    USECOVER_STRAFE_TOP_RIGHT_STANDING,
    USECOVER_STRAFE_LEFT_CROUCHED,
    USECOVER_STRAFE_RIGHT_CROUCHED,
    USECOVER_CENTER_CROUCHED,
    USECOVER_LAST,
}

// Note: CAIHideObject was forward-declared in PipeUser.cs.
// Replace with full literal port here.
public class CAIHideObjectReal
{
    // friend class CPipeUser;

    public CAIHideObjectReal() { /* impl in .cpp */ }
    // ~CAIHideObject();

    public void Set(SHideSpot hs, Vec3 hidePos, Vec3 hideDir) { /* impl in .cpp */ }

    public void Invalidate() { m_bIsValid = false; }
    public bool IsValid() { return m_bIsValid; /* impl in .cpp */ }
    public bool IsCompromised(CPipeUser pRequester, Vec3 targetPos) { return false; /* impl in .cpp */ }
    public bool IsNearCover(CPipeUser pRequester) { return false; /* impl in .cpp */ }
    public bool IsSmartObject() { return m_bIsSmartObject; }
    public CQueryEvent GetSmartObject() { return m_HideSmartObject; }
    public void SetSmartObject(CQueryEvent smObject) { m_HideSmartObject = smObject; }
    public void ClearSmartObject() { /* m_HideSmartObject.Clear(); */ }

    public bool IsUsingCover() { return m_isUsingCover; }
    public void SetUsingCover(bool state) { m_isUsingCover = state; }

    public int GetCoverUsage() { return m_useCover; }
    public void SetCoverUsage(int type) { m_useCover = type; }

    public uint GetCoverId() { return m_id; }

    public string GetAnchorName() { return m_sAnchorName; }

    public float GetObjectRadius() { return m_objectRadius; }
    public Vec3 GetObjectPos() { return m_objectPos; }
    public Vec3 GetObjectDir() { return m_objectDir; }
    public bool IsObjectCollidable() { return m_objectCollidable; }
    public Vec3 GetLastHidePos() { return m_vLastHidePos; }
    public SHideSpotInfo.EHideSpotType GetHideSpotType() { return m_hideSpotType; }

    public void Update(CPipeUser pOperand) { /* impl in .cpp */ }

    public bool HasLowCover() { return m_lowCoverValid; }
    public bool HasHighCover() { return m_highCoverValid; }
    public bool IsLeftEdgeValid(bool useLowCover) { return false; /* impl in .cpp */ }
    public bool IsRightEdgeValid(bool useLowCover) { return false; /* impl in .cpp */ }

    public void GetCoverPoints(bool useLowCover, float peekOverLeft, float peekOverRight, Vec3 targetPos,
        out Vec3 hidePos, out Vec3 peekPosLeft, out Vec3 peekPosRight, out bool peekLeftClamped, out bool peekRightClamped, out bool coverCompromised)
    {
        hidePos = m_vLastHidePos; peekPosLeft = m_vLastHidePos; peekPosRight = m_vLastHidePos;
        peekLeftClamped = false; peekRightClamped = false; coverCompromised = false;
        /* impl in .cpp */
    }

    public void GetCoverDistances(bool useLowCover, Vec3 target, out bool coverCompromised, out float leftEdge, out float rightEdge, out float leftUmbra, out float rightUmbra)
    { coverCompromised = false; leftEdge = 0; rightEdge = 0; leftUmbra = 0; rightUmbra = 0; /* impl in .cpp */ }

    public float GetCoverWidth(bool useLowCover) { return useLowCover ? m_lowCoverWidth : m_highCoverWidth; }
    public bool IsCoverPathComplete() { return m_pathComplete; }
    public void HurryUpCoverPathGen() { m_pathHurryUp = true; }
    public float GetDistanceAlongCoverPath(Vec3 pt) { return m_pathDir.Dot(pt - m_pathOrig); }
    public float GetDistanceToCoverPath(Vec3 pt) { return 0; /* impl in .cpp */ }
    public Vec3 ProjectPointOnCoverPath(Vec3 pt) { return m_pathOrig + m_pathDir * (m_pathDir.Dot(pt - m_pathOrig)); }
    public Vec3 GetCoverPathDir() { return m_pathDir; }
    public float GetMaxCoverPathLen() { return 0; /* impl in .cpp */ }
    public Vec3 GetPointAlongCoverPath(float distance) { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public void GetCoverHeightAlongCoverPath(float distance, Vec3 target, out bool hasLowCover, out bool hasHighCover)
    { hasLowCover = false; hasHighCover = false; /* impl in .cpp */ }

    public void DebugDraw() { /* impl in .cpp */ }
    public void Serialize(TSerialize ser) { /* impl in .cpp */ }

    private void SetupPathExpand(CPipeUser pOperand) { /* impl in .cpp */ }
    private void UpdatePathExpand(CPipeUser pOperand) { /* impl in .cpp */ }
    private bool IsSegmentValid(CPipeUser pOperand, Vec3 posFrom, Vec3 posTo) { return false; /* impl in .cpp */ }

    private void SampleCover(CPipeUser pOperand, ref float maxCover, ref float maxDepth, Vec3 startPos, float maxWidth,
        float sampleDist, float sampleRad, float sampleDepth, LinkedList<Vec3> points, bool pushBack, ref bool reachedEdge)
    { /* impl in .cpp */ }
    private void SampleCoverRefine(CPipeUser pOperand, ref float maxCover, ref float maxDepth, Vec3 startPos, float maxWidth,
        float sampleDist, float sampleRad, float sampleDepth, LinkedList<Vec3> points, bool pushBack)
    { /* impl in .cpp */ }
    private void SampleLine(CPipeUser pOperand, ref float maxMove, float maxWidth, float sampleDist) { /* impl in .cpp */ }
    private void SampleLineRefine(CPipeUser pOperand, ref float maxMove, float maxWidth, float sampleDist) { /* impl in .cpp */ }

    private /*mutable*/ bool m_bIsValid;
    private bool m_isUsingCover;
    private Vec3 m_objectPos;
    private Vec3 m_objectDir;
    private float m_objectRadius;
    private float m_objectHeight;
    private bool m_objectCollidable;
    private Vec3 m_vLastHidePos;
    private Vec3 m_vLastHideDir;
    private bool m_bIsSmartObject;
    private CQueryEvent m_HideSmartObject;
    private int m_useCover;
    private uint m_dynCoverEntityId;
    private Vec3 m_dynCoverEntityPos;
    private Vec3 m_dynCoverPosLocal;
    private SHideSpotInfo.EHideSpotType m_hideSpotType;
    private string m_sAnchorName = "";

    // Cover sampling
    private Vec3 m_coverPos;
    private float m_distToCover;
    private Vec3 m_pathOrig;
    private Vec3 m_pathDir;
    private Vec3 m_pathNorm;
    private float m_pathLimitLeft;
    private float m_pathLimitRight;
    private float m_tempCover;
    private float m_tempDepth;
    private bool m_pathComplete;
    private bool m_highCoverValid;
    private bool m_lowCoverValid;
    private bool m_pathHurryUp;
    private bool m_lowLeftEdgeValid;
    private bool m_lowRightEdgeValid;
    private bool m_highLeftEdgeValid;
    private bool m_highRightEdgeValid;
    private LinkedList<Vec3> m_lowCoverPoints = new LinkedList<Vec3>();
    private LinkedList<Vec3> m_highCoverPoints = new LinkedList<Vec3>();
    private float m_lowCoverWidth;
    private float m_highCoverWidth;

    private float m_lowLeftEdge;
    private float m_lowRightEdge;
    private float m_highLeftEdge;
    private float m_highRightEdge;
    private int m_pathUpdateIter;
    private uint m_id;
}
