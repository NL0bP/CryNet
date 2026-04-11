// Literal port of struct types from dev/Code/CryEngine/CryCommon/IAgent.h that are needed
// to literally translate the standalone serialize functions at the end of AIObject.cpp.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.CryCommon;

// IAgent.h:990-1060 — SAIActorTargetRequest
public class SAIActorTargetRequest
{
    public SAIActorTargetRequest()
    {
        id = 0;
        approachLocation = new Vec3(0, 0, 0);
        approachDirection = new Vec3(0, 0, 0);
        animLocation = new Vec3(0, 0, 0);
        animDirection = new Vec3(0, 0, 0);
        vehicleSeat = 0;
        speed = 0;
        directionTolerance = 0;
        startArcAngle = 0;
        startWidth = 0;
        loopDuration = -1;
        signalAnimation = true;
        projectEndPoint = true;
        lowerPrecision = false;
        useAssetAlignment = false;
        stance = EStance.STANCE_NULL;
        pQueryStart = null;
        pQueryEnd = null;
    }

    public void Reset()
    {
        id = 0;
        approachLocation = new Vec3(0, 0, 0);
        approachDirection = new Vec3(0, 0, 0);
        animLocation = new Vec3(0, 0, 0);
        animDirection = new Vec3(0, 0, 0);
        vehicleSeat = 0;
        speed = 0;
        directionTolerance = 0;
        startArcAngle = 0;
        startWidth = 0;
        loopDuration = -1;
        signalAnimation = true;
        projectEndPoint = true;
        lowerPrecision = false;
        useAssetAlignment = false;
        stance = EStance.STANCE_NULL;
        pQueryStart = null;
        pQueryEnd = null;
        vehicleName = "";
        animation = "";
    }

    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("SAIActorTargetRequest");
        {
            ser.Value("id", ref id);
            if (id != 0)
            {
                ser.Value("approachLocation", ref approachLocation);
                ser.Value("approachDirection", ref approachDirection);
                ser.Value("animLocation", ref animLocation);
                ser.Value("animDirection", ref animDirection);
                ser.Value("vehicleName", ref vehicleName);
                ser.Value("vehicleSeat", ref vehicleSeat);
                ser.Value("speed", ref speed);
                ser.Value("directionTolerance", ref directionTolerance);
                ser.Value("startArcAngle", ref startArcAngle);
                ser.Value("startWidth", ref startWidth);
                ser.Value("signalAnimation", ref signalAnimation);
                ser.Value("projectEndPoint", ref projectEndPoint);
                ser.Value("lowerPrecision", ref lowerPrecision);
                ser.Value("useAssetAlignment", ref useAssetAlignment);
                ser.Value("animation", ref animation);
                ser.EnumValue("stance", ref stance, EStance.STANCE_NULL, EStance.STANCE_LAST);
                // TODO: Pointers!
                //		TAnimationGraphQueryID * pQueryStart;
                //		TAnimationGraphQueryID * pQueryEnd;
            }
        }
        ser.EndGroup();
    }

    public int id; // id=0 means invalid
    public Vec3 approachLocation;
    public Vec3 approachDirection;
    public Vec3 animLocation;
    public Vec3 animDirection;
    public string vehicleName = "";
    public int vehicleSeat;
    public float speed;
    public float directionTolerance;
    public float startArcAngle;
    public float startWidth;
    public float loopDuration; // (-1 = forever)
    public bool signalAnimation;
    public bool projectEndPoint;
    public bool lowerPrecision; // Lower precision should be true when passing through a navSO.
    public bool useAssetAlignment;
    public string animation = "";
    public EStance stance;
    public TAnimationGraphQueryID pQueryStart;
    public TAnimationGraphQueryID pQueryEnd;
}

// TAnimationGraphQueryID — forward decl from ICryAnimation.h. Full literal port pending.
public class TAnimationGraphQueryID { }

// IAgent.h:1394-1405 — SAIPredictedCharacterState
public class SAIPredictedCharacterState
{
    public SAIPredictedCharacterState() { predictionTime = 0f; position = new Vec3(0, 0, 0); velocity = new Vec3(0, 0, 0); }
    public void Set(Vec3 pos, Vec3 vel, float predT)
    {
        position = pos; velocity = vel; predictionTime = predT;
    }

    public void Serialize(TSerialize ser)
    {
        ser.Value("position", ref position);
        ser.Value("velocity", ref velocity);
        ser.Value("predictionTime", ref predictionTime);
    }

    public Vec3 position;
    public Vec3 velocity;
    public float predictionTime; // Time of prediction relative to when it was made.
}

// IAgent.h:1407-1415 — SAIPredictedCharacterStates
public class SAIPredictedCharacterStates
{
    public SAIPredictedCharacterStates()
    {
        nStates = 0;
        // C++ array `SAIPredictedCharacterState states[maxStates];` default-constructs each element.
        // C# array of class is null per slot, so we explicitly construct each to preserve semantics.
        for (int i = 0; i < maxStates; ++i) states[i] = new SAIPredictedCharacterState();
    }

    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("SAIPredictedCharacterStates");
        {
            ser.Value("nStates", ref nStates);
            int counter = 0;
            for (int i = 0; i < maxStates; ++i, ++counter)
            {
                string stateGroupName = string.Format("State_{0}", counter);
                ser.BeginGroup(stateGroupName);
                {
                    states[i].Serialize(ser);
                }
                ser.EndGroup();
            }
        }
        ser.EndGroup();
    }

    public const int maxStates = 32;
    public SAIPredictedCharacterState[] states = new SAIPredictedCharacterState[maxStates];
    public int nStates;
}
