// Literal port of dev/Code/CryEngine/CryAISystem/PostureManager.h
// (PostureManager.cpp impl deferred — see deferred.md)
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public class PostureManager
{
    // typedef int32 PostureID;
    // typedef uint32 PostureQueryID;

    public enum PostureType
    {
        InvalidPosture = 0,
        PeekPosture = 1,
        AimPosture = 2,
        HidePosture = 3,
    }

    [System.Flags]
    public enum PostureQueryChecks
    {
        CheckVisibility = 1 << 0,
        CheckAimability = 1 << 1,
        CheckLeanability = 1 << 2,
        CheckFriendlyFire = 1 << 3,
        DefaultChecks = CheckVisibility | CheckAimability | CheckLeanability | CheckFriendlyFire,
    }

    public struct PostureQuery
    {
        public PostureQuery(int dummy = 0)
        {
            position = new Vec3(0, 0, 0);
            target = new Vec3(0, 0, 0);
            actor = null;
            distancePercent = 0.5f;
            coverID = new CoverID();
            checks = (uint)PostureQueryChecks.DefaultChecks;
            hintPostureID = -1;
            type = PostureType.InvalidPosture;
            allowLean = true;
            allowProne = false;
            stickyStance = true;
        }

        public Vec3 position;
        public Vec3 target;

        public CAIActor actor;

        public float distancePercent;  // distance from actor to target to use for checks
        public CoverID coverID;        // currenct actor cover

        public uint checks;            // which checks to perform
        public int hintPostureID;      // current posture - check this first
        public PostureType type;
        public bool allowLean;
        public bool allowProne;
        public bool stickyStance;
    }

    public struct PostureInfo
    {
        public PostureInfo(int dummy = 0)
        {
            type = PostureType.InvalidPosture;
            lean = 0.0f;
            peekOver = 0.0f;
            stance = EStance.STANCE_NULL;
            priority = 0.0f;
            minDistanceToTarget = 0.0f;
            maxDistanceToTarget = float.MaxValue;
            parentID = -1;
            enabled = true;
            agInput = "";
            name = "";
        }

        public PostureInfo(PostureType _type, string _name, float _lean, float _peekOver, EStance _stance, float _priority, string _agInput = "", int _parentID = -1)
        {
            type = _type;
            lean = _lean;
            peekOver = _peekOver;
            stance = _stance;
            priority = _priority;
            minDistanceToTarget = 0.0f;
            maxDistanceToTarget = float.MaxValue;
            parentID = _parentID;
            enabled = true;
            agInput = _agInput;
            name = _name;
        }

        public void GetMemoryUsage(ICrySizer pSizer)
        {
            // pSizer.AddObject(agInput);
            // pSizer.AddObject(name);
        }

        public PostureType type;
        public float lean;
        public float peekOver;
        public float priority;
        public float minDistanceToTarget;
        public float maxDistanceToTarget;
        public EStance stance;
        public int parentID;
        public bool enabled;
        public string agInput;
        public string name;
    }

    // Literal port of PostureManager.cpp lines 19-181 (session 5)

    public PostureManager()
    {
        m_queryGenID = 0;
        m_queueTail = 0;
        m_queueSize = 0;
        // m_queue.resize(2)
        m_queue.Add(new QueuedQuery(0));
        m_queue.Add(new QueuedQuery(0));
    }

    // C++ ~PostureManager() — cancels rays for every queued query.
    ~PostureManager()
    {
        for (int it = 0; it < m_queue.Count; ++it)
        {
            QueuedQuery query = m_queue[it];
            CancelRays(query);
        }
    }

    public void ResetPostures()
    {
        for (int it = 0; it < m_queue.Count; ++it)
        {
            QueuedQuery query = m_queue[it];
            CancelRays(query);
        }

        m_postureInfos.Clear();
    }

    public void AddDefaultPostures(PostureType type)
    {
        switch (type)
        {
            case PostureType.HidePosture:
                {
                    AddPosture(new PostureInfo(PostureType.HidePosture, "HideProne",   0.0f, 0.0f, EStance.STANCE_PRONE,   5.0f));
                    AddPosture(new PostureInfo(PostureType.HidePosture, "HideCrouch",  0.0f, 0.0f, EStance.STANCE_CROUCH,  3.0f));
                    AddPosture(new PostureInfo(PostureType.HidePosture, "HideStealth", 0.0f, 0.0f, EStance.STANCE_STEALTH, 2.0f));
                    AddPosture(new PostureInfo(PostureType.HidePosture, "HideStand",   0.0f, 0.0f, EStance.STANCE_STAND,   1.0f));
                }
                break;
            case PostureType.AimPosture:
                {
                    int crouchID = -1;
                    int standID = -1;

                    AddPosture(new PostureInfo(PostureType.AimPosture, "AimProne", 0.0f, 0.0f, EStance.STANCE_PRONE, 5.0f));
                    crouchID = AddPosture(new PostureInfo(PostureType.AimPosture, "AimCrouch", 0.0f, 0.0f, EStance.STANCE_CROUCH, 4.0f));

                    AddPosture(new PostureInfo(PostureType.AimPosture, "AimCrouchRightLean",  1.0f, 0.0f, EStance.STANCE_CROUCH, 3.0f, "peekRight", crouchID));
                    AddPosture(new PostureInfo(PostureType.AimPosture, "AimCrouchLeftLean",  -1.0f, 0.0f, EStance.STANCE_CROUCH, 3.0f, "peekLeft",  crouchID));

                    standID = AddPosture(new PostureInfo(PostureType.AimPosture, "AimStand", 0.0f, 0.0f, EStance.STANCE_STAND, 2.0f));
                    AddPosture(new PostureInfo(PostureType.AimPosture, "AimStandRightLean",  1.0f, 0.0f, EStance.STANCE_STAND, 1.0f, "peekRight", standID));
                    AddPosture(new PostureInfo(PostureType.AimPosture, "AimStandLeftLean",  -1.0f, 0.0f, EStance.STANCE_STAND, 1.0f, "peekLeft",  standID));
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
        }
    }

    public int AddPosture(PostureInfo posture)
    {
        m_postureInfos.Add(posture);
        return m_postureInfos.Count - 1;
    }

    public void SetPosture(int postureID, PostureInfo posture)
    {
        System.Diagnostics.Debug.Assert(postureID >= 0 && postureID < m_postureInfos.Count);
        m_postureInfos[postureID] = posture;
    }

    public bool GetPosture(int postureID, out PostureInfo posture)
    {
        if ((postureID < 0) || (postureID >= m_postureInfos.Count))
        {
            posture = default;
            return false;
        }

        posture = m_postureInfos[postureID];
        return true;
    }

    public int GetPostureID(string postureName)
    {
        for (uint32 i = 0; i < (uint32)m_postureInfos.Count; ++i)
        {
            if (m_postureInfos[(int)i].name == postureName)
                return (int)i;
        }

        return -1;
    }

    public bool GetPostureByName(string postureName, out PostureInfo posture)
    {
        for (uint32 i = 0; i < (uint32)m_postureInfos.Count; ++i)
        {
            PostureInfo info = m_postureInfos[(int)i];

            if (info.name == postureName)
            {
                posture = info;
                return true;
            }
        }

        posture = default;
        return false;
    }

    public void SetPosturePriority(int postureID, float priority)
    {
        if ((postureID < 0) || (postureID >= m_postureInfos.Count))
            return;

        PostureInfo info = m_postureInfos[postureID];
        info.priority = priority;
        m_postureInfos[postureID] = info;
    }

    public float GetPosturePriority(int postureID)
    {
        if ((postureID < 0) || (postureID >= m_postureInfos.Count))
            return 0.0f;

        PostureInfo postureInfo = m_postureInfos[postureID];

        return GetPosturePriority(postureInfo.parentID) + postureInfo.priority;
    }

    public uint QueryPosture(PostureQuery postureQuery) { return 0; }
    public void CancelPostureQuery(uint queryID) { }
    public AsyncState GetPostureQueryResult(uint queryID, out int postureID, out PostureInfo postureInfo)
    {
        postureID = -1;
        postureInfo = default;
        return AsyncState.AsyncFailed;
    }

    private List<PostureInfo> m_postureInfos = new List<PostureInfo>();

    private struct RunningPosture
    {
        public RunningPosture(short _postureID = -1)
        {
            postureID = _postureID;
            targetVis = false;
            targetAim = false;
            eye = new Vec3(0, 0, 0);
            weapon = new Vec3(0, 0, 0);
            processed = false;
        }

        public Vec3 eye;
        public Vec3 weapon;

        public int postureID;
        public bool targetVis; // : 1
        public bool targetAim; // : 1
        public bool processed; // : 1
    }

    private struct PostureSorter : IComparer<RunningPosture>
    {
        public PostureSorter(PostureManager _manager, int _hintPostureID = -1)
        {
            manager = _manager;
            hintPostureID = _hintPostureID;
        }

        public int Compare(RunningPosture lhs, RunningPosture rhs)
        {
            if (hintPostureID == lhs.postureID)
                return -1;

            if (hintPostureID == rhs.postureID)
                return 1;

            return CAISystemStatics.CompareFloatsFPUBugWorkaround(manager.GetPosturePriority(lhs.postureID),
                manager.GetPosturePriority(rhs.postureID)) ? -1 : 1;
        }

        public int hintPostureID;
        public PostureManager manager;
    }

    private struct StickyStancePostureSorter : IComparer<RunningPosture>
    {
        public StickyStancePostureSorter(PostureManager _manager, List<PostureInfo> _infos, EStance _stickyStance,
            int _hintPostureID = -1)
        {
            manager = _manager;
            infos = _infos;
            stickyStance = _stickyStance;
            hintPostureID = _hintPostureID;
        }

        public int Compare(RunningPosture lhs, RunningPosture rhs)
        {
            if (hintPostureID == lhs.postureID)
                return -1;

            if (hintPostureID == rhs.postureID)
                return 1;

            EStance lhsStance = infos[(int)lhs.postureID].stance;
            EStance rhsStance = infos[(int)rhs.postureID].stance;

            if (lhsStance != rhsStance)
            {
                if (lhsStance == stickyStance)
                    return -1;
                else if (rhsStance == stickyStance)
                    return 1;
            }

            return CAISystemStatics.CompareFloatsFPUBugWorkaround(manager.GetPosturePriority(lhs.postureID),
                manager.GetPosturePriority(rhs.postureID)) ? -1 : 1;
        }

        public List<PostureInfo> infos;
        public EStance stickyStance;
        public int hintPostureID;
        public PostureManager manager;
    }

    private struct QueuedPostureCheck
    {
        public QueuedRayID leanabilityRayID;
        public QueuedRayID visibilityRayID;
        public QueuedRayID aimabilityRayID;

        public int postureID;

        public uint8 awaitingResultCount;
        public uint8 positiveResultCount;
    }

    // typedef std::vector<QueuedPostureCheck> QueuedPostureChecks;

    private struct QueuedQuery
    {
        public QueuedQuery(int dummy = 0)
        {
            queryID = 0;
            status = AsyncState.AsyncFailed;
            result = -1;
            postureChecks = new List<QueuedPostureCheck>();
        }

        public uint queryID;
        public AsyncState status;
        public int result;
        public List<QueuedPostureCheck> postureChecks;
    }

    // typedef std::vector<QueuedQuery> QueuedPostureQueries;
    private List<QueuedQuery> m_queue = new List<QueuedQuery>();

    private uint m_queryGenID;
    private uint m_queueTail;
    private uint m_queueSize;

    private const int TotalCheckCount = 3;

    private void CancelRays(QueuedQuery query)
    {
        List<QueuedPostureCheck> postureChecks = query.postureChecks;
        for (int pit = 0; pit < postureChecks.Count; ++pit)
        {
            QueuedPostureCheck cancellingCheck = postureChecks[pit];

            if (cancellingCheck.leanabilityRayID.id != 0)
                gAIEnv.pRayCaster.Cancel(cancellingCheck.leanabilityRayID);

            if (cancellingCheck.aimabilityRayID.id != 0)
                gAIEnv.pRayCaster.Cancel(cancellingCheck.aimabilityRayID);

            if (cancellingCheck.visibilityRayID.id != 0)
                gAIEnv.pRayCaster.Cancel(cancellingCheck.visibilityRayID);
        }

        query.postureChecks.Clear();
    }
    private void RayComplete(QueuedRayID rayID, RayCastResult result) { }
}

// Forward decls / shells
public struct CoverID { public uint id; public bool IsValid() { return id != 0; } public CoverID(uint id = 0) { this.id = id; } }
// Literal port of dev/Code/CryEngine/CryCommon/IAgent.h:204-219 enum EStance
public enum EStance
{
    STANCE_NULL = -1,
    STANCE_STAND = 0,
    STANCE_CROUCH,
    STANCE_PRONE,
    STANCE_RELAXED,
    STANCE_STEALTH,
    STANCE_LOW_COVER,
    STANCE_ALERTED,
    STANCE_HIGH_COVER,
    STANCE_SWIM,
    STANCE_ZEROG,
    // This value must be last
    STANCE_LAST
}
public enum AsyncState { AsyncFailed, AsyncReady, AsyncInProgress, AsyncComplete }
public struct QueuedRayID { public int id; }
// RayCastResult lives in Environment.cs (proper shell with operator overloads).

// Static helper on CAISystem (Phase 11 will provide the real one)
public static class CAISystemStatics
{
    public static bool CompareFloatsFPUBugWorkaround(float a, float b) { return a > b; }
}
