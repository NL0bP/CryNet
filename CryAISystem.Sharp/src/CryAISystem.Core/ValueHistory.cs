// Literal port of dev/Code/CryEngine/CryAISystem/ValueHistory.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// Simple class to track a history of float values. Assumes positive values.
public class CValueHistory<T> where T : struct, System.IComparable<T>
{
    public CValueHistory(uint s, float sampleIterval)
    {
        this.sampleIterval = sampleIterval;
        head = 0;
        size = 0;
        t = 0;
        v = default;
        data = new List<T>(new T[s]);
    }

    public void Reset()
    {
        size = 0; head = 0;
        v = default; t = 0;
    }

    public void Sample(T nv)
    {
        data[(int)head] = nv;
        head = (head + 1) % (uint)data.Count;
        if (size < (uint)data.Count) size++;
    }

    public void Sample(T nv, float dt)
    {
        t += dt;
        v = (v.CompareTo(nv) >= 0) ? v : nv;

        int iter = 0;
        while (t > sampleIterval && iter < 5)
        {
            data[(int)head] = v;
            head = (head + 1) % (uint)data.Count;
            if (size < (uint)data.Count) size++;
            ++iter;
            t -= sampleIterval;
        }
        if (iter == 5)
            t = 0;
        v = default;
    }

    public uint GetSampleCount() { return size; }
    public uint GetMaxSampleCount() { return (uint)data.Count; }
    public T GetSampleInterval() { return (T)(object)sampleIterval; }

    public T GetSample(uint i)
    {
        uint n = (uint)data.Count;
        return data[(int)((head + (n - 1 - i)) % n)];
    }

    public T GetMaxSampleValue()
    {
        T maxVal = default;
        uint n = (uint)data.Count;
        for (uint i = 0; i < size; ++i)
        {
            T cur = data[(int)((head + (n - 1 - i)) % n)];
            if (cur.CompareTo(maxVal) > 0) maxVal = cur;
        }
        return maxVal;
    }

    private List<T> data;
    private uint head, size;
    private T v;
    private float t;
    private readonly float sampleIterval;
}
