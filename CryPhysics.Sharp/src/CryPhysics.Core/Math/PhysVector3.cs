// Port of CryEngine Vec3 with physics-specific operators
// Original: Copyright Crytek GMBH, used under license

using System.Numerics;
using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// 3D vector for physics calculations. Wraps System.Numerics.Vector3 with
/// physics-specific operations matching CryEngine's Vec3 conventions.
/// </summary>
public struct PhysVector3 : IEquatable<PhysVector3>
{
    public float X, Y, Z;

    // Lowercase aliases matching the C++ Cry_Vector3.h Vec3 fields exactly.
    // Patched in by the CryAISystem.Sharp port to preserve literal C++ field-access (`v.x`, `v.y`, `v.z`).
    public float x { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => X; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => X = value; }
    public float y { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Y; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => Y = value; }
    public float z { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Z; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => Z = value; }

    public static readonly PhysVector3 Zero = new(0, 0, 0);
    public static readonly PhysVector3 UnitX = new(1, 0, 0);
    public static readonly PhysVector3 UnitY = new(0, 1, 0);
    public static readonly PhysVector3 UnitZ = new(0, 0, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3(float x, float y, float z) { X = x; Y = y; Z = z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3(Vector3 v) { X = v.X; Y = v.Y; Z = v.Z; }

    /// Patch — port of `Vec3::Vec3(const Vec2& v)` from Cry_Vector3.h. Z is set to 0.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3(PhysVector2 v) { X = v.X; Y = v.Y; Z = 0f; }

    public float this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => i switch { 0 => X, 1 => Y, 2 => Z, _ => throw new IndexOutOfRangeException() };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set { switch (i) { case 0: X = value; break; case 1: Y = value; break; case 2: Z = value; break; default: throw new IndexOutOfRangeException(); } }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(float x, float y, float z) { X = x; Y = y; Z = z; }

    // Arithmetic operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator +(in PhysVector3 a, in PhysVector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator -(in PhysVector3 a, in PhysVector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator -(in PhysVector3 a) => new(-a.X, -a.Y, -a.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator *(in PhysVector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator *(float s, in PhysVector3 a) => new(a.X * s, a.Y * s, a.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator /(in PhysVector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);

    // Dot product (CryEngine uses * for dot on Vec3mem)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dot(in PhysVector3 other) => X * other.X + Y * other.Y + Z * other.Z;

    // Cross product (CryEngine uses ^ for cross on Vec3mem)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 Cross(in PhysVector3 other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X
    );

    /// <summary>Cross product operator (matches CryEngine's ^ operator).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator ^(in PhysVector3 a, in PhysVector3 b) => a.Cross(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSq() => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(LengthSq());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 Normalized()
    {
        float len = LengthSq();
        if (len > 0f)
        {
            float rlen = 1f / MathF.Sqrt(len);
            return new PhysVector3(X * rlen, Y * rlen, Z * rlen);
        }
        return UnitX;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        float len = LengthSq();
        if (len > 0f)
        {
            float rlen = 1f / MathF.Sqrt(len);
            X *= rlen; Y *= rlen; Z *= rlen;
        }
    }

    // Patches added by CryAISystem.Sharp port — literal C++ Vec3 method names from Cry_Vector3.h.
    // Logged in CryAISystem.Sharp/cryphysics_patches.md.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NormalizeSafe()
    {
        float lenSq = X * X + Y * Y + Z * Z;
        if (lenSq > 0f)
        {
            float len = MathF.Sqrt(lenSq);
            float rlen = 1f / len;
            X *= rlen; Y *= rlen; Z *= rlen;
            return len;
        }
        X = 0f; Y = 0f; Z = 0f;
        return 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NormalizeSafe(PhysVector3 safe)
    {
        float lenSq = X * X + Y * Y + Z * Z;
        if (lenSq > 0f)
        {
            float len = MathF.Sqrt(lenSq);
            float rlen = 1f / len;
            X *= rlen; Y *= rlen; Z *= rlen;
            return len;
        }
        X = safe.X; Y = safe.Y; Z = safe.Z;
        return 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetNormalizedSafe()
    {
        float lenSq = X * X + Y * Y + Z * Z;
        if (lenSq > 0f)
        {
            float rlen = 1f / MathF.Sqrt(lenSq);
            return new PhysVector3(X * rlen, Y * rlen, Z * rlen);
        }
        return UnitX;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetNormalizedSafe(PhysVector3 safe)
    {
        float lenSq = X * X + Y * Y + Z * Z;
        if (lenSq > 0f)
        {
            float rlen = 1f / MathF.Sqrt(lenSq);
            return new PhysVector3(X * rlen, Y * rlen, Z * rlen);
        }
        return safe;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetLengthSquared() => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetLengthSquared2D() => X * X + Y * Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float len2() => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsZero(float epsilon = 0f)
    {
        return MathF.Abs(X) <= epsilon && MathF.Abs(Y) <= epsilon && MathF.Abs(Z) <= epsilon;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid()
    {
        return !float.IsNaN(X) && !float.IsInfinity(X)
            && !float.IsNaN(Y) && !float.IsInfinity(Y)
            && !float.IsNaN(Z) && !float.IsInfinity(Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEquivalent(PhysVector3 other, float epsilon = 0.05f)
    {
        return MathF.Abs(X - other.X) <= epsilon
            && MathF.Abs(Y - other.Y) <= epsilon
            && MathF.Abs(Z - other.Z) <= epsilon;
    }

    /// <summary>Component-wise min.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 Min(in PhysVector3 a, in PhysVector3 b) => new(
        MathUtils.Min(a.X, b.X), MathUtils.Min(a.Y, b.Y), MathUtils.Min(a.Z, b.Z));

    /// <summary>Component-wise max.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 Max(in PhysVector3 a, in PhysVector3 b) => new(
        MathUtils.Max(a.X, b.X), MathUtils.Max(a.Y, b.Y), MathUtils.Max(a.Z, b.Z));

    /// <summary>
    /// Rotate this vector around an axis by angle defined by cos/sin.
    /// Port of Vec3::GetRotated(axis, cosa, sina).
    /// Rodrigues' rotation formula: v' = v*cos + (axis x v)*sin + axis*(axis.v)*(1-cos)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetRotated(in PhysVector3 axis, float cosa, float sina)
    {
        var cross = axis.Cross(this);
        float dot = axis.Dot(this);
        return this * cosa + cross * sina + axis * (dot * (1f - cosa));
    }

    /// <summary>
    /// Get an orthogonal vector. Port of Vec3::GetOrthogonal().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetOrthogonal()
    {
        // Choose the axis least aligned with this vector
        float ax = MathF.Abs(X), ay = MathF.Abs(Y), az = MathF.Abs(Z);
        if (ax < ay && ax < az)
            return new PhysVector3(0, -Z, Y);
        if (ay < az)
            return new PhysVector3(-Z, 0, X);
        return new PhysVector3(-Y, X, 0);
    }

    // Conversion to/from System.Numerics.Vector3
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector3(in PhysVector3 v) => new(v.X, v.Y, v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PhysVector3(Vector3 v) => new(v);

    // Equality
    public bool Equals(PhysVector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is PhysVector3 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(in PhysVector3 a, in PhysVector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(in PhysVector3 a, in PhysVector3 b) => !(a == b);
    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
}

/// <summary>
/// 2D vector for physics calculations.
/// Port of CryEngine Vec2 / vector2df.
/// </summary>
public struct PhysVector2 : IEquatable<PhysVector2>
{
    public float X, Y;

    /// Lowercase aliases matching the C++ Cry_Vector2.h Vec2 fields exactly.
    /// Patched in by the CryAISystem.Sharp port to preserve literal C++ field-access (`v.x`, `v.y`).
    public float x { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => X; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => X = value; }
    public float y { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Y; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => Y = value; }

    public static readonly PhysVector2 Zero = new(0, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector2(float x, float y) { X = x; Y = y; }

    /// Patch — port of `Vec2::Vec2(const Vec3& v)` from Cry_Vector2.h. Truncates Z.
    public PhysVector2(PhysVector3 v) { X = v.X; Y = v.Y; }

    public float this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => i == 0 ? X : Y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set { if (i == 0) X = value; else Y = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(float x, float y) { X = x; Y = y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector2 operator +(in PhysVector2 a, in PhysVector2 b) => new(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector2 operator -(in PhysVector2 a, in PhysVector2 b) => new(a.X - b.X, a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector2 operator *(in PhysVector2 a, float s) => new(a.X * s, a.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector2 operator *(float s, in PhysVector2 a) => new(a.X * s, a.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSq() => X * X + Y * Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(LengthSq());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector2 Normalized()
    {
        float rlen = LengthSq();
        if (rlen > 0f)
        {
            rlen = 1f / MathF.Sqrt(rlen);
            return new PhysVector2(X * rlen, Y * rlen);
        }
        return new PhysVector2(1, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector2 Min(in PhysVector2 a, in PhysVector2 b) => new(
        MathUtils.Min(a.X, b.X), MathUtils.Min(a.Y, b.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector2 Max(in PhysVector2 a, in PhysVector2 b) => new(
        MathUtils.Max(a.X, b.X), MathUtils.Max(a.Y, b.Y));

    public bool Equals(PhysVector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is PhysVector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(in PhysVector2 a, in PhysVector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(in PhysVector2 a, in PhysVector2 b) => !(a == b);
    public override string ToString() => $"({X:F4}, {Y:F4})";
}

/// <summary>
/// 2D integer vector. Port of vector2di.
/// </summary>
public struct Vector2i
{
    public int X, Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2i(int x, int y) { X = x; Y = y; }

    public int this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => i == 0 ? X : Y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set { if (i == 0) X = value; else Y = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int x, int y) { X = x; Y = y; }
}
