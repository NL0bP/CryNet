// Port of CryEngine quaternionf for physics calculations
// Original: Copyright Crytek GMBH, used under license

using System.Numerics;
using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// Quaternion for physics rotations. Convention: (W, X, Y, Z) where W is scalar part.
/// Matches CryEngine's Quat (w,v) convention.
/// </summary>
public struct PhysQuaternion : IEquatable<PhysQuaternion>
{
    public float W, X, Y, Z;

    // Lowercase aliases matching the C++ Cry_Quat.h Quat layout (`Vec3 v; float w;`).
    // Patched in by the CryAISystem.Sharp port to preserve literal C++ field-access (`q.v.x`, `q.w`).
    public PhysVector3 v { get => new PhysVector3(X, Y, Z); set { X = value.X; Y = value.Y; Z = value.Z; } }
    public float w { get => W; set => W = value; }

    public static readonly PhysQuaternion Identity = new(1, 0, 0, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysQuaternion(float w, float x, float y, float z)
    {
        W = w; X = x; Y = y; Z = z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysQuaternion(float w, in PhysVector3 v)
    {
        W = w; X = v.X; Y = v.Y; Z = v.Z;
    }

    /// <summary>Construct from System.Numerics.Quaternion (X,Y,Z,W format).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysQuaternion(Quaternion q)
    {
        W = q.W; X = q.X; Y = q.Y; Z = q.Z;
    }

    /// <summary>Construct from rotation matrix.</summary>
    public PhysQuaternion(in PhysMatrix33 m)
    {
        float tr = m.M00 + m.M11 + m.M22;
        if (tr > 0)
        {
            float s = MathF.Sqrt(tr + 1f) * 2f;
            W = 0.25f * s;
            X = (m.M21 - m.M12) / s;
            Y = (m.M02 - m.M20) / s;
            Z = (m.M10 - m.M01) / s;
        }
        else if (m.M00 > m.M11 && m.M00 > m.M22)
        {
            float s = MathF.Sqrt(1f + m.M00 - m.M11 - m.M22) * 2f;
            W = (m.M21 - m.M12) / s;
            X = 0.25f * s;
            Y = (m.M01 + m.M10) / s;
            Z = (m.M02 + m.M20) / s;
        }
        else if (m.M11 > m.M22)
        {
            float s = MathF.Sqrt(1f + m.M11 - m.M00 - m.M22) * 2f;
            W = (m.M02 - m.M20) / s;
            X = (m.M01 + m.M10) / s;
            Y = 0.25f * s;
            Z = (m.M12 + m.M21) / s;
        }
        else
        {
            float s = MathF.Sqrt(1f + m.M22 - m.M00 - m.M11) * 2f;
            W = (m.M10 - m.M01) / s;
            X = (m.M02 + m.M20) / s;
            Y = (m.M12 + m.M21) / s;
            Z = 0.25f * s;
        }
    }

    /// <summary>Imaginary part as vector.</summary>
    public PhysVector3 V
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(X, Y, Z);
    }

    /// <summary>Quaternion multiplication (Hamilton product).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysQuaternion operator *(in PhysQuaternion a, in PhysQuaternion b) => new(
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W
    );

    /// <summary>Rotate a vector by this quaternion: q * v * q^(-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 Rotate(in PhysVector3 v)
    {
        // Optimized: t = 2 * cross(q.xyz, v), result = v + w*t + cross(q.xyz, t)
        var qv = new PhysVector3(X, Y, Z);
        var t = 2f * (qv ^ v);
        return v + W * t + (qv ^ t);
    }

    /// <summary>Inverse rotate (rotate by conjugate).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 InverseRotate(in PhysVector3 v)
    {
        var qv = new PhysVector3(-X, -Y, -Z);
        var t = 2f * (qv ^ v);
        return v + W * t + (qv ^ t);
    }

    /// <summary>Conjugate (inverse for unit quaternions).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysQuaternion Conjugate() => new(W, -X, -Y, -Z);

    /// <summary>Negation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysQuaternion operator -(in PhysQuaternion q) => new(-q.W, -q.X, -q.Y, -q.Z);

    /// <summary>Squared magnitude.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSq() => W * W + X * X + Y * Y + Z * Z;

    /// <summary>Normalize in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        float len = MathF.Sqrt(LengthSq());
        if (len > 1e-30f)
        {
            float rlen = 1f / len;
            W *= rlen; X *= rlen; Y *= rlen; Z *= rlen;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysQuaternion Normalized()
    {
        var q = this;
        q.Normalize();
        return q;
    }

    /// <summary>Convert to System.Numerics.Quaternion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Quaternion(in PhysQuaternion q) => new(q.X, q.Y, q.Z, q.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PhysQuaternion(Quaternion q) => new(q);

    /// <summary>
    /// Create a quaternion that rotates vector v0 to vector v1.
    /// Port of Quat::CreateRotationV0V1 from CryEngine.
    /// </summary>
    public static PhysQuaternion CreateRotationV0V1(in PhysVector3 v0, in PhysVector3 v1)
    {
        float dot = v0.Dot(v1);
        if (dot > 0.9999f)
            return Identity;
        if (dot < -0.9999f)
        {
            // 180 degree rotation around any perpendicular axis
            var perp = v0.GetOrthogonal().Normalized();
            return new PhysQuaternion(0, perp.X, perp.Y, perp.Z);
        }
        var cross = v0 ^ v1;
        float w = MathF.Sqrt(v0.LengthSq() * v1.LengthSq()) + dot;
        var q = new PhysQuaternion(w, cross.X, cross.Y, cross.Z);
        q.Normalize();
        return q;
    }

    /// <summary>Create from axis-angle.</summary>
    public static PhysQuaternion FromAxisAngle(in PhysVector3 axis, float angle)
    {
        float half = angle * 0.5f;
        float s = MathF.Sin(half);
        return new PhysQuaternion(MathF.Cos(half), axis.X * s, axis.Y * s, axis.Z * s);
    }

    public bool Equals(PhysQuaternion other) => W == other.W && X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is PhysQuaternion q && Equals(q);
    public override int GetHashCode() => HashCode.Combine(W, X, Y, Z);
    public static bool operator ==(in PhysQuaternion a, in PhysQuaternion b) => a.Equals(b);
    public static bool operator !=(in PhysQuaternion a, in PhysQuaternion b) => !a.Equals(b);
    public override string ToString() => $"(W:{W:F4}, X:{X:F4}, Y:{Y:F4}, Z:{Z:F4})";
}
