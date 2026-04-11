// Literal port of dev/Code/CryEngine/CryAISystem/AIVehicle.{h,cpp}
// Session 12: ctor/dtor + RecalculateAccuracy + PredictMovingTarget literal port.
// Other methods (Update/UpdateDisabled/Navigate/Event/Reset/ParseParameters/AlertPuppets/
// Serialize/HandleVerticalMovement/IsPlayerInside/GetDriverEntity/GetDriver/FireCommand/
// GetEnemyTarget/OnDriverChanged/CheckExplosion/GetError/etc) deferred — heavy CPuppet
// state and goal/target dependencies.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryAISystem.CryCommon;

namespace CryAISystem;

public class CAIVehicle : CPuppet
{
    // Literal port of CAIVehicle::CAIVehicle() from AIVehicle.cpp lines 30-45.
    public CAIVehicle()
    {
        m_bDriverInside = false;
        m_driverInsideCheck = -1;
        m_playerInsideCheck = -1;
        // m_fNextFiringTime/m_fFiringStartTime/m_fFiringPauseTime use GetAISystem()->GetFrameStartTime()
        // — until CAISystem.GetFrameStartTime is wired up the literal init resolves to default(CTimeValue).
        m_fNextFiringTime = GetAISystem() != null ? GetAISystem().GetFrameStartTime() : new CTimeValue();
        m_fFiringStartTime = GetAISystem() != null ? GetAISystem().GetFrameStartTime() : new CTimeValue();
        m_fFiringPauseTime = GetAISystem() != null ? GetAISystem().GetFrameStartTime() : new CTimeValue();
        m_bPoweredUp = false;
        m_ShootPhase = 0;
        m_vDeltaTarget = new Vec3(0, 0, 0);

        _fastcast_CAIVehicle = true;
        // can't reset now - no parameters are initialized yet
        //	Reset();
    }

    // ~CAIVehicle() — empty per literal C++ (AIVehicle.cpp lines 49-52)
    ~CAIVehicle() { }

    public new void Update(EObjectUpdate type) { /* impl in .cpp */ }
    public new void UpdateDisabled(EObjectUpdate type) { /* impl in .cpp */ }
    public void Navigate(CAIObject pTarget) { /* impl in .cpp */ }
    public override void Event(ushort eType, SAIEVENT pEvent) { /* impl in .cpp */ }

    public override void Reset(EObjectResetType type) { /* impl in .cpp */ }
    public new void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true) { /* impl in .cpp */ }
    public override uint GetPerceivedEntityID() { return 0; /* impl in .cpp */ }

    public void AlertPuppets() { /* impl in .cpp */ }
    public override void Serialize(TSerialize ser) { /* impl in .cpp */ }

    public bool HandleVerticalMovement(Vec3 targetPos) { return false; /* impl in .cpp */ }

    public bool IsDriverInside() { return m_bDriverInside; /* impl in .cpp */ }
    public bool IsPlayerInside() { return false; /* impl in .cpp */ }

    public uint GetDriverEntity() { return 0; /* impl in .cpp */ }
    public CAIActor GetDriver() { return null; /* impl in .cpp */ }

    public override bool IsTargetable() { return IsActive(); }
    public new bool IsActive() { return m_bEnabled || IsDriverInside(); }

    public new void GetPathFollowerParams(PathFollowerParams outParams) { /* impl in .cpp */ }

    protected void FireCommand() { /* impl in .cpp */ }
    protected bool GetEnemyTarget(int objectType, out Vec3 hitPosition, float fDamageRadius2, out CAIObject pTarget) { hitPosition = new Vec3(0, 0, 0); pTarget = null; return false; /* impl in .cpp */ }
    protected void OnDriverChanged(bool bEntered) { /* impl in .cpp */ }

    // local functions for firecommand()

    // Literal port of CAIVehicle::PredictMovingTarget (AIVehicle.cpp lines 162-193).
    private Vec3 PredictMovingTarget(CAIObject pTarget, Vec3 vTargetPos, Vec3 vFirePos, float duration, float distpred)
    {
        Vec3 vError = new Vec3(0, 0, 0);

        // if we need a prediction of the target
        if (pTarget != null)
        {
            pe_status_dynamics dSt = new pe_status_dynamics();
            GetProxy().GetPhysics().GetStatus(dSt);
            IPhysicalEntity pPhys = pTarget.GetPhysics();
            if (pPhys != null)
            {
                pPhys.GetStatus(dSt);
                if (GetSubType() == ESubType.STP_HELI)
                {
                    vError = dSt.v * duration;
                }
                else
                {
                    Vec3 vPrediction = vTargetPos + dSt.v * duration;
                    Vec3 vTmp = vPrediction - vFirePos;
                    if (distpred > 0.0f)
                    {
                        float len = vTmp.Length();
                        vError.z = len / distpred;
                    }
                }
            }
        }
        return vError;
    }

    private bool CheckExplosion(Vec3 vTargetPos, Vec3 vFirePos, Vec3 vActuallFireDir, float fDamageRadius) { return false; /* impl in .cpp — needs gAIEnv.pWorld.RayWorldIntersection */ }

    private Vec3 GetError(Vec3 vTargetPos, Vec3 vFirePos, float fAccuracy) { return new Vec3(0, 0, 0); /* impl in .cpp — needs cry_random + p3DEngine */ }

    // Literal port of CAIVehicle::RecalculateAccuracy (AIVehicle.cpp lines 149-161).
    private float RecalculateAccuracy()
    {
        float fAccuracy = m_Parameters.m_fAccuracy;

        if (fAccuracy > 1.0f)
            fAccuracy = 1.0f;
        else if (fAccuracy < 0)
            fAccuracy = 0;

        return fAccuracy;
    }

    private bool m_bPoweredUp;

    private CTimeValue m_fNextFiringTime;
    private CTimeValue m_fFiringPauseTime;
    private CTimeValue m_fFiringStartTime;

    private Vec3 m_vDeltaTarget;

    private int m_ShootPhase;
    private /*mutable*/ int m_driverInsideCheck;
    private int m_playerInsideCheck;
    private bool m_bDriverInside;
}

public static class CAIVehicleHelpers
{
    public static CAIVehicle CastToCAIVehicleSafe(IAIObject pAI) { return pAI != null ? null /* pAI.CastToCAIVehicle() */ : null; }
}

// Forward decl for PathFollowerParams (Phase 3)
public class PathFollowerParams { }
