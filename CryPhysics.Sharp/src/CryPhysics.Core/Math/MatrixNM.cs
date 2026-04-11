// Port of CryPhysics matrixnm.h - NxM matrix with pool allocation
// Original: Copyright Crytek GMBH, used under license

using System.Buffers;
using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// Matrix flags matching CryEngine's mtxflags enum.
/// </summary>
[Flags]
public enum MatrixFlags
{
    None = 0,
    Invalid = 1,
    Normal = 2,
    Orthogonal = 4,
    PSD = 8,             // Positive Semi-Definite
    PDFlag = 16,
    PD = PSD | PDFlag,   // Positive Definite
    Symmetric = 32,
    DiagonalFlag = 64,
    Diagonal = Symmetric | Normal | DiagonalFlag,
    IdentityFlag = 128,
    Identity = PD | Diagonal | Orthogonal | Normal | Symmetric | IdentityFlag,
    Singular = 256,
    ForeignData = 1024,  // Data not owned by this matrix
    Allocate = 32768     // Force heap allocation (no pool)
}

/// <summary>
/// Dynamic NxM matrix for physics calculations (LU decomposition, solvers, etc.).
/// Port of matrix_tpl from CryEngine. Uses ArrayPool for small matrices.
/// </summary>
public sealed class MatrixNM : IDisposable
{
    private const int PoolThreshold = 36; // Matches CryEngine's pool threshold

    public int Rows { get; private set; }
    public int Cols { get; private set; }
    public MatrixFlags Flags { get; set; }
    public float[] Data { get; private set; }

    private bool _ownsData;

    public MatrixNM(int rows, int cols, MatrixFlags flags = MatrixFlags.None, float[]? data = null)
    {
        Rows = rows;
        Cols = cols;
        Flags = flags;

        if (data != null)
        {
            Data = data;
            _ownsData = false;
            Flags |= MatrixFlags.ForeignData;
        }
        else
        {
            int sz = rows * cols;
            Data = ArrayPool<float>.Shared.Rent(System.Math.Max(sz, 1));
            _ownsData = true;
        }
    }

    public void Dispose()
    {
        if (_ownsData && Data != null)
        {
            ArrayPool<float>.Shared.Return(Data);
            _ownsData = false;
        }
    }

    /// <summary>Access element by row,col.</summary>
    public float this[int row, int col]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Data[row * Cols + col];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Data[row * Cols + col] = value;
    }

    /// <summary>Access row as a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Row(int row) => Data.AsSpan(row * Cols, Cols);

    /// <summary>Zero all elements.</summary>
    public MatrixNM Zero()
    {
        Array.Clear(Data, 0, Rows * Cols);
        return this;
    }

    /// <summary>Set to identity matrix.</summary>
    public MatrixNM SetIdentity()
    {
        Zero();
        int n = System.Math.Min(Rows, Cols);
        for (int i = 0; i < n; i++)
            Data[i * (Cols + 1)] = 1f;
        return this;
    }

    /// <summary>Transpose in place (square matrices only for in-place).</summary>
    public MatrixNM Transpose()
    {
        if (Rows == Cols)
        {
            if ((Flags & MatrixFlags.Symmetric) == 0)
            {
                for (int i = 0; i < Rows; i++)
                    for (int j = 0; j < i; j++)
                    {
                        (Data[i * Cols + j], Data[j * Cols + i]) = (Data[j * Cols + i], Data[i * Cols + j]);
                    }
            }
        }
        else
        {
            var result = Transposed();
            Data = result.Data;
            (Rows, Cols) = (Cols, Rows);
        }
        return this;
    }

    /// <summary>Returns a new transposed matrix.</summary>
    public MatrixNM Transposed()
    {
        if ((Flags & MatrixFlags.Symmetric) != 0)
            return Clone();

        var res = new MatrixNM(Cols, Rows);
        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
                res[j, i] = this[i, j];
        return res;
    }

    /// <summary>Clone this matrix.</summary>
    public MatrixNM Clone()
    {
        var res = new MatrixNM(Rows, Cols, Flags & ~MatrixFlags.ForeignData);
        Array.Copy(Data, res.Data, Rows * Cols);
        return res;
    }

    /// <summary>
    /// LU decomposition with partial pivoting.
    /// Returns the number of row swaps (for determinant sign).
    /// </summary>
    public int LUDecomposition(float[] luData, int[] luIdx)
    {
        int n = Rows;
        Array.Copy(Data, luData, n * n);
        for (int i = 0; i < n; i++) luIdx[i] = i;

        int swaps = 0;
        for (int k = 0; k < n - 1; k++)
        {
            // Find pivot
            float maxVal = MathF.Abs(luData[k * n + k]);
            int maxRow = k;
            for (int i = k + 1; i < n; i++)
            {
                float val = MathF.Abs(luData[i * n + k]);
                if (val > maxVal)
                {
                    maxVal = val;
                    maxRow = i;
                }
            }

            if (maxRow != k)
            {
                // Swap rows
                for (int j = 0; j < n; j++)
                    (luData[k * n + j], luData[maxRow * n + j]) = (luData[maxRow * n + j], luData[k * n + j]);
                (luIdx[k], luIdx[maxRow]) = (luIdx[maxRow], luIdx[k]);
                swaps++;
            }

            float pivot = luData[k * n + k];
            if (MathF.Abs(pivot) < 1e-30f)
                continue;

            float rpivot = 1f / pivot;
            for (int i = k + 1; i < n; i++)
            {
                float factor = luData[i * n + k] * rpivot;
                luData[i * n + k] = factor;
                for (int j = k + 1; j < n; j++)
                    luData[i * n + j] -= factor * luData[k * n + j];
            }
        }

        return swaps;
    }

    /// <summary>Solve Ax = b using LU decomposition.</summary>
    public bool SolveAxB(float[] x, float[] b, float[]? luData = null, int[]? luIdx = null)
    {
        int n = Rows;
        bool ownLU = luData == null;
        luData ??= new float[n * n];
        luIdx ??= new int[n];

        if (ownLU)
            LUDecomposition(luData, luIdx);

        // Forward substitution (Ly = Pb)
        float[] y = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = b[luIdx[i]];
            for (int j = 0; j < i; j++)
                sum -= luData[i * n + j] * y[j];
            y[i] = sum;
        }

        // Back substitution (Ux = y)
        for (int i = n - 1; i >= 0; i--)
        {
            float sum = y[i];
            for (int j = i + 1; j < n; j++)
                sum -= luData[i * n + j] * x[j];
            float diag = luData[i * n + i];
            if (MathF.Abs(diag) < 1e-30f)
                return false;
            x[i] = sum / diag;
        }

        return true;
    }

    /// <summary>In-place matrix inversion via LU decomposition.</summary>
    public MatrixNM Invert()
    {
        if ((Flags & MatrixFlags.Orthogonal) != 0)
        {
            Transpose();
            return this;
        }

        int n = Rows;
        float[] luData = new float[n * n];
        int[] luIdx = new int[n];
        LUDecomposition(luData, luIdx);

        float[] col = new float[n];
        float[] result = new float[n * n];
        float[] b = new float[n];

        for (int j = 0; j < n; j++)
        {
            Array.Clear(b, 0, n);
            b[j] = 1f;
            SolveAxB(col, b, luData, luIdx);
            for (int i = 0; i < n; i++)
                result[i * n + j] = col[i];
        }

        Array.Copy(result, Data, n * n);
        return this;
    }

    /// <summary>Determinant via LU decomposition.</summary>
    public float Determinant()
    {
        int n = Rows;
        float[] luData = new float[n * n];
        int[] luIdx = new int[n];
        int swaps = LUDecomposition(luData, luIdx);

        float det = (swaps % 2 == 0) ? 1f : -1f;
        for (int i = 0; i < n; i++)
            det *= luData[i * n + i];
        return det;
    }

    /// <summary>Matrix multiplication: result = this * other.</summary>
    public MatrixNM Multiply(MatrixNM other)
    {
        var result = new MatrixNM(Rows, other.Cols);
        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < other.Cols; j++)
            {
                float sum = 0;
                for (int k = 0; k < Cols; k++)
                    sum += this[i, k] * other[k, j];
                result[i, j] = sum;
            }
        return result;
    }

    /// <summary>Add: this += other.</summary>
    public void Add(MatrixNM other)
    {
        int sz = Rows * Cols;
        for (int i = 0; i < sz; i++)
            Data[i] += other.Data[i];
    }

    /// <summary>Subtract: this -= other.</summary>
    public void Subtract(MatrixNM other)
    {
        int sz = Rows * Cols;
        for (int i = 0; i < sz; i++)
            Data[i] -= other.Data[i];
    }

    /// <summary>Scale: this *= scalar.</summary>
    public void Scale(float s)
    {
        int sz = Rows * Cols;
        for (int i = 0; i < sz; i++)
            Data[i] *= s;
    }

    /// <summary>Multiply matrix by vector: result[i] = sum_j(this[i,j] * vec[j]).</summary>
    public void MultiplyVector(ReadOnlySpan<float> vec, Span<float> result)
    {
        for (int i = 0; i < Rows; i++)
        {
            float sum = 0;
            for (int j = 0; j < Cols; j++)
                sum += Data[i * Cols + j] * vec[j];
            result[i] = sum;
        }
    }

    /// <summary>Multiply matrix by float array: result[i] = sum_j(this[i,j] * vec[j]).</summary>
    public void MultiplyVector(float[] vec, Span<float> result)
    {
        MultiplyVector(vec.AsSpan(), result);
    }

    /// <summary>
    /// Conjugate gradient solver for symmetric positive-definite matrices.
    /// Solves Ax = b where A is this matrix, startX is the initial guess (modified in place),
    /// and rightSide is b. Port of conjugate_gradient from matrixnm.cpp.
    /// Returns number of iterations performed.
    /// </summary>
    public int ConjugateGradient(float[] startX, float[] rightSide, float minLen = 0f, float minEl = 0f)
    {
        int n = Rows;
        int iter = n * 3;
        minLen *= minLen;

        float[] pbuf = new float[n * 3];
        var r = pbuf.AsSpan(0, n);
        var p = pbuf.AsSpan(n, n);
        var Ap = pbuf.AsSpan(n * 2, n);

        // r = rh - A*x; p = r
        MultiplyVector(startX, Ap);
        for (int i = 0; i < n; i++) { r[i] = rightSide[i] - Ap[i]; p[i] = r[i]; }
        float r2 = DotSpan(r, r, n);

        do
        {
            MultiplyVector(p, Ap);
            float denom = DotSpan(p, Ap, n);
            if (denom * denom < 1e-30f) break;
            float a = r2 / denom;
            for (int i = 0; i < n; i++) r[i] -= Ap[i] * a;
            float r2new = DotSpan(r, r, n);
            if (r2new > r2 * 500) break;
            for (int i = 0; i < n; i++) startX[i] += p[i] * a;
            float b = r2new / r2; r2 = r2new;
            for (int i = 0; i < n; i++) p[i] = p[i] * b + r[i];
            float maxel = 0;
            for (int i = 0; i < n; i++) maxel = MathF.Max(maxel, MathF.Abs(r[i]));
            if (r2new <= minLen && maxel <= minEl) break;
        } while (--iter > 0);

        return n * 3 - iter;
    }

    /// <summary>
    /// Biconjugate gradient solver for non-symmetric matrices.
    /// Port of biconjugate_gradient from matrixnm.cpp.
    /// Returns number of iterations performed.
    /// </summary>
    public int BiconjugateGradient(float[] startX, float[] rightSide, float minLen = 0f, float minEl = 0f)
    {
        int n = Rows;
        int iter = n * 3;
        minLen *= minLen;

        float[] pbuf = new float[n * 6];
        var r = pbuf.AsSpan(0, n);
        var rc = pbuf.AsSpan(n, n);
        var p = pbuf.AsSpan(n * 2, n);
        var pc = pbuf.AsSpan(n * 3, n);
        var Ap = pbuf.AsSpan(n * 4, n);
        var tmp = pbuf.AsSpan(n * 5, n);

        // r = rh - A*x; rc = r; p = r; pc = rc
        MultiplyVector(startX, Ap);
        for (int i = 0; i < n; i++)
        {
            r[i] = rightSide[i] - Ap[i];
            rc[i] = r[i]; p[i] = r[i]; pc[i] = r[i];
        }
        float r2 = DotSpan(r, r, n);

        do
        {
            MultiplyVector(p, Ap);
            float denom = DotSpan(pc, Ap, n);
            if (denom * denom < 1e-30f) break;
            float a = r2 / denom;
            for (int i = 0; i < n; i++) r[i] -= Ap[i] * a;
            // rc -= (pc * A^T) * a  => tmp = A^T * pc
            MultiplyVectorTransposed(pc, tmp);
            for (int i = 0; i < n; i++) rc[i] -= tmp[i] * a;
            for (int i = 0; i < n; i++) startX[i] += p[i] * a;
            float r2new = DotSpan(rc, r, n);
            float b = r2new / r2; r2 = r2new;
            for (int i = 0; i < n; i++) { p[i] = p[i] * b + r[i]; pc[i] = pc[i] * b + rc[i]; }
            float err = 0, maxel = 0;
            for (int i = 0; i < n; i++) { err += r[i] * r[i]; maxel = MathF.Max(maxel, MathF.Abs(r[i])); }
            if (err <= minLen && maxel <= minEl) break;
        } while (--iter > 0 && r2 * r2 > 1e-30f);

        return n * 3 - iter;
    }

    /// <summary>
    /// Minimum residual solver for symmetric matrices.
    /// Port of minimum_residual from matrixnm.cpp.
    /// Returns number of iterations performed.
    /// </summary>
    public int MinimumResidual(float[] startX, float[] rightSide, float minLen = 0f, float minEl = 0f)
    {
        int n = Rows;
        int iter = n * 3;
        minLen *= minLen;

        float[] pbuf = new float[n * 4];
        var r = pbuf.AsSpan(0, n);
        var rc = pbuf.AsSpan(n, n);
        var p = pbuf.AsSpan(n * 2, n);
        var Ap = pbuf.AsSpan(n * 3, n);

        // r = rh - A*x
        MultiplyVector(startX, Ap);
        for (int i = 0; i < n; i++) r[i] = rightSide[i] - Ap[i];
        // rc = A*r
        MultiplyVector(r, rc);
        for (int i = 0; i < n; i++) p[i] = r[i];
        float r2 = DotSpan(rc, r, n);

        do
        {
            MultiplyVector(p, Ap);
            float denom = DotSpan(Ap, Ap, n);
            if (denom * denom < 1e-30f) break;
            float a = r2 / denom;
            for (int i = 0; i < n; i++) { r[i] -= Ap[i] * a; startX[i] += p[i] * a; }
            MultiplyVector(r, rc);
            float r2new = DotSpan(rc, r, n);
            float b = r2new / r2; r2 = r2new;
            for (int i = 0; i < n; i++) p[i] = p[i] * b + r[i];
            float err = 0, maxel = 0;
            for (int i = 0; i < n; i++) { err += r[i] * r[i]; maxel = MathF.Max(maxel, MathF.Abs(r[i])); }
            if (err <= minLen && maxel <= minEl) break;
        } while (--iter > 0 && r2 * r2 > 1e-30f);

        return n * 3 - iter;
    }

    /// <summary>
    /// Linear programming via simplex method.
    /// Port of LPsimplex from matrixnm.cpp.
    /// The matrix layout: rows 0..M-1 are constraints, row M is the objective function,
    /// row M+1 is used for Phase I if m1 &lt; M. Column N is the RHS.
    /// Returns: 0 = infeasible, 1 = optimal found, 2 = unbounded.
    /// </summary>
    public int LPSimplex(int m1, int m2, out float objFunOut, float[]? xOut = null, int nvars = -1, float e = -1f)
    {
        int M = Rows - 2, N = Cols - 1;
        int i, j, imax, jmax, iobjfun = M, res = 0, iter = (M + N) * 8;
        float t;
        const int imask = 0x7FFFFFFF;
        int[] irow = new int[M], icol = new int[N];
        objFunOut = 0;

        if (e < 0)
        {
            e = 0;
            for (i = Rows * Cols - 1; i >= 0; i--) e += Data[i];
            e *= 1e-6f / (Rows * Cols);
        }

        if (nvars < 0) nvars = N + m1 + m2;
        if (m1 < M)
        {
            iobjfun++;
            for (j = 0; j < N; j++)
            {
                t = 0;
                for (i = 0; i < M; i++) t -= this[i, j];
                this[iobjfun, j] = t;
            }
        }
        for (i = 0; i < N; i++) icol[i] = i;
        for (i = 0; i < M; i++) irow[i] = (N + i) | ~imask;

        do
        {
            t = 0; jmax = -1;
            for (j = 0; j < N; j++)
                if (icol[j] < nvars && this[iobjfun, j] > t) { jmax = j; t = this[iobjfun, j]; }
            if (jmax < 0) { res = 1; break; }

            imax = 0;
            while (imax < M && this[imax, jmax] > 0) imax++;
            if (imax == M) { res = 2; break; }
            for (i = imax + 1; i < M; i++)
                if (this[i, jmax] < 0 && this[i, N] * this[imax, jmax] < this[imax, N] * this[i, jmax])
                    imax = i;

            LPPivot(imax, jmax, M, N, m1, m2, iobjfun, irow, icol);
        } while (--iter > 0);

        if (m1 < M)
        {
            if (res == 2 || this[M, N] * this[M, N] < e * e)
            {
                res = 0;
            }
            else
            {
                for (i = 0; i < M; i++)
                {
                    if ((irow[i] & imask) > M + m1 + m2)
                    {
                        for (jmax = 0; jmax < N && icol[jmax] >= nvars; jmax++) ;
                        imax = i;
                        LPPivot(imax, jmax, M, N, m1, m2, iobjfun, irow, icol);
                    }
                    else if ((irow[i] & imask) > N + m1 && (irow[i] & ~imask) != 0)
                    {
                        for (j = 0; j < N; j++) this[i, j] *= -1;
                        irow[i] &= imask;
                    }
                }
                res = LPSimplex(M, 0, out objFunOut, xOut, nvars, e);
            }
        }
        else
        {
            if (xOut != null)
            {
                for (i = 0; i < N; i++) xOut[i] = 0;
                for (i = 0; i < M; i++)
                    if ((irow[i] & imask) < N)
                        xOut[irow[i] & imask] = this[i, N];
            }
            objFunOut = this[iobjfun, N];
        }
        return res;
    }

    /// <summary>LP simplex pivot operation (variable exchange). Extracted from LPSimplex for goto elimination.</summary>
    private void LPPivot(int imax, int jmax, int M, int N, int m1, int m2, int iobjfun, int[] irow, int[] icol)
    {
        const int imask = 0x7FFFFFFF;
        float rpivot = 1.0f / this[imax, jmax];
        for (int j = 0; j < Cols; j++) this[imax, j] *= -rpivot;
        this[imax, jmax] = rpivot;
        for (int i = 0; i < Rows; i++)
        {
            if (i == imax) continue;
            this[i, jmax] *= rpivot;
            for (int j = 0; j < Cols; j++)
                if (j != jmax)
                    this[i, j] -= this[imax, j] * this[i, jmax];
        }

        if ((irow[imax] & imask) >= N + m1 && (irow[imax] & imask) < N + m1 + m2 && (irow[imax] & ~imask) != 0)
        {
            this[iobjfun, jmax] += 1;
            for (int i = 0; i <= M; i++) this[i, jmax] *= -1;
        }

        int tmp = irow[imax] & imask; irow[imax] = icol[jmax]; icol[jmax] = tmp;
    }

    /// <summary>Multiply A^T * vec (transpose multiply without actually transposing).</summary>
    private void MultiplyVectorTransposed(ReadOnlySpan<float> vec, Span<float> result)
    {
        result.Slice(0, Cols).Clear();
        for (int i = 0; i < Rows; i++)
            for (int j = 0; j < Cols; j++)
                result[j] += Data[i * Cols + j] * vec[i];
    }

    /// <summary>Dot product of two spans.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DotSpan(ReadOnlySpan<float> a, ReadOnlySpan<float> b, int n)
    {
        float res = 0;
        for (int i = 0; i < n; i++) res += a[i] * b[i];
        return res;
    }

    /// <summary>
    /// Jacobi eigenvalue algorithm for symmetric matrices.
    /// Port of jacobi_transformation from matrixnm.cpp.
    /// Returns eigenvalues in eval[] and eigenvectors as rows of evec.
    /// </summary>
    public int JacobiTransformation(MatrixNM evec, float[] eval, float prec = 0f)
    {
        int n = Rows;
        evec.SetIdentity();
        Array.Copy(Data, eval, 0); // initialize eval from diagonal? Let's follow original more carefully

        // Copy diagonal to eval
        for (int i = 0; i < n; i++)
            eval[i] = Data[i * n + i];

        // Working copy
        float[] a = new float[n * n];
        Array.Copy(Data, a, n * n);

        int maxIter = 100;
        for (int iter = 0; iter < maxIter; iter++)
        {
            // Find max off-diagonal
            float maxOff = 0;
            int p = 0, q = 1;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (MathF.Abs(a[i * n + j]) > maxOff)
                    {
                        maxOff = MathF.Abs(a[i * n + j]);
                        p = i; q = j;
                    }

            if (maxOff < prec)
                break;

            // Compute rotation
            float app = a[p * n + p], aqq = a[q * n + q], apq = a[p * n + q];
            float theta = (aqq - app) / (2f * apq);
            float t = MathF.Sign(theta) / (MathF.Abs(theta) + MathF.Sqrt(theta * theta + 1f));
            float c = 1f / MathF.Sqrt(t * t + 1f);
            float s = t * c;

            // Update matrix
            a[p * n + p] -= t * apq;
            a[q * n + q] += t * apq;
            a[p * n + q] = a[q * n + p] = 0;

            for (int i = 0; i < n; i++)
            {
                if (i != p && i != q)
                {
                    float aip = a[i * n + p], aiq = a[i * n + q];
                    a[i * n + p] = a[p * n + i] = c * aip - s * aiq;
                    a[i * n + q] = a[q * n + i] = s * aip + c * aiq;
                }
            }

            // Update eigenvectors
            for (int i = 0; i < n; i++)
            {
                float eip = evec[i, p], eiq = evec[i, q];
                evec[i, p] = c * eip - s * eiq;
                evec[i, q] = s * eip + c * eiq;
            }
        }

        for (int i = 0; i < n; i++)
            eval[i] = a[i * n + i];

        return n;
    }
}
