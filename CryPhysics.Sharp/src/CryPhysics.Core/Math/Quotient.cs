// Port of CryPhysics quotient.h - rational arithmetic for exact physics comparisons
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// Rational number x/y for exact comparisons without division.
/// WARNING: All comparisons assume y >= 0. Use FixSign() to ensure this.
/// Port of quotient_tpl from CryEngine.
/// </summary>
public struct Quotient<T> where T : struct
{
    public T X, Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quotient(T x, T y) { X = x; Y = y; }
}

/// <summary>
/// Float specialization of Quotient with full operator support.
/// </summary>
public struct QuotientF : IComparable<QuotientF>
{
    public float X, Y;

    public static readonly QuotientF MinValue = new(-1f, 1e-20f);
    public static readonly QuotientF MaxValue = new(1f, 1e-20f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuotientF(float x, float y = 1f) { X = x; Y = y; }

    /// <summary>Ensure Y >= 0 by flipping signs if needed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuotientF FixSign()
    {
        int sgny = MathUtils.SgnNZ(Y);
        return new QuotientF(X * sgny, Y * sgny);
    }

    /// <summary>Evaluate the quotient as a float value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Val() => Y != 0f ? X / Y : 0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuotientF Set(float nx, float ny) { X = nx; Y = ny; return this; }

    // Unary
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator -(in QuotientF q) => new(-q.X, q.Y);

    // Quotient op scalar
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator *(in QuotientF q, float op) => new(q.X * op, q.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator /(in QuotientF q, float op) => new(q.X, q.Y * op);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator +(in QuotientF q, float op) => new(q.X + q.Y * op, q.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator -(in QuotientF q, float op) => new(q.X - q.Y * op, q.Y);

    // Scalar op quotient
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator *(float op, in QuotientF q) => new(q.X * op, q.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator +(float op, in QuotientF q) => new(op * q.Y + q.X, q.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator -(float op, in QuotientF q) => new(op * q.Y - q.X, q.Y);

    // Quotient op quotient
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator *(in QuotientF a, in QuotientF b) => new(a.X * b.X, a.Y * b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator /(in QuotientF a, in QuotientF b) => new(a.X * b.Y, a.Y * b.X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator +(in QuotientF a, in QuotientF b) => new(a.X * b.Y + b.X * a.Y, a.Y * b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF operator -(in QuotientF a, in QuotientF b) => new(a.X * b.Y - b.X * a.Y, a.Y * b.Y);

    // Comparison with scalar (assumes Y >= 0)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in QuotientF q, float op) => q.X == op * q.Y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in QuotientF q, float op) => q.X != op * q.Y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(in QuotientF q, float op) => q.X - op * q.Y < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(in QuotientF q, float op) => q.X - op * q.Y > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(in QuotientF q, float op) => q.X - op * q.Y <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(in QuotientF q, float op) => q.X - op * q.Y >= 0;

    // Comparison with quotient (includes epsilon tie-breaking like original)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(in QuotientF a, in QuotientF b) =>
        a.X * b.Y - b.X * a.Y + 1e-20f * (a.X - b.X) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(in QuotientF a, in QuotientF b) =>
        a.X * b.Y - b.X * a.Y + 1e-20f * (a.X - b.X) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(in QuotientF a, in QuotientF b) =>
        a.X * b.Y - b.X * a.Y + 1e-20f * (a.X - b.X) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(in QuotientF a, in QuotientF b) =>
        a.X * b.Y - b.X * a.Y + 1e-20f * (a.X - b.X) >= 0;

    // Sign queries
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Sgn() => MathUtils.Sgn(X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SgnNZ() => MathUtils.SgnNZ(X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IsNeg() => MathUtils.IsNeg(X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IsNonNeg() => MathUtils.IsNonNeg(X);

    /// <summary>Returns 1 if value is in (0,1) range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IsIn01() => MathUtils.IsNeg(MathF.Abs(X * 2 - Y) - MathF.Abs(Y));

    public static QuotientF Max(in QuotientF a, in QuotientF b) => a > b ? a : b;
    public static QuotientF Min(in QuotientF a, in QuotientF b) => a < b ? a : b;

    public int CompareTo(QuotientF other) => (X * other.Y - other.X * Y).CompareTo(0);
    public override bool Equals(object? obj) => obj is QuotientF q && X == q.X && Y == q.Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"{X}/{Y} (={Val():F6})";
}

/// <summary>
/// Double specialization of Quotient.
/// </summary>
public struct QuotientD : IComparable<QuotientD>
{
    public double X, Y;

    public static readonly QuotientD MinValue = new(-1.0, 1e-80);
    public static readonly QuotientD MaxValue = new(1.0, 1e-80);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuotientD(double x, double y = 1.0) { X = x; Y = y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuotientD FixSign()
    {
        double sgny = System.Math.Sign(Y) >= 0 ? 1.0 : -1.0;
        return new QuotientD(X * sgny, Y * sgny);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Val() => Y != 0.0 ? X / Y : 0.0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientD operator *(in QuotientD a, in QuotientD b) => new(a.X * b.X, a.Y * b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientD operator /(in QuotientD a, in QuotientD b) => new(a.X * b.Y, a.Y * b.X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientD operator +(in QuotientD a, in QuotientD b) => new(a.X * b.Y + b.X * a.Y, a.Y * b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientD operator -(in QuotientD a, in QuotientD b) => new(a.X * b.Y - b.X * a.Y, a.Y * b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(in QuotientD a, in QuotientD b) =>
        a.X * b.Y - b.X * a.Y + 1e-80 * (a.X - b.X) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(in QuotientD a, in QuotientD b) =>
        a.X * b.Y - b.X * a.Y + 1e-80 * (a.X - b.X) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(in QuotientD a, in QuotientD b) =>
        a.X * b.Y - b.X * a.Y + 1e-80 * (a.X - b.X) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(in QuotientD a, in QuotientD b) =>
        a.X * b.Y - b.X * a.Y + 1e-80 * (a.X - b.X) >= 0;

    public int CompareTo(QuotientD other) => (X * other.Y - other.X * Y).CompareTo(0);
    public override bool Equals(object? obj) => obj is QuotientD q && X == q.X && Y == q.Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"{X}/{Y} (={Val():F6})";
}

/// <summary>
/// Integer specialization of Quotient.
/// </summary>
public struct QuotientI
{
    public int X, Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuotientI(int x, int y = 1) { X = x; Y = y; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Val() => Y != 0 ? (float)X / Y : 0f;
}
