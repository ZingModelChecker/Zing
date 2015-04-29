using System.Collections.Generic;
using System.Compiler;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Zing
{
    // Code added by Jiri Adamek to support native ZOM classes

    public class NativeZOM : Class
    {
        public NativeZOM()
            : base()
        {
        }
    }

    // END of added code

    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    public enum ZingNodeType
    {
        Range = 5000,
        Chan,
        Set,
        Array,
        Choose,         // used as the NodeType in UnaryExpression
        In,             // used as the NodeType in BinaryExpression (for set membership)

        Accept,
        Assert,
        Atomic,
        Async,
        Assume,
        AttributedStatement,
        Event,
        Send,
        Self,
        Select,
        JoinStatement,
        WaitPattern,
        ReceivePattern,
        TimeoutPattern,
        EventPattern,
        InvokePlugin,
        InvokeSched,
        Trace,
        Try,
        With,
        Yield,

        BasicBlock,
    }

    /*
     *  New types
     */

    [SuppressMessage("Microsoft.Maintainability", "CA1501:AvoidExcessiveInheritance")]
    public class Range : ConstrainedType
    {
        private Expression min;

        public Expression Min
        {
            get { return this.min; }
            set { this.min = value; }
        }

        private Expression max;

        public Expression Max
        {
            get { return this.max; }
            set { this.max = value; }
        }

        public Range()
            : base(SystemTypes.Int32, null)
        {
            this.NodeType = (NodeType)ZingNodeType.Range;
            this.Flags = TypeFlags.Public | TypeFlags.Sealed;
        }

        public Range(Expression min, Expression max)
            : base(SystemTypes.Int32, null)
        {
            this.NodeType = (NodeType)ZingNodeType.Range;
            this.Flags = TypeFlags.Public | TypeFlags.Sealed;
            this.Min = min;
            this.Max = max;
        }

        public override bool IsPrimitiveComparable
        {
            get { return true; }
        }
    }

    [SuppressMessage("Microsoft.Maintainability", "CA1501:AvoidExcessiveInheritance")]
    public class Chan : TypeAlias
    {
        public Chan()
        {
            this.NodeType = (NodeType)ZingNodeType.Chan;
        }

        public Chan(TypeNode channelType)
            : this()
        {
            this.ChannelType = channelType;
        }

        public TypeNode ChannelType
        {
            // We use aliasedType here rather than AliasedType because the getter
            // for AliasedType has some logic that we don't want for the case
            // where aliasedType is null.
            get { return this.aliasedType; }
            set { this.aliasedType = value; }
        }

        public override bool IsPrimitiveComparable
        {
            get { return true; }
        }
    }

    [SuppressMessage("Microsoft.Maintainability", "CA1501:AvoidExcessiveInheritance")]
    public class Set : TypeAlias
    {
        public Set()
        {
            this.NodeType = (NodeType)ZingNodeType.Set;
        }

        public Set(TypeNode setType)
            : this()
        {
            this.SetType = setType;
        }

        public TypeNode SetType
        {
            // We use aliasedType here rather than AliasedType because the getter
            // for AliasedType has some logic that we don't want for the case
            // where aliasedType is null.
            get { return this.aliasedType; }
            set { this.aliasedType = value; }
        }

        public override bool IsPrimitiveComparable
        {
            get { return true; }
        }
    }

    [SuppressMessage("Microsoft.Maintainability", "CA1501:AvoidExcessiveInheritance")]
    public class ZArray : ArrayTypeExpression
    {
        internal TypeNode domainType;     // if size is given as a type

        public TypeNode DomainType
        {
            get { return this.domainType; }
            set { this.domainType = value; }
        }

        public bool IsDynamic { get { return this.Sizes == null; } }

        public ZArray()
        {
            this.NodeType = (NodeType)ZingNodeType.Array;
            this.domainType = SystemTypes.Int32;
            this.Rank = 1;
            this.LowerBounds = new int[] { 0 };
        }

        public ZArray(TypeNode elementType, int[] sizes)
            : this()
        {
            this.ElementType = elementType;
            this.Sizes = sizes;
        }

        public override bool IsPrimitiveComparable
        {
            get { return true; }
        }
    }

    public class ZMethod : Method
    {
        private bool isAtomic;

        public bool Atomic
        {
            get { return this.isAtomic; }
            set { this.isAtomic = value; }
        }

        private bool isActivated;

        public bool Activated
        {
            get { return this.isActivated; }
            set { this.isActivated = value; }
        }

        private List<Field> localVars = new List<Field>();

        public List<Field> LocalVars
        {
            get { return this.localVars; }
        }

        // For use by the duplicator
        internal void ResetLocals()
        {
            localVars = new List<Field>();
        }

        public ZMethod()
        {
        }

        public ZMethod(TypeNode declaringType, AttributeList attributes, Identifier name, ParameterList parameters, TypeNode returnType, Block body)
            : base(declaringType, attributes, name, parameters, returnType, body)
        {
        }

        public ZMethod(TypeNode declaringType, bool atomic, bool activated, Identifier name, ParameterList parameters, TypeNode returnType, Block body)
            : base(declaringType, null, name, parameters, returnType, body)
        {
            this.isActivated = activated;
            this.isAtomic = atomic;

            if (activated)
                this.Flags |= MethodFlags.Static;
        }
    }

    /*
     * New statements
     */

    public class AtomicBlock : Block
    {
        public AtomicBlock()
        {
            this.NodeType = (NodeType)ZingNodeType.Atomic;
        }

        public AtomicBlock(StatementList statements)
            : base(statements)
        {
            this.NodeType = (NodeType)ZingNodeType.Atomic;
        }

        public AtomicBlock(StatementList statements, SourceContext sourceContext)
            : base(statements, sourceContext)
        {
            this.NodeType = (NodeType)ZingNodeType.Atomic;
        }
    }

    public class AssertStatement : Statement
    {
        internal Expression booleanExpr;

        public Expression Expression
        {
            get { return this.booleanExpr; }
            set { this.booleanExpr = value; }
        }

        private string comment;

        public string Comment
        {
            get { return this.comment; }
            set { this.comment = value; }
        }

        public AssertStatement()
            : base((NodeType)ZingNodeType.Assert) { }

        public AssertStatement(Expression booleanExpr, string comment)
            : this()
        {
            this.booleanExpr = booleanExpr;
            this.Comment = comment;
        }

        public AssertStatement(Expression booleanExpr, string comment, SourceContext sourceContext)
            : base((NodeType)ZingNodeType.Assert)
        {
            this.booleanExpr = booleanExpr;
            this.Comment = comment;
            this.SourceContext = sourceContext;
        }
    }

    public class AcceptStatement : Statement
    {
        internal Expression booleanExpr;

        public Expression Expression
        {
            get { return this.booleanExpr; }
            set { this.booleanExpr = value; }
        }

        public AcceptStatement()
            : base((NodeType)ZingNodeType.Accept) { }

        public AcceptStatement(Expression booleanExpr)
            : this()
        {
            this.booleanExpr = booleanExpr;
        }

        public AcceptStatement(Expression booleanExpr, SourceContext sourceContext)
            : base((NodeType)ZingNodeType.Accept)
        {
            this.booleanExpr = booleanExpr;
            this.SourceContext = sourceContext;
        }
    }

    public class EventStatement : Statement
    {
        internal Expression channelNumber;

        public Expression ChannelNumber
        {
            get { return this.channelNumber; }
            set { this.channelNumber = value; }
        }

        internal Expression messageType;

        public Expression MessageType
        {
            get { return this.messageType; }
            set { this.messageType = value; }
        }

        internal Expression direction;

        public Expression Direction
        {
            get { return this.direction; }
            set { this.direction = value; }
        }

        public EventStatement()
            : base((NodeType)ZingNodeType.Event) { }

        public EventStatement(Expression channelNumber, Expression messageType, Expression direction)
            : this()
        {
            this.channelNumber = channelNumber;
            this.messageType = messageType;
            this.direction = direction;
        }

        public EventStatement(Expression messageType, Expression direction)
            : this(new Literal(0, SystemTypes.Int32), messageType, direction)
        {
        }

        public EventStatement(Expression messageType, Expression direction, SourceContext sourceContext)
            : this(messageType, direction)
        {
            this.SourceContext = sourceContext;
        }

        public EventStatement(Expression channelNumber, Expression messageType, Expression direction, SourceContext sourceContext)
            : this(channelNumber, messageType, direction)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class AssumeStatement : Statement
    {
        internal Expression booleanExpr;

        public Expression Expression
        {
            get { return this.booleanExpr; }
            set { this.booleanExpr = value; }
        }

        public AssumeStatement()
            : base((NodeType)ZingNodeType.Assume) { }

        public AssumeStatement(Expression booleanExpr)
            : this()
        {
            this.booleanExpr = booleanExpr;
        }

        public AssumeStatement(Expression booleanExpr, SourceContext sourceContext)
            : this(booleanExpr)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class YieldStatement : Statement
    {
        public YieldStatement()
            : base((NodeType)ZingNodeType.Yield) { }

        public YieldStatement(SourceContext sourceContext)
            : this()
        {
            this.SourceContext = sourceContext;
        }
    }

    public class SelfExpression : Expression
    {
        public SelfExpression()
            : base((NodeType)ZingNodeType.Self)
        {
            this.NodeType = (NodeType)ZingNodeType.Self;
            this.Type = SystemTypes.Int32;
        }

        public SelfExpression(SourceContext sourceContext)
            : this()
        {
            this.SourceContext = sourceContext;
        }
    }

    public class AttributedStatement : Statement
    {
        private AttributeList attributes;

        public AttributeList Attributes
        {
            get { return this.attributes; }
            set { this.attributes = value; }
        }

        private Statement statement;

        public Statement Statement
        {
            get { return this.statement; }
            set { this.statement = value; }
        }

        public AttributedStatement()
            : base((NodeType)ZingNodeType.AttributedStatement) { }

        public AttributedStatement(AttributeList attributes)
            : this()
        {
            this.Attributes = attributes;
        }

        public AttributedStatement(AttributeList attributes, Statement statement)
            : this()
        {
            this.Attributes = attributes;
            this.Statement = statement;
        }

        public AttributedStatement(AttributeNode attr, Statement statement)
            : this()
        {
            this.Attributes = new AttributeList(1);
            this.Attributes.Add(attr);
            this.Statement = statement;
        }
    }

    public class TraceStatement : Statement
    {
        private ExpressionList operands;

        public ExpressionList Operands
        {
            get { return this.operands; }
            set { this.operands = value; }
        }

        public TraceStatement()
            : base((NodeType)ZingNodeType.Trace) { }

        public TraceStatement(ExpressionList operands)
            : this()
        {
            this.operands = operands;
        }

        public TraceStatement(ExpressionList operands, SourceContext sourceContext)
            : this(operands)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class InvokePluginStatement : Statement
    {
        private ExpressionList operands;

        public ExpressionList Operands
        {
            get { return this.operands; }
            set { this.operands = value; }
        }

        public InvokePluginStatement()
            : base((NodeType)ZingNodeType.InvokePlugin) { }

        public InvokePluginStatement(ExpressionList operands)
            : this()
        {
            this.operands = operands;
        }

        public InvokePluginStatement(ExpressionList operands, SourceContext sourceContext)
            : this(operands)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class InvokeSchedulerStatement : Statement
    {
        private ExpressionList operands;

        public ExpressionList Operands
        {
            get { return this.operands; }
            set { this.operands = value; }
        }

        public InvokeSchedulerStatement()
            : base((NodeType)ZingNodeType.InvokeSched) { }

        public InvokeSchedulerStatement(ExpressionList operands)
            : this()
        {
            this.operands = operands;
        }

        public InvokeSchedulerStatement(ExpressionList operands, SourceContext sourceContext)
            : this(operands)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class AsyncMethodCall : ExpressionStatement
    {
        public AsyncMethodCall()
        {
            this.NodeType = (NodeType)ZingNodeType.Async;
        }

        public AsyncMethodCall(Expression callee, ExpressionList arguments)
            : this()
        {
            this.Expression = new MethodCall(callee, arguments);
        }

        public AsyncMethodCall(Expression callee, ExpressionList arguments, SourceContext sourceContext)
            : this(callee, arguments)
        {
            this.SourceContext = sourceContext;
            this.Expression.SourceContext = sourceContext;
        }

        public Expression Callee
        {
            get
            {
                Debug.Assert(this.Expression is MethodCall);
                return ((MethodCall)this.Expression).Callee;
            }
            set
            {
                Debug.Assert(this.Expression is MethodCall);
                ((MethodCall)this.Expression).Callee = value;
            }
        }

        public ExpressionList Operands
        {
            get
            {
                Debug.Assert(this.Expression is MethodCall);
                return ((MethodCall)this.Expression).Operands;
            }
            set
            {
                Debug.Assert(this.Expression is MethodCall);
                ((MethodCall)this.Expression).Operands = value;
            }
        }
    }

    public class SendStatement : Statement
    {
        internal Expression channel;

        public Expression Channel
        {
            get { return this.channel; }
            set { this.channel = value; }
        }

        internal Expression data;

        public Expression Data
        {
            get { return this.data; }
            set { this.data = value; }
        }

        public SendStatement()
            : base((NodeType)ZingNodeType.Send) { }

        public SendStatement(Expression channel, Expression data)
            : this()
        {
            this.channel = channel;
            this.data = data;
        }

        public SendStatement(Expression channel, Expression data, SourceContext sourceContext)
            : this(channel, data)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class Select : Statement
    {
        internal bool validEndState;

        public bool ValidEndState
        {
            get { return this.validEndState; }
            set { this.validEndState = value; }
        }

        internal bool deterministicSelection;

        public bool DeterministicSelection
        {
            get { return this.deterministicSelection; }
            set { this.deterministicSelection = value; }
        }

        internal bool visible;

        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        internal JoinStatementList joinStatementList;

        public JoinStatementList JoinStatements
        {
            get { return this.joinStatementList; }
            set { this.joinStatementList = value; }
        }

        public Select()
            : base((NodeType)ZingNodeType.Select) { }

        public Select(bool endState, bool deterministic, JoinStatementList joinStatementList)
            : this(endState, deterministic, false, joinStatementList)
        {
        }

        public Select(bool endState, bool deterministic, JoinStatementList joinStatementList, SourceContext sourceContext)
            : this(endState, deterministic, joinStatementList)
        {
            this.SourceContext = sourceContext;
        }

        public Select(bool endState, bool deterministic, bool visible, JoinStatementList joinStatementList)
            : this()
        {
            this.validEndState = endState;
            this.deterministicSelection = deterministic;
            this.visible = visible;
            this.joinStatementList = joinStatementList;
        }

        public Select(bool endState, bool deterministic, bool visible, JoinStatementList joinStatementList, SourceContext sourceContext)
            : this(endState, deterministic, visible, joinStatementList)
        {
            this.SourceContext = sourceContext;
        }
    }

    public class JoinStatement : Statement
    {
        internal AttributeList attributes;

        public AttributeList Attributes
        {
            get { return this.attributes; }
            set { this.attributes = value; }
        }

        internal Statement statement;

        public Statement Statement
        {
            get { return this.statement; }
            set { this.statement = value; }
        }

        internal JoinPatternList joinPatternList;

        public JoinPatternList JoinPatterns
        {
            get { return this.joinPatternList; }
            set { this.joinPatternList = value; }
        }

        public JoinStatement()
            : base((NodeType)ZingNodeType.JoinStatement) { }

        public JoinStatement(JoinPatternList joinPatternList, Statement statement)
            : this()
        {
            this.joinPatternList = joinPatternList;
            this.statement = statement;
        }

        public JoinStatement(JoinPatternList joinPatternList, Statement statement, SourceContext sourceContext)
            : this(joinPatternList, statement)
        {
            this.SourceContext = sourceContext;
        }

        internal bool visible;  // propagated from the select statement by the looker
    }

    abstract public class JoinPattern : Node
    {
        protected JoinPattern(ZingNodeType nodeType)
            : base((NodeType)nodeType) { }
    }

    public class TimeoutPattern : JoinPattern
    {
        public TimeoutPattern()
            : base(ZingNodeType.TimeoutPattern) { }
    }

    public class WaitPattern : JoinPattern
    {
        internal Expression expression;

        public Expression Expression
        {
            get { return this.expression; }
            set { this.expression = value; }
        }

        public WaitPattern()
            : base(ZingNodeType.WaitPattern) { }

        public WaitPattern(Expression expression)
            : this()
        {
            this.expression = expression;
        }
    }

    public class ReceivePattern : JoinPattern
    {
        internal Expression channel;

        public Expression Channel
        {
            get { return this.channel; }
            set { this.channel = value; }
        }

        internal Expression data;

        public Expression Data
        {
            get { return this.data; }
            set { this.data = value; }
        }

        public ReceivePattern()
            : base(ZingNodeType.ReceivePattern) { }

        public ReceivePattern(Expression channel, Expression data)
            : this()
        {
            this.channel = channel;
            this.data = data;
        }
    }

    public class EventPattern : JoinPattern
    {
        internal Expression channelNumber;

        public Expression ChannelNumber
        {
            get { return this.channelNumber; }
            set { this.channelNumber = value; }
        }

        internal Expression messageType;

        public Expression MessageType
        {
            get { return this.messageType; }
            set { this.messageType = value; }
        }

        internal Expression direction;

        public Expression Direction
        {
            get { return this.direction; }
            set { this.direction = value; }
        }

        public EventPattern()
            : base(ZingNodeType.EventPattern) { }

        public EventPattern(Expression messageType, Expression direction)
            : this(new Literal(0, SystemTypes.Int32), messageType, direction)
        {
        }

        public EventPattern(Expression channelNumber, Expression messageType, Expression direction)
            : this()
        {
            this.channelNumber = channelNumber;
            this.messageType = messageType;
            this.direction = direction;
        }
    }

    public class ZTry : Statement
    {
        private WithList catchers;

        public WithList Catchers
        {
            get { return this.catchers; }
            set { this.catchers = value; }
        }

        private Block body;

        public Block Body
        {
            get { return this.body; }
            set { this.body = value; }
        }

        public ZTry()
            : base((NodeType)ZingNodeType.Try)
        {
        }

        public ZTry(Block body, WithList catchers)
            : this()
        {
            this.body = body;
            this.catchers = catchers;
        }
    }

    public class With : Statement
    {
        internal Block Block;

        public Block Body
        {
            get { return this.Block; }
            set { this.Block = value; }
        }

        private Identifier name;

        public Identifier Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public With()
            : base((NodeType)ZingNodeType.With)
        {
        }

        public With(Identifier name, Block block)
            : this()
        {
            this.Block = block;
            this.Name = name;
        }
    }

    public sealed class JoinStatementList
    {
        private JoinStatement[] elements = new JoinStatement[16];
        private int length;

        public JoinStatementList()
        {
            this.elements = new JoinStatement[16];
        }

        public JoinStatementList(int capacity)
        {
            this.elements = new JoinStatement[capacity];
        }

        public JoinStatementList(params JoinStatement[] elements)
        {
            if (elements == null) elements = new JoinStatement[0];
            this.elements = elements;
            this.length = elements.Length;
        }

        public void Add(JoinStatement element)
        {
            int n = this.elements.Length;
            int i = this.length++;
            if (i == n)
            {
                int m = n * 2; if (m < 16) m = 16;
                JoinStatement[] newElements = new JoinStatement[m];
                for (int j = 0; j < n; j++) newElements[j] = elements[j];
                this.elements = newElements;
            }
            this.elements[i] = element;
        }

        public JoinStatementList Clone()
        {
            int n = this.length;
            JoinStatementList result = new JoinStatementList(n);
            result.length = n;
            JoinStatement[] newElements = result.elements;
            for (int i = 0; i < n; i++)
                newElements[i] = this.elements[i];
            return result;
        }

        public int Length
        {
            get { return this.length; }
        }

        public JoinStatement this[int index]
        {
            get
            {
                return this.elements[index];
            }
            set
            {
                this.elements[index] = value;
            }
        }
    }

    public sealed class JoinPatternList
    {
        private JoinPattern[] elements = new JoinPattern[16];
        private int length;

        public JoinPatternList()
        {
            this.elements = new JoinPattern[16];
        }

        public JoinPatternList(int capacity)
        {
            this.elements = new JoinPattern[capacity];
        }

        public JoinPatternList(params JoinPattern[] elements)
        {
            if (elements == null) elements = new JoinPattern[0];
            this.elements = elements;
            this.length = elements.Length;
        }

        public void Add(JoinPattern element)
        {
            int n = this.elements.Length;
            int i = this.length++;
            if (i == n)
            {
                int m = n * 2; if (m < 16) m = 16;
                JoinPattern[] newElements = new JoinPattern[m];
                for (int j = 0; j < n; j++) newElements[j] = elements[j];
                this.elements = newElements;
            }
            this.elements[i] = element;
        }

        public JoinPatternList Clone()
        {
            int n = this.length;
            JoinPatternList result = new JoinPatternList(n);
            result.length = n;
            JoinPattern[] newElements = result.elements;
            for (int i = 0; i < n; i++)
                newElements[i] = this.elements[i];
            return result;
        }

        public int Length
        {
            get { return this.length; }
        }

        public JoinPattern this[int index]
        {
            get
            {
                return this.elements[index];
            }
            set
            {
                this.elements[index] = value;
            }
        }
    }

    public sealed class WithList
    {
        private With[] elements = new With[16];
        private int length;

        public WithList()
        {
            this.elements = new With[16];
        }

        public WithList(int capacity)
        {
            this.elements = new With[capacity];
        }

        public WithList(params With[] elements)
        {
            if (elements == null) elements = new With[0];
            this.elements = elements;
            this.length = elements.Length;
        }

        public void Add(With element)
        {
            int n = this.elements.Length;
            int i = this.length++;
            if (i == n)
            {
                int m = n * 2; if (m < 16) m = 16;
                With[] newElements = new With[m];
                for (int j = 0; j < n; j++) newElements[j] = elements[j];
                this.elements = newElements;
            }
            this.elements[i] = element;
        }

        public WithList Clone()
        {
            int n = this.length;
            WithList result = new WithList(n);
            result.length = n;
            With[] newElements = result.elements;
            for (int i = 0; i < n; i++)
                newElements[i] = this.elements[i];
            return result;
        }

        public int Length
        {
            get { return this.length; }
        }

        public With this[int index]
        {
            get
            {
                return this.elements[index];
            }
            set
            {
                this.elements[index] = value;
            }
        }
    }

    internal class BasicBlock : Statement
    {
        internal Statement Statement;
        internal Scope Scope;

        // If the block corresponds to the start of a select statement, this gives us a
        // reference to the select statement itself.
        internal Select selectStmt;

        internal Expression ConditionalExpression;
        internal BasicBlock ConditionalTarget;
        internal BasicBlock UnconditionalTarget;

        internal AttributeList Attributes;     // for attributed statements

        internal BasicBlock handlerTarget;

        internal int RelativeAtomicLevel;
        internal bool IsAtomicEntry;
        internal bool MiddleOfTransition;
        internal bool Yields;

        // EndsAtomicBlock was used for 2 purposes:
        //      1) Transition Delimiter
        //      2) Transaction Delimiter
        // We use MiddleOfTransition for the 1st purpose
        //    and {Enter,Exit}AtomicScope for the 2nd purpose
        // Precise list of places EndAtomicBlock used as
        // Transition Delimiter:
        //   1) when target of a branch points to a null block (VisitBlock)
        //   2) ditto (VisitBranch)
        //   3) raiseBlock (visitThrow)
        //   4) defaultHandler, catchTester, setHandler (VisitZtry)
        //   5) incrBlock, derefBlock, initBlock (VisitForEach)
        //   6) jsTester, jsReceivers (VisitSelect)
        //   7) choiceHelper, selectBlock
        //   8) callBlock
        // internal bool EndsAtomicBlock;
        internal bool IsEntryPoint;

        internal bool SecondOfTwo;
        internal bool PropagatesException;
        internal bool SkipNormalizer;
        internal int Id;

        internal BasicBlock(Statement statement, Expression conditionalExpression, BasicBlock conditionalTarget
            , BasicBlock unconditionalTarget)
            : base((NodeType)ZingNodeType.BasicBlock)
        {
            this.Id = -1;

            this.Statement = statement;
            ConditionalExpression = conditionalExpression;
            ConditionalTarget = conditionalTarget;
            UnconditionalTarget = unconditionalTarget;
        }

        internal BasicBlock(Statement statement)
            : this(statement, null, null, null)
        {
        }

        internal BasicBlock(Statement statement, BasicBlock unconditionalTarget)
            : this(statement, null, null, unconditionalTarget)
        {
        }

        internal bool IsReturn
        {
            get
            {
                return ConditionalTarget == null && UnconditionalTarget == null && !PropagatesException;
            }
        }

        internal string Name
        {
            get
            {
                if (IsEntryPoint)
                    return "Enter";
                else
                    return "B" + Id.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}