// Literal port of dev/Code/CryEngine/CryAISystem/PolygonSetOps/BiDirMap.h
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// NOTE: the C++ implementation uses raw pointers and reinterpret_cast plus offsetof
// to share storage between the two indices. C# cannot replicate that exact memory
// layout, so this port keeps the public API identical (Insert/Erase/Find/operator[]/
// Begin/End/Iterator/Size/Empty/GetPrimaryKeys/GetSecondaryKeys) while using two
// SortedDictionary indices into a flat list of DataRecord pairs.

using System.Collections.Generic;

namespace CryAISystem.PolygonSetOps;

/// Small utility to compare pointers by the values they point to.
public class PointerValueCompare<T> : IComparer<T> where T : System.IComparable<T>
{
    public int Compare(T lhs, T rhs) { return lhs.CompareTo(rhs); }
}

/**
 * @brief Allows going from values to keys as well.
 */
public class BidirectionalMap<T0, T1>
    where T0 : System.IComparable<T0>
    where T1 : System.IComparable<T1>
{
    public struct DataRecord
    {
        public T0 first;
        public T1 second;
        public DataRecord(T0 a, T1 b) { first = a; second = b; }
    }

    private SortedDictionary<T0, DataRecord> mFwd;
    private SortedDictionary<T1, DataRecord> mBwd;

    private void Swap(BidirectionalMap<T0, T1> rhs)
    {
        var f = mFwd; mFwd = rhs.mFwd; rhs.mFwd = f;
        var b = mBwd; mBwd = rhs.mBwd; rhs.mBwd = b;
    }

    public BidirectionalMap()
    {
        mFwd = new SortedDictionary<T0, DataRecord>();
        mBwd = new SortedDictionary<T1, DataRecord>();
    }

    public BidirectionalMap(BidirectionalMap<T0, T1> rhs) : this()
    {
        foreach (var kv in rhs.mFwd)
        {
            DataRecord data = kv.Value;
            Insert(data.first, data.second);
        }
    }

    // ~BidirectionalMap() { Clear(); }

    public BidirectionalMap<T0, T1> AssignFrom(BidirectionalMap<T0, T1> rhs)
    {
        BidirectionalMap<T0, T1> tmp = new BidirectionalMap<T0, T1>(rhs);
        Swap(tmp);
        return this;
    }

    public void Insert(T1 f, T0 i)
    {
        DataRecord new_val = new DataRecord(i, f);
        bool fwd_result = !mFwd.ContainsKey(i);
        if (fwd_result) mFwd.Add(i, new_val);
        System.Diagnostics.Debug.Assert(fwd_result);
        bool bwd_result = !mBwd.ContainsKey(f);
        if (bwd_result) mBwd.Add(f, new_val);
        System.Diagnostics.Debug.Assert(bwd_result);
    }

    public void Insert(T0 i, T1 f)
    {
        Insert(f, i);
    }

    public void Erase(T0 i)
    {
        DataRecord to_be_deleted;
        bool found_fwd = mFwd.TryGetValue(i, out to_be_deleted);
        System.Diagnostics.Debug.Assert(found_fwd);

        mFwd.Remove(i);
        mBwd.Remove(to_be_deleted.second);
    }

    public void EraseSecond(T1 f)
    {
        DataRecord to_be_deleted;
        bool found_bwd = mBwd.TryGetValue(f, out to_be_deleted);
        System.Diagnostics.Debug.Assert(found_bwd);

        mFwd.Remove(to_be_deleted.first);
        mBwd.Remove(f);
    }

    public void Clear()
    {
        mFwd.Clear();
        mBwd.Clear();
    }

    public class Iterator
    {
        public SortedDictionary<T0, DataRecord>.Enumerator mPrimKeysIter;
        public bool mEnd;

        public Iterator(SortedDictionary<T0, DataRecord>.Enumerator prim_keys_iter, bool end)
        {
            mPrimKeysIter = prim_keys_iter;
            mEnd = end;
        }

        public Iterator IncrementOp()
        {
            mEnd = !mPrimKeysIter.MoveNext();
            return this;
        }

        public DataRecord DerefOp()
        {
            return mPrimKeysIter.Current.Value;
        }

        public static bool operator ==(Iterator a, Iterator b)
        {
            return a.mEnd == b.mEnd;
        }
        public static bool operator !=(Iterator a, Iterator b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj) { return obj is Iterator i && this == i; }
        public override int GetHashCode() { return mEnd.GetHashCode(); }
    }

    public Iterator Find(T0 i)
    {
        var en = mFwd.GetEnumerator();
        bool foundOne = false;
        while (en.MoveNext())
        {
            if (en.Current.Key.CompareTo(i) == 0)
            {
                foundOne = true;
                break;
            }
        }
        return new Iterator(en, !foundOne);
    }

    public Iterator FindSecond(T1 f)
    {
        DataRecord rec;
        if (!mBwd.TryGetValue(f, out rec))
            return End();
        return Find(rec.first);
    }

    public T1 IndexerFwd(T0 i)
    {
        return Find(i).DerefOp().second;
    }

    public T0 IndexerBwd(T1 f)
    {
        return FindSecond(f).DerefOp().first;
    }

    public int Size()
    {
        System.Diagnostics.Debug.Assert(mFwd.Count == mBwd.Count);
        return mFwd.Count;
    }

    public bool Empty()
    {
        System.Diagnostics.Debug.Assert(mFwd.Count == mBwd.Count);
        return mFwd.Count == 0;
    }

    public System.Collections.Generic.List<T0> GetPrimaryKeys()
    {
        var result = new System.Collections.Generic.List<T0>(mFwd.Count);
        foreach (var kv in mFwd)
            result.Add(kv.Key);
        return result;
    }

    public System.Collections.Generic.List<T1> GetSecondaryKeys()
    {
        var result = new System.Collections.Generic.List<T1>(mBwd.Count);
        var it = Begin();
        var endIt = End();
        for (; it != endIt; it.IncrementOp())
        {
            result.Add(it.DerefOp().second);
        }
        return result;
    }

    public Iterator Begin()
    {
        var en = mFwd.GetEnumerator();
        bool any = en.MoveNext();
        return new Iterator(en, !any);
    }

    public Iterator End()
    {
        return new Iterator(default(SortedDictionary<T0, DataRecord>.Enumerator), true);
    }
}
