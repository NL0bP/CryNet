// Literal port of dev/Code/CryEngine/CryAISystem/AIPIDController.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : implementation of the CAIPIDController class.

using CryAISystem.CryCommon;

namespace CryAISystem;

//====================================================================
// CAIPIDController
//====================================================================
public struct CAIPIDController
{
    /// CP, CI and CD are the three coefficients. integralTimescale is the time over which
    /// to accululate the integral term (approximate).
    /// proportionalPower indicates the power that the error term should be raised to
    public CAIPIDController(float CP = 0.0f, float CI = 0.0f, float CD = 0.0f, float integralTimescale = 1.0f, uint proportionalPower = 1)
    {
        this.CP = CP;
        this.CI = CI;
        this.CD = CD;
        this.integralTimescale = integralTimescale;
        this.proportionalPower = proportionalPower;
        this.runningIntegral = 0.0f;
        this.lastError = 0.0f;
    }

    /// Update the intenal state and calculate an output
    public float Update(float error, float dt)
    {
        float frac = System.Math.Min(dt / integralTimescale, 1.0f);
        runningIntegral = frac * error + (1.0f - frac) * runningIntegral; // not quite dt independant...
        float output = CP * (float)System.Math.Pow(error, (float)proportionalPower);
        output += CI * runningIntegral;
        if (dt > 0.0f)
            output += CD * (error - lastError) / dt;
        lastError = error;
        return output;
    }

    /// The proportional, integral and derivative coefficients
    public float CP, CI, CD;

    /// The time over which to accululate the integral
    public float integralTimescale;

    /// output is proportional to error raised to this power
    public uint proportionalPower;

    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("AIPIDController");
        ser.Value("CP", ref CP);
        ser.Value("CI", ref CI);
        ser.Value("CD", ref CD);
        ser.Value("runningIntegral", ref runningIntegral);
        ser.Value("lastError", ref lastError);
        ser.Value("proportionalPower", ref proportionalPower);
        ser.Value("integralTimescale", ref integralTimescale);
        ser.EndGroup();
    }

    private float runningIntegral;
    private float lastError;
}
