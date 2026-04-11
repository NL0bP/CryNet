// Helper for CRY_ASSERT macro from CryAssert.h.
// CRY_ASSERT in C++ is a macro that boils down to assert(cond). Translated literally
// here as a static method that wraps Debug.Assert.

namespace CryAISystem.CryCommon;

public static class CryAssert
{
    public static void Check(bool exp)
    {
        System.Diagnostics.Debug.Assert(exp);
    }
}
