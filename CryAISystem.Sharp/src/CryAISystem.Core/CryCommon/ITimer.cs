// Literal port of dev/Code/CryEngine/CryCommon/ITimer.h (subset — members used so far).
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.CryCommon;

// Summary:
// Interface to the Timer System.
public interface ITimer
{
    enum ETimer
    {
        ETIMER_GAME = 0, // Pausable, serialized, frametime is smoothed/scaled/clamped.
        ETIMER_UI,       // Non-pausable, non-serialized, frametime unprocessed.
        ETIMER_LAST
    }

    enum ETimeScaleChannels
    {
        eTSC_Trackview = 0,
        eTSC_GameStart
    }

    // <interfuscator:shuffle>
    // virtual ~ITimer() {};

    // Resets the timer
    void ResetTimer();

    // Updates the timer every frame, needs to be called by the system.
    void UpdateOnFrameStart();

    // Returns the absolute time at the last UpdateOnFrameStart() call.
    float GetCurrTime(ETimer which = ETimer.ETIMER_GAME);

    // Returns the absolute time at the last UpdateOnFrameStart() call.
    CTimeValue GetFrameStartTime(ETimer which = ETimer.ETIMER_GAME);

    // Returns the absolute current time.
    CTimeValue GetAsyncTime();

    // Returns the absolute current time at the moment of the call.
    float GetAsyncCurTime();

    // Returns the relative time passed from the last UpdateOnFrameStart() in seconds.
    float GetFrameTime(ETimer which = ETimer.ETIMER_GAME);

    // Returns the relative time passed from the last UpdateOnFrameStart() in seconds without any dilation, smoothing, clamping, etc...
    float GetRealFrameTime();

    float GetTimeScale();
    float GetTimeScale(uint32 channel);
    void ClearTimeScales();
    void SetTimeScale(float s, uint32 channel = 0);

    void EnableTimer(bool bEnable);
    bool IsTimerEnabled();

    float GetFrameRate();
    // </interfuscator:shuffle>
}
