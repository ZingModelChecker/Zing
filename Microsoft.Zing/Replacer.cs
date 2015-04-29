using System;
using System.Compiler;

namespace Microsoft.Zing
{
    /// <summary>
    /// This class aids in code generation by making substitutions in a given tree. It will
    /// be extended as needed to deal with additional types of substitution. See the static
    /// methods on the class for a description of what is supported.
    /// </summary>
    public class Replacer : System.Compiler.StandardVisitor
    {
        private enum ReplaceType
        {
            Identifier,         // Replace one identifier with another
            LabeledStatement    // Replace a labeled statement with a block
        };

        private ReplaceType replaceType;

        private Identifier oldName;
        private Node newNode;

        // used to be private
        internal Replacer(Identifier oldName, Node newNode)
        {
            this.oldName = oldName;
            this.newNode = newNode;

            if (newNode is Expression)
                replaceType = ReplaceType.Identifier;
            else if (newNode is Block)
                replaceType = ReplaceType.LabeledStatement;
            else
                throw new ArgumentException("Replacer: newNode must be Expression or Block");
        }

        /// <summary>
        /// Within "node", replace occurrences of identifiers matching oldName (by
        /// value) with the given newNode. newNode must be an expression.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="oldName"></param>
        /// <param name="newNode"></param>
        public static void Replace(Node node, Identifier oldName, Node newNode)
        {
            if (!(newNode is Expression))
                throw new ArgumentException("Replace: newNode must be an Expression");

            Replacer replacer = new Replacer(oldName, newNode);
            replacer.Visit(node);
        }

        /// <summary>
        /// Within "node", replace identifiers matching the value of oldName with
        /// the given newNode (of type Expression).
        /// </summary>
        /// <param name="node"></param>
        /// <param name="oldName"></param>
        /// <param name="newNode"></param>
        public static void Replace(Node node, string oldName, Node newNode)
        {
            Replace(node, new Identifier(oldName), newNode);
        }

        /// <summary>
        /// Within "node", find a labeled statement whose label matches the given
        /// string, and replace it with the supplied statement block.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="labelName"></param>
        /// <param name="block"></param>
        public static void Replace(Node node, string labelName, Block block)
        {
            Replacer replacer = new Replacer(new Identifier(labelName), block);
            replacer.Visit(node);
        }

        public override Node VisitUnknownNodeType(Node node)
        {
            if (node == null) return null;

            if (node.NodeType == NodeType.TypeExpression)
                return this.VisitTypeExpression((TypeExpression)node);

            return base.VisitUnknownNodeType(node);
        }

        // Methods that perform substitution...

        public override Expression VisitIdentifier(Identifier identifier)
        {
            if (identifier == null) return null;

            if (replaceType == ReplaceType.Identifier && identifier.Name == oldName.Name)
                return (Expression)newNode;

            return identifier;
        }

        public override Statement VisitLabeledStatement(LabeledStatement lStatement)
        {
            if (lStatement == null) return null;

            if (replaceType == ReplaceType.LabeledStatement && lStatement.Label.Name == oldName.Name)
                return (Statement)newNode;

            lStatement.Statement = (Statement)this.Visit(lStatement.Statement);
            return lStatement;
        }

        // Methods that fix traversal issues in StandardVisitor...

        public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier)
        {
            if (qualifiedIdentifier == null) return null;
            qualifiedIdentifier.Identifier = (Identifier)this.VisitIdentifier(qualifiedIdentifier.Identifier);
            qualifiedIdentifier.Qualifier = this.VisitExpression(qualifiedIdentifier.Qualifier);
            return qualifiedIdentifier;
        }

        public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations)
        {
            if (localDeclarations == null) return null;
            localDeclarations.Type = this.VisitTypeReference(localDeclarations.Type);
            LocalDeclarationList decls = localDeclarations.Declarations;
            if (decls != null)
            {
                int n = decls.Count;
                LocalDeclarationList newDecls = localDeclarations.Declarations = new LocalDeclarationList(n);
                for (int i = 0; i < n; i++)
                    newDecls.Add(this.VisitLocalDeclaration(decls[i]));
            }
            return localDeclarations;
        }

        public override LocalDeclaration VisitLocalDeclaration(LocalDeclaration localDeclaration)
        {
            if (localDeclaration == null) return null;
            localDeclaration.InitialValue = this.VisitExpression(localDeclaration.InitialValue);
            localDeclaration.Name = (Identifier)this.VisitIdentifier(localDeclaration.Name);
            return localDeclaration;
        }

        public override TypeNode VisitTypeReference(TypeNode type)
        {
            if (type == null) return null;
            switch (type.NodeType)
            {
                case NodeType.ArrayType:
                case NodeType.ArrayTypeExpression:
                    ArrayType arrType = (ArrayType)type;
                    TypeNode elemType = this.VisitTypeReference(arrType.ElementType);
                    if (elemType == arrType.ElementType) return arrType;
                    return elemType.GetArrayType(arrType.Rank, arrType.Sizes, arrType.LowerBounds);

                case NodeType.Pointer:
                    System.Compiler.Pointer pType = (System.Compiler.Pointer)type;
                    elemType = this.VisitTypeReference(pType.ElementType);
                    if (elemType == pType.ElementType) return pType;
                    return elemType.GetPointerType();

                case NodeType.Reference:
                    Reference rType = (Reference)type;
                    elemType = this.VisitTypeReference(rType.ElementType);
                    if (elemType == rType.ElementType) return rType;
                    return elemType.GetReferenceType();
                //TODO: other parameterized types, such as constrained types
                case NodeType.ClassExpression:
                    ClassExpression cExpr = (ClassExpression)type;
                    cExpr.Expression = this.VisitExpression(cExpr.Expression);
                    cExpr.TemplateArguments = this.VisitTypeReferenceList(cExpr.TemplateArguments);
                    return cExpr;

                case NodeType.InterfaceExpression:
                    InterfaceExpression iExpr = (InterfaceExpression)type;
                    iExpr.Expression = this.VisitExpression(iExpr.Expression);
                    iExpr.TemplateArguments = this.VisitTypeReferenceList(iExpr.TemplateArguments);
                    return iExpr;

                case NodeType.TypeExpression:
                    TypeExpression tExpr = (TypeExpression)type;
                    tExpr.Expression = this.VisitExpression(tExpr.Expression);
                    tExpr.TemplateArguments = this.VisitTypeReferenceList(tExpr.TemplateArguments);
                    return tExpr;
                //TODO: handle array expresssions, etc.
            }
            return type;
        }

        public override TypeNode VisitTypeNode(TypeNode typeNode)
        {
            if (typeNode == null) return null;
            TypeNode result = base.VisitTypeNode(typeNode);
            result.Name = (Identifier)this.VisitIdentifier(typeNode.Name);

            TypeExpression te = result as TypeExpression;
            if (te != null)
                te.Expression = this.VisitExpression(te.Expression);
            return result;
        }

        public TypeExpression VisitTypeExpression(TypeExpression typeExpression)
        {
            typeExpression.Expression = this.VisitExpression(typeExpression.Expression);
            return typeExpression;
        }

        public override Expression VisitMemberBinding(MemberBinding memberBinding)
        {
            if (memberBinding == null) return null;
            MemberBinding result = (MemberBinding)base.VisitMemberBinding(memberBinding);
            //
            // Check the bound member to avoid examining the predefined types.
            //
            if (result.BoundMember.NodeType != NodeType.Struct)
                result.BoundMember = (Member)this.Visit(result.BoundMember);
            return result;
        }

        public override AttributeList VisitAttributeList(AttributeList attributes)
        {
            // This avoids an infinite recursion related to attribute constructors.
            // We never need to perform a replacement in an attribute, so this is fine,
            // although there's no doubt a more correct way of avoiding the recursion.
            return attributes;
        }

        public override Expression VisitConstruct(Construct cons)
        {
            if (cons == null) return null;
            Construct result = (Construct)base.VisitConstruct(cons);
            result.Constructor.Type = (TypeNode)this.Visit(result.Constructor.Type);
            return result;
        }
    }
}