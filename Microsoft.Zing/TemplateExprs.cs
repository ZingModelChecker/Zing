namespace Microsoft.Zing
{
	public class ExpressionTemplates
	{
        int NativeZOMCallFirstArgument = p;
        
        int NativeZOMCallee = ((_TypeName)(application.LookupObject(_Pointer)))._MethodName;

        int NativeZOMStaticCall = _ClassName._MethodName;
                                
        int PtrAllocation = application.Allocate(new Z.Application._Constructor(application));
        
		int InitializerPtrAllocation = application.Allocate(new Z.Application._Constructor(application));

        int NativeZOMPtrAllocation = application.Allocate(new Z._Constructor(application));

        int InitializerNativeZOMPtrAllocation = application.Allocate(new Z._Constructor(application));

		int ArrayPtrAllocation = application.Allocate(new Z.Application._Constructor(application, _size));

		int InitializerArrayPtrAllocation = application.Allocate(new Z.Application._Constructor(application, _size));

		int GlobalFieldAccess = application.globals._fieldName;

		int StructFieldAccess = (_structExpr)._fieldName;

        int SelfAccess = (int)p.Id;

		int ClassFieldAccess = ((Z.Application._objectType) application.LookupObject(_ptrExpr))._fieldName;

		int IndexerAccess = ((Z.Application._arrayType) application.LookupObject(_ptrExpr))[_indexExpr];

		int ThisFieldAccess  = ((Z.Application._objectType) application.LookupObject(This))._fieldName;

		int ReceivePatternPredicate = ((Z.Application._chanType) application.LookupObject(_chanExpr)).CanReceive;

		int EnumTypeRestoration = ((_enumType) _enumValue);

        int IntTypeRestoration = ((int) _enumValue);

		int CatchTest = CurrentException == (int) Exceptions._exception;

		int Sizeof = ((ZingCollectionType) application.LookupObject(_sizeofOperand)).Count;

		int JoinStatementTester = (this.SavedRunnableJoinStatements & _jsBitMask) != 0ul;

		int JoinStatementRunnableBit = (_jsRunnableBoolExpr ? _jsBitMask : 0ul);

		int NDSelectTest = application.SetPendingSelectChoices(p, SavedRunnableJoinStatements);

		int SetMembershipTest = ((ZingSet) application.LookupObject(_setOperand)).IsMember(_itemOperand);

		int foreachTest = locals._iterator < ((ZingCollectionType) application.LookupObject(_sourceEnumerable)).Count;

		int SimpleFieldRef = this._fieldName;
	
		int GetFieldInfo = typeof(_class).GetField(_fieldName);

        int ContextAttributeConstructor = new Z.Attributes._AttributeName();
    }
}