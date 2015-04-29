using System;
using System.Collections.Generic;
using System.Compiler;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Zing
{
    internal sealed class BBSplitter : System.Compiler.StandardVisitor
    {
        internal BBSplitter(Splicer splicer)
        {
            this.splicer = splicer;
            branchTargets = new TrivialHashtable();
            blockList = new List<BasicBlock>();
            continuationStack = new Stack<BasicBlock>();
            scopeStack = new Stack<Scope>();
            handlerBlocks = new Stack<BasicBlock>();
        }

        private Stack<Scope> scopeStack;

        // The Splicer holds information that we need here about referenced labels
        private Splicer splicer;

        // branchTargets maps the uniqueKey of a referenced label to its BasicBlock
        // object. Entries are created either in VisitBlock or in VisitBranch (in
        // the case of a forward reference).
        private TrivialHashtable branchTargets;

        //
        // Track branches that occur in atomic blocks so we can see later if they
        // need to be patched up (because they turn out to leave the atomic block).
        //
        internal List<BasicBlock> atomicBranches;

        private List<BasicBlock> blockList;
        private int nextBlockId;
        private bool insideAtomicBlock;

        private BasicBlock AddBlock(BasicBlock block)
        {
            block.Id = nextBlockId++;
            block.Scope = this.scopeStack.Peek();

            if (insideAtomicBlock)
                block.RelativeAtomicLevel = 1;
            blockList.Add(block);
            return block;
        }

        //
        // Helpers to manage the current continuation point as we work backward
        // from the end of a method to the entry point.
        //
        private Stack<BasicBlock> continuationStack;

        private BasicBlock CurrentContinuation
        {
            get
            {
                return continuationStack.Peek();
            }
            set
            {
                continuationStack.Pop();
                continuationStack.Push(value);
            }
        }

        private void PushContinuationStack()
        {
            continuationStack.Push(continuationStack.Peek());
        }

        private void PushContinuationStack(BasicBlock block)
        {
            continuationStack.Push(block);
        }

        private BasicBlock PopContinuationStack()
        {
            return continuationStack.Pop();
        }

        //
        // Helpers to manage a stack of exception contexts. We push a new context
        // during the body of a "try" block (but not its handlers). While catching
        // an exception, the CurrentHandlerBlock always points to the next outer
        // try block, or null if there's no outer block in the current method.
        //
        private Stack<BasicBlock> handlerBlocks;

        private void PushExceptionContext(BasicBlock handler)
        {
            handlerBlocks.Push(handler);
        }

        private void PopExceptionContext()
        {
            handlerBlocks.Pop();
        }

        private BasicBlock CurrentHandlerBlock
        {
            get
            {
                if (handlerBlocks.Count == 0)
                    return null;
                else
                    return handlerBlocks.Peek();
            }
        }

        //
        // This is the primary entry point into BBSplitter. This is called from the
        // Splicer to do basic block analysis before using the Normalizer to do code-gen
        // for each basic block.
        //
        public static List<BasicBlock> SplitMethod(ZMethod method, Splicer splicer)
        {
            BBSplitter splitter = new BBSplitter(splicer);

            splitter.atomicBranches = new List<BasicBlock>();
            splitter.scopeStack.Push(method.Body.Scope);

            BasicBlock finalBlock = splitter.AddBlock(new BasicBlock(null));

            if (method.Atomic)
            {
                //
                // For summarization, we need for atomic methods to have an "extra"
                // terminating block. This corresponds to what happens in VisitAtomic
                // for regular atomic blocks.
                //
                finalBlock = splitter.AddBlock(new BasicBlock(null, finalBlock));
                finalBlock.MiddleOfTransition = true;

                splitter.insideAtomicBlock = true;
            }

            splitter.PushContinuationStack(finalBlock);

            splitter.VisitStatementList(method.Body.Statements);

            // For each branch within the atomic block, we need to look at the target
            // and see if it too is within an atomic block (by definition, the same
            // one). If not, then we need to make the branch's block be the effective
            // end of the atomic execution. We can do this by resetting its atomicity
            // level
            foreach (BasicBlock b in splitter.atomicBranches)
            {
                // The actual branch target is two blocks away because branch targets
                // always introduce an extra block that flow atomically into "real" code.
                BasicBlock branchTarget;
                if (b.UnconditionalTarget.IsReturn)
                    continue;

                Debug.Assert(b.UnconditionalTarget.UnconditionalTarget != null);
                branchTarget = b.UnconditionalTarget.UnconditionalTarget;

                // If the target isn't atomic, then we need to introduce an interleaving
                // just after the branch. We do this by forcing it to be the end of
                // the atomic block.  We must also make sure such a block cannot be the entry
                // point of the atomic block.
                if (branchTarget.RelativeAtomicLevel == 0)
                {
                    b.RelativeAtomicLevel = 0;
                    b.IsAtomicEntry = false;
                }
            }

            BasicBlock firstBlock = splitter.PopContinuationStack();

            firstBlock.IsEntryPoint = true;
            if (firstBlock.RelativeAtomicLevel > 0)
                firstBlock.IsAtomicEntry = true;

            return splitter.blockList;
        }

        public override Block VisitBlock(Block block)
        {
            if (block == null) return null;
            this.scopeStack.Push(block.Scope);
            this.VisitStatementList(block.Statements);
            this.scopeStack.Pop();
            return block;
        }

        public override StatementList VisitStatementList(StatementList stmts)
        {
            if (stmts == null || stmts.Count == 0)
            {
                if (insideAtomicBlock)
                    return null;

                // An empty block introduces a full-fledged block if we aren't inside
                // an atomic region. The empty statement (i.e. ";") is translated as
                // a Block with a null statement list.

                BasicBlock b = AddBlock(new BasicBlock(null, CurrentContinuation));
                CurrentContinuation = b;
            }
            else if (insideAtomicBlock)
            {
                // break statements up into coalescable regions
                // enter/exit-atomicscope flags are handled by the visitAtomic
                // function
                int regionsize = 0;

                for (int i = stmts.Count - 1; i >= -1; i--)
                {
                    if (i == -1 || !IsCoalescableStmt(stmts[i]))
                    {
                        // we coalesce everything in the previous region
                        // [i+1, i+regionsize] and construct a basic block for them

                        if (regionsize == 1)
                        {
                            // a small optimization here
                            this.Visit(stmts[i + 1]);
                        }
                        else if (regionsize > 1)
                        {
                            Statement[] tmp = new Statement[regionsize];

                            // copy all statements in the region to tmp
                            int numRealStatements = 0;
                            for (int j = 0; j < regionsize; j++)
                            {
                                tmp[j] = stmts[i + 1 + j];
                                if (tmp[j] != null)
                                    numRealStatements += 1;
                            }

                            if (numRealStatements > 0)
                            {
                                Block b = new Block(new StatementList(tmp));
                                CurrentContinuation = CoalesceBlock(b, CurrentContinuation);
                            }
                        }
                        if (i >= 0) this.Visit(stmts[i]);

                        regionsize = 0;
                    }
                    else
                        regionsize++;
                }
            }
            else
            {
                // NOTE: must visit statements in reverse order
                for (int i = stmts.Count - 1; i >= 0; i--)
                    this.Visit(stmts[i]);
            }

            return stmts;
        }

        public override Node Visit(Node node)
        {
            if (node == null) return null;
            switch (((ZingNodeType)node.NodeType))
            {
                case ZingNodeType.Accept:
                    return this.VisitAccept((AcceptStatement)node);

                case ZingNodeType.Assert:
                    return this.VisitAssert((AssertStatement)node);

                case ZingNodeType.Assume:
                    return this.VisitAssume((AssumeStatement)node);

                case ZingNodeType.Atomic:
                    return this.VisitAtomic((AtomicBlock)node);

                case ZingNodeType.AttributedStatement:
                    return this.VisitAttributedStatement((AttributedStatement)node);

                case ZingNodeType.Event:
                    return this.VisitEventStatement((EventStatement)node);

                case ZingNodeType.Try:
                    return this.VisitZTry((ZTry)node);

                case ZingNodeType.Async:
                    return this.VisitExpressionStatement((ExpressionStatement)node);

                case ZingNodeType.Select:
                    return this.VisitSelect((Select)node);

                case ZingNodeType.Send:
                    return this.VisitSend((SendStatement)node);

                case ZingNodeType.Trace:
                    return this.VisitTrace((TraceStatement)node);

                case ZingNodeType.InvokePlugin:
                    return this.VisitInvokePlugin((InvokePluginStatement)node);

                case ZingNodeType.InvokeSched:
                    return this.VisitInvokeSched((InvokeSchedulerStatement)node);

                case ZingNodeType.Yield:
                    return this.VisitYield((YieldStatement)node);

                default:
                    return base.Visit(node);
            }
        }

        private AssertStatement VisitAssert(AssertStatement assert)
        {
            BasicBlock block = AddBlock(new BasicBlock(assert, CurrentContinuation));
            CurrentContinuation = block;

            return assert;
        }

        private AcceptStatement VisitAccept(AcceptStatement accept)
        {
            BasicBlock block = AddBlock(new BasicBlock(accept, CurrentContinuation));
            CurrentContinuation = block;

            return accept;
        }

        private AssumeStatement VisitAssume(AssumeStatement assume)
        {
            BasicBlock block = AddBlock(new BasicBlock(assume, CurrentContinuation));
            CurrentContinuation = block;

            return assume;
        }

        private EventStatement VisitEventStatement(EventStatement Event)
        {
            BasicBlock block = AddBlock(new BasicBlock(Event, CurrentContinuation));
            CurrentContinuation = block;

            return Event;
        }

        private TraceStatement VisitTrace(TraceStatement trace)
        {
            BasicBlock block = AddBlock(new BasicBlock(trace, CurrentContinuation));
            CurrentContinuation = block;

            return trace;
        }

        private InvokePluginStatement VisitInvokePlugin(InvokePluginStatement InvokePlugin)
        {
            BasicBlock block = AddBlock(new BasicBlock(InvokePlugin, CurrentContinuation));
            CurrentContinuation = block;
            return InvokePlugin;
        }

        private InvokeSchedulerStatement VisitInvokeSched(InvokeSchedulerStatement InvokeSched)
        {
            BasicBlock block = AddBlock(new BasicBlock(InvokeSched, CurrentContinuation));
            CurrentContinuation = block;
            return InvokeSched;
        }

        private static bool IsCoalescableAssignment(AssignmentStatement aStmt)
        {
            if (aStmt == null) return true;
            if (aStmt.Source == null || aStmt.Target == null)
                return false;

            if (aStmt.Source is MethodCall) return false;
            if ((ZingNodeType)aStmt.Source.NodeType == ZingNodeType.Choose)
                return false;
            return true;
        }

        // IsCoalescableExpr -- tests if expression is coalescable
        // Specifically, this function returns false if the expression
        // contains a method call, or a choose statement
        private bool IsCoalescableExpr(Expression e)
        {
            AssignmentExpression aExpr;
            if (e is MethodCall) return false;

            aExpr = e as AssignmentExpression;
            if (aExpr != null)
            {
                AssignmentStatement aStmt;

                aStmt = aExpr.AssignmentStatement as AssignmentStatement;

                return IsCoalescableAssignment(aStmt);
            }
            return true;
        }

        private bool IsCoalescableIfStmt(If s)
        {
            return (IsCoalescableExpr(s.Condition) &&
                    IsCoalescableBlock(s.TrueBlock) &&
                    IsCoalescableBlock(s.FalseBlock));
        }

        private bool IsCoalescableWhileStmt(While s)
        {
            return (IsCoalescableExpr(s.Condition) &&
                    IsCoalescableBlock(s.Body));
        }

        private bool IsCoalescableStmt(Statement s)
        {
            if (s == null) return true;
            switch (s.NodeType)
            {
                case NodeType.Block:
                    return IsCoalescableBlock((Block)s);

                case (NodeType)ZingNodeType.Atomic:
                    return false;

                case NodeType.ExpressionStatement:
                    return IsCoalescableExpr(((ExpressionStatement)s).Expression);

                case NodeType.AssignmentStatement:
                    return IsCoalescableAssignment((AssignmentStatement)s);

                case NodeType.If:
                    return IsCoalescableIfStmt((If)s);

                case NodeType.While:
                    return IsCoalescableWhileStmt((While)s);

                case (NodeType)ZingNodeType.Async:
                    return true;

                default:
                    return false;
            }
        }

        // IsCoalescableBlock -- tests if the block is coalescable
        // Specifically, this function returns false if any statement in
        // the block is not coalescable
        private bool IsCoalescableBlock(Block block)
        {
            StatementList stmts;

            // null block will translates to "nothing" (if it isn't a branch target)
            if (block == null || block.Statements == null || block.Statements.Count == 0)
            {
                if (block != null && splicer.referencedLabels[block.UniqueKey] != null)
                    return false;   // label is referenced - can't coalesce

                return true;
            }

            stmts = block.Statements;

            for (int i = 0; i < stmts.Count; i++)
            {
                if (!IsCoalescableStmt(stmts[i]))
                    return false;
            }
            return true;
        }

        // Transforms block into a single BasicBlock
        private BasicBlock CoalesceBlock(Block block, BasicBlock ctng)
        {
            // We handle:
            //
            //   - Basic blocks of coalescable statements
            //   - Atomic blocks of coalescable statements
            //   - If statements with coalescable statements
            //   - ExpressionStatements w/o MethodCalls and Choose
            //   - AssignmentStatements w/o MethodCalls and Choose
            //
            if (block.NodeType != NodeType.Block)
            {
                StatementList stmts = block.Statements;
                int len = stmts.Count, i;

                for (i = 0; i < len; i++)
                    if (stmts[i] != null)
                        break;

                if (i < len)
                    block = new Block(stmts, stmts[i].SourceContext);
                else
                    block = new Block(stmts, block.SourceContext);
            }
            return AddBlock(new BasicBlock((Statement)block, ctng));
        }

        private AtomicBlock VisitAtomic(AtomicBlock atomic)
        {
            Debug.Assert(!insideAtomicBlock);

            // Create a block to cause the current atomic block to end. The atomicity
            // level brings us to this block atomically. We do nothing here, but our
            // atomicity level goes back to zero, so we get a state transition at the
            // end of this empty block.
            BasicBlock exitBlock = AddBlock(new BasicBlock(null, CurrentContinuation));
            CurrentContinuation = exitBlock;

            insideAtomicBlock = true;
            this.VisitBlock((Block)atomic);
            insideAtomicBlock = false;

            // The following test is needed in case the atomic block does not have any statements
            // in it. In that case, CurrentContinuation is the block following the atomic block.
            if (CurrentContinuation.RelativeAtomicLevel > 0)
            {
                CurrentContinuation.IsAtomicEntry = true;
            }

            return atomic;
        }

        public override Statement VisitLabeledStatement(LabeledStatement lStatement)
        {
            if (lStatement == null) return null;

            BasicBlock savedCC = CurrentContinuation;
            this.Visit(lStatement.Statement);

            // If this is a branch target, make sure we build a separate BB for it.
            if (splicer.referencedLabels[lStatement.UniqueKey] != null)
            {
                BasicBlock bbTarget = (BasicBlock)branchTargets[lStatement.UniqueKey];

                if (bbTarget == null)
                {
                    bbTarget = new BasicBlock(null);
                    AddBlock(bbTarget);
                    bbTarget.MiddleOfTransition = true;
                    branchTargets[lStatement.UniqueKey] = bbTarget;
                }

                // fix for the "Label:; atomic { ... }" problem
                if (savedCC == CurrentContinuation &&
                    ((insideAtomicBlock && (savedCC.RelativeAtomicLevel == 0)) ||
                    (!insideAtomicBlock && (savedCC.RelativeAtomicLevel > 0))))
                {
                    // create a new basic block with the correct
                    // atomiticity level
                    CurrentContinuation = AddBlock(new BasicBlock(null, CurrentContinuation));
                }

                // Link this target block onto the front of the current chain
                bbTarget.UnconditionalTarget = CurrentContinuation;
                CurrentContinuation = bbTarget;
            }

            splicer.AddBlockLabel(CurrentContinuation, lStatement.Label.Name.Substring(3));

            return lStatement;
        }

        private AttributedStatement VisitAttributedStatement(AttributedStatement attributedStmt)
        {
            // Process our associated statement. Then, attach our attributes to the
            // current continuation so we can expose them through the runtime.

            this.Visit(attributedStmt.Statement);
            CurrentContinuation.Attributes = attributedStmt.Attributes;
            return attributedStmt;
        }

        public override Statement VisitBranch(Branch branch)
        {
            if (branch == null) return null;
            if (branch.Condition != null)
                throw new InvalidOperationException("Unexpected branch condition in BBSplitter");

            // Check to see if a basic block has been created for this target yet
            BasicBlock bbTarget = (BasicBlock)branchTargets[branch.Target.UniqueKey];

            // If not, create one now and register it in branchTargets
            if (bbTarget == null)
            {
                bbTarget = new BasicBlock(null);
                AddBlock(bbTarget);
                bbTarget.SourceContext = branch.SourceContext;
                bbTarget.MiddleOfTransition = true;
                branchTargets[branch.Target.UniqueKey] = bbTarget;
            }

#if false
// Note: this optimization was overly aggressive because it assumes that branch targets
// fall within the current atomic block. If not, then we need an extra "branch" block
// which can be the end of the atomic block.

            if (this.insideAtomicBlock)
            {
                // We don't need a basic block for the branch itself. We just redirect the
                // CurrentContinuation to the target of the branch.

                CurrentContinuation = bbTarget;
            }
            else
#endif
            {
                // If we aren't in an atomic block, we can't optimize away the block for the
                // goto itself.
                BasicBlock branchBlock = new BasicBlock(null, bbTarget);
                AddBlock(branchBlock);
                branchBlock.SourceContext = branch.SourceContext;
                CurrentContinuation = branchBlock;

                // Remember this branch if it happens to be inside an atomic block. If the
                // target is *not* within the atomic block, then we'll need to do some
                // fixup work later.
                if (this.insideAtomicBlock)
                    atomicBranches.Add(branchBlock);
            }

            return branch;
        }

        public override Statement VisitThrow(Throw Throw)
        {
            Statement setExceptionStmt = Templates.GetStatementTemplate("SetException");
            Debug.Assert(Throw.Expression is Identifier);
            Replacer.Replace(setExceptionStmt, "_exception", Throw.Expression);

            BasicBlock raiseBlock = new BasicBlock(null);
            raiseBlock.SkipNormalizer = true;
            raiseBlock.SourceContext = Throw.SourceContext;
            raiseBlock.MiddleOfTransition = true;
            AddBlock(raiseBlock);

            if (this.CurrentHandlerBlock != null)
            {
                // If we have a handler, pass control locally
                raiseBlock.Statement = setExceptionStmt;
                raiseBlock.UnconditionalTarget = this.CurrentHandlerBlock;
            }
            else
            {
                // No local handler, so propagate outward immediately.
                Block b = new Block(new StatementList());
                b.Statements.Add(setExceptionStmt);
                b.Statements.Add(Templates.GetStatementTemplate("PropagateException"));
                raiseBlock.Statement = b;
                raiseBlock.PropagatesException = true;
            }

            CurrentContinuation = raiseBlock;

            return Throw;
        }

        private ZTry VisitZTry(ZTry Try)
        {
            BasicBlock testerChain = null;

            //
            // Build a sequence of basic blocks to test the thrown exception type
            // and dispatch it to the appropriate handler. Check first to see if
            // a default handler is provided. If not, we need to create one here
            // and fall through to it if no matching handler is found.
            //
            if (Try.Catchers.Length > 0 && Try.Catchers[Try.Catchers.Length - 1].Name != null)
            {
                // No default handler, so we need to put a default of our
                // own in place to rethrow the exception. If we have an
                // outer exception context in this method, then we can
                // simply transfer control there. Otherwise, we need to
                // emit a block to make the runtime pass it up the stack.
                BasicBlock defaultHandler = null;
                if (this.CurrentHandlerBlock != null)
                {
                    // Empty block just transfers control to the outer handler
                    defaultHandler = new BasicBlock(null, this.CurrentHandlerBlock);
                }
                else
                {
                    defaultHandler = new BasicBlock(Templates.GetStatementTemplate("PropagateException"));
                    defaultHandler.PropagatesException = true;
                }
                defaultHandler.MiddleOfTransition = true;
                defaultHandler.SkipNormalizer = true;
                AddBlock(defaultHandler);
                testerChain = defaultHandler;
            }

            //
            // We traverse the list of catchers in reverse order to make
            // sure the default "with" (if any) is handled first, as it's
            // guaranteed (by the parser) to be the last one in the list.
            //
            for (int i = Try.Catchers.Length - 1; i >= 0; i--)
            {
                With with = Try.Catchers[i];

                this.PushContinuationStack();
                this.Visit(with.Block);
                BasicBlock catchBody = this.PopContinuationStack();

                if (!this.insideAtomicBlock)
                {
                    // If we aren't inside an atomic block, then we need an extra block
                    // between the tester and the body of the specific handler. All of
                    // the blocks involved in testing the exception are connected in
                    // an atomic way. If the handler isn't also going to be reached
                    // atomically, then we need an extra block with a non-atomic link
                    // to the body.
                    BasicBlock bodyConnector = new BasicBlock(null, catchBody);
                    AddBlock(bodyConnector);
                    catchBody = bodyConnector;
                }

                BasicBlock catchTester = new BasicBlock(null);

                if (with.Name != null)
                {
                    Expression catchTestExpr = Templates.GetExpressionTemplate("CatchTest");
                    Replacer.Replace(catchTestExpr, "_exception", (Expression)with.Name);
                    catchTester.ConditionalExpression = catchTestExpr;
                    catchTester.ConditionalTarget = catchBody;
                    catchTester.UnconditionalTarget = testerChain;
                }
                else
                {
                    // We're the default catch clause, so no test is necessary.
                    catchTester.UnconditionalTarget = catchBody;
                }
                catchTester.SkipNormalizer = true;
                catchTester.MiddleOfTransition = true;
                AddBlock(catchTester);

                // We just linked the new tester to the front of the tester chain.
                // This tester becomes the new head.
                testerChain = catchTester;
            }

            // All of the catch handlers are in place. Add a statement to the first handler
            // to reset the current handler block. If there's an outer scope, we point there.
            // If not, we point to "None".
            if (testerChain == null)        // could be null if all handlers had syntax errors
                return Try;

            testerChain.Statement = Templates.GetStatementTemplate("SetHandler");
            if (CurrentHandlerBlock != null)
            {
                Replacer.Replace(testerChain.Statement, "_blockName", new Identifier(CurrentHandlerBlock.Name));
                testerChain.handlerTarget = CurrentHandlerBlock;
            }
            else
                Replacer.Replace(testerChain.Statement, "_blockName", new Identifier("None"));

            // Now begin processing the body of the try block. Push the exception context
            // before starting.
            this.PushExceptionContext(testerChain);

            this.Visit(Try.Body);

            // Now create a basic block to establish the current handler as we enter the
            // body of the "try".
            Statement setHandlerStmt = Templates.GetStatementTemplate("SetHandler");
            Replacer.Replace(setHandlerStmt, "_blockName",
                             new Identifier(CurrentHandlerBlock.Name));
            BasicBlock setHandler = new BasicBlock(setHandlerStmt, CurrentContinuation);
            setHandler.MiddleOfTransition = true;
            setHandler.SkipNormalizer = true;
            setHandler.handlerTarget = CurrentHandlerBlock;
            AddBlock(setHandler);
            CurrentContinuation = setHandler;

            this.PopExceptionContext();

            return Try;
        }

        public override Statement VisitGoto(Goto Goto)
        {
            throw new InvalidOperationException("Unexpected Goto node in BBSplitter");
        }

        public override Statement VisitIf(If If)
        {
            if (insideAtomicBlock && IsCoalescableIfStmt(If))
            {
                CurrentContinuation =
                    AddBlock(new BasicBlock(If, CurrentContinuation));
                return If;
            }

            PushContinuationStack();
            this.Visit(If.TrueBlock);
            BasicBlock trueBlock = PopContinuationStack();

            PushContinuationStack();
            this.Visit(If.FalseBlock);
            BasicBlock falseBlock = PopContinuationStack();

            SourceContext savedConditionalContext = If.SourceContext;
            if (If.Condition != null)
                savedConditionalContext = If.Condition.SourceContext;

            // We normalize all of the conditional expressions here to make life
            // easier for "select"
            Normalizer normalizer = new Normalizer(splicer, null, false);

            CurrentContinuation = AddBlock(new BasicBlock(null,
                normalizer.VisitExpression(If.Condition), trueBlock, falseBlock));

            CurrentContinuation.SourceContext = savedConditionalContext;
            if (CurrentContinuation.ConditionalExpression != null)
                CurrentContinuation.ConditionalExpression.SourceContext = savedConditionalContext;

            return If;
        }

        public override Statement VisitWhile(While While)
        {
            if (While == null || While.Condition == null)
                return While;

            if (insideAtomicBlock && IsCoalescableWhileStmt(While))
            {
                CurrentContinuation =
                    AddBlock(new BasicBlock(While, CurrentContinuation));
                return While;
            }

            Normalizer normalizer = new Normalizer(splicer, null, false);
            BasicBlock testBlock = new BasicBlock(null);
            testBlock.SourceContext = While.Condition.SourceContext;

            PushContinuationStack(testBlock);
            this.Visit(While.Body);
            BasicBlock bodyBlock = PopContinuationStack();

            testBlock.ConditionalExpression = normalizer.VisitExpression(While.Condition);
            testBlock.ConditionalTarget = bodyBlock;
            testBlock.UnconditionalTarget = CurrentContinuation;

            testBlock.ConditionalExpression.SourceContext = testBlock.SourceContext;
            CurrentContinuation = AddBlock(testBlock);

            return While;
        }

        public Statement VisitYield(YieldStatement yield)
        {
            if (yield == null) return null;

            BasicBlock block = AddBlock(new BasicBlock(yield, CurrentContinuation));
            block.Yields = true;
            CurrentContinuation = block;

            return yield;
        }

        public override Statement VisitForEach(ForEach forEach)
        {
            Normalizer normalizer = new Normalizer(false);
            Identifier incrVar = new Identifier("____" + forEach.UniqueKey.ToString(CultureInfo.InvariantCulture));

            Expression sourceEnumerable = normalizer.VisitExpression(forEach.SourceEnumerable);

            Statement incrStmt = Templates.GetStatementTemplate("foreachIncrementer");
            Replacer.Replace(incrStmt, "_iterator", incrVar);
            BasicBlock incrBlock = new BasicBlock(incrStmt);
            AddBlock(incrBlock);
            incrBlock.MiddleOfTransition = true;
            incrBlock.SkipNormalizer = true;
            incrBlock.SourceContext = forEach.SourceContext;

            PushContinuationStack(incrBlock);
            this.Visit(forEach.Body);
            BasicBlock bodyBlock = PopContinuationStack();

            Statement derefStmt = Templates.GetStatementTemplate("foreachDeref");
            Replacer.Replace(derefStmt, "_tmpVar",
                             normalizer.VisitExpression(forEach.TargetVariable));
            Replacer.Replace(derefStmt, "_collectionExpr", sourceEnumerable);
            Replacer.Replace(derefStmt, "_collectionType",
                             new Identifier(forEach.SourceEnumerable.Type.FullName));
            Replacer.Replace(derefStmt, "_iterator", incrVar);
            BasicBlock derefBlock = new BasicBlock(derefStmt, bodyBlock);
            AddBlock(derefBlock);
            derefBlock.MiddleOfTransition = true;
            derefBlock.SkipNormalizer = true;

            Expression testExpr = Templates.GetExpressionTemplate("foreachTest");
            Replacer.Replace(testExpr, "_iterator", incrVar);
            Replacer.Replace(testExpr, "_sourceEnumerable", sourceEnumerable);
            BasicBlock testBlock = new BasicBlock(null, testExpr, derefBlock, CurrentContinuation);
            AddBlock(testBlock);
            testBlock.SkipNormalizer = true;

            incrBlock.UnconditionalTarget = testBlock;

            Statement initStmt = Templates.GetStatementTemplate("foreachInit");
            Replacer.Replace(initStmt, "_iterator", incrVar);
            BasicBlock initBlock = new BasicBlock(initStmt, testBlock);
            AddBlock(initBlock);
            initBlock.MiddleOfTransition = true;
            initBlock.SkipNormalizer = true;
            initBlock.SourceContext = forEach.SourceContext;

            CurrentContinuation = initBlock;

            return forEach;
        }

        private Select VisitSelect(Select select)
        {
            if (select == null) return null;
            BasicBlock[] jsBodies = new BasicBlock[select.joinStatementList.Length];

            // Traverse each join statement body, with the current continuation
            // pointing to the statement following the "select". Remember where
            // each join statement body begins so we can wire things up correctly.
            for (int i = 0, n = select.joinStatementList.Length; i < n; i++)
            {
                PushContinuationStack();
                this.Visit(select.joinStatementList[i].statement);

                // We add an extra block in front of the body because all of the transitions
                // within the internal processing of select are atomic. We need for the
                // transition to a join statement body to be appropriate for the context
                // (atomic or not). Having an extra block here is the simplest way to do
                // that. Otherwise, for our tester blocks, we'd need for one outgoing link
                // to be atomic, and the other one not. In the case of a select within an
                // atomic block, these extra blocks should be optimized away at some point.
                jsBodies[i] = new BasicBlock(null, PopContinuationStack());
                jsBodies[i].SourceContext = select.joinStatementList[i].statement.SourceContext;
                AddBlock(jsBodies[i]);
            }

            // For each join statement, create a pair of basic blocks - one to
            // see whether the join statement is runnable, and another to do
            // any "receives" contained in the join pattern list. If there are
            // no receives to be processed, the second block may be omitted.
            //
            // We walk through the list backward to make the wiring easier.
            BasicBlock nextTester = null;

            for (int i = select.joinStatementList.Length - 1; i >= 0; i--)
            {
                JoinStatement js = select.joinStatementList[i];

                if (i == select.joinStatementList.Length - 1)
                {
                    // If we fall through all of the select test blocks, then we must have
                    // reached this select statement from within an atomic block. Otherwise,
                    // we'd know that at least one branch is runnable. In this case, we raise
                    // a well-known exception and report this as an invalid state.
                    nextTester = new BasicBlock(Templates.GetStatementTemplate("InvalidBlockingSelect"));
                    nextTester.SourceContext = select.SourceContext;
                    AddBlock(nextTester);
                    nextTester.SkipNormalizer = true;
                }

                BasicBlock jsTester = new BasicBlock(js);
                AddBlock(jsTester);
                jsTester.MiddleOfTransition = true;
                jsTester.UnconditionalTarget = nextTester;
                nextTester = jsTester;
                jsTester.ConditionalExpression = Templates.GetExpressionTemplate("JoinStatementTester");
                Replacer.Replace(jsTester.ConditionalExpression, "_jsBitMask", (Expression)new Literal((ulong)1 << i, SystemTypes.UInt64));

                bool jsHasReceives = false;
                for (int j = 0, n = js.joinPatternList.Length; j < n; j++)
                {
                    if (js.joinPatternList[j] is ReceivePattern)
                    {
                        jsHasReceives = true;
                        break;
                    }
                }

                //
                // We need an executable block if there are receives to be processed
                // OR if we need to generate any external events OR if there are
                // attributes that we need to deal with on the join statement.
                //
                if (jsHasReceives || select.visible || js.attributes != null)
                {
                    BasicBlock jsReceiver = new BasicBlock(js);
                    AddBlock(jsReceiver);
                    jsReceiver.Attributes = js.Attributes;
                    jsReceiver.MiddleOfTransition = true;
                    jsReceiver.SecondOfTwo = true;
                    jsReceiver.UnconditionalTarget = jsBodies[i];
                    jsTester.ConditionalTarget = jsReceiver;
                }
                else
                    jsTester.ConditionalTarget = jsBodies[i];
            }

            BasicBlock selectBlock = new BasicBlock(Templates.GetStatementTemplate("SelectStatementProlog"));

            if (select.deterministicSelection || select.joinStatementList.Length == 1)
            {
                // For deterministic selection, we fall directly through to the first
                // "tester" block.
                selectBlock.UnconditionalTarget = nextTester;
                selectBlock.MiddleOfTransition = true;
            }
            else
            {
                // For non-deterministic selection, we conditionally branch to a "choice" block if
                // more than one join statement is runnable. Otherwise, we fall through to the first
                // tester.
                BasicBlock choiceHelper = new BasicBlock(Templates.GetStatementTemplate("NDSelectBlock"));
                choiceHelper.UnconditionalTarget = nextTester;
                AddBlock(choiceHelper);
                choiceHelper.MiddleOfTransition = true;
                choiceHelper.SkipNormalizer = true;
                choiceHelper.SourceContext = select.SourceContext;

                selectBlock.UnconditionalTarget = nextTester;
                selectBlock.ConditionalExpression = Templates.GetExpressionTemplate("NDSelectTest");
                selectBlock.ConditionalTarget = choiceHelper;
                selectBlock.MiddleOfTransition = false;
            }

            AddBlock(selectBlock);
            selectBlock.selectStmt = select;
            selectBlock.SkipNormalizer = true;
            // selectBlock.MiddleOfTransition = true;
            selectBlock.SourceContext = select.SourceContext;

            CurrentContinuation = selectBlock;

            return select;
        }

        private SendStatement VisitSend(SendStatement send)
        {
            if (send == null) return null;

            BasicBlock block = AddBlock(new BasicBlock(send, CurrentContinuation));
            CurrentContinuation = block;

            return send;
        }

        public override Statement VisitExpressionStatement(ExpressionStatement statement)
        {
            AssignmentStatement assignmentStatement = null;
            AssignmentExpression assignmentExpr = statement.Expression as AssignmentExpression;
            MethodCall methodCall = statement.Expression as MethodCall;
            UnaryExpression choose = null;

            if (assignmentExpr != null)
            {
                assignmentStatement = assignmentExpr.AssignmentStatement as AssignmentStatement;
                if (assignmentStatement != null && assignmentStatement.Source is MethodCall)
                    methodCall = (MethodCall)assignmentStatement.Source;

                if (assignmentStatement != null && assignmentStatement.Source is UnaryExpression &&
                    assignmentStatement.Source.NodeType == (NodeType)ZingNodeType.Choose)
                    choose = (UnaryExpression)assignmentStatement.Source;
            }

            // Check for statements that require special handling

            // The if-condition changed by Jiri Adamek
            // Reason: the NativeZOM method calls should not be splitted into two blocks,
            //         default handling is used instead
            // The original if-condition:
            // if (choose != null || (methodCall != null && !(statement is AsyncMethodCall)))
            if (choose != null || (methodCall != null && !(statement is AsyncMethodCall))
                && !(((MemberBinding)methodCall.Callee).BoundMember.DeclaringType is NativeZOM))
            {
                BasicBlock returnBlock = AddBlock(new BasicBlock(statement, CurrentContinuation));
                returnBlock.SecondOfTwo = true;
                BasicBlock callBlock = AddBlock(new BasicBlock(statement, returnBlock));
                //TODO: Handle the case where the method is itself tagged atomic here.
                //TODO: Code review this with Tony
                if (methodCall != null)
                {
                    if (returnBlock.UnconditionalTarget.Statement != null)
                        returnBlock.SourceContext = returnBlock.UnconditionalTarget.Statement.SourceContext;
                }
                CurrentContinuation = callBlock;
                return statement;
            }

            // The default scenario...
            BasicBlock block = AddBlock(new BasicBlock(statement, CurrentContinuation));
            CurrentContinuation = block;
            return statement;
        }

        public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
        {
            if (assignment == null) return null;

            BasicBlock block = AddBlock(new BasicBlock(assignment, CurrentContinuation));
            CurrentContinuation = block;

            return assignment;
        }

        public override Statement VisitReturn(Return Return)
        {
            if (Return == null) return null;

            if (insideAtomicBlock)
            {
                // If we're returning from inside an atomic block, we want to
                // have one block that leaves the atomic region (w/ atomic
                // level at zero), and a second block that performs the return.
                // The evaluation of the return expression must happen in the first block.
                // We use MiddleOfTransition to make sure this doesn't create
                // an additional state transition. The extra block is for the
                // benefit of the summarization code in the runtime.
                BasicBlock block = AddBlock(new BasicBlock(null));
                block.RelativeAtomicLevel = 0;

                BasicBlock extraBlock = AddBlock(new BasicBlock(Return, block));
                extraBlock.RelativeAtomicLevel = 0;
                extraBlock.MiddleOfTransition = true;
                CurrentContinuation = extraBlock;
            }
            else
            {
                BasicBlock block = AddBlock(new BasicBlock(Return));
                CurrentContinuation = block;
            }

            return Return;
        }
    }
}