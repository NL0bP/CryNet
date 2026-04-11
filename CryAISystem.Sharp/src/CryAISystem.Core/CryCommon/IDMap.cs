// Literal port of dev/Code/CryEngine/CryCommon/IDMap.h.
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// id_map<IDType, ValueType, IndexType=ushort, CounterType=ushort> is an O(1) sparse-handle
// container that bit-packs a generation counter into the upper bits of each ID and reuses
// freed slots via a circular free queue.
//
// Layout in C++:
//   id  = (counter << index_bits) | (index + 1)
//   index_bits   = sizeof(IndexType) * 8
//   counter_bits = sizeof(CounterType) * 8
//   counter_free_bit = 1 << (counter_bits - 1)
//
// The C# literal port preserves the same bit layout, the same free-list semantics, the
// same insert/erase/grow contracts, and the same operator[] / get / get_index API surface.

using System.Collections.Generic;

namespace CryAISystem.CryCommon;

public class id_map<IDType, ValueType>
    where IDType : struct, System.IConvertible
    where ValueType : class
{
    // Sizes of the index/counter fields in bits. The C++ template defaults to
    // ushort/ushort (16 bits each). The literal C# port hard-codes the same.
    private const int index_bits = 16;
    private const int counter_bits = 16;
    private const ushort counter_free_bit = 1 << (counter_bits - 1);
    private const ushort counter_count_mask = counter_free_bit - 1;

    private struct Container
    {
        public ValueType value;
        public ushort counter;
    }

    private List<ushort> frees_;
    private int freesBegin_; // circular queue
    private int freesEnd_;
    private int freesLen_;

    private List<Container> values_;

    public id_map(int size)
    {
        values_ = new List<Container>(size);
        for (int i = 0; i < size; ++i)
            values_.Add(new Container { value = null, counter = counter_free_bit });
        frees_ = new List<ushort>(size);
        for (int i = 0; i < size; ++i)
            frees_.Add((ushort)(size - i - 1));
        freesBegin_ = 0;
        freesEnd_ = 0;
        freesLen_ = size;
    }

    // Helpers — bit packing of (counter, index) into the IDType.
    private uint id_(ushort index, ushort counter)
    {
        return (uint)(((uint)counter << index_bits) | ((uint)index + 1u));
    }

    private uint counter_(uint id)
    {
        return id >> index_bits;
    }

    private ushort index_(uint id)
    {
        return (ushort)((id & ((1u << index_bits) - 1u)) - 1u);
    }

    private static bool free_(ushort counter)
    {
        return (counter & counter_free_bit) != 0;
    }

    public void clear()
    {
        int count = values_.Count;
        for (int i = 0; i < count; ++i)
        {
            Container container = values_[i];
            container.value = null;
            container.counter = counter_free_bit;
            values_[i] = container;
        }
        frees_.Clear();
        for (int i = 0; i < count; ++i)
            frees_.Add((ushort)(count - i - 1));
        freesBegin_ = 0;
        freesEnd_ = 0;
        freesLen_ = frees_.Count;
    }

    public uint insert(ValueType value)
    {
        System.Diagnostics.Debug.Assert(frees_.Count > 0); // capacity exceeded

        if (freesLen_ != 0)
        {
            ushort index = frees_[freesBegin_];
            freesBegin_ = (freesBegin_ + 1) % values_.Count;
            --freesLen_;

            Container container = values_[index];
            ushort counter = container.counter;
            container.value = value;
            System.Diagnostics.Debug.Assert(free_(counter));

            counter = (ushort)((counter & counter_count_mask) + 1);
            counter |= (ushort)((uint)counter >> (counter_bits - 1));
            counter &= counter_count_mask;
            System.Diagnostics.Debug.Assert(counter != 0);

            container.counter = counter;
            values_[index] = container;
            return id_(index, counter);
        }

        return 0;
    }

    public void insert(uint id, ValueType value)
    {
        ushort index = index_(id);
        System.Diagnostics.Debug.Assert(index < values_.Count);

        if (index < values_.Count)
        {
            ushort counter = (ushort)counter_(id);

            Container container = values_[index];
            container.value = value;
            container.counter = counter;
            values_[index] = container;

            int it = frees_.IndexOf(index);
            System.Diagnostics.Debug.Assert(it != -1);
            // std::swap(*it, frees_[freesBegin_]);
            ushort tmp = frees_[it];
            frees_[it] = frees_[freesBegin_];
            frees_[freesBegin_] = tmp;
            freesBegin_ = (freesBegin_ + 1) % values_.Count;
            --freesLen_;
        }
    }

    public void erase(uint id)
    {
        ushort index = index_(id);
        System.Diagnostics.Debug.Assert(index < values_.Count);

        if (index < values_.Count)
        {
            ushort counter = (ushort)counter_(id);

            Container container = values_[index];

            if (container.counter == counter)
            {
                frees_[freesEnd_] = index;
                freesEnd_ = (freesEnd_ + 1) % values_.Count;
                ++freesLen_;
                container.value = null;
                container.counter |= counter_free_bit;
                values_[index] = container;
            }
        }
    }

    public int size() { return values_.Count - freesLen_; }
    public int capacity() { return values_.Count; }
    public bool empty() { return freesLen_ == values_.Count; }
    public bool full() { return freesLen_ == 0; }

    public bool validate(uint id)
    {
        ushort index = index_(id);
        ushort counter = (ushort)counter_(id);
        return (index < values_.Count) && !free_(counter) && (values_[index].counter == counter);
    }

    public bool free(uint id)
    {
        ushort index = index_(id);
        return index_free(index);
    }

    public bool index_free(int index)
    {
        Container container = values_[index];
        return free_(container.counter);
    }

    public uint get_index_id(int index)
    {
        Container container = values_[index];
        return id_((ushort)index, container.counter);
    }

    public ValueType get_index(int index)
    {
        return values_[index].value;
    }

    public ValueType get(uint id)
    {
        ushort index = index_(id);
        Container container = values_[index];
        System.Diagnostics.Debug.Assert(counter_(id) == container.counter);
        return container.value;
    }

    public int get_index_for_id(uint id)
    {
        return index_(id);
    }

    public ValueType this[uint id]
    {
        get { return get(id); }
        set
        {
            ushort index = index_(id);
            if (index < values_.Count)
            {
                Container container = values_[index];
                container.value = value;
                values_[index] = container;
            }
        }
    }
}
