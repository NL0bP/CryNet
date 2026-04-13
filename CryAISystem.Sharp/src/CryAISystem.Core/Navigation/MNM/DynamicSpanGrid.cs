// Literal port of dev/Code/CryEngine/CryAISystem/Navigation/MNM/DynamicSpanGrid.h (126L)
// and DynamicSpanGrid.cpp (310L).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;

namespace CryAISystem.Navigation.MNM;

public class DynamicSpanGrid
{
    public class Element
    {
        public const int MaxWaterDepth = (1 << 10) - 1;

        // C++ bitfields: backface:1, flags:3, bottom:9, top:9, depth:10
        public uint backface;
        public uint flags;
        public uint bottom;
        public uint top;
        public uint depth;
        public Element next;

        public Element() { backface = 0; flags = 0; bottom = 0; top = 0; depth = 0; next = null; }

        public Element(ushort _bottom, ushort _top, bool _backface)
        {
            backface = _backface ? 1u : 0u;
            flags = 0;
            bottom = _bottom;
            top = _top;
            depth = 0;
            next = null;
        }

        public Element(Element other)
        {
            backface = other.backface;
            flags = other.flags;
            bottom = other.bottom;
            top = other.top;
            depth = other.depth;
            next = null; // do not copy linked list pointer
        }

        public bool IsBackface => backface != 0;
    }

    private int m_width;
    private int m_height;
    private int m_count;
    private List<Element> m_grid = new();

    public DynamicSpanGrid()
    {
        m_width = 0;
        m_height = 0;
        m_count = 0;
    }

    public DynamicSpanGrid(int width, int height)
    {
        m_width = width;
        m_height = height;
        m_count = 0;
        m_grid = new List<Element>(width * height);
        for (int i = 0; i < width * height; i++)
            m_grid.Add(null);
    }

    public DynamicSpanGrid(DynamicSpanGrid other)
    {
        m_width = other.m_width;
        m_height = other.m_height;
        m_count = other.m_count;

        int gridSize = m_width * m_height;
        m_grid = new List<Element>(gridSize);
        for (int i = 0; i < gridSize; i++)
            m_grid.Add(null);

        for (int i = 0; i < gridSize; ++i)
        {
            if (other.m_grid[i] != null)
            {
                Element ospan = other.m_grid[i];
                Element span = new Element(ospan);
                m_grid[i] = span;

                for (ospan = ospan.next; ospan != null; ospan = ospan.next)
                {
                    Element next = new Element(ospan);
                    span.next = next;
                    span = next;
                }
            }
        }
    }

    public void Swap(DynamicSpanGrid other)
    {
        var tmpGrid = m_grid; m_grid = other.m_grid; other.m_grid = tmpGrid;
        (m_width, other.m_width) = (other.m_width, m_width);
        (m_height, other.m_height) = (other.m_height, m_height);
        (m_count, other.m_count) = (other.m_count, m_count);
    }

    public void Reset(int width, int height)
    {
        m_width = width;
        m_height = height;
        m_count = 0;

        m_grid = new List<Element>(width * height);
        for (int i = 0; i < width * height; i++)
            m_grid.Add(null);
    }

    public Element GetElement(int i) => m_grid[i];

    public Element GetSpan(int x, int y) => m_grid[y * m_width + x];

    public int GetWidth() => m_width;
    public int GetHeight() => m_height;
    public int GetCount() => m_count;

    public int GetMemoryUsage()
    {
        // Approximate
        return 24 + m_grid.Capacity * 8 + m_count * 40;
    }

    private void TryMergeNext(Element span, ushort top, bool backface)
    {
        if (span.next != null && span.next.bottom == top && (span.next.IsBackface == backface))
        {
            span.top = span.next.top;
            Element next = span.next;
            span.next = span.next.next;
            --m_count;
            // In C++ calls Destruct(next) — in C# the GC handles it
        }
    }

    private void TryMergePrev(Element span, Element prev, ushort bottom, bool backface)
    {
        if (prev != null && prev.top == bottom && (prev.IsBackface == backface))
        {
            prev.next = span.next;
            prev.top = span.top;
            --m_count;
            // In C++ calls Destruct(span)
        }
    }

    public void AddVoxel(int x, int y, int z, bool backface = false)
    {
        int offset = y * m_width + x;
        Element curr = m_grid[offset];
        Element last = null;

        ushort z0 = (ushort)z;
        ushort z1 = (ushort)(z + 1);

        if (curr == null)
        {
            ++m_count;
            m_grid[offset] = new Element(z0, z1, backface);
            return;
        }

        while (curr != null)
        {
            ushort bottom = (ushort)curr.bottom;
            ushort top = (ushort)curr.top;

            if ((z0 >= bottom) && (z1 <= top))
            {
                if ((backface == curr.IsBackface) || backface)
                    return;

                if (z0 == bottom)
                {
                    if (z1 == top)
                    {
                        curr.backface = 0;
                        TryMergeNext(curr, z1, backface);
                        TryMergePrev(curr, last, z0, backface);
                    }
                    else
                    {
                        curr.bottom = z1;

                        ++m_count;
                        Element newSpan = new Element(z0, z1, backface);
                        newSpan.next = curr;

                        if (last != null)
                        {
                            last.next = newSpan;
                            TryMergePrev(newSpan, last, z0, backface);
                        }
                        else
                            m_grid[offset] = newSpan;
                    }
                    return;
                }
                else if (z1 == top) // insert at top
                {
                    curr.top = z0;

                    ++m_count;
                    Element newSpan = new Element(z0, z1, backface);
                    newSpan.next = curr.next;
                    curr.next = newSpan;

                    TryMergeNext(newSpan, z1, backface);
                }
                else // insert in the middle
                {
                    curr.top = z0;

                    ++m_count;
                    Element newSpanTop = new Element(z1, top, curr.IsBackface);
                    newSpanTop.next = curr.next;

                    ++m_count;
                    Element newSpanMiddle = new Element(z0, z1, backface);
                    newSpanMiddle.next = newSpanTop;
                    curr.next = newSpanMiddle;
                }
                return;
            }
            else if (z0 == top)
            {
                if (curr.next == null || curr.next.bottom > z0)
                {
                    if (backface == curr.IsBackface)
                        curr.top = z1;
                    else
                    {
                        ++m_count;
                        Element newSpan = new Element(z0, z1, backface);
                        newSpan.next = curr.next;
                        curr.next = newSpan;
                    }

                    TryMergeNext(curr, z1, backface);
                    return;
                }
            }
            else if (z1 == bottom)
            {
                if (backface == curr.IsBackface)
                    curr.bottom = z0;
                else
                {
                    ++m_count;
                    Element newSpan = new Element(z0, z1, backface);
                    newSpan.next = curr;

                    if (last != null)
                        last.next = newSpan;
                    else
                        m_grid[offset] = newSpan;
                }

                TryMergePrev(curr, last, z0, backface);
                return;
            }
            else if (z1 < bottom)
            {
                ++m_count;
                Element newSpan = new Element(z0, z1, backface);
                newSpan.next = curr;

                if (last != null)
                    last.next = newSpan;
                else
                    m_grid[offset] = newSpan;

                return;
            }

            last = curr;
            curr = curr.next;
        }

        if (curr == null)
        {
            ++m_count;
            Element newSpan = new Element(z0, z1, backface);
            last.next = newSpan;
        }
    }
}
