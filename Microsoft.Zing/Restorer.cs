using System;
using System.Collections;
using System.Compiler;
using System.Diagnostics;

namespace Microsoft.Zing
{
    /// <summary>
    /// Patches the type nodes in an IR tree to refer to the given target module.
    /// </summary>
    internal sealed class Restorer : System.Compiler.StandardVisitor
    {
        public Restorer(Module targetModule)
            : base()
        {
            this.targetModule = targetModule;
        }

        private Module targetModule;

        public override Node Visit(Node node)
        {
            if (node == null) return null;
          
            TypeNode tn = node as TypeNode;

            if (tn != null)
                tn.DeclaringModule = targetModule;

            return base.Visit(node);
        }

        public override Node VisitUnknownNodeType(Node node)
        {
            // wrwg+sriram 20/7/04: review 
            return node;
        }

    }
}
