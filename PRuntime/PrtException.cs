using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.Zing;

namespace P.PRuntime
{
    /// <summary>
    /// This is the base class for all Prt exceptions.
    /// </summary>
    [Serializable]
    public abstract class PrtException : Exception
    {
        protected PrtException()
            : base()
        {
        }

        protected PrtException(string message)
            : base(message)
        {
        }

        protected PrtException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
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

        public override string StackTrace
        {
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

            //PrtDeadlockException does not need a stack trace
            if (this is PrtDeadlockException)
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
                    if (this is PrtAssertFailureException && IsInnerMostFrame)
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
                        for (line = 0, offset = 0; offset < SourceContext.StartColumn && line < sourceLines.Length; line++)
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

    public class PrtIllegalEnqueueException : PrtException
    {
        public PrtIllegalEnqueueException()
            : base()
        {
        }

        public PrtIllegalEnqueueException(string message)
            : this(message, null)
        {
        }

        public PrtIllegalEnqueueException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtIllegalEnqueueException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }

    public class PrtInhabitsTypeException : PrtException
    {
        public PrtInhabitsTypeException()
            : base()
        {
        }

        public PrtInhabitsTypeException(string message)
            : this(message, null)
        {
        }

        public PrtInhabitsTypeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtInhabitsTypeException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }

    public class PrtMaxBufferSizeExceededException : PrtException
    {
        public PrtMaxBufferSizeExceededException()
            : base()
        {
        }

        public PrtMaxBufferSizeExceededException(string message)
            : this(message, null)
        {
        }

        public PrtMaxBufferSizeExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtMaxBufferSizeExceededException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }

    public class PrtInternalException : PrtException
    {
        public PrtInternalException()
            : base()
        {
        }

        public PrtInternalException(string message)
            : this(message, null)
        {
        }

        public PrtInternalException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtInternalException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }

    public class PrtDeadlockException : PrtException
    {
        public PrtDeadlockException()
            : base()
        {
        }

        public PrtDeadlockException(string message)
            : this(message, null)
        {
        }

        public PrtDeadlockException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtDeadlockException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }

    public class PrtUnhandledEventException : PrtException
    {
        public PrtUnhandledEventException()
            : base()
        {
        }

        public PrtUnhandledEventException(string message)
            : this(message, null)
        {
        }

        public PrtUnhandledEventException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtUnhandledEventException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }

    public class PrtApplicationException : PrtException
    {
        public PrtApplicationException()
            : base()
        {
        }

        public PrtApplicationException(string message)
            : this(message, null)
        {
        }

        public PrtApplicationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtApplicationException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }

    public class PrtAssertFailureException : PrtException
    {
        public PrtAssertFailureException()
            : base()
        {
        }

        public PrtAssertFailureException(string message)
            : this(message, null)
        {
        }

        public PrtAssertFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtAssertFailureException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }

    public class PrtMaxEventInstancesExceededException : PrtException
    {
        public PrtMaxEventInstancesExceededException()
            : base()
        {
        }

        public PrtMaxEventInstancesExceededException(string message)
            : this(message, null)
        {
        }

        public PrtMaxEventInstancesExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected PrtMaxEventInstancesExceededException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

    }
}