// Literal port of dev/Code/CryEngine/CryAISystem/Configuration.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Simple struct for storing fundamental AI system settings

namespace CryAISystem;

// These values should not change and are carefully chosen but arbitrary
public enum EConfigCompatibilityMode
{
    ECCM_NONE = 0,
    ECCM_CRYSIS = 2,
    ECCM_GAME04 = 4,
    ECCM_WARFACE = 7,
    ECCM_CRYSIS2 = 8
}


public struct SConfiguration
{
    public EConfigCompatibilityMode eCompatibilityMode;

    // Should probably include logging and debugging flags
}
