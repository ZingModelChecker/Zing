#define DISABLE_INC_FINGERPRINTS    // Turns off Inc Fingerprinting, defaults to Noninc & Iosif's algo
//#define DEBUG_INC_FINGERPRINTS      // This checks on each cache hit, if cached fingerprint == uncached
//#define PRINT_FINGERPRINTS

//#define STATS_INC_FINGERPRINTS      // Write stats about Inc Fingeprinting every 1000 traversals
#define REUSE_CANONS                // An  improvement of the inc fingerprint algorithm

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Zing
{
    /// <summary>
    /// HeapCanonicalizer encodes all the functionalities of heap canonicalization.
    /// StateImpl forwards all heap canonicalization related functionalities to this class. 
    /// 
    /// The heap canonicalization itself is performed by the HeapCanonicalizationAlgorithm class. 
    ///   Currently there are two implemented algorithms: the Iosif algorithm and the Incremental algorithm
    ///
    /// Apart from its own state, Heap Canonicalizer is responsible for the following fields
    ///    HeapElement.canonId     :  The canonical Id for a particular heap element
    ///    HeapElement.fingerprint :  The (cached) fingerprint of the heap element
    ///                               Note: This fingerprint is a function of:
    ///                                    * Contents of all non-reference fields of heap element
    ///                                    * CanonIds of all reference fields
    ///                                    * Its own canonId
    ///                                    
    ///   These two fields are 'essential' fields of a heap element. Thus, a heap element gets dirty when these
    ///   fields change. (There is no need to clone the entire object in such a case - TODO for the future) 
    ///                                    
    /// Additionally, the heap canonicalizer maintains these TEMPORARY fields
    ///    HeapEntry.currCanonId : CanonId in the current traversal
    ///    HeapEntry.fingerprint : Fingeprint in the current traversal
    ///    
    ///  Obviously, these fields are valid only during the heap traversal
    /// 
    /// The fingerprint computation starts with a call to HeapCanonicalizer.OnFingerprintStart() and ends with
    /// HeapCanonicalizer.OnFingerprintEnd(). During the computation, the heap canonicalizer computes he.canonId 
    /// using the HeapCanonicalizationAlgorithm. Note, helem.canonId reflects the 'old' canonId before the current
    /// traversal
    /// 
    /// </summary>

    internal class HeapCanonicalizer
    {
        
#if DISABLE_INC_FINGERPRINTS
        private IosifAlgorithm algorithm;
#else
		private IncrementalAlgorithm algorithm;
#endif

        public HeapCanonicalizer()
        {
           
#if DISABLE_INC_FINGERPRINTS
            algorithm = new IosifAlgorithm(this);
#else
			algorithm = new IncrementalAlgorithm(this);
#endif
        }

        public HeapCanonicalizer(int SerialNum) : this()
        {
            this.SerialNumber = SerialNum;
        }

        # region Per Fingerprinting Traversal State
        // The following state is maintained during a fingerprint traversal
        // and should be cleared at the end of the traversal. 

        // The list heap objects whose canonicalization state (canonId and fingerprint)
        // have changed during this traversal
        private Queue dirtyHeapEntries = new Queue();

        //The queue of pending heap objects that need to be traversed during this
        //fingerprinting traversal
        //   --- This corresponds to StateImpl.sortedHeap in the old implementation
        private Queue pendingHeapEntries = new Queue(512);
        //private ArrayQueue pendingHeapEntries = new ArrayQueue(512);

        // number of heap objects traversed during this traversal
        // this member is incremented whenever a new heap object is added to pendingHeapEntries
        private uint numHeapEntries;

        // The canonical id of the object that is being traversed
        // currentParentId == 0 means that the global objects are being traversed
        private int currentParentId;

        // The offset of the child reference that is being traversed for the currentParentId object
        //  This field is incremented by GetCanonicalId
        private int currentFieldId;

        private int currentOffset;

#if STATS_INC_FINGERPRINTS
		private struct IncFingerprintStats
		{
			public int numHeapTraversals;
			public int numHeapEntries;
			public int numCacheHits;
			public int numDirtyEntries;
			public int numDirtyCanonIds;
			public int numDirtyRefs;
			public int numCanonIdCacheHits;
			public int numZeroCanonIds;
			public int numCanonIdReuses;
			public int numCanonIdMutations;
		}
		private IncFingerprintStats stats;
		private IncFingerprintStats oldStats;
		internal void IncCanonIdMutations()
		{
			stats.numCanonIdMutations++;			
		}
		internal void IncCanonIdReuses()
		{
			stats.numCanonIdReuses++;			
		}
#endif

        #endregion

        //---- Old Code
        // To avoid having to clear the HeapEntry.Order fields in before WriteString
        // (which turns out to be VERY expensive), we using an incrementing order
        // base number. Any order values less than orderBase are effectively zero.
        // When WriteString completes, we bump up orderBase by the number of order
        // values consumed. We have 2^32 order numbers to work with. For a model
        // with 1000 heap elements, we could explore over 4M states, which would be
        // a lot for a model that large. If we eventually scale up to this, we'll
        // see an exception here and we can bump the order field to a ulong.
        //
        //private static uint orderBase = 0;
        //private static uint nextOrderBase = 0;
        //
        //---- Old Code

        // Fields orderBase and nextOrderBase are not necessary as we keep a table of seen pointers
        // and clear this table after every traversal 
        //                 --- madanm 
        //
        //		Hashtable seenPtrs = new Hashtable();
        private bool[] seenPtrs;

        private bool traversingHeap;

        public int SerialNumber;

        public void OnHeapTraversalStart(StateImpl state)
        {
            //Debug.Assert(!traversingHeap); // VS fails this assert during debugging, for unknown reasons
            traversingHeap = true;
            currentParentId = 0;
            currentFieldId = 0;
            currentOffset = 0;
            if (state.Heap != null)
            {
                seenPtrs = new bool[state.Heap.Length];
            }
            else
            {
                seenPtrs = null;
            }
            algorithm.OnStart();
        }

        public void OnHeapTraversalEnd(StateImpl state)
        {
            //Debug.Assert(traversingHeap);
            algorithm.OnEnd();

            traversingHeap = false;
            numHeapEntries = 0;
            currentOffset = 0;
            currentFieldId = 0;
            currentParentId = 0;

            // Doing Garbage Collection;
            if (state.Heap != null)
            {
                Debug.Assert(seenPtrs != null);
                for (int ptr = 0; ptr < seenPtrs.Length; ptr++)
                {
                    if (state.Heap[ptr] != null && !seenPtrs[ptr])
                    {
                        state.MarkGarbage((Pointer)((uint)ptr));
                    }
                }
                seenPtrs = null;
            }

            // Commit in all the canonIds of dirty heap entries
            while (dirtyHeapEntries.Count > 0)
            {
                HeapEntry he = (HeapEntry)dirtyHeapEntries.Dequeue();
                HeapElement helem = he.heList;

                // Since we are modifying the canonId and the fingerprint
                // we need to make this object dirty
                helem.SetDirty();

                helem.canonId = he.currCanonId;
                if (he.currFingerprint != null)
                {
                    helem.fingerprint = he.currFingerprint;
                }
                he.currCanonId = 0;
                he.currFingerprint = null;
            }

#if STATS_INC_FINGERPRINTS
			stats.numHeapTraversals++;
			
			if(stats.numHeapTraversals%5000 == 0)
			{
				System.Console.Write("#Travs: {0}, ", stats.numHeapTraversals);
				System.Console.Write("#HElems: {0}, ", stats.numHeapEntries);
				System.Console.Write("#Hits: {0:f}, ", stats.numCacheHits*1.0/stats.numHeapEntries*100);
				System.Console.Write("Misses = [dirty: {0:f}, ", stats.numDirtyEntries*1.0/ stats.numHeapEntries*100);
				System.Console.Write("childRefs: {0:f}, ", stats.numDirtyRefs*1.0/ stats.numHeapEntries*100);
				System.Console.Write("canonIds: {0:f} ", stats.numDirtyCanonIds*1.0/ stats.numHeapEntries*100);
				System.Console.Write("(zero: {0:f}) ] ", stats.numZeroCanonIds*1.0/ stats.numHeapEntries*100);
				System.Console.Write("#CanonHits: {0:f}, ", stats.numCanonIdCacheHits*1.0/ stats.numHeapEntries*100);
				System.Console.Write("#CanonReuses: {0:f}, ", stats.numCanonIdReuses);
				System.Console.Write("#CanonMutations: {0:f}, ", stats.numCanonIdMutations);
				System.Console.Write("unused: {0}", algorithm.unusedId);
				System.Console.WriteLine();
				System.Console.Write("Diffs: {0,5} ", stats.numHeapTraversals);
				System.Console.Write("{0,5} ", stats.numHeapEntries - oldStats.numHeapEntries);
				System.Console.Write("{0,5} ", stats.numDirtyEntries - oldStats.numDirtyEntries);
				System.Console.Write("{0,5} ", stats.numDirtyCanonIds - oldStats.numDirtyCanonIds);
				System.Console.Write("{0,5} ", stats.numDirtyRefs - oldStats.numDirtyRefs);
				System.Console.WriteLine();
				oldStats = stats;
			}
#endif

            // make sure the per traversal state is clean for the next traversal 
            Debug.Assert(dirtyHeapEntries.Count == 0);
            Debug.Assert(pendingHeapEntries.Count == 0);
            Debug.Assert(numHeapEntries == 0);
            Debug.Assert(currentParentId == 0);
            Debug.Assert(currentFieldId == 0);
            Debug.Assert(currentOffset == 0);
        }


        #region Entry points from StateImpl

        /// <summary>
        /// Get the canonicalized version of a particular heap object
        /// 
        /// When GetCanonicalId is called for a particular heap object
        /// for the first time in a traversal, the heap object is inserted
        /// in the pendingHeapEntries queue for future traversals. 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        internal uint GetCanonicalId(StateImpl state, Pointer p)
        {
            //Debug.Assert(traversingHeap);

            uint ptr = (uint)p;
            currentFieldId++;

            if (ptr == 0u) return 0;

            HeapEntry he = state.Heap[ptr];
            Debug.Assert(he != null);

            if (!seenPtrs[ptr])
            {
                pendingHeapEntries.Enqueue(he);
                numHeapEntries++;
                seenPtrs[ptr] = true;

                HeapElement helem = he.HeapObj;
                he.currCanonId = algorithm.CanonId(currentParentId, currentFieldId, helem.canonId);

#if !DISABLE_INC_FINGERPRINTS
			    if(he.currCanonId != helem.canonId)
				{
					// commit this new information at the end of this heap traversal
					dirtyHeapEntries.Enqueue(he);
				}
#endif
            }
            return (uint)he.currCanonId;
        }


        /// <summary>
        /// Write the contents of all the heap objects in a StateImpl
        /// </summary>
        /// <param name="state"></param>
        /// <param name="bw"></param>
        internal void WriteString(StateImpl state, BinaryWriter bw)
        {
            Debug.Assert(traversingHeap);

            if (pendingHeapEntries.Count != 0)
            {
                // During the course if this loop, we may encounter additional, new
                // heap references, which will cause pendingHeapEntries.Count to increase.
                while (pendingHeapEntries.Count != 0)
                {
                    // Using Queue for pendingHeapEntries results in a breadth-first traversal of the heap
                    HeapEntry he = (HeapEntry)pendingHeapEntries.Dequeue();

                    currentParentId = he.currCanonId;
                    currentFieldId = 0;

                    HeapElement helem = he.HeapObj;
                    helem.WriteString(state, bw);
                }

                bw.Write((short)numHeapEntries);

            }
            else
                bw.Write((int)0);
        }

        /// <summary>
        /// ComputeFingerprint computes the fingerprint of the state of heap in a given StateImpl
        /// after performing heap canonicalization.
        /// This function assumes that StateImpl has already computed the fingerprints of the globals
        /// and the process states.  
        /// Thus pendingHeapEntries already contains all the heap objects referenced by these 'root' states
        /// </summary>
        /// <param name="state">The StateImpl containing the heap</param>
        /// <returns>Fingerprint of the heap state in StateImpl</returns>
        internal Fingerprint ComputeHeapFingerprint(StateImpl state)
        {
            Fingerprint heapPrint = new Fingerprint();
            if (pendingHeapEntries.Count != 0)
            {
                // During the course if this loop, we may encounter additional, new
                // heap references, which will cause pendingHeapEntries to increase in size
                while (pendingHeapEntries.Count != 0)
                {
                    // Using Queue for pendingHeapEntries results in a breadth-first traversal of the heap
                    HeapEntry he = (HeapEntry)pendingHeapEntries.Dequeue();

                    currentParentId = he.currCanonId;
                    currentFieldId = 0;
                    Fingerprint hePrint = FingerprintHeapEntry(state, he);

                    heapPrint.Concatenate(hePrint);
                }

#if DISABLE_INC_FINGERPRINTS
                // Add the heap count to the fingerprint, this is for bacward comapitibility with WriteString
                // But, when we do inc fingerprinting, there is no place to add this extra information (the heap is
                // no longer contiguous), so we give up compatibility with WriteString

                short heapCount = (short)numHeapEntries;

                byte[] len = new byte[2];
                // the order is Little Endian to match the corresponding
                //		bw.Write((short) pendingHeapEntries.Count);
                // in the WriteString method. 
                len[1] = (byte)((heapCount >> 8) & 0xff);
                len[0] = (byte)((heapCount) & 0xff);

                Fingerprint lenPrint = computeHASH.GetFingerprint(len, 2, currentOffset);
                currentOffset += 2;
                heapPrint.Concatenate(lenPrint);
#endif
            }
            else
            {
                // add in a zero when there are no heap elements for backward compatibility
                byte[] zer = new byte[4];
                zer[0] = 0;
                zer[1] = 0;
                zer[2] = 0;
                zer[3] = 0;

                Fingerprint zeroPrint = computeHASH.GetFingerprint(zer, 4, currentOffset);
                currentOffset += 4;
                heapPrint.Concatenate(zeroPrint);
            }

            return heapPrint;
        }

        #endregion

        // Compute the fingerprint of a heap entry
        // If a cached fingerprint is present and valid, this function returns this value
        //  otherwise, this function calls FingerprintHeapEntryUncached
        private Fingerprint FingerprintHeapEntry(StateImpl state, HeapEntry he)
        {
            HeapElement helem = he.HeapObj;

#if DISABLE_INC_FINGERPRINTS
            helem.fingerprint = FingerprintHeapEntryUncached(state, he);
            return helem.fingerprint;
#endif
#if STATS_INC_FINGERPRINTS
			stats.numHeapEntries ++;
#endif

            // helem.fingerprint is valid if all the following are true
            //      * it is first present
            //      * helem is not dirty
            //      * If helem's canonId did not change
            //      * If the canonId of all its references have not changed
            if (helem.fingerprint != null && !helem.IsDirty && helem.canonId == he.currCanonId)
            {
                bool dirtyRefs = CheckDirtyReferences(state, helem);
#if STATS_INC_FINGERPRINTS
				if(dirtyRefs) 
					stats.numDirtyRefs++;
				else
					stats.numCacheHits++;										
#endif
                if (!dirtyRefs)
                {
#if DEBUG_INC_FINGERPRINTS
					int cachedOffset = helem.fingerprintedOffset;
					byte[] cachedBuffer = helem.fingerprintedBuffer;
					Fingerprint cached = helem.fingerprint;
					Fingerprint uncached = FingerprintHeapEntryUncached(state, he);
					byte[] uncachedBuffer = helem.fingerprintedBuffer;
					int uncachedOffset = helem.fingerprintedOffset;
					if(!uncached.Equals(cached))
					{
						System.Console.WriteLine("Incremental Fingerprinting Is Not Working");
						System.Console.Write("{0} @{1} : ", cached, cachedOffset);
						for(int i=0; i<cachedBuffer.Length; i++) System.Console.Write("{0} ", cachedBuffer[i]);
						System.Console.WriteLine();
						System.Console.Write("{0} @{1} : ", uncached, uncachedOffset);
						for(int i=0; i<uncachedBuffer.Length; i++) System.Console.Write("{0} ", uncachedBuffer[i]);
						System.Console.WriteLine();

						System.Diagnostics.Debugger.Break();
					}
#endif
                 return helem.fingerprint;
                }

            }

#if STATS_INC_FINGERPRINTS
			if(helem.IsDirty) stats.numDirtyEntries ++;
			if(helem.canonId != he.currCanonId)
			{
				if(helem.canonId == 0)
				{
					stats.numZeroCanonIds++;
				}
				//System.Console.WriteLine("DirtyCanonId from {0} to {1}", helem.canonId, he.currCanonId);
				stats.numDirtyCanonIds++;
			}
#endif

            he.currFingerprint = FingerprintHeapEntryUncached(state, he);

            // he.currFingerprint will be committed to helem.fingerprint at the end of the
            // fingerprint traversal			
            // if he.canonId != helem.canonId, the heap entry is already in the dirtyHeapEntries queue
            if (he.currCanonId == helem.canonId)
            {
                dirtyHeapEntries.Enqueue(he);
            }

            // All info necessary for incremental fingerprinting is cached by the following function
            // this greatly improves performance
            UpdateFingerprintCache(helem);

            return he.currFingerprint;
        }

        private static MemoryStream[] memStream = new MemoryStream[Options.DegreeOfParallelism];
        private static BinaryWriter[] binWriter = new BinaryWriter[Options.DegreeOfParallelism];

        // Gives the fingerprint offset for each canonId
        //   Hashtable for integers is expensive due to boxing overhead 
        //   TODO in the future
        private Hashtable OffsetMap = new Hashtable();

        private const int startHeapOffset = 8096;

        public Fingerprint FingerprintNonHeapBuffer(byte[] buffer, int len)
        {
            if (currentOffset + len >= startHeapOffset)
            {
                Debug.Assert(false, "Recompile with a larger startHeapOffset");
                throw new ArgumentException("buffer too large - recompile with a larger startHeapOffset");
            }
            Fingerprint res = computeHASH.GetFingerprint(buffer, len, currentOffset);
            currentOffset += len;
            return res;
        }

        private Fingerprint FingerprintHeapEntryUncached(StateImpl state, HeapEntry he)
        {
            HeapElement helem = he.HeapObj;

            if (memStream[SerialNumber] == null)
            {
                memStream[SerialNumber] = new MemoryStream();
                binWriter[SerialNumber] = new BinaryWriter(memStream[SerialNumber]);
            }
            binWriter[SerialNumber].Seek(0, SeekOrigin.Begin);

            currentFieldId = 0;
            helem.WriteString(state, binWriter[SerialNumber]);

            int objLen = (int)memStream[SerialNumber].Position;
            int offset;

#if DISABLE_INC_FINGERPRINTS
            offset = currentOffset;
            currentOffset += objLen;
#else
			if(OffsetMap.Contains(he.currCanonId))
			{
				offset = (int)OffsetMap[he.currCanonId];	
			}
			else
			{
				//generate an offset map
				offset = unusedOffset;
				unusedOffset += objLen;
				OffsetMap[he.currCanonId] = offset;
#if DEBUG_INC_FINGERPRINTS
				System.Console.WriteLine("OffsetTable[{0}] = {1}", he.currCanonId, offset);
#endif
			}
#endif
            Fingerprint ret = computeHASH.GetFingerprint(memStream[SerialNumber].GetBuffer(), objLen, offset);

#if DEBUG_INC_FINGERPRINTS
			helem.fingerprintedOffset = offset;
			helem.fingerprintedBuffer = new byte[objLen];
			Array.Copy(memStream.GetBuffer(), 0, helem.fingerprintedBuffer, 0, objLen);
#endif
#if PRINT_FINGERPRINTS
			if(StateImpl.printFingerprintsFlag)
			{
				System.Console.Write("@{0} ", offset); 
				state.PrintFingerprintBuffer(memStream.GetBuffer(), objLen, true);
				System.Console.WriteLine("{0}", ret);
			}
#endif
            return ret;

        }

        // This version of dirty references, uses the cache of childReferences
        // in helem. This is much faster than doing an object traversal
        private static bool CheckDirtyReferences(StateImpl state, HeapElement helem)
        {
            Debug.Assert(helem.childReferences != null);

            Pointer[] refs = helem.childReferences;
            for (int i = 0; i < refs.Length; i++)
            {
                Pointer ptr = refs[i];

                // this call for GetCanonicalId is absolutely necessary: 
                //       to mark the child objectss as reachable, 
                //       and update their canonId if necessary
                // To get the currentFieldIds correct, we need to make this call before the null check
                state.GetCanonicalId(ptr);

                if (ptr == 0u) continue;

                HeapEntry he = state.Heap[ptr];

                if (he.currCanonId != he.HeapObj.canonId)
                {
                    return true;
                }
            }
            return false;
        }

        private ChildRefGenerator childRefGenerator;

        private void UpdateFingerprintCache(HeapElement helem)
        {
            if (childRefGenerator == null)
                childRefGenerator = new ChildRefGenerator();
            helem.TraverseFields(childRefGenerator);
            helem.childReferences = childRefGenerator.GetChilRef();
        }

        private class ChildRefGenerator : FieldTraverser
        {
            public ArrayList children = new ArrayList();
            public int numSymFields;

            public ChildRefGenerator() { }

            public override void DoTraversal(Object field) { /* nothing */ }
  
            public override void DoTraversal(Pointer ptr)
            {
                children.Add(ptr);
            }

            public Pointer[] GetChilRef()
            {

                Pointer[] ret = new Pointer[children.Count];
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = (Pointer)children[i];
                }
                children.Clear();
                return ret;
            }

            public int GetNumSymFields()
            {
                int ret = numSymFields;
                numSymFields = 0;
                return ret;
            }
        }

        #region Heap Canonicalization Algorithms

        /*
		// This is the "interface" for the canonicalization algorithm
		//  This is just for documentation and reference, 
		//  To avoid the virtual function call, the algorithms do not derive from this interface
		private interface HeapCanonicalizationAlgorithm
		{
			void OnStart();
			CanonInfo CanonId(HeapEntry he, uint parentId, uint fieldId, uint oldCanonId);
			void OnEnd();
		}
		*/

        private class IosifAlgorithm
        {
            private HeapCanonicalizer heapCanonicalizer;
            private int heapCount;
            public IosifAlgorithm(HeapCanonicalizer parent)
            {
                heapCanonicalizer = parent;
            }
            public void OnStart()
            {
                heapCount = 0;
            }

            public int CanonId(int parentId, int fieldId, int oldCanonId)
            {
                heapCount++;
                return heapCount;
            }
            public void OnEnd()
            {
                // nothing - sleep tight
            }
        }

		private class IncrementalAlgorithm 
		{
			private HeapCanonicalizer heapCanonicalizer;
			private ArrayList canonTable = ArrayList.Repeat(null,1024);
			public int unusedId = 1;
		
			public IncrementalAlgorithm(HeapCanonicalizer parent)
			{	
				heapCanonicalizer = parent;
			}
#if REUSE_CANONS
			bool[] seen = new bool[1024];
#endif
			
			public void OnStart()
			{
				// nothing here
			}

			public int CanonId(int parentId, int fieldId, int oldCanonId)
			{
				if(parentId >= canonTable.Count)
				{
					for(int i=canonTable.Count; i<=parentId; i++)
						canonTable.Add(null);
					Debug.Assert(canonTable.Count > parentId);
				}
				if(canonTable[parentId] == null)
				{
					int[] newFieldArray = new int[4];
					newFieldArray.Initialize();
					canonTable[parentId] = newFieldArray;
				}

				int[] fieldArray = (int[])canonTable[parentId];
				if(fieldId >= fieldArray.Length)
				{
					int newLength = fieldArray.Length * 2;
					while(fieldId >= newLength)
					{
						newLength *= 2;
					}
					int[] newFieldArray = new int[newLength];
					Array.Copy(fieldArray, 0, newFieldArray, 0, fieldArray.Length);
					Array.Clear(newFieldArray, fieldArray.Length, newFieldArray.Length-fieldArray.Length);
					canonTable[parentId] = newFieldArray;
					fieldArray = newFieldArray;
				}
				if(fieldArray[fieldId] == 0)
				{
#if REUSE_CANONS
					if(oldCanonId!=0)
					{
#if STATS_INC_FINGERPRINTS
						heapCanonicalizer.IncCanonIdReuses();
						System.Console.WriteLine("{0} canon for {1},{2} (reuse)", oldCanonId, parentId, fieldId);
#endif
						fieldArray[fieldId] = oldCanonId;
					}
					else
					{
						fieldArray[fieldId] = unusedId++;
					}
#else
					fieldArray[fieldId] = unusedId++;
#endif
				}

#if REUSE_CANONS
				// The returnId is fieldArray[fieldId]
				// should ensure that this is not seen in the current state
				if(fieldArray[fieldId] < seen.Length && seen[fieldArray[fieldId]])
				{
					// mutation
					fieldArray[fieldId] = unusedId++;
#if STATS_INC_FINGERPRINTS
					System.Console.WriteLine("{0} canon for {1},{2} (mutation)", unusedId-1, parentId, fieldId);
					heapCanonicalizer.IncCanonIdMutations();
#endif
				}

				if(fieldArray[fieldId] >= seen.Length)
				{
					int newLength = seen.Length * 2;
					while(fieldArray[fieldId] >= newLength)
					{	
						newLength *= 2;
					}		
					bool[] newSeen = new bool[newLength];
					Array.Copy(seen, 0, newSeen, 0, seen.Length);
					seen = newSeen;
				}
				seen[fieldArray[fieldId]] = true;					
#endif
				return fieldArray[fieldId];
			}

			public void OnEnd()
			{
#if REUSE_CANONS
				Array.Clear(seen, 0, seen.Length);
#endif
			}
		}
						
//		#endregion
	}

	internal class ArrayQueue
	{
		// Queue based on a Array - A slightly faster implementation than the one in standard library
		// At this point, it is not clear if using ArrayQueue (over ArrayList) buys us anything --- madanm
		// 
		
		// objList forms a circular queue from head to tail
		private Object[] objList;
		private int head;
		private int tail;
		private bool empty;
	
		// Class Invariant:
		//  head, tail \in [0 .. objList.Length-1] provided objList.Length != 0
		//   empty => head == tail
		//   !empty =>
		//          head points to the first element of the queue
		//          tail points right after the last element in the queue
		//

#if false
		void CheckInvariant()
		{
			Debug.Assert(objList.Length == 0 || 0 <= head && head <= objList.Length-1);
			Debug.Assert(objList.Length == 0 || 0 <= tail && tail <= objList.Length-1);
			Debug.Assert(!empty || head == tail);
		}
#endif

		private void InitializeQueue(int capacity)
		{
			objList = new Object[capacity];
			head = 0;
			tail = 0;
			empty = (capacity != 0); // if capacity is zero, we are full - otherwise we are empty

#if false
			CheckInvariant();
#endif
		}

		private void DoubleCapacity()
		{
			Debug.Assert(tail == head && !empty);

			int newCapacity = objList.Length * 2;
			if(newCapacity == 0) newCapacity = 4;
			Object[] newList = new Object[newCapacity];
			System.Console.WriteLine("Doubling to {0}", newCapacity);

			Array.Copy(objList, head, newList, 0, objList.Length-head);
			if(head != 0)
			{
				Array.Copy(objList, 0, newList, objList.Length-head, head);
			}
			head = 0;
			tail = objList.Length;
			objList = newList;

#if false
			CheckInvariant();
#endif
        }

		public ArrayQueue()
		{
			InitializeQueue(0);
		}

		public ArrayQueue(int capacity)
		{
			InitializeQueue(capacity);
		}

#if UNUSED
		public bool IsEmpty(){return empty;}
#endif

		public void Enqueue(Object obj)
		{
			if(tail == head && !empty)
			{
				// we are full
				DoubleCapacity();
			}
			objList[tail] = obj;
			tail++;
			if(tail >= objList.Length)
			{
				tail = 0;
			}
			empty = false;

#if false
			CheckInvariant();
#endif
        }

		public Object Dequeue()
		{
			if(empty)
			{
                throw new InvalidOperationException();
			}

			Object ret = objList[head];
			objList[head] = null;

			head++;
			if(head >= objList.Length)
			{
				head = 0;
			}
			if(head == tail)
			{
				empty = true;
			}

#if false
			CheckInvariant();
#endif
            return ret;
		}

		public int Count 
		{ 
			get
			{
				if(empty) return 0;
				if(head < tail) return tail - head;
				else return objList.Length + tail - head;
			}
		}
	}
}

#endregion