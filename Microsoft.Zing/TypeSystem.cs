using System;
using System.Compiler;

namespace Microsoft.Zing
{
    internal class TypeSystem : System.Compiler.TypeSystem
    {
        internal TypeSystem(ErrorHandler errorHandler)
            :base(errorHandler)
        {
        }

        //
        // We add an implicit conversion from "object" to any of our heap-allocated
        // types.
        //
        // We also permit implicit conversions between "int" and "byte".
        //
        // TODO: We may need to construct a more elaborate expression here for the
        // conversion to permit the runtime to make the appropriate checks.
        //
        public override bool ImplicitCoercionFromTo(Expression source, TypeNode t1, TypeNode t2)
        {
            if (t1 == SystemTypes.Object)
            {
                if (t2 is Chan || t2 is Set || t2 is ZArray || t2 is Class)
                    return true;
                else
                    return false;
            }

            if (t2 == SystemTypes.Object)
            {
                if (t1 is Chan || t1 is Set || t1 is ZArray || t1 is Class)
                    return true;
                else
                    return false;
            }

            if (t1 == SystemTypes.Int32 && t2 == SystemTypes.UInt8)
                return true;

            if (t1 == SystemTypes.UInt8 && t2 == SystemTypes.Int32)
                return true;

            return base.ImplicitCoercionFromTo (source, t1, t2);
        }

        public override Expression ImplicitCoercion(Expression source, TypeNode targetType, TypeViewer typeViewer)
        { // LJW: added third parameter "typeViewer" so we override the correct thing
            
            if (source == null || source.Type == null || targetType == null)
                return null;

            if (source.Type is EnumNode && targetType == SystemTypes.Int32)
            {
                return source;
            }
             
            if (source.Type == SystemTypes.Object)
            {
                if (targetType is Chan || targetType is Set || targetType is ZArray || targetType is Class)
                    return source;
                else
                {
                    this.HandleError(source, System.Compiler.Error.NoImplicitCoercion, "object", targetType.FullName);
                    return null;
                }
            }

            if (targetType == SystemTypes.Object)
            {
                if (!(source.Type is Chan || source.Type is Set || source.Type is ZArray || source.Type is Class))
                {
                    this.HandleError(source, System.Compiler.Error.NoImplicitCoercion, source.Type.FullName, "object");
                    return null;
                }
            }

            if (source.Type == SystemTypes.Int32 && targetType == SystemTypes.UInt8)
            {
                BinaryExpression binExpr = new BinaryExpression(source, new MemberBinding(null, SystemTypes.UInt8),
                    NodeType.Castclass, source.SourceContext);

                binExpr.Type = SystemTypes.UInt8;

                return binExpr;
            }

            if (source.Type == SystemTypes.UInt8 && targetType == SystemTypes.Int32)
            {
                BinaryExpression binExpr =  new BinaryExpression(source, new MemberBinding(null, SystemTypes.Int32),
                    NodeType.Castclass, source.SourceContext);

                binExpr.Type = SystemTypes.Int32;

                return binExpr;
            }

            return base.ImplicitCoercion (source, targetType, typeViewer);
        }


#if false
        public override Expression ExplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType)
        {
            if (this.suppressOverflowCheck || !sourceType.IsPrimitiveInteger || !targetType.IsPrimitiveInteger)
                return this.ExplicitCoercion(lit, targetType);
            else
                return this.ImplicitLiteralCoercion(lit, sourceType, targetType, true);
        }    
        public override Literal ImplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType)
        {
            return this.ImplicitLiteralCoercion(lit, sourceType, targetType, false);
        }
        Literal ImplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType, bool explicitCoercion)
        {
            if (sourceType == targetType) return lit;
            object val = lit.Value;
            EnumNode eN = targetType as EnumNode;
            if (eN != null)
            {
                if (val is int && ((int)val) == 0)
                {
                    if (eN.UnderlyingType == SystemTypes.Int64 || eN.UnderlyingType == SystemTypes.UInt64) val = 0L;
                    return new Literal(val, eN, lit.SourceContext);
                }
                goto error;
            }
            if (targetType.TypeCode == TypeCode.Boolean)
            {
                this.HandleError(lit, Error.ConstOutOfRange, lit.SourceContext.SourceText, "bool");
                lit.SourceContext.Document = null;
                return null;
            }
            if (targetType.TypeCode == TypeCode.String)
            {
                if (val != null || lit.Type != SystemTypes.Object)
                {
                    this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
                    lit.SourceContext.Document = null;
                    return null;
                }
                return lit;
            }
            if (targetType.TypeCode == TypeCode.Char || sourceType.TypeCode == TypeCode.Boolean || sourceType.TypeCode == TypeCode.Decimal) goto error;
            switch (sourceType.TypeCode)
            {
                case TypeCode.Double:
                switch (targetType.TypeCode)
                {
                    case TypeCode.Single: this.HandleError(lit, Error.LiteralDoubleCast, "float", "F"); return lit;
                    case TypeCode.Decimal: this.HandleError(lit, Error.LiteralDoubleCast, "decimal", "M"); return lit;
                    default: 
                        this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
                        lit.SourceContext.Document = null;
                        return null;
                }
                case TypeCode.Single:
                switch (targetType.TypeCode)
                {
                    case TypeCode.Double: break;
                    default: 
                        this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
                        lit.SourceContext.Document = null;
                        return null;
                }
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                switch (targetType.TypeCode)
                {
                    case TypeCode.Int64: 
                    case TypeCode.UInt64:
                    case TypeCode.Decimal:
                    case TypeCode.Single:
                    case TypeCode.Double:
                        break;
                    default: 
                        if (explicitCoercion) break;
                        this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
                        lit.SourceContext.Document = null;
                        return null;
                }
                    break;
            }
            try
            {
                if (val == null)
                {
                    if (targetType.IsValueType) goto error;
                }
                else
                    val = System.Convert.ChangeType(val, targetType.TypeCode);
                return new Literal(val, targetType);
            }
            catch(InvalidCastException)
            {
            }
            catch(OverflowException)
            {
            }
            catch(FormatException){}
            error:
                if (sourceType.IsPrimitiveNumeric && lit.SourceContext.Document != null)
                {
                    Error e = Error.ConstOutOfRange;
                    if (explicitCoercion) e = Error.ConstOutOfRangeChecked;
                    this.HandleError(lit, e, lit.SourceContext.SourceText, this.GetTypeName(targetType));
                }
                else
                    this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
            lit.SourceContext.Document = null;
            return null;
        }
        public override bool ImplicitLiteralCoercionFromTo(Literal lit, TypeNode sourceType, TypeNode targetType)
        {
            if (lit == null) return false;
            if (sourceType == targetType) return true;
            object val = lit.Value;
            if (targetType is EnumNode)
            {
                if (val is int && ((int)val) == 0)
                    return true;
                return false;
            }
            if (targetType.TypeCode == TypeCode.Boolean) return false;
            if (targetType.TypeCode == TypeCode.String)
            {
                if (val != null || lit.Type != SystemTypes.Object)
                    return false;
                return true;
            }
            if (targetType.TypeCode == TypeCode.Char || sourceType.TypeCode == TypeCode.Boolean || sourceType.TypeCode == TypeCode.Decimal) return false;
            switch (sourceType.TypeCode)
            {
                case TypeCode.Double:
                    return false;
                case TypeCode.Single:
                switch (targetType.TypeCode)
                {
                    case TypeCode.Double: return true;
                    default: return false;
                }
                case TypeCode.Int64:
                case TypeCode.UInt64:
                switch (targetType.TypeCode)
                {
                    case TypeCode.Int64: 
                    case TypeCode.UInt64:
                    case TypeCode.Decimal:
                    case TypeCode.Single:
                    case TypeCode.Double:
                        break;
                    default: 
                        return false;
                }
                    break;
            }
            try
            {
                if (val == null)
                {
                    if (targetType.IsValueType) return false;
                }
                else
                    val = System.Convert.ChangeType(val, targetType.TypeCode);
                return true;
            }
            catch(InvalidCastException)
            {
            }
            catch(OverflowException)
            {
            }
            catch(FormatException){}
            return false;
        }
        public override bool IsVoid(TypeNode type)
        {
            return (type == SystemTypes.Void || type == Runtime.AsyncClass);
        }
        public override string GetTypeName(TypeNode type)
        {
            if (this.ErrorHandler == null){return "";}
            ((ErrorHandler)this.ErrorHandler).currentParameter = this.currentParameter;
            return this.ErrorHandler.GetTypeName(type);
        }
        private void HandleError(Node offendingNode, Error error, params string[] messageParameters)
        {
            if (this.ErrorHandler == null) return;
            ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, error, messageParameters);
        }
#endif
    }
}
