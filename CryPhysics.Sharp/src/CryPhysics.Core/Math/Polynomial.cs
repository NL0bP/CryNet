// Port of CryPhysics polynomial.h - polynomial root finding
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// Polynomial of a given maximum degree with root-finding capabilities.
/// Port of polynomial_tpl from CryEngine.
/// Coefficients stored as data[0] + data[1]*x + data[2]*x^2 + ... + data[degree]*x^degree.
/// </summary>
public struct Polynomial
{
    public const int MaxDegree = 8;
    private const float PolyEpsilonF = 1e-6f;
    private const double PolyEpsilonD = 1e-10;

    // Fixed-size buffer for coefficients (up to degree 8)
    public float C0, C1, C2, C3, C4, C5, C6, C7, C8;
    public float Denom;
    public int Degree;

    public Polynomial(int degree)
    {
        this = default;
        Degree = degree;
        Denom = 1f;
    }

    /// <summary>Create a monomial with leading coefficient. Port of polynomial_tpl(ftype op): data[degree]=op.</summary>
    public Polynomial(int degree, float leadingCoeff) : this(degree)
    {
        this[degree] = leadingCoeff;
    }

    public float this[int idx]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => idx switch
        {
            0 => C0, 1 => C1, 2 => C2, 3 => C3,
            4 => C4, 5 => C5, 6 => C6, 7 => C7, 8 => C8,
            _ => 0f
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (idx)
            {
                case 0: C0 = value; break; case 1: C1 = value; break;
                case 2: C2 = value; break; case 3: C3 = value; break;
                case 4: C4 = value; break; case 5: C5 = value; break;
                case 6: C6 = value; break; case 7: C7 = value; break;
                case 8: C8 = value; break;
            }
        }
    }

    /// <summary>Zero all coefficients.</summary>
    public void Zero()
    {
        C0 = C1 = C2 = C3 = C4 = C5 = C6 = C7 = C8 = 0;
        Denom = 1f;
    }

    /// <summary>Set coefficients from array (highest degree first, like CryEngine's set()).</summary>
    public void SetFromHighToLow(ReadOnlySpan<float> coeffs)
    {
        for (int i = 0; i <= Degree && i < coeffs.Length; i++)
            this[Degree - i] = coeffs[i];
    }

    /// <summary>Evaluate polynomial at x.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Eval(float x)
    {
        float res = 0;
        for (int i = Degree; i >= 0; i--)
            res = res * x + this[i];
        return res;
    }

    /// <summary>Evaluate polynomial at x up to a given sub-degree.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Eval(float x, int subDegree)
    {
        float res = this[subDegree];
        for (int i = subDegree - 1; i >= 0; i--)
            res = res * x + this[i];
        return res;
    }

    /// <summary>Compute derivative polynomial.</summary>
    public Polynomial Derivative()
    {
        var deriv = new Polynomial(System.Math.Max(Degree - 1, 0));
        deriv.Denom = Denom;
        for (int i = 0; i < Degree; i++)
            deriv[i] = this[i + 1] * (i + 1);
        return deriv;
    }

    /// <summary>Ensure leading coefficient sign matches denom sign.</summary>
    public void FixSign()
    {
        int sg = MathUtils.SgnNZ(Denom);
        Denom *= sg;
        for (int i = 0; i <= Degree; i++)
            this[i] *= sg;
    }

    /// <summary>
    /// Find real roots of the polynomial in [start, end].
    /// Returns number of roots found. Roots stored in proots[].
    /// Port of polynomial_tpl::findroots.
    /// </summary>
    public int FindRoots(float start, float end, Span<float> proots, int nIters = 20)
    {
        return FindRoots(start, end, proots, nIters, Degree, false);
    }

    public int FindRoots(float start, float end, Span<float> proots, int nIters, int degree, bool noDegreeCheck)
    {
        int nRoots = 0;

        if (!noDegreeCheck)
        {
            float maxEl = MathF.Abs(this[0]);
            for (int i = 1; i <= degree; i++)
                maxEl = MathUtils.MaxSafe(maxEl, MathF.Abs(this[i]));
            float thr = maxEl * PolyEpsilonF;
            while (degree > 0 && MathF.Abs(this[degree]) <= thr)
                degree--;
        }

        if (degree == 1)
        {
            proots[0] = this[0] / this[1];
            nRoots = 1;
        }
        else if (degree == 2)
        {
            float a = this[2], b = this[1], c = this[0];
            float d = MathUtils.SgnNZ(a);
            a *= d; b *= d; c *= d;
            d = b * b - a * c * 4;

            float bound0 = start * a * 2 + b;
            float bound1 = end * a * 2 + b;
            int sg = (MathUtils.SgnNZ(bound0 * bound1) + 1) >> 1;
            float ab0 = bound0 * bound0, ab1 = bound1 * bound1;
            if (MathF.Abs(ab1) < MathF.Abs(ab0))
                ab1 *= sg;
            else
                ab0 *= sg;

            if (MathUtils.IsNonNeg(d) != 0 && d >= MathUtils.MinSafe(ab0, ab1) && d <= MathUtils.MaxSafe(ab0, ab1))
            {
                d = MathF.Sqrt(d);
                a = 0.5f / a;
                float r0 = (-b - d) * a;
                if (r0 >= start && r0 <= end) proots[nRoots++] = r0;
                float r1 = (-b + d) * a;
                if (r1 >= start && r1 <= end) proots[nRoots++] = r1;
            }
        }
        else if (degree == 3)
        {
            float t = 1f / this[3];
            float a = this[2] * t, b = this[1] * t, c = this[0] * t;
            float a3 = a * (1f / 3f);
            float p = b - a * a3;
            float q = (a3 * b - c) * 0.5f - MathUtils.Cube(a3);
            float Q = MathUtils.Cube(p * (1f / 3f)) + q * q;
            float Qr = MathF.Sqrt(MathF.Abs(Q));

            if (Q > 0)
            {
                proots[0] = MathUtils.Cubert(q + Qr) + MathUtils.Cubert(q - Qr) - a3;
                nRoots = 1;
            }
            else
            {
                float phi = MathF.Atan2(Qr, q) * (1f / 3f);
                t = MathF.Pow(Qr * Qr + q * q, 1f / 6f);
                float Ar = t * MathF.Cos(phi), Ai = t * MathF.Sin(phi);
                proots[0] = 2 * Ar - a3;
                proots[1] = -Ar + Ai * MathUtils.Sqrt3 - a3;
                proots[2] = -Ar - Ai * MathUtils.Sqrt3 - a3;
                // Sort: port of C++ idxmax3+swap pattern
                // Move maximum of [0..2] to position 2
                int idx = MathUtils.IdxMax3(proots);
                (proots[idx], proots[2]) = (proots[2], proots[idx]);
                // Sort first two: swap if proots[0] > proots[1]
                // C++ uses: i = isneg(proots[0]-proots[1]); swap(proots,i,1)
                // isneg returns 1 when arg<0, i.e. when proots[0]<proots[1] => swap(1,1)=nop
                // isneg returns 0 when arg>=0, i.e. when proots[0]>=proots[1] => swap(0,1)
                if (proots[0] > proots[1])
                    (proots[0], proots[1]) = (proots[1], proots[0]);
                nRoots = 3;
            }
        }
        else if (degree == 4)
        {
            float t = 1f / this[4];
            float a3 = this[3] * t, a2 = this[2] * t, a1 = this[1] * t, a0 = this[0] * t;
            const float e = 1e-9f;

            // Resolvent cubic
            var p3 = new Polynomial(3);
            // Coefficients from high to low: 1, -a2, a1*a3-4*a0, 4*a2*a0-a1*a1-a3*a3*a0
            p3[3] = 1;
            p3[2] = -a2;
            p3[1] = a1 * a3 - 4 * a0;
            p3[0] = 4 * a2 * a0 - a1 * a1 - a3 * a3 * a0;

            Span<float> subRoots = stackalloc float[3];
            if (p3.FindRoots(-1e20f, 1e20f, subRoots, 20, 3, false) == 0)
                return 0;

            float y = subRoots[0];
            float R = a3 * a3 * 0.25f - a2 + y;

            if (R > -e)
            {
                float D, E;
                if (R < e)
                {
                    D = E = a3 * a3 * 0.75f - 2 * a2;
                    t = y * y - 4 * a0;
                    if (t < -e) return 0;
                    t = 2 * MathF.Sqrt(MathF.Max(0, t));
                }
                else
                {
                    R = MathF.Sqrt(MathF.Max(0, R));
                    D = E = a3 * a3 * 0.75f - R * R - 2 * a2;
                    t = (4 * a3 * a2 - 8 * a1 - a3 * a3 * a3) / R * 0.25f;
                }

                if (D + t > -e)
                {
                    D = MathF.Sqrt(MathF.Max(0, D + t));
                    proots[nRoots++] = a3 * -0.25f + (R - D) * 0.5f;
                    proots[nRoots++] = a3 * -0.25f + (R + D) * 0.5f;
                }
                if (E - t > -e)
                {
                    E = MathF.Sqrt(MathF.Max(0, E - t));
                    proots[nRoots++] = a3 * -0.25f - (R + E) * 0.5f;
                    proots[nRoots++] = a3 * -0.25f - (R - E) * 0.5f;
                }

                // Sort roots
                if (nRoots == 4)
                {
                    Span<float> tmp = proots.Slice(0, 4);
                    tmp.Sort();
                }
            }
        }
        else if (degree > 4)
        {
            // General case: find derivative roots, then bisect
            var deriv = Derivative();
            Span<float> roots = stackalloc float[degree + 2];
            int nExtremes = deriv.FindRoots(start, end, roots.Slice(1), nIters, degree - 1, false) + 1;

            // Trim to range
            while (nExtremes > 1 && roots[nExtremes - 1] > end) nExtremes--;
            int iStart = 1;
            while (iStart < nExtremes && roots[iStart] < start) iStart++;
            roots[iStart - 1] = start;
            roots[nExtremes++] = end;

            float prevVal = Eval(start, degree);
            float prevRoot = start;

            for (int i = iStart; i < nExtremes; i++)
            {
                float val = Eval(roots[i], degree);
                if (val * prevVal < 0)
                {
                    // Bisection
                    float lo = prevRoot, hi = roots[i];
                    float loVal = prevVal;
                    for (int iter = 0; iter < nIters; iter++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        float midVal = Eval(mid, degree);
                        if (loVal * midVal < 0)
                            hi = mid;
                        else
                        {
                            lo = mid;
                            loVal = midVal;
                        }
                    }
                    proots[nRoots++] = (lo + hi) * 0.5f;
                }
                prevVal = val;
                prevRoot = roots[i];
            }
        }

        // Clamp roots to [start, end]
        int first = 0;
        while (first < nRoots && proots[first] < start) first++;
        while (nRoots > first && proots[nRoots - 1] > end) nRoots--;
        if (first > 0)
            for (int j = first; j < nRoots; j++)
                proots[j - first] = proots[j];

        return nRoots - first;
    }

    /// <summary>Polynomial addition.</summary>
    public static Polynomial Add(in Polynomial a, in Polynomial b)
    {
        int maxDeg = System.Math.Max(a.Degree, b.Degree);
        var res = new Polynomial(maxDeg);
        int minDeg = System.Math.Min(a.Degree, b.Degree);
        for (int i = 0; i <= minDeg; i++)
            res[i] = a[i] * b.Denom + b[i] * a.Denom;
        for (int i = minDeg + 1; i <= a.Degree; i++)
            res[i] = a[i] * b.Denom;
        for (int i = minDeg + 1; i <= b.Degree; i++)
            res[i] = b[i] * a.Denom;
        res.Denom = a.Denom * b.Denom;
        return res;
    }

    /// <summary>Polynomial subtraction.</summary>
    public static Polynomial Subtract(in Polynomial a, in Polynomial b)
    {
        int maxDeg = System.Math.Max(a.Degree, b.Degree);
        var res = new Polynomial(maxDeg);
        int minDeg = System.Math.Min(a.Degree, b.Degree);
        for (int i = 0; i <= minDeg; i++)
            res[i] = a[i] * b.Denom - b[i] * a.Denom;
        for (int i = minDeg + 1; i <= a.Degree; i++)
            res[i] = a[i] * b.Denom;
        for (int i = minDeg + 1; i <= b.Degree; i++)
            res[i] = -b[i] * a.Denom;
        res.Denom = a.Denom * b.Denom;
        return res;
    }

    /// <summary>Polynomial multiplication.</summary>
    public static Polynomial Multiply(in Polynomial a, in Polynomial b)
    {
        var res = new Polynomial(a.Degree + b.Degree);
        res.Zero();
        for (int i = 0; i <= a.Degree; i++)
            for (int j = 0; j <= b.Degree; j++)
                res[i + j] += a[i] * b[j];
        res.Denom = a.Denom * b.Denom;
        return res;
    }

    /// <summary>Scalar multiply.</summary>
    public static Polynomial operator *(in Polynomial p, float s)
    {
        var res = p;
        for (int i = 0; i <= res.Degree; i++)
            res[i] *= s;
        return res;
    }

    /// <summary>Scalar divide.</summary>
    public static Polynomial operator /(in Polynomial p, float s)
    {
        var res = p;
        res.Denom *= s;
        return res;
    }

    public override string ToString()
    {
        var terms = new System.Collections.Generic.List<string>();
        for (int i = Degree; i >= 0; i--)
        {
            float c = this[i];
            if (MathF.Abs(c) < 1e-30f) continue;
            string term = i switch
            {
                0 => $"{c:F4}",
                1 => $"{c:F4}x",
                _ => $"{c:F4}x^{i}"
            };
            terms.Add(term);
        }
        return terms.Count > 0 ? string.Join(" + ", terms) : "0";
    }
}
