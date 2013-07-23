using System;
using System.Collections.Generic;
using System.Compiler;
using System.CodeDom.Compiler;
using System.Diagnostics;

#if UNUSED  // The splicer no longer uses the optimizer because it breaks summarization
namespace Microsoft.Zing
{
    internal sealed class BBOptimizer
    {
        internal static List<BasicBlock> Optimize(List<BasicBlock> blocks)
        {
            foreach (BasicBlock b in blocks)
            {
                BasicBlock target = b.UnconditionalTarget;

                // Look for:
                //  1. an unconditional atomic branch to the target block
                //  2. first block has no statements
                //  3. target block is referenced by nobody else
                //
                // If found:
                //   Move the contents of the target block to the first block
                //
                if (b.ConditionalTarget == null && target != null &&
                    (b.RelativeAtomicLevel > 0 || b.MiddleOfTransition) &&
                    (b.Statement == null || ((b.Statement is Block) && ((Block)b.Statement).Statements.Length == 0)) &&
                    RefCount(blocks, target) == 1)
                {
                    //
                    // Copy (almost all) the fields of the target to the first block,
                    // leaving the target unreachable.
                    //
                    b.ConditionalExpression = target.ConditionalExpression;
                    b.ConditionalTarget = target.ConditionalTarget;
                    b.MiddleOfTransition = target.MiddleOfTransition;
                    b.PropagatesException = target.PropagatesException;
                    b.RelativeAtomicLevel = target.RelativeAtomicLevel;
					b.IsAtomicEntry = target.IsAtomicEntry;
                    b.selectStmt = target.selectStmt;
                    b.SkipNormalizer = target.SkipNormalizer;
                    b.SourceContext = target.SourceContext;
                    b.Statement = target.Statement;
                    b.UnconditionalTarget = target.UnconditionalTarget;

                    // Merge or copy block attributes, as necessary
                    if (b.Attributes == null)
                        b.Attributes = target.Attributes;
                    else
                    {
                        if (target.Attributes != null)
                        {
                            for (int i=0, n=target.Attributes.Length; i < n ;i++)
                                b.Attributes.Add(target.Attributes[i]);
                        }
                    }
                }
            }

            //
            // Some blocks will now be unreachable. Return a revised block list
            // containing only reachable blocks.
            //
            // TODO: make this reachability test more correct.
            //
            List<BasicBlock> optBlocks = new List<BasicBlock>();

            foreach (BasicBlock b in blocks)
            {
                if (b.IsEntryPoint || RefCount(blocks, b) > 0)
                    optBlocks.Add(b);
            }

            return optBlocks;
        }

        private static int RefCount(List<BasicBlock> blocks, BasicBlock block)
        {
            int refcnt = 0;

            foreach (BasicBlock b in blocks)
            {
                if (b.UnconditionalTarget == block)
                    refcnt++;
                if (b.ConditionalTarget == block)
                    refcnt++;
                if (b.handlerTarget == block)
                    refcnt++;
            }

            return refcnt;
        }

        private static void Redirect(List<BasicBlock> blocks, BasicBlock oldBlock, BasicBlock newBlock)
        {
            foreach (BasicBlock b in blocks)
            {
                if (b.UnconditionalTarget == oldBlock)
                    b.UnconditionalTarget = newBlock;
                if (b.ConditionalTarget == oldBlock)
                    b.ConditionalTarget = newBlock;
            }
        }
    }
}
#endif