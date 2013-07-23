using System;
using System.Compiler;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Zing
{
    [SuppressMessage("Microsoft.Naming", "CA1712:DoNotPrefixEnumValuesWithTypeName")]
    public enum Error
    {
        None = 0,

        AtomicBlockInAtomicMethod = 2000,
        AtomicBlockNested,
        BadDirectivePlacement,
        BadEmbeddedStmt,
        BooleanExpressionRequired,
        DuplicateModifier,
        DuplicateNameInNS,
        EmbeddedChoose,
        EmbeddedMethodCall,
        EmptyCharConst,
        EndifDirectiveExpected,
        EndOfPPLineExpected,
        EndRegionDirectiveExpected,
        ErrorDirective,
        FloatOverflow,
        ExpectedChannelType,
        ExpectedCommaOrRightParen,
        ExpectedComplexType,
        ExpectedDoubleQuote,
        ExpectedExceptionHandler,
        ExpectedIdentifier,
        ExpectedJoinPattern,
        ExpectedJoinPatternSeparator,
        ExpectedJoinStatement,
        ExpectedLeftBrace,
        ExpectedLeftBracket,
        ExpectedMethodCall,
        ExpectedPeriod,
        ExpectedRightBrace,
        ExpectedRightBraceOrJoinPattern,
        ExpectedRightBracket,
        ExpectedRightParenthesis,
        ExpectedSemicolon,
        ExpectedSetType,
        ExpectedSingleQuote,
        ExpectedStringLiteral,
        ExpectedParameterlessMethod,
        ExpectedStaticMethod,
        ExpectedVoidMethod,
        ExpectedPluginDllName,
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly")]
        _unusedErrorCode5,
        TraceExpectedArguments,
        InvokePluginExpectedArguments,
        IllegalEscape,
        IllegalYieldInAtomicBlock,
        IllegalStatement,
        IncompatibleSetTypes,
        IncompatibleSetOperand,
        InternalCompilerError,
        InternalCompilerWarning,
        IntOverflow,
        InvalidAsyncCallTarget,
        InvalidEventPattern,
        InvalidExprTerm,
        InvalidForeachSource,
        InvalidForeachTargetType,
        InvalidLineNumber,
        InvalidModifier,
        InvalidPreprocExpr,
        InvalidStructFieldInitializer,
        InvalidChoiceType,
        InvalidChoiceExpr,
        InvalidExceptionExpression,
        InvalidIndexType,
        InvalidMessageType,
        InvalidSetAssignment,
        InvalidSetExpression,
        InvalidUseOfSymbolicFunction,
        InvalidSizeofOperand,
        InvalidSymbolicType,
        IntegerExpressionRequired,
        LowercaseEllSuffix,
        MissingPPFile,
        MultipleDefaultHandlers,
        NamedArgumentExpected,
        NewlineInConst,
        NoCommentEnd,
        NoVoidField,
        NoVoidParameter,
        NumericLiteralTooLarge,
        PossibleMistakenNullStatement,
        PPDirectiveExpected,
        PPDefFollowsToken,
        RelatedErrorLocation,
        SyntaxError,
        TimeoutNotAlone,
        TooManyTimeouts,
        TooManyCharsInConst,
        TypeExpected,
        UnescapedSingleQuote,
        TooManyJoinStatements,
        UnexpectedDirective,
        UnexpectedSymbolicType,
        UnexpectedToken,
        UnexpectedVoidType,
        WarningDirective,
        InvalidSymbolicAssignment,
        InvalidPredicateReturnType,
        UnexpectedPredicateOutputParameter,
    }

    internal class ZingErrorNode : System.Compiler.ErrorNode
    {
        private static System.Resources.ResourceManager resourceManager;

        public ZingErrorNode(Error code, params string[] messageParameters)
            : base((int)code, messageParameters)
        {
        }
        public override string GetMessage(System.Globalization.CultureInfo culture)
        {
            if (ZingErrorNode.resourceManager == null)
                ZingErrorNode.resourceManager = new System.Resources.ResourceManager("Microsoft.Zing.ErrorMessages", typeof(ZingErrorNode).Module.Assembly);
            return this.GetMessage(((Error)this.Code).ToString(), ZingErrorNode.resourceManager, culture);
        }
        public override int Severity
        {
            get
            {
                switch ((Error)this.Code)
                {
                    case Error.InvalidAsyncCallTarget: return 2;    // temporary - for Sling's benefit
                    case Error.WarningDirective: return 2;
                    case Error.InternalCompilerWarning: return 2;
                }
                return 0;
            }
        }
    }

    internal class ErrorHandler : System.Compiler.ErrorHandler
    {
        public ErrorHandler(System.Compiler.ErrorNodeList errors)
            : base(errors)
        {
        }

        public override string GetUnqualifiedMemberSignature(System.Compiler.Member mem)
        {
            if (mem.IsSpecialName || mem.DeclaringType is Range)
                return null;

            return base.GetUnqualifiedMemberSignature (mem);
        }

        //
        // We override this so we can return null for methods we don't want to
        // show in the editor drop-down list.
        //
        public override string GetUnqualifiedMethodSignature(System.Compiler.Method method, bool noAccessor)
        {
            if (method.HasCompilerGeneratedSignature)
                return null;

            return base.GetUnqualifiedMethodSignature (method, noAccessor);
        }

        public override string GetMemberAccessString(System.Compiler.Member mem)
        {
            return string.Empty;
        }

        public override string GetInstanceMemberSignature(Member mem)
        {
            Field f = mem as Field;
            Method m = mem as Method;
            TypeNode mType = f != null ? f.Type : m != null ? m.ReturnType : null;
            string mName = mem.Name.ToString();
            return this.GetTypeName(mType) + " this." + mName;
        }

        public override string GetTypeName(System.Compiler.TypeNode type)
        {
            if (type == null)
                return "?";

            if (type.TypeCode == TypeCode.Boolean)
                return "bool";
            else if (type.TypeCode == TypeCode.Empty)
                return "void";
            else if (type.TypeCode == TypeCode.Int32)
                return "int";
            else
                return base.GetTypeName (type);
        }
    }
}
