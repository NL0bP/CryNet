// Port of CryEngine Matrix33 for physics calculations
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// 3x3 matrix for physics calculations (rotation, inertia tensors).
/// Row-major storage matching CryEngine's Matrix33.
/// Elements: M{row}{col}, e.g., M01 = row 0, col 1.
/// </summary>
public struct PhysMatrix33
{
    public float M00, M01, M02;
    public float M10, M11, M12;
    public float M20, M21, M22;

    public static readonly PhysMatrix33 Identity = new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1
    );

    public static readonly PhysMatrix33 Zero = default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysMatrix33(
        float m00, float m01, float m02,
        float m10, float m11, float m12,
        float m20, float m21, float m22)
    {
        M00 = m00; M01 = m01; M02 = m02;
        M10 = m10; M11 = m11; M12 = m12;
        M20 = m20; M21 = m21; M22 = m22;
    }

    /// <summary>Construct from 3 row vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysMatrix33(in PhysVector3 row0, in PhysVector3 row1, in PhysVector3 row2)
    {
        M00 = row0.X; M01 = row0.Y; M02 = row0.Z;
        M10 = row1.X; M11 = row1.Y; M12 = row1.Z;
        M20 = row2.X; M21 = row2.Y; M22 = row2.Z;
    }

    /// <summary>Construct from a quaternion rotation.</summary>
    public PhysMatrix33(in PhysQuaternion q)
    {
        float x2 = q.X + q.X, y2 = q.Y + q.Y, z2 = q.Z + q.Z;
        float xx = q.X * x2, xy = q.X * y2, xz = q.X * z2;
        float yy = q.Y * y2, yz = q.Y * z2, zz = q.Z * z2;
        float wx = q.W * x2, wy = q.W * y2, wz = q.W * z2;

        M00 = 1f - (yy + zz); M01 = xy - wz;         M02 = xz + wy;
        M10 = xy + wz;         M11 = 1f - (xx + zz);  M12 = yz - wx;
        M20 = xz - wy;         M21 = yz + wx;          M22 = 1f - (xx + yy);
    }

    /// <summary>Element access by row,col.</summary>
    public float this[int row, int col]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (row * 3 + col) switch
        {
            0 => M00, 1 => M01, 2 => M02,
            3 => M10, 4 => M11, 5 => M12,
            6 => M20, 7 => M21, 8 => M22,
            _ => throw new IndexOutOfRangeException()
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (row * 3 + col)
            {
                case 0: M00 = value; break; case 1: M01 = value; break; case 2: M02 = value; break;
                case 3: M10 = value; break; case 4: M11 = value; break; case 5: M12 = value; break;
                case 6: M20 = value; break; case 7: M21 = value; break; case 8: M22 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>Get row as vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetRow(int i) => i switch
    {
        0 => new(M00, M01, M02),
        1 => new(M10, M11, M12),
        2 => new(M20, M21, M22),
        _ => throw new IndexOutOfRangeException()
    };

    /// <summary>Get column as vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetColumn(int i) => i switch
    {
        0 => new(M00, M10, M20),
        1 => new(M01, M11, M21),
        2 => new(M02, M12, M22),
        _ => throw new IndexOutOfRangeException()
    };

    /// <summary>Set row from vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetRow(int i, in PhysVector3 v)
    {
        switch (i)
        {
            case 0: M00 = v.X; M01 = v.Y; M02 = v.Z; break;
            case 1: M10 = v.X; M11 = v.Y; M12 = v.Z; break;
            case 2: M20 = v.X; M21 = v.Y; M22 = v.Z; break;
        }
    }

    // Matrix * Vector
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator *(in PhysMatrix33 m, in PhysVector3 v) => new(
        m.M00 * v.X + m.M01 * v.Y + m.M02 * v.Z,
        m.M10 * v.X + m.M11 * v.Y + m.M12 * v.Z,
        m.M20 * v.X + m.M21 * v.Y + m.M22 * v.Z
    );

    // Matrix * Matrix
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 operator *(in PhysMatrix33 a, in PhysMatrix33 b) => new(
        a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20,
        a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21,
        a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,

        a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20,
        a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21,
        a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,

        a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20,
        a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21,
        a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22
    );

    // Matrix + Matrix
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 operator +(in PhysMatrix33 a, in PhysMatrix33 b) => new(
        a.M00 + b.M00, a.M01 + b.M01, a.M02 + b.M02,
        a.M10 + b.M10, a.M11 + b.M11, a.M12 + b.M12,
        a.M20 + b.M20, a.M21 + b.M21, a.M22 + b.M22
    );

    // Matrix - Matrix
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 operator -(in PhysMatrix33 a, in PhysMatrix33 b) => new(
        a.M00 - b.M00, a.M01 - b.M01, a.M02 - b.M02,
        a.M10 - b.M10, a.M11 - b.M11, a.M12 - b.M12,
        a.M20 - b.M20, a.M21 - b.M21, a.M22 - b.M22
    );

    // Matrix * Scalar
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 operator *(in PhysMatrix33 m, float s) => new(
        m.M00 * s, m.M01 * s, m.M02 * s,
        m.M10 * s, m.M11 * s, m.M12 * s,
        m.M20 * s, m.M21 * s, m.M22 * s
    );

    /// <summary>Transpose (returns transposed copy).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysMatrix33 Transposed() => new(
        M00, M10, M20,
        M01, M11, M21,
        M02, M12, M22
    );

    // Patches added by CryAISystem.Sharp port — literal C++ Matrix33 method names from Cry_Math.h.

    /// <summary>Port of `Matrix33::GetColumn0()` — first column (x-axis).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetColumn0() => new(M00, M10, M20);

    /// <summary>Port of `Matrix33::GetColumn1()` — second column (y-axis).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetColumn1() => new(M01, M11, M21);

    /// <summary>Port of `Matrix33::GetColumn2()` — third column (z-axis).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 GetColumn2() => new(M02, M12, M22);

    /// <summary>Port of `Matrix33::SetIdentity()` — set this to identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetIdentity() => this = Identity;

    /// <summary>
    /// Port of `Matrix33::SetRotationVDir(Vec3 vdir, float roll)`.
    /// Creates a rotation matrix that maps (0,1,0) to vdir.
    /// </summary>
    public void SetRotationVDir(in PhysVector3 vdir, float roll = 0f)
    {
        // CryEngine Cry_Matrix33.h SetRotationVDir implementation
        float l = MathF.Sqrt(vdir.X * vdir.X + vdir.Y * vdir.Y);
        if (l > 1e-6f)
        {
            float xl = -vdir.X / l;
            float yl = vdir.Y / l;

            M00 = yl;                M01 = vdir.X;  M02 = xl * vdir.Z;
            M10 = xl;                M11 = vdir.Y;  M12 = -yl * vdir.Z;
            M20 = 0f;                M21 = vdir.Z;  M22 = l;
        }
        else
        {
            // vdir is nearly vertical
            float s = vdir.Z < 0f ? -1f : 1f;
            M00 = 1f; M01 = 0f; M02 = 0f;
            M10 = 0f; M11 = 0f; M12 = -s;
            M20 = 0f; M21 = s;  M22 = 0f;
        }

        if (MathF.Abs(roll) > 1e-6f)
        {
            float cr = MathF.Cos(roll);
            float sr = MathF.Sin(roll);
            // Rotate around vdir (column1) by roll
            float t00 = M00 * cr + M02 * sr;
            float t10 = M10 * cr + M12 * sr;
            float t20 = M20 * cr + M22 * sr;
            M02 = -M00 * sr + M02 * cr;
            M12 = -M10 * sr + M12 * cr;
            M22 = -M20 * sr + M22 * cr;
            M00 = t00; M10 = t10; M20 = t20;
        }
    }

    /// <summary>
    /// Port of `Matrix33::CreateRotationVDir(Vec3 vdir, float roll)`.
    /// </summary>
    public static PhysMatrix33 CreateRotationVDir(in PhysVector3 vdir, float roll = 0f)
    {
        var m = new PhysMatrix33();
        m.SetRotationVDir(vdir, roll);
        return m;
    }

    /// <summary>Port of `Matrix33::CreateRotationZ(float rad)` — rotation around Z axis.</summary>
    public static PhysMatrix33 CreateRotationZ(float rad)
    {
        float c = MathF.Cos(rad);
        float s = MathF.Sin(rad);
        return new PhysMatrix33(
            c, -s, 0,
            s,  c, 0,
            0,  0, 1
        );
    }

    /// <summary>Port of `Matrix33(const Matrix34&)` — construct from the 3x3 rotation part of a Matrix34.</summary>
    public PhysMatrix33(in CryPhysics.Math.PhysVector3 col0, in CryPhysics.Math.PhysVector3 col1, in CryPhysics.Math.PhysVector3 col2, bool columnMajor)
    {
        if (columnMajor)
        {
            M00 = col0.X; M01 = col1.X; M02 = col2.X;
            M10 = col0.Y; M11 = col1.Y; M12 = col2.Y;
            M20 = col0.Z; M21 = col1.Z; M22 = col2.Z;
        }
        else
        {
            M00 = col0.X; M01 = col0.Y; M02 = col0.Z;
            M10 = col1.X; M11 = col1.Y; M12 = col1.Z;
            M20 = col2.X; M21 = col2.Y; M22 = col2.Z;
        }
    }

    /// <summary>Port of `Matrix33::Transpose()` — in-place transpose.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Transpose()
    {
        float t;
        t = M01; M01 = M10; M10 = t;
        t = M02; M02 = M20; M20 = t;
        t = M12; M12 = M21; M21 = t;
    }

    /// <summary>Port of `Matrix33::GetTransposed()` — alias of `Transposed()`.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysMatrix33 GetTransposed() => Transposed();

    /// <summary>Port of `Matrix33::TransformVector(v)` — equivalent to operator*(this, v).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysVector3 TransformVector(in PhysVector3 v) => new(
        M00 * v.X + M01 * v.Y + M02 * v.Z,
        M10 * v.X + M11 * v.Y + M12 * v.Z,
        M20 * v.X + M21 * v.Y + M22 * v.Z
    );

    /// <summary>Port of `Matrix33::SetFromVectors(vx, vy, vz)` — set columns from three vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFromVectors(in PhysVector3 vx, in PhysVector3 vy, in PhysVector3 vz)
    {
        M00 = vx.X; M01 = vy.X; M02 = vz.X;
        M10 = vx.Y; M11 = vy.Y; M12 = vz.Y;
        M20 = vx.Z; M21 = vy.Z; M22 = vz.Z;
    }

    /// <summary>Determinant.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Determinant() =>
        M00 * (M11 * M22 - M12 * M21) -
        M01 * (M10 * M22 - M12 * M20) +
        M02 * (M10 * M21 - M11 * M20);

    /// <summary>Inverse (returns adjugate/det).</summary>
    public PhysMatrix33 Inverted()
    {
        float det = Determinant();
        if (MathF.Abs(det) < 1e-30f)
            return Identity;
        float rdet = 1f / det;
        return new PhysMatrix33(
            (M11 * M22 - M12 * M21) * rdet, (M02 * M21 - M01 * M22) * rdet, (M01 * M12 - M02 * M11) * rdet,
            (M12 * M20 - M10 * M22) * rdet, (M00 * M22 - M02 * M20) * rdet, (M02 * M10 - M00 * M12) * rdet,
            (M10 * M21 - M11 * M20) * rdet, (M01 * M20 - M00 * M21) * rdet, (M00 * M11 - M01 * M10) * rdet
        );
    }

    /// <summary>Creates a cross product matrix [v]x such that [v]x * u = v x u.</summary>
    public static PhysMatrix33 CrossProductMatrix(in PhysVector3 v) => new(
        0, -v.Z, v.Y,
        v.Z, 0, -v.X,
        -v.Y, v.X, 0
    );

    /// <summary>Create diagonal matrix from Diag33 values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 Diagonal(float d0, float d1, float d2) => new(
        d0, 0, 0,
        0, d1, 0,
        0, 0, d2
    );

    /// <summary>Create diagonal matrix from vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 Diagonal(in PhysVector3 d) => Diagonal(d.X, d.Y, d.Z);

    /// <summary>
    /// Get matrix from basis vectors (rows = basis vectors).
    /// Port of GetMtxFromBasis.
    /// </summary>
    public static PhysMatrix33 FromBasis(in PhysVector3 b0, in PhysVector3 b1, in PhysVector3 b2) => new(b0, b1, b2);

    /// <summary>
    /// Get transposed matrix from basis vectors (columns = basis vectors).
    /// Port of GetMtxFromBasisT.
    /// </summary>
    public static PhysMatrix33 FromBasisTransposed(in PhysVector3 b0, in PhysVector3 b1, in PhysVector3 b2) => new(
        b0.X, b1.X, b2.X,
        b0.Y, b1.Y, b2.Y,
        b0.Z, b1.Z, b2.Z
    );

    public override string ToString() =>
        $"[{M00:F4} {M01:F4} {M02:F4}]\n[{M10:F4} {M11:F4} {M12:F4}]\n[{M20:F4} {M21:F4} {M22:F4}]";
}

/// <summary>
/// Diagonal 3x3 matrix. Port of CryEngine's Diag33.
/// </summary>
public struct Diag33
{
    public float X, Y, Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Diag33(float x, float y, float z) { X = x; Y = y; Z = z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Diag33(in PhysVector3 v) { X = v.X; Y = v.Y; Z = v.Z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 operator *(in Diag33 d, in PhysVector3 v) => new(d.X * v.X, d.Y * v.Y, d.Z * v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysMatrix33 operator *(in Diag33 d, in PhysMatrix33 m) => new(
        d.X * m.M00, d.X * m.M01, d.X * m.M02,
        d.Y * m.M10, d.Y * m.M11, d.Y * m.M12,
        d.Z * m.M20, d.Z * m.M21, d.Z * m.M22
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Diag33 Inverted()
    {
        return new Diag33(
            MathF.Abs(X) > 1e-30f ? 1f / X : 0f,
            MathF.Abs(Y) > 1e-30f ? 1f / Y : 0f,
            MathF.Abs(Z) > 1e-30f ? 1f / Z : 0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PhysMatrix33(in Diag33 d) => PhysMatrix33.Diagonal(d.X, d.Y, d.Z);
}
