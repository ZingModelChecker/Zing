using System;
using System.Compiler;
using System.Runtime.InteropServices;

namespace Microsoft.Zing
{
    [Guid("6398952D-5CC9-4bb4-B026-7A5C53FA9D41")]
    internal class DebuggerLanguage{}

#if !ReducedFootprint
/* LJW
    [ComVisible(true), Guid("55A681C2-CD89-4961-BC58-0804BF3B27BE")]
    public class DebuggerEE : Microsoft.VisualStudio.IntegrationHelper.BaseExpressionEvaluator 
    {
        public DebuggerEE() 
        {
            this.cciEvaluator.ExprCompiler = new Microsoft.Zing.Compiler();
            this.cciEvaluator.ExprErrorHandler = new Microsoft.Zing.ErrorHandler(new ErrorNodeList());
        }
    }
*/
#endif
}
