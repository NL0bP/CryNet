// Literal port of dev/Code/CryEngine/CryCommon/FixedPoint.h
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// fixed_t<int, 16> is the only instantiation used by MNM (as real_t).
// C# cannot do templates on value types with integer params, so we port
// the concrete instantiation fixed_t<int, 16> as struct real_t.
//
// BaseType = int (32-bit signed)
// IntegerBitCount = 16
// bit_size = 32
// is_signed = 1
// fractional_bitcount = 32 - 16 - 1 = 15
// integer_scale = 1 << 15 = 32768
// half_unit = 16384

using System;
using System.Runtime.CompilerServices;

namespace CryAISystem.Navigation.MNM;

/// <summary>
/// Literal port of fixed_t&lt;int, 16&gt; from CryCommon/FixedPoint.h.
/// This is the MNM::real_t type used throughout the navigation mesh system.
/// </summary>
public struct real_t : IComparable<real_t>, IEquatable<real_t>
{
    // Template constants for fixed_t<int, 16>
    public const int bit_size = 32;
    public const int is_signed = 1;
    public const int integer_bitcount = 16;
    public const int fractional_bitcount = bit_size - integer_bitcount - is_signed; // 15
    public const int integer_scale = 1 << fractional_bitcount; // 32768
    public const int half_unit = integer_scale >> 1; // 16384

    public int v;

    // Default parameterless ctor: v = 0 (C# default)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t(real_t other) { v = other.v; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t(int value) { v = value << fractional_bitcount; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t(uint value) { v = (int)(value << fractional_bitcount); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t(float value) { v = (int)(value * integer_scale + (value >= 0.0f ? 0.5f : -0.5f)); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t(double value) { v = (int)(value * integer_scale + (value >= 0.0 ? 0.5 : -0.5)); }

    // Negation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t operator -(real_t x) { real_t r; r.v = -x.v; return r; }

    // Arithmetic
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t operator +(real_t a, real_t b) { real_t r; r.v = a.v + b.v; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t operator -(real_t a, real_t b) { real_t r; r.v = a.v - b.v; return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t operator *(real_t a, real_t b)
    {
        // Truncate rounding: if negative, add (integer_scale - 1) before shift
        long product = (long)a.v * b.v;
        if (product < 0)
            product += integer_scale - 1;
        product >>= fractional_bitcount;
        real_t r; r.v = (int)product; return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t operator /(real_t a, real_t b)
    {
        long quotient = ((long)a.v << fractional_bitcount) / b.v;
        real_t r; r.v = (int)quotient; return r;
    }

    // Comparison
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(real_t a, real_t b) => a.v < b.v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(real_t a, real_t b) => a.v > b.v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(real_t a, real_t b) => a.v <= b.v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(real_t a, real_t b) => a.v >= b.v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(real_t a, real_t b) => a.v == b.v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(real_t a, real_t b) => a.v != b.v;

    // Implicit conversion from int
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator real_t(int value) => new real_t(value);

    // sqr() — returns unsigned_overflow_type (ulong)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong sqr()
    {
        return (ulong)(((long)v * v) >> fractional_bitcount);
    }

    // Conversions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float as_float() => v / (float)integer_scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double as_double() => v / (double)integer_scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int as_int() => v >> fractional_bitcount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint as_uint() => (uint)(v >> fractional_bitcount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int get() => v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void set(int value) { v = value; }

    // Static factories
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t max()
    {
        real_t r; r.v = int.MaxValue; return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t min()
    {
        real_t r; r.v = int.MinValue; return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t epsilon()
    {
        real_t r; r.v = 1; return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t fraction(int num, int denom)
    {
        real_t r;
        r.v = (int)(((long)num << (fractional_bitcount + fractional_bitcount))
            / ((long)denom << fractional_bitcount));
        return r;
    }

    // sqrtf for unsigned_overflow_type (ulong)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t sqrtf(ulong x)
    {
        ulong root = 0;
        ulong remHi = 0;
        ulong remLo = x << (fractional_bitcount & 1);
        // bit_size of unsigned_overflow_type = 64
        const int uot_bit_size = 64;
        long count = (uot_bit_size >> 1) + (fractional_bitcount >> 1) - ((fractional_bitcount + 1) & 1);

        do
        {
            remHi = (remHi << 2) | (remLo >> (uot_bit_size - 2));
            remLo <<= 2;
            root <<= 1;
            ulong div = (root << 1) + 1;
            if (remHi >= div)
            {
                remHi -= div;
                root += 1;
            }
        } while (count-- > 0);

        real_t r;
        r.v = (int)(root >> (fractional_bitcount & 1));
        return r;
    }

    // IComparable / IEquatable
    public int CompareTo(real_t other) => v.CompareTo(other.v);
    public bool Equals(real_t other) => v == other.v;
    public override bool Equals(object obj) => obj is real_t rt && rt.v == v;
    public override int GetHashCode() => v;
    public override string ToString() => as_float().ToString("F4");
}

// Free functions matching the C++ free function templates instantiated for real_t

public static class FixedPointMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t fabsf(real_t x)
    {
        int mask = x.v >> (real_t.bit_size - 1);
        real_t r; r.v = (x.v + mask) ^ mask; return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t sqrtf(real_t x)
    {
        // Uses overflow_type (long) based algorithm
        long root = 0;
        long remHi = 0;
        long remLo = (long)x.v << (real_t.fractional_bitcount & 1);
        long count = (real_t.bit_size >> 1) + (real_t.fractional_bitcount >> 1)
            - ((real_t.fractional_bitcount + 1) & 1);

        do
        {
            remHi = (remHi << 2) | ((remLo >> (real_t.bit_size - 2)) & 3);
            remLo <<= 2;
            remLo &= ((long)1 << real_t.bit_size) - 1;
            root <<= 1;
            root &= ((long)1 << real_t.bit_size) - 1;
            long div = (root << 1) + 1;
            if (remHi >= div)
            {
                remHi -= div;
                remHi &= ((long)1 << real_t.bit_size) - 1;
                root += 1;
            }
        } while (count-- > 0);

        real_t r;
        r.v = (int)(root >> (real_t.fractional_bitcount & 1));
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t fmodf(real_t x, real_t y)
    {
        real_t r; r.v = x.v % y.v; return r;
    }

    // min / max for real_t
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t min(real_t a, real_t b) => a.v < b.v ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static real_t max(real_t a, real_t b) => a.v > b.v ? a : b;
}

// Also provide a concrete type for fixed_t<uint16, 5> used by Tile::Vertex
// BaseType = ushort (16-bit unsigned)
// IntegerBitCount = 5
// bit_size = 16
// is_signed = 0
// fractional_bitcount = 16 - 5 - 0 = 11
// integer_scale = 1 << 11 = 2048
public struct fixed_t_u16_5 : IComparable<fixed_t_u16_5>, IEquatable<fixed_t_u16_5>
{
    public const int bit_size = 16;
    public const int is_signed = 0;
    public const int integer_bitcount = 5;
    public const int fractional_bitcount = bit_size - integer_bitcount; // 11
    public const int integer_scale = 1 << fractional_bitcount; // 2048

    public ushort v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public fixed_t_u16_5(ushort value) { v = (ushort)(value << fractional_bitcount); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public fixed_t_u16_5(float value) { v = (ushort)(value * integer_scale + 0.5f); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float as_float() => v / (float)integer_scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int get() => v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void set(int value) { v = (ushort)value; }

    // Convert to real_t (fractional_bitcount 11 -> 15, shift left by 4)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public real_t ToReal()
    {
        real_t r;
        r.v = (int)v << (real_t.fractional_bitcount - fractional_bitcount);
        return r;
    }

    public int CompareTo(fixed_t_u16_5 other) => v.CompareTo(other.v);
    public bool Equals(fixed_t_u16_5 other) => v == other.v;
    public override bool Equals(object obj) => obj is fixed_t_u16_5 f && f.v == v;
    public override int GetHashCode() => v;
}
