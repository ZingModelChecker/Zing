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
        protected ZingException()
            : base()
        {
        }

        protected ZingException(string message)
            : base(message)
        {
        }

        protected ZingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ZingException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

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
                    "{0}\r\n{1}\r\n", this.ZingMessage, StackTrace);
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

        /// <summary>
        /// Useful in the case of multithreaded execution of Zinger
        /// </summary>
        public int myThreadId;

        private string BuildStackTrace()
        {
            //
            // We encounter many exceptions during state-space exploration, so
            // we only want to build a stack trace when we're preparing a Zing
            // trace for viewing. Options.ExecuteTraceStatements is the best way to tell
            // if that is the case.
            //

            //ZingInvalidEndStateException does not need a stack trace
            if (this is ZingInvalidEndStateException)
            {
                return "";
            }

            ZingSourceContext SourceContext;
            bool IsInnerMostFrame = true;
            if (ZingerConfiguration.ExecuteTraceStatements && Process.LastProcess[myThreadId] != null &&
                Process.LastProcess[myThreadId].TopOfStack != null)
            {
                StateImpl s = Process.LastProcess[myThreadId].StateImpl;
                StringBuilder sb = new StringBuilder();
                string[] sourceFiles = s.GetSourceFiles();

                sb.AppendFormat(CultureInfo.CurrentUICulture, "\r\nStack trace:\r\n");

                for (ZingMethod sf = Process.LastProcess[myThreadId].TopOfStack; sf != null; sf = sf.Caller)
                {
                    if (this is ZingAssertionFailureException && IsInnerMostFrame)
                    {
                        SourceContext = Process.AssertionFailureCtx[myThreadId];
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
        
        public ZingUnexpectedFailureException()
            : base()
        {
        }

        
        public ZingUnexpectedFailureException(string message)
            : this(message, null)
        {
        }

        
        public ZingUnexpectedFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
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

        
        public ZingAssertionFailureException()
        {
        }

        
        public ZingAssertionFailureException(string expression)
        {
            this.Expression = expression;
            this.Comment = string.Empty;
        }

        
        public ZingAssertionFailureException(string expression, string comment)
        {
            this.Expression = expression;
            this.Comment = comment;
        }

        
        public ZingAssertionFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingAssertionFailureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }

        
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

        
        public ZingAssumeFailureException()
        {
        }

        
        public ZingAssumeFailureException(string expression)
        {
            this.Expression = expression;
        }

        
        public ZingAssumeFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingAssumeFailureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }


        
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
        
        public ZingDivideByZeroException()
        {
        }

        
        public ZingDivideByZeroException(string message)
            : base(message)
        {
        }

        
        public ZingDivideByZeroException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingDivideByZeroException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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
        
        public ZingOverflowException()
        {
        }

        
        public ZingOverflowException(string message)
            : base(message)
        {
        }

        
        public ZingOverflowException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingOverflowException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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
        
        public ZingIndexOutOfRangeException()
        {
        }

        
        public ZingIndexOutOfRangeException(string message)
            : base(message)
        {
        }

        
        public ZingIndexOutOfRangeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingIndexOutOfRangeException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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
        
        public ZingInvalidEndStateException()
            : base()
        {
        }

        
        public ZingInvalidEndStateException(string message)
            : base(message)
        {
        }

        
        public ZingInvalidEndStateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingInvalidEndStateException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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
        
        public ZingInvalidBlockingSelectException()
            : base()
        {
        }

        
        public ZingInvalidBlockingSelectException(string message)
            : base(message)
        {
        }

        
        public ZingInvalidBlockingSelectException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingInvalidBlockingSelectException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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
        
        public ZingUnhandledExceptionException()
        {
            throw new NotImplementedException();
        }

        
        public ZingUnhandledExceptionException(string message)
            : base(message)
        {
        }

        
        public ZingUnhandledExceptionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
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

        
        protected ZingUnhandledExceptionException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter=true)]
        
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData (info, context);
        }

        
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
        
        public ZingNullReferenceException()
            : base()
        {
        }

        
        public ZingNullReferenceException(string message)
            : base(message)
        {
        }

        
        public ZingNullReferenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingNullReferenceException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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
    /// This exception is thrown for choose statements without any possible choices.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when a Zing "choose" operator finds that no choices are possible.
    /// This could occur, for example, if choose is applied to an empty set.
    /// </remarks>
    [Serializable]
    public class ZingInvalidChooseException : ZingException
    {
        
        public ZingInvalidChooseException()
            : base()
        {
        }

        
        public ZingInvalidChooseException(string message)
            : base(message)
        {
        }

        
        public ZingInvalidChooseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingInvalidChooseException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "A choose operator was executed with no choices possible.\r\n");
            }
        }
    }


    [Serializable]
    public class ZingerDFSStackOverFlow : ZingException
    {
        
        public ZingerDFSStackOverFlow ()
            : base()
        {

        }

        
        public ZingerDFSStackOverFlow (string message)
            : base(message)
        {
        }

        
        public ZingerDFSStackOverFlow (string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingerDFSStackOverFlow (SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "The length of Zinger Depth First Search Stack exceeded the cutoff\r\n");
            }
        }
    }

    [Serializable]
    public class ZingerAcceptingCycleFound : ZingException
    {

        public ZingerAcceptingCycleFound()
            : base()
        {

        }


        public ZingerAcceptingCycleFound(string message)
            : base(message)
        {
        }


        public ZingerAcceptingCycleFound(string message, Exception innerException)
            : base(message, innerException)
        {
        }


        protected ZingerAcceptingCycleFound(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }


        protected override string ZingMessage
        {
            get
            {
                return string.Format(CultureInfo.CurrentUICulture,
                    "Accepting Cycle Found\r\n");
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
        
        public ZingInfiniteLoopException()
            : base()
        {
        }

        
        public ZingInfiniteLoopException(string message)
            : base(message)
        {
        }

        
        public ZingInfiniteLoopException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        
        protected ZingInfiniteLoopException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        
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