// Literal port of dev/Code/CryEngine/CryCommon/Cry_Vector3.h Vec3Constants<T> template,
// instantiated for T = float (the only specialization the CryAISystem code uses).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// C++:
//   template<typename T> struct Vec3Constants
//   {
//       static const Vec3_tpl<T> fVec3_Zero;
//       static const Vec3_tpl<T> fVec3_OneX;
//       static const Vec3_tpl<T> fVec3_OneY;
//       static const Vec3_tpl<T> fVec3_OneZ;
//       static const Vec3_tpl<T> fVec3_One;
//   };
//   template <typename T> const Vec3_tpl<T> Vec3Constants<T>::fVec3_Zero(0, 0, 0);
//   ... etc.

namespace CryAISystem.CryCommon;

public static class Vec3Constants
{
    public static readonly Vec3 fVec3_Zero = new Vec3(0, 0, 0);
    public static readonly Vec3 fVec3_OneX = new Vec3(1, 0, 0);
    public static readonly Vec3 fVec3_OneY = new Vec3(0, 1, 0);
    public static readonly Vec3 fVec3_OneZ = new Vec3(0, 0, 1);
    public static readonly Vec3 fVec3_One  = new Vec3(1, 1, 1);
}
