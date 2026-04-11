// Literal port of dev/Code/CryEngine/CryAISystem/AIQuadTree.{h,inl}
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// The element stored in CAIQuadTree must provide the following interface:
//
// The z component of the AABB should be ignored:
// bool DoesIntersectAABB(const AABB& aabb);
//
// const AABB &GetAABB();
//
// There should be a counter that CAIQuadTree can use internally - shouldn't
// be used for anything else:
// mutable int m_counter
//
// Returns some debug info about this element
// const char *GetDebugName() const
public interface IAIQuadTreeElement
{
    bool DoesIntersectAABB(AABB aabb);
    AABB GetAABB();
    int m_counter { get; set; }
    string GetDebugName();
    bool DoesAABBIntersectPoint(Vec3 point);
    bool DoesAABBIntersectAABB(AABB aabb);
}

public class CAIQuadTree<SAIQuadTreeElement> where SAIQuadTreeElement : class, IAIQuadTreeElement
{
    public CAIQuadTree()
    {
        m_testCounter = 0;
    }

    // ~CAIQuadTree() {}

    /// Adds a element to the list of elements, but doesn't add it to the QuadTree
    public void AddElement(SAIQuadTreeElement element)
    {
        m_elements.Add(element);
    }

    /// Clears elements and cells
    public void Clear(bool freeMemory = true)
    {
        if (freeMemory)
        {
            m_cells.Clear();
            m_elements.Clear();
        }
        else
        {
            // m_cells.resize(0); m_elements.resize(0);
            m_cells.Clear();
            m_elements.Clear();
        }
    }

    /// Builds the QuadTree from scratch (not incrementally) - deleting any previous tree.
    public void BuildQuadTree(int maxElementsPerCell, float minCellSize)
    {
        m_boundingBox.Reset();
        for (int idx = 0; idx < m_elements.Count; ++idx)
        {
            SAIQuadTreeElement it = m_elements[idx];
            m_boundingBox.Add(it.GetAABB());
            it.m_counter = -1;
        }

        // clear any existing cells
        m_cells.Clear();
        m_testCounter = 0;

        // set up the root
        m_cells.Add(new CQuadTreeCell(m_boundingBox));
        uint numElements = (uint)m_elements.Count;
        for (uint i = 0; i < numElements; ++i)
            m_cells[m_cells.Count - 1].m_elementIndices.Add((int)i);

        // rather than doing things recursively, use a stack of cells that need
        // to be processed - for each cell if it contains too many elements we
        // create child cells and move the elements down into them (then we
        // clear the parent elements).
        List<int> cellsToProcess = new List<int>();
        cellsToProcess.Add(0);

        // bear in mind during this that any time a new cell gets created any pointer
        // or reference to an existing cell may get invalidated - so use indexing.
        while (cellsToProcess.Count > 0)
        {
            int cellIndex = cellsToProcess[cellsToProcess.Count - 1];
            cellsToProcess.RemoveAt(cellsToProcess.Count - 1);

            if ((int)m_cells[cellIndex].m_elementIndices.Count <= maxElementsPerCell)
                continue;
            float radius = m_cells[cellIndex].m_aabb.GetSize().GetLength2D() * 0.5f;
            if (radius < minCellSize)
                continue;

            // we need to put these elements into the children
#if _DEBUG
            HashSet<int> handledElements = new HashSet<int>();
#endif
            for (uint iChild = 0; iChild < (uint)CQuadTreeCell.EChild.NUM_CHILDREN; ++iChild)
            {
                m_cells[cellIndex].m_childCellIndices[iChild] = (int)m_cells.Count;
                cellsToProcess.Add((int)m_cells.Count);
                m_cells.Add(new CQuadTreeCell(CreateAABB(m_cells[cellIndex].m_aabb, (CQuadTreeCell.EChild)iChild)));

                CQuadTreeCell childCell = m_cells[m_cells.Count - 1];
                uint numCellElements = (uint)m_cells[cellIndex].m_elementIndices.Count;
                for (uint i = 0; i < numCellElements; ++i)
                {
                    int iElement = m_cells[cellIndex].m_elementIndices[(int)i];
                    SAIQuadTreeElement element = m_elements[iElement];
                    if (DoesElementIntersectCell(element, childCell))
                    {
                        childCell.m_elementIndices.Add(iElement);
#if _DEBUG
                        handledElements.Add(iElement);
#endif
                    }
                }
            }
#if _DEBUG
            AILog.AIAssert(handledElements.Count == m_cells[cellIndex].m_elementIndices.Count);
#endif
            // the children handle all the elements now - we no longer need them
            m_cells[cellIndex].m_elementIndices.Clear();
        }
    }

    /// Returns elements that claim to intersect the point (ignoring z), and the number.
    public uint GetElements(List<SAIQuadTreeElement> elements, Vec3 point)
    {
        elements.Clear();

        if (m_cells.Count == 0)
            return 0;

        m_cellsToTest.Clear();
        m_cellsToTest.Add(0);
        IncrementTestCounter();
        while (m_cellsToTest.Count > 0)
        {
            int cellIndex = m_cellsToTest[m_cellsToTest.Count - 1];
            m_cellsToTest.RemoveAt(m_cellsToTest.Count - 1);

            CQuadTreeCell cell = m_cells[cellIndex];

            if (!Overlap.Point_AABB2D(point, cell.m_aabb))
                continue;

            if (cell.IsLeaf())
            {
                // if leaf test the elements
                uint numElements = (uint)cell.m_elementIndices.Count;
                for (uint i = 0; i != numElements; ++i)
                {
                    int elementIndex = cell.m_elementIndices[(int)i];
                    SAIQuadTreeElement element = m_elements[elementIndex];
                    // see if we've already done this triangle
                    if (element.m_counter == (int)m_testCounter)
                        continue;
                    element.m_counter = (int)m_testCounter;
                    if (element.DoesAABBIntersectPoint(point))
                        elements.Add(element);
                }
            }
            else
            {
                // if non-leaf, just add the children,
                for (uint iChild = 0; iChild < (uint)CQuadTreeCell.EChild.NUM_CHILDREN; ++iChild)
                {
                    int childIndex = cell.m_childCellIndices[iChild];
                    m_cellsToTest.Add(childIndex);
                }
            }
        }

        return (uint)elements.Count;
    }

    /// Returns elements that claim to intersect the aabb (ignoring z), and the number.
    public uint GetElements(List<SAIQuadTreeElement> elements, AABB aabb)
    {
        elements.Clear();

        if (m_cells.Count == 0)
            return 0;

        m_cellsToTest.Clear();
        m_cellsToTest.Add(0);
        IncrementTestCounter();
        while (m_cellsToTest.Count > 0)
        {
            int cellIndex = m_cellsToTest[m_cellsToTest.Count - 1];
            m_cellsToTest.RemoveAt(m_cellsToTest.Count - 1);

            CQuadTreeCell cell = m_cells[cellIndex];

            if (!Overlap.AABB_AABB2D(aabb, cell.m_aabb))
                continue;

            if (cell.IsLeaf())
            {
                // if leaf test the elements
                uint numElements = (uint)cell.m_elementIndices.Count;
                for (uint i = 0; i != numElements; ++i)
                {
                    int elementIndex = cell.m_elementIndices[(int)i];
                    SAIQuadTreeElement element = m_elements[elementIndex];
                    // see if we've already done this triangle
                    if (element.m_counter == (int)m_testCounter)
                        continue;
                    element.m_counter = (int)m_testCounter;
                    if (element.DoesAABBIntersectAABB(aabb))
                        elements.Add(element);
                }
            }
            else
            {
                // if non-leaf, just add the children,
                for (uint iChild = 0; iChild < (uint)CQuadTreeCell.EChild.NUM_CHILDREN; ++iChild)
                {
                    int childIndex = cell.m_childCellIndices[iChild];
                    m_cellsToTest.Add(childIndex);
                }
            }
        }

        return (uint)elements.Count;
    }

    /// Dumps our contents
    public void Dump(string debugName)
    {
        AILog.AILogAlways("QuadTree {0}", debugName);
        int indentLevel = 0;

        if (m_cells.Count == 0)
        {
            AILog.AILogAlways("  empty");
        }
        else
        {
            DumpCell(m_cells[0], 2);
        }
    }

    /// Internally we don't store pointers but store indices into a single contiguous
    /// array of cells and triangles (so that the vectors can get resized).
    ///
    /// Each cell will either contain children OR contain triangles.
    public class CQuadTreeCell
    {
        /// constructor clears everything
        public CQuadTreeCell() { Clear(); }
        /// constructor clears everything
        public CQuadTreeCell(AABB aabb) { m_aabb = aabb; Clear(); }
        /// Sets all child indices to -1 and clears the triangle indices.
        public void Clear()
        {
            for (uint i = 0; i < (uint)EChild.NUM_CHILDREN; ++i)
                m_childCellIndices[i] = -1;
            m_elementIndices.Clear();
        }

        /// Indicates if we contain triangles (if not then we should/might have children)
        public bool IsLeaf() { return m_childCellIndices[0] == -1; }

        /// indices into the children - P means "plus" and M means "minus" and the
        /// letters are xy. So PM means +ve x, -ve y
        public enum EChild
        {
            PP,
            PM,
            MP,
            MM,
            NUM_CHILDREN
        }

        /// indices of the children (if not leaf). Will be -1 if there is no child
        public int[] m_childCellIndices = new int[(int)EChild.NUM_CHILDREN];

        /// indices of the elements (if leaf)
        public List<int> m_elementIndices = new List<int>();

        /// Bounding box for the space we own
        public AABB m_aabb;
    }

    /// Functor that can be passed to std::sort so that it sorts equal sized cells along a specified
    /// direction such that cells near the beginning of a line with dirPtr come at the end of the
    /// sorted container. This means they get processed first when that container is used as a stack.
    public class CCellSorter : IComparer<int>
    {
        public CCellSorter(Vec3 dirPtr, List<CQuadTreeCell> cellsPtr) { m_dirPtr = dirPtr; m_cellsPtr = cellsPtr; }
        public int Compare(int cell1Index, int cell2Index)
        {
            Vec3 delta = m_cellsPtr[cell2Index].m_aabb.min - m_cellsPtr[cell1Index].m_aabb.min;
            return delta.Dot(m_dirPtr) < 0.0f ? -1 : 1;
        }
        public Vec3 m_dirPtr;
        public List<CQuadTreeCell> m_cellsPtr;
    }

    /// Create a bounding box appropriate for a child, based on a parents AABB
    private AABB CreateAABB(AABB aabb, CQuadTreeCell.EChild child)
    {
        Vec3 dims = 0.5f * (aabb.max - aabb.min);
        Vec3 offset;
        switch (child)
        {
            case CQuadTreeCell.EChild.PP: offset = new Vec3(1, 1, 0); break;
            case CQuadTreeCell.EChild.PM: offset = new Vec3(1, 0, 0); break;
            case CQuadTreeCell.EChild.MP: offset = new Vec3(0, 1, 0); break;
            case CQuadTreeCell.EChild.MM: offset = new Vec3(0, 0, 0); break;
            default:
                AILog.AIWarning("CAIQuadTree::CreateAABB Got impossible child: {0}", (int)child);
                offset = new Vec3(0, 0, 0);
                break;
        }

        AABB result = new AABB();
        result.min = aabb.min + new Vec3(offset.x * dims.x, offset.y * dims.y, 0.0f);
        result.max = result.min + dims;
        // expand it just a tiny bit just to be safe!
        float extra = 0.00001f;
        result.min -= extra * dims;
        result.max += extra * dims;
        return result;
    }

    /// Returns true if the triangle intersects or is contained by a cell
    private bool DoesElementIntersectCell(SAIQuadTreeElement element, CQuadTreeCell cell)
    {
        return element.DoesIntersectAABB(cell.m_aabb);
    }

    /// Increment our test counter, wrapping around if necessary and zapping the
    /// triangle counters.
    /// Const because we only modify mutable members.
    private void IncrementTestCounter()
    {
        ++m_testCounter;
        if (m_testCounter == 0)
        {
            // wrap around - clear all the element counters
            uint numElements = (uint)m_elements.Count;
            for (uint i = 0; i != numElements; ++i)
                m_elements[(int)i].m_counter = 0;
            m_testCounter = 1;
        }
    }

    /// Dumps the cell and all its children, indented
    private void DumpCell(CQuadTreeCell cell, int indentLevel)
    {
        string indent = "";
        for (int i = 0; i < indentLevel; ++i)
            indent += " ";
        if (cell.IsLeaf())
        {
            if (cell.m_elementIndices.Count == 0)
            {
                AILog.AILogAlways("{0} No elements", indent);
            }
            else
            {
                for (uint i = 0; i < (uint)cell.m_elementIndices.Count; ++i)
                    AILog.AILogAlways("{0} {1} {2}", indent, cell.m_elementIndices[(int)i], m_elements[cell.m_elementIndices[(int)i]].GetDebugName());
            }
        }
        else
        {
            indentLevel += 2;
            AILog.AILogAlways("{0} PP", indent);
            DumpCell(m_cells[cell.m_childCellIndices[(int)CQuadTreeCell.EChild.PP]], indentLevel);
            AILog.AILogAlways("{0} PM", indent);
            DumpCell(m_cells[cell.m_childCellIndices[(int)CQuadTreeCell.EChild.PM]], indentLevel);
            AILog.AILogAlways("{0} MP", indent);
            DumpCell(m_cells[cell.m_childCellIndices[(int)CQuadTreeCell.EChild.MP]], indentLevel);
            AILog.AILogAlways("{0} MM", indent);
            DumpCell(m_cells[cell.m_childCellIndices[(int)CQuadTreeCell.EChild.MM]], indentLevel);
        }
    }

    /// All our cells. The only thing guaranteed about this is that m_cell[0] (if
    /// it exists) is the root cell.
    private List<CQuadTreeCell> m_cells = new List<CQuadTreeCell>();
    /// All our elements.
    private List<SAIQuadTreeElement> m_elements = new List<SAIQuadTreeElement>();

    private AABB m_boundingBox;

    /// During intersection testing we keep a stack of cells to test (rather than recursing) -
    /// to avoid excessive memory allocation we don't free the memory between calls unless
    /// the user calls FreeTemporaryMemory();
    private /*mutable*/ List<int> m_cellsToTest = new List<int>();

    /// Counter used to prevent multiple tests when triangles are contained in more than
    /// one cell
    private /*mutable*/ uint m_testCounter;
}

/// ElementOutsideAABB
public class ElementOutsideAABB
{
    public ElementOutsideAABB(AABB aabb) { m_aabb = aabb; }

    public bool Apply<SAIQuadTreeElement>(SAIQuadTreeElement element) where SAIQuadTreeElement : IAIQuadTreeElement
    {
        return !element.DoesIntersectAABB(m_aabb);
    }
    public AABB m_aabb;
}
