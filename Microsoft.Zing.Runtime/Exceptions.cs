using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace Microsoft.Zing
{

    /// <summary>
    /// This is the base class for all Zing exceptions.
    /// </summary>
    [Serializable]
    public abstract class ZingException : Exception
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }

        protected virtual string ZingMessage { get { return string.Empty; } }

        /// <summary>
        /// Returns a formatted version of the exception for human consumption.
        /// </summary>
        /// <returns>String-formatted version of the exception</returns>
        public sealed override string ToString()
        {
            string zingMessage = this.ZingMessage;

            if (zingMessage != null && zingMessage.Length > 0)
                return string.Format(CultureInfo.CurrentUICulture,
                    "{0}\r\n{1}\r\n{2}", this.ZingMessage, base.ToString(),StackTrace);
            else
                return base.ToString();
        }

        /// <summary>
        /// Returns a Zing backtrace from the point at which the exception
        /// was thrown, if possible.
        /// </summary>

        private string stackTrace = null;
        public override string StackTrace {
            get
            {
                if (stackTrace == null)
                {
                    stackTrace = BuildStackTrace();
                }
                return (stackTrace);
            }
        }
        //private string stackTrace = BuildStackTrace();

        protected int mySerialNumber;
        public int SerialNumber
        {
            get { return mySerialNumber; }
            set { mySerialNumber = value; }
        }

        private string BuildStackTrace()
        {
            //
            // We encounter many exceptions during state-space exploration, so
            // we only want to build a stack trace when we're preparing a Zing
            // trace for viewing. Options.EnableEvents is the best way to tell
            // if that is the case.
            //

            // Abhishek: ZingInvalidEndStateException does not need a stack trace
            if (this is ZingInvalidEndStateException)
            {
                return "";
            }

            ZingSourceContext SourceContext;
            bool IsInnerMostFrame = true;
            if (Options.EnableEvents && Process.LastProcess[mySerialNumber] != null &&
                Process.LastProcess[mySerialNumber].TopOfStack != null)
            {
                StateImpl s = Process.LastProcess[mySerialNumber].StateImpl;
                StringBuilder sb = new StringBuilder();
                string[] sourceFiles = s.GetSourceFiles();

                sb.AppendFormat(CultureInfo.CurrentUICulture, "\r\nStack trace:\r\n");

                for (ZingMethod sf = Process.LastProcess[mySerialNumber].TopOfStack; sf != null ;sf = sf.Caller)
                {
                    if (this is ZingAssertionFailureException && IsInnerMostFrame)
                    {
                        SourceContext = Process.AssertionFailureCtx[mySerialNumber];
                        IsInnerMostFrame = false;
                    }
                    else
                    {
                        SourceContext = sf.Context;
                    }
                    string[] sources = s.GetSources();

                    if (sources != null && SourceContext.DocIndex < sources.Length)
                    {
                        // Translate the column offset to a line offset. This is horribly
                        // inefficient since we don't cache anything, but we do this rarely
                        // so the added complexity isn't worthwhile.
                        string sourceText = sources[SourceContext.DocIndex];
                        string[] sourceLines = sourceText.Split('\n');

                        int line, offset;
                        for (line=0, offset=0; offset < SourceContext.StartColumn && line < sourceLines.Length ;line++)
                            offset += sourceLines[line].Length + 1;

                        sb.AppendFormat(CultureInfo.CurrentUICulture, "    {0} ({1}, Line {2})\r\n",
                            Utils.Unmangle(sf.MethodName),
                            System.IO.Path.GetFileName(sourceFiles[SourceContext.DocIndex]),
                            line);
                    }
                    else
                    {
                        // If we don't have Zing source, just provide the method names
                        sb.AppendFormat(CultureInfo.CurrentUICulture, "    {0}\r\n",
                            Utils.Unmangle(sf.MethodName));
                    }
                }

                return sb.ToString();
            }
            else
                return string.Empty;
        }
        
    }

    /// <summary>
    /// This is a catch-all exception for unexpected failures.
    /// </summary>
    /// <remarks>
    /// This exception is used to wrap any unexpected exceptions that are
    /// thrown during the execution of a Zing model. If this exception is
    /// seen, it almost always indicates a bug in the Zing compiler or runtime.
    /// </remarks>
    [Serializable]
    public class ZingUnexpectedFailureException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnexpectedFailureException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnexpectedFailureException(string message)
            : this(message, null)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnexpectedFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingUnexpectedFailureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }

    /// <summary>
    /// This exception represents a Zing assertion failure.
    /// </summary>
    /// <remarks>
    /// A Zing assertion failure is indicated by a state of type StateType.Error
    /// and an Error property pointing to an instance of this exception. The
    /// exception contains a string representation of the failing predicate. It
    /// also contains the optional failure message, if one was present in the
    /// Zing source code.
    /// </remarks>
    [Serializable]
    public class ZingAssertionFailureException : ZingException
    {
        /// <summary>
        /// Returns (or sets) the string representation of the failing expression.
        /// </summary>
        public string Expression { get { return expression; } set { expression = value; } }
        private string expression;

        /// <summary>
        /// Returns (or sets) the optional comment string, if present.
        /// </summary>
        public string Comment { get { return comment; } set { comment = value; } }
        private string comment;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssertionFailureException()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssertionFailureException(string expression)
        {
            this.Expression = expression;
            this.Comment = string.Empty;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssertionFailureException(string expression, string comment)
        {
            this.Expression = expression;
            this.Comment = comment;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssertionFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingAssertionFailureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture, "Zing Assertion failed:\r\n" +
                    "    Expression: {0}\r\n" +
                    "    Comment: {1}\r\n", Expression, Comment);
            }
        }
    }

    /// <summary>
    /// This exception indicates the failure of a Zing "assume" statement.
    /// </summary>
    /// <remarks>
    /// Zing assume failures throw this exception. We use an exception to propagate
    /// this condition through the runtime, but it isn't considered an error per se
    /// by the object model.
    /// </remarks>
    [Serializable]
    public class ZingAssumeFailureException : ZingException
    {
        /// <summary>
        /// Returns (or sets) the expression whose evaluation returned "false".
        /// </summary>
        public string Expression { get { return expression; } set { expression = value; } }
        private string expression;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssumeFailureException()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssumeFailureException(string expression)
        {
            this.Expression = expression;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingAssumeFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingAssumeFailureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }


        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture, "Zing 'assume' statement failed:\r\n" +
                    "    Expression: {0}\r\n", Expression);
            }
        }
    }

    /// <summary>
    /// This exception indicates an attempt to divide by zero.
    /// </summary>
    [Serializable]
    public class ZingDivideByZeroException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingDivideByZeroException()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingDivideByZeroException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingDivideByZeroException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingDivideByZeroException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture, "Divide by zero in a Zing expression.\r\n");
            }
        }
    }

    /// <summary>
    /// This exception indicates an arithmetic overflow.
    /// </summary>
    [Serializable]
    public class ZingOverflowException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingOverflowException()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingOverflowException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingOverflowException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingOverflowException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture, "Arithmetic overflow in a Zing expression.\r\n");
            }
        }
    }

    /// <summary>
    /// This exception indicates an array index exceeded the bounds of its array.
    /// </summary>
    [Serializable]
    public class ZingIndexOutOfRangeException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingIndexOutOfRangeException()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingIndexOutOfRangeException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingIndexOutOfRangeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingIndexOutOfRangeException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture, "Invalid index in a Zing array reference.\r\n");
            }
        }
    }

    /// <summary>
    /// This exception marks states that are "stuck".
    /// </summary>
    /// <remarks>
    /// This exception isn't ever "thrown", but is attached to states in which no processes
    /// are runnable and one or more of them are not in valid endstates.
    /// </remarks>
    [Serializable]
    public class ZingInvalidEndStateException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidEndStateException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidEndStateException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidEndStateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingInvalidEndStateException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "Deadlock: no processes are runnable and one or more processes is blocked in an invalid end state\r\n");
            }
        }
    }

    /// <summary>
    /// This exception indicates a select statement has blocked within an atomic region
    /// </summary>
    /// <remarks>
    /// This exception marks error states in which a select statement within the body of an
    /// atomic region (i.e. not the first statement in the region) has no runnable join
    /// statements. Only the first statement of an atomic region may be a blocking select.
    /// </remarks>
    [Serializable]
    public class ZingInvalidBlockingSelectException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidBlockingSelectException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidBlockingSelectException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidBlockingSelectException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingInvalidBlockingSelectException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "For Preemptive : A select statement nested within an atomic region in not runnable. " +
                    "Selects within an atomic region may only block if they are the first statement in the region.\r\n" +
                    "For Cooperative : There should be an explicit yield before a select statement that can block\r\n\n");
            }
        }
    }

    /// <summary>
    /// This exception occurs when a Zing exception reaches the bottom of the stack
    /// without finding a handler.
    /// </summary>
    [Serializable]
    public class ZingUnhandledExceptionException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnhandledExceptionException()
        {
            throw new NotImplementedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnhandledExceptionException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnhandledExceptionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingUnhandledExceptionException(int exception)
            : base()
        {
            this.exception = exception;
        }

        /// <summary>
        /// Returns (or sets) the number of the unhandled Zing exception.
        /// </summary>
        public int Exception { get { return exception; } set { exception = value; } }
        private int exception;

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingUnhandledExceptionException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter=true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "ZingUnhandledException ({0})\r\n", exception);
            }
        }
    }

    /// <summary>
    /// Attempting to dereference a null Zing pointer causes this exception to be
    /// thrown.
    /// </summary>
    [Serializable]
    public class ZingNullReferenceException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNullReferenceException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNullReferenceException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNullReferenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingNullReferenceException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "A Zing null pointer was dereferenced.\r\n");
            }
        }
    }

	/// <summary>
	/// Attempting to do summarization with unsupported constructs
	/// </summary>
	[Serializable]
	public class ZingUnsupportedFeatureException : ZingException
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ZingUnsupportedFeatureException()
			: base()
		{
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public ZingUnsupportedFeatureException(string message)
			: base(message)
		{
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public ZingUnsupportedFeatureException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected ZingUnsupportedFeatureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
			: base(serializationInfo, streamingContext)
		{
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override string ZingMessage
		{
			get
			{
				return string.Format(CultureInfo.CurrentUICulture,
					"Summarization is currently not supported for sets, channels and symbolic datatypes.\r\n");
			}
		}
	}

    /// <summary>
    /// This exception is thrown for choose statements without any possible choices.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when a Zing "choose" operator finds that no choices are possible.
    /// This could occur, for example, if choose is applied to an empty set.
    /// </remarks>
    [Serializable]
    public class ZingInvalidChooseException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidChooseException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidChooseException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInvalidChooseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingInvalidChooseException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "A choose operator was executed with no choices possible.\r\n");
            }
        }
    }

    /// <summary>
    /// This exception is thrown when non-determinism is encountered while running a predicate method
    /// </summary>
    /// <remarks>
    /// A predicate method is one that returns bool and is used within a "wait" condition. Non-determinism
    /// cannot be permitted in this context for obvious reasons.
    /// </remarks>
    [Serializable]
    public class ZingNondeterministicPredicateException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNondeterministicPredicateException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNondeterministicPredicateException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNondeterministicPredicateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingNondeterministicPredicateException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "Non-determinism was encountered while executing a predicate method\r\n");
            }
        }
    }

    /// <summary>
    /// This exception is thrown when an exception occurs while running a predicate method
    /// </summary>
    /// <remarks>
    /// A predicate method is one that returns bool and is used within a "wait" condition.
    /// </remarks>
    [Serializable]
    public class ZingPredicateExceptionException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingPredicateExceptionException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingPredicateExceptionException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingPredicateExceptionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingPredicateExceptionException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "An unhandled or uncatchable exception occurred while executing a predicate method. Inner exception:\r\n{0}",
                    this.InnerException);
            }
        }
    }

    /// <summary>
    /// This exception is thrown when we encounter a predicate-style wait condition while
    /// executing a predicate method.
    /// </summary>
    /// <remarks>
    /// A predicate method is one that returns bool and is used within a "wait" condition.
    /// </remarks>
    [Serializable]
    public class ZingNestedPredicateException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNestedPredicateException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNestedPredicateException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingNestedPredicateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingNestedPredicateException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "A select statement containing a predicate method call was encountered while executing a predicate method\r\n");
            }
        }
    }


    [Serializable]
    public class ZingerDFSStackOverFlow : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingerDFSStackOverFlow ()
            : base()
        {

        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingerDFSStackOverFlow (string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingerDFSStackOverFlow (string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingerDFSStackOverFlow (SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "The length of zing Depth First Search Stack exceeded the cutoff\r\n");
            }
        }
    }

    /// <summary>
    /// This exception is thrown when we execute more than 10000 basic blocks in a single
    /// state transition.
    /// </summary>
    /// <remarks>
    /// This exception typically indicates an infinite loop within an atomic block.
    /// </remarks>
    [Serializable]
    public class ZingInfiniteLoopException : ZingException
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInfiniteLoopException()
            : base()
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInfiniteLoopException(string message)
            : base(message)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ZingInfiniteLoopException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected ZingInfiniteLoopException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "The Zing model appears to have encountered an infinite loop within an atomic block\r\n");
            }
        }
    }
}