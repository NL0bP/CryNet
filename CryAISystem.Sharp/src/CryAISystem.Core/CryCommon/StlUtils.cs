// Literal port of dev/Code/CryEngine/CryCommon/StlUtils.h (subset — symbols used by the AI port so far).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// CryEngine wraps STL helpers in a `stl::` namespace. The C# port mirrors this with a
// static class `stl` at the global scope so the calls remain literal: `stl.push_back_unique(v, item)`.

using System.Collections.Generic;

namespace CryAISystem;

public static class stl
{
    // Returns true if the item was added (not already present).
    public static bool push_back_unique<T>(List<T> v, T item)
    {
        if (!v.Contains(item))
        {
            v.Add(item);
            return true;
        }
        return false;
    }

    public static bool find_and_erase<T>(List<T> v, T item)
    {
        return v.Remove(item);
    }
}
