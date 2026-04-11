// Port of CryPhysics vectorn.h - N-dimensional vector with pool allocation
// Original: Copyright Crytek GMBH, used under license

using System.Buffers;
using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// Dynamic N-dimensional vector for physics calculations.
/// Port of vectorn_tpl from CryEngine. Uses ArrayPool for small vectors.
/// </summary>
public sealed class VectorN : IDisposable
{
    public float[] Data { get; private set; }
    public int Length { get; private set; }

    private bool _ownsData;

    public VectorN(int length, float[]? data = null)
    {
        Length = length;
        if (data != null)
        {
            Data = data;
            _ownsData = false;
        }
        else
        {
            Data = ArrayPool<float>.Shared.Rent(System.Math.Max(length, 1));
            _ownsData = true;
        }
    }

    /// <summary>Create from existing span (copies data).</summary>
    public VectorN(ReadOnlySpan<float> source)
    {
        Length = source.Length;
        Data = ArrayPool<float>.Shared.Rent(Length);
        source.CopyTo(Data);
        _ownsData = true;
    }

    public void Dispose()
    {
        if (_ownsData && Data != null)
        {
            ArrayPool<float>.Shared.Return(Data);
            _ownsData = false;
        }
    }

    public float this[int idx]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Data[idx];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Data[idx] = value;
    }

    /// <summary>Access as span.</summary>
    public Span<float> AsSpan() => Data.AsSpan(0, Length);

    /// <summary>Zero all elements.</summary>
    public VectorN Zero()
    {
        Array.Clear(Data, 0, Length);
        return this;
    }

    /// <summary>Squared L2 norm.</summary>
    public float LengthSq()
    {
        float res = 0;
        for (int i = 0; i < Length; i++)
            res += Data[i] * Data[i];
        return res;
    }

    /// <summary>Scale in place.</summary>
    public VectorN Scale(float s)
    {
        for (int i = 0; i < Length; i++)
            Data[i] *= s;
        return this;
    }

    /// <summary>Dot product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(VectorN a, VectorN b)
    {
        float res = 0;
        int n = System.Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
            res += a.Data[i] * b.Data[i];
        return res;
    }

    /// <summary>Copy from another vector.</summary>
    public void CopyFrom(VectorN src)
    {
        int n = System.Math.Min(Length, src.Length);
        Array.Copy(src.Data, Data, n);
    }

    /// <summary>Add: this += other.</summary>
    public void Add(VectorN other)
    {
        int n = System.Math.Min(Length, other.Length);
        for (int i = 0; i < n; i++)
            Data[i] += other.Data[i];
    }

    /// <summary>Subtract: this -= other.</summary>
    public void Subtract(VectorN other)
    {
        int n = System.Math.Min(Length, other.Length);
        for (int i = 0; i < n; i++)
            Data[i] -= other.Data[i];
    }

    /// <summary>Assign from matrix-vector product: this = mtx * vec.</summary>
    public void AssignMatrixVectorProduct(MatrixNM mtx, VectorN vec)
    {
        for (int i = 0; i < mtx.Rows; i++)
        {
            float sum = 0;
            for (int j = 0; j < mtx.Cols; j++)
                sum += mtx[i, j] * vec[j];
            Data[i] = sum;
        }
    }

    /// <summary>Add matrix-vector product: this += mtx * vec.</summary>
    public void AddMatrixVectorProduct(MatrixNM mtx, VectorN vec)
    {
        for (int i = 0; i < mtx.Rows; i++)
            for (int j = 0; j < mtx.Cols; j++)
                Data[i] += mtx[i, j] * vec[j];
    }
}
