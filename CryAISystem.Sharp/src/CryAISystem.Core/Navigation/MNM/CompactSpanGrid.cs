// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/CompactSpanGrid.h (210L)
// and CompactSpanGrid.cpp (107L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CryAISystem.Navigation.MNM;

public class CompactSpanGrid
{
    public struct Cell
    {
        public const int MaxSpanCount = 255;

        public uint index;  // 24 bits in C++ bitfield
        public uint count;  //  8 bits in C++ bitfield

        public Cell(uint _index, uint _count) { index = _index; count = _count; }

        // operator bool
        public bool IsValid => count != 0;
    }

    public struct Span
    {
        // C++ bitfields: backface:1, flags:3, bottom:9, height:9, depth:10
        public uint backface;
        public uint flags;
        public uint bottom;
        public uint height;
        public uint depth; // at surface

        public Span(ushort _bottom, ushort _height, ushort _depth, bool _backface)
        {
            backface = _backface ? 1u : 0u;
            flags = 0;
            bottom = _bottom;
            height = _height;
            depth = _depth;
            Debug.Assert(height > 0);
        }
    }

    private int m_width;
    private int m_height;
    private List<Cell> m_cells = new();
    private List<Span> m_spans = new();

    public CompactSpanGrid() { m_width = 0; m_height = 0; }

    public int GetCellCount() => m_cells.Count;
    public int GetSpanCount() => m_spans.Count;
    public int GetWidth() => m_width;
    public int GetHeight() => m_height;

    public int GetMemoryUsage()
    {
        // Approximate: struct size * capacity
        return 16 + m_cells.Capacity * 8 + m_spans.Capacity * 20;
    }

    public Cell GetCellByIndex(int i)
    {
        if (i < m_cells.Count)
            return m_cells[i];
        return new Cell(0, 0);
    }

    public Cell GetCell(int x, int y)
    {
        if (x < m_width && y < m_height)
            return m_cells[y * m_width + x];
        return new Cell(0, 0);
    }

    public ref Span GetSpan(int i) => ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(m_spans)[i];

    public Span GetSpanReadOnly(int i) => m_spans[i];

    public bool GetSpanAt(int x, int y, int top, int tolerance, ref int outSpan)
    {
        if (x < m_width && y < m_height)
        {
            Cell cell = m_cells[y * m_width + x];
            if (cell.IsValid)
            {
                int count = (int)cell.count;
                int index = (int)cell.index;

                for (int s = 0; s < count; ++s)
                {
                    Span span = m_spans[index + s];
                    int otop = (int)(span.bottom + span.height);
                    int dtop = top - otop;

                    if (Math.Abs(dtop) <= tolerance)
                    {
                        outSpan = index + s;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public bool GetSpanAtOffset(int cellOffset, int top, int tolerance, ref int outSpan)
    {
        Cell cell = m_cells[cellOffset];
        if (cell.IsValid)
        {
            int count = (int)cell.count;
            int index = (int)cell.index;

            for (int s = 0; s < count; ++s)
            {
                Span span = m_spans[index + s];
                int otop = (int)(span.bottom + span.height);
                int dtop = top - otop;

                if (Math.Abs(dtop) <= tolerance)
                {
                    outSpan = index + s;
                    return true;
                }
            }
        }
        return false;
    }

    public void Swap(CompactSpanGrid other)
    {
        (m_width, other.m_width) = (other.m_width, m_width);
        (m_height, other.m_height) = (other.m_height, m_height);

        var tmpCells = m_cells; m_cells = other.m_cells; other.m_cells = tmpCells;
        var tmpSpans = m_spans; m_spans = other.m_spans; other.m_spans = tmpSpans;
    }

    public void Clear()
    {
        m_width = 0;
        m_height = 0;
        m_cells.Clear();
        m_spans.Clear();
    }

    public void BuildFrom(DynamicSpanGrid dynGrid)
    {
        int cellCount = dynGrid.GetWidth() * dynGrid.GetHeight();
        int spanCount = dynGrid.GetCount();

        m_width = dynGrid.GetWidth();
        m_height = dynGrid.GetHeight();

        m_cells.Clear();
        m_cells.AddRange(new Cell[cellCount]);
        m_spans.Clear();
        m_spans.AddRange(new Span[spanCount]);

        int spanIndex = 0;

        for (int i = 0; i < cellCount; ++i)
        {
            DynamicSpanGrid.Element span = dynGrid.GetElement(i);
            if (span != null)
            {
                int index = spanIndex;
                m_cells[i] = new Cell((uint)index, 0);
                m_spans[spanIndex++] = new Span((ushort)span.bottom, (ushort)(span.top - span.bottom), (ushort)span.depth, span.IsBackface);

                for (span = span.next; span != null; span = span.next)
                    m_spans[spanIndex++] = new Span((ushort)span.bottom, (ushort)(span.top - span.bottom), (ushort)span.depth, span.IsBackface);

                int count = spanIndex - index;
                Debug.Assert(count <= Cell.MaxSpanCount);

                m_cells[i] = new Cell((uint)index, (uint)(count & 0xff));
            }
        }
    }

    public void CompactExcluding(CompactSpanGrid spanGrid, uint flags, int newSpanCount)
    {
        int cellCount = spanGrid.GetWidth() * spanGrid.GetHeight();

        m_width = spanGrid.GetWidth();
        m_height = spanGrid.GetHeight();

        m_cells.Clear();
        m_cells.AddRange(new Cell[cellCount]);
        m_spans.Clear();
        m_spans.AddRange(new Span[newSpanCount]);

        int spanIndex = 0;

        for (int i = 0; i < cellCount; ++i)
        {
            Cell cell = spanGrid.GetCellByIndex(i);
            if (cell.IsValid)
            {
                int ncount = 0;
                int nindex = spanIndex;

                int count = (int)cell.count;
                int index = (int)cell.index;

                for (int s = 0; s < count; ++s)
                {
                    Span span = spanGrid.GetSpanReadOnly(index + s);

                    if ((span.flags & flags) != 0)
                        continue;

                    ++ncount;
                    m_spans[spanIndex++] = span;
                }

                m_cells[i] = new Cell(ncount > 0 ? (uint)nindex : 0, (uint)ncount);
            }
        }
    }
}
