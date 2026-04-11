// Literal port of dev/Code/CryEngine/CryAISystem/AIHash.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Runtime.InteropServices;

namespace CryAISystem;

public static class AIHash
{
    [StructLayout(LayoutKind.Explicit)]
    private struct f32_u
    {
        [FieldOffset(0)] public float floatVal;
        [FieldOffset(0)] public nuint uintVal;
    }

    //===================================================================
    // HashFromFloat
    //===================================================================
    public static nuint HashFromFloat(float key, float tol, float invTol)
    {
        float val = key;

        if (tol > 0.0f)
        {
            val *= invTol;
            val = (float)System.Math.Floor(val);
            val *= tol;
        }

        f32_u u = default;
        u.floatVal = val;

        nuint hash = u.uintVal;
        hash += ~(hash << 15);
        hash ^= (hash >> 10);
        hash += (hash << 3);
        hash ^= (hash >> 6);
        hash += ~(hash << 11);
        hash ^= (hash >> 16);

        return hash;
    }

    public static nuint HashFromUInt(nuint value)
    {
        nuint hash = value;
        hash += ~(hash << 15);
        hash ^= (hash >> 10);
        hash += (hash << 3);
        hash ^= (hash >> 6);
        hash += ~(hash << 11);
        hash ^= (hash >> 16);

        return hash;
    }




    //===================================================================
    // HashFromVec3
    //===================================================================
    public static nuint HashFromVec3(Vec3 v, float tol, float invTol)
    {
        return HashFromFloat(v.x, tol, invTol) + HashFromFloat(v.y, tol, invTol) + HashFromFloat(v.z, tol, invTol);
    }

    //===================================================================
    // HashFromQuat
    //===================================================================
    public static nuint HashFromQuat(Quat q, float tol, float invTol)
    {
        return HashFromFloat(q.v.x, tol, invTol) + HashFromFloat(q.v.y, tol, invTol) +
            HashFromFloat(q.v.z, tol, invTol) + HashFromFloat(q.w, tol, invTol);
    }

    //===================================================================
    // GetHashFromEntities
    //===================================================================
    public static nuint GetHashFromEntities(IPhysicalEntity[] entities, nuint entityCount)
    {
        nuint hash = 0;
        pe_status_pos status = new pe_status_pos();

        for (nuint i = 0; i < entityCount; ++i)
        {
            IPhysicalEntity entity = entities[i];
            entity.GetStatus(status);

            hash += HashFromUInt((nuint)entity.GetHashCode());
            hash += HashFromVec3(status.pos, 0.05f, 1.0f / 0.05f);
            hash += HashFromQuat(status.q, 0.01f, 1.0f / 0.01f);
        }

        if (hash == 0)
            hash = 0x1337d00d;

        return hash;
    }
}
