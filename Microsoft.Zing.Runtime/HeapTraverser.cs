using System;
using System.Collections;

namespace Microsoft.Zing
{
    /// <summary>
    /// Summary description for HeapTraverser
    /// </summary>
    [CLSCompliant(false)]
    sealed public class HeapTraverser
    {
        private HeapTraverser()
        {
        }

        private static Queue heapQueue;
        private static Hashtable seen;

        private class ReferenceTraverser : FieldTraverser
        {
            private StateImpl state;
            private FieldTraverser perEdgeTraverser;

            public ReferenceTraverser(StateImpl s, FieldTraverser e)
            {
                state = s;
                perEdgeTraverser = e;
            }

            public override void DoTraversal(Object field)
            {
                //nothing
            }

            public override void DoTraversal(Pointer ptr)
            {
                if (ptr != 0u && !seen.Contains(ptr))
                {
                    seen.Add(ptr, null);
                    heapQueue.Enqueue(ptr);
                }
                if (perEdgeTraverser != null) perEdgeTraverser.DoTraversal(ptr);
            }
        }

        /*      private class EdgeCounter : FieldTraverser
                {
                    private int count = 0;
                    public override void DoTraversal(Object field)
                    {
                        throw new ApplicationException("EdgePrinter should never traverse non Ptrs");
                    }
                    public override void DoTraversal(Pointer ptr)
                    {
                        if(ptr == 0u) return;
                        count ++;
                    }
                    public void reset(){count = 0;}
                    public int edgeCount(){return count;}
                }

        */

        static public void TraverseHeap(StateImpl state, FieldTraverser edge, FieldTraverser node)
        {
            heapQueue = new Queue();
            seen = new Hashtable();

            ReferenceTraverser et = new ReferenceTraverser(state, edge);

            state.TraverseGlobalReferences(et);

            for (int i = 0; i < state.NumProcesses; i++)
            {
                Process p = state.GetProcess(i);
                for (ZingMethod m = p.TopOfStack; m != null; m = m.Caller)
                {
                    m.TraverseFields(et);
                }
            }

            while (heapQueue.Count > 0)
            {
                Pointer ptr = (Pointer)heapQueue.Dequeue();
                if (node != null) node.DoTraversal(ptr);
                HeapEntry he = (HeapEntry)state.Heap[ptr];
                he.heList.TraverseFields(et);
            }
        }

        private class HeapElementPrinter
        {
            private StateImpl state;
            public FieldTraverser nodeTrav;
            public FieldTraverser edgeTrav;
            private int offset;

            public HeapElementPrinter(StateImpl s)
            {
                state = s;
                nodeTrav = new NodePrinter(this);
                edgeTrav = new EdgePrinter(this);
            }

            private void OnEdge(Pointer ptr)
            {
                offset++;
                System.Console.WriteLine("\t Offset[{0}] -> Ptr {1}", offset, ptr);
            }

            private void OnNode(Pointer ptr)
            {
                if (ptr == 0u)
                {
                    return;
                }
                //              HeapEntry he = state.GetHeapEntryFromPointer(ptr);
                HeapEntry he = (HeapEntry)state.Heap[ptr];
                HeapElement helem = he.HeapObj;
                System.Console.WriteLine("heap[{0}] = (dirty:{1}, canonId:{3}, fingerprint:{2})",
                    ptr, helem.IsDirty, helem.fingerprint, helem.canonId);
                offset = 0;
            }

            private class EdgePrinter : FieldTraverser
            {
                private HeapElementPrinter parent;

                public EdgePrinter(HeapElementPrinter p)
                {
                    parent = p;
                }

                public override void DoTraversal(Object field)
                {
                    throw new InvalidOperationException("HeapElementPrinter should never traverse non-pointers");
                }

                public override void DoTraversal(Pointer ptr)
                {
                    parent.OnEdge(ptr);
                }
            }

            private class NodePrinter : FieldTraverser
            {
                private HeapElementPrinter parent;

                public NodePrinter(HeapElementPrinter p)
                {
                    parent = p;
                }

                public override void DoTraversal(Object field)
                {
                    throw new InvalidOperationException("HeapElementPrinter should never traverse non-pointers");
                }

                public override void DoTraversal(Pointer ptr)
                {
                    parent.OnNode(ptr);
                }
            }
        }

        public static void PrintHeapElements(StateImpl state)
        {
            HeapElementPrinter hep = new HeapElementPrinter(state);
            TraverseHeap(state, hep.edgeTrav, hep.nodeTrav);
            System.Console.WriteLine("============================================");
        }
    }
}